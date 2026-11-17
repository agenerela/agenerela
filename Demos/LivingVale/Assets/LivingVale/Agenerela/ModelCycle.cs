using System;
using System.Threading;
using System.Threading.Tasks;
using Agenerela.Providers;
using LivingVale.Interaction;
using UnityEngine;

namespace LivingVale.Agenerela
{
    /// <summary>
    /// What the model key (F7) does: closes the open conversation, so no turn crosses models, asks
    /// <see cref="ModelSelector"/> for the next installed model, and puts a provider for it in the F6
    /// switch (<see cref="NpcProviderSwitch.UseModel"/>), so every NPC keeps its agent and memory. Loading and unloading are the provider's own <see cref="IModelLifecycle"/> calls (#198),
    /// which never wait in the decision queue.
    /// </summary>
    /// <remarks>
    /// Plain C#: the bootstrap owns the key and the overlay. One press at a time; a press while one
    /// is still asking the server does nothing. A server that is down or slow only leaves a notice.
    /// What stays the game's is the press debounce: pressing through nine models loads only the one
    /// the key rests on.
    /// </remarks>
    public sealed class ModelCycle
    {
        private readonly ConversationCoordinator coordinator;
        private readonly NpcProviderSwitch providers;
        private readonly ModelSelector selector;
        private readonly Func<string, ILLMProvider> makeProvider;
        private readonly Func<TimeSpan, CancellationToken, Task> delay;
        private CancellationTokenSource settling;

        // The provider whose model the server was last asked to load, which a settled switch
        // unloads, and that model's name; null while an unload is out or after it.
        private ILLMProvider loaded;
        private string loadedModel;

        // The newest settle's unload. Each settle waits for the one before it, so two never read
        // or write what is loaded at once.
        private Task transition = Task.CompletedTask;

        /// <summary>How long the key must rest before the model it landed on is loaded (the press debounce).</summary>
        public static readonly TimeSpan DefaultSettleDelay = TimeSpan.FromSeconds(1);

        /// <param name="providers">The F6 switch, whose model provider a press replaces; the driver stays the same object.</param>
        /// <param name="makeProvider">Makes the provider for a model; null uses a local Ollama (<see cref="AgenerelaNpcDecisionDriver.OllamaFor"/>).</param>
        /// <param name="delay">Waits out the settle delay; null uses <see cref="Task.Delay(TimeSpan, CancellationToken)"/>. Tests replace it.</param>
        public ModelCycle(ConversationCoordinator coordinator, NpcProviderSwitch providers, ModelSelector selector,
            Func<string, ILLMProvider> makeProvider = null, Func<TimeSpan, CancellationToken, Task> delay = null)
        {
            this.coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
            this.providers = providers ?? throw new ArgumentNullException(nameof(providers));
            this.selector = selector ?? throw new ArgumentNullException(nameof(selector));
            this.makeProvider = makeProvider ?? (model => AgenerelaNpcDecisionDriver.OllamaFor(model));
            this.delay = delay ?? Task.Delay;
            loaded = providers.ModelProvider;
            loadedModel = providers.ModelLabel;
        }

        public TimeSpan SettleDelay { get; set; } = DefaultSettleDelay;

        /// <summary>
        /// The newest settle-then-load, for tests and tools: it waits <see cref="SettleDelay"/>,
        /// unloads the model loaded before, then warms the new one up. A later press cancels it.
        /// </summary>
        public Task Loading { get; private set; } = Task.CompletedTask;

        /// <summary>Raised on the caller's context whenever <see cref="Notice"/> changes after a press, as a model starts and finishes loading.</summary>
        public event Action NoticeChanged;

        /// <summary>The NPCs' driver, the same object whatever the model.</summary>
        public AgenerelaNpcDecisionDriver Driver => providers.Driver;

        public ModelSelector Selector => selector;

        /// <summary>The last press's game-authored line, for the overlay; empty when there is nothing to say.</summary>
        public string Notice { get; private set; } = "";

        public bool Busy { get; private set; }

        /// <summary>Raised on the caller's context once a press has finished, changed or not.</summary>
        public event Action Finished;

        /// <summary>
        /// One press of the model key. Returns false when a press is already in progress, or when
        /// <paramref name="ct"/> was cancelled (the owner went away): then nothing changes and
        /// <see cref="Finished"/> is not raised.
        /// </summary>
        public async Task<bool> NextAsync(CancellationToken ct = default)
        {
            if (Busy)
            {
                return false;
            }

            Busy = true;
            bool finished = false;
            try
            {
                coordinator.Close(ConversationCloseReason.Interrupted);

                ModelChoice choice;
                try
                {
                    choice = await selector.NextAsync(ct);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    return false;
                }

                if (ct.IsCancellationRequested)
                {
                    return false;
                }

                finished = true;
                ILLMProvider switchedTo = null;
                if (choice.Changed)
                {
                    // Again, right before the swap: the player may have opened a chat and sent a
                    // line while the server was asked. Closing ends that turn, so its reply is
                    // dropped and no turn starts on one model and lands on another.
                    coordinator.Close(ConversationCloseReason.Interrupted);
                    switchedTo = makeProvider(choice.Model);
                    providers.UseModel(switchedTo, choice.Model);
                }

                Notice = choice.Notice;
                if (switchedTo != null)
                {
                    // Only once the key rests: pressing through nine models must not load nine
                    // models on one card. A newer press cancels this one, warm-up included.
                    Settle(switchedTo, ct);
                }

                return true;
            }
            finally
            {
                Busy = false;
                if (finished)
                {
                    Finished?.Invoke();
                }
            }
        }

        /// <summary>
        /// Loads the model in use now, through the same settle as a press: what F6 calls when the
        /// player turns the model driver back on, and the bootstrap at the start. A model loaded any
        /// other way would be missing from what the next switch unloads.
        /// </summary>
        public void LoadCurrent(CancellationToken ct = default)
        {
            Settle(providers.ModelProvider, ct);
        }

        private void Settle(ILLMProvider next, CancellationToken ct)
        {
            settling?.Cancel();
            settling?.Dispose();
            settling = CancellationTokenSource.CreateLinkedTokenSource(ct);
            Loading = SettleThenLoad(next, providers.ModelLabel, settling.Token);
        }

        private bool Superseded(ILLMProvider next, CancellationToken token) =>
            token.IsCancellationRequested || !ReferenceEquals(next, providers.ModelProvider);

        private async Task SettleThenLoad(ILLMProvider next, string model, CancellationToken token)
        {
            try
            {
                await delay(SettleDelay, token);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (Superseded(next, token))
            {
                return;
            }

            var previous = transition;
            var mine = new TaskCompletionSource<bool>();
            transition = mine.Task;
            try
            {
                // An earlier settle's unload may still be out; it must land before this one reads
                // what is loaded.
                await previous;
                if (Superseded(next, token))
                {
                    return;
                }

                if (loaded != null && loadedModel != model)
                {
                    var leaving = loaded;
                    loaded = null;
                    loadedModel = null;

                    // Not cancelled with the press: the old model should leave the card either way.
                    await UnloadAsync(leaving);
                    if (Superseded(next, token))
                    {
                        return;
                    }
                }

                if (providers.Choice != NpcDriverChoice.Agenerela)
                {
                    return;
                }

                // Recorded as the load is asked for, never by a settle that a newer one replaced.
                loaded = next;
                loadedModel = model;
            }
            finally
            {
                mine.SetResult(true);
            }

            SetNotice($"loading {model}…");
            bool ready = await WarmUpAsync(next, model, token);
            if (!Superseded(next, token))
            {
                SetNotice(ready ? "" : $"{model} did not load. F7: another model · F6: scripted");
            }
        }

        // The provider's own warm-up (#198): outside the decision queue, so a player's turn never
        // waits behind it, and cancelled, unsent or aborted, by a newer press.
        private static async Task<bool> WarmUpAsync(ILLMProvider provider, string model, CancellationToken token)
        {
            if (!(provider is IModelLifecycle lifecycle))
            {
                return true;
            }

            try
            {
                await lifecycle.WarmUpAsync(token);
                return true;
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                return false;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[LivingVale] {model} did not load: {e.Message}");
                return false;
            }
        }

        private static async Task UnloadAsync(ILLMProvider provider)
        {
            if (!(provider is IModelLifecycle lifecycle))
            {
                return;
            }

            try
            {
                await lifecycle.UnloadAsync(CancellationToken.None);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[LivingVale] The server did not unload the model left behind: {e.Message}");
            }
        }

        private void SetNotice(string notice)
        {
            Notice = notice;
            NoticeChanged?.Invoke();
        }
    }
}

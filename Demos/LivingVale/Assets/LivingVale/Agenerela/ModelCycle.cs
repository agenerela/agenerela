using System;
using System.Threading;
using System.Threading.Tasks;
using LivingVale.Interaction;

namespace LivingVale.Agenerela
{
    /// <summary>
    /// What the model key (F7) does: closes the open conversation, so no turn crosses models, asks
    /// <see cref="ModelSelector"/> for the next installed model, and puts a driver rebuilt for it
    /// into the switch. The rebuilt driver keeps the executor, personas and every NPC's memory.
    /// </summary>
    /// <remarks>
    /// Plain C#: the bootstrap owns the key and the overlay. One press at a time; a press while one
    /// is still asking the server does nothing. A server that is down or slow only leaves a notice.
    /// </remarks>
    public sealed class ModelCycle
    {
        private readonly ConversationCoordinator coordinator;
        private readonly SwitchableNpcDecisionDriver drivers;
        private readonly ModelSelector selector;
        private readonly Func<AgenerelaNpcDecisionDriver, string, AgenerelaNpcDecisionDriver> rebuild;
        private readonly IModelLoader loader;
        private readonly Func<TimeSpan, CancellationToken, Task> delay;
        private CancellationTokenSource settling;

        // The model the server was last asked to load, which a settled switch unloads; null while
        // an unload is out or after it, when nothing this cycle asked for is loaded.
        private string loadedModel;

        // The newest settle's unload. Each settle waits for the one before it, so two never read
        // or write loadedModel at once.
        private Task transition = Task.CompletedTask;

        /// <summary>How long the key must rest before the model it landed on is loaded (the press debounce).</summary>
        public static readonly TimeSpan DefaultSettleDelay = TimeSpan.FromSeconds(1);

        /// <param name="rebuild">Makes the driver for a model from the current one; null uses <see cref="AgenerelaNpcDecisionDriver.WithOllamaModel"/>.</param>
        /// <param name="loader">Unloads the model left behind; null unloads nothing.</param>
        /// <param name="delay">Waits out the settle delay; null uses <see cref="Task.Delay(TimeSpan, CancellationToken)"/>. Tests replace it.</param>
        public ModelCycle(ConversationCoordinator coordinator, SwitchableNpcDecisionDriver drivers, ModelSelector selector,
            AgenerelaNpcDecisionDriver current, Func<AgenerelaNpcDecisionDriver, string, AgenerelaNpcDecisionDriver> rebuild = null,
            IModelLoader loader = null, Func<TimeSpan, CancellationToken, Task> delay = null)
        {
            this.coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
            this.drivers = drivers ?? throw new ArgumentNullException(nameof(drivers));
            this.selector = selector ?? throw new ArgumentNullException(nameof(selector));
            Driver = current ?? throw new ArgumentNullException(nameof(current));
            this.rebuild = rebuild ?? ((driver, model) => driver.WithOllamaModel(model));
            this.loader = loader;
            this.delay = delay ?? Task.Delay;
            loadedModel = current.ModelLabel;
        }

        public TimeSpan SettleDelay { get; set; } = DefaultSettleDelay;

        /// <summary>
        /// The newest settle-then-load, for tests and tools: it waits <see cref="SettleDelay"/>,
        /// unloads the model loaded before, then warms the new one up. A later press cancels it.
        /// </summary>
        public Task Loading { get; private set; } = Task.CompletedTask;

        /// <summary>Raised on the caller's context whenever <see cref="Notice"/> changes after a press, as a model starts and finishes loading.</summary>
        public event Action NoticeChanged;

        /// <summary>The model-backed driver in use now.</summary>
        public AgenerelaNpcDecisionDriver Driver { get; private set; }

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
                AgenerelaNpcDecisionDriver switchedTo = null;
                if (choice.Changed)
                {
                    // Again, right before the swap: the player may have opened a chat and sent a
                    // line while the server was asked. Closing ends that turn, so its reply is
                    // dropped and no turn starts on one model and lands on another.
                    coordinator.Close(ConversationCloseReason.Interrupted);
                    var next = rebuild(Driver, choice.Model);
                    drivers.ReplaceAgenerela(next);
                    Driver = next;
                    switchedTo = next;
                }

                Notice = choice.Notice;
                if (switchedTo != null)
                {
                    // Only once the key rests: pressing through nine models must not queue nine
                    // loads on one card. A newer press cancels this one, warm-up included.
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
        /// player turns the model driver back on. A model loaded any other way would be missing
        /// from what the next switch unloads.
        /// </summary>
        public void LoadCurrent(CancellationToken ct = default)
        {
            Settle(Driver, ct);
        }

        private void Settle(AgenerelaNpcDecisionDriver next, CancellationToken ct)
        {
            settling?.Cancel();
            settling?.Dispose();
            settling = CancellationTokenSource.CreateLinkedTokenSource(ct);
            Loading = SettleThenLoad(next, settling.Token);
        }

        private async Task SettleThenLoad(AgenerelaNpcDecisionDriver next, CancellationToken token)
        {
            try
            {
                await delay(SettleDelay, token);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (token.IsCancellationRequested || !ReferenceEquals(next, Driver))
            {
                return;
            }

            string model = next.ModelLabel;
            var previous = transition;
            var mine = new TaskCompletionSource<bool>();
            transition = mine.Task;
            try
            {
                // An earlier settle's unload may still be out; it must land before this one reads
                // what is loaded.
                await previous;
                if (token.IsCancellationRequested || !ReferenceEquals(next, Driver))
                {
                    return;
                }

                if (loader != null && loadedModel != null && loadedModel != model)
                {
                    string leaving = loadedModel;
                    loadedModel = null;

                    // Not cancelled with the press: the old model should leave the card either way.
                    await loader.UnloadAsync(leaving, CancellationToken.None);
                    if (token.IsCancellationRequested || !ReferenceEquals(next, Driver))
                    {
                        return;
                    }
                }

                if (drivers.Choice != NpcDriverChoice.Agenerela)
                {
                    return;
                }

                // Recorded as the load is asked for, never by a settle that a newer one replaced.
                loadedModel = model;
            }
            finally
            {
                mine.SetResult(true);
            }

            SetNotice($"loading {model}…");
            bool ready = await next.WarmUpAsync(token);
            if (!token.IsCancellationRequested && ReferenceEquals(next, Driver))
            {
                SetNotice(ready ? "" : $"{model} did not load. F7: another model · F6: scripted");
            }
        }

        private void SetNotice(string notice)
        {
            Notice = notice;
            NoticeChanged?.Invoke();
        }
    }
}

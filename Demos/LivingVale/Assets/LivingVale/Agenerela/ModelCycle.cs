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

        /// <param name="rebuild">Makes the driver for a model from the current one; null uses <see cref="AgenerelaNpcDecisionDriver.WithOllamaModel"/>.</param>
        public ModelCycle(ConversationCoordinator coordinator, SwitchableNpcDecisionDriver drivers, ModelSelector selector,
            AgenerelaNpcDecisionDriver current, Func<AgenerelaNpcDecisionDriver, string, AgenerelaNpcDecisionDriver> rebuild = null)
        {
            this.coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
            this.drivers = drivers ?? throw new ArgumentNullException(nameof(drivers));
            this.selector = selector ?? throw new ArgumentNullException(nameof(selector));
            Driver = current ?? throw new ArgumentNullException(nameof(current));
            this.rebuild = rebuild ?? ((driver, model) => driver.WithOllamaModel(model));
        }

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
                if (choice.Changed)
                {
                    // Again, right before the swap: the player may have opened a chat and sent a
                    // line while the server was asked. Closing ends that turn, so its reply is
                    // dropped and no turn starts on one model and lands on another.
                    coordinator.Close(ConversationCloseReason.Interrupted);
                    var next = rebuild(Driver, choice.Model);
                    drivers.ReplaceAgenerela(next);
                    Driver = next;
                    if (drivers.Choice == NpcDriverChoice.Agenerela)
                    {
                        next.WarmUp();
                    }
                }

                Notice = choice.Notice;
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
    }
}

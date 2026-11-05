using System;
using System.Threading;
using System.Threading.Tasks;
using LivingVale.Interaction;

namespace LivingVale.Agenerela
{
    /// <summary>Switches future turns without changing who executes a pending decision.</summary>
    public sealed class SwitchableNpcDecisionDriver : INpcDecisionDriver
    {
        private readonly INpcDecisionDriver scripted;
        private readonly INpcDecisionDriver agenerela;

        public NpcDriverChoice Choice { get; private set; }
        public INpcDecisionDriver Current => Choice == NpcDriverChoice.Scripted ? scripted : agenerela;
        public string Label => Current.Label;

        public SwitchableNpcDecisionDriver(INpcDecisionDriver scripted, INpcDecisionDriver agenerela,
            NpcDriverChoice initialSelection)
        {
            this.scripted = scripted ?? throw new ArgumentNullException(nameof(scripted));
            this.agenerela = agenerela ?? throw new ArgumentNullException(nameof(agenerela));
            Select(initialSelection);
        }

        public void Select(NpcDriverChoice choice)
        {
            if (choice != NpcDriverChoice.Scripted && choice != NpcDriverChoice.Agenerela)
            {
                throw new ArgumentOutOfRangeException(nameof(choice));
            }

            Choice = choice;
        }

        public void Next() => Select(Choice == NpcDriverChoice.Scripted
            ? NpcDriverChoice.Agenerela : NpcDriverChoice.Scripted);

        public Task<NpcReply> DecideAsync(NpcTurn turn, CancellationToken ct)
        {
            // Capture before calling or awaiting the backend: selection may change while it runs.
            var decidingDriver = Current;
            var pending = decidingDriver.DecideAsync(turn, ct);
            return pending == null ? null : RouteAsync(decidingDriver, pending);
        }

        private async Task<NpcReply> RouteAsync(INpcDecisionDriver decidingDriver, Task<NpcReply> pending)
        {
            var inner = await pending;
            if (inner == null)
            {
                return null;
            }

            return inner.Failure != NpcReplyFailure.None
                ? NpcReply.Failed(inner.Failure)
                : NpcReply.Decided(inner.Decision, new Routed(this, decidingDriver, inner));
        }

        public bool Execute(NpcTurn turn, NpcReply reply)
        {
            if (!(reply?.DriverState is Routed routed) || !ReferenceEquals(routed.Owner, this))
            {
                return false;
            }

            return routed.Driver.Execute(turn, routed.Reply);
        }

        private sealed class Routed
        {
            public readonly SwitchableNpcDecisionDriver Owner;
            public readonly INpcDecisionDriver Driver;
            public readonly NpcReply Reply;

            public Routed(SwitchableNpcDecisionDriver owner, INpcDecisionDriver driver, NpcReply reply)
            {
                Owner = owner;
                Driver = driver;
                Reply = reply;
            }
        }
    }
}

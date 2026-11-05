using System;
using System.Collections.Generic;
using Agenerela;
using Agenerela.Providers;

namespace LivingVale.Agenerela
{
    /// <summary>
    /// The framework's <see cref="RollingHistory"/>, remembering a decision only once the game has
    /// shown it. <see cref="Agent.DecideAsync"/> remembers each decision as it returns it, but the
    /// conversation layer may still drop that reply as stale (a timeout, a cancel, a switch of
    /// speaker), and then the NPC would remember a line the player never saw. Here a decision waits
    /// until <see cref="Commit"/>, which the driver calls when the coordinator executes it.
    /// </summary>
    /// <remarks>
    /// Only the newest decision waits: the coordinator keeps one turn in flight per conversation and
    /// executes a reply before the next can start, so an older one still waiting was dropped.
    /// </remarks>
    public sealed class CommittedHistory : IMemoryStrategy, IReportsDroppedTurns
    {
        private readonly RollingHistory inner;
        private Pending pending;

        public CommittedHistory(RollingHistory inner = null)
        {
            this.inner = inner ?? new RollingHistory();
        }

        /// <summary>The exchanges remembered, committed ones only.</summary>
        public int Count => inner.Count;

        public int Dropped => inner.Dropped;

        public int Forgotten => inner.Forgotten;

        public IReadOnlyList<string> Recall(AgentContext ctx) => inner.Recall(ctx);

        public void Remember(AgentContext ctx, DecisionRequest request, DecisionResult result)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            pending = new Pending(ctx, request, result);
        }

        /// <summary>Remembers <paramref name="result"/> if it is the decision waiting. Returns whether it was.</summary>
        public bool Commit(DecisionResult result)
        {
            var waiting = pending;
            if (waiting == null || !ReferenceEquals(waiting.Result, result))
            {
                return false;
            }

            pending = null;
            inner.Remember(waiting.Context, waiting.Request, waiting.Result);
            return true;
        }

        private sealed class Pending
        {
            public readonly AgentContext Context;
            public readonly DecisionRequest Request;
            public readonly DecisionResult Result;

            public Pending(AgentContext context, DecisionRequest request, DecisionResult result)
            {
                Context = context;
                Request = request;
                Result = result;
            }
        }
    }
}

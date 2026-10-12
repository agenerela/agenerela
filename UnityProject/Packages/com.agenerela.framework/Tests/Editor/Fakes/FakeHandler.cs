using System;
using System.Collections.Generic;

namespace Agenerela.Tests
{
    /// <summary>
    /// An action handler that runs no game code: it answers availability from an optional
    /// predicate, and records every execution, so a test can assert on what ran and with what.
    /// </summary>
    public sealed class FakeHandler : IActionHandler
    {
        private readonly Func<AgentContext, bool> available;
        private readonly List<(AgentContext Context, AgentDecision Decision)> executions =
            new List<(AgentContext Context, AgentDecision Decision)>();

        /// <param name="available">Whether the action is legal in a context. Null means always.</param>
        public FakeHandler(Func<AgentContext, bool> available = null)
        {
            this.available = available;
        }

        /// <summary>Every call to <see cref="Execute"/>, oldest first.</summary>
        public IReadOnlyList<(AgentContext Context, AgentDecision Decision)> Executions => executions;

        /// <summary>The contexts <see cref="IsAvailable"/> was asked about, oldest first.</summary>
        public List<AgentContext> AvailabilityChecks { get; } = new List<AgentContext>();

        public bool IsAvailable(AgentContext ctx)
        {
            AvailabilityChecks.Add(ctx);
            return available == null || available(ctx);
        }

        public void Execute(AgentContext ctx, AgentDecision decision)
        {
            executions.Add((ctx, decision));
        }
    }
}

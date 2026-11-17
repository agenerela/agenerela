using System;

namespace Agenerela
{
    /// <summary>
    /// One agent's place in a <see cref="DecisionRound"/>: what it decided, and, once the round has
    /// executed it, what <see cref="Agent.Execute(DecisionResult, out ActionResult)"/> reported.
    /// </summary>
    public sealed class RoundEntry
    {
        internal RoundEntry(int index, Agent agent, string stimulus, DecideOptions options)
        {
            Index = index;
            Agent = agent;
            Stimulus = stimulus;
            Options = options;
        }

        /// <summary>Its place in the calls the round was given, from 0.</summary>
        public int Index { get; }

        /// <summary>The agent.</summary>
        public Agent Agent { get; }

        /// <summary>What it was asked.</summary>
        public string Stimulus { get; }

        // The round's own copy, its chain set; read once, when the round decides.
        internal DecideOptions Options { get; }

        /// <summary>Where it stands: decided and waiting, executed, or never executed and why.</summary>
        public RoundEntryStatus Status { get; internal set; }

        /// <summary>
        /// What <see cref="Agent.DecideAsync"/> returned: a decision, or a result with no decision
        /// for a pipeline error, which this entry's agent alone suffers. Null only when the call
        /// threw instead (<see cref="RoundEntryStatus.Failed"/>, <see cref="Error"/>).
        /// </summary>
        public DecisionResult Result { get; internal set; }

        /// <summary>
        /// Why the entry has no result (<see cref="RoundEntryStatus.Failed"/>): the exception its
        /// <see cref="Agent.DecideAsync"/> ended with, an <see cref="OperationCanceledException"/>
        /// when the agent was <see cref="Agent.Reset"/> during the round. For
        /// <see cref="RoundEntryStatus.Threw"/>, what its handler threw. Null otherwise.
        /// </summary>
        public Exception Error { get; internal set; }

        /// <summary>
        /// What <see cref="Agent.Execute(DecisionResult, out ActionResult)"/> reported, once the round
        /// executed this entry (<see cref="RoundEntryStatus.Executed"/>); null before, and for an
        /// entry never executed or whose handler threw.
        /// </summary>
        public ActionResult? Outcome { get; internal set; }

        /// <summary>
        /// Whether <see cref="Agent.Execute(DecisionResult, out ActionResult)"/> refused a decision
        /// <see cref="Agent.DecideAsync"/> had accepted: its re-check found the move no longer legal,
        /// because an earlier execution in the round, or anything else since the round decided,
        /// changed the agent's state or targets. <see cref="Outcome"/>'s
        /// <see cref="ActionResult.Reason"/> says why. False for a pipeline error or a reply a guard
        /// rejected, which <c>Execute</c> refuses too but which were never legal moves.
        /// </summary>
        public bool Stale { get; internal set; }

        /// <summary>The decision, or null: <see cref="Result"/>'s <see cref="DecisionResult.Decision"/>.</summary>
        public AgentDecision Decision => Result?.Decision;

        /// <summary>The decision's action id, or null with no decision; a convenience for ordering a round by what was chosen.</summary>
        public string ActionId => Result?.Decision?.ActionId;

        /// <inheritdoc/>
        public override string ToString()
        {
            string who = Agent.Id ?? Agent.Identity?.Name ?? $"#{Index}";
            return Decision == null ? $"{who}: {Status}" : $"{who}: {Decision.ActionId} {Decision.TargetId} ({Status}{(Stale ? ", stale" : "")})";
        }
    }

    /// <summary>Where a <see cref="RoundEntry"/> stands.</summary>
    public enum RoundEntryStatus
    {
        /// <summary>Decided, and waiting for <see cref="DecisionRound.Execute(Agent[])"/>. A pipeline error is decided too: executing it is refused, as <see cref="Agent.Execute(DecisionResult)"/> refuses one.</summary>
        Decided,

        /// <summary><see cref="Agent.DecideAsync"/> threw for this agent alone, so there is no result to execute (<see cref="RoundEntry.Error"/>).</summary>
        Failed,

        /// <summary>Executed: <see cref="RoundEntry.Outcome"/> says what came of it, <see cref="RoundEntry.Stale"/> whether it was refused as stale.</summary>
        Executed,

        /// <summary>Its handler threw (<see cref="RoundEntry.Error"/>), which the round passed on to its caller; it is never executed again.</summary>
        Threw,

        /// <summary><see cref="DecisionRound.End"/> ended the round before this entry was executed; its decision was discarded from memory.</summary>
        Skipped,
    }
}

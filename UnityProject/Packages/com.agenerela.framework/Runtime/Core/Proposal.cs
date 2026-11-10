namespace Agenerela
{
    /// <summary>
    /// One decision a <see cref="Proposals"/> holds for the player to confirm or decline: the
    /// agent offered it, and nothing has run yet (#190, Living Vale #16).
    /// </summary>
    /// <remarks>
    /// A card in the game's UI keeps a reference to its proposal and hands that same object to
    /// <see cref="Proposals.Confirm(Proposal, out ActionResult)"/>, so a click on a card whose
    /// proposal has since been superseded, declined or reset is refused rather than carrying out
    /// whatever is pending now. Its <see cref="Status"/> changes once, away from
    /// <see cref="ProposalStatus.Pending"/>.
    /// </remarks>
    public sealed class Proposal
    {
        internal Proposal(Proposals owner, DecisionResult result, int number)
        {
            Owner = owner;
            Result = result;
            Number = number;
            Status = ProposalStatus.Pending;
        }

        internal Proposals Owner { get; }

        /// <summary>The decision held, as <see cref="Agent.DecideAsync"/> returned it.</summary>
        public DecisionResult Result { get; }

        /// <summary>What the agent decided: <see cref="DecisionResult.Decision"/>, never null or <c>none</c>.</summary>
        public AgentDecision Decision => Result.Decision;

        /// <summary>The proposed action's id.</summary>
        public string ActionId => Result.Decision.ActionId;

        /// <summary>The proposed target's id, which may be <see cref="TargetRegistry.NoTarget"/>.</summary>
        public string TargetId => Result.Decision.TargetId;

        /// <summary>The caller's own correlation, <see cref="DecisionResult.Correlation"/>: a conversation generation, a quest id.</summary>
        public object Correlation => Result.Correlation;

        /// <summary>The proposal's place in its <see cref="Proposals"/>, from 1, in the order held.</summary>
        public int Number { get; }

        /// <summary>Where it stands. <see cref="ProposalStatus.Pending"/> until confirmed, declined, superseded or reset.</summary>
        public ProposalStatus Status { get; internal set; }

        /// <summary>Whether it is still waiting for the player.</summary>
        public bool IsPending => Status == ProposalStatus.Pending;

        /// <summary>
        /// What <see cref="Agent.Execute(DecisionResult, out ActionResult)"/> reported when it was
        /// confirmed. Null until then, and for one that never reached it: declined, superseded,
        /// reset, refused by <see cref="Proposals.StillValid"/>, or whose handler threw.
        /// </summary>
        public ActionResult? Outcome { get; internal set; }

        /// <summary>
        /// Why it ended without being carried out, in words for the developer: the refusal's reason,
        /// or what superseded, declined or reset it. Null while pending and once accepted.
        /// </summary>
        public string Reason { get; internal set; }

        public override string ToString()
        {
            return $"#{Number} {ActionId} -> {TargetId} ({Status})";
        }
    }

    /// <summary>Where a <see cref="Proposal"/> stands. It changes once, away from <see cref="Pending"/>.</summary>
    public enum ProposalStatus
    {
        /// <summary>Held, waiting for the player to confirm or decline it.</summary>
        Pending,

        /// <summary>Confirmed, and <see cref="Agent.Execute(DecisionResult)"/> carried it out.</summary>
        Accepted,

        /// <summary>Confirmed, and the handler ran but reported <see cref="ExecutionStatus.Failed"/>, or threw.</summary>
        Failed,

        /// <summary>
        /// Confirmed, and refused without running a handler: <see cref="Proposals.StillValid"/> said
        /// no, or the agent's own re-check at <see cref="Agent.Execute(DecisionResult)"/> did, the
        /// per-target check (<see cref="ITargetAvailability"/>) included. <see cref="Proposal.Reason"/> says why.
        /// </summary>
        Refused,

        /// <summary>Declined, by <see cref="Proposals.Decline"/> or <see cref="Proposals.DeclineAll"/>.</summary>
        Declined,

        /// <summary>A newer proposal replaced it while <see cref="Proposals.OnePending"/> was on.</summary>
        Superseded,

        /// <summary>The agent was <see cref="Agent.Reset"/> while it waited.</summary>
        Reset,
    }
}

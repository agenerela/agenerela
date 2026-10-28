using System.Collections.Generic;

namespace Agenerela
{
    /// <summary>
    /// The settings for one call to <see cref="Agent.DecideAsync"/>. Every field has a default, and
    /// a null <c>DecideOptions</c> means all of them.
    /// </summary>
    /// <remarks>
    /// Open on purpose (DR-008): free strings and switches the developer sets per call, never an
    /// enum of situations. Whether this call is a player's line, a timer or a report is the
    /// developer's knowledge, and the framework never asks. What comes later goes here as more
    /// switches of the same kind, such as Phase 3's per-call guard settings, starting with
    /// <see cref="CheckTarget"/> (DR-016). <see cref="Priority"/> is one too: it says whether anyone
    /// is waiting, never why the agent is asked (DR-015).
    /// </remarks>
    public sealed class DecideOptions
    {
        /// <summary>
        /// How the stimulus is labelled in the prompt, and in the examples: "Player", "Advisor",
        /// "Report", "Situation"... Any non-empty string; an empty one is a mistake the call
        /// throws for.
        /// </summary>
        public string StimulusLabel = "Player";

        /// <summary>
        /// The stimulus of the idle example, the one answered with <c>none</c>, for this call only.
        /// Null uses the profile's <see cref="AgentProfile.IdleExampleStimulus"/>; empty leaves the
        /// example out of this call.
        /// </summary>
        public string IdleExampleStimulus;

        /// <summary>
        /// What the game wants the agent to know right now, one fact per entry, sent verbatim. The
        /// list is copied when the call starts, so changing it afterwards changes nothing.
        /// </summary>
        /// <remarks>
        /// Observations are what the model reads about the world. <see cref="Agent.State"/> is
        /// not: it stays with the developer's handlers and never reaches the prompt.
        /// </remarks>
        public IList<string> Observations = new List<string>();

        /// <summary>
        /// Whether anyone is waiting on this decision (DR-015). Every agent on one provider shares
        /// its queue, which sends a <see cref="DecisionPriority.PlayerFacing"/> request before any
        /// <see cref="DecisionPriority.Background"/> one. Player-facing by default; set it to
        /// background for ambient behaviour, a timer or anything else nobody is waiting for.
        /// </summary>
        /// <remarks>
        /// Going first means jumping the queue: a background request already sent is not aborted.
        /// The decision's telemetry records the priority, how long it waited and behind how many.
        /// </remarks>
        public DecisionPriority Priority = DecisionPriority.PlayerFacing;

        /// <summary>
        /// Whether <see cref="TargetGroundingGuard"/> checks this call's target: whether the model
        /// wanted something that is not on the list, and so would act on the nearest listed target
        /// instead (DR-016). On by default, whoever calls. Turn it off for a call where the agent
        /// should act on its best guess rather than do nothing.
        /// </summary>
        /// <remarks>
        /// Off, the guard does not run on that call, and the decision's telemetry records
        /// <see cref="GroundingStatus.Off"/>. It also turns off the guard's fallback: on a provider
        /// that reports no token probabilities, the prompt carries the one-line target rule only
        /// while this is on (<see cref="PromptOptions.TargetRule"/>, #107).
        /// </remarks>
        public bool CheckTarget = true;

        // The options one call keeps, taken when it is made, so changing the caller's object
        // afterwards changes nothing, the guards' view included (#106). A memberwise copy, so a
        // switch added later is copied without being listed here; only a field holding a mutable
        // object needs copying by hand.
        internal DecideOptions Copy()
        {
            var copy = (DecideOptions)MemberwiseClone();
            copy.Observations = Observations == null ? null : new List<string>(Observations);
            return copy;
        }
    }
}

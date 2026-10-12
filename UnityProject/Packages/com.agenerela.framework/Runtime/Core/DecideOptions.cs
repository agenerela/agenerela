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
    /// switches of the same kind: Phase 3's per-call guard settings, starting with
    /// <c>CheckTarget</c> (DR-016), and the scheduler's <c>Priority</c> (DR-015).
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
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace Agenerela
{
    /// <summary>The Developers description of who this agent is.</summary>
    /// <remarks>
    /// An agent built in code takes one of these in its constructor, so it is given
    /// <see cref="Knowledge"/> and <see cref="StatementRules"/> the same way as the rest: set the
    /// fields, before or after the agent is made. The model reads them on every call.
    /// </remarks>
    [System.Serializable]
    public sealed class AgentIdentity
    {
        /// <summary>Display name, for example "Gate Guard".</summary>
        public string Name;
        /// <summary>Functionality in the game, for example "Village Sentry".</summary>
        public string Role;
        /// <summary>How the agent speaks or behaves.</summary>
        [TextArea] public string Personality;
        /// <summary>What the agent wants. Most important factor for a faction or colony.</summary>
        [TextArea] public string Goals;

        /// <summary>
        /// What the agent always knows, one line each, such as the game's rules for it ("When trouble
        /// is reported at a place you can reach, go there.") or facts that never change. Sent in the
        /// system prompt on every call, under its own heading after the goals, and never counted
        /// against the observation cap (#190, Living Vale #14). Empty by default, which sends
        /// nothing. Blank lines are skipped, and a line's own line breaks become spaces.
        /// </summary>
        /// <remarks>
        /// What is true only now belongs in the observations instead, and what happened in the
        /// conversation in memory. On the evaluation set, five policy lines did better here than as
        /// observations, within noise (findings, 9 November 2026).
        /// </remarks>
        [Tooltip("Optional. What this agent always knows, one line each: standing facts and the " +
                 "game's rules for it. Sent on every call. What is true only now goes in observations.")]
        public List<string> Knowledge = new List<string>();

        /// <summary>
        /// How the agent's <c>statement</c> should read, such as "One short sentence. Never give
        /// the player orders." Sent in the system prompt after <see cref="Knowledge"/> (#190, Living
        /// Vale #14). Named for the statement field, not for speech: the agent may be a country
        /// writing a report (DR-008). Empty by default, which sends nothing. Line breaks become
        /// spaces, so it stays one line.
        /// </summary>
        [Tooltip("Optional. How this agent's statement should read, such as its length or what it " +
                 "never says. Sent on every call.")]
        [TextArea] public string StatementRules = "";
    }
}

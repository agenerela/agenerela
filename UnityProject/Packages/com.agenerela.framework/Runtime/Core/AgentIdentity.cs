using UnityEngine;

namespace Agenerela
{
    /// <summary>The Developers description of who this agent is.</summary>
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
    }
}

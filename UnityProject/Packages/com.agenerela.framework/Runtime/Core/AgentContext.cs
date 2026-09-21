using System.Collections.Generic;

namespace Agenerela
{
    /// <summary>What the agent knows at a decision time.</summary>
    public sealed class AgentContext
    {
        /// <summary>Who this agent is.</summary>
        public AgentIdentity Identity{ get; }
        /// <summary>What prompted this decision.</summary>
        public string Stimulus{ get; }
        /// <summary>What the game wants the agent to know right now.</summary>
        public IReadOnlyList<string> Observations{ get; }
        /// <summary>The current state of the agent, such as "isFollowing".</summary>
        public IReadOnlyDictionary<string, object> State{ get; }
        public TargetRegistry Targets{ get; }
        // !! IMPORTANT TODO: #6 issue must be resolved first
        // public ActionRegistry Actions{ get; }


        /// <summary>Contrustor for AgentContext.</summary>
        public AgentContext(AgentIdentity identity, string stimulus, IReadOnlyList<string> observations, IReadOnlyDictionary<string, object> state, TargetRegistry targets)
        {
            Identity = identity;
            Stimulus = stimulus;
            Observations = observations;
            State = state;
            Targets = targets;
        }
    }
}

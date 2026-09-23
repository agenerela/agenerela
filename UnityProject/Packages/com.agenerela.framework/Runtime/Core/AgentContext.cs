using System.Collections.Generic;
using System;

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
        /// <summary>What this agent may refer to this request. Live list from 'TargetRegistry'.</summary>
        public TargetRegistry Targets{ get; }
        // !! IMPORTANT TODO: add Actions once ActionRegistry lands
        // public ActionRegistry Actions{ get; }

        /// <summary>
        /// Creates a snapshot of what the agent knows for one decision.
        /// Identity and Targets cannot be null.
        /// Null stimulus becomes empty.
        /// Null observations or state become empty collections and stay non-null.</summary>
        public AgentContext(AgentIdentity identity, string stimulus, IReadOnlyList<string> observations, IReadOnlyDictionary<string, object> state, TargetRegistry targets)
        {
            if (identity == null)
            {
                throw new ArgumentNullException(nameof(identity));
            }

            if (targets == null)
            {
                throw new ArgumentNullException(nameof(targets));
            }

            if (stimulus == null)
            {
                stimulus = "";
            }

            if (observations == null)
            {
                observations = Array.Empty<string>();
            }

            if (state == null)
            {
                state = new Dictionary<string, object>();
            }

            Identity = identity;
            Stimulus = stimulus;
            Observations = observations;
            State = state;
            Targets = targets;
        }
    }
}

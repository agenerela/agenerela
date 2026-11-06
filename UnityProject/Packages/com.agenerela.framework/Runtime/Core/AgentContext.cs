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
        /// <summary>What this agent may choose to do this request. Live list from 'ActionRegistry'.</summary>
        public ActionRegistry Actions{ get; }

        /// <summary>
        /// The caller's own object for the call this context belongs to, from
        /// <see cref="DecideOptions.Correlation"/>: which conversation, turn or request a decision
        /// answers. The framework never reads it (DR-018). The same object on every context made
        /// for one decision, the one <c>Agent.Execute</c> hands the handler included; null when the
        /// call set none.
        /// </summary>
        public object Correlation{ get; }

        /// <summary>
        /// Creates a snapshot of what the agent knows for one decision.
        /// Identity, Targets, and Actions cannot be null.
        /// Null stimulus becomes empty.
        /// Null observations or state become empty collections and stay non-null.</summary>
        public AgentContext(
            AgentIdentity identity,
            string stimulus,
            IReadOnlyList<string> observations,
            IReadOnlyDictionary<string, object> state,
            TargetRegistry targets,
            ActionRegistry actions)
            : this(identity, stimulus, observations, state, targets, actions, null)
        {
        }

        /// <summary>
        /// A snapshot as the constructor above makes it, carrying the call's
        /// <paramref name="correlation"/>, which may be null.
        /// </summary>
        public AgentContext(
            AgentIdentity identity,
            string stimulus,
            IReadOnlyList<string> observations,
            IReadOnlyDictionary<string, object> state,
            TargetRegistry targets,
            ActionRegistry actions,
            object correlation)
        {
            if (identity == null)
            {
                throw new ArgumentNullException(nameof(identity));
            }

            if (targets == null)
            {
                throw new ArgumentNullException(nameof(targets));
            }

            if (actions == null)
            {
                throw new ArgumentNullException(nameof(actions));
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
            Actions = actions;
            Correlation = correlation;
        }
    }
}

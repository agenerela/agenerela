using System.Collections.Generic;
using UnityEngine;

namespace Agenerela
{
    /// <summary>
    /// Who this agent is and what it can do. Shared by every instance that uses it. For example,
    /// ten village guards read one profile.
    /// </summary>
    [CreateAssetMenu(menuName = "Agenerela/Agent Profile", fileName = "NewAgentProfile")]
    public sealed class AgentProfile : ScriptableObject
    {
        public AgentIdentity Identity = new AgentIdentity();

        [Tooltip("Every action this agent can ever take. Availability at a given moment " +
                 "is decided by each action's handler, not here.")]
        public List<ActionDefinitionAsset> Actions = new List<ActionDefinitionAsset>();

        [Tooltip("A stimulus for which the right answer is 'none'. Shown to the model as " +
                 "the idle example. Leave empty to omit it.")]
        public string IdleExampleStimulus = "Nothing has changed since last time.";

        public bool Validate(out string problem)
        {
            problem = null;

            if (Identity == null || string.IsNullOrWhiteSpace(Identity.Name))
            {
                problem = "Identity.Name is empty";
                return false;
            }

            for (int i = 0; i < Actions.Count; i++)
            {
                if (Actions[i] == null)
                {
                    problem = $"Actions[{i}] is empty";
                    return false;
                }
            }

            for (int i = 0; i < Actions.Count; i++)
            {
                string id = Actions[i].Action.Id;
                if (string.IsNullOrWhiteSpace(id)) continue;

                for (int j = i + 1; j < Actions.Count; j++)
                {
                    if (Actions[j].Action.Id == id)
                    {
                        problem = $"Action id '{id}' is listed twice";
                        return false;
                    }
                }
            }

            return true;
        }

        private void OnValidate()
        {
            if (!Validate(out string problem))
            {
                Debug.LogWarning($"{name}: {problem}", this);
            }
        }
    }
}

using UnityEngine;

namespace Agenerela
{
    /// <summary>
    /// One thing an agent can do, authored by the game developer as an asset rather than
    /// declared in framework source. The action vocabulary is data, not an enum, so a
    /// developer can add "build_house" to their own project without editing Agenerela.
    /// </summary>
    [CreateAssetMenu(menuName = "Agenerela/Action", fileName = "NewAction")]
    public sealed class ActionDefinition : ScriptableObject
    {
        [Tooltip("snake_case. Becomes the value the model picks from.")]
        public string Id;

        [Tooltip("A human readable name for the action, e.g. 'Pick up'.")]
        [TextArea(1, 3)] public string Description;

        [Tooltip("Does this action need something to act on?")]
        public bool RequiresTarget;

        [Tooltip("A stimulus that should lead to this action.")]
        public string ExampleStimulus;

        [Tooltip("Target ids this verb sensibly applies to. Prevents nonsense examples " +
                 "like 'Pick up the Blacksmith'.")]
        public string[] PreferredExampleTargets;

        // Warns rather than throws: an asset is malformed for as long as it takes the
        // developer to finish typing into it, and that is not an error.
        private void OnValidate()
        {
            if (!ValidateId(Id, out string problem))
            {
                Debug.LogWarning($"{name}: {problem}", this);
            }
        }

        public static bool ValidateId(string id, out string problem)
        {
            problem = null;
            if (string.IsNullOrWhiteSpace(id))
            {
                problem = "Id is empty";                
                return false;
            }
            
            if (id != id.ToLower())
            {
                problem = $"Id '{id}' cannot have uppercase characters";
                return false;
            }
            
            if (id.Contains(' '))
            {
                problem = $"Id '{id}' cannot have spaces";
                return false;
            }
            return true;
        }
    }
}

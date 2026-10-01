using UnityEngine;

namespace Agenerela
{
    /// <summary>
    /// Marks a scene object as something an agent may name, and carries the one line an agent
    /// reads about it (DR-014, §2.8). One component does both on purpose: an object an agent
    /// can notice and one it can refer to are the same object, so the line it reads and the id
    /// it may choose cannot drift apart.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Targetable : MonoBehaviour
    {
        [Tooltip("snake_case, explicit. This is the literal string the model may emit. " +
                 "Never derived from the GameObject's name.")]
        public string Id;

        [Tooltip("One short line the agent reads: \"The gate is open.\" Optional.")]
        [TextArea(1, 2)] public string Description;

        [Tooltip("Free string target sources can filter on: landmark, prop, character.")]
        public string Category;

        [Tooltip("Untick to stop this object being nameable without destroying it, " +
                 "such as a door that has been sealed.")]
        public bool IsCurrentlyTargetable = true;

        /// <summary>
        /// The id rules for a targetable: those of an action id (not empty, no uppercase, no
        /// whitespace), and never <see cref="TargetRegistry.NoTarget"/>, which the schema reserves.
        /// </summary>
        public static bool ValidateId(string id, out string problem)
        {
            if (!ActionDefinition.ValidateId(id, out problem))
            {
                return false;
            }

            if (id == TargetRegistry.NoTarget)
            {
                problem = $"Id '{id}' is reserved for the schema's \"no target\" value";
                return false;
            }

            return true;
        }

        // Warns rather than throws, like ActionDefinitionAsset: a component is malformed for
        // as long as it takes the developer to finish typing into it.
        private void OnValidate()
        {
            if (!ValidateId(Id, out string problem))
            {
                Debug.LogWarning($"{name}: Targetable {problem}, so it can never be named.", this);
            }
        }
    }
}

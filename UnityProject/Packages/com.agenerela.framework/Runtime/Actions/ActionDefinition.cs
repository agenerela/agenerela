using System;
using UnityEngine;

namespace Agenerela
{
    [Serializable]
    public sealed class ActionDefinition
    {
        [Tooltip("snake_case. Becomes the value the model picks from.")]
        public string Id;

        [Tooltip("A human-readable name for the action, e.g. 'Pick up'")]
        [TextArea(1, 3)] public string Description;

        [Tooltip("Does this action need something to act on?")]
        public bool RequiresTarget;

        [Tooltip("A stimulus that should lead to this action")]
        public string ExampleStimulus;

        [Tooltip("Target ids this verb sensibly applies to. Prevents nonsense examples " +
                 "like 'Pick up the Blacksmith'.")]
        public string[] PreferredExampleTargets;

        public static bool ValidateId(string id, out string problem)
        {
            problem = null;

            if (string.IsNullOrWhiteSpace(id))
            {
                problem = "Id is empty";
                return false;
            }

            // Invariant, not the current culture: under a Turkish locale 'I'.ToLower()
            // is the dotless i (U+0131), so a culture-sensitive compare classifies ids by
            // whichever machine happens to open the asset.
            if (id != id.ToLowerInvariant())
            {
                problem = $"Id '{id}' cannot have uppercase characters";
                return false;
            }

            // Any whitespace, not just ' '. A tab is invisible in the Inspector but the
            // id goes verbatim into the schema enum, so it has to be caught here.
            foreach (char c in id)
            {
                if (char.IsWhiteSpace(c))
                {
                    problem = $"Id '{id}' cannot have whitespace";
                    return false;
                }
            }

            return true;
        }
    }
}

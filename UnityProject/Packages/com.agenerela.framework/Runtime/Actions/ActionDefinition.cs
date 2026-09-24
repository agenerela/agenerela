using System;
using UnityEngine;

namespace Agenerela
{
    [Serializable]
    public sealed class ActionDefinition
    {
        [Tooltip("snake_case. Becomes the value the model picks from.")]
        public string Id;

        [Tooltip("One SHORT clause telling the model WHEN to use this action. Keep it " +
         "comparable in length to your other actions. A long emphatic description " +
         "measurably biases small models toward picking it.")]
        [TextArea(1, 3)] public string Description;

        [Tooltip("Does this action need something to act on?")]
        public bool RequiresTarget;

        [Tooltip("A stimulus that should lead to this action. A player's input, an event, " +
         "or behavior that triggers this action. Use {0} for the target and never reuse an evaluation prompt")]
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

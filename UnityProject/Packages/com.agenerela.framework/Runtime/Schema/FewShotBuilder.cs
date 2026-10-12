using System.Collections.Generic;
using System.Text;

namespace Agenerela
{
    /// <summary>
    /// Assembles the block of example exchanges sent with every request. Every word of
    /// game-specific text comes from the developer's own action assets, so the same code
    /// serves a talking guard and a silent colony and never knows which it produced.
    /// </summary>
    public static class FewShotBuilder
    {
        private const string Header = "Examples of correct decisions:";

        // Stands in for a target the agent does not have, so the model learns to refuse
        // rather than substitute the nearest legal one. Checked against the registry in
        // case a game really has an Atlantis.
        private const string UnknownTarget = "Atlantis";
        private const string UnknownTargetFallback = "Avalon";

        
        // Builds the example block from the actions available right now.
        public static string Build(IReadOnlyList<ActionDefinition> available, TargetRegistry targets, string stimulusLabel = "Player", string idleExampleStimulus = null)
        {
            List<string> lines = new List<string> { Header };

            foreach (ActionDefinition action in available)
            {
                if (string.IsNullOrWhiteSpace(action.ExampleStimulus)) continue;

                if (!action.RequiresTarget)
                {
                    lines.Add(Example(stimulusLabel, action.ExampleStimulus, action.Id, TargetRegistry.NoTarget));
                    continue;
                }

                string target = FirstRegisteredPreferred(action, targets);
                if (target == null) continue;

                lines.Add(Example(stimulusLabel, Fill(action.ExampleStimulus, DisplayName(target)), action.Id, target));
            }

            if (!string.IsNullOrWhiteSpace(idleExampleStimulus))
            {
                lines.Add(Example(stimulusLabel, idleExampleStimulus, "none", TargetRegistry.NoTarget));
            }

            string refusal = RefusalExample(available, targets, stimulusLabel);
            if (refusal != null)
            {
                lines.Add(refusal);
            }

            return string.Join("\n", lines);
        }

        private static string RefusalExample(IReadOnlyList<ActionDefinition> available, TargetRegistry targets, string stimulusLabel)
        {
            string placeholder = targets.Contains(DisplayNameToId(UnknownTarget)) ? UnknownTargetFallback : UnknownTarget;

            foreach (ActionDefinition action in available)
            {
                if (!action.RequiresTarget) continue;
                if (string.IsNullOrWhiteSpace(action.ExampleStimulus)) continue;
                if (!action.ExampleStimulus.Contains("{0}")) continue;

                return Example(stimulusLabel, Fill(action.ExampleStimulus, placeholder), "none", TargetRegistry.NoTarget + " (not an available target)");
            }

            return null;
        }

        private static string FirstRegisteredPreferred(ActionDefinition action, TargetRegistry targets)
        {
            if (action.PreferredExampleTargets == null) return null;

            foreach (string candidate in action.PreferredExampleTargets)
            {
                if (!string.IsNullOrWhiteSpace(candidate) && targets.Contains(candidate))
                {
                    return candidate;
                }
            }

            return null;
        }

        private static string Example(string label, string stimulus, string actionId, string targetId) => $"{label}: \"{stimulus}\" -> action: {actionId}, target: {targetId}";

        private static string Fill(string stimulus, string target) => stimulus.Replace("{0}", target);

        private static string DisplayName(string id)
        {
            var display = new StringBuilder(id.Length);
            bool startOfWord = true;

            foreach (char c in id)
            {
                if (c == '_')
                {
                    startOfWord = true;
                    continue;
                }

                display.Append(startOfWord ? char.ToUpperInvariant(c) : c);
                startOfWord = false;
            }

            return display.ToString();
        }

        private static string DisplayNameToId(string display) => display.ToLowerInvariant();
    }
}

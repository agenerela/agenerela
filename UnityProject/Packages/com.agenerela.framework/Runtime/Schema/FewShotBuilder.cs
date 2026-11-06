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

        /// <summary>
        /// The example block for the actions available right now: each action's example, then its
        /// counter-example when it has one, then the idle example, then one refusal of a target
        /// that is not here.
        /// </summary>
        /// <param name="available">The actions offered in this request, in schema order.</param>
        /// <param name="targets">The targets offered in this request; examples name only these.</param>
        /// <param name="stimulusLabel">How each example's stimulus is labelled, as the request's is.</param>
        /// <param name="idleExampleStimulus">A stimulus answered with <c>none</c>. Null or empty leaves it out.</param>
        /// <param name="counterExamples">
        /// Whether each action's <see cref="ActionDefinition.CounterExampleStimulus"/> is shown, answered
        /// with <c>none</c> right after the action's own example (#190). On by default, which shows only
        /// what a developer wrote: the field is empty unless someone fills it. False leaves them all
        /// out, as an A/B run's control arm does.
        /// </param>
        public static string Build(IReadOnlyList<ActionDefinition> available, TargetRegistry targets, string stimulusLabel = "Player", string idleExampleStimulus = null, bool counterExamples = true)
        {
            List<string> lines = new List<string> { Header };

            foreach (ActionDefinition action in available)
            {
                string example = ActionExample(action, targets, stimulusLabel);
                if (example != null)
                {
                    lines.Add(example);
                }

                string counter = counterExamples ? CounterExample(action, targets, stimulusLabel) : null;
                if (counter != null)
                {
                    lines.Add(counter);
                }
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

        // The action's own example, or null when it has none or none of its preferred targets is
        // registered.
        private static string ActionExample(ActionDefinition action, TargetRegistry targets, string stimulusLabel)
        {
            if (string.IsNullOrWhiteSpace(action.ExampleStimulus)) return null;

            if (!action.RequiresTarget)
            {
                return Example(stimulusLabel, action.ExampleStimulus, action.Id, TargetRegistry.NoTarget);
            }

            string target = FirstRegisteredPreferred(action, targets);
            if (target == null) return null;

            return Example(stimulusLabel, Fill(action.ExampleStimulus, DisplayName(target)), action.Id, target);
        }

        // A line that touches the action's subject and asks for nothing, answered with none (#190,
        // Living Vale #9). Its {0} is filled as the example's is; with no preferred target
        // registered there is nothing to fill it with, so it is left out rather than sent with a
        // placeholder in it. Without {0} it is sent as written.
        private static string CounterExample(ActionDefinition action, TargetRegistry targets, string stimulusLabel)
        {
            string stimulus = action.CounterExampleStimulus;
            if (string.IsNullOrWhiteSpace(stimulus)) return null;

            if (stimulus.Contains("{0}"))
            {
                string target = FirstRegisteredPreferred(action, targets);
                if (target == null) return null;

                stimulus = Fill(stimulus, DisplayName(target));
            }

            return Example(stimulusLabel, stimulus, ActionRegistry.None, TargetRegistry.NoTarget);
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

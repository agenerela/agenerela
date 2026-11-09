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
            return Build(available, targets, new PromptOptions
            {
                StimulusLabel = stimulusLabel,
                IdleExampleStimulus = idleExampleStimulus,
                CounterExamples = counterExamples,
            });
        }

        /// <summary>
        /// The example block as <paramref name="options"/> asks for it: the overload <c>Agent</c> uses,
        /// so every per-call example setting comes from one object.
        /// </summary>
        /// <remarks>
        /// <para>
        /// It reads <see cref="PromptOptions.StimulusLabel"/>, <see cref="PromptOptions.IdleExampleStimulus"/>
        /// and <see cref="PromptOptions.CounterExamples"/>, and the statements of #190's G14, which show
        /// only what a developer wrote.
        /// </para>
        /// <list type="bullet">
        ///   <item><description><see cref="PromptOptions.ExampleStatements"/>: an example whose
        ///   <see cref="ActionDefinition.ExampleStatement"/> or <see cref="ActionDefinition.CounterExampleStatement"/>
        ///   is set, and the idle example when <see cref="PromptOptions.IdleExampleStatement"/> is, ends with
        ///   <c>, statement: "..."</c>. The refusal of a target that is not here never has one: its line would
        ///   be the framework's own words, sent with every prompt.</description></item>
        /// </list>
        /// </remarks>
        public static string Build(IReadOnlyList<ActionDefinition> available, TargetRegistry targets, PromptOptions options)
        {
            options = options ?? new PromptOptions();
            string label = options.StimulusLabel;
            bool statements = options.ExampleStatements;

            List<string> lines = new List<string> { Header };

            foreach (ActionDefinition action in available)
            {
                string target = FirstRegisteredPreferred(action, targets);

                string example = ActionExample(action, targets, target, label, statements);
                if (example != null)
                {
                    lines.Add(example);
                }

                string counter = options.CounterExamples ? CounterExample(action, targets, target, label, statements) : null;
                if (counter != null)
                {
                    lines.Add(counter);
                }
            }

            if (!string.IsNullOrWhiteSpace(options.IdleExampleStimulus))
            {
                lines.Add(Example(label, options.IdleExampleStimulus, "none", TargetRegistry.NoTarget,
                                  statements ? options.IdleExampleStatement : null));
            }

            string refusal = RefusalExample(available, targets, label);
            if (refusal != null)
            {
                lines.Add(refusal);
            }

            return string.Join("\n", lines);
        }

        // The action's own example, or null when it has none, or needs a target and has none to name.
        private static string ActionExample(ActionDefinition action, TargetRegistry targets, string target,
                                            string label, bool statements)
        {
            if (string.IsNullOrWhiteSpace(action.ExampleStimulus)) return null;

            string statement = statements ? action.ExampleStatement : null;

            if (!action.RequiresTarget)
            {
                return Example(label, action.ExampleStimulus, action.Id, TargetRegistry.NoTarget, statement);
            }

            if (target == null) return null;

            string name = DisplayName(target);
            return Example(label, Fill(action.ExampleStimulus, name), action.Id, target, Fill(statement, name));
        }

        // A line that touches the action's subject and asks for nothing, answered with none (#190,
        // Living Vale #9). Its {0}s, in the stimulus and the statement, name the example's target;
        // with no target to name, the line is left out rather than sent with a placeholder in it.
        // Without {0} it is sent as written.
        private static string CounterExample(ActionDefinition action, TargetRegistry targets, string target,
                                             string label, bool statements)
        {
            string stimulus = action.CounterExampleStimulus;
            if (string.IsNullOrWhiteSpace(stimulus)) return null;

            string statement = statements ? action.CounterExampleStatement : null;

            if (HasPlaceholder(stimulus) || HasPlaceholder(statement))
            {
                if (target == null) return null;

                string name = DisplayName(target);
                stimulus = Fill(stimulus, name);
                statement = Fill(statement, name);
            }

            return Example(label, stimulus, ActionRegistry.None, TargetRegistry.NoTarget, statement);
        }

        private static string RefusalExample(IReadOnlyList<ActionDefinition> available, TargetRegistry targets, string stimulusLabel)
        {
            string placeholder = targets.Contains(DisplayNameToId(UnknownTarget)) ? UnknownTargetFallback : UnknownTarget;

            foreach (ActionDefinition action in available)
            {
                if (!action.RequiresTarget) continue;
                if (string.IsNullOrWhiteSpace(action.ExampleStimulus)) continue;
                if (!action.ExampleStimulus.Contains("{0}")) continue;

                return Example(stimulusLabel, Fill(action.ExampleStimulus, placeholder), "none", TargetRegistry.NoTarget + " (not an available target)", null);
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

        // "Label: "stimulus" -> action: id, target: id", then ", statement: "..."" when there is one,
        // in the order the reply's fields come: action, target, statement.
        private static string Example(string label, string stimulus, string actionId, string targetId, string statement)
        {
            string line = $"{label}: {Quoted(stimulus)} -> action: {actionId}, target: {targetId}";
            return string.IsNullOrWhiteSpace(statement) ? line : line + $", statement: {Quoted(statement)}";
        }

        // Text inside an example's quotes, escaped as JSON escapes it, so a quote the developer wrote
        // ("He said \"halt\".") cannot end the stimulus or the statement early (#190, G14).
        private static string Quoted(string text)
        {
            return "\"" + (text ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        }

        private static bool HasPlaceholder(string text) => text != null && text.Contains("{0}");

        private static string Fill(string text, string target) => text?.Replace("{0}", target);

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

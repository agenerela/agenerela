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

            List<string> lines = new List<string> { Header };
            foreach (Shot shot in Shots(available, targets, options))
            {
                // The refusal's note says why there is no target. It never has a statement (see Shots).
                string target = shot.Refusal ? shot.Target + " (not an available target)" : shot.Target;
                lines.Add(Example(label, shot.Stimulus, shot.Action, target, shot.Statement));
            }

            return string.Join("\n", lines);
        }

        /// <summary>
        /// The same examples as <see cref="Build(IReadOnlyList{ActionDefinition}, TargetRegistry, PromptOptions)"/>,
        /// in the same order, as chat turns for <see cref="Providers.DecisionRequest.ExampleTurns"/> (#151):
        /// two entries per example, encoded as <see cref="Providers.DecisionRequest.History"/> is.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The first entry of each pair is the example's stimulus labelled as the request's own is,
        /// <c>Player: "Go to the Tower."</c>, its <c>{0}</c> filled with the target's name. The second
        /// is the answer as the JSON object <paramref name="schema"/> asks for, in its field order, on
        /// one line, as memory writes the agent's earlier decisions:
        /// <c>{"action":"move_to","target":"tower","statement":""}</c>. An example with a statement
        /// (<see cref="PromptOptions.ExampleStatements"/>) carries it; one without carries an empty one.
        /// </para>
        /// <para>
        /// The refusal of a target that is not here is answered <c>none</c> and <c>no_target</c> with an
        /// empty statement. The prose block's "(not an available target)" note is left out: it is not a
        /// value the schema allows, and putting it in the statement would teach the model to say the
        /// framework's own words with every refusal.
        /// </para>
        /// </remarks>
        /// <param name="available">The actions offered in this request, in schema order.</param>
        /// <param name="targets">The targets offered in this request; examples name only these.</param>
        /// <param name="options">The same settings <c>Build</c> reads. Null uses the defaults.</param>
        /// <param name="schema">The request's schema, whose fields the answers are written in. Null writes all three, action first.</param>
        public static IReadOnlyList<string> BuildTurns(IReadOnlyList<ActionDefinition> available, TargetRegistry targets,
                                                       PromptOptions options, DecisionSchema schema)
        {
            options = options ?? new PromptOptions();

            var turns = new List<string>();
            foreach (Shot shot in Shots(available, targets, options))
            {
                turns.Add(PromptBuilder.Labelled(options.StimulusLabel, shot.Stimulus));
                turns.Add(RollingHistory.Write(new AgentDecision(shot.Action, shot.Target, shot.Statement ?? ""), schema));
            }

            return turns;
        }

        // One example, before it is written as a line or as a turn.
        private readonly struct Shot
        {
            public readonly string Stimulus;
            public readonly string Action;
            public readonly string Target;
            public readonly string Statement;
            public readonly bool Refusal;

            public Shot(string stimulus, string action, string target, string statement, bool refusal = false)
            {
                Stimulus = stimulus;
                Action = action;
                Target = target;
                Statement = statement;
                Refusal = refusal;
            }
        }

        // Every example, in the order both forms send them: each action's example, then its
        // counter-example, then the idle example, then the refusal.
        private static List<Shot> Shots(IReadOnlyList<ActionDefinition> available, TargetRegistry targets, PromptOptions options)
        {
            bool statements = options.ExampleStatements;
            var shots = new List<Shot>();

            foreach (ActionDefinition action in available)
            {
                string target = FirstRegisteredPreferred(action, targets);

                Shot? example = ActionExample(action, target, statements);
                if (example.HasValue)
                {
                    shots.Add(example.Value);
                }

                Shot? counter = options.CounterExamples ? CounterExample(action, target, statements) : (Shot?)null;
                if (counter.HasValue)
                {
                    shots.Add(counter.Value);
                }
            }

            if (!string.IsNullOrWhiteSpace(options.IdleExampleStimulus))
            {
                shots.Add(new Shot(options.IdleExampleStimulus, ActionRegistry.None, TargetRegistry.NoTarget,
                                   statements ? options.IdleExampleStatement : null));
            }

            Shot? refusal = RefusalExample(available, targets);
            if (refusal.HasValue)
            {
                shots.Add(refusal.Value);
            }

            return shots;
        }

        // The action's own example, or null when it has none, or needs a target and has none to name.
        private static Shot? ActionExample(ActionDefinition action, string target, bool statements)
        {
            if (string.IsNullOrWhiteSpace(action.ExampleStimulus)) return null;

            string statement = statements ? action.ExampleStatement : null;

            if (!action.RequiresTarget)
            {
                return new Shot(action.ExampleStimulus, action.Id, TargetRegistry.NoTarget, statement);
            }

            if (target == null) return null;

            string name = DisplayName(target);
            return new Shot(Fill(action.ExampleStimulus, name), action.Id, target, Fill(statement, name));
        }

        // A line that touches the action's subject and asks for nothing, answered with none (#190,
        // Living Vale #9). Its {0}s, in the stimulus and the statement, name the example's target;
        // with no target to name, the line is left out rather than sent with a placeholder in it.
        // Without {0} it is sent as written.
        private static Shot? CounterExample(ActionDefinition action, string target, bool statements)
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

            return new Shot(stimulus, ActionRegistry.None, TargetRegistry.NoTarget, statement);
        }

        // Never a statement: its line would be the framework's own words, sent with every prompt.
        private static Shot? RefusalExample(IReadOnlyList<ActionDefinition> available, TargetRegistry targets)
        {
            string placeholder = targets.Contains(DisplayNameToId(UnknownTarget)) ? UnknownTargetFallback : UnknownTarget;

            foreach (ActionDefinition action in available)
            {
                if (!action.RequiresTarget) continue;
                if (string.IsNullOrWhiteSpace(action.ExampleStimulus)) continue;
                if (!action.ExampleStimulus.Contains("{0}")) continue;

                return new Shot(Fill(action.ExampleStimulus, placeholder), ActionRegistry.None, TargetRegistry.NoTarget, null, refusal: true);
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

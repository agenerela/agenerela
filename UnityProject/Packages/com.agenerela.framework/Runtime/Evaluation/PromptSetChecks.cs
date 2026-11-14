using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Agenerela.Evaluation
{
    /// <summary>
    /// The rules for writing a prompt, checked in code rather than left to a reviewer. Hand-edited
    /// JSON is where a target that is not registered, <c>none</c> without <c>no_target</c>, or a
    /// stimulus that repeats a few-shot example comes from.
    /// </summary>
    public static class PromptSetChecks
    {
        /// <summary>Every rule every prompt in <paramref name="set"/> breaks, in the set's order.</summary>
        /// <param name="profile">
        /// The agent the set is measured against, for its few-shot examples. Null skips
        /// <see cref="PromptRule.StimulusIsExample"/>, which needs one.
        /// </param>
        public static IReadOnlyList<PromptProblem> Check(PromptSet set, AgentProfile profile = null)
        {
            var problems = new List<PromptProblem>();
            foreach (var category in set.Categories)
            {
                foreach (var prompt in category.Prompts)
                {
                    problems.AddRange(CheckPrompt(set, category, prompt, profile));
                }
            }

            return problems;
        }

        /// <summary>Every rule <paramref name="prompt"/> breaks. Its id is checked against the whole set.</summary>
        public static IReadOnlyList<PromptProblem> CheckPrompt(PromptSet set, PromptCategory category, EvalPrompt prompt, AgentProfile profile = null)
        {
            var problems = new List<PromptProblem>();
            void Add(PromptRule rule, string message) => problems.Add(new PromptProblem(prompt.Id, rule, message));

            CheckId(set, category, prompt, Add);

            if (string.IsNullOrWhiteSpace(prompt.Label)) Add(PromptRule.Incomplete, "The label is empty");
            if (string.IsNullOrWhiteSpace(prompt.Stimulus)) Add(PromptRule.Incomplete, "The stimulus is empty");

            if (string.IsNullOrWhiteSpace(prompt.ExpectedAction))
            {
                Add(PromptRule.Incomplete, "No expected action");
            }
            else if (prompt.ExpectedTarget == null)
            {
                Add(PromptRule.Incomplete, "No expected target: use no_target for none");
            }
            else
            {
                CheckAnswer(set, prompt, Add);
            }

            if (profile != null)
            {
                CheckExamples(prompt, profile, Add);
            }

            return problems;
        }

        /// <summary>
        /// Where <paramref name="profile"/> and the actions <paramref name="set"/> declares disagree:
        /// an action only one of them has, or one whose need for a target differs. <c>none</c> is in
        /// every set and no profile. The window shows these and never fixes them.
        /// </summary>
        public static IReadOnlyList<string> CompareWithProfile(PromptSet set, AgentProfile profile)
        {
            var differences = new List<string>();
            var profileActions = profile.Actions.Where(a => a != null && a.Action != null).Select(a => a.Action).ToList();

            foreach (var action in profileActions)
            {
                var declared = set.FindAction(action.Id);
                if (declared == null)
                {
                    differences.Add($"The profile has '{action.Id}', which the set does not declare");
                }
                else if (declared.NeedsTarget != action.RequiresTarget)
                {
                    differences.Add(action.RequiresTarget
                        ? $"'{action.Id}' requires a target in the profile, but the set says it takes none"
                        : $"'{action.Id}' takes no target in the profile, but the set says it needs one");
                }
            }

            foreach (var declared in set.Actions)
            {
                if (declared.Id != ActionRegistry.None && profileActions.All(a => a.Id != declared.Id))
                {
                    differences.Add($"The set declares '{declared.Id}', which the profile does not have");
                }
            }

            return differences;
        }

        /// <summary>
        /// True when <paramref name="stimulus"/> is <paramref name="example"/>, ignoring case, spacing
        /// and end punctuation. A <c>{0}</c> in the example stands for any target.
        /// </summary>
        public static bool MatchesExample(string stimulus, string example)
        {
            if (string.IsNullOrWhiteSpace(stimulus) || string.IsNullOrWhiteSpace(example))
            {
                return false;
            }

            string pattern = Regex.Escape(Normalize(example)).Replace(@"\{0}", ".+");
            return Regex.IsMatch(Normalize(stimulus), "^" + pattern + "$");
        }

        private static void CheckId(PromptSet set, PromptCategory category, EvalPrompt prompt, System.Action<PromptRule, string> add)
        {
            if (string.IsNullOrWhiteSpace(prompt.Id))
            {
                add(PromptRule.Incomplete, "The id is empty");
                return;
            }

            if (!Regex.IsMatch(prompt.Id, "^" + Regex.Escape(category.Prefix) + @"_\d{3,}$"))
            {
                add(PromptRule.IdMalformed, $"The id should be {category.Prefix}_<nnn>, such as {category.Prefix}_001");
            }

            if (set.Prompts.Count(p => p.Id == prompt.Id) > 1)
            {
                add(PromptRule.IdDuplicate, $"Another prompt is also {prompt.Id}");
            }
        }

        private static void CheckAnswer(PromptSet set, EvalPrompt prompt, System.Action<PromptRule, string> add)
        {
            string action = prompt.ExpectedAction;
            var targets = prompt.ExpectedTarget.Ids;
            bool noTarget = targets.Contains(TargetRegistry.NoTarget);

            foreach (string target in targets)
            {
                if (target != TargetRegistry.NoTarget && !set.IsRegisteredTarget(target))
                {
                    add(PromptRule.TargetNotRegistered, $"'{target}' is not a registered target. Declare it in the set first, or use no_target");
                }
            }

            if (action == ActionRegistry.None)
            {
                if (targets.Count != 1 || !noTarget)
                {
                    add(PromptRule.NoneTakesNoTarget, "none always takes no_target, even when the request names a registered target");
                }

                return;
            }

            var declared = set.FindAction(action);
            if (declared == null)
            {
                add(PromptRule.ActionNotDeclared, $"'{action}' is not an action the set declares");
                return;
            }

            if (declared.NeedsTarget && noTarget)
            {
                add(PromptRule.ActionNeedsTarget, $"{action} needs a target, so it cannot take no_target");
            }
            else if (!declared.NeedsTarget && (targets.Count != 1 || !noTarget))
            {
                add(PromptRule.ActionTakesNoTarget, $"{action} takes no target: use no_target");
            }

            if (!declared.IsAvailableIn(prompt.PreconditionState))
            {
                add(PromptRule.PreconditionMismatch,
                    $"{action} is only available when {declared.OnlyWhen.ToString(Newtonsoft.Json.Formatting.None)}; set the state to match");
            }
        }

        /// <summary>
        /// True when every line of an example's situation (<see cref="ActionDefinition.ExampleObservations"/>)
        /// is one of <paramref name="observations"/>, by <see cref="MatchesExample"/>, a <c>{0}</c>
        /// standing for any target. An example with no situation matches any observations, so it is
        /// judged on its stimulus alone, as before (#216 item 1).
        /// </summary>
        public static bool MatchesSituation(IEnumerable<string> observations, IEnumerable<string> situation)
        {
            var seen = (observations ?? Enumerable.Empty<string>()).Where(o => !string.IsNullOrWhiteSpace(o)).ToList();
            foreach (string line in situation ?? Enumerable.Empty<string>())
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                if (!seen.Any(o => MatchesExample(o, line))) return false;
            }

            return true;
        }

        private static bool HasSituation(IEnumerable<string> situation) =>
            situation != null && situation.Any(l => !string.IsNullOrWhiteSpace(l));

        private static void CheckExamples(EvalPrompt prompt, AgentProfile profile, System.Action<PromptRule, string> add)
        {
            CheckExamples(prompt, profile, add, withSituations: true);
        }

        // withSituations: whether the examples are shown with their observations. An arm that shows
        // them without (DecideOptions.ExampleObservations off) shows the bare stimulus, which is then
        // judged alone, as every example was before #216 item 1.
        internal static void CheckExamples(EvalPrompt prompt, AgentProfile profile, System.Action<PromptRule, string> add, bool withSituations)
        {
            // Every line an example shows the model: its stimulus, and its statement (#190, G14).
            void Check(string text, string what)
            {
                if (MatchesExample(prompt.Stimulus, text))
                {
                    add(PromptRule.StimulusIsExample,
                        $"The stimulus is {what} \"{text}\": never score on a few-shot example (hard rule 4)");
                }
            }

            foreach (var asset in profile.Actions)
            {
                var action = asset != null ? asset.Action : null;
                if (action != null)
                {
                    Check(action.ExampleStatement, $"{action.Id}'s example statement");
                    Check(action.CounterExampleStatement, $"{action.Id}'s counter-example statement");
                }

                // An example with a situation (#216 item 1) is its stimulus and its observations
                // together: a prompt that asks the same thing in another situation is a new prompt.
                if (action != null && MatchesExample(prompt.Stimulus, action.ExampleStimulus) &&
                    (!withSituations || MatchesSituation(prompt.Observations, action.ExampleObservations)))
                {
                    add(PromptRule.StimulusIsExample, HasSituation(action.ExampleObservations) && withSituations
                        ? $"The stimulus and observations are {action.Id}'s example \"{action.ExampleStimulus}\" in its situation: never score on a few-shot example (hard rule 4)"
                        : $"The stimulus is {action.Id}'s example \"{action.ExampleStimulus}\": never score on a few-shot example (hard rule 4)");
                }

                // Shown answered with none, so scoring on it measures nothing either (#190).
                if (action != null && MatchesExample(prompt.Stimulus, action.CounterExampleStimulus))
                {
                    add(PromptRule.StimulusIsExample,
                        $"The stimulus is {action.Id}'s counter-example \"{action.CounterExampleStimulus}\": never score on a few-shot example (hard rule 4)");
                }
            }

            if (MatchesExample(prompt.Stimulus, profile.IdleExampleStimulus))
            {
                add(PromptRule.StimulusIsExample,
                    $"The stimulus is the profile's idle example \"{profile.IdleExampleStimulus}\": never score on a few-shot example (hard rule 4)");
            }

            Check(profile.IdleExampleStatement, "the profile's idle example statement");
        }

        // Lower case, single spaces, no end punctuation or quotes: "Proceed to the Bridge!" is
        // still the example "Proceed to the {0}.".
        private static string Normalize(string text)
        {
            string lower = Regex.Replace(text.ToLowerInvariant(), @"\s+", " ").Trim();
            return lower.Trim('.', '!', '?', ',', ';', ':', '"', '\'', ' ');
        }
    }
}

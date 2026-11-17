using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;

namespace Agenerela.Evaluation
{
    /// <summary>
    /// The mechanical checks <see cref="EvalRunner"/> makes on every answer's <c>statement</c>, the
    /// line the agent says: does it repeat a few-shot line, is it empty, too long, or does it show
    /// the machinery behind it (<see cref="ReplyFlag"/>). A second score, beside the five outcomes
    /// and never part of them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Only checks that need no judgement, so a count means the same thing in every run. Whether a
    /// statement promises an action the agent did not take ("I will stand guard here" with
    /// <c>none</c>) needs a judge, and a judge's numbers mean nothing until it has been validated
    /// against people's labels (hard rule 2), so it is not here.
    /// </para>
    /// <para>
    /// The same checks apply to every prompt of a set; nothing in the set's format turns one on or
    /// off.
    /// </para>
    /// </remarks>
    public static class ReplyChecks
    {
        /// <summary>
        /// The stock phrases of a chat assistant, which no game character says: matched as whole
        /// words, ignoring case. Kept short on purpose, since each one is a guess at what a model
        /// slips into, until a run shows another.
        /// </summary>
        public static readonly IReadOnlyList<string> AssistantPhrases = new ReadOnlyCollection<string>(new[]
        {
            "as an AI", "I am an AI", "I'm an AI", "AI assistant", "language model",
        });

        // The schema's fields, each a leak when the statement uses it as a label: "action: move_to",
        // or "action": as JSON writes it.
        private static readonly Regex FieldLabel = new Regex(
            @"(?<![A-Za-z0-9_])(" + string.Join("|", new[]
            {
                DecisionSchema.ActionFieldName, DecisionSchema.TargetFieldName, DecisionSchema.StatementFieldName,
            }.Select(Regex.Escape)) + @")[""']?\s*:",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        private static readonly Regex Phrases = new Regex(
            @"(?<![A-Za-z0-9])(" + string.Join("|", AssistantPhrases.Select(p => Regex.Escape(p).Replace(@"\ ", @"\s+"))) + @")(?![A-Za-z0-9])",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        /// <summary>
        /// What is wrong with <paramref name="statement"/>, an answer to a prompt of
        /// <paramref name="set"/> asked of <paramref name="profile"/> in <paramref name="arm"/>:
        /// one finding per flag it carries, in <see cref="ReplyFlag"/>'s order, or none.
        /// </summary>
        /// <param name="asksForStatement">
        /// Whether the request's schema asked for a statement, as a decision's always does; only
        /// then is an empty one <see cref="ReplyFlag.Empty"/>.
        /// </param>
        /// <exception cref="ArgumentNullException"><paramref name="set"/>, <paramref name="profile"/> or <paramref name="arm"/> is null.</exception>
        public static IReadOnlyList<ReplyFinding> Check(string statement, PromptSet set, AgentProfile profile, EvalArm arm,
                                                        bool asksForStatement = true)
        {
            return new Checker(set, profile, arm).Check(statement, asksForStatement);
        }

        /// <summary>
        /// The checks for one arm, made once: its few-shot lines, the ids that would be a leak, and
        /// its length limit.
        /// </summary>
        internal sealed class Checker
        {
            private readonly List<(string Text, string What)> examples = new List<(string Text, string What)>();
            private readonly List<(Regex Pattern, string Id)> ids = new List<(Regex Pattern, string Id)>();
            private readonly int maxLength;

            public Checker(PromptSet set, AgentProfile profile, EvalArm arm)
            {
                if (set == null) throw new ArgumentNullException(nameof(set));
                if (profile == null) throw new ArgumentNullException(nameof(profile));
                if (arm == null) throw new ArgumentNullException(nameof(arm));

                maxLength = arm.MaxStatementLength;

                // Every line a few-shot example can show, as PromptSetChecks and the runner check
                // stimuli against: whether or not this arm's switches showed it, a repeat of it is
                // still a line written for an example, not for this prompt.
                foreach (var asset in profile.Actions)
                {
                    var action = asset != null ? asset.Action : null;
                    if (action == null)
                    {
                        continue;
                    }

                    examples.Add((action.ExampleStimulus, $"{action.Id}'s example"));
                    examples.Add((action.ExampleStatement, $"{action.Id}'s example statement"));
                    examples.Add((action.CounterExampleStimulus, $"{action.Id}'s counter-example"));
                    examples.Add((action.CounterExampleStatement, $"{action.Id}'s counter-example statement"));
                }

                examples.Add((profile.IdleExampleStimulus, "the profile's idle example"));
                examples.Add((profile.IdleExampleStatement, "the profile's idle example statement"));
                examples.Add((arm.Options.IdleExampleStimulus, $"arm '{arm.Name}''s idle example"));
                examples.Add((arm.Options.IdleExampleStatement, $"arm '{arm.Name}''s idle example statement"));
                examples.RemoveAll(e => string.IsNullOrWhiteSpace(e.Text));

                // An id is a leak only when it is not also a word: "move_to" and "no_target" are
                // nobody's speech, but "bridge" and "none" are how anyone would say them.
                var all = set.Actions.Where(a => a != null).Select(a => a.Id)
                    .Concat(profile.Actions.Where(a => a != null && a.Action != null).Select(a => a.Action.Id))
                    .Concat(set.Targets.Where(t => t != null).Select(t => t.Id))
                    .Concat(new[] { ActionRegistry.None, TargetRegistry.NoTarget })
                    .Where(id => !string.IsNullOrWhiteSpace(id) && !id.All(char.IsLetter))
                    .Distinct(StringComparer.OrdinalIgnoreCase);
                foreach (string id in all)
                {
                    ids.Add((new Regex(@"(?<![A-Za-z0-9_])" + Regex.Escape(id) + @"(?![A-Za-z0-9_])",
                        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant), id));
                }
            }

            public List<ReplyFinding> Check(string statement, bool asksForStatement)
            {
                var findings = new List<ReplyFinding>();
                if (string.IsNullOrWhiteSpace(statement))
                {
                    if (asksForStatement)
                    {
                        findings.Add(new ReplyFinding { Flag = ReplyFlag.Empty, Detail = "the schema asked for a statement" });
                    }

                    return findings;
                }

                var echoed = examples.Where(e => PromptSetChecks.MatchesExample(statement, e.Text))
                    .Select(e => $"{e.What} \"{e.Text}\"")
                    .Distinct()
                    .ToList();
                if (echoed.Count > 0)
                {
                    findings.Add(new ReplyFinding { Flag = ReplyFlag.Echo, Detail = string.Join("; ", echoed) });
                }

                int length = statement.Trim().Length;
                if (length > maxLength)
                {
                    findings.Add(new ReplyFinding { Flag = ReplyFlag.TooLong, Detail = $"{length} characters, over {maxLength}" });
                }

                var leaks = Leaks(statement);
                if (leaks.Count > 0)
                {
                    findings.Add(new ReplyFinding { Flag = ReplyFlag.Leak, Detail = string.Join(", ", leaks) });
                }

                return findings;
            }

            private List<string> Leaks(string statement)
            {
                var leaks = new List<string>();
                leaks.AddRange(ids.Where(i => i.Pattern.IsMatch(statement)).Select(i => $"the id {i.Id}"));
                leaks.AddRange(FieldLabel.Matches(statement).Cast<Match>()
                    .Select(m => m.Groups[1].Value.ToLowerInvariant())
                    .Distinct()
                    .Select(field => $"the field label {field}:"));
                if (statement.IndexOf('{') >= 0 || statement.IndexOf('}') >= 0)
                {
                    leaks.Add("a JSON brace");
                }

                // A typographic apostrophe is still "I'm an AI".
                string plain = statement.Replace('’', '\'').Replace('‘', '\'');
                leaks.AddRange(Phrases.Matches(plain).Cast<Match>()
                    .Select(m => AssistantPhrases.First(p => string.Equals(
                        Regex.Replace(p, @"\s+", " "), Regex.Replace(m.Value, @"\s+", " "), StringComparison.OrdinalIgnoreCase)))
                    .Distinct()
                    .Select(p => $"\"{p}\""));
                return leaks;
            }
        }
    }
}

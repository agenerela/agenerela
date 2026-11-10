using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

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
        /// and <see cref="PromptOptions.CounterExamples"/>, and the settings of #190's G14 and Living
        /// Vale #20. Each is off, or shows only what a developer wrote, by default.
        /// </para>
        /// <list type="bullet">
        ///   <item><description><see cref="PromptOptions.ExampleStatements"/>: an example whose
        ///   <see cref="ActionDefinition.ExampleStatement"/> or <see cref="ActionDefinition.CounterExampleStatement"/>
        ///   is set, and the idle example when <see cref="PromptOptions.IdleExampleStatement"/> is, ends with
        ///   <c>, statement: "..."</c>. The refusal of a target that is not here never has one: its line would
        ///   be the framework's own words, sent with every prompt.</description></item>
        ///   <item><description><see cref="PromptOptions.ExampleTargetFallback"/>: an example whose action
        ///   has no registered target among its <see cref="ActionDefinition.PreferredExampleTargets"/> names
        ///   the first offered target the action takes that <see cref="PromptOptions.Stimulus"/> does not
        ///   mention, instead of being left out.</description></item>
        ///   <item><description><see cref="PromptOptions.SpacedExampleNames"/>: a target is called by its
        ///   id's words ("north tower", not "NorthTower"), and the refusal drops the article before the
        ///   missing target ("stand by Atlantis", not "stand by the Atlantis").</description></item>
        ///   <item><description>A target that implements <see cref="INamedTarget"/> is called by its name
        ///   wherever an example writes <c>{0}</c>; any other by the name made from its id.</description></item>
        /// </list>
        /// </remarks>
        public static string Build(IReadOnlyList<ActionDefinition> available, TargetRegistry targets, PromptOptions options)
        {
            options = options ?? new PromptOptions();
            string label = options.StimulusLabel;
            bool statements = options.ExampleStatements;
            bool spaced = options.SpacedExampleNames;
            HashSet<string> mentioned = options.ExampleTargetFallback ? Words(options.Stimulus) : null;

            List<string> lines = new List<string> { Header };

            foreach (ActionDefinition action in available)
            {
                string target = FirstRegisteredPreferred(action, targets)
                                ?? (mentioned != null ? FallbackTarget(action, targets, mentioned, spaced) : null);
                string name = target == null ? null : NameOf(target, targets, spaced);

                string example = ActionExample(action, target, name, label, statements);
                if (example != null)
                {
                    lines.Add(example);
                }

                string counter = options.CounterExamples ? CounterExample(action, name, label, statements) : null;
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

            string refusal = RefusalExample(available, targets, label, spaced);
            if (refusal != null)
            {
                lines.Add(refusal);
            }

            return string.Join("\n", lines);
        }

        // The action's own example, or null when it has none, or needs a target and has none to name.
        private static string ActionExample(ActionDefinition action, string target, string name,
                                            string label, bool statements)
        {
            if (string.IsNullOrWhiteSpace(action.ExampleStimulus)) return null;

            string statement = statements ? action.ExampleStatement : null;

            if (!action.RequiresTarget)
            {
                return Example(label, action.ExampleStimulus, action.Id, TargetRegistry.NoTarget, statement);
            }

            if (target == null) return null;

            return Example(label, Fill(action.ExampleStimulus, name), action.Id, target, Fill(statement, name));
        }

        // A line that touches the action's subject and asks for nothing, answered with none (#190,
        // Living Vale #9). Its {0}s, in the stimulus and the statement, name the example's target;
        // with no target to name, the line is left out rather than sent with a placeholder in it.
        // Without {0} it is sent as written.
        private static string CounterExample(ActionDefinition action, string name, string label, bool statements)
        {
            string stimulus = action.CounterExampleStimulus;
            if (string.IsNullOrWhiteSpace(stimulus)) return null;

            string statement = statements ? action.CounterExampleStatement : null;

            if (HasPlaceholder(stimulus) || HasPlaceholder(statement))
            {
                if (name == null) return null;

                stimulus = Fill(stimulus, name);
                statement = Fill(statement, name);
            }

            return Example(label, stimulus, ActionRegistry.None, TargetRegistry.NoTarget, statement);
        }

        // One refusal of a target that is not here, worded from the first targeted action's own
        // stimulus, naming a place no offered target is called, by id or by the name an example
        // gives it; with both placeholders taken there is no refusal, rather than one contradicting
        // an example. Spaced, the stimulus reads as English around the placeholder (FitArticle).
        private static string RefusalExample(IReadOnlyList<ActionDefinition> available, TargetRegistry targets,
                                             string stimulusLabel, bool spaced)
        {
            string placeholder = IsOffered(UnknownTarget, targets, spaced) ? UnknownTargetFallback : UnknownTarget;
            if (IsOffered(placeholder, targets, spaced)) return null;

            foreach (ActionDefinition action in available)
            {
                if (!action.RequiresTarget) continue;
                if (string.IsNullOrWhiteSpace(action.ExampleStimulus)) continue;
                if (!action.ExampleStimulus.Contains("{0}")) continue;

                string stimulus = spaced ? FitArticle(action.ExampleStimulus) : action.ExampleStimulus;
                return Example(stimulusLabel, Fill(stimulus, placeholder), "none", TargetRegistry.NoTarget + " (not an available target)", null);
            }

            return null;
        }

        // Whether an offered target is called placeholder: its id ("atlantis") or the name an example
        // would give it, an INamedTarget's included.
        private static bool IsOffered(string placeholder, TargetRegistry targets, bool spaced)
        {
            foreach (string id in targets.Ids)
            {
                if (string.Equals(id, DisplayNameToId(placeholder), StringComparison.OrdinalIgnoreCase)) return true;
                if (string.Equals(NameOf(id, targets, spaced), placeholder, StringComparison.OrdinalIgnoreCase)) return true;
            }

            return false;
        }

        // The refusal's stimulus with its article fitted to a proper name. An article goes where {0}
        // ends its noun phrase, at the end, before punctuation or before a short word that starts
        // something else ("for", "now", "please"): "stand by the {0}." gives "stand by {0}.", "Fetch
        // an {0} for me." "Fetch {0} for me.". One before a {0} that goes on into a longer phrase
        // stays, as "an" for the vowel both placeholders start with: "Open the {0} gate." and "Bring
        // an {0}-shaped token."
        private static string FitArticle(string text)
        {
            text = ArticleEndingThePhrase.Replace(text, "{0}");
            return ArticleA.Replace(text, m => (m.Groups[1].Value == "A" ? "An" : "an") + m.Groups[2].Value + "{0}");
        }

        private static readonly Regex ArticleEndingThePhrase = new Regex(
            @"\b(?:the|an|a)\s+\{0\}(?=$|[.,!?;:)""'\u2019]|\s+(?:for|to|at|in|on|by|with|from|and|or|but|so|if|when|now|please|here|there|again|first|then|today|tonight|before|after)\b)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        private static readonly Regex ArticleA = new Regex(@"\b([Aa])(\s+)\{0\}", RegexOptions.CultureInvariant);

        // The first offered target the action takes that the stimulus does not mention (Living Vale
        // #20), so a game whose targets change every turn needs no list kept up to date, and the
        // example never shows the model the answer to the request it is sent with. Null when there is
        // none.
        private static string FallbackTarget(ActionDefinition action, TargetRegistry targets,
                                              HashSet<string> mentioned, bool spaced)
        {
            foreach (string id in targets.Ids)
            {
                if (!action.AcceptsCategory(targets.CategoryOf(id))) continue;
                if (Mentions(mentioned, id, NameOf(id, targets, spaced))) continue;

                return id;
            }

            return null;
        }

        // Whether the stimulus says a word of the target's id or name: the word itself, of any length
        // ("ox"); a word that starts with it, from three letters ("inn" in innkeeper, "tower" in
        // towers); or a word of four letters or more that starts it ("dock" for docks). Generous on
        // purpose: a skipped target costs only a different example, a named one shows the answer. A
        // few words any name might hold ("the", "of") never count, unless the name holds nothing
        // else, and then all of them must be said.
        private static bool Mentions(HashSet<string> stimulusWords, string id, string name)
        {
            if (stimulusWords.Count == 0) return false;

            var names = Words(SplitId(id));
            names.UnionWith(Words(name));

            bool anyCounted = false;
            foreach (string word in names)
            {
                if (NeverAMention(word)) continue;
                anyCounted = true;

                foreach (string said in stimulusWords)
                {
                    if (said == word) return true;
                    if (word.Length >= 3 && said.StartsWith(word, StringComparison.Ordinal)) return true;
                    if (said.Length >= 4 && word.StartsWith(said, StringComparison.Ordinal)) return true;
                }
            }

            if (anyCounted || names.Count == 0) return false;
            foreach (string word in names)
            {
                if (!stimulusWords.Contains(word)) return false;
            }

            return true;
        }

        private static bool NeverAMention(string word)
        {
            switch (word)
            {
                case "the": case "a": case "an": case "of": case "to": case "and": case "for": case "with": case "from":
                    return true;
                default:
                    return false;
            }
        }

        // The lowercase words of a text, letters and digits only. Empty for null.
        private static HashSet<string> Words(string text)
        {
            var words = new HashSet<string>(StringComparer.Ordinal);
            if (string.IsNullOrEmpty(text)) return words;

            var word = new StringBuilder();
            foreach (char c in text)
            {
                if (char.IsLetterOrDigit(c))
                {
                    word.Append(char.ToLowerInvariant(c));
                }
                else if (word.Length > 0)
                {
                    words.Add(word.ToString());
                    word.Clear();
                }
            }

            if (word.Length > 0) words.Add(word.ToString());
            return words;
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

        // What an example calls a target: its INamedTarget name when it gives one, otherwise the name
        // made from its id, spaced ("north tower") or run together ("NorthTower").
        private static string NameOf(string id, TargetRegistry targets, bool spaced)
        {
            targets.TryGet(id, out object target);
            string name = (target as INamedTarget)?.Name;
            if (!string.IsNullOrWhiteSpace(name)) return name.Trim();

            return spaced ? SplitId(id).ToLowerInvariant() : DisplayName(id);
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

        // "north_tower" -> "NorthTower": the name every example used before SpacedExampleNames.
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

        // An id's words with single spaces between them, case kept: "north_tower" -> "north tower",
        // "village-gate" -> "village gate", "NorthTower" -> "North Tower", and an acronym kept whole
        // before the next word: "NPCGuard" -> "NPC Guard".
        private static string SplitId(string id)
        {
            var words = new StringBuilder(id.Length + 4);

            for (int i = 0; i < id.Length; i++)
            {
                char c = id[i];
                if (c == '_' || c == '-' || char.IsWhiteSpace(c))
                {
                    if (words.Length > 0 && words[words.Length - 1] != ' ') words.Append(' ');
                    continue;
                }

                char previous = i > 0 ? id[i - 1] : ' ';
                char next = i + 1 < id.Length ? id[i + 1] : ' ';
                bool wordStarts = char.IsUpper(c)
                                  && (char.IsLower(previous) || (char.IsUpper(previous) && char.IsLower(next)));
                if (wordStarts && words.Length > 0 && words[words.Length - 1] != ' ') words.Append(' ');

                words.Append(c);
            }

            return words.ToString().Trim();
        }

        private static string DisplayNameToId(string display) => display.ToLowerInvariant();
    }
}

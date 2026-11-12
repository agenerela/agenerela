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

        // The same, as a common noun, for a refusal whose {0} keeps its article because the
        // phrase goes on past it ("The {0} is unstaffed."), where a proper name would not read.
        private const string UnknownThing = "sunken city";
        private const string UnknownThingFallback = "lost city";

        /// <summary>
        /// The example block for the actions available right now: each action's example, then its
        /// counter-example when it has one, then the idle example, then one refusal of a target
        /// that is not here. Empty when there is not one example to show, so a request then carries
        /// no example block at all, not its header alone (#244 item 3).
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
        ///   <item><description>A target that implements <see cref="INamedTarget"/> is called by its name
        ///   wherever an example writes <c>{0}</c>; any other by the name made from its id.</description></item>
        /// </list>
        /// <para>
        /// The block <see cref="Agent"/> builds for a decision also knows that decision's availability:
        /// for an action whose handler has a per-target check (<see cref="ITargetAvailability"/>), an
        /// example never names a target the check rejected or one outside the action's
        /// <see cref="ActionDefinition.TargetCategories"/>, a preferred target included; it takes the
        /// next preferred one, or the fallback, or is left out. An action with no such check is shown
        /// exactly as before. This overload has no decision behind it, so it cannot skip them.
        /// </para>
        /// </remarks>
        public static string Build(IReadOnlyList<ActionDefinition> available, TargetRegistry targets, PromptOptions options)
        {
            return Build(available, targets, options, null);
        }

        // As above, with the decision's availability answer, which Agent passes: an example then never
        // names a target its action cannot take right now, a preferred one included (#235 follow-up).
        // Only an action with a per-target check is affected; with no answer, or no check, the block
        // is what it always was, byte for byte.
        internal static string Build(IReadOnlyList<ActionDefinition> available, TargetRegistry targets, PromptOptions options,
                                     AvailabilityAnswer availability)
        {
            options = options ?? new PromptOptions();
            string label = options.StimulusLabel;
            bool statements = options.ExampleStatements;
            HashSet<string> mentioned = options.ExampleTargetFallback ? Words(options.Stimulus) : null;

            List<string> lines = new List<string> { Header };

            foreach (ActionDefinition action in available)
            {
                string target = FirstRegisteredPreferred(action, targets, availability)
                                ?? (mentioned != null ? FallbackTarget(action, targets, mentioned, availability) : null);
                string name = target == null ? null : NameOf(target, targets);

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

            string refusal = RefusalExample(available, targets, label);
            if (refusal != null)
            {
                lines.Add(refusal);
            }

            // No example at all: no block, rather than a header with nothing under it (#244 item 3).
            if (lines.Count == 1)
            {
                return "";
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
        // an example.
        //
        // The stimulus reads as English around the placeholder (#190, Living Vale #20). An article that ends its
        // phrase at {0} goes, and a proper name takes its place: "Go and stand by the {0}." gives
        // "Go and stand by Atlantis.". Where the phrase goes on past {0}, the article stays and a
        // common noun does: "The {0} is unstaffed." gives "The sunken city is unstaffed.", "Open the
        // {0} gate." "Open the sunken city gate.", with "an" as "a".
        private static string RefusalExample(IReadOnlyList<ActionDefinition> available, TargetRegistry targets,
                                             string stimulusLabel)
        {
            foreach (ActionDefinition action in available)
            {
                if (!action.RequiresTarget) continue;
                if (string.IsNullOrWhiteSpace(action.ExampleStimulus)) continue;
                if (!action.ExampleStimulus.Contains("{0}")) continue;

                string stimulus = action.ExampleStimulus;
                string placeholder;
                string stripped = ArticleEndingThePhrase.Replace(stimulus, "{0}");
                if (ArticleBeforePlaceholder.IsMatch(stripped))
                {
                    // Every article kept, so a {0} whose phrase ends there reads "the sunken city" too.
                    stimulus = AnBeforePlaceholder.Replace(stimulus, m => (m.Groups[1].Value == "An" ? "A" : "a") + m.Groups[2].Value + "{0}");
                    placeholder = Unused(UnknownThing, UnknownThingFallback, targets);
                }
                else
                {
                    stimulus = stripped;
                    placeholder = Unused(UnknownTarget, UnknownTargetFallback, targets);
                }

                if (placeholder == null) return null;
                return Example(stimulusLabel, Fill(stimulus, placeholder), "none", TargetRegistry.NoTarget + " (not an available target)", null);
            }

            return null;
        }

        // The first of two placeholders no offered target is called; null when both are.
        private static string Unused(string first, string second, TargetRegistry targets)
        {
            if (!IsOffered(first, targets)) return first;
            return IsOffered(second, targets) ? null : second;
        }

        // Whether an offered target is called placeholder: its id ("atlantis") or the name an example
        // would give it, an INamedTarget's included.
        private static bool IsOffered(string placeholder, TargetRegistry targets)
        {
            foreach (string id in targets.Ids)
            {
                if (string.Equals(id, DisplayNameToId(placeholder), StringComparison.OrdinalIgnoreCase)) return true;
                if (string.Equals(SplitId(id), placeholder, StringComparison.OrdinalIgnoreCase)) return true;
                if (string.Equals(NameOf(id, targets), placeholder, StringComparison.OrdinalIgnoreCase)) return true;
            }

            return false;
        }

        // An article whose phrase ends at {0}: at the end, before punctuation, or before a short word
        // that starts something else ("for", "now", "please").
        private static readonly Regex ArticleEndingThePhrase = new Regex(
            @"\b(?:the|an|a)\s+\{0\}(?=$|[.,!?;:)""'\u2019]|\s+(?:for|to|at|in|on|by|with|from|and|or|but|so|if|when|now|please|here|there|again|first|then|today|tonight|before|after)\b)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        private static readonly Regex ArticleBeforePlaceholder =
            new Regex(@"\b(?:the|an|a)\s+\{0\}", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        private static readonly Regex AnBeforePlaceholder = new Regex(@"\b(An|an)(\s+)\{0\}", RegexOptions.CultureInvariant);

        // The first offered target the action takes that the stimulus does not mention (Living Vale
        // #20), so a game whose targets change every turn needs no list kept up to date, and the
        // example never shows the model the answer to the request it is sent with. Null when there is
        // none.
        private static string FallbackTarget(ActionDefinition action, TargetRegistry targets,
                                              HashSet<string> mentioned, AvailabilityAnswer availability)
        {
            foreach (string id in targets.Ids)
            {
                if (!action.AcceptsCategory(targets.CategoryOf(id))) continue;
                if (!MayName(action, id, targets, availability)) continue;
                if (Mentions(mentioned, id, NameOf(id, targets))) continue;

                return id;
            }

            return null;
        }

        // Whether the stimulus says a word of the target's id or name: the word itself, of any length
        // ("ox"); a word that starts with it, from three letters ("inn" in innkeeper, "tower" in
        // towers); or a word of four letters or more that starts it ("dock" for docks). Generous on
        // purpose: a skipped target costs only a different example, a named one shows the answer. A
        // few words any name might hold ("the", "of") never count, unless the name holds nothing
        // else, and then all of them must be said. The id's words and the name's are read apart, so
        // one cannot hide the other's.
        private static bool Mentions(HashSet<string> stimulusWords, string id, string name)
        {
            if (stimulusWords.Count == 0) return false;

            return SaysAny(stimulusWords, Words(SplitId(id))) || SaysAny(stimulusWords, Words(name));
        }

        private static bool SaysAny(HashSet<string> stimulusWords, HashSet<string> names)
        {
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

        // The first preferred target that is registered and, for an action with a per-target check,
        // one the action can take right now; a rejected one is skipped for the next.
        private static string FirstRegisteredPreferred(ActionDefinition action, TargetRegistry targets,
                                                       AvailabilityAnswer availability)
        {
            if (action.PreferredExampleTargets == null) return null;

            foreach (string candidate in action.PreferredExampleTargets)
            {
                if (!string.IsNullOrWhiteSpace(candidate) && targets.Contains(candidate) &&
                    MayName(action, candidate, targets, availability))
                {
                    return candidate;
                }
            }

            return null;
        }

        private static bool MayName(ActionDefinition action, string id, TargetRegistry targets, AvailabilityAnswer availability)
        {
            return availability == null || availability.ExampleMayName(action, id, targets.CategoryOf(id));
        }

        // What an example calls a target: its INamedTarget name when it gives one, otherwise the name
        // made from its id's words, lowercase: "north_tower" is "north tower" (#190, Living Vale #20;
        // before it, the words were run together, "NorthTower").
        private static string NameOf(string id, TargetRegistry targets)
        {
            targets.TryGet(id, out object target);
            string name = (target as INamedTarget)?.Name;
            if (!string.IsNullOrWhiteSpace(name)) return name.Trim();

            return SplitId(id).ToLowerInvariant();
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

        // Each {0} as the target's name, its first letter capitalised where it starts a sentence: at the
        // start of the text or after ". ", "! " or "? ", an opening quote included ("eastmarch" opens
        // "Eastmarch massed troops."). Mid-sentence it stays lowercase; a proper noun that must keep
        // its capital everywhere is what INamedTarget is for.
        private static string Fill(string text, string target)
        {
            if (text == null || target == null) return text?.Replace("{0}", target);

            string opening = target.Length == 0 ? target : char.ToUpperInvariant(target[0]) + target.Substring(1);
            text = SentenceStartPlaceholder.Replace(text, m => m.Groups[1].Value + opening);
            return text.Replace("{0}", target);
        }

        private static readonly Regex SentenceStartPlaceholder =
            new Regex(@"(^\s*[""'‘“]?|[.!?][""'’”]?\s+[""'‘“]?)\{0\}", RegexOptions.CultureInvariant);

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

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using Agenerela.Providers;

namespace Agenerela
{
    /// <summary>
    /// Assembles the <see cref="DecisionRequest"/> a provider sends: who the agent is, the
    /// examples, what it knows right now, the stimulus and the schema. This is the one place the
    /// framework decides what the model reads.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Short and flat on purpose. Moving rules out of prose and into the schema took the
    /// prototype's prompt from 332 tokens to 143 while accuracy rose from 58.5% to 84.9%
    /// (Appendix A), and two of the three regressions its A/B harness caught were prompt wording
    /// changes. So the system prompt is the identity and one fixed instruction line, with no
    /// emphasis and never a prose rule for something the schema enforces: "Do not repeat an
    /// action you are already doing" is the rule that failed, and state masking (#7) now does it
    /// structurally.
    /// </para>
    /// <para>
    /// Developer text goes in verbatim. The identity fields are only trimmed of surrounding
    /// whitespace, because they sit inside a sentence; observations and the stimulus are not
    /// touched at all, except that a call's own observations placed after the stimulus
    /// (<see cref="ObservationPlacement.WithStimulus"/>) are each put on one line and ended as a
    /// sentence, since they share the stimulus's line. The framework never classifies the stimulus or assumes a speaking
    /// character (DR-008): the label is whatever the caller passed.
    /// </para>
    /// <para>
    /// Deterministic: <c>\n</c> line endings on every platform, and the same input gives
    /// byte-identical output, which the golden-file test depends on.
    /// </para>
    /// <para>
    /// Two additions are made only on request. The target rule is one line, the fallback for a
    /// provider that cannot report token probabilities, without which <see cref="TargetGroundingGuard"/>
    /// cannot run (build plan §2.5, DR-016). The action and target list is for a provider that never
    /// reads the schema (#190). With <see cref="PromptOptions.TargetRule"/> and
    /// <see cref="PromptOptions.ListActionsAndTargets"/> off, the defaults and what <c>Agent</c> sends
    /// to Ollama, the prompt is exactly what it was without them.
    /// </para>
    /// </remarks>
    public static class PromptBuilder
    {
        // #16's golden text. It names no player and no character, because the agent may be a
        // country reading a report, and no_target is written once, in TargetRegistry.
        private const string InstructionLine =
            "Choose exactly one action from the allowed list. Use " + TargetRegistry.NoTarget +
            " when none of the listed targets applies.";

        // The target rule around its list of ids (#107). The probe's RULE, which DR-016 measured,
        // pointed at an "Around you" block this prompt does not have, and said "the player", which
        // DR-008 rules out. So this line carries the list itself and names no one: an adaptation,
        // and unmeasured.
        private const string TargetRuleStart = "Only these things are here: ";

        private const string TargetRuleEnd =
            ". If you are asked to act on anything else, choose action " + ActionRegistry.None +
            " and target " + TargetRegistry.NoTarget +
            ", and say it is not here. Never act on a different thing instead.";

        // The same rule for a schema with no statement (#266, DecideOptions.AskForStatement): it
        // cannot ask the agent to say anything. New and unmeasured.
        private const string TargetRuleEndSilent =
            ". If you are asked to act on anything else, choose action " + ActionRegistry.None +
            " and target " + TargetRegistry.NoTarget +
            ". Never act on a different thing instead.";

        // Internal so an example's situation is rendered under the same heading (#216 item 1).
        internal const string ObservationsHeader = "What you know right now:";

        // The identity's standing knowledge and statement rules (#190, Living Vale #14), after the
        // goals. "What you know:" sets the knowledge against the observations' "What you know right
        // now:"; the rules line is framed like "Your goals:" and named for the statement field,
        // never for speech (DR-008). New and unmeasured as wording, and here alone so an A/B can
        // change them in one place. The rules line has three wordings (StatementRulesWording, #244
        // item 8): the first reads like the statement field already filled in, which a 2B model
        // copied out as its statement (GreyBoxStrategy gap N9).
        private const string KnowledgeHeader = "What you know:";
        private const string StatementRulesPrefix = "Your statement: ";
        private const string StatementRulesHowItReads = "How your statement should read: ";
        private const string StatementRulesOwnWords = "Write your statement in your own words, following these rules: ";

        // The action and target list's headings (#190). "Allowed" echoes the instruction line's
        // "from the allowed list"; the target list is the "listed targets" it names. New and
        // unmeasured, and here alone so the A/B can change them in one place.
        private const string ActionsHeader = "Allowed actions:";
        private const string TargetsHeader = "Targets:";

        // The legal pairs' wording (#151, LegalTargetListing). New, and here alone so an A/B can
        // change it in one place. "Listed" because the cap may leave targets out of the list.
        private const string TargetNoneNote = "Target: " + TargetRegistry.NoTarget + ".";
        private const string TargetsUnlistedNote = "Targets: none listed.";
        private const string ForPrefix = "for ";

        // The legal pairs as one observation line (#244 item 5, LegalTargetListing.ObservationLine),
        // after GreyBoxStrategy's own "Moves open to us:" line, which measured better than the list
        // (#247). "you" because the system prompt speaks to the agent; a game's facts may say "us".
        private const string MovesLinePrefix = "Moves open to you: ";

        // The user turn of a refusal line's request (#109): {0} is the stimulus, {1} the guard's
        // reason. New and unmeasured. It changes only what is said after a refusal, never which
        // action runs, and is here alone so Phase 4 can change it in one place.
        private const string RefusalLineTemplate =
            "You were asked: \"{0}\" You will not do it, because {1} Say so in one short sentence, in character.";

        /// <summary>
        /// The request for one decision, every field filled: the system prompt from
        /// <c>ctx.Identity</c>, <paramref name="fewShotBlock"/> as given, the observations block
        /// from <c>ctx.Observations</c>, <paramref name="history"/>, the stimulus from
        /// <c>ctx.Stimulus</c> labelled <c>Label: "text"</c>, and <paramref name="schema"/>.
        /// </summary>
        /// <remarks>
        /// <see cref="PromptOptions.ObservationPlacement"/> says where the last
        /// <see cref="PromptOptions.CallObservationCount"/> observations, the call's own, go: at the
        /// end of the block (the default), or after the stimulus's closing quote in the user turn (#151).
        /// </remarks>
        /// <param name="ctx">What the agent knows at this decision.</param>
        /// <param name="schema">The schema <see cref="DecisionSchema.Build"/> made from the same context.</param>
        /// <param name="fewShotBlock">
        /// The example block, already built by <see cref="FewShotBuilder.Build"/> with the same
        /// <see cref="PromptOptions.StimulusLabel"/> and with
        /// <see cref="PromptOptions.IdleExampleStimulus"/>. Placed in the request unchanged; null
        /// or empty means no examples. Passed in rather than built here so that an A/B run can
        /// swap or drop this block alone, the rest of the request byte-identical.
        /// </param>
        /// <param name="history">Previous turns, oldest first. Null becomes an empty list.</param>
        /// <param name="options">Null uses the defaults.</param>
        /// <exception cref="ArgumentNullException"><paramref name="ctx"/> or <paramref name="schema"/> is null.</exception>
        /// <exception cref="ArgumentException"><see cref="PromptOptions.StimulusLabel"/> is null, empty or whitespace.</exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <see cref="PromptOptions.MaxListedTargets"/> or <see cref="PromptOptions.CallObservationCount"/> is
        /// negative, or <see cref="PromptOptions.ObservationPlacement"/> is not a defined value.
        /// </exception>
        public static DecisionRequest Build(
            AgentContext ctx,
            DecisionSchema schema,
            string fewShotBlock,
            IReadOnlyList<string> history = null,
            PromptOptions options = null)
        {
            return Build(ctx, schema, fewShotBlock, history, options, null, null);
        }

        // For Agent, which has asked ActionAvailability once for this decision: the legal pairs
        // (#151) come from that answer, so a per-target check is never asked twice, and the ids the
        // listing left out for MaxListedTargets go into unlisted, when given. Without an answer, one
        // is asked for here, and only when the pairs are listed.
        internal static DecisionRequest Build(
            AgentContext ctx,
            DecisionSchema schema,
            string fewShotBlock,
            IReadOnlyList<string> history,
            PromptOptions options,
            AvailabilityAnswer answer,
            List<string> unlisted)
        {
            if (ctx == null)
            {
                throw new ArgumentNullException(nameof(ctx));
            }

            if (schema == null)
            {
                throw new ArgumentNullException(nameof(schema));
            }

            options = options ?? new PromptOptions();

            if (string.IsNullOrWhiteSpace(options.StimulusLabel))
            {
                throw new ArgumentException(
                    "PromptOptions.StimulusLabel is empty. It labels the stimulus turn; the default is \"Player\".",
                    nameof(options));
            }

            if (options.MaxListedTargets < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(options), options.MaxListedTargets,
                    "PromptOptions.MaxListedTargets cannot be negative. 0 lists no targets.");
            }

            if (options.CallObservationCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(options), options.CallObservationCount,
                    "PromptOptions.CallObservationCount cannot be negative. 0 means none of the observations is the call's own.");
            }

            if (!Enum.IsDefined(typeof(LegalTargetListing), options.LegalTargets))
            {
                throw new ArgumentOutOfRangeException(nameof(options), options.LegalTargets,
                    "PromptOptions.LegalTargets is not one of Off, PerTarget or ObservationLine.");
            }

            if (!Enum.IsDefined(typeof(StatementRulesWording), options.StatementRulesWording))
            {
                throw new ArgumentOutOfRangeException(nameof(options), options.StatementRulesWording,
                    "PromptOptions.StatementRulesWording is not one of YourStatement, HowItShouldRead or InYourOwnWords.");
            }

            string choices = null;
            string moves = null;
            if (options.LegalTargets == LegalTargetListing.PerTarget)
            {
                choices = LegalChoicesBlock(ctx, schema, options, answer ?? ActionAvailability.Evaluate(ctx), unlisted);
            }
            else if (options.ListActionsAndTargets)
            {
                choices = ChoicesBlock(ctx, schema, options.MaxListedTargets, unlisted);
            }

            if (options.LegalTargets == LegalTargetListing.ObservationLine)
            {
                moves = MovesLine(ctx, schema, options, answer ?? ActionAvailability.Evaluate(ctx), unlisted);
            }

            Place(ctx.Observations, options, out var block, out int sourceLines, out string facts);
            if (moves != null)
            {
                // After the sources' lines and before the call's own, wherever those go.
                var withMoves = new List<string>(block);
                withMoves.Insert(sourceLines, moves);
                block = withMoves;
            }

            return new DecisionRequest
            {
                // With no statement field, the rules for one are not sent either (#266).
                SystemPrompt = SystemPrompt(ctx.Identity, schema.AsksForStatement ? options.StatementRulesWording : (StatementRulesWording?)null,
                    choices, options.TargetRule ? TargetRuleLine(schema) : null),
                FewShotBlock = fewShotBlock,
                Observations = ObservationsBlock(block),
                // Copied, so a memory strategy changing its list afterwards does not change a
                // request already built.
                History = new ReadOnlyCollection<string>(new List<string>(history ?? Array.Empty<string>())),
                Stimulus = $"{options.StimulusLabel}: \"{ctx.Stimulus}\"" + facts,
                Schema = schema,
                AgentName = ctx.Identity?.Name,
                StimulusText = ctx.Stimulus,
                StimulusLabel = options.StimulusLabel,
            };
        }

        // The second, short request a guard's verdict can lead to (#109, build plan §2.5): the same
        // system prompt as the decision's, one user turn saying what was asked and why it will not
        // be done, and a schema with only the statement. No examples, observations or history, so
        // it stays short.
        internal static DecisionRequest BuildRefusalLine(DecisionRequest decided, AgentContext ctx, GuardVerdict verdict)
        {
            return new DecisionRequest
            {
                SystemPrompt = decided.SystemPrompt,
                Stimulus = string.Format(RefusalLineTemplate, ctx.Stimulus, EndSentence(verdict.ModelReason.Trim())),
                Schema = DecisionSchema.StatementOnly(),
                AgentName = decided.AgentName,
                StimulusText = decided.StimulusText,
                StimulusLabel = decided.StimulusLabel,
                AgentId = decided.AgentId,
                // Sampled as the decision was (#266 item 7, #244 item 11).
                Temperature = decided.Temperature,
                Seed = decided.Seed,
                MaxReplyTokens = decided.MaxReplyTokens,
            };
        }

        // Whether Build, given these options and this schema, puts the target rule in the system
        // prompt. Agent records the answer in DecisionTelemetry.TargetRuleSent, so the two cannot
        // disagree.
        internal static bool SendsTargetRule(DecisionSchema schema, PromptOptions options)
        {
            return options != null && options.TargetRule && TargetRuleLine(schema) != null;
        }

        // The target rule, naming the schema's target ids in its order, no_target left out. Null
        // when the schema has no target field, since there is nothing to ground, or when that
        // field offers nothing but no_target, which DecisionSchema.Build never makes.
        private static string TargetRuleLine(DecisionSchema schema)
        {
            foreach (var field in schema.Fields)
            {
                if (field.Name != DecisionSchema.TargetFieldName || field.AllowedValues == null) continue;

                var ids = new List<string>(field.AllowedValues.Count);
                foreach (string id in field.AllowedValues)
                {
                    if (id != TargetRegistry.NoTarget)
                    {
                        ids.Add(id);
                    }
                }

                return ids.Count > 0
                    ? TargetRuleStart + string.Join(", ", ids) + (schema.AsksForStatement ? TargetRuleEnd : TargetRuleEndSilent)
                    : null;
            }

            return null;
        }

        // The actions the schema offers, each with its description, then the targets it offers,
        // each with its own line when it has one (#190). Built from the schema rather than from
        // the registries, so the list and the enum cannot disagree. Every action is listed; the
        // targets stop at maxTargets.
        private static string ChoicesBlock(AgentContext ctx, DecisionSchema schema, int maxTargets, List<string> unlisted)
        {
            var lines = new List<string>();

            foreach (var field in schema.Fields)
            {
                if (field.AllowedValues == null) continue;

                if (field.Name == DecisionSchema.ActionFieldName)
                {
                    lines.Add(ActionsHeader);
                    foreach (string id in field.AllowedValues)
                    {
                        lines.Add(Entry(id, id == ActionRegistry.None ? DecisionSchema.NoneDescription : DescriptionOf(ctx.Actions, id)));
                    }
                }
                else if (field.Name == DecisionSchema.TargetFieldName)
                {
                    var targets = new List<string>();
                    foreach (string id in field.AllowedValues)
                    {
                        if (id == TargetRegistry.NoTarget) continue;
                        if (targets.Count == maxTargets)
                        {
                            // Still offered by the schema; only its line is left out (#151).
                            unlisted?.Add(id);
                            continue;
                        }

                        ctx.Targets.TryGet(id, out object target);
                        targets.Add(Entry(id, (target as IDescribedTarget)?.Description));
                    }

                    if (targets.Count > 0)
                    {
                        lines.Add(TargetsHeader);
                        lines.AddRange(targets);
                    }
                }
            }

            return lines.Count > 0 ? string.Join("\n", lines) : null;
        }

        // The action and target list with the legal pairs (#151): the schema's actions, each with
        // its description, and the targets some offered action may take, at most MaxListedTargets of
        // them in schema order, with the pairs said per action or per target. The pairs are the
        // paired schema's (DecisionSchema.PairsOf), so the list, the schema and the guards agree. A
        // target no offered action may take is not listed, since no answer naming it would stand;
        // only one the cap leaves out goes into unlisted.
        private static string LegalChoicesBlock(AgentContext ctx, DecisionSchema schema, PromptOptions options,
                                                AvailabilityAnswer answer, List<string> unlisted)
        {
            SchemaField actionField = null, targetField = null;
            foreach (var field in schema.Fields)
            {
                if (field.AllowedValues == null) continue;
                if (field.Name == DecisionSchema.ActionFieldName) actionField = field;
                else if (field.Name == DecisionSchema.TargetFieldName) targetField = field;
            }

            if (actionField == null)
            {
                return null;
            }

            var TakenBy = LegalPairs(ctx, schema, answer, actionField, targetField, options.MaxListedTargets, unlisted, out var listed);

            var lines = new List<string> { ActionsHeader };
            foreach (string id in actionField.AllowedValues)
            {
                string line = Entry(id, id == ActionRegistry.None ? DecisionSchema.NoneDescription : DescriptionOf(ctx.Actions, id));
                var set = TakenBy(id);
                var real = listed.FindAll(set.Contains);
                bool noTarget = set.Contains(TargetRegistry.NoTarget);

                // An action that may take no listed target says so on its own line; the others are
                // named on their targets' lines below.
                lines.Add(real.Count > 0 ? line : WithNote(line, noTarget ? TargetNoneNote : TargetsUnlistedNote));
            }

            if (listed.Count > 0)
            {
                lines.Add(TargetsHeader);
                foreach (string id in listed)
                {
                    ctx.Targets.TryGet(id, out object target);
                    string description = (target as IDescribedTarget)?.Description;
                    var takers = new List<string>();
                    foreach (string action in actionField.AllowedValues)
                    {
                        if (TakenBy(action).Contains(id)) takers.Add(action);
                    }

                    lines.Add(Entry(id + " (" + ForPrefix + string.Join(", ", takers) + ")", description));
                }
            }

            return string.Join("\n", lines);
        }

        // What each offered action may take (none's no_target included, an action the pairs do not
        // name taking no_target alone), and the targets some offered action may take, at most max of
        // them in schema order; one past the cap goes into unlisted, unless it is there already. The
        // pairs are the paired schema's (DecisionSchema.PairsOf), so the prompt, the schema and the
        // guards agree, whichever way the prompt says them (#151, #244 item 5).
        private static Func<string, HashSet<string>> LegalPairs(AgentContext ctx, DecisionSchema schema, AvailabilityAnswer answer,
                                                                SchemaField actionField, SchemaField targetField, int max,
                                                                List<string> unlisted, out List<string> listed)
        {
            var takes = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            foreach (var pair in DecisionSchema.PairsOf(ctx, schema, answer))
            {
                foreach (string action in pair.Actions)
                {
                    takes[action] = new HashSet<string>(pair.Targets, StringComparer.Ordinal);
                }
            }

            var noTargetOnly = new HashSet<string>(StringComparer.Ordinal) { TargetRegistry.NoTarget };
            HashSet<string> TakenBy(string action) => takes.TryGetValue(action, out var set) ? set : noTargetOnly;

            listed = new List<string>();
            if (targetField != null)
            {
                foreach (string id in targetField.AllowedValues)
                {
                    if (id == TargetRegistry.NoTarget) continue;

                    bool taken = false;
                    foreach (string action in actionField.AllowedValues)
                    {
                        if (TakenBy(action).Contains(id))
                        {
                            taken = true;
                            break;
                        }
                    }

                    if (!taken) continue;
                    if (listed.Count == max)
                    {
                        if (unlisted != null && !unlisted.Contains(id)) unlisted.Add(id);
                        continue;
                    }

                    listed.Add(id);
                }
            }

            return TakenBy;
        }

        // The legal pairs as one sentence (#244 item 5): "Moves open to you: attack fenmark or
        // westmarch; fortify ashby." Each offered action that may take a listed target, in schema
        // order, with those targets in schema order; null when there is none.
        private static string MovesLine(AgentContext ctx, DecisionSchema schema, PromptOptions options,
                                        AvailabilityAnswer answer, List<string> unlisted)
        {
            SchemaField actionField = null, targetField = null;
            foreach (var field in schema.Fields)
            {
                if (field.AllowedValues == null) continue;
                if (field.Name == DecisionSchema.ActionFieldName) actionField = field;
                else if (field.Name == DecisionSchema.TargetFieldName) targetField = field;
            }

            if (actionField == null || targetField == null)
            {
                return null;
            }

            var TakenBy = LegalPairs(ctx, schema, answer, actionField, targetField, options.MaxListedTargets, unlisted, out var listed);
            var parts = new List<string>();
            foreach (string action in actionField.AllowedValues)
            {
                var targets = listed.FindAll(TakenBy(action).Contains);
                if (targets.Count == 0) continue;

                string list = targets.Count == 1
                    ? targets[0]
                    : string.Join(", ", targets.GetRange(0, targets.Count - 1)) + " or " + targets[targets.Count - 1];
                parts.Add(action + " " + list);
            }

            return parts.Count == 0 ? null : MovesLinePrefix + string.Join("; ", parts) + ".";
        }

        // An action's line with its pairs note: "- id: Its description. Target: no_target.", or with
        // no description "- id: Target: no_target.".
        private static string WithNote(string entry, string note)
        {
            return entry.IndexOf(": ", StringComparison.Ordinal) >= 0 ? entry + " " + note : entry + ": " + note;
        }

        // The description of the action the schema offers as id. Matched on each definition's Id
        // as it is now, the value the schema was built from, so an action renamed after it was
        // registered is still described.
        private static string DescriptionOf(ActionRegistry actions, string id)
        {
            foreach (var definition in actions.Definitions)
            {
                if (definition != null && string.Equals(definition.Id, id, StringComparison.Ordinal))
                {
                    return definition.Description;
                }
            }

            return null;
        }

        // "- id: Its description." or "- id". The description is the developer's, kept on one line
        // so it cannot break the list, and ended as a sentence like the other framed lines.
        private static string Entry(string id, string description)
        {
            string text = description == null ? "" : Regex.Replace(description, @"\s+", " ").Trim();
            return text.Length > 0 ? "- " + id + ": " + EndSentence(text) : "- " + id;
        }

        // Who the agent is, its goals, its knowledge and its statement rules, each when it has
        // them, then the fixed instruction line, then the action and target list and the target
        // rule, each when there is one. With no knowledge and no rules the prompt is exactly what
        // it was before them (#190, Living Vale #14). A null wording sends no rules line: the
        // schema asks for no statement (#266).
        private static string SystemPrompt(AgentIdentity identity, StatementRulesWording? wording, string choices, string targetRule)
        {
            var lines = new List<string>(7);

            string who = WhoLine(identity);
            if (who.Length > 0)
            {
                lines.Add(who);
            }

            string goals = Clean(identity.Goals);
            if (goals.Length > 0)
            {
                lines.Add("Your goals: " + EndSentence(goals));
            }

            string knowledge = KnowledgeBlock(identity.Knowledge);
            if (knowledge != null)
            {
                lines.Add(knowledge);
            }

            string rules = OneLine(identity.StatementRules);
            if (rules.Length > 0 && wording.HasValue)
            {
                lines.Add(RulesPrefix(wording.Value) + EndSentence(rules));
            }

            lines.Add(InstructionLine);

            if (choices != null)
            {
                lines.Add(choices);
            }

            if (targetRule != null)
            {
                lines.Add(targetRule);
            }

            return string.Join("\n", lines);
        }

        // The words before the statement rules (#244 item 8).
        private static string RulesPrefix(StatementRulesWording wording)
        {
            switch (wording)
            {
                case StatementRulesWording.HowItShouldRead: return StatementRulesHowItReads;
                case StatementRulesWording.InYourOwnWords: return StatementRulesOwnWords;
                default: return StatementRulesPrefix;
            }
        }

        // "You are <name>, <role>. <personality>", leaving out whichever parts are empty. A
        // code-only agent (#17) has no profile to warn it about an empty name.
        private static string WhoLine(AgentIdentity identity)
        {
            string name = Clean(identity.Name);
            string role = Clean(identity.Role);
            string personality = Clean(identity.Personality);

            string who = name.Length > 0 && role.Length > 0 ? name + ", " + role : name + role;
            string line = who.Length > 0 ? "You are " + EndSentence(who) : "";

            if (personality.Length == 0)
            {
                return line;
            }

            return line.Length > 0 ? line + " " + personality : personality;
        }

        // Where the call's own observations go (#151). ctx holds the sources' lines first and the
        // call's own last, CallObservationCount of them. AfterSources renders the list as it is, so
        // the request is byte-identical to one built before the option; WithStimulus takes them out of
        // the block and returns them as the text that follows the stimulus's closing quote: each on one line,
        // ended as a sentence, after a space, so the developer's stimulus stays whole inside the
        // quote and the facts read as what comes after it. Blank lines are skipped, as in the block.
        // sourceLines says how many of block's lines came from the sources: where a line that belongs
        // between them and the call's own goes.
        private static void Place(IReadOnlyList<string> observations, PromptOptions options,
                                  out IReadOnlyList<string> block, out int sourceLines, out string facts)
        {
            block = observations;
            facts = "";
            int own = Math.Min(options.CallObservationCount, observations.Count);
            sourceLines = observations.Count - own;
            if (own == 0 || options.ObservationPlacement == ObservationPlacement.AfterSources)
            {
                return;
            }

            int sources = sourceLines;
            var fromSources = new List<string>(sources);
            var callLines = new List<string>(own);
            for (int i = 0; i < observations.Count; i++)
            {
                (i < sources ? fromSources : callLines).Add(observations[i]);
            }

            if (options.ObservationPlacement != ObservationPlacement.WithStimulus)
            {
                throw new ArgumentOutOfRangeException(nameof(options), options.ObservationPlacement,
                    "PromptOptions.ObservationPlacement is not one of AfterSources or WithStimulus.");
            }

            block = fromSources;
            var text = new System.Text.StringBuilder();
            foreach (string line in callLines)
            {
                string fact = OneLine(line);
                if (fact.Length == 0) continue;

                text.Append(' ').Append(EndSentence(fact));
            }

            facts = text.ToString();
        }

        // The developer's observations, one "- " line each under a heading, or null when there
        // are none, so the block is left out rather than sent as an empty heading. A blank
        // entry is skipped: an empty line is not something the agent knows.
        private static string ObservationsBlock(IReadOnlyList<string> observations)
        {
            var lines = new List<string> { ObservationsHeader };

            foreach (string observation in observations)
            {
                if (string.IsNullOrWhiteSpace(observation)) continue;

                lines.Add("- " + observation);
            }

            return lines.Count > 1 ? string.Join("\n", lines) : null;
        }

        // The identity's knowledge, one "- " line each under its heading, or null when it has
        // none. Like an observation, a line goes in as written, except that a blank one is skipped
        // and its own line breaks become spaces, so one entry cannot pose as several or as a
        // heading of the prompt.
        private static string KnowledgeBlock(IReadOnlyList<string> knowledge)
        {
            if (knowledge == null)
            {
                return null;
            }

            var lines = new List<string> { KnowledgeHeader };

            foreach (string line in knowledge)
            {
                string text = OneLine(line);
                if (text.Length == 0) continue;

                lines.Add("- " + text);
            }

            return lines.Count > 1 ? string.Join("\n", lines) : null;
        }

        private static string Clean(string text) => text == null ? "" : text.Trim();

        // Trimmed, and every run of whitespace, line breaks included, one space.
        private static string OneLine(string text) => text == null ? "" : Regex.Replace(text, @"\s+", " ").Trim();

        // Ends a framed sentence with a full stop unless the developer's text already ends one,
        // so "a guard at the town gate." does not become "gate..".
        private static string EndSentence(string text)
        {
            char last = text[text.Length - 1];
            return last == '.' || last == '!' || last == '?' ? text : text + ".";
        }
    }
}

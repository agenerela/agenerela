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
    /// touched at all. The framework never classifies the stimulus or assumes a speaking
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

        private const string ObservationsHeader = "What you know right now:";

        // The action and target list's headings (#190). "Allowed" echoes the instruction line's
        // "from the allowed list"; the target list is the "listed targets" it names. New and
        // unmeasured, and here alone so the A/B can change them in one place.
        private const string ActionsHeader = "Allowed actions:";
        private const string TargetsHeader = "Targets:";

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
        /// <exception cref="ArgumentOutOfRangeException"><see cref="PromptOptions.MaxListedTargets"/> is negative.</exception>
        public static DecisionRequest Build(
            AgentContext ctx,
            DecisionSchema schema,
            string fewShotBlock,
            IReadOnlyList<string> history = null,
            PromptOptions options = null)
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

            string choices = options.ListActionsAndTargets ? ChoicesBlock(ctx, schema, options.MaxListedTargets) : null;

            return new DecisionRequest
            {
                SystemPrompt = SystemPrompt(ctx.Identity, choices, options.TargetRule ? TargetRuleLine(schema) : null),
                FewShotBlock = fewShotBlock,
                Observations = ObservationsBlock(ctx.Observations),
                // Copied, so a memory strategy changing its list afterwards does not change a
                // request already built.
                History = new ReadOnlyCollection<string>(new List<string>(history ?? Array.Empty<string>())),
                Stimulus = $"{options.StimulusLabel}: \"{ctx.Stimulus}\"",
                Schema = schema,
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

                return ids.Count > 0 ? TargetRuleStart + string.Join(", ", ids) + TargetRuleEnd : null;
            }

            return null;
        }

        // The actions the schema offers, each with its description, then the targets it offers,
        // each with its own line when it has one (#190). Built from the schema rather than from
        // the registries, so the list and the enum cannot disagree. Every action is listed; the
        // targets stop at maxTargets.
        private static string ChoicesBlock(AgentContext ctx, DecisionSchema schema, int maxTargets)
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
                        if (targets.Count == maxTargets) break;
                        if (id == TargetRegistry.NoTarget) continue;

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

        // Who the agent is, its goals if it has any, then the fixed instruction line, then the
        // action and target list and the target rule, each when there is one.
        private static string SystemPrompt(AgentIdentity identity, string choices, string targetRule)
        {
            var lines = new List<string>(5);

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

        private static string Clean(string text) => text == null ? "" : text.Trim();

        // Ends a framed sentence with a full stop unless the developer's text already ends one,
        // so "a guard at the town gate." does not become "gate..".
        private static string EndSentence(string text)
        {
            char last = text[text.Length - 1];
            return last == '.' || last == '!' || last == '?' ? text : text + ".";
        }
    }
}

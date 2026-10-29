using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
    /// One line is added only on request: the target rule, the fallback for a provider that
    /// cannot report token probabilities, without which <see cref="TargetGroundingGuard"/> cannot
    /// run (build plan §2.5, DR-016). With <see cref="PromptOptions.TargetRule"/> off, the default and
    /// what <c>Agent</c> sends to Ollama, the prompt is exactly what it was without it.
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

            return new DecisionRequest
            {
                SystemPrompt = SystemPrompt(ctx.Identity, options.TargetRule ? TargetRuleLine(schema) : null),
                FewShotBlock = fewShotBlock,
                Observations = ObservationsBlock(ctx.Observations),
                // Copied, so a memory strategy changing its list afterwards does not change a
                // request already built.
                History = new ReadOnlyCollection<string>(new List<string>(history ?? Array.Empty<string>())),
                Stimulus = $"{options.StimulusLabel}: \"{ctx.Stimulus}\"",
                Schema = schema,
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

        // Who the agent is, its goals if it has any, then the fixed instruction line, then the
        // target rule when there is one.
        private static string SystemPrompt(AgentIdentity identity, string targetRule)
        {
            var lines = new List<string>(4);

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

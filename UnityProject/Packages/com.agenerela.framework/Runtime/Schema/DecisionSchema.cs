using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Agenerela
{
    /// <summary>
    /// The shape of the answer one decision asks for: which fields, in which order, and the only
    /// values each may take. Built per request from the actions available right now and the
    /// targets registered right now.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Provider-neutral on purpose: no JSON and no grammar syntax lives here. Each dialect has its
    /// own serializer over this type, JSON Schema for Ollama and the cloud APIs (#9) and GBNF for
    /// the in-process provider (Phase 6b), so a new dialect is a new serializer rather than a
    /// second schema system.
    /// </para>
    /// <para>
    /// Three measured rules are built in here (build plan §2.2 and §2.3). Read Appendix A before
    /// changing any of them:
    /// </para>
    /// <list type="number">
    ///   <item><description>
    ///   Fields go <c>action</c>, <c>target</c>, <c>statement</c>. Models generate left to right:
    ///   with the free text first, the model wrote a reply and then picked whichever action agreed
    ///   with it, and commands collapsed to "no action". Action first was worth +11.7 accuracy
    ///   points on its own.
    ///   </description></item>
    ///   <item><description>
    ///   <c>target</c> is required, with <see cref="TargetRegistry.NoTarget"/> in its enum. Left
    ///   optional, a 4B model omitted it even when it was needed (0/5), and Gemini rejects
    ///   <c>""</c> as an enum value with HTTP 400.
    ///   </description></item>
    ///   <item><description>
    ///   <see cref="ActionRegistry.None"/> is in every action enum, always last, so idling reads
    ///   as a fallback rather than a default.
    ///   </description></item>
    /// </list>
    /// <para>
    /// No reasoning or chain-of-thought field goes before <c>action</c>: it scored 35.3%, the
    /// worst of five variants and below the unmodified baseline. Any field added later, such as
    /// an action parameter, goes after <c>statement</c>.
    /// </para>
    /// </remarks>
    public sealed class DecisionSchema
    {
        /// <summary>Name of the field holding the chosen action id.</summary>
        public const string ActionFieldName = "action";

        /// <summary>Name of the field holding the chosen target id.</summary>
        public const string TargetFieldName = "target";

        /// <summary>Name of the free-text field. The prototype called it <c>dialogue</c> (DR-008).</summary>
        public const string StatementFieldName = "statement";

        // The three descriptions below are sent with every request and are exactly the strings in
        // #9's golden output. They name no player and no character: the agent may be a country
        // reading a report. Only a provider that shows the schema to the model puts them in front
        // of it (the cloud APIs; unmeasured). Ollama uses the schema only to constrain sampling and
        // never shows it (findings, 10 October 2026), so a local model never reads these and meets
        // the no_target rule only in PromptBuilder's instruction line. Rewording one here changes
        // nothing a local model is told.

        /// <summary>Description of the <c>action</c> field.</summary>
        public const string ActionDescription = "The one action to take now, from the allowed list.";

        /// <summary>Description of the <c>target</c> field.</summary>
        public const string TargetDescription =
            "What the action applies to. Use " + TargetRegistry.NoTarget + " when none of the listed targets applies.";

        /// <summary>Description of the <c>statement</c> field.</summary>
        public const string StatementDescription =
            "What the agent says or announces. One or two short sentences, or empty.";

        /// <summary>
        /// The description of <see cref="ActionRegistry.None"/>, for wherever actions are listed
        /// with their descriptions. One short clause, like every action's (build plan §2.2 rule 2).
        /// That rule's evidence came with a biased system prompt, and the description itself never
        /// reached the model through Ollama (findings, 10 October 2026); keeping it costs nothing.
        /// </summary>
        public const string NoneDescription = "Take no action";

        /// <summary>
        /// A schema with exactly these fields, in this order. <see cref="Build"/> is how the
        /// framework makes one; this is for a known shape, such as a serializer's test fixture,
        /// and it does not apply the rules <see cref="Build"/> does.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="fields"/> is null.</exception>
        /// <exception cref="ArgumentException">One of <paramref name="fields"/> is null.</exception>
        public DecisionSchema(IEnumerable<SchemaField> fields)
        {
            if (fields == null)
            {
                throw new ArgumentNullException(nameof(fields));
            }

            // Copied, so changing the caller's list afterwards does not change this schema.
            var copy = new List<SchemaField>(fields);
            for (int i = 0; i < copy.Count; i++)
            {
                if (copy[i] == null)
                {
                    throw new ArgumentException($"Schema field {i} is null.", nameof(fields));
                }
            }

            Fields = new ReadOnlyCollection<SchemaField>(copy);
        }

        /// <summary>Fields in generation order. THE ORDER IS LOAD-BEARING: see the remarks on this type.</summary>
        public IReadOnlyList<SchemaField> Fields { get; }

        /// <summary>
        /// The schema for one decision: the actions <see cref="ActionAvailability.For"/> returns,
        /// then <see cref="ActionRegistry.None"/>; the targets registered in <c>ctx.Targets</c>;
        /// and a free-form statement. All three fields are required.
        /// </summary>
        /// <remarks>
        /// Built fresh on every call, and copies what it reads, so registering a target after
        /// building does not change a schema already built. An empty target registry removes the
        /// <c>target</c> field entirely rather than leaving it with <c>no_target</c> alone: every
        /// action that takes a target is already gone (#7), so there is nothing to choose.
        /// </remarks>
        /// <exception cref="ArgumentNullException"><paramref name="ctx"/> is null.</exception>
        public static DecisionSchema Build(AgentContext ctx)
        {
            if (ctx == null)
            {
                throw new ArgumentNullException(nameof(ctx));
            }

            return Build(ctx, ActionAvailability.For(ctx));
        }

        // For a caller that has already asked ActionAvailability about this context, as Agent has
        // for FewShotBuilder, so the enum and the examples come from one answer.
        internal static DecisionSchema Build(AgentContext ctx, IReadOnlyList<ActionDefinition> available)
        {
            if (ctx == null)
            {
                throw new ArgumentNullException(nameof(ctx));
            }

            if (available == null)
            {
                throw new ArgumentNullException(nameof(available));
            }

            var fields = new List<SchemaField> { ActionField(available) };

            if (ctx.Targets.Count > 0)
            {
                fields.Add(TargetField(ctx.Targets.Ids));
            }

            fields.Add(StatementField());
            return new DecisionSchema(fields);
        }

        // The available actions in registry order, then none. ActionRegistry.Register rejects
        // none, so it is never already in the list and appears exactly once.
        private static SchemaField ActionField(IReadOnlyList<ActionDefinition> available)
        {
            var ids = new List<string>(available.Count + 1);
            foreach (var definition in available)
            {
                ids.Add(definition.Id);
            }

            ids.Add(ActionRegistry.None);

            return new SchemaField
            {
                Name = ActionFieldName,
                Description = ActionDescription,
                AllowedValues = new ReadOnlyCollection<string>(ids),
                Required = true,
            };
        }

        // no_target, then the registered ids in registration order. Ids is a live view, so it
        // is copied.
        private static SchemaField TargetField(IReadOnlyList<string> registered)
        {
            var ids = new List<string>(registered.Count + 1) { TargetRegistry.NoTarget };
            ids.AddRange(registered);

            return new SchemaField
            {
                Name = TargetFieldName,
                Description = TargetDescription,
                AllowedValues = new ReadOnlyCollection<string>(ids),
                Required = true,
            };
        }

        // Required but free-form, and may be empty: an agent with nothing to say still answers
        // in the measured three-field shape.
        private static SchemaField StatementField()
        {
            return new SchemaField
            {
                Name = StatementFieldName,
                Description = StatementDescription,
                AllowedValues = null,
                Required = true,
            };
        }
    }
}

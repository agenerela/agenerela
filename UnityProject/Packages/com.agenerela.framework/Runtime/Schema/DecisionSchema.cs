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
            : this(fields, null)
        {
        }

        /// <summary>
        /// A schema with exactly these fields, and with <paramref name="pairs"/> saying which targets
        /// each action takes (DR-020). Null pairs make a flat schema, as the other constructor does.
        /// Like it, this applies none of the rules <see cref="Build(AgentContext, bool)"/> does; the
        /// serializer checks the pairs against the fields.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="fields"/> is null.</exception>
        /// <exception cref="ArgumentException">One of <paramref name="fields"/> or <paramref name="pairs"/> is null.</exception>
        public DecisionSchema(IEnumerable<SchemaField> fields, IEnumerable<ActionTargets> pairs)
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

            if (pairs != null)
            {
                var branches = new List<ActionTargets>(pairs);
                for (int i = 0; i < branches.Count; i++)
                {
                    if (branches[i] == null)
                    {
                        throw new ArgumentException($"Pair {i} is null.", nameof(pairs));
                    }
                }

                Pairs = new ReadOnlyCollection<ActionTargets>(branches);
            }
        }

        /// <summary>Fields in generation order. THE ORDER IS LOAD-BEARING: see the remarks on this type.</summary>
        public IReadOnlyList<SchemaField> Fields { get; }

        /// <summary>
        /// Which targets each action takes, one <see cref="ActionTargets"/> per distinct target list,
        /// or null for a flat schema, where every action takes every target: the default (DR-020).
        /// </summary>
        /// <remarks>
        /// <para>
        /// A paired schema keeps its flat <c>action</c> and <c>target</c> fields, the same values a
        /// flat one offers, so everything that reads the fields, the prompt's action and target list,
        /// the target rule, the grounding guard and the parser, reads exactly what it would without
        /// the pairs. Only a serializer reads them, and only <see cref="SchemaLegalityGuard"/> holds a
        /// decision to them.
        /// </para>
        /// <para>
        /// <see cref="Build(AgentContext, bool)"/> makes them only when asked
        /// (<see cref="DecideOptions.PairTargetsWithActions"/>, off by default and unmeasured, #151).
        /// </para>
        /// </remarks>
        public IReadOnlyList<ActionTargets> Pairs { get; }

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
            return Build(ctx, pairTargets: false);
        }

        /// <summary>
        /// The schema for one decision, as <see cref="Build(AgentContext)"/> makes it, and with
        /// <paramref name="pairTargets"/> each action paired with the targets it takes (DR-020).
        /// </summary>
        /// <remarks>
        /// <para>
        /// Paired, each action offered takes the targets in its
        /// <see cref="ActionDefinition.TargetCategories"/>, or every target when it names none, and
        /// <see cref="TargetRegistry.NoTarget"/> only when it does not require a target.
        /// <see cref="ActionRegistry.None"/> takes <see cref="TargetRegistry.NoTarget"/> alone.
        /// Actions with the same list share one <see cref="ActionTargets"/>, in the order their first
        /// action is offered. An action that requires a target and takes none of those registered is
        /// left out of the schema altogether, as one is when no target is registered at all.
        /// </para>
        /// <para>
        /// A target the action's per-target check (<see cref="ITargetAvailability"/>) rejects is not
        /// one it takes, so that pair is left out. With pairing off the target enum is every
        /// registered target whatever the checks say; only an action left with no target its check
        /// allows is left out, as <see cref="ActionAvailability.For"/> does in both modes.
        /// </para>
        /// <para>
        /// With no target registered there is nothing to pair, and the schema is flat whatever
        /// <paramref name="pairTargets"/> says.
        /// </para>
        /// </remarks>
        /// <exception cref="ArgumentNullException"><paramref name="ctx"/> is null.</exception>
        public static DecisionSchema Build(AgentContext ctx, bool pairTargets)
        {
            if (ctx == null)
            {
                throw new ArgumentNullException(nameof(ctx));
            }

            var answer = ActionAvailability.Evaluate(ctx);
            var available = answer.Offered;
            return Build(ctx, pairTargets ? Takeable(available, ctx.Targets, answer) : available, pairTargets, answer);
        }

        // For a caller that has already asked ActionAvailability about this context, as Agent has
        // for FewShotBuilder, so the enum and the examples come from one answer. Paired, it has
        // passed the answer through Takeable, so the examples show no action the schema leaves out.
        // The answer, when given, is the one available came from: the paired schema leaves out each
        // pair a per-target check rejected in it (#190 LV #19).
        internal static DecisionSchema Build(AgentContext ctx, IReadOnlyList<ActionDefinition> available, bool pairTargets = false,
            AvailabilityAnswer answer = null)
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
            return new DecisionSchema(fields, pairTargets && ctx.Targets.Count > 0 ? PairsFor(available, ctx.Targets, answer) : null);
        }

        // The actions a paired schema can offer: without an action that requires a target and takes
        // none of those registered, since its branch would have an empty target enum, which allows
        // no answer at all. In availability order. With an answer, a target its per-target check
        // rejected is not one it takes.
        internal static IReadOnlyList<ActionDefinition> Takeable(IReadOnlyList<ActionDefinition> available, TargetRegistry targets,
            AvailabilityAnswer answer = null)
        {
            var takeable = new List<ActionDefinition>(available.Count);
            foreach (var definition in available)
            {
                if (!definition.RequiresTarget || TargetsOf(definition, targets, answer).Count > 0)
                {
                    takeable.Add(definition);
                }
            }

            return takeable;
        }

        // One branch per distinct target list, in the order each list's first action is offered,
        // none's last unless an action shares its list. Keyed on the list itself, so two actions
        // share a branch exactly when they take the same targets in the same order.
        private static List<ActionTargets> PairsFor(IReadOnlyList<ActionDefinition> available, TargetRegistry targets,
            AvailabilityAnswer answer)
        {
            var order = new List<string>();
            var actions = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            var lists = new Dictionary<string, List<string>>(StringComparer.Ordinal);

            void Add(string actionId, List<string> takes)
            {
                string key = KeyOf(takes);
                if (!actions.TryGetValue(key, out var ids))
                {
                    ids = new List<string>();
                    actions.Add(key, ids);
                    lists.Add(key, takes);
                    order.Add(key);
                }

                ids.Add(actionId);
            }

            foreach (var definition in available)
            {
                var takes = new List<string>();
                if (!definition.RequiresTarget)
                {
                    takes.Add(TargetRegistry.NoTarget);
                }

                // An action that takes no target is offered with no_target alone (#244 item 2).
                if (!definition.TakesNoTarget)
                {
                    takes.AddRange(TargetsOf(definition, targets, answer));
                }
                Add(definition.Id, takes);
            }

            Add(ActionRegistry.None, new List<string> { TargetRegistry.NoTarget });

            var pairs = new List<ActionTargets>(order.Count);
            foreach (string key in order)
            {
                pairs.Add(new ActionTargets(actions[key], lists[key]));
            }

            return pairs;
        }

        // A list of ids as one string that no other list shares: each id with its length in front,
        // since an id may hold any character a separator could be made of.
        private static string KeyOf(List<string> ids)
        {
            var key = new System.Text.StringBuilder();
            foreach (string id in ids)
            {
                key.Append(id.Length).Append(':').Append(id);
            }

            return key.ToString();
        }

        // The registered targets this action takes, in registration order: in its categories, and
        // not rejected by its per-target check in the answer, when there is one.
        private static List<string> TargetsOf(ActionDefinition definition, TargetRegistry targets, AvailabilityAnswer answer)
        {
            var takes = new List<string>();
            foreach (string id in targets.Ids)
            {
                if (definition.AcceptsCategory(targets.CategoryOf(id)) && (answer == null || answer.Allows(definition, id)))
                {
                    takes.Add(id);
                }
            }

            return takes;
        }

        // The shape of a refusal line's reply (#109): the one free-text field, so it serializes as
        // any other schema does. Not a decision: it offers no action and no target.
        internal static DecisionSchema StatementOnly()
        {
            return new DecisionSchema(new[] { StatementField() });
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

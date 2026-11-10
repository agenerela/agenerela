namespace Agenerela
{
    /// <summary>
    /// The per-call prompt settings. <c>Agent</c> (#17) fills one on every decision from
    /// <c>DecideOptions</c> and the profile, and hands it to both builders.
    /// </summary>
    public sealed class PromptOptions
    {
        /// <summary>
        /// How the stimulus is labelled in the prompt. Free string, set by the developer per
        /// call: "Player", "Advisor", "Report", "Situation"...
        /// </summary>
        /// <remarks>
        /// <see cref="PromptBuilder.Build"/> labels the stimulus turn with it. The caller passes
        /// the same value to <see cref="FewShotBuilder.Build"/> as its <c>stimulusLabel</c>, so
        /// the examples are labelled the way the question is. The framework never reads the
        /// label or branches on it, and never enumerates the possibilities (DR-008): a timer, a
        /// report or a perception update is not a line of dialogue.
        /// </remarks>
        public string StimulusLabel = "Player";

        /// <summary>
        /// Idle few-shot example. Agent (#17) fills it from the call's
        /// <see cref="DecideOptions.IdleExampleStimulus"/>, else its own
        /// <see cref="Agent.IdleExampleStimulus"/>, else its <see cref="AgentProfile"/>'s. Null or empty
        /// omits the example.
        /// </summary>
        /// <remarks>
        /// Read by <see cref="FewShotBuilder.Build"/>, not by <see cref="PromptBuilder"/>: the
        /// few-shot block reaches <see cref="PromptBuilder.Build"/> already built, so the caller
        /// passes this to <c>FewShotBuilder.Build</c> as its <c>idleExampleStimulus</c>. It is
        /// here so that one object carries every per-call prompt setting to both builders.
        /// </remarks>
        public string IdleExampleStimulus;

        /// <summary>
        /// Whether the system prompt carries the target rule, the fallback for a provider that
        /// reports no token probabilities (#107, DR-016). Off by default. <c>Agent</c> turns it on
        /// when <see cref="DecideOptions.CheckTarget"/> is on and the provider's
        /// <c>Capabilities.ReportsTokenProbabilities</c> is false, so it is never sent beside
        /// <see cref="TargetGroundingGuard"/>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <see cref="PromptBuilder.Build"/> adds it on its own line, right after the fixed
        /// instruction line, naming the schema's target ids in its order without
        /// <c>no_target</c>: "Only these things are here: tower, training_dummy. If you are asked
        /// to act on anything else, choose action none and target no_target, and say it is not
        /// here. Never act on a different thing instead." With no <c>target</c> field in the
        /// schema there is nothing to ground, and no line is added.
        /// </para>
        /// <para>
        /// <b>The wording is unmeasured.</b> DR-016 measured a rule that pointed at an "Around you"
        /// block listing every target with a description, and said "the player" (2B 73 → 88, 4B
        /// 98 → 101, below the guard's 95 and 103, and nothing added on top of it). This prompt
        /// has no such block and names no player (DR-008), so this line carries the list itself.
        /// Phase 4 owes the A/B: this line against the probe's form, the "Around you" block plus
        /// its rule, on a provider without probabilities. Until then, cite no number for it.
        /// </para>
        /// </remarks>
        public bool TargetRule;

        /// <summary>
        /// Whether the system prompt lists the actions offered, each with its description, and the
        /// targets offered, each with its own line when it has one (#190, Living Vale #1). Off by
        /// default. <c>Agent</c> copies it from <see cref="DecideOptions.ListActionsAndTargets"/>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// For a provider that never reads the schema. Ollama uses it only to constrain sampling
        /// (findings, 10 October 2026), so without this a local model learns the actions only from
        /// the few-shot examples and the targets only from what the game writes into observations.
        /// </para>
        /// <para>
        /// <see cref="PromptBuilder.Build"/> adds it right after the fixed instruction line, which
        /// says "from the allowed list", and before the target rule when that is on too:
        /// </para>
        /// <code>
        /// Allowed actions:
        /// - move_to: Walk to a named place.
        /// - none: Take no action.
        /// Targets:
        /// - stock_bread_one: One loaf of bread, 3 gold.
        /// - north_tower
        /// </code>
        /// <para>
        /// The actions are the schema's, in its order, so <c>none</c> comes last (build plan §2.2,
        /// rule 3), with <see cref="DecisionSchema.NoneDescription"/>. Each description is trimmed,
        /// put on one line and ended with a full stop; an action with none is listed by its id. The
        /// targets are the schema's, in its order, without <c>no_target</c>, which the instruction
        /// line already names, and at most <see cref="MaxListedTargets"/> of them. A target object that
        /// implements <see cref="IDescribedTarget"/> gets its description after its id; any other is
        /// listed by id. With no target field in the schema there is no target list.
        /// </para>
        /// <para>
        /// <b>Unmeasured.</b> Its form follows the "Around you" list of the probe DR-016 measured, which
        /// listed targets the same way, but no actions, and was never measured on its own. It stays
        /// off until the evaluation A/B shows it helps (#151, hard rule 2).
        /// </para>
        /// </remarks>
        public bool ListActionsAndTargets;

        /// <summary>
        /// The most targets <see cref="ListActionsAndTargets"/> lists, 12 by default, the first ones in
        /// schema order. Every offered action is always listed: a developer chooses that set, and
        /// dropping one would hide it. 0 lists no targets.
        /// </summary>
        /// <remarks>
        /// The schema still offers a target past the cap; only its line is left out. A source that
        /// offers the nearest first, as <c>ProximityTargetSource</c> does, so loses the far ones. 12
        /// lists every target of the evaluation set (11) and of a <c>ProximityTargetSource</c> at its
        /// default cap (8). <see cref="PromptBuilder.Build"/> throws for a negative value.
        /// </remarks>
        public int MaxListedTargets = DefaultMaxListedTargets;

        /// <summary>The default of <see cref="MaxListedTargets"/> and <see cref="DecideOptions.MaxListedTargets"/>.</summary>
        public const int DefaultMaxListedTargets = 12;

        /// <summary>
        /// Whether the few-shot block shows each action's
        /// <see cref="ActionDefinition.CounterExampleStimulus"/>. On by default, which shows only what
        /// a developer wrote. <c>Agent</c> copies it from <see cref="DecideOptions.CounterExamples"/>.
        /// </summary>
        /// <remarks>
        /// Read by <see cref="FewShotBuilder.Build"/>, not by <see cref="PromptBuilder"/>, like
        /// <see cref="IdleExampleStimulus"/>: the caller passes it as <c>counterExamples</c>.
        /// </remarks>
        public bool CounterExamples = true;

        /// <summary>
        /// What the agent says in the idle example (#190, G14). <c>Agent</c> fills it from the call,
        /// the agent or the profile, never from further down than the idle stimulus came from
        /// (<see cref="DecideOptions.IdleExampleStatement"/>). Null or empty gives no statement. Read by
        /// <see cref="FewShotBuilder.Build(System.Collections.Generic.IReadOnlyList{ActionDefinition}, TargetRegistry, PromptOptions)"/>.
        /// </summary>
        public string IdleExampleStatement;

        /// <summary>
        /// Whether the examples show the statements developers wrote (#190, G14). On by default, which
        /// shows only what was written. <c>Agent</c> copies it from <see cref="DecideOptions.ExampleStatements"/>.
        /// Read by <c>FewShotBuilder.Build</c>.
        /// </summary>
        public bool ExampleStatements = true;

        /// <summary>
        /// Whether an example with no registered preferred target names the first offered target its
        /// action takes, skipping any target <see cref="Stimulus"/> mentions (#190, Living Vale #20).
        /// Off by default. <c>Agent</c> copies it from <see cref="DecideOptions.ExampleTargetFallback"/>.
        /// Read by <c>FewShotBuilder.Build</c>.
        /// </summary>
        public bool ExampleTargetFallback;

        /// <summary>
        /// The request's stimulus, which <see cref="ExampleTargetFallback"/> reads so a fallback example
        /// never names a target the stimulus mentions: that example would show the model the answer.
        /// <c>Agent</c> sets it. Null mentions nothing.
        /// </summary>
        public string Stimulus;
    }
}

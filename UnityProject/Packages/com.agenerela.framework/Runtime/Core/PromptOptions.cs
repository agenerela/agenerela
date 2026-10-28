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
        /// Idle few-shot example. Agent (#17) copies it from <see cref="AgentProfile"/>; a caller
        /// may override per call. Null or empty omits the example.
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
    }
}

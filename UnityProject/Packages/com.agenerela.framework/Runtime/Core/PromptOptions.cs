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
    }
}

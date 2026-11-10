namespace Agenerela
{
    /// <summary>
    /// Where a call's own observations, its <see cref="DecideOptions.Observations"/>, go in the
    /// request (#151). The lines of <see cref="Agent.ObservationSources"/>, the agent's standing
    /// facts, stay in the observation block whichever is chosen.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The choice is about where a fact sits, never about what kind of call it is: the framework
    /// never reads the stimulus or its label to decide it (DR-008). The facts a developer passes
    /// with one call are what that call is about; the facts that hold on every call belong in an
    /// observation source.
    /// </para>
    /// <para>
    /// <b>Measured on 10 November 2026</b> (findings), interleaved on the RTX 5080 over the 389-prompt
    /// interim set, with the standing lines (state, targets) in an observation source and only each
    /// prompt's own one to three lines as the call's: <see cref="WithStimulus"/> against
    /// <see cref="AfterSources"/> scored 351 against 330 on <c>qwen3.5:4b</c> and 283 against 261 on
    /// <c>qwen3.5:2b</c>, beyond noise only on situations and events, with player prompts and calm
    /// scenes no worse; on the curated 76 it was within noise on both. Moving the call's lines to the front of
    /// the block instead changed nothing (328 against 330) and was dropped. The default stays
    /// <see cref="AfterSources"/>, because a game that passes every fact with its call, as all three
    /// demo games do, would have its whole block moved, and that layout is measured separately.
    /// </para>
    /// </remarks>
    public enum ObservationPlacement
    {
        /// <summary>
        /// The default, and what every request was before #151: the observation block holds each
        /// source's lines, then the call's own, the last thing read before the stimulus, and the
        /// stimulus is alone in its turn.
        /// </summary>
        AfterSources = 0,

        /// <summary>
        /// The call's own lines leave the observation block and follow the stimulus in its user
        /// turn, after its closing quote, each on the same line ended as a sentence:
        /// <c>Situation: "Screams are coming from the well." A wolf has cornered a child.</c> The
        /// stimulus inside the quote is the developer's text, unchanged. The sources' lines stay in
        /// the block; with none, the block is left out. Use it when the call's own lines are the
        /// facts of this one moment and the standing facts come from an observation source.
        /// </summary>
        /// <remarks>
        /// The user turn is what a memory strategy such as <see cref="RollingHistory"/> remembers,
        /// so a later decision's history carries these facts as part of this turn.
        /// </remarks>
        WithStimulus = 1,
    }
}

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
    /// never reads the stimulus or its label to decide it (DR-008). A developer who knows that the
    /// facts of this one call decide it, such as what a situation is made of, chooses where they go.
    /// </para>
    /// <para>
    /// <b>Measured on 10 November 2026 by rewriting the request</b> (findings): moving a situation's
    /// own lines out of the end of the observation block and into its stimulus gained 22 of 389
    /// answers on <c>qwen3.5:4b</c> and 17 on <c>qwen3.5:2b</c>. Which of the two readings that
    /// result allows is right, the fact being in the user turn or no longer behind the standing
    /// lines, is what <see cref="BeforeSources"/> and <see cref="WithStimulus"/> separate. The
    /// default stays <see cref="AfterSources"/> until the A/B run says otherwise.
    /// </para>
    /// </remarks>
    public enum ObservationPlacement
    {
        /// <summary>
        /// The default, and what every request was before #151: the observation block holds each
        /// source's lines, then the call's own, the last thing read before the stimulus.
        /// </summary>
        AfterSources = 0,

        /// <summary>
        /// The call's own lines open the observation block, ahead of every source's. Nothing moves
        /// out of the block, and the stimulus turn is unchanged.
        /// </summary>
        BeforeSources = 1,

        /// <summary>
        /// The call's own lines leave the observation block and follow the stimulus in its user
        /// turn, after its closing quote, each on the same line ended as a sentence:
        /// <c>Situation: "Screams are coming from the well." A wolf has cornered a child.</c> The
        /// stimulus inside the quote is the developer's text, unchanged. The sources' lines stay in
        /// the block; with none, the block is left out.
        /// </summary>
        /// <remarks>
        /// The user turn is what a memory strategy such as <see cref="RollingHistory"/> remembers,
        /// so a later decision's history carries these facts as part of this turn.
        /// </remarks>
        WithStimulus = 2,
    }
}

namespace Agenerela
{
    /// <summary>
    /// A memory strategy that can keep what came of a decision beside the decision itself (#216
    /// item 3). Optional, beside <see cref="IMemoryStrategy"/>, as <see cref="IExportsMemory"/> is, so
    /// a strategy written before it still works and simply keeps no outcomes.
    /// <see cref="RollingHistory"/> implements it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The outcome is the game's own free text, such as <c>"took fenmark from orsk (34 against 21);
    /// lost 6"</c> or <c>"the merchant had already left"</c>, never a value from a list the framework
    /// defines (DR-008). A game sets it through <see cref="Agent.SetOutcome"/>, which finds the memory
    /// that holds the decision, or will once the game settles it; a strategy is never asked by
    /// anything else.
    /// </para>
    /// <para>
    /// An outcome often is known only after the handler runs, or later still: a battle resolves
    /// after every country's move has landed. So it can be set, replaced or cleared for as long as
    /// the memory still holds the decision, and the next recall reads it as it is then.
    /// </para>
    /// <para>
    /// <b>Extension point: the Memory research area (#119).</b> How an outcome reaches the model is
    /// the strategy's to decide. <see cref="RollingHistory"/> sends it only when
    /// <see cref="RollingHistory.Remembers"/> is <see cref="RememberedDecision.MoveAndOutcome"/>.
    /// Called on the main thread, like the rest of a strategy.
    /// </para>
    /// </remarks>
    public interface IRemembersOutcomes
    {
        /// <summary>
        /// Sets what came of the remembered decision <paramref name="result"/> carries, replacing an
        /// outcome set before. Null or blank clears it. Returns whether this memory holds that
        /// decision: false for one it never remembered, has forgotten, or holds only from an import.
        /// </summary>
        /// <param name="result">The decision, as <see cref="Agent.DecideAsync"/> returned it.</param>
        /// <param name="outcome">One short line in the game's own words, or null.</param>
        bool SetOutcome(DecisionResult result, string outcome);
    }
}

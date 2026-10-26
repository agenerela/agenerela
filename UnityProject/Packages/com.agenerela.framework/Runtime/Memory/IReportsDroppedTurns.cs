namespace Agenerela
{
    /// <summary>
    /// A memory strategy that can say how much of the agent's past a request lacks. Optional,
    /// beside <see cref="IMemoryStrategy"/>, as <see cref="IReportsDroppedTargets"/> is beside a
    /// target source. <c>Agent</c> copies both counts into
    /// <see cref="DecisionTelemetry.HistoryTurnsDropped"/> and
    /// <see cref="DecisionTelemetry.HistoryTurnsForgotten"/> right after each
    /// <see cref="IMemoryStrategy.Recall"/>, so Phase 4 can tell when the window is what limits
    /// an agent.
    /// </summary>
    /// <remarks>
    /// The two never count one exchange twice, so their sum is everything remembered that the
    /// request does not carry. A strategy that never lets go of anything reports 0 forgotten.
    /// </remarks>
    public interface IReportsDroppedTurns
    {
        /// <summary>
        /// Exchanges the strategy still holds that the last <see cref="IMemoryStrategy.Recall"/>
        /// left out, for this request only. Each recall replaces it, so it is read straight after
        /// the call it describes.
        /// </summary>
        int Dropped { get; }

        /// <summary>
        /// Exchanges the strategy no longer holds at all, counted since it was made: a total
        /// that keeps rising, not a count for one request.
        /// </summary>
        int Forgotten { get; }
    }
}

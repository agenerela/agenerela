namespace Agenerela
{
    /// <summary>
    /// A memory strategy that can say how much it remembers but left out of the last recall.
    /// Optional, beside <see cref="IMemoryStrategy"/>, as <see cref="IReportsDroppedTargets"/> is
    /// beside a target source. <c>Agent</c> copies it into
    /// <see cref="DecisionTelemetry.HistoryTurnsDropped"/> right after each
    /// <see cref="IMemoryStrategy.Recall"/>, so Phase 4 can tell when the window is what limits
    /// an agent.
    /// </summary>
    public interface IReportsDroppedTurns
    {
        /// <summary>
        /// Exchanges the last <see cref="IMemoryStrategy.Recall"/> left out. Each recall replaces
        /// it, so it is read straight after the call it describes.
        /// </summary>
        int Dropped { get; }
    }
}

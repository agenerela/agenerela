using System.Collections.Generic;

namespace Agenerela
{
    /// <summary>
    /// An observation source that can say which lines it had but did not return, such as the far
    /// ones its own cap cut. Optional, beside <see cref="IObservationSource"/>, as
    /// <see cref="IReportsDroppedTargets"/> is beside a target source, so a source with nothing to
    /// drop implements nothing more. <c>Agent</c> copies the list into
    /// <see cref="DecisionTelemetry.ObservationsDropped"/> right after each <c>Observe</c>, so a
    /// thin-looking prompt can be diagnosed (#91).
    /// </summary>
    public interface IReportsDroppedObservations
    {
        /// <summary>
        /// Lines the last <see cref="IObservationSource.Observe"/> had but did not return, in the
        /// source's own order. Empty, never null, when it dropped nothing. Each <c>Observe</c>
        /// replaces it, so it is read straight after the call it describes.
        /// </summary>
        IReadOnlyList<string> Dropped { get; }
    }
}

using System.Collections.Generic;

namespace Agenerela
{
    /// <summary>
    /// A target source that can say which targets it found but did not offer, such as the far ones
    /// a cap cut. Optional, beside <see cref="ITargetSource"/>, so a source with nothing to drop
    /// implements nothing more. <c>Agent</c> copies the list into
    /// <see cref="DecisionTelemetry.TargetsDropped"/> right after each <c>Collect</c> on the
    /// decision path, so a thin target list can be told apart from a model that ignored a listed
    /// target (#81).
    /// </summary>
    public interface IReportsDroppedTargets
    {
        /// <summary>
        /// Ids the last <see cref="ITargetSource.Collect"/> found but did not offer, in the
        /// source's own order. Empty, never null, when it dropped nothing. Each <c>Collect</c>
        /// replaces it, so it is read straight after the call it describes.
        /// </summary>
        IReadOnlyList<string> Dropped { get; }
    }
}

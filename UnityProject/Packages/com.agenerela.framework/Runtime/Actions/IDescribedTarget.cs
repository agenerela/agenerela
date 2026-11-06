namespace Agenerela
{
    /// <summary>
    /// A target object that says what it is, in one short line the model can read beside its id.
    /// Optional: implement it on the object a game registers as a target, such as a stock entry
    /// or a quest offer. Read only when a call lists its actions and targets
    /// (<see cref="DecideOptions.ListActionsAndTargets"/>, #190), which is off by default.
    /// </summary>
    /// <remarks>
    /// A target that does not implement it is listed by its id alone. A scene target is registered
    /// as its <c>Transform</c>, so it is listed by id; its <c>Targetable.Description</c> reaches the
    /// model as an observation through <c>ProximityObservationSource</c> instead (build plan §2.8).
    /// </remarks>
    public interface IDescribedTarget
    {
        /// <summary>
        /// One short line, such as "One loaf of bread, 3 gold.". Null or blank lists the target
        /// by its id alone. Read on every decision that lists the targets.
        /// </summary>
        string Description { get; }
    }
}

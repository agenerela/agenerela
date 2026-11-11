namespace Agenerela
{
    /// <summary>
    /// Whether, and how, the system prompt says which targets each offered action may take right now
    /// (#151). For a provider that never reads the schema, such as Ollama, where the target
    /// categories, the paired schema and the per-target checks otherwise reach the model nowhere.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The pairs come from the same place the paired schema's do
    /// (<see cref="DecisionSchema.Build(AgentContext, bool)"/>): an action takes the targets in its
    /// <see cref="ActionDefinition.TargetCategories"/>, or every target when it names none, less any
    /// its per-target check (<see cref="ITargetAvailability"/>) rejected for this decision, and
    /// <see cref="TargetRegistry.NoTarget"/> only when it does not require a target. So the prompt,
    /// the paired schema and <see cref="TargetCategoryGuard"/> and <see cref="TargetAvailabilityGuard"/>
    /// cannot disagree. An action that requires a target and may take none is not offered, in the
    /// schema or its examples either, as with the paired schema.
    /// </para>
    /// <para>
    /// Any value but <see cref="Off"/> lists the actions and targets as
    /// <see cref="DecideOptions.ListActionsAndTargets"/> does, whether that is on or not, with only
    /// the targets some offered action may take, at most <see cref="DecideOptions.MaxListedTargets"/>.
    /// </para>
    /// </remarks>
    public enum LegalTargetListing
    {
        /// <summary>The default: nothing in the prompt says which action takes which target.</summary>
        Off = 0,

        /// <summary>
        /// Each action's line ends with the targets it may take:
        /// <c>- attack_target: Strike something. Targets: wolf.</c> An action that takes every listed
        /// target says <c>Targets: any listed target.</c>, one that takes no target
        /// <c>Target: no_target.</c>
        /// </summary>
        PerAction = 1,

        /// <summary>
        /// Each target's line ends with the actions that may take it:
        /// <c>- wolf: A wolf from the woods. For: attack_target.</c> An action that takes no target
        /// at all says <c>Target: no_target.</c> on its own line.
        /// </summary>
        PerTarget = 2,
    }
}

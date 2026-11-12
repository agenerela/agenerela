namespace Agenerela
{
    /// <summary>
    /// Whether, and how, the system prompt says which targets each offered action may take right now
    /// (#151). For a provider that never reads the schema, such as Ollama, where the target
    /// categories, the paired schema and the per-target checks otherwise reach the model nowhere.
    /// An enum rather than a switch, so another rendering can be added beside the one measured.
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
    /// <see cref="PerTarget"/> lists the actions and targets as
    /// <see cref="DecideOptions.ListActionsAndTargets"/> does, whether that is on or not, with only
    /// the targets some offered action may take, at most <see cref="DecideOptions.MaxListedTargets"/>.
    /// <see cref="ObservationLine"/> leaves the system prompt as <see cref="DecideOptions.ListActionsAndTargets"/>
    /// makes it and says the pairs in one sentence among the observations instead. Either way an
    /// action that requires a target and may take none is not offered.
    /// </para>
    /// <para>
    /// Measured on 10 November 2026 (findings, "Saying which targets each action may take"):
    /// <see cref="PerTarget"/> gained 4.1 points beyond noise on <c>qwen3.5:4b</c> where actions take
    /// only some targets, and lost 3.9 to 4.4 beyond noise on <c>qwen3.5:2b</c>, for about 50 to 140
    /// more prompt tokens. Do not use it with a 2B-class model. To keep any model off illegal pairs,
    /// use <see cref="DecideOptions.PairTargetsWithActions"/>, which enforces the same pairs in the
    /// schema at no prompt cost.
    /// </para>
    /// </remarks>
    public enum LegalTargetListing
    {
        /// <summary>The default: nothing in the prompt says which action takes which target.</summary>
        Off = 0,

        /// <summary>
        /// Each target's line names the actions that may take it:
        /// <c>- wolf (for attack_target): A wolf from the woods.</c> An action that may take no listed
        /// target says so on its own line: <c>Target: no_target.</c>, or <c>Targets: none listed.</c>
        /// when the cap left its targets out.
        /// </summary>
        PerTarget = 1,

        /// <summary>
        /// One sentence among the observations, grouped by action, naming the targets each action that
        /// takes one may take now (#244 item 5): <c>Moves open to you: attack fenmark or westmarch;
        /// fortify ashby; declare_war orsk.</c> It goes right after the observation sources' lines and
        /// before the call's own, so it reads as a standing fact, where a game's own line of this kind
        /// would sit. Actions that take only <c>no_target</c> are left out, since the sentence names
        /// targets; with no action taking a listed target there is no sentence.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The system prompt is not changed: its list is <see cref="DecideOptions.ListActionsAndTargets"/>'s,
        /// unannotated, or none. The sentence names actions by id, the values the schema offers, and
        /// targets in schema order, at most <see cref="DecideOptions.MaxListedTargets"/> of them in all;
        /// one past the cap goes into <see cref="DecisionTelemetry.TargetsUnlisted"/>. It is rendered
        /// in the request only: it is not in <see cref="AgentContext.Observations"/>, counts against no
        /// cap and no guard reads it.
        /// </para>
        /// <para>
        /// For GreyBoxStrategy's gap N8: in #247 the same pairs as a sentence among the game's facts
        /// made <c>qwen3.5:4b</c> attack 6 to 8 points more often than <see cref="PerTarget"/>'s
        /// annotated list did. Measured on 11 November 2026 (findings, "The legal moves as an
        /// observation line").
        /// </para>
        /// </remarks>
        ObservationLine = 2,
    }
}

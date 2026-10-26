namespace Agenerela
{
    /// <summary>
    /// One check between the provider's answer and the game's code (build plan §2.5). Every
    /// decision passes through an ordered list of these before any handler runs: the built-in
    /// guards first, <see cref="SchemaLegalityGuard"/> and then the grounding guard (#105), then
    /// the developer's own (#108).
    /// </summary>
    /// <remarks>
    /// <para>
    /// A guard judges an answer after the fact. To keep the model from choosing something at all,
    /// make it unavailable instead (<see cref="IActionHandler.IsAvailable"/>): an option that is
    /// absent from the schema beats one forbidden in prose.
    /// </para>
    /// <para>
    /// A guard is called on the main thread, once per decision, and only for a decision: a reply
    /// that could not be read, or a provider that failed, never reaches one. It never changes the
    /// decision itself; it returns a verdict, and the pipeline acts on it.
    /// </para>
    /// </remarks>
    public interface IDecisionGuard
    {
        /// <summary>
        /// What <see cref="DecisionTelemetry.GuardsFired"/> records when this guard stops a
        /// decision, and what <see cref="GuardVerdict.Guard"/> says. Short and stable, such as
        /// <c>"SchemaLegalityGuard"</c>.
        /// </summary>
        string Name { get; }

        /// <summary>Judges one decision.</summary>
        GuardVerdict Inspect(GuardInput input);
    }
}

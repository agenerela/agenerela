namespace Agenerela
{
    /// <summary>
    /// An optional addition to <see cref="IActionHandler"/>: whether the action can be done to one
    /// particular target right now, and why not (#190 LV #19, #216 item 5, DR-020). For a rule that
    /// categories cannot say, such as "chop any tree nobody has claimed" or "propose a route whose
    /// conditions are met".
    /// </summary>
    /// <remarks>
    /// <para>
    /// A handler that implements it is asked once per decision for each registered target the action
    /// takes (in its <see cref="ActionDefinition.TargetCategories"/>, or every target when it names
    /// none), and only while its <see cref="IActionHandler.IsAvailable"/> says yes.
    /// <see cref="TargetRegistry.NoTarget"/> is never asked about. Then:
    /// </para>
    /// <list type="bullet">
    ///   <item><description>
    ///   An action that requires a target, and has no target it takes that this check allows, is left
    ///   out of the schema, flat or paired. That is state masking the developer opts into by
    ///   implementing this interface: the option is absent, not forbidden in prose.
    ///   </description></item>
    ///   <item><description>
    ///   The paired schema (<see cref="DecideOptions.PairTargetsWithActions"/>) leaves out each pair
    ///   this check rejects.
    ///   </description></item>
    ///   <item><description>
    ///   The flat target enum stays every registered target, so target grounding and the prompt's
    ///   target list read the same list as without it. <see cref="TargetAvailabilityGuard"/> turns a
    ///   rejected pair the model names anyway into <c>none</c>, and <see cref="Agent.Execute(DecisionResult)"/>
    ///   asks again, so a decision gone stale while the model thought is refused with the reason.
    ///   </description></item>
    /// </list>
    /// <para>
    /// Rejected pairs and their reasons are recorded in <see cref="DecisionTelemetry.TargetsUnavailable"/>
    /// and shown by <see cref="Agent.Preview"/>. No prompt line names them.
    /// </para>
    /// <para>
    /// The code front door's form is <see cref="AvailableForAttribute"/>, and
    /// <see cref="DelegateActionHandler.IsAvailableFor"/> is the delegate's. A handler without either
    /// behaves exactly as before: the same schema, the same prompt, byte for byte.
    /// </para>
    /// <para>
    /// Called on the main thread, as <see cref="IActionHandler.IsAvailable"/> is. An exception
    /// propagates as one from <see cref="IActionHandler.IsAvailable"/> does: a decision ends in a
    /// <see cref="PipelineErrorKind.RequestPreparation"/> error, and <see cref="Agent.Execute(DecisionResult)"/> throws.
    /// </para>
    /// </remarks>
    public interface ITargetAvailability
    {
        /// <summary>Whether this action can be done to <paramref name="targetId"/> right now.</summary>
        /// <param name="ctx">The context of the decision, or of the <see cref="Agent.Execute(DecisionResult)"/> asking again.</param>
        /// <param name="targetId">A registered target's id, never <see cref="TargetRegistry.NoTarget"/>.</param>
        Availability IsAvailableFor(AgentContext ctx, string targetId);
    }
}

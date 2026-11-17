namespace Agenerela
{
    /// <summary>
    /// An optional handler interface: whether one value of one of the action's parameters can be
    /// chosen right now, such as a quantity the stall can cover (DR-023, first step). The per-value
    /// counterpart of <see cref="ITargetAvailability"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Asked once a decision for each declared value of each parameter, while the action itself is
    /// available. A value it rejects is absent from that decision's schema and from the prompt's list,
    /// its reason recorded in <see cref="DecisionTelemetry.ValuesUnavailable"/>; an action with a
    /// parameter that has no value left is left out of the decision, as one with no legal target is.
    /// <see cref="Agent.Execute(DecisionResult)"/> asks again and refuses a value gone stale.
    /// </para>
    /// <para>
    /// The code front door is <see cref="AvailableValueAttribute"/>, and
    /// <see cref="DelegateActionHandler.IsAvailableValue"/> the delegate one. A handler without a
    /// check offers every declared value.
    /// </para>
    /// </remarks>
    public interface IArgumentAvailability
    {
        /// <summary>Whether <paramref name="value"/> may be chosen for <paramref name="parameter"/> now, with a reason when not.</summary>
        Availability IsAvailableValue(AgentContext ctx, string parameter, string value);
    }
}

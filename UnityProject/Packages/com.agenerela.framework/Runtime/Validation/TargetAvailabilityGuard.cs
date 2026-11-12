using System;

namespace Agenerela
{
    /// <summary>
    /// The fourth built-in guard (#190 LV #19, #216 item 5, DR-020): the action can be done to this
    /// target right now, as the handler's per-target check (<see cref="ITargetAvailability"/>,
    /// <see cref="AvailableForAttribute"/> or <see cref="DelegateActionHandler.IsAvailableFor"/>) says.
    /// A pair it rejects becomes <c>none</c> before any handler sees it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// It acts only for an action whose handler has a per-target check, and only on a real target:
    /// every other action, <see cref="ActionRegistry.None"/> and <see cref="TargetRegistry.NoTarget"/>
    /// always pass. So it is always on and changes nothing until a developer gives a check.
    /// </para>
    /// <para>
    /// Inside <see cref="Agent.DecideAsync"/> it runs after <see cref="TargetCategoryGuard"/> and
    /// reads the answer the schema was built from, so it never asks the check again. With pairing
    /// off, the flat target enum offers every registered target with every action, so this guard is
    /// what catches a rejected pair; its verdict is <see cref="GuardVerdict.RewriteToNone"/>, a
    /// contained refusal, as the category guard's is. With pairing on, the grammar should never have
    /// let the pair through, and <see cref="SchemaLegalityGuard"/> rejects one that comes anyway.
    /// </para>
    /// <para>
    /// <see cref="Agent.Execute(DecisionResult)"/> runs it again, asking the check against the agent as
    /// it is then, so a pair that went stale while the model thought is refused, with the check's
    /// reason in the <see cref="ActionResult"/>. The reason never reaches the model: a refusal line's
    /// request says only that it cannot be done right now.
    /// </para>
    /// </remarks>
    public sealed class TargetAvailabilityGuard : IDecisionGuard
    {
        // What a refusal line's request says instead of Reason, which is the developer's own words
        // (#109). Unmeasured, and it changes only what is said.
        private const string NotNow = "that cannot be done to it right now.";

        /// <summary>Always <c>"TargetAvailabilityGuard"</c>; <see cref="Agent.Execute(DecisionResult)"/>'s re-check records it with <c>"@Execute"</c> after it.</summary>
        public string Name => nameof(TargetAvailabilityGuard);

        /// <exception cref="ArgumentNullException"><paramref name="input"/> is null.</exception>
        public GuardVerdict Inspect(GuardInput input)
        {
            if (input == null)
            {
                throw new ArgumentNullException(nameof(input));
            }

            var decision = input.Decision;
            var ctx = input.Context;

            if (decision.ActionId == ActionRegistry.None || decision.TargetId == TargetRegistry.NoTarget)
            {
                return GuardVerdict.Pass;
            }

            // An unregistered action or target is SchemaLegalityGuard's to refuse, and a target of
            // the wrong kind TargetCategoryGuard's; both run first.
            if (!ctx.Actions.TryGet(decision.ActionId, out var definition, out var handler) ||
                !ctx.Targets.Contains(decision.TargetId) ||
                !definition.AcceptsCategory(ctx.Targets.CategoryOf(decision.TargetId)) ||
                TargetChecks.Of(handler) == null)
            {
                return GuardVerdict.Pass;
            }

            if (!input.AvailabilityAnswer.IsRejected(definition, decision.TargetId, out string reason))
            {
                return GuardVerdict.Pass;
            }

            string because = reason == null ? "its per-target check says no." : reason;
            return GuardVerdict.RewriteToNone(
                $"'{definition.Id}' cannot be done to '{decision.TargetId}' right now: {because}", NotNow, reason);
        }
    }
}

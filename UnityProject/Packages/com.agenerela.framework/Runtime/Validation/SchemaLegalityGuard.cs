using System;
using Agenerela.Providers;

namespace Agenerela
{
    /// <summary>
    /// The first built-in guard (build plan §2.5): the decision is one the agent could legally
    /// make. What the schema's grammar should already have enforced, checked again because
    /// providers are untrusted (§2.4). Every refusal is a <see cref="GuardVerdict.Reject"/>: the
    /// provider broke the contract.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The rules, in order. <see cref="ActionRegistry.None"/> always passes. Otherwise the action
    /// is registered; the target is <see cref="TargetRegistry.NoTarget"/> or registered in the
    /// context's targets; an action that needs a target got a real one, which the schema cannot
    /// enforce because all actions share one target enum; and the action is available.
    /// </para>
    /// <para>
    /// Available means two things, depending on when it runs. Inside <see cref="Agent.DecideAsync"/>,
    /// the action was offered: it is in the action enum of the request's schema. That enum came
    /// from one availability answer, which the examples share, so the guard holds the model to
    /// exactly what it was shown and runs none of the developer's availability checks again. In
    /// <see cref="Agent.Execute"/>, which has no request, availability is recomputed through
    /// <see cref="ActionAvailability"/> against the agent as it is then, never cached, since the
    /// world can change while the model thinks (hard rule 5). An exception from the developer's
    /// availability check propagates there, as it always has from <see cref="Agent.Execute"/>.
    /// </para>
    /// </remarks>
    public sealed class SchemaLegalityGuard : IDecisionGuard
    {
        /// <summary>Always <c>"SchemaLegalityGuard"</c>; <see cref="Agent.Execute"/>'s re-check records it with <c>"@Execute"</c> after it.</summary>
        public string Name => nameof(SchemaLegalityGuard);

        /// <exception cref="ArgumentNullException"><paramref name="input"/> is null.</exception>
        public GuardVerdict Inspect(GuardInput input)
        {
            if (input == null)
            {
                throw new ArgumentNullException(nameof(input));
            }

            var decision = input.Decision;
            var ctx = input.Context;

            if (decision.ActionId == ActionRegistry.None)
            {
                return GuardVerdict.Pass;
            }

            if (!ctx.Actions.TryGet(decision.ActionId, out var definition, out _))
            {
                return GuardVerdict.Reject("the action is not registered.");
            }

            if (decision.TargetId != TargetRegistry.NoTarget && !ctx.Targets.Contains(decision.TargetId))
            {
                return GuardVerdict.Reject("the target is not registered.");
            }

            if (definition.RequiresTarget && decision.TargetId == TargetRegistry.NoTarget)
            {
                return GuardVerdict.Reject("the action needs a target, and the decision has none.");
            }

            if (input.Request != null)
            {
                if (!Offered(input.Request, decision.ActionId))
                {
                    return GuardVerdict.Reject("the action was not offered: it was not available for this decision.");
                }
            }
            else if (!IsAvailable(definition, ctx))
            {
                return GuardVerdict.Reject("the action is not available right now.");
            }

            return GuardVerdict.Pass;
        }

        // In the action enum the model was shown. A request without a schema offered nothing.
        private static bool Offered(DecisionRequest request, string actionId)
        {
            if (request.Schema == null)
            {
                return false;
            }

            foreach (var field in request.Schema.Fields)
            {
                if (field.Name != DecisionSchema.ActionFieldName || field.AllowedValues == null)
                {
                    continue;
                }

                foreach (string allowed in field.AllowedValues)
                {
                    if (string.Equals(allowed, actionId, StringComparison.Ordinal))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        // Asks ActionAvailability, the same rule the schema was built by, rather than the handler
        // alone, so the two can never disagree about what "available" means.
        private static bool IsAvailable(ActionDefinition definition, AgentContext ctx)
        {
            var available = ActionAvailability.For(ctx);
            foreach (var candidate in available)
            {
                if (candidate == definition)
                {
                    return true;
                }
            }

            return false;
        }
    }
}

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
    /// context's targets; an action that needs a target got a real one, which a flat schema cannot
    /// enforce because all actions share one target enum; an action that takes no target
    /// (<see cref="ActionDefinition.TakesNoTarget"/>) got none; and the action is available. Against a
    /// request whose schema pairs actions with targets (<see cref="DecisionSchema.Pairs"/>, DR-020),
    /// the action must also have been offered with that target, which the grammar should have
    /// enforced.
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

            // Inside DecideAsync a flat schema's stray target was already dropped (#244 item 2), so
            // this is a paired schema whose grammar forbade it, or a decision Execute was handed.
            if (definition.TakesNoTarget && decision.TargetId != TargetRegistry.NoTarget)
            {
                return GuardVerdict.Reject("the action takes no target, and the decision has one.");
            }

            if (input.Request != null)
            {
                if (!Offered(input.Request, decision.ActionId))
                {
                    return GuardVerdict.Reject("the action was not offered: it was not available for this decision.");
                }

                if (!OfferedTogether(input.Request, decision.ActionId, decision.TargetId))
                {
                    return GuardVerdict.Reject("the target was not offered with this action: the schema paired them apart.");
                }
            }
            else
            {
                // Asked once per input, the same rule the schema was built by, so the two can never
                // disagree about what "available" means.
                var answer = input.AvailabilityAnswer;
                if (!answer.IsOffered(definition))
                {
                    // Left out because no target it takes is allowed now (#190 LV #19): say why the
                    // decision's own target is not, when its check said.
                    if (answer.TryGetLeftOut(definition, out var because) && because == LeftOutBecause.NoLegalTarget)
                    {
                        string why = answer.IsRejected(definition, decision.TargetId, out string reason) && reason != null
                            ? $" '{decision.TargetId}': {reason}"
                            : "";
                        return GuardVerdict.Reject("the action is not available right now: no target it takes is." + why,
                            why.Length > 0 ? reason : null);
                    }

                    return GuardVerdict.Reject("the action is not available right now.");
                }
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

        // In one pair with the target, when the schema paired them; always, when it did not.
        private static bool OfferedTogether(DecisionRequest request, string actionId, string targetId)
        {
            var pairs = request.Schema?.Pairs;
            if (pairs == null)
            {
                return true;
            }

            foreach (var pair in pairs)
            {
                if (Contains(pair.Actions, actionId))
                {
                    return Contains(pair.Targets, targetId);
                }
            }

            return false;
        }

        private static bool Contains(System.Collections.Generic.IReadOnlyList<string> values, string id)
        {
            foreach (string value in values)
            {
                if (string.Equals(value, id, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }
}

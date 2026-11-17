using System;
using System.Collections.Generic;

namespace Agenerela
{
    /// <summary>Filters the registered action vocabulary down to the actions legal right now.</summary>
    public static class ActionAvailability
    {
        /// <summary>
        /// The actions legal for this agent right now. Recomputed every decision; never cached,
        /// because an agent's state and visible targets can change between two decisions.
        /// </summary>
        /// <remarks>
        /// An action is left out when it needs a target and none is registered, when its handler's
        /// <see cref="IActionHandler.IsAvailable"/> says no, and, for a handler with a per-target check
        /// (<see cref="ITargetAvailability"/>), when it needs a target and no registered target it
        /// takes is one the check allows.
        /// </remarks>
        public static IReadOnlyList<ActionDefinition> For(AgentContext ctx)
        {
            return Evaluate(ctx).Offered;
        }

        // The whole answer for one decision: what is offered, what is left out and why, and every
        // pair a per-target check rejected. Asked once per decision, so the schema, the examples,
        // the preview, the telemetry and the guard all read one answer, even from a check that is
        // not a pure function of the context.
        internal static AvailabilityAnswer Evaluate(AgentContext ctx)
        {
            if (ctx == null)
            {
                throw new ArgumentNullException(nameof(ctx));
            }

            // Start with an empty result for this one decision. This answer is intentionally new
            // every call, so yesterday's action state cannot leak into today's request.
            var answer = new AvailabilityAnswer();

            // Definitions is the model-facing action order from ActionRegistry. Iterating this
            // list preserves that stable order while removing options that do not make sense now.
            foreach (var definition in ctx.Actions.Definitions)
            {
                // Target-requiring actions are impossible when the agent has nothing it may
                // reference. Leaving them out of the schema prevents the model from choosing
                // nonsense such as "walk_to" when TargetRegistry is empty.
                if (definition.RequiresTarget && ctx.Targets.Count == 0)
                {
                    answer.LeaveOut(definition, LeftOutBecause.NoTarget);
                    continue;
                }

                // The handler owns game-specific legality, such as "do not offer follow_player
                // while the agent is already following." Structural filtering beats prompt prose:
                // if this says no, the option is absent from the schema.
                var handler = ctx.Actions.HandlerFor(definition);
                if (!handler.IsAvailable(ctx))
                {
                    answer.LeaveOut(definition, LeftOutBecause.Handler);
                    continue;
                }

                // Per target (#190 LV #19, DR-020): only for a handler that has a check, so one
                // without behaves exactly as before. Only the targets the action takes are asked
                // about; categories already rule out the rest.
                var check = TargetChecks.Of(handler);
                if (check != null)
                {
                    answer.MarkChecked(definition);
                    bool anyAllowed = false;
                    foreach (string id in ctx.Targets.Ids)
                    {
                        if (!definition.AcceptsCategory(ctx.Targets.CategoryOf(id)))
                        {
                            continue;
                        }

                        var availability = check(ctx, id);
                        if (availability.IsAvailable)
                        {
                            anyAllowed = true;
                        }
                        else
                        {
                            answer.Reject(definition, id, availability.Reason);
                        }
                    }

                    // An action that needs a target and has no legal one left is masked in the flat
                    // schema too: the developer opted into it by giving the check.
                    if (definition.RequiresTarget && !anyAllowed)
                    {
                        answer.LeaveOut(definition, LeftOutBecause.NoLegalTarget);
                        continue;
                    }
                }

                // Per value (DR-023): only for an action that declares parameters and a handler
                // that has a check. A parameter with no value left leaves the action out, as an
                // action with no legal target is: there is no answer the schema could take.
                var valueCheck = definition.DeclaresParameters ? ValueChecks.Of(handler) : null;
                if (valueCheck != null)
                {
                    bool everyParameterHasOne = true;
                    foreach (var parameter in definition.Parameters)
                    {
                        bool anyValue = false;
                        foreach (string value in parameter.Values)
                        {
                            var availability = valueCheck(ctx, parameter.Name, value);
                            if (availability.IsAvailable)
                            {
                                anyValue = true;
                            }
                            else
                            {
                                answer.RejectValue(definition, parameter.Name, value, availability.Reason);
                            }
                        }

                        everyParameterHasOne &= anyValue;
                    }

                    if (!everyParameterHasOne)
                    {
                        answer.LeaveOut(definition, LeftOutBecause.NoLegalValue);
                        continue;
                    }
                }

                answer.Offer(definition);
            }

            return answer;
        }
    }

    // Why ActionAvailability left an action out.
    internal enum LeftOutBecause
    {
        // It needs a target, and none is registered
        NoTarget,

        // Its handler's IsAvailable said no
        Handler,

        // It needs a target, and its per-target check allows none of those it takes
        NoLegalTarget,

        // One of its parameters has no value its per-value check allows (DR-023)
        NoLegalValue,
    }

    // One decision's availability, asked once (ActionAvailability.Evaluate).
    internal sealed class AvailabilityAnswer
    {
        private readonly List<ActionDefinition> offered = new List<ActionDefinition>();
        private readonly Dictionary<ActionDefinition, LeftOutBecause> leftOut = new Dictionary<ActionDefinition, LeftOutBecause>();
        private readonly Dictionary<ActionDefinition, Dictionary<string, string>> rejected =
            new Dictionary<ActionDefinition, Dictionary<string, string>>();
        private readonly List<UnavailableTarget> unavailable = new List<UnavailableTarget>();
        private readonly HashSet<ActionDefinition> checkedPerTarget = new HashSet<ActionDefinition>();
        private readonly Dictionary<ActionDefinition, Dictionary<string, string>> rejectedValues =
            new Dictionary<ActionDefinition, Dictionary<string, string>>();
        private readonly List<UnavailableValue> unavailableValues = new List<UnavailableValue>();

        // The actions offered, in registry order.
        public IReadOnlyList<ActionDefinition> Offered => offered;

        // Every pair a per-target check rejected, in registry then target order, offered actions
        // and left-out ones alike.
        public IReadOnlyList<UnavailableTarget> Unavailable => unavailable;

        // Every value a per-value check rejected, in registry, parameter then value order (DR-023).
        public IReadOnlyList<UnavailableValue> UnavailableValues => unavailableValues;

        public void Offer(ActionDefinition definition)
        {
            offered.Add(definition);
        }

        public void RejectValue(ActionDefinition definition, string parameter, string value, string reason)
        {
            if (!rejectedValues.TryGetValue(definition, out var values))
            {
                values = new Dictionary<string, string>(StringComparer.Ordinal);
                rejectedValues.Add(definition, values);
            }

            values[ValueKey(parameter, value)] = reason;
            unavailableValues.Add(new UnavailableValue(definition.Id, parameter, value, reason));
        }

        public bool IsValueRejected(ActionDefinition definition, string parameter, string value, out string reason)
        {
            reason = null;
            return rejectedValues.TryGetValue(definition, out var values) && values.TryGetValue(ValueKey(parameter, value), out reason);
        }

        // The declared values of one parameter that no per-value check rejected, in declared order.
        public List<string> ValuesOf(ActionDefinition definition, ActionParameter parameter)
        {
            var values = new List<string>(parameter.Values.Length);
            foreach (string value in parameter.Values)
            {
                if (!IsValueRejected(definition, parameter.Name, value, out _))
                {
                    values.Add(value);
                }
            }

            return values;
        }

        // The values rejected for one action, as "name 'value' (reason)" phrases, for a sentence.
        public string RejectedValuesPhrase(ActionDefinition definition)
        {
            var parts = new List<string>();
            foreach (var value in unavailableValues)
            {
                if (value.Action == definition.Id)
                {
                    parts.Add(value.Reason == null ? $"{value.Parameter} '{value.Value}'" : $"{value.Parameter} '{value.Value}' ({value.Reason})");
                }
            }

            return string.Join(", ", parts);
        }

        // A parameter name is snake_case with no whitespace (ActionParameter.Validate), so a space
        // cannot be part of it and the key is unambiguous.
        private static string ValueKey(string parameter, string value) => parameter + " " + value;

        public void LeaveOut(ActionDefinition definition, LeftOutBecause because)
        {
            leftOut[definition] = because;
        }

        public void Reject(ActionDefinition definition, string targetId, string reason)
        {
            if (!rejected.TryGetValue(definition, out var targets))
            {
                targets = new Dictionary<string, string>(StringComparer.Ordinal);
                rejected.Add(definition, targets);
            }

            targets[targetId] = reason;
            unavailable.Add(new UnavailableTarget(definition.Id, targetId, reason));
        }

        // The action's handler has a per-target check, which this answer asked.
        public void MarkChecked(ActionDefinition definition)
        {
            checkedPerTarget.Add(definition);
        }

        // Whether an example may name this target for this action (#235 follow-up). Always for an
        // action whose handler has no per-target check, so its examples read exactly as before; for
        // one with a check, only a target in the action's categories that the check allowed, the
        // same pairs the schema offers.
        public bool ExampleMayName(ActionDefinition definition, string targetId, string category)
        {
            if (!checkedPerTarget.Contains(definition))
            {
                return true;
            }

            return definition.AcceptsCategory(category) && Allows(definition, targetId);
        }

        public bool IsOffered(ActionDefinition definition)
        {
            return offered.Contains(definition);
        }

        public bool TryGetLeftOut(ActionDefinition definition, out LeftOutBecause because)
        {
            return leftOut.TryGetValue(definition, out because);
        }

        // False when a per-target check rejected this pair; true otherwise, no_target included.
        public bool Allows(ActionDefinition definition, string targetId)
        {
            return !IsRejected(definition, targetId, out _);
        }

        public bool IsRejected(ActionDefinition definition, string targetId, out string reason)
        {
            reason = null;
            return rejected.TryGetValue(definition, out var targets) && targets.TryGetValue(targetId, out reason);
        }

        // The pairs rejected for one action, as "'id' (reason)" phrases, for a sentence.
        public string RejectedPhrase(ActionDefinition definition)
        {
            if (!rejected.TryGetValue(definition, out var targets))
            {
                return "";
            }

            var parts = new List<string>();
            foreach (var pair in unavailable)
            {
                if (pair.Action == definition.Id && targets.ContainsKey(pair.Target))
                {
                    parts.Add(pair.Reason == null ? $"'{pair.Target}'" : $"'{pair.Target}' ({pair.Reason})");
                }
            }

            return string.Join(", ", parts);
        }
    }

    // The per-value check behind a handler, or null when it has none (DR-023).
    internal static class ValueChecks
    {
        public static Func<AgentContext, string, string, Availability> Of(IActionHandler handler)
        {
            switch (handler)
            {
                case DelegateActionHandler delegated:
                    return delegated.IsAvailableValue;
                case MethodActionHandler method:
                    return method.ValueCheck;
                case IArgumentAvailability checks:
                    return checks.IsAvailableValue;
                default:
                    return null;
            }
        }
    }

    // The per-target check behind a handler, or null when it has none (#190 LV #19).
    internal static class TargetChecks
    {
        public static Func<AgentContext, string, Availability> Of(IActionHandler handler)
        {
            // The framework's own two handlers carry the check as an optional member, so one built
            // without it has none, rather than one that always says yes.
            switch (handler)
            {
                case DelegateActionHandler delegated:
                    return delegated.IsAvailableFor;
                case MethodActionHandler method:
                    return method.TargetCheck;
                case ITargetAvailability checks:
                    return checks.IsAvailableFor;
                default:
                    return null;
            }
        }
    }
}

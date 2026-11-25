using System;
using System.Collections.Generic;
using System.Reflection;

namespace Agenerela
{
    /// <summary>
    /// The code front door's reader (DR-011): turns each [AgentAction] method on an object into an
    /// <see cref="ActionDefinition"/> and a handler that calls the method. Used by
    /// <see cref="ActionRegistry.RegisterMethods"/>.
    /// </summary>
    /// <remarks>
    /// Only reflection reaches the game's methods, so nothing here names a scene type even when a
    /// method takes one. IL2CPP's managed-code stripping can remove a method that nothing calls
    /// directly; keeping these ([Preserve] or a link.xml) is part of the Phase 6b player test
    /// (DR-011).
    /// </remarks>
    internal static class AgentActionReader
    {
        private const BindingFlags Declared = BindingFlags.DeclaredOnly | BindingFlags.Instance |
                                              BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        // One [AgentAction] method, and how far from the owner's own type it was declared.
        private struct Found
        {
            public MethodInfo Method;
            public AgentActionAttribute Action;
            public ExampleAttribute Example;
            public CounterExampleAttribute CounterExample;
            public ActionParameter[] Parameters;
            public int Depth;
        }

        /// <summary>
        /// The owner's actions, base class first and each class's in declaration order, so the
        /// schema enum comes out the same every time. Throws ArgumentException for a malformed
        /// method or attribute, or for an id that is malformed, reserved or declared twice.
        /// </summary>
        public static List<(ActionDefinition Definition, IActionHandler Handler)> Read(object owner)
        {
            var found = new List<Found>();
            var checks = new Dictionary<string, MethodInfo>();
            var targetChecks = new Dictionary<string, MethodInfo>();
            // Action id, then parameter name, to its [AvailableValue] method (DR-023).
            var valueChecks = new Dictionary<string, Dictionary<string, MethodInfo>>();
            var counted = new HashSet<RuntimeMethodHandle>();

            // Every class in the hierarchy, private methods included, so a base class can declare
            // actions its subclasses share. Most derived first: an override is met before the method
            // it overrides, and its attributes are the ones that count.
            int depth = 0;
            for (Type type = owner.GetType(); type != null; type = type.BaseType, depth++)
            {
                // Reflection promises no order. Declaration order keeps the registry and every
                // message below the same from one run to the next.
                MethodInfo[] methods = type.GetMethods(Declared);
                Array.Sort(methods, (a, b) => a.MetadataToken.CompareTo(b.MetadataToken));

                foreach (MethodInfo method in methods)
                {
                    bool isAction = method.IsDefined(typeof(AgentActionAttribute), false);
                    bool isCheck = method.IsDefined(typeof(AvailableAttribute), false);
                    bool isTargetCheck = method.IsDefined(typeof(AvailableForAttribute), false);
                    bool hasExample = method.IsDefined(typeof(ExampleAttribute), false);
                    bool hasCounterExample = method.IsDefined(typeof(CounterExampleAttribute), false);
                    bool hasParameters = method.IsDefined(typeof(AgentParameterAttribute), false);
                    bool isValueCheck = method.IsDefined(typeof(AvailableValueAttribute), false);
                    if (!isAction && !isCheck && !isTargetCheck && !hasExample && !hasCounterExample && !hasParameters && !isValueCheck)
                    {
                        continue;
                    }

                    // An overridden method counts once, whichever declaration carries the attributes.
                    if (!counted.Add(method.GetBaseDefinition().MethodHandle))
                    {
                        continue;
                    }

                    if (isAction && isCheck)
                    {
                        throw Error($"{Name(method)} has both [AgentAction] and [Available]. " +
                                    "Give the check its own method that returns bool.");
                    }

                    if (isAction && isTargetCheck)
                    {
                        throw Error($"{Name(method)} has both [AgentAction] and [AvailableFor]. " +
                                    "Give the per-target check its own method that takes the target's id.");
                    }

                    if (isCheck && isTargetCheck)
                    {
                        throw Error($"{Name(method)} has both [Available] and [AvailableFor]. " +
                                    "[Available] takes nothing or one AgentContext, and [AvailableFor] takes the target's id: give each its own method.");
                    }

                    if (isValueCheck && (isAction || isCheck || isTargetCheck))
                    {
                        throw Error($"{Name(method)} has [AvailableValue] beside another agent attribute. " +
                                    "Give the per-value check its own method that takes the value.");
                    }

                    if (hasParameters && !isAction)
                    {
                        throw Error($"{Name(method)} has [AgentParameter] but no [AgentAction]. " +
                                    "A parameter goes on the action's own method.");
                    }

                    ActionParameter[] declared = null;
                    if (isAction)
                    {
                        var action = method.GetCustomAttribute<AgentActionAttribute>(false);
                        declared = ParametersOf(method);
                        CheckActionMethod(method, declared);
                        CheckId(method, action.Id);

                        int twin = found.FindIndex(f => f.Action.Id == action.Id);
                        if (twin >= 0)
                        {
                            throw Error($"Action id '{action.Id}' is declared twice on {owner.GetType().Name}: " +
                                        $"{Name(found[twin].Method)} and {Name(method)}.");
                        }

                        found.Add(new Found
                        {
                            Method = method,
                            Action = action,
                            Example = method.GetCustomAttribute<ExampleAttribute>(false),
                            CounterExample = method.GetCustomAttribute<CounterExampleAttribute>(false),
                            Parameters = declared,
                            Depth = depth
                        });
                    }
                    else if (hasExample)
                    {
                        throw Error($"{Name(method)} has [Example] but no [AgentAction]. " +
                                    "The example goes on the action's own method.");
                    }
                    else if (hasCounterExample)
                    {
                        throw Error($"{Name(method)} has [CounterExample] but no [AgentAction]. " +
                                    "The counter-example goes on the action's own method.");
                    }

                    if (isCheck)
                    {
                        CheckAvailableMethod(method);
                        foreach (var available in method.GetCustomAttributes<AvailableAttribute>(false))
                        {
                            string id = available.ActionId ?? "";
                            if (checks.TryGetValue(id, out MethodInfo other))
                            {
                                throw Error($"Action '{id}' has two [Available] checks: {Name(other)} and {Name(method)}.");
                            }

                            checks.Add(id, method);
                        }
                    }

                    if (isValueCheck)
                    {
                        CheckAvailableValueMethod(method);
                        foreach (var availableValue in method.GetCustomAttributes<AvailableValueAttribute>(false))
                        {
                            string id = availableValue.ActionId ?? "";
                            string parameter = availableValue.Parameter ?? "";
                            if (!valueChecks.TryGetValue(id, out var byParameter))
                            {
                                byParameter = new Dictionary<string, MethodInfo>(StringComparer.Ordinal);
                                valueChecks.Add(id, byParameter);
                            }

                            if (byParameter.TryGetValue(parameter, out MethodInfo other))
                            {
                                throw Error($"Parameter '{parameter}' of action '{id}' has two [AvailableValue] checks: {Name(other)} and {Name(method)}.");
                            }

                            byParameter.Add(parameter, method);
                        }
                    }

                    if (isTargetCheck)
                    {
                        CheckAvailableForMethod(method);
                        foreach (var availableFor in method.GetCustomAttributes<AvailableForAttribute>(false))
                        {
                            string id = availableFor.ActionId ?? "";
                            if (targetChecks.TryGetValue(id, out MethodInfo other))
                            {
                                throw Error($"Action '{id}' has two [AvailableFor] checks: {Name(other)} and {Name(method)}.");
                            }

                            targetChecks.Add(id, method);
                        }
                    }
                }
            }

            // A check must name an action here. A typo would otherwise leave that action always
            // available, and nothing would say so.
            foreach (var check in checks)
            {
                if (!found.Exists(f => f.Action.Id == check.Key))
                {
                    throw Error($"{Name(check.Value)} is the [Available] check for '{check.Key}', " +
                                $"but {owner.GetType().Name} has no [AgentAction] with that id.");
                }
            }

            foreach (var check in targetChecks)
            {
                if (!found.Exists(f => f.Action.Id == check.Key))
                {
                    throw Error($"{Name(check.Value)} is the [AvailableFor] check for '{check.Key}', " +
                                $"but {owner.GetType().Name} has no [AgentAction] with that id.");
                }
            }

            // A per-value check must name an action here and one of its parameters, for the same reason.
            foreach (var check in valueChecks)
            {
                int at = found.FindIndex(f => f.Action.Id == check.Key);
                foreach (var byParameter in check.Value)
                {
                    if (at < 0)
                    {
                        throw Error($"{Name(byParameter.Value)} is an [AvailableValue] check for '{check.Key}', " +
                                    $"but {owner.GetType().Name} has no [AgentAction] with that id.");
                    }

                    if (!Array.Exists(found[at].Parameters, p => p.Name == byParameter.Key))
                    {
                        throw Error($"{Name(byParameter.Value)} is the [AvailableValue] check for '{byParameter.Key}', " +
                                    $"but the action '{check.Key}' declares no [AgentParameter] with that name.");
                    }
                }
            }

            // Base class first, then declaration order. The schema enum (#8) is built in
            // registration order.
            found.Sort((a, b) => a.Depth != b.Depth
                ? b.Depth.CompareTo(a.Depth)
                : a.Method.MetadataToken.CompareTo(b.Method.MetadataToken));

            var actions = new List<(ActionDefinition Definition, IActionHandler Handler)>(found.Count);
            foreach (var f in found)
            {
                checks.TryGetValue(f.Action.Id, out MethodInfo check);
                targetChecks.TryGetValue(f.Action.Id, out MethodInfo targetCheck);
                var definition = new ActionDefinition
                {
                    Id = f.Action.Id,
                    Description = f.Action.Description,
                    RequiresTarget = f.Action.RequiresTarget,
                    TakesNoTarget = f.Action.TakesNoTarget,
                    // An action without [Example] reads like an asset whose example was left blank.
                    ExampleStimulus = f.Example?.Stimulus ?? "",
                    PreferredExampleTargets = f.Example?.PreferredTargets ?? Array.Empty<string>(),
                    ExampleStatement = f.Example?.Statement ?? "",
                    // Copied, so the definition never shares an array with the attribute.
                    ExampleObservations = f.Example?.Observations == null ? Array.Empty<string>() : (string[])f.Example.Observations.Clone(),
                    CounterExampleStimulus = f.CounterExample?.Stimulus ?? "",
                    CounterExampleStatement = f.CounterExample?.Statement ?? "",
                    // Copied, so the definition never shares an array with the attribute.
                    TargetCategories = f.Action.TargetCategories == null
                        ? Array.Empty<string>()
                        : (string[])f.Action.TargetCategories.Clone(),
                    Parameters = f.Parameters
                };

                valueChecks.TryGetValue(f.Action.Id, out var perValue);
                actions.Add((definition, new MethodActionHandler(owner, f.Method, check, targetCheck, perValue)));
            }

            return actions;
        }

        /// <summary>"Type.Method", for messages.</summary>
        internal static string Name(MethodInfo method)
        {
            return $"{method.DeclaringType.Name}.{method.Name}";
        }

        // The method's [AgentParameter]s as parameters, by Order then name: reflection promises no
        // order for several attributes on one method. Each must pass ActionParameter.Validate, and
        // no two may share a name.
        private static ActionParameter[] ParametersOf(MethodInfo method)
        {
            var attributes = new List<AgentParameterAttribute>(method.GetCustomAttributes<AgentParameterAttribute>(false));
            attributes.Sort((a, b) => a.Order != b.Order
                ? a.Order.CompareTo(b.Order)
                : string.CompareOrdinal(a.Name, b.Name));

            var parameters = new ActionParameter[attributes.Count];
            var names = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < attributes.Count; i++)
            {
                var attribute = attributes[i];
                parameters[i] = new ActionParameter
                {
                    Name = attribute.Name,
                    Description = attribute.Description,
                    // Copied, so the definition never shares an array with the attribute.
                    Values = attribute.Values == null ? null : (string[])attribute.Values.Clone(),
                    MaxLength = attribute.MaxLength,
                    Default = attribute.Default,
                    ExampleValue = attribute.ExampleValue,
                };

                if (!ActionParameter.Validate(parameters[i], out string problem))
                {
                    throw Error($"{Name(method)}: {problem}");
                }

                if (!names.Add(attribute.Name))
                {
                    throw Error($"{Name(method)} declares the parameter '{attribute.Name}' twice.");
                }
            }

            return parameters;
        }

        private static void CheckActionMethod(MethodInfo method, ActionParameter[] declared)
        {
            // ActionResult says what the action did (DR-018); void counts as done whenever it returns.
            if (method.ReturnType != typeof(void) && method.ReturnType != typeof(ActionResult))
            {
                throw Error($"{Name(method)} returns {method.ReturnType.Name}, but an [AgentAction] method must return void or ActionResult.");
            }

            if (method.ContainsGenericParameters)
            {
                throw Error($"{Name(method)} is generic, and an [AgentAction] method cannot be.");
            }

            bool takesContext = false;
            bool takesDecision = false;
            string target = null;
            foreach (ParameterInfo parameter in method.GetParameters())
            {
                Type type = parameter.ParameterType;
                if (type.IsByRef)
                {
                    throw Error($"{Name(method)}: parameter '{parameter.Name}' is ref, out or in, " +
                                "which an [AgentAction] method cannot take.");
                }

                var argument = parameter.GetCustomAttribute<ArgumentAttribute>(false);
                if (argument != null)
                {
                    var of = Array.Find(declared, p => p.Name == argument.Name);
                    if (of == null)
                    {
                        throw Error($"{Name(method)}: parameter '{parameter.Name}' takes the argument '{argument.Name}', " +
                                    "but the method has no [AgentParameter] with that name.");
                    }

                    if (type != typeof(string) && type != typeof(int))
                    {
                        throw Error($"{Name(method)}: parameter '{parameter.Name}' takes the argument '{argument.Name}' " +
                                    $"as {type.Name}, but an argument is a string or an int.");
                    }

                    if (type == typeof(int) && !of.IsNumber)
                    {
                        throw Error($"{Name(method)}: parameter '{parameter.Name}' takes '{argument.Name}' as an int, " +
                                    "but not every value of that parameter is a whole number.");
                    }

                    continue;
                }

                if (type == typeof(AgentContext))
                {
                    if (takesContext)
                    {
                        throw Error($"{Name(method)} takes two AgentContext parameters.");
                    }

                    takesContext = true;
                }
                else if (type == typeof(AgentDecision))
                {
                    if (takesDecision)
                    {
                        throw Error($"{Name(method)} takes two AgentDecision parameters.");
                    }

                    takesDecision = true;
                }
                else
                {
                    // Named like a declared parameter and without [Argument], it would be handed the
                    // target, or null: almost certainly a forgotten attribute (DR-023).
                    if (Array.Exists(declared, p => p.Name == parameter.Name))
                    {
                        throw Error($"{Name(method)}: parameter '{parameter.Name}' is named like the action's [AgentParameter] " +
                                    $"but has no [Argument(\"{parameter.Name}\")], so it would receive the target. Add the attribute, or rename it.");
                    }

                    if (target != null)
                    {
                        throw Error($"{Name(method)} takes two targets, '{target}' and '{parameter.Name}'. " +
                                    "An action has at most one: every parameter that is not an " +
                                    "AgentContext or an AgentDecision receives the target." +
                                    (declared.Length > 0 ? " One that should receive an [AgentParameter]'s value needs [Argument(\"name\")]." : ""));
                    }

                    target = parameter.Name;
                }
            }
        }

        private static void CheckAvailableMethod(MethodInfo method)
        {
            if (method.ReturnType != typeof(bool))
            {
                throw Error($"{Name(method)} returns {method.ReturnType.Name}, but an [Available] check must return bool.");
            }

            var parameters = method.GetParameters();
            if (method.ContainsGenericParameters || parameters.Length > 1 ||
                (parameters.Length == 1 && parameters[0].ParameterType != typeof(AgentContext)))
            {
                throw Error($"{Name(method)} is an [Available] check, so it takes nothing or one AgentContext.");
            }
        }

        private static void CheckAvailableValueMethod(MethodInfo method)
        {
            if (method.ReturnType != typeof(bool) && method.ReturnType != typeof(Availability))
            {
                throw Error($"{Name(method)} returns {method.ReturnType.Name}, but an [AvailableValue] check must return bool or Availability.");
            }

            var parameters = method.GetParameters();
            bool shape = parameters.Length == 1
                ? parameters[0].ParameterType == typeof(string)
                : parameters.Length == 2 && parameters[0].ParameterType == typeof(AgentContext) && parameters[1].ParameterType == typeof(string);
            if (method.ContainsGenericParameters || !shape)
            {
                throw Error($"{Name(method)} is an [AvailableValue] check, so it takes the value as a string, " +
                            "alone or after one AgentContext.");
            }
        }

        private static void CheckAvailableForMethod(MethodInfo method)
        {
            if (method.ReturnType != typeof(bool) && method.ReturnType != typeof(Availability))
            {
                throw Error($"{Name(method)} returns {method.ReturnType.Name}, but an [AvailableFor] check must return bool or Availability.");
            }

            var parameters = method.GetParameters();
            bool shape = parameters.Length == 1
                ? parameters[0].ParameterType == typeof(string)
                : parameters.Length == 2 && parameters[0].ParameterType == typeof(AgentContext) && parameters[1].ParameterType == typeof(string);
            if (method.ContainsGenericParameters || !shape)
            {
                throw Error($"{Name(method)} is an [AvailableFor] check, so it takes the target's id as a string, " +
                            "alone or after one AgentContext.");
            }
        }

        private static void CheckId(MethodInfo method, string id)
        {
            if (!ActionDefinition.ValidateId(id, out string problem))
            {
                throw Error($"{Name(method)}: {problem}.");
            }

            if (id == ActionRegistry.None)
            {
                throw Error($"{Name(method)}: action id '{ActionRegistry.None}' is reserved for the schema's idle action.");
            }
        }

        // Every problem found here is in the owner's code, which RegisterMethods received as "owner".
        private static ArgumentException Error(string message)
        {
            return new ArgumentException(message, "owner");
        }
    }
}

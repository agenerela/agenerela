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
                    bool hasExample = method.IsDefined(typeof(ExampleAttribute), false);
                    if (!isAction && !isCheck && !hasExample)
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

                    if (isAction)
                    {
                        var action = method.GetCustomAttribute<AgentActionAttribute>(false);
                        CheckActionMethod(method);
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
                            Depth = depth
                        });
                    }
                    else if (hasExample)
                    {
                        throw Error($"{Name(method)} has [Example] but no [AgentAction]. " +
                                    "The example goes on the action's own method.");
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

            // Base class first, then declaration order. The schema enum (#8) is built in
            // registration order.
            found.Sort((a, b) => a.Depth != b.Depth
                ? b.Depth.CompareTo(a.Depth)
                : a.Method.MetadataToken.CompareTo(b.Method.MetadataToken));

            var actions = new List<(ActionDefinition Definition, IActionHandler Handler)>(found.Count);
            foreach (var f in found)
            {
                checks.TryGetValue(f.Action.Id, out MethodInfo check);
                var definition = new ActionDefinition
                {
                    Id = f.Action.Id,
                    Description = f.Action.Description,
                    RequiresTarget = f.Action.RequiresTarget,
                    // An action without [Example] reads like an asset whose example was left blank.
                    ExampleStimulus = f.Example?.Stimulus ?? "",
                    PreferredExampleTargets = f.Example?.PreferredTargets ?? Array.Empty<string>()
                };

                actions.Add((definition, new MethodActionHandler(owner, f.Method, check)));
            }

            return actions;
        }

        /// <summary>"Type.Method", for messages.</summary>
        internal static string Name(MethodInfo method)
        {
            return $"{method.DeclaringType.Name}.{method.Name}";
        }

        private static void CheckActionMethod(MethodInfo method)
        {
            if (method.ReturnType != typeof(void))
            {
                throw Error($"{Name(method)} returns {method.ReturnType.Name}, but an [AgentAction] method must return void.");
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
                    if (target != null)
                    {
                        throw Error($"{Name(method)} takes two targets, '{target}' and '{parameter.Name}'. " +
                                    "An action has at most one: every parameter that is not an " +
                                    "AgentContext or an AgentDecision receives the target.");
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

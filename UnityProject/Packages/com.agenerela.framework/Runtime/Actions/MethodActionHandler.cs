using System;
using System.Reflection;
using System.Runtime.ExceptionServices;

namespace Agenerela
{
    /// <summary>
    /// The handler behind an [AgentAction] method: Execute calls the method, and IsAvailable calls
    /// its [Available] check. Built only by <see cref="AgentActionReader"/>, which has already
    /// checked both methods' shape.
    /// </summary>
    internal sealed class MethodActionHandler : IActionHandler
    {
        private readonly object owner;
        private readonly MethodInfo action;
        private readonly string name;
        private readonly Type[] parameterTypes;
        private readonly MethodInfo check;
        private readonly bool checkTakesContext;

        /// <param name="check">The [Available] method, or null when the action is always available.</param>
        public MethodActionHandler(object owner, MethodInfo action, MethodInfo check)
        {
            this.owner = owner;
            this.action = action;
            this.check = check;
            name = AgentActionReader.Name(action);

            var parameters = action.GetParameters();
            parameterTypes = new Type[parameters.Length];
            for (int i = 0; i < parameters.Length; i++)
            {
                parameterTypes[i] = parameters[i].ParameterType;
            }

            checkTakesContext = check != null && check.GetParameters().Length == 1;
        }

        public bool IsAvailable(AgentContext ctx)
        {
            if (check == null)
            {
                return true;
            }

            return (bool)Call(check, checkTakesContext ? new object[] { ctx } : Array.Empty<object>());
        }

        public void Execute(AgentContext ctx, AgentDecision decision)
        {
            // Each parameter is filled by its type; the reader allows at most one of each kind.
            var args = new object[parameterTypes.Length];
            for (int i = 0; i < args.Length; i++)
            {
                Type type = parameterTypes[i];
                if (type == typeof(AgentContext))
                {
                    args[i] = ctx;
                }
                else if (type == typeof(AgentDecision))
                {
                    args[i] = decision;
                }
                else
                {
                    args[i] = TargetFor(type, ctx, decision);
                }
            }

            Call(action, args);
        }

        // The object registered under the decision's target id, as the method's own parameter type.
        // It is passed as registered, never converted: turning one scene object into another would
        // take scene calls, and only Runtime/Unity/ may make those (DR-011).
        private object TargetFor(Type type, AgentContext ctx, AgentDecision decision)
        {
            if (ctx == null)
            {
                throw new ArgumentNullException(nameof(ctx), $"{name} takes a target, which is looked up in the context's targets.");
            }

            if (decision == null)
            {
                throw new ArgumentNullException(nameof(decision), $"{name} takes a target, which the decision names.");
            }

            if (decision.TargetId == TargetRegistry.NoTarget)
            {
                if (type.IsValueType && Nullable.GetUnderlyingType(type) == null)
                {
                    throw new InvalidOperationException(
                        $"{name} takes a {type.Name} target, which cannot be empty, " +
                        $"but the decision's target is '{TargetRegistry.NoTarget}'.");
                }

                return null;
            }

            if (!ctx.Targets.TryGet(decision.TargetId, out object target))
            {
                throw new InvalidOperationException(
                    $"'{decision.TargetId}' is not a registered target, so {name} cannot be given it.");
            }

            if (!type.IsInstanceOfType(target))
            {
                throw new InvalidOperationException(
                    $"{name} takes a {type.Name} target, but '{decision.TargetId}' is registered " +
                    $"as a {target.GetType().Name}. Register the object the method takes.");
            }

            return target;
        }

        private object Call(MethodInfo method, object[] args)
        {
            try
            {
                return method.Invoke(method.IsStatic ? null : owner, args);
            }
            catch (TargetInvocationException e) when (e.InnerException != null)
            {
                // Rethrow the game code's own exception with its own stack trace, so the console
                // points at the developer's line rather than at reflection's wrapper.
                ExceptionDispatchInfo.Capture(e.InnerException).Throw();
                throw;
            }
        }
    }
}

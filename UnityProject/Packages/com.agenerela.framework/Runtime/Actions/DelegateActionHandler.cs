using System;

namespace Agenerela
{
    /// <summary>Adapts delegates into an action handler without requiring a dedicated class.</summary>
    /// <remarks>
    /// Built with the constructor, it runs an <see cref="Action{T1, T2}"/> and reports
    /// <see cref="ExecutionStatus.Done"/> whenever it returns. Built with <see cref="WithResult"/>,
    /// its function reports what it did (DR-018).
    /// </remarks>
    public sealed class DelegateActionHandler : IActionHandlerWithResult
    {
        private readonly Func<AgentContext, bool> isAvailable;
        private readonly Func<AgentContext, AgentDecision, ActionResult> execute;

        public DelegateActionHandler(
            Func<AgentContext, bool> isAvailable,
            Action<AgentContext, AgentDecision> execute)
            : this(isAvailable, Done(execute), nameof(execute))
        {
        }

        private DelegateActionHandler(
            Func<AgentContext, bool> isAvailable,
            Func<AgentContext, AgentDecision, ActionResult> execute,
            string executeName)
        {
            if (isAvailable == null)
            {
                throw new ArgumentNullException(nameof(isAvailable));
            }

            if (execute == null)
            {
                throw new ArgumentNullException(executeName);
            }

            this.isAvailable = isAvailable;
            this.execute = execute;
        }

        /// <summary>
        /// A handler whose <paramref name="execute"/> returns what it did: <see cref="ActionResult.Done"/>,
        /// or <see cref="ActionResult.Failed"/> with the game's reason.
        /// </summary>
        /// <remarks>
        /// A factory rather than a second constructor, so <c>new DelegateActionHandler(check, null)</c>
        /// still means the constructor above and is not ambiguous.
        /// </remarks>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        public static DelegateActionHandler WithResult(
            Func<AgentContext, bool> isAvailable,
            Func<AgentContext, AgentDecision, ActionResult> execute)
        {
            return new DelegateActionHandler(isAvailable, execute, nameof(execute));
        }

        public bool IsAvailable(AgentContext ctx)
        {
            return isAvailable(ctx);
        }

        public void Execute(AgentContext ctx, AgentDecision decision)
        {
            execute(ctx, decision);
        }

        public ActionResult ExecuteWithResult(AgentContext ctx, AgentDecision decision)
        {
            return execute(ctx, decision);
        }

        // The constructor's action as a function that is done whenever it returns. Null stays null,
        // so the check above names the argument.
        private static Func<AgentContext, AgentDecision, ActionResult> Done(Action<AgentContext, AgentDecision> execute)
        {
            if (execute == null)
            {
                return null;
            }

            return (ctx, decision) =>
            {
                execute(ctx, decision);
                return ActionResult.Done();
            };
        }
    }
}

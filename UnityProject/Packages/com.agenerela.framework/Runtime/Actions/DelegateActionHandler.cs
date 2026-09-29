using System;

namespace Agenerela
{
    /// <summary>Adapts delegates into an action handler without requiring a dedicated class.</summary>
    public sealed class DelegateActionHandler : IActionHandler
    {
        private readonly Func<AgentContext, bool> isAvailable;
        private readonly Action<AgentContext, AgentDecision> execute;

        public DelegateActionHandler(
            Func<AgentContext, bool> isAvailable,
            Action<AgentContext, AgentDecision> execute)
        {
            if (isAvailable == null)
            {
                throw new ArgumentNullException(nameof(isAvailable));
            }

            if (execute == null)
            {
                throw new ArgumentNullException(nameof(execute));
            }

            this.isAvailable = isAvailable;
            this.execute = execute;
        }

        public bool IsAvailable(AgentContext ctx)
        {
            return isAvailable(ctx);
        }

        public void Execute(AgentContext ctx, AgentDecision decision)
        {
            execute(ctx, decision);
        }
    }
}

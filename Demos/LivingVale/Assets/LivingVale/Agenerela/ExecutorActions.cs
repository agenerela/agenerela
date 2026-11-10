using Agenerela;
using LivingVale.Game;
using LivingVale.Interaction;

namespace LivingVale.Agenerela
{
    /// <summary>
    /// What every Living Vale action handler does (DR-018, #195): runs the decision through the
    /// shared <see cref="NpcActionExecutor"/>, for the conversation the decision was made in, and
    /// returns what it did as an <see cref="ActionResult"/> whose payload is the executor's
    /// <see cref="ActionOutcome"/>.
    /// </summary>
    internal static class ExecutorActions
    {
        /// <summary>
        /// The turn's conversation generation, from the <see cref="DecideOptions.Correlation"/> the
        /// driver passes (the <see cref="NpcTurn"/>); -1, which the executor refuses, without one.
        /// </summary>
        public static long Generation(AgentContext ctx) => ctx?.Correlation is NpcTurn turn ? turn.Generation : -1;

        public static ActionResult Execute(NpcActionExecutor executor, string npcId, AgentContext ctx, AgentDecision decision)
        {
            var outcome = executor.Execute(npcId, decision.ActionId, decision.TargetId, Generation(ctx));
            // The outcome's own text names the quest or route rejection when there is one, not only the trade one.
            return outcome.Executed ? ActionResult.Done(outcome) : ActionResult.Failed(outcome.ToString(), outcome);
        }
    }

    /// <summary>
    /// The handler every Living Vale action registers: it carries a decision out through
    /// <see cref="ExecutorActions.Execute"/>, and its per-target check (<see cref="ITargetAvailability"/>,
    /// #235) allows a target only while the executor offers it for this action now. The framework
    /// asks that check for every target in the action's category, leaves the action out of the
    /// schema when none is allowed, rewrites a rejected pair to <c>none</c>
    /// (<c>TargetAvailabilityGuard</c>), and asks again at <see cref="Agent.Execute(DecisionResult)"/>,
    /// with the reason recorded in telemetry.
    /// </summary>
    internal sealed class ExecutorHandler : IActionHandlerWithResult, ITargetAvailability
    {
        private readonly NpcActionExecutor executor;
        private readonly string npcId;
        private readonly string actionId;

        public ExecutorHandler(NpcActionExecutor executor, string npcId, string actionId)
        {
            this.executor = executor;
            this.npcId = npcId;
            this.actionId = actionId;
        }

        /// <summary>Always: whether there is anything to do it to is the per-target check's, which masks the action when nothing is allowed.</summary>
        public bool IsAvailable(AgentContext ctx) => true;

        public Availability IsAvailableFor(AgentContext ctx, string targetId)
        {
            foreach (var option in executor.AvailableFor(npcId))
            {
                if (option.ActionId != actionId)
                {
                    continue;
                }

                foreach (var target in option.Targets)
                {
                    if (target.Id == targetId)
                    {
                        return Availability.Yes;
                    }
                }
            }

            return Availability.No("the game does not offer it now");
        }

        public void Execute(AgentContext ctx, AgentDecision decision) => ExecuteWithResult(ctx, decision);

        public ActionResult ExecuteWithResult(AgentContext ctx, AgentDecision decision) =>
            ExecutorActions.Execute(executor, npcId, ctx, decision);
    }
}

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
}

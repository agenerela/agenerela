namespace Agenerela
{
    /// <summary>
    /// A handler that says what carrying out a decision did (DR-018): done, or failed with a
    /// reason, and anything it made. Optional, beside <see cref="IActionHandler"/>, so every
    /// handler written before it still works and counts as <see cref="ExecutionStatus.Done"/>
    /// whenever it returns.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="Agent.Execute(DecisionResult)"/> calls <see cref="ExecuteWithResult"/> instead of
    /// <see cref="IActionHandler.Execute"/>, once, and never both. The inherited <c>Execute</c> is
    /// still required, for any other code that calls the handler; make it a one-line forward:
    /// </para>
    /// <code>
    /// public void Execute(AgentContext ctx, AgentDecision decision) =&gt; ExecuteWithResult(ctx, decision);
    /// </code>
    /// <para>
    /// The same result comes from a <see cref="DelegateActionHandler.WithResult"/> function or an
    /// <c>[AgentAction]</c> method that returns <see cref="ActionResult"/>, with no class at all.
    /// </para>
    /// </remarks>
    public interface IActionHandlerWithResult : IActionHandler
    {
        /// <summary>
        /// Ordinary game code, as <see cref="IActionHandler.Execute"/> is, that also reports what it
        /// did. Only reached after every guard and the re-check have passed. An exception still
        /// propagates out of <see cref="Agent.Execute(DecisionResult)"/>; <see cref="ActionResult.Failed"/>
        /// is for a refusal the game expects.
        /// </summary>
        ActionResult ExecuteWithResult(AgentContext ctx, AgentDecision decision);
    }
}

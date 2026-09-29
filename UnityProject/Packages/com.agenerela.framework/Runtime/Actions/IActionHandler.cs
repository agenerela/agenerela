namespace Agenerela
{
    /// <summary>Runs the game code behind a registered action.</summary>
    public interface IActionHandler
    {
        /// <summary>Returns whether this action is legal for the current decision context.</summary>
        bool IsAvailable(AgentContext ctx);

        /// <summary>Runs ordinary game code after every guard has passed.</summary>
        void Execute(AgentContext ctx, AgentDecision decision);
    }
}

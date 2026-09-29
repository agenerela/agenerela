namespace Agenerela
{
    /// <summary>Runs the game code behind a registered action.</summary>
    public interface IActionHandler
    {
        /// <summary>Is this action legal right now?</summary>
        bool IsAvailable(AgentContext ctx);

        /// <summary>Ordinary game code. Only reached after every guard has passed.</summary>
        void Execute(AgentContext ctx, AgentDecision decision);
    }
}

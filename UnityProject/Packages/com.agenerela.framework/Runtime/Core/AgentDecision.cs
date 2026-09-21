namespace Agenerela
{
    /// <summary>What the model chose for one decision.</summary>
    public sealed class AgentDecision
    {
        /// <summary>Registered action id such as "move_to".</summary>
        public string ActionId { get; }
        /// <summary>registered target id or "no_target", never can be null.</summary>
        public string TargetId { get; }
        /// <summary>What agent says or announces, could be empty.</summary>
        public string Statement { get; }

        public AgentDecision(string actionId, string targetId, string statement)
        {
            ActionId = actionId;
            TargetId = targetId;
            Statement = statement;
        }
    }
}

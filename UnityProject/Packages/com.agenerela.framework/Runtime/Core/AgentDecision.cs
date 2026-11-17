using System;

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

        /// <summary>Creates a decision from an action id, a target id, and a statement. 
        /// ActionId and TargetId throw ArgumentException if they are null or whitespace. 
        /// A null statement becomes empty. Unregistered ids still construct.</summary>
        public AgentDecision(string actionId, string targetId, string statement)
        {
            if (string.IsNullOrWhiteSpace(actionId))
            {
                throw new ArgumentException("ActionId cannot be null or whitespace", nameof(actionId));
            }

            if (string.IsNullOrWhiteSpace(targetId))
            {
                throw new ArgumentException("TargetId cannot be null or whitespace", nameof(targetId));
            }

            if (statement == null)
            {
                statement = "";
            }

            ActionId = actionId;
            TargetId = targetId;
            Statement = statement;
        }
    }
}

using System;

namespace Agenerela
{
    /// <summary>
    /// Marks the method that says whether an <see cref="AgentActionAttribute"/> action is legal right
    /// now: state masking for the code front door. It becomes the action's
    /// <see cref="IActionHandler.IsAvailable"/>, so while it returns false the action is left out of
    /// what the model can pick, rather than forbidden in prose (build plan §2.2).
    /// </summary>
    /// <remarks>
    /// The method returns bool and takes nothing or one <see cref="AgentContext"/>. An action with no
    /// check is always available. One method may check several actions, with one attribute each.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public sealed class AvailableAttribute : Attribute
    {
        /// <param name="actionId">The id of an [AgentAction] on the same object.</param>
        public AvailableAttribute(string actionId)
        {
            ActionId = actionId;
        }

        /// <summary>The id of the action this method checks.</summary>
        public string ActionId { get; }
    }
}

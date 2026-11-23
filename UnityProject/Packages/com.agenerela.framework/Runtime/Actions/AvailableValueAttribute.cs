using System;

namespace Agenerela
{
    /// <summary>
    /// Marks the method that says whether one value of an <see cref="AgentActionAttribute"/> action's
    /// parameter can be chosen right now: <see cref="IValueAvailability"/> for the code front door
    /// (DR-023, first step).
    /// </summary>
    /// <remarks>
    /// The method returns bool or <see cref="Availability"/> (to give a reason), and takes the value as
    /// a string, optionally after an <see cref="AgentContext"/>: <c>(string)</c> or
    /// <c>(AgentContext, string)</c>. One method may check several parameters, with one attribute each,
    /// and a parameter has at most one.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public sealed class AvailableValueAttribute : Attribute
    {
        /// <param name="actionId">The id of an [AgentAction] on the same object.</param>
        /// <param name="parameter">The name of one of that action's [AgentParameter]s.</param>
        public AvailableValueAttribute(string actionId, string parameter)
        {
            ActionId = actionId;
            Parameter = parameter;
        }

        /// <summary>The id of the action whose parameter this method checks.</summary>
        public string ActionId { get; }

        /// <summary>The name of the parameter this method checks.</summary>
        public string Parameter { get; }
    }
}

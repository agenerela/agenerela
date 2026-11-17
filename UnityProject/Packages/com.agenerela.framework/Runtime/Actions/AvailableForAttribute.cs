using System;

namespace Agenerela
{
    /// <summary>
    /// Marks the method that says whether an <see cref="AgentActionAttribute"/> action can be done to
    /// one target right now: <see cref="ITargetAvailability"/> for the code front door (#190 LV #19,
    /// #216 item 5). <see cref="AvailableAttribute"/> answers for the action as a whole; this answers
    /// per target.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The method returns bool or <see cref="Availability"/> (to give a reason), and takes the target's
    /// id as a string, optionally after an <see cref="AgentContext"/>: <c>(string)</c> or
    /// <c>(AgentContext, string)</c>. One method may check several actions, with one attribute each,
    /// and an action has at most one.
    /// </para>
    /// <para>
    /// What it changes is in <see cref="ITargetAvailability"/>'s remarks: an action that needs a target
    /// and has none this allows is left out of the schema, the paired schema drops each pair it
    /// rejects, and a guard and <see cref="Agent.Execute(DecisionResult)"/> hold a decision to it.
    /// </para>
    /// </remarks>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public sealed class AvailableForAttribute : Attribute
    {
        /// <param name="actionId">The id of an [AgentAction] on the same object.</param>
        public AvailableForAttribute(string actionId)
        {
            ActionId = actionId;
        }

        /// <summary>The id of the action this method checks.</summary>
        public string ActionId { get; }
    }
}

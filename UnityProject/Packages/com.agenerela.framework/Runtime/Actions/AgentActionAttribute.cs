using System;

namespace Agenerela
{
    /// <summary>
    /// Makes a method an action the agent can choose, and the code that runs it: the code front
    /// door (DR-011). <see cref="ActionRegistry.RegisterMethods"/> turns each marked method into an
    /// <see cref="ActionDefinition"/> and a handler that calls the method.
    /// </summary>
    /// <remarks>
    /// The method returns void. Each parameter is filled by its type: an <see cref="AgentContext"/>
    /// receives the context, an <see cref="AgentDecision"/> the decision, and at most one parameter
    /// of any other type the target. That is the object registered under the decision's target id,
    /// passed as registered, or null for <see cref="TargetRegistry.NoTarget"/>, which a parameter
    /// that cannot hold null, such as an int, refuses. Pair it with <see cref="ExampleAttribute"/>
    /// for the few-shot example and <see cref="AvailableAttribute"/> for state masking.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class AgentActionAttribute : Attribute
    {
        /// <param name="id">snake_case. Becomes the value the model picks from.</param>
        /// <param name="description">One SHORT clause telling the model WHEN to use this action.
        /// Keep it comparable in length to your other actions. A long emphatic description
        /// measurably biases small models toward picking it.</param>
        public AgentActionAttribute(string id, string description)
        {
            Id = id;
            Description = description;
        }

        /// <summary>snake_case. Becomes the value the model picks from.</summary>
        public string Id { get; }

        /// <summary>One short clause telling the model when to use this action.</summary>
        public string Description { get; }

        /// <summary>Does this action need something to act on?</summary>
        public bool RequiresTarget { get; set; }
    }
}

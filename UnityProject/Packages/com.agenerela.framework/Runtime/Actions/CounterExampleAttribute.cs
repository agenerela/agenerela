using System;

namespace Agenerela
{
    /// <summary>
    /// The counter-example for an <see cref="AgentActionAttribute"/> method: the code front door's
    /// <see cref="ActionDefinition.CounterExampleStimulus"/>, a stimulus that touches the action's
    /// subject but asks for nothing, shown answered with <c>none</c> (#190). It belongs on the
    /// action's own method, beside its <see cref="ExampleAttribute"/>.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class CounterExampleAttribute : Attribute
    {
        /// <param name="stimulus">A stimulus that mentions this action's subject but asks for
        /// nothing. Use {0} for the target, as in the example, and never reuse an evaluation
        /// prompt.</param>
        public CounterExampleAttribute(string stimulus)
        {
            Stimulus = stimulus;
        }

        /// <summary>A stimulus answered with <c>none</c>, with {0} for the target.</summary>
        public string Stimulus { get; }
    }
}

using System;

namespace Agenerela
{
    /// <summary>
    /// The few-shot example for an <see cref="AgentActionAttribute"/> method: the code front door's
    /// <see cref="ActionDefinition.ExampleStimulus"/> and <see cref="ActionDefinition.PreferredExampleTargets"/>.
    /// It belongs on the action's own method.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class ExampleAttribute : Attribute
    {
        /// <param name="stimulus">A stimulus that should lead to this action. A player's input, an
        /// event, or behavior that triggers this action. Use {0} for the target and never reuse an
        /// evaluation prompt.</param>
        /// <param name="preferredTargets">Target ids this verb sensibly applies to. Prevents
        /// nonsense examples like "Pick up the Blacksmith".</param>
        public ExampleAttribute(string stimulus, params string[] preferredTargets)
        {
            Stimulus = stimulus;
            PreferredTargets = preferredTargets ?? Array.Empty<string>();
        }

        /// <summary>A stimulus that should lead to this action, with {0} for the target.</summary>
        public string Stimulus { get; }

        /// <summary>Target ids this verb sensibly applies to. Never null.</summary>
        public string[] PreferredTargets { get; }
    }
}

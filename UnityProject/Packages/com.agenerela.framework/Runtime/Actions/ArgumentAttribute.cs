using System;

namespace Agenerela
{
    /// <summary>
    /// On a parameter of an <see cref="AgentActionAttribute"/> method: it receives the value the
    /// decision chose for the action's <see cref="AgentParameterAttribute"/> of this name, as a
    /// <c>string</c>, or as an <c>int</c> when every value of that parameter is a whole number
    /// (DR-023, first step). A method parameter without it that is not an <see cref="AgentContext"/>
    /// or an <see cref="AgentDecision"/> receives the target, as before.
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter)]
    public sealed class ArgumentAttribute : Attribute
    {
        /// <param name="name">The name of one of the action's [AgentParameter]s.</param>
        public ArgumentAttribute(string name)
        {
            Name = name;
        }

        /// <summary>The name of the parameter whose value this receives.</summary>
        public string Name { get; }
    }
}

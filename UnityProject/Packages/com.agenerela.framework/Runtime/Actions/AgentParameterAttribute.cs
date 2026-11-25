using System;

namespace Agenerela
{
    /// <summary>
    /// Declares one of an <see cref="AgentActionAttribute"/> action's parameters, a closed choice the
    /// model makes beside the target (<see cref="ActionParameter"/>, DR-023, first step). Goes on the
    /// action's own method, once per parameter, with <see cref="Order"/> when there are several.
    /// </summary>
    /// <remarks>
    /// The method receives a value with <see cref="ArgumentAttribute"/> on one of its parameters, as a
    /// <c>string</c>, or as an <c>int</c> when every value is a whole number; or it reads
    /// <see cref="AgentDecision.Arguments"/> from the decision it takes.
    /// </remarks>
    /// <example>
    /// <code>
    /// [AgentAction("blight", "A country falls sick.", RequiresTarget = true)]
    /// [AgentParameter("strength", "how hard it strikes", "minor", "major")]
    /// void Blight(Country country, [Argument("strength")] string strength) { ... }
    /// </code>
    /// </example>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public sealed class AgentParameterAttribute : Attribute
    {
        /// <param name="name">snake_case, the key the model writes the value under.</param>
        /// <param name="description">What the value means, shown beside the values; may be null.</param>
        /// <param name="values">The values the model may pick, in order. At least one.</param>
        public AgentParameterAttribute(string name, string description, params string[] values)
        {
            Name = name;
            Description = description;
            Values = values;
        }

        /// <summary>The parameter's name.</summary>
        public string Name { get; }

        /// <summary>What the value means, or null.</summary>
        public string Description { get; }

        /// <summary>The values the model may pick.</summary>
        public string[] Values { get; }

        /// <summary>The value used when the reply leaves this out or names one not offered (<see cref="ActionParameter.Default"/>).</summary>
        public string Default { get; set; }

        /// <summary>
        /// Above 0, a free text of at most this many characters the model writes itself, given with no values
        /// (DR-030): <c>[AgentParameter("world_name", "the world's name", MaxLength = 24)]</c>. 0, the default, for a
        /// closed choice.
        /// </summary>
        public int MaxLength { get; set; }

        /// <summary>The value this action's example shows (<see cref="ActionParameter.ExampleValue"/>).</summary>
        public string ExampleValue { get; set; }

        /// <summary>
        /// Where this parameter comes among the action's, lowest first: the order the reply gives them
        /// in. Reflection promises no order for several attributes on one method, so an action with two
        /// parameters gives each a different <c>Order</c>; ties fall back to the name.
        /// </summary>
        public int Order { get; set; }
    }
}

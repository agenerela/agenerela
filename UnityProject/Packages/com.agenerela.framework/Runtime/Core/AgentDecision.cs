using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Agenerela
{
    /// <summary>What the model chose for one decision.</summary>
    public sealed class AgentDecision
    {
        private static readonly IReadOnlyDictionary<string, string> NoArguments =
            new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(StringComparer.Ordinal));

        /// <summary>Registered action id such as "move_to".</summary>
        public string ActionId { get; }
        /// <summary>registered target id or "no_target", never can be null.</summary>
        public string TargetId { get; }
        /// <summary>What agent says or announces, could be empty.</summary>
        public string Statement { get; }

        /// <summary>
        /// The value chosen for each of the action's declared parameters, by name
        /// (<see cref="ActionDefinition.Parameters"/>, DR-023). Empty, never null, for an action that
        /// declares none.
        /// </summary>
        /// <remarks>
        /// Enumerated in the order the parameters were declared. <see cref="Agent.Execute(DecisionResult)"/>
        /// checks each value against the action's declaration before any handler runs (hard rule 5).
        /// </remarks>
        public IReadOnlyDictionary<string, string> Arguments { get; }

        /// <summary>Creates a decision from an action id, a target id, and a statement.
        /// ActionId and TargetId throw ArgumentException if they are null or whitespace.
        /// A null statement becomes empty. Unregistered ids still construct.</summary>
        public AgentDecision(string actionId, string targetId, string statement)
            : this(actionId, targetId, statement, null)
        {
        }

        /// <summary>
        /// Creates a decision as the three-argument constructor does, with these
        /// <paramref name="arguments"/>, copied in their order. Null is none. A null or blank name, or
        /// a null value, throws <see cref="ArgumentException"/>.
        /// </summary>
        public AgentDecision(string actionId, string targetId, string statement, IEnumerable<KeyValuePair<string, string>> arguments)
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
            Arguments = arguments == null ? NoArguments : Copy(arguments);
        }

        /// <summary>The value chosen for the parameter <paramref name="name"/>, if the decision has one.</summary>
        public bool TryGetArgument(string name, out string value)
        {
            value = null;
            return name != null && Arguments.TryGetValue(name, out value);
        }

        /// <summary>
        /// The value chosen for <paramref name="name"/> read as a whole number in the invariant
        /// culture, if the decision has one and it reads as one.
        /// </summary>
        public bool TryGetNumber(string name, out int value)
        {
            value = 0;
            return TryGetArgument(name, out string text) && ActionParameter.TryReadNumber(text, out value);
        }

        // The same move with another statement, its arguments kept.
        internal AgentDecision WithStatement(string statement) => new AgentDecision(ActionId, TargetId, statement, Arguments);

        // The same move with another target, its arguments kept.
        internal AgentDecision WithTarget(string targetId) => new AgentDecision(ActionId, targetId, Statement, Arguments);

        private static IReadOnlyDictionary<string, string> Copy(IEnumerable<KeyValuePair<string, string>> arguments)
        {
            var copy = new OrderedArguments();
            foreach (var argument in arguments)
            {
                if (string.IsNullOrWhiteSpace(argument.Key))
                {
                    throw new ArgumentException("An argument has no name.", nameof(arguments));
                }

                if (argument.Value == null)
                {
                    throw new ArgumentException($"The argument '{argument.Key}' has no value.", nameof(arguments));
                }

                copy.Set(argument.Key, argument.Value);
            }

            return copy.Count == 0 ? NoArguments : copy;
        }

        // A read-only map that enumerates in the order its names were first set, so the log, memory
        // and a handler see the arguments in the order the parameters were declared.
        private sealed class OrderedArguments : IReadOnlyDictionary<string, string>
        {
            private readonly List<string> order = new List<string>();
            private readonly Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.Ordinal);

            public void Set(string name, string value)
            {
                if (!values.ContainsKey(name)) order.Add(name);
                values[name] = value;
            }

            public string this[string key] => values[key];
            public IEnumerable<string> Keys => order;
            public IEnumerable<string> Values
            {
                get
                {
                    foreach (string name in order) yield return values[name];
                }
            }

            public int Count => order.Count;
            public bool ContainsKey(string key) => key != null && values.ContainsKey(key);
            public bool TryGetValue(string key, out string value)
            {
                value = null;
                return key != null && values.TryGetValue(key, out value);
            }

            public IEnumerator<KeyValuePair<string, string>> GetEnumerator()
            {
                foreach (string name in order) yield return new KeyValuePair<string, string>(name, values[name]);
            }

            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
        }
    }
}

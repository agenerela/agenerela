using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Agenerela
{
    /// <summary>
    /// One branch of a paired schema (DR-020): these actions, each of which takes exactly these
    /// targets. <see cref="DecisionSchema.Pairs"/> holds one per distinct target list, so the
    /// grammar a provider compiles from it can only pair an action with a target it takes.
    /// </summary>
    /// <remarks>
    /// Provider-neutral, like <see cref="DecisionSchema"/>: the lists are ids, in schema order, and
    /// how a branch is written out is each serializer's business. <see cref="JsonSchemaSerializer"/>
    /// writes the branches as a root <c>anyOf</c>.
    /// </remarks>
    public sealed class ActionTargets
    {
        /// <summary>A branch with exactly these actions and targets, in this order. Both lists are copied.</summary>
        /// <exception cref="ArgumentNullException">A list is null.</exception>
        public ActionTargets(IEnumerable<string> actions, IEnumerable<string> targets)
            : this(actions, targets, null)
        {
        }

        /// <summary>
        /// A branch as the two-list constructor makes it, whose actions also ask for these
        /// <paramref name="parameters"/>, in this order (DR-023). Null is none. The list is copied.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="actions"/> or <paramref name="targets"/> is null.</exception>
        /// <exception cref="ArgumentException">One of <paramref name="parameters"/> is null.</exception>
        public ActionTargets(IEnumerable<string> actions, IEnumerable<string> targets, IEnumerable<SchemaField> parameters)
        {
            if (actions == null)
            {
                throw new ArgumentNullException(nameof(actions));
            }

            if (targets == null)
            {
                throw new ArgumentNullException(nameof(targets));
            }

            Actions = new ReadOnlyCollection<string>(new List<string>(actions));
            Targets = new ReadOnlyCollection<string>(new List<string>(targets));

            var fields = parameters == null ? new List<SchemaField>() : new List<SchemaField>(parameters);
            for (int i = 0; i < fields.Count; i++)
            {
                if (fields[i] == null)
                {
                    throw new ArgumentException($"Parameter {i} is null.", nameof(parameters));
                }
            }

            Parameters = new ReadOnlyCollection<SchemaField>(fields);
        }

        /// <summary>The action ids of this branch, in the action enum's order.</summary>
        public IReadOnlyList<string> Actions { get; }

        /// <summary>
        /// The target ids each of them takes, in the target enum's order: <see cref="TargetRegistry.NoTarget"/>
        /// first when they may take none.
        /// </summary>
        public IReadOnlyList<string> Targets { get; }

        /// <summary>
        /// The parameters each of this branch's actions asks for, in the order the reply gives them:
        /// after <c>target</c> and before <c>statement</c> (DR-023). Each is a required field whose
        /// allowed values are the ones offered for this decision. Empty, never null, for actions that
        /// declare none.
        /// </summary>
        public IReadOnlyList<SchemaField> Parameters { get; }
    }
}

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
        }

        /// <summary>The action ids of this branch, in the action enum's order.</summary>
        public IReadOnlyList<string> Actions { get; }

        /// <summary>
        /// The target ids each of them takes, in the target enum's order: <see cref="TargetRegistry.NoTarget"/>
        /// first when they may take none.
        /// </summary>
        public IReadOnlyList<string> Targets { get; }
    }
}

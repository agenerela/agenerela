using System;
using System.Collections.Generic;

namespace Agenerela
{
    /// <summary>
    /// A target set the developer supplies by hand. It is what an agent with nothing to query
    /// from uses — a country in GreyBoxStrategy has no Transform — and it needs no scene.
    /// </summary>
    public sealed class ExplicitTargetSource : ITargetSource
    {
        // A registry of its own, so an id is validated and a duplicate rejected when it is
        // added rather than at the first decision, and so the order added is the order offered.
        private readonly TargetRegistry targets = new TargetRegistry();

        /// <summary>The ids this source offers, in the order they were added.</summary>
        public IReadOnlyList<string> Ids => targets.Ids;

        public int Count => targets.Count;

        /// <summary>
        /// Adds a target. Throws for a malformed, reserved or duplicate id and for a null
        /// target, like <see cref="TargetRegistry.Register"/>. Returns this source, so a set
        /// can be written as one chain.
        /// </summary>
        public ExplicitTargetSource Add(string id, object target)
        {
            targets.Register(id, target);
            return this;
        }

        /// <summary>Removes a target. Returns false for any id this source does not hold.</summary>
        public bool Remove(string id)
        {
            return targets.Unregister(id);
        }

        public void Collect(AgentContext ctx, TargetRegistry into)
        {
            if (into == null)
            {
                throw new ArgumentNullException(nameof(into));
            }

            foreach (var id in targets.Ids)
            {
                if (into.Contains(id))
                {
                    throw new InvalidOperationException(
                        $"Target id '{id}' is supplied by an ExplicitTargetSource but an earlier " +
                        "source already added it. Every target in one agent's resolved set needs " +
                        "its own id.");
                }

                targets.TryGet(id, out var target);
                into.Register(id, target);
            }
        }
    }
}

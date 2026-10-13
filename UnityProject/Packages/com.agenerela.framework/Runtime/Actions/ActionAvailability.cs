using System;
using System.Collections.Generic;

namespace Agenerela
{
    /// <summary>Filters the registered action vocabulary down to the actions legal right now.</summary>
    public static class ActionAvailability
    {
        /// <summary>
        /// The actions legal for this agent right now. Recomputed every decision; never cached,
        /// because an agent's state and visible targets can change between two decisions.
        /// </summary>
        public static IReadOnlyList<ActionDefinition> For(AgentContext ctx)
        {
            if (ctx == null)
            {
                throw new ArgumentNullException(nameof(ctx));
            }

            // Start with an empty result for this one decision. This local list is intentionally
            // new every call, so yesterday's action state cannot leak into today's request.
            var available = new List<ActionDefinition>();

            // Definitions is the model-facing action order from ActionRegistry. Iterating this
            // list preserves that stable order while removing options that do not make sense now.
            foreach (var definition in ctx.Actions.Definitions)
            {
                // Target-requiring actions are impossible when the agent has nothing it may
                // reference. Leaving them out of the schema prevents the model from choosing
                // nonsense such as "walk_to" when TargetRegistry is empty.
                if (definition.RequiresTarget && ctx.Targets.Count == 0)
                {
                    continue;
                }

                // The handler owns game-specific legality, such as "do not offer follow_player
                // while the agent is already following." Structural filtering beats prompt prose:
                // if this says no, the option is absent from the schema.
                if (!ctx.Actions.HandlerFor(definition).IsAvailable(ctx))
                {
                    continue;
                }

                available.Add(definition);
            }

            return available;
        }
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Agenerela
{
    /// <summary>
    /// The default target source for a scene agent (DR-014): every <see cref="Targetable"/>
    /// within <see cref="Radius"/> of <see cref="Origin"/>, filtered by layer and category and
    /// optionally by line of sight, nearest first and capped at <see cref="Cap"/>.
    /// Each one is registered as its <see cref="Transform"/>, so an <c>[AgentAction]</c> method
    /// that takes a <c>Transform</c> target receives it with no conversion (#50).
    /// </summary>
    [Serializable]
    public sealed class ProximityTargetSource : ITargetSource, IReportsDroppedTargets
    {
        [Tooltip("Where distance is measured from, normally the agent itself. Targetables on " +
                 "this object or its children are never offered: an agent does not name itself.")]
        public Transform Origin;

        [Tooltip("How far away a Targetable can be and still be nameable.")]
        [Min(0f)] public float Radius = 18f;

        [Tooltip("Only Targetables on these layers are offered.")]
        public LayerMask Layers = ~0;

        [Tooltip("Only Targetables with one of these categories are offered. Leave empty for any category.")]
        public string[] Categories;

        [Tooltip("Only offer Targetables with no collider between the origin and them. " +
                 "Colliders on the origin's own hierarchy never block. A Targetable inside another " +
                 "object's collider, even its parent's, counts as hidden: put it on the collider's object.")]
        public bool RequireLineOfSight;

        [Tooltip("Most targets offered at once. Every target is one more enum value in the " +
                 "prompt, so the far ones are dropped first.")]
        [Min(1)] public int Cap = 8;

        [NonSerialized] private List<string> dropped;

        /// <summary>
        /// Ids the last <see cref="Collect"/> found but left out because of the cap, nearest
        /// first. An <c>Agent</c> copies them into <see cref="DecisionTelemetry.TargetsDropped"/>
        /// on every decision (#81), so a thin-looking target list can be diagnosed. Targetables
        /// outside the radius, layers, categories or line of sight were never found, so they are
        /// not here.
        /// </summary>
        public IReadOnlyList<string> Dropped => (IReadOnlyList<string>)dropped ?? Array.Empty<string>();

        public void Collect(AgentContext ctx, TargetRegistry into)
        {
            if (into == null)
            {
                throw new ArgumentNullException(nameof(into));
            }

            TargetableQuery.Validate(nameof(ProximityTargetSource), Origin, Radius, Cap);

            if (dropped == null)
            {
                dropped = new List<string>();
            }

            dropped.Clear();

            // Nearest first, so the cap cuts the far ones rather than arbitrary ones, and in the same
            // order every time for the same scene, so the enum is too.
            var inRange = TargetableQuery.Nearest(Origin, Radius, Layers, Categories, RequireLineOfSight);

            // Every candidate is checked before the cap is applied, so whether a scene throws
            // does not depend on where the agent happens to stand.
            var kept = new Dictionary<string, Targetable>(StringComparer.Ordinal);

            foreach (var candidate in inRange)
            {
                var targetable = candidate.Targetable;

                if (!Targetable.ValidateId(targetable.Id, out string problem))
                {
                    throw new InvalidOperationException(
                        $"Targetable on '{targetable.name}': {problem}. Give it an explicit lower_snake_case id.");
                }

                if (kept.TryGetValue(targetable.Id, out var other))
                {
                    throw new InvalidOperationException(
                        $"Targetables on '{other.name}' and '{targetable.name}' both have the id " +
                        $"'{targetable.Id}'. Every target in one agent's resolved set needs its own id.");
                }

                if (into.Contains(targetable.Id))
                {
                    throw new InvalidOperationException(
                        $"Targetable on '{targetable.name}' has the id '{targetable.Id}', which an " +
                        "earlier source already added. Every target in one agent's resolved set needs its own id.");
                }

                kept.Add(targetable.Id, targetable);
            }

            for (int i = 0; i < inRange.Count; i++)
            {
                var targetable = inRange[i].Targetable;

                if (i < Cap)
                {
                    into.Register(targetable.Id, targetable.transform);
                }
                else
                {
                    dropped.Add(targetable.Id);
                }
            }
        }
    }
}

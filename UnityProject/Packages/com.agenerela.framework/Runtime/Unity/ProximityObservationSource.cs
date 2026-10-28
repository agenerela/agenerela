using System;
using System.Collections.Generic;
using UnityEngine;

namespace Agenerela
{
    /// <summary>
    /// The default observation source for a scene agent (DR-012, §2.8): the
    /// <see cref="Targetable.Description"/> of every <see cref="Targetable"/> within
    /// <see cref="Radius"/> of <see cref="Origin"/>, filtered by layer and category and optionally
    /// by line of sight, nearest first and capped at <see cref="Cap"/>. So "The watchtower door
    /// stands open." reaches a guard while it stands by the tower, and stops when it walks away.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The same query, with the same options, as <see cref="ProximityTargetSource"/>: given the
    /// same settings, the two describe and name the same objects, and a line the agent reads
    /// comes from the object whose id it may answer with (DR-014).
    /// </para>
    /// <para>
    /// Each description is sent verbatim, with no id or distance added. A Targetable with no
    /// description says nothing and takes no place under the cap. Ids are not checked here: a
    /// line needs none, and <see cref="ProximityTargetSource"/> already rejects a bad one for any
    /// object an agent may name.
    /// </para>
    /// </remarks>
    [Serializable]
    public sealed class ProximityObservationSource : IObservationSource, IReportsDroppedObservations
    {
        [Tooltip("Where distance is measured from, normally the agent itself. Targetables on " +
                 "this object or its children are never described: an agent does not describe itself.")]
        public Transform Origin;

        [Tooltip("How far away a Targetable can be and still be noticed.")]
        [Min(0f)] public float Radius = 18f;

        [Tooltip("Only Targetables on these layers are described.")]
        public LayerMask Layers = ~0;

        [Tooltip("Only Targetables with one of these categories are described. Leave empty for any category.")]
        public string[] Categories;

        [Tooltip("Only describe Targetables with no collider between the origin and them. " +
                 "Colliders on the origin's own hierarchy never block. A Targetable inside another " +
                 "object's collider, even its parent's, counts as hidden: put it on the collider's object.")]
        public bool RequireLineOfSight;

        [Tooltip("Most lines returned at once. Observations are the last thing the model reads, " +
                 "so the far ones are dropped first. The agent's MaxObservations caps every source together.")]
        [Min(1)] public int Cap = 8;

        [NonSerialized] private List<string> dropped;

        /// <summary>
        /// Descriptions the last <see cref="Observe"/> found but left out because of the cap,
        /// nearest first. An <c>Agent</c> copies them into
        /// <see cref="DecisionTelemetry.ObservationsDropped"/> on every decision. Targetables
        /// outside the radius, layers, categories or line of sight were never found, so they are
        /// not here.
        /// </summary>
        public IReadOnlyList<string> Dropped => (IReadOnlyList<string>)dropped ?? Array.Empty<string>();

        /// <summary>
        /// The descriptions found now, nearest first, at most <see cref="Cap"/> of them. Built in
        /// full before it returns, so <see cref="Dropped"/> is ready as soon as it has.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// No <see cref="Origin"/>, a negative <see cref="Radius"/>, or a <see cref="Cap"/> below 1.
        /// </exception>
        public IEnumerable<string> Observe(AgentContext ctx)
        {
            TargetableQuery.Validate(nameof(ProximityObservationSource), Origin, Radius, Cap);

            if (dropped == null)
            {
                dropped = new List<string>();
            }

            dropped.Clear();

            var lines = new List<string>();

            foreach (var found in TargetableQuery.Nearest(Origin, Radius, Layers, Categories, RequireLineOfSight))
            {
                string description = found.Targetable.Description;

                if (string.IsNullOrWhiteSpace(description))
                {
                    continue;
                }

                if (lines.Count < Cap)
                {
                    lines.Add(description);
                }
                else
                {
                    dropped.Add(description);
                }
            }

            return lines;
        }
    }
}

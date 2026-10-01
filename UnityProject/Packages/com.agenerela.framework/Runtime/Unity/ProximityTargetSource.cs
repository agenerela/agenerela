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
    public sealed class ProximityTargetSource : ITargetSource
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
        /// first. Copy this into <see cref="DecisionTelemetry.TargetsDropped"/>, so a
        /// thin-looking target list can be diagnosed.
        /// </summary>
        public IReadOnlyList<string> Dropped => (IReadOnlyList<string>)dropped ?? Array.Empty<string>();

        public void Collect(AgentContext ctx, TargetRegistry into)
        {
            if (into == null)
            {
                throw new ArgumentNullException(nameof(into));
            }

            if (Origin == null)
            {
                throw new InvalidOperationException(
                    "ProximityTargetSource has no Origin, so there is nothing to measure distance from.");
            }

            if (Radius < 0f)
            {
                throw new InvalidOperationException($"ProximityTargetSource.Radius is {Radius}; it cannot be negative.");
            }

            if (Cap < 1)
            {
                throw new InvalidOperationException($"ProximityTargetSource.Cap is {Cap}; it must be at least 1.");
            }

            if (dropped == null)
            {
                dropped = new List<string>();
            }

            dropped.Clear();

            var inRange = FindInRange();

            // Nearest first, so the cap cuts the far ones rather than arbitrary ones. Ties break
            // on id, so the same scene always gives the same enum in the same order.
            inRange.Sort((a, b) =>
            {
                int byDistance = a.SqrDistance.CompareTo(b.SqrDistance);
                return byDistance != 0 ? byDistance : string.CompareOrdinal(a.Targetable.Id, b.Targetable.Id);
            });

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

        private List<Candidate> FindInRange()
        {
            var origin = Origin.position;
            float sqrRadius = Radius * Radius;
            var found = new List<Candidate>();

            // A scene search rather than an overlap query, so a Targetable needs no collider.
            // Decisions are seconds apart, so the cost is paid rarely.
            var all = UnityEngine.Object.FindObjectsByType<Targetable>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

            foreach (var targetable in all)
            {
                // enabled rather than isActiveAndEnabled, which reads false in edit mode for a
                // component without [ExecuteAlways]; inactive objects are already excluded.
                if (!targetable.enabled || !targetable.IsCurrentlyTargetable)
                {
                    continue;
                }

                var transform = targetable.transform;

                if (transform.IsChildOf(Origin))
                {
                    continue;
                }

                if ((Layers.value & (1 << targetable.gameObject.layer)) == 0)
                {
                    continue;
                }

                if (!MatchesCategory(targetable.Category))
                {
                    continue;
                }

                float sqrDistance = (transform.position - origin).sqrMagnitude;
                if (sqrDistance > sqrRadius)
                {
                    continue;
                }

                if (RequireLineOfSight && !CanSee(origin, transform))
                {
                    continue;
                }

                found.Add(new Candidate(targetable, sqrDistance));
            }

            return found;
        }

        private bool MatchesCategory(string category)
        {
            if (Categories == null || Categories.Length == 0)
            {
                return true;
            }

            foreach (var wanted in Categories)
            {
                if (string.Equals(wanted, category, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        // Visible when nothing is in the way, or the first thing in the way is the target itself.
        // Colliders on the agent's own hierarchy are skipped, so its body never blocks its view.
        // A hit is attributed by hit.collider rather than hit.transform, which is the attached
        // Rigidbody's transform and so would miss a Targetable under a Rigidbody parent.
        private bool CanSee(Vector3 origin, Transform target)
        {
            var toTarget = target.position - origin;
            float distance = toTarget.magnitude;

            if (distance <= 0f)
            {
                return true;
            }

            var hits = Physics.RaycastAll(origin, toTarget / distance, distance,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            foreach (var hit in hits)
            {
                var hitTransform = hit.collider.transform;

                if (hitTransform.IsChildOf(Origin))
                {
                    continue;
                }

                return hitTransform.IsChildOf(target);
            }

            return true;
        }

        private readonly struct Candidate
        {
            public readonly Targetable Targetable;
            public readonly float SqrDistance;

            public Candidate(Targetable targetable, float sqrDistance)
            {
                Targetable = targetable;
                SqrDistance = sqrDistance;
            }
        }
    }
}

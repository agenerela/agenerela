using System;
using System.Collections.Generic;
using UnityEngine;

namespace Agenerela
{
    /// <summary>
    /// The scene query the proximity sources share: every <see cref="Targetable"/> within a radius
    /// of an origin, filtered by layer, category and optionally line of sight, nearest first.
    /// <see cref="ProximityTargetSource"/> names what it finds and
    /// <see cref="ProximityObservationSource"/> describes it, so one query keeps the two from
    /// drifting apart: given the same options, they find the same objects.
    /// </summary>
    internal static class TargetableQuery
    {
        /// <summary>
        /// Throws if the options cannot be queried with, in the words of <paramref name="source"/>,
        /// the type the developer configured.
        /// </summary>
        public static void Validate(string source, Transform origin, float radius, int cap)
        {
            if (origin == null)
            {
                throw new InvalidOperationException(
                    $"{source} has no Origin, so there is nothing to measure distance from.");
            }

            if (radius < 0f)
            {
                throw new InvalidOperationException($"{source}.Radius is {radius}; it cannot be negative.");
            }

            if (cap < 1)
            {
                throw new InvalidOperationException($"{source}.Cap is {cap}; it must be at least 1.");
            }
        }

        /// <summary>
        /// The Targetables that pass every filter, nearest first. Ties break on id, so the same
        /// scene always gives the same order. Targetables on <paramref name="origin"/> or its
        /// children are never found: an agent does not name or describe itself.
        /// </summary>
        public static List<Found> Nearest(
            Transform origin, float radius, LayerMask layers, string[] categories, bool requireLineOfSight)
        {
            var found = FindInRange(origin, radius, layers, categories, requireLineOfSight);

            found.Sort((a, b) =>
            {
                int byDistance = a.SqrDistance.CompareTo(b.SqrDistance);
                return byDistance != 0 ? byDistance : string.CompareOrdinal(a.Targetable.Id, b.Targetable.Id);
            });

            return found;
        }

        private static List<Found> FindInRange(
            Transform originTransform, float radius, LayerMask layers, string[] categories, bool requireLineOfSight)
        {
            var origin = originTransform.position;
            float sqrRadius = radius * radius;
            var found = new List<Found>();

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

                if (transform.IsChildOf(originTransform))
                {
                    continue;
                }

                if ((layers.value & (1 << targetable.gameObject.layer)) == 0)
                {
                    continue;
                }

                if (!MatchesCategory(categories, targetable.Category))
                {
                    continue;
                }

                float sqrDistance = (transform.position - origin).sqrMagnitude;
                if (sqrDistance > sqrRadius)
                {
                    continue;
                }

                if (requireLineOfSight && !CanSee(originTransform, origin, transform))
                {
                    continue;
                }

                found.Add(new Found(targetable, sqrDistance));
            }

            return found;
        }

        private static bool MatchesCategory(string[] categories, string category)
        {
            if (categories == null || categories.Length == 0)
            {
                return true;
            }

            foreach (var wanted in categories)
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
        private static bool CanSee(Transform self, Vector3 origin, Transform target)
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

                if (hitTransform.IsChildOf(self))
                {
                    continue;
                }

                return hitTransform.IsChildOf(target);
            }

            return true;
        }

        /// <summary>One Targetable the query found, and how far it is, squared.</summary>
        public readonly struct Found
        {
            public readonly Targetable Targetable;
            public readonly float SqrDistance;

            public Found(Targetable targetable, float sqrDistance)
            {
                Targetable = targetable;
                SqrDistance = sqrDistance;
            }
        }
    }
}

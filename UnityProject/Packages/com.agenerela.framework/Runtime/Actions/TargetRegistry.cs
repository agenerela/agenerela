using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Agenerela
{
    /// <summary>
    /// Stores the objects an agent is currently allowed to reference.
    /// </summary>
    public sealed class TargetRegistry
    {
        /// <summary>The schema's "no target" value (#8). Reserved: it can never be registered.</summary>
        public const string NoTarget = "no_target";

        private readonly Dictionary<string, object> targets = new Dictionary<string, object>();
        private readonly List<string> ids = new List<string>();

        public TargetRegistry()
        {
            Ids = new ReadOnlyCollection<string>(ids);
        }

        /// <summary>
        /// Registered ids in insertion order. The schema enum is built from this, so the order
        /// must be stable across calls: the same registry must always produce the same enum.
        /// This is a live read-only view that reflects later Register and Unregister calls;
        /// copy it before modifying the registry in a loop.
        /// </summary>
        public IReadOnlyList<string> Ids { get; }

        public int Count => ids.Count;

        /// <summary>
        /// Registers a target. Throws for a malformed or reserved id, a duplicate id, or a
        /// null target: registering is developer code, so mistakes should be loud.
        /// </summary>
        public void Register(string id, object target)
        {
            var idError = GetIdError(id);
            if (idError != null)
            {
                throw new ArgumentException(idError, nameof(id));
            }

            if (target == null)
            {
                throw new ArgumentNullException(nameof(target), "A target cannot be null.");
            }

            if (targets.ContainsKey(id))
            {
                throw new ArgumentException(
                    $"A target with the id '{id}' is already registered.",
                    nameof(id));
            }

            targets.Add(id, target);
            ids.Add(id);
        }

        /// <summary>
        /// Removes a target. Returns false for any id that is not registered, including
        /// malformed and reserved ids; never throws.
        /// </summary>
        public bool Unregister(string id)
        {
            if (!CanBeRegistered(id) || !targets.Remove(id))
            {
                return false;
            }

            ids.Remove(id);
            return true;
        }

        /// <summary>
        /// Looks up a target. Returns false and sets <paramref name="target"/> to null for any
        /// id that is not registered, including malformed and reserved ids; never throws, so
        /// it is safe to call with a provider's raw output.
        /// </summary>
        public bool TryGet(string id, out object target)
        {
            if (!CanBeRegistered(id))
            {
                target = null;
                return false;
            }

            return targets.TryGetValue(id, out target);
        }

        /// <summary>
        /// Returns false for any id that is not registered, including malformed and reserved
        /// ids; never throws.
        /// </summary>
        public bool Contains(string id)
        {
            return CanBeRegistered(id) && targets.ContainsKey(id);
        }

        private static bool CanBeRegistered(string id)
        {
            return GetIdError(id) == null;
        }

        // The single place ids are validated. Returns null for an id that may be registered,
        // otherwise the reason it may not. Comparisons are ordinal, like the dictionary's.
        private static string GetIdError(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return "Target IDs must contain at least one non-whitespace character.";
            }

            if (id.Trim() != id)
            {
                return "Target IDs cannot start or end with whitespace.";
            }

            if (string.Equals(id, NoTarget, StringComparison.Ordinal))
            {
                return $"Target IDs cannot be '{NoTarget}': it is reserved for the schema's \"no target\" value.";
            }

            return null;
        }
    }
}

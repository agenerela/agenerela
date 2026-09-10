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
        private readonly Dictionary<string, object> targets = new Dictionary<string, object>();
        private readonly List<string> ids = new List<string>();

        public TargetRegistry()
        {
            Ids = new ReadOnlyCollection<string>(ids);
        }

        /// <summary>
        /// Registered IDs in insertion order.
        /// </summary>
        public IReadOnlyList<string> Ids { get; }

        public int Count => ids.Count;

        public void Register(string id, object target)
        {
            ValidateId(id);

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

        public bool Unregister(string id)
        {
            ValidateId(id);

            if (!targets.Remove(id))
            {
                return false;
            }

            ids.Remove(id);
            return true;
        }

        public bool TryGet(string id, out object target)
        {
            ValidateId(id);
            return targets.TryGetValue(id, out target);
        }

        public bool Contains(string id)
        {
            ValidateId(id);
            return targets.ContainsKey(id);
        }

        private static void ValidateId(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException(
                    "Target IDs must contain at least one non-whitespace character.",
                    nameof(id));
            }

            if (id.Trim() != id)
            {
                throw new ArgumentException(
                    "Target IDs cannot start or end with whitespace.",
                    nameof(id));
            }
        }
    }
}
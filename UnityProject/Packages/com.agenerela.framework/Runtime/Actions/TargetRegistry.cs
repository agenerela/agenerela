using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Agenerela
{
    /// <summary>
    /// Stores the objects an agent is currently allowed to reference, each with an optional
    /// category, such as <c>place</c> or <c>quest</c>, that an action can name as the kind of target
    /// it takes (<see cref="ActionDefinition.TargetCategories"/>, DR-020).
    /// </summary>
    public sealed class TargetRegistry
    {
        /// <summary>The schema's "no target" value (#8). Reserved: it can never be registered.</summary>
        public const string NoTarget = "no_target";

        private readonly Dictionary<string, object> targets = new Dictionary<string, object>();
        private readonly Dictionary<string, string> categories = new Dictionary<string, string>();
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
        /// Registers a target with no category. Throws for a malformed or reserved id, a duplicate
        /// id, or a null target: registering is developer code, so mistakes should be loud.
        /// </summary>
        public void Register(string id, object target)
        {
            Register(id, target, null);
        }

        /// <summary>
        /// Registers a target in <paramref name="category"/>. Throws as
        /// <see cref="Register(string, object)"/> does.
        /// </summary>
        /// <param name="id">The id the model may choose.</param>
        /// <param name="target">The object a handler receives.</param>
        /// <param name="category">
        /// A free string, such as <c>place</c> or <c>quest</c>, compared ordinally and kept as given,
        /// like <c>Targetable.Category</c>. Null, empty or whitespace means no category. The model
        /// never reads it: it is for <see cref="ActionDefinition.TargetCategories"/> and
        /// <see cref="Agent.TargetCategories"/> (DR-020).
        /// </param>
        public void Register(string id, object target, string category)
        {
            Check(id, target);

            if (targets.ContainsKey(id))
            {
                throw new ArgumentException(
                    $"A target with the id '{id}' is already registered.",
                    nameof(id));
            }

            Add(id, target, category);
        }

        /// <summary>
        /// Registers a target unless the same object is already registered under this id, the merge
        /// rule for target sources (DR-020): one object offered by two sources is one target, and
        /// the first offer, its category included, is the one kept. Returns whether it was added.
        /// </summary>
        /// <remarks>
        /// "The same object" is reference equality, so two equal but distinct objects are two
        /// targets, and that is still an error. A source written before this rule calls
        /// <see cref="Register(string, object, string)"/>, which throws for any id already present,
        /// and still may.
        /// </remarks>
        /// <exception cref="ArgumentException">
        /// The id is malformed or reserved, or already registered for another object.
        /// </exception>
        /// <exception cref="ArgumentNullException"><paramref name="target"/> is null.</exception>
        public bool Offer(string id, object target, string category = null)
        {
            Check(id, target);

            if (targets.TryGetValue(id, out object existing))
            {
                if (ReferenceEquals(existing, target))
                {
                    return false;
                }

                throw new ArgumentException(
                    $"A target with the id '{id}' is already registered for another object. Two objects " +
                    "answering to one id cannot both be offered.",
                    nameof(id));
            }

            Add(id, target, category);
            return true;
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

            categories.Remove(id);
            ids.Remove(id);
            return true;
        }

        /// <summary>
        /// The category a target was registered in, or null when it has none or the id is not
        /// registered, including malformed and reserved ids; never throws.
        /// </summary>
        public string CategoryOf(string id)
        {
            return CanBeRegistered(id) && categories.TryGetValue(id, out string category) ? category : null;
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

        // What Register and Offer both refuse, before anything is stored.
        private static void Check(string id, object target)
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
        }

        // A blank category is no category, so CategoryOf never returns one.
        private void Add(string id, object target, string category)
        {
            targets.Add(id, target);
            if (!string.IsNullOrWhiteSpace(category))
            {
                categories.Add(id, category);
            }

            ids.Add(id);
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

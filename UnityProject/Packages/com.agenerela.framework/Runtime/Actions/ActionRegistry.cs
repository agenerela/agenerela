using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Agenerela
{
    /// <summary>Stores action definitions in stable order and maps action ids to handlers.</summary>
    public sealed class ActionRegistry
    {
        /// <summary>The schema's idle action value. Reserved: it can never be registered by game code.</summary>
        public const string None = "none";

        // Holds the action definitions/options in stable order for the model to choose from.
        private readonly List<ActionDefinition> _definitions = new List<ActionDefinition>();

        // Uses the action definition id as the key, and stores the definition and handler together.
        private readonly Dictionary<string, (ActionDefinition Definition, IActionHandler Handler)> _byId =
            new Dictionary<string, (ActionDefinition Definition, IActionHandler Handler)>();

        public ActionRegistry()
        {
            Definitions = new ReadOnlyCollection<ActionDefinition>(_definitions);
        }

        /// <summary>
        /// Registered action definitions in insertion order. These are every registered action,
        /// not only the actions currently available for a decision.
        /// </summary>
        public IReadOnlyList<ActionDefinition> Definitions { get; }

        /// <summary>
        /// Registers an action definition with the handler that executes it.
        /// </summary>
        public void Register(ActionDefinition definition, IActionHandler handler)
        {
            // Adds an action to the registry. Developer mistakes throw clearly before anything is stored.
            Check(definition, handler);
            Add(definition, handler);
        }

        /// <summary>
        /// Registers every [AgentAction] method on <paramref name="owner"/>: the code front door
        /// (DR-011). Each method becomes an action whose handler calls it, and whose IsAvailable
        /// calls its [Available] check if it has one. Actions are added base class first, then in
        /// the order they are declared.
        /// </summary>
        /// <param name="assets">
        /// Action assets that win over an attribute with the same id (DR-011): the asset's
        /// definition is registered, and the method still runs it. That is how one action's
        /// wording moves into an asset, where Phase 4 can vary it without a recompile. Assets that
        /// match no method are left for <see cref="Register"/>, and null entries are skipped.
        /// </param>
        /// <remarks>
        /// Registers all of the owner's actions or, if one is rejected, none of them. Throws for an
        /// owner with no [AgentAction] methods, a malformed method or attribute, an id that is
        /// malformed, reserved or already registered, and two assets with one method's id. The asset
        /// wins the definition only: an action still has exactly one handler, so an id already
        /// registered through <see cref="Register"/> cannot come from a method as well.
        /// </remarks>
        public void RegisterMethods(object owner, IEnumerable<ActionDefinitionAsset> assets = null)
        {
            if (owner == null)
            {
                throw new ArgumentNullException(nameof(owner));
            }

            var actions = AgentActionReader.Read(owner);
            if (actions.Count == 0)
            {
                throw new ArgumentException($"{owner.GetType().Name} has no [AgentAction] methods.", nameof(owner));
            }

            var candidates = assets == null
                ? new List<ActionDefinitionAsset>()
                : new List<ActionDefinitionAsset>(assets);

            // The asset wins (DR-011): its definition replaces the attribute's, and the method
            // stays the handler.
            for (int i = 0; i < actions.Count; i++)
            {
                var asset = AssetFor(actions[i].Definition.Id, candidates);
                if (asset != null)
                {
                    actions[i] = (asset.Action, actions[i].Handler);
                }
            }

            // Checks every action before storing any, so a rejected one leaves the registry as it was.
            foreach (var (definition, handler) in actions)
            {
                Check(definition, handler);
            }

            foreach (var (definition, handler) in actions)
            {
                Add(definition, handler);
            }
        }

        /// <summary>
        /// Looks up a registered action by id. Returns false and null out values for any
        /// unregistered, null, or malformed id.
        /// </summary>
        public bool TryGet(string actionId, out ActionDefinition definition, out IActionHandler handler)
        {
            // Checks whether the action id is already inside the dictionary.
            if (ActionDefinition.ValidateId(actionId, out _) &&
                _byId.TryGetValue(actionId, out var entry))
            {
                // If found, fills the out variables with the registered definition and handler.
                definition = entry.Definition;
                handler = entry.Handler;
                return true;
            }

            // If not found, clears the out variables and returns false.
            definition = null;
            handler = null;
            return false;
        }

        /// <summary>
        /// Returns the handler for a definition that is expected to already be registered.
        /// </summary>
        public IActionHandler HandlerFor(ActionDefinition definition)
        {
            // Gets the handler from the dictionary for a definition expected to be registered.
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            if (!TryGet(definition.Id, out _, out var handler))
            {
                throw new KeyNotFoundException(
                    $"No handler is registered for action '{definition.Id}'. Call Register(definition, handler) first.");
            }

            return handler;
        }

        // Throws for anything Register refuses. Stores nothing.
        private void Check(ActionDefinition definition, IActionHandler handler)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition), "Action definition cannot be null.");
            }

            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler), "Action handler cannot be null.");
            }

            if (!ActionDefinition.ValidateId(definition.Id, out string problem))
            {
                throw new ArgumentException(problem, nameof(definition));
            }

            if (string.Equals(definition.Id, None, StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    $"Action id '{None}' is reserved for the schema's idle action.",
                    nameof(definition));
            }

            if (_byId.ContainsKey(definition.Id))
            {
                throw new InvalidOperationException($"Action already registered: {definition.Id}");
            }
        }

        private void Add(ActionDefinition definition, IActionHandler handler)
        {
            _byId.Add(definition.Id, (definition, handler));
            _definitions.Add(definition);
        }

        // The one asset that defines this id, or null. Two would leave its wording ambiguous.
        private static ActionDefinitionAsset AssetFor(string id, List<ActionDefinitionAsset> assets)
        {
            ActionDefinitionAsset match = null;
            foreach (var asset in assets)
            {
                if (asset == null || asset.Action == null || asset.Action.Id != id)
                {
                    continue;
                }

                if (match != null)
                {
                    throw new ArgumentException(
                        $"Two assets define action '{id}': {match.name} and {asset.name}.",
                        nameof(assets));
                }

                match = asset;
            }

            return match;
        }
    }
}

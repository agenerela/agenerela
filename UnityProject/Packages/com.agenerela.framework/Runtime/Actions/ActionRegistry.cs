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

            if (!_byId.TryAdd(definition.Id, (definition, handler)))
            {
                throw new InvalidOperationException($"Action already registered: {definition.Id}");
            }

            _definitions.Add(definition);
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
    }
}

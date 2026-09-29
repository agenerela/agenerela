using System;
using System.Collections.Generic;

namespace Agenerela
{
    public interface IActionHandler
    {
        // Is this action legal right now?
        bool IsAvailable(AgentContext ctx);

        // Ordinary game code. Only reached after every guard has passed.
        void Execute(AgentContext ctx, AgentDecision decision);
    }

    public sealed class ActionRegistry
    {
        // Make the list of action definitions/options that the AI can choose through for the to give to the handler
        private readonly List<ActionDefinition> _definitions = new List<ActionDefinition>();

        // Find the Action Definiton's ID and store the actual definition and handler under the same key which is the
        // Action Definition's ID
        private readonly Dictionary<string, (ActionDefinition Definition, IActionHandler Handler)> _byId =
        new Dictionary<string, (ActionDefinition Definition, IActionHandler Handler)>();

        // List meant to only real all the Action definitions that are available in the object
        public IReadOnlyList<ActionDefinition> Definitions => _definitions;

        // Method to add Action in to the dictionary of actions the AI can choose through from.
        // If duplicate/Null values is found throws expeception
        public void Register(ActionDefinition definition, IActionHandler handler)
        {

            if (definition == null)
                throw new ArgumentNullException(nameof(definition), "Action definition cannot be null.");

            if (handler == null)
                throw new ArgumentNullException(nameof(handler), "Action handler cannot be null.");

            if (!_byId.TryAdd(definition.Id, (definition, handler)))
                throw new InvalidOperationException($"Action already registered: {definition.Id}");

            _definitions.Add(definition);
        }

        // Method to check if the action is already inside of the dictionary
        public bool TryGet(string actionId, out ActionDefinition definition, out IActionHandler handler)
        {
            // Check if value is found in dictionary
            if (_byId.TryGetValue(actionId, out var entry))
            {
                // Returns true if found as a valid action and definition and handler variables are made and given values
                definition = entry.Definition;
                handler = entry.Handler;
                return true;
            }

            // Returns false if found as a valid action and definition and handler variables are set to null and cant be used
            definition = null;
            handler = null;
            return false;
        }

        // This method gets the literal action from the dictionary and returns it to see if it should be used or not from the
        // handler.
        public IActionHandler HandlerFor(ActionDefinition definition)
        {
            return _byId[definition.Id].Handler;
        }
    }
}

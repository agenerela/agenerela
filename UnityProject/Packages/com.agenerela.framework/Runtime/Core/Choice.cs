using System;

namespace Agenerela
{
    /// <summary>
    /// One option a call asks the agent to choose among (<see cref="DecideOptions.Choices"/>, DR-022):
    /// an id, the words that describe it, and whether it can be chosen now. Immutable, so a list
    /// handed to a call cannot change under it.
    /// </summary>
    /// <remarks>
    /// The id becomes a value of the reply's <c>action</c> field for that call only, so it follows the
    /// rules an action id does (<see cref="ActionDefinition.ValidateId"/>: lowercase, no whitespace)
    /// and cannot be <see cref="ActionRegistry.None"/>, which every call offers anyway. The
    /// description is what a local model reads, in the prompt's list of allowed actions. Nothing here
    /// says what the choice is for (DR-008).
    /// </remarks>
    public sealed class Choice
    {
        /// <param name="id">What the reply answers with, such as <c>hire_them</c>.</param>
        /// <param name="description">What the option means, in one short clause: "Pay 30 gold, and 14 soldiers join our army".</param>
        /// <param name="available">
        /// Whether it can be chosen now: <see cref="Availability.Yes"/> by default. One that cannot
        /// is left out of the schema and the prompt, its reason recorded in
        /// <see cref="DecisionTelemetry.ChoicesUnavailable"/>. A bool converts to one.
        /// </param>
        /// <exception cref="ArgumentNullException"><paramref name="id"/> or <paramref name="description"/> is null.</exception>
        public Choice(string id, string description, Availability available = default)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Description = description ?? throw new ArgumentNullException(nameof(description));
            Available = available;
        }

        /// <summary>The id the reply answers with.</summary>
        public string Id { get; }

        /// <summary>What the option means, as the prompt lists it.</summary>
        public string Description { get; }

        /// <summary>Whether it can be chosen now, and why not when it cannot.</summary>
        public Availability Available { get; }

        /// <summary>The id, and "(unavailable)" when it cannot be chosen.</summary>
        public override string ToString()
        {
            return Available.IsAvailable ? Id : Id + " (unavailable)";
        }
    }
}

using System;

namespace Agenerela
{
    /// <summary>
    /// One agent's part of a <see cref="DecisionRound"/> (#216, item 2): the agent, and the stimulus
    /// and <see cref="DecideOptions"/> it is asked with, exactly as <see cref="Agent.DecideAsync"/>
    /// would take them.
    /// </summary>
    public readonly struct RoundCall
    {
        /// <param name="agent">The agent asked. A round asks each agent once.</param>
        /// <param name="stimulus">What it is asked, as <see cref="Agent.DecideAsync"/> takes it.</param>
        /// <param name="options">
        /// Its options, copied when the round starts; null uses the defaults. Its
        /// <see cref="DecideOptions.Correlation"/> stays the caller's. With no
        /// <see cref="DecideOptions.Chain"/>, the round gives it its own (<see cref="DecisionRound.Id"/>).
        /// </param>
        /// <exception cref="ArgumentNullException"><paramref name="agent"/> is null.</exception>
        public RoundCall(Agent agent, string stimulus, DecideOptions options = null)
        {
            Agent = agent ?? throw new ArgumentNullException(nameof(agent));
            Stimulus = stimulus;
            Options = options;
        }

        /// <summary>The agent asked.</summary>
        public Agent Agent { get; }

        /// <summary>What it is asked.</summary>
        public string Stimulus { get; }

        /// <summary>The call's options as the caller gave them; null for the defaults.</summary>
        public DecideOptions Options { get; }
    }
}

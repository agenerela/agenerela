using System;

namespace Agenerela
{
    /// <summary>
    /// Adapts a function into a guard without requiring a dedicated class, for a game rule added
    /// to <see cref="Agent.Guards"/>:
    /// <c>agent.Guards.Add(new DelegateGuard("bell_cooldown", input => …))</c>.
    /// </summary>
    public sealed class DelegateGuard : IDecisionGuard
    {
        private readonly Func<GuardInput, GuardVerdict> inspect;

        /// <param name="name">What telemetry records when this guard stops a decision.</param>
        /// <param name="inspect">The verdict for one decision.</param>
        /// <exception cref="ArgumentException"><paramref name="name"/> is null, empty or whitespace.</exception>
        /// <exception cref="ArgumentNullException"><paramref name="inspect"/> is null.</exception>
        public DelegateGuard(string name, Func<GuardInput, GuardVerdict> inspect)
        {
            // Every DelegateGuard has the same type, so a blank name could not be told apart.
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("A delegate guard needs a name, which telemetry records.", nameof(name));
            }

            if (inspect == null)
            {
                throw new ArgumentNullException(nameof(inspect));
            }

            Name = name;
            this.inspect = inspect;
        }

        public string Name { get; }

        public GuardVerdict Inspect(GuardInput input)
        {
            return inspect(input);
        }
    }
}

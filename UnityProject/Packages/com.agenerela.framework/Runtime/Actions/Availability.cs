using System;

namespace Agenerela
{
    /// <summary>
    /// A per-target availability answer (#190 LV #19, #216 item 5, DR-020): this action can be done
    /// to this target right now, or it cannot, with an optional reason why not.
    /// <see cref="ITargetAvailability.IsAvailableFor"/> returns one.
    /// </summary>
    /// <remarks>
    /// The reason is for the developer: it goes to telemetry (<see cref="DecisionTelemetry.TargetsUnavailable"/>),
    /// to <see cref="Agent.Preview"/>, and to the <see cref="ActionResult"/> of an
    /// <see cref="Agent.Execute(DecisionResult)"/> that refuses a stale pair. It never reaches the model:
    /// an option that is not legal is left out of the schema rather than forbidden in prose
    /// (build plan §2.2). A bool converts to one, so a check may simply return true or false.
    /// </remarks>
    public readonly struct Availability : IEquatable<Availability>
    {
        private readonly bool unavailable;

        private Availability(bool unavailable, string reason)
        {
            this.unavailable = unavailable;
            Reason = reason;
        }

        /// <summary>The action can be done to the target right now.</summary>
        /// <remarks><c>default(Availability)</c> is this too, so a forgotten answer never masks anything.</remarks>
        public static Availability Yes => default;

        /// <summary>The action cannot be done to the target right now.</summary>
        /// <param name="reason">Why not, in words a developer reads; null or blank for no reason.</param>
        public static Availability No(string reason = null)
        {
            return new Availability(true, string.IsNullOrWhiteSpace(reason) ? null : reason);
        }

        /// <summary>True when the action can be done to the target right now.</summary>
        public bool IsAvailable => !unavailable;

        /// <summary>Why not, as the check gave it. Null when available, or when the check gave no reason.</summary>
        public string Reason { get; }

        /// <summary><see cref="Yes"/> for true, <see cref="No"/> with no reason for false.</summary>
        public static implicit operator Availability(bool available)
        {
            return available ? Yes : No();
        }

        public bool Equals(Availability other)
        {
            return unavailable == other.unavailable && string.Equals(Reason, other.Reason, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is Availability other && Equals(other);
        }

        public override int GetHashCode()
        {
            return (unavailable ? 1 : 0) ^ (Reason?.GetHashCode() ?? 0);
        }

        public override string ToString()
        {
            if (IsAvailable)
            {
                return "Yes";
            }

            return Reason == null ? "No" : "No: " + Reason;
        }
    }
}

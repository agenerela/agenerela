using System;

namespace GreyBoxStrategy
{
    /// <summary>
    /// The action ids. They are the ids stage 2 registers with the framework, so a log line from
    /// the scripted AI and one from a model read the same.
    /// </summary>
    public static class Actions
    {
        // Council: one per country per turn.
        public const string Recruit = "recruit";
        public const string Fortify = "fortify";
        public const string RaiseTaxes = "raise_taxes";
        public const string DeclareWar = "declare_war";
        public const string Attack = "attack";
        public const string OfferPeace = "offer_peace";
        /// <summary>Hold: do nothing this turn. The framework's own <c>none</c>.</summary>
        public const string None = "none";

        // Envoy: the answer to another country's offer of peace.
        public const string AcceptPeace = "accept_peace";
        public const string RefusePeace = "refuse_peace";

        public static readonly string[] Council = { Recruit, Fortify, RaiseTaxes, DeclareWar, Attack, OfferPeace, None };
        public static readonly string[] Envoy = { AcceptPeace, RefusePeace };

        /// <summary>Whether the action names a province (fortify, attack) rather than a country.</summary>
        public static bool TargetsProvince(string action) => action == Fortify || action == Attack;

        /// <summary>Whether the action names a country (declare_war, offer_peace).</summary>
        public static bool TargetsCountry(string action) => action == DeclareWar || action == OfferPeace;
    }

    /// <summary>
    /// One choice: an action and, for the four that take one, a target id. The framework's
    /// <c>no_target</c> is <see cref="Target"/> null here.
    /// </summary>
    public sealed class Move : IEquatable<Move>
    {
        public Move(string action, string target = null, string detail = null)
        {
            Action = action ?? throw new ArgumentNullException(nameof(action));
            Target = string.IsNullOrEmpty(target) ? null : target;
            Detail = detail;
        }

        public static Move Hold => new Move(Actions.None);

        public string Action { get; }
        public string Target { get; }
        /// <summary>
        /// What the target is right now, as the log and later the framework's target description
        /// show it: "neutral, garrison 12". Not part of the move's identity.
        /// </summary>
        public string Detail { get; }

        public bool Equals(Move other) => other != null && other.Action == Action && other.Target == Target;
        public override bool Equals(object obj) => Equals(obj as Move);
        public override int GetHashCode() => (Action.GetHashCode() * 397) ^ (Target?.GetHashCode() ?? 0);

        /// <summary>"attack fenmark", or "recruit".</summary>
        public override string ToString() => Target == null ? Action : Action + " " + Target;
    }
}

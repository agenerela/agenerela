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

        // The writs turn (rules v2, R1).
        /// <summary>An army order: march to a province, the rules' shortest path.</summary>
        public const string March = "march";
        /// <summary>A treaty term: all occupied land between the two returns (v2 5.1).</summary>
        public const string OfferWhitePeace = "offer_white_peace";
        /// <summary>A treaty term: every province of theirs we occupy becomes ours; ours they occupy return.</summary>
        public const string DemandOccupied = "demand_occupied";
        /// <summary>A treaty term, the losing side's offer: every province of ours they occupy becomes theirs.</summary>
        public const string CedeOccupied = "cede_occupied";

        public static readonly string[] Council = { Recruit, Fortify, RaiseTaxes, DeclareWar, Attack, OfferPeace, None };
        /// <summary>The court's orders in the writs turn: no attack (armies do that) and treaty terms for peace.</summary>
        public static readonly string[] Court = { Recruit, Fortify, RaiseTaxes, DeclareWar, OfferWhitePeace, DemandOccupied, CedeOccupied, None };
        public static readonly string[] ArmyOrders = { March, None };

        // R3: recruiting one unit type, in place of recruit while GameRules.UnitTypes is on (rules v2 3.1).
        public const string RecruitPikes = "recruit_pikes";
        public const string RecruitRiders = "recruit_riders";
        public const string RecruitBows = "recruit_bows";
        /// <summary>The court's recruit orders by type, offered only while <see cref="GameRules.UnitTypes"/> is on.</summary>
        public static readonly string[] RecruitTypes = { RecruitPikes, RecruitRiders, RecruitBows };
        /// <summary>The unit type a recruit order raises, or null for any other action.</summary>
        public static UnitType? RecruitsType(string action) =>
            action == RecruitPikes ? UnitType.Pikes : action == RecruitRiders ? UnitType.Riders : action == RecruitBows ? UnitType.Bows : (UnitType?)null;
        public static string RecruitFor(UnitType t) => t == UnitType.Riders ? RecruitRiders : t == UnitType.Bows ? RecruitBows : RecruitPikes;
        /// <summary>Whether the action is a treaty term, answered by the other side.</summary>
        public static bool IsTerm(string action) => action == OfferWhitePeace || action == DemandOccupied || action == CedeOccupied;
        public static readonly string[] Envoy = { AcceptPeace, RefusePeace };

        /// <summary>Whether the action names a province (fortify, attack) rather than a country.</summary>
        public static bool TargetsProvince(string action) => action == Fortify || action == Attack || action == March;

        /// <summary>Whether the action names a country (declare_war, offer_peace).</summary>
        public static bool TargetsCountry(string action) => action == DeclareWar || action == OfferPeace || IsTerm(action);
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

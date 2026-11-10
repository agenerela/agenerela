using System.Collections.Generic;
using System.Linq;

namespace GreyBoxStrategy
{
    /// <summary>
    /// Which moves a country may make, derived from the state alone. One function,
    /// <see cref="WhyNot"/>, answers for every move, and both the list a player is offered and the
    /// re-check at resolution call it, so the two can never disagree.
    /// </summary>
    /// <remarks>
    /// Stage 2 maps these rules one to one onto the framework: an action-wide rule onto
    /// <c>[Available]</c>, a per-target rule onto <c>[AvailableFor]</c> (<c>ITargetAvailability</c>),
    /// and the re-check onto <c>Agent.Execute</c>'s. That is why every rule here returns a reason:
    /// the framework's <c>Availability</c> carries one too.
    /// </remarks>
    public static class Legality
    {
        /// <summary>
        /// Null when the move is legal for this country right now, otherwise why not, in a short
        /// sentence the log prints: "fenmark is not next to our land".
        /// </summary>
        public static string WhyNot(World world, string countryId, Move move)
        {
            var me = world.Country(countryId);
            if (me == null) return "no such country";
            if (!me.Alive) return "we are out of the game";
            var rules = world.Rules;

            switch (move.Action)
            {
                case Actions.None:
                    return move.Target == null ? null : "hold takes no target";

                case Actions.Recruit:
                    if (move.Target != null) return "recruit takes no target";
                    return world.Levy(me) >= rules.MinLevy ? null : $"recruiting needs {rules.MinLevy * rules.GoldPerSoldier} gold";

                case Actions.RaiseTaxes:
                    if (move.Target != null) return "raise_taxes takes no target";
                    return me.Stability >= rules.MinStabilityToTax ? null : $"taxes need stability {rules.MinStabilityToTax}";

                case Actions.Fortify:
                {
                    var p = world.Province(move.Target);
                    if (p == null) return "fortify needs one of our provinces";
                    if (p.Owner != countryId) return $"{p.Id} is not ours";
                    if (me.Gold < rules.FortCost) return $"a fort needs {rules.FortCost} gold";
                    if (p.Forts >= rules.MaxFortsPerProvince) return $"{p.Id} already has {p.Forts} forts";
                    if (!world.IsBorder(p)) return $"{p.Id} borders no one";
                    return null;
                }

                case Actions.Attack:
                {
                    var p = world.Province(move.Target);
                    if (p == null) return "attack needs a province";
                    if (p.Owner == countryId) return $"{p.Id} is already ours";
                    if (me.Army < rules.MinArmyToAttack) return $"attacking needs {rules.MinArmyToAttack} soldiers";
                    if (!world.Touches(countryId, p)) return $"{p.Id} is not next to our land";
                    if (p.Owner != null && !world.AtWar(countryId, p.Owner)) return $"we are not at war with {p.Owner}";
                    return null;
                }

                case Actions.DeclareWar:
                {
                    var other = world.Country(move.Target);
                    if (other == null || other.Id == countryId) return "declare_war needs another country";
                    if (!other.Alive) return $"{other.Id} is out of the game";
                    var rel = world.RelationBetween(countryId, other.Id);
                    if (rel == Relation.War) return $"we are already at war with {other.Id}";
                    if (rel == Relation.Truce) return $"we have a truce with {other.Id}";
                    if (me.Army < rules.MinArmyToDeclareWar) return $"war needs {rules.MinArmyToDeclareWar} soldiers";
                    if (!world.Borders(countryId, other.Id)) return $"{other.Id} does not border us";
                    return null;
                }

                case Actions.OfferPeace:
                {
                    var other = world.Country(move.Target);
                    if (other == null || other.Id == countryId) return "offer_peace needs another country";
                    if (!other.Alive) return $"{other.Id} is out of the game";
                    return world.AtWar(countryId, other.Id) ? null : $"we are not at war with {other.Id}";
                }

                default:
                    return $"{move.Action} is not a council move";
            }
        }

        public static bool IsLegal(World world, string countryId, Move move) => WhyNot(world, countryId, move) == null;

        /// <summary>
        /// Every legal council move for this country, in a stable order: the actions in
        /// <see cref="Actions.Council"/>'s order, each action's targets in id order, hold last.
        /// </summary>
        public static List<Move> CouncilMoves(World world, string countryId)
        {
            var legal = new List<Move>();
            foreach (string action in Actions.Council)
            {
                if (action == Actions.None) continue;
                foreach (var candidate in Candidates(world, action))
                    if (IsLegal(world, countryId, candidate))
                        legal.Add(new Move(candidate.Action, candidate.Target, Describe(world, countryId, candidate.Target)));
            }
            legal.Add(Move.Hold);
            return legal;
        }

        /// <summary>The two answers an envoy call offers.</summary>
        public static List<Move> EnvoyMoves() =>
            new List<Move> { new Move(Actions.AcceptPeace), new Move(Actions.RefusePeace) };

        private static IEnumerable<Move> Candidates(World world, string action)
        {
            if (Actions.TargetsProvince(action))
                return world.ProvinceList.OrderBy(p => p.Id, System.StringComparer.Ordinal).Select(p => new Move(action, p.Id));
            if (Actions.TargetsCountry(action))
                return world.CountryList.OrderBy(c => c.Id, System.StringComparer.Ordinal).Select(c => new Move(action, c.Id));
            return new[] { new Move(action) };
        }

        /// <summary>
        /// A target as a player would want to see it: "neutral, garrison 12", "orsk's, 1 fort",
        /// "ours, 0 forts", "at war, army 38". Stage 2 uses the same text as the target's description.
        /// </summary>
        public static string Describe(World world, string countryId, string targetId)
        {
            if (targetId == null) return null;
            var p = world.Province(targetId);
            if (p != null)
            {
                string forts = p.Forts == 1 ? "1 fort" : $"{p.Forts} forts";
                string capital = p.IsCapital ? ", a capital" : "";
                if (p.Owner == null) return $"neutral, garrison {p.Garrison}" + (p.Forts > 0 ? ", " + forts : "");
                if (p.Owner == countryId) return $"ours, {forts}{capital}";
                return $"{p.Owner}'s, {forts}{capital}";
            }
            var c = world.Country(targetId);
            if (c != null)
            {
                var rel = world.RelationBetween(countryId, c.Id);
                string r = rel == Relation.War ? "at war" : rel == Relation.Truce ? "truce" : "at peace";
                return $"{r}, army {c.Army}, {Facts.Provinces(world.ProvinceCount(c.Id))}";
            }
            return null;
        }
    }
}

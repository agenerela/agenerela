using System;
using System.Collections.Generic;
using System.Linq;

namespace GreyBoxStrategy
{
    /// <summary>
    /// What a ruler reads in the writs turn (rules v2, section 1.5): facts kept local, so the prompt does
    /// not grow with the map. The one-move game's <see cref="Facts"/> is unchanged; these are only the
    /// writs turn's.
    /// </summary>
    /// <remarks>
    /// R2 found the one-move facts list every rival, one line each: twelve lines with ten countries.
    /// Here only the rivals that matter are named: those at war with us, then those our land borders,
    /// at most <see cref="MaxRivals"/>. The standing facts stay at eight lines on any map.
    /// </remarks>
    public static class WritFacts
    {
        public const int MaxRivals = 3;
        public const int MaxArmiesListed = 5;
        public const int MaxStanding = 8;

        public static List<string> Standing(World world, string countryId)
        {
            var me = world.Country(countryId);
            var rules = world.Rules;
            int held = world.ProvinceCount(countryId);
            var armies = world.ArmiesOf(countryId).OrderByDescending(a => a.Soldiers).ThenBy(a => a.Id, StringComparer.Ordinal).ToList();
            var lines = new List<string>
            {
                Facts.Goal(world, held) + (world.Writs(countryId) == GameRules.UnlimitedWrits ? " Writs: no limit this turn." : $" Writs: {world.Writs(countryId)} this turn."),
                $"Treasury: {me.Gold} gold; income {world.Income(me)} and upkeep {world.Upkeep(me)} a turn.",
                $"Soldiers: {me.Army} in {Plural(armies.Count, "army", "armies")}. Stability: {me.Stability} of {rules.MaxStability}."
                    + (me.Stability <= 1 ? " A province may revolt." : ""),
            };
            if (armies.Count > 0)
            {
                // With unit types (R3), each army's mix: "maren_host 50 (25 pikes, 15 riders, 10 bows) at stonereach".
                var shown = armies.Take(MaxArmiesListed).Select(a => $"{a.Id} {a.Soldiers}" + (rules.UnitTypes ? $" ({Units.Mix(a)})" : "") + $" at {a.Location}, " +
                    (a.Marching ? $"marching to {a.Destination}" : a.Awaiting ? "awaiting orders" : "holding"));
                lines.Add("Our armies: " + string.Join("; ", shown) + (armies.Count > MaxArmiesListed ? $"; and {armies.Count - MaxArmiesListed} more" : "") + ".");
            }
            var occupiedOfOurs = world.ProvinceList.Where(p => p.Owner == countryId && p.Occupier != null).ToList();

            var rivals = world.Alive.Where(c => c.Id != countryId && (world.AtWar(countryId, c.Id) || world.Borders(countryId, c.Id)))
                .OrderByDescending(c => world.AtWar(countryId, c.Id))
                .ThenByDescending(c => c.Army).ThenBy(c => c.Id, StringComparer.Ordinal)
                .Take(MaxRivals);
            foreach (var other in rivals)
            {
                var rel = world.RelationBetween(countryId, other.Id);
                string relation = rel == Relation.War ? $"at war with us, war score {WarScore.Of(world, countryId, other.Id):+0;-0;0}"
                    : rel == Relation.Truce ? $"truce with us for {world.TruceTurnsLeft(countryId, other.Id)} more turns" : "at peace with us";
                int theirArmies = world.ArmiesOf(other.Id).Count();
                // With OccupationBecomesOwnership on, each occupation says how long it has been held: "corwick (held 3 of 8 turns)".
                int keepAfter = world.Rules.OccupationBecomesOwnership;
                string Held(Province p) => keepAfter > 0 ? $"{p.Id} (held {p.OccupiedTurns} of {keepAfter} turns)" : p.Id;
                string occupies = string.Join(", ", occupiedOfOurs.Where(p => p.Occupier == other.Id).Select(Held));
                string weOccupy = string.Join(", ", world.ProvinceList.Where(p => p.Owner == other.Id && p.Occupier == countryId).Select(Held));
                // With unit types (R3), what its armies are mostly: "orsk: at war with us; 60 soldiers in 2 armies, mostly riders, ...".
                string mostly = rules.UnitTypes ? Units.Mostly(UnitMix.Of(world.ArmiesOf(other.Id))) : null;
                lines.Add($"{other.Id}: {relation}; {other.Army} soldiers in {Plural(theirArmies, "army", "armies")}" + (mostly != null ? $", {mostly}" : "") + $", {Facts.Provinces(world.ProvinceCount(other.Id))}"
                          + (world.Borders(countryId, other.Id) ? "; borders us" : "")
                          + (weOccupy.Length > 0 ? $"; we occupy {weOccupy}" : "")
                          + (occupies.Length > 0 ? $"; it occupies our {occupies}" : "") + ".");
            }

            int neutralNear = world.ProvinceList.Count(p => p.IsNeutral && world.Neighbours(p).Any(n => n.Controller == countryId));
            if (neutralNear > 0 && lines.Count < MaxStanding)
                lines.Add(neutralNear == 1 ? "1 neutral province borders our land." : $"{neutralNear} neutral provinces border our land.");
            return lines.Take(MaxStanding).ToList();
        }

        /// <summary>The court's moves as one line among its facts, from the moves it was offered.</summary>
        public static string CourtMovesLine(IReadOnlyList<Move> legal)
        {
            var parts = new List<string>();
            void Add(string action, string verb)
            {
                var ms = legal.Where(m => m.Action == action).ToList();
                if (ms.Count == 0) return;
                if (ms[0].Target == null) { parts.Add(verb); return; }
                var targets = ms.Select(m => m.Target).ToList();
                parts.Add(verb + " " + (targets.Count == 1 ? targets[0] : string.Join(", ", targets.Take(targets.Count - 1)) + " or " + targets.Last()));
            }
            Add(Actions.Recruit, "recruit");
            Add(Actions.RecruitPikes, "recruit pikes");
            Add(Actions.RecruitRiders, "recruit riders");
            Add(Actions.RecruitBows, "recruit bows");
            Add(Actions.Fortify, "fortify");
            Add(Actions.RaiseTaxes, "raise taxes");
            Add(Actions.DeclareWar, "declare war on");
            Add(Actions.OfferWhitePeace, "offer white peace to");
            Add(Actions.DemandOccupied, "demand what we occupy from");
            Add(Actions.CedeOccupied, "cede what is occupied to");
            return parts.Count == 0 ? null : "Moves open to us: " + string.Join("; ", parts) + ".";
        }

        /// <summary>An army's orders as one line: "Orders open to maren_host: march to fenmark (neutral, garrison 9; next to us), ...; hold."</summary>
        public static string ArmyMovesLine(string armyId, IReadOnlyList<Move> legal)
        {
            var marches = legal.Where(m => m.Action == Actions.March).Select(m => m.Detail == null ? m.Target : $"{m.Target} ({m.Detail})").ToList();
            return $"Orders open to {armyId}: " + (marches.Count == 0 ? "" : "march to " + string.Join(", ", marches) + "; ") + "hold.";
        }

        /// <summary>"Ordered this turn: recruit; maren_host to march to corwick." Null before the first order.</summary>
        public static string OrderedLine(IReadOnlyList<string> ordered) =>
            ordered == null || ordered.Count == 0 ? null : "Ordered this turn: " + string.Join("; ", ordered) + ".";

        private static string Plural(int n, string one, string many) => n == 1 ? $"1 {one}" : $"{n} {many}";
    }
}

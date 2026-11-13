using System;
using System.Collections.Generic;
using System.Linq;

namespace GreyBoxStrategy
{
    /// <summary>
    /// What a country may order in the writs turn (rules v2, R1): the court's orders and its armies'
    /// marches. As in <see cref="Legality"/>, one function answers each, with a reason, and both the
    /// list a player is offered and the re-check when the order lands call it.
    /// </summary>
    public static class WritLegality
    {
        /// <summary>The most provinces a fortify order is offered at once.</summary>
        public const int MaxFortTargets = 6;

        // --- The court ------------------------------------------------------------------------

        /// <summary>Null when the court order is legal now, otherwise why not.</summary>
        public static string WhyNotCourt(World world, string countryId, Move move)
        {
            var me = world.Country(countryId);
            if (me == null) return "no such country";
            if (!me.Alive) return "we are out of the game";
            var rules = world.Rules;
            switch (move.Action)
            {
                case Actions.None:
                    return move.Target == null ? null : "none takes no target";

                case Actions.Recruit:
                    if (move.Target != null) return "recruit takes no target";
                    if (RecruitSite(world, countryId) == null) return "we hold no province of ours to recruit in";
                    return world.Levy(me) >= rules.MinLevy ? null : $"recruiting needs {rules.MinLevy * rules.GoldPerSoldier} gold";

                case Actions.RaiseTaxes:
                    if (move.Target != null) return "raise_taxes takes no target";
                    return me.Stability >= rules.MinStabilityToTax ? null : $"taxes need stability {rules.MinStabilityToTax}";

                case Actions.Fortify:
                {
                    var p = world.Province(move.Target);
                    if (p == null) return "fortify needs one of our provinces";
                    if (p.Owner != countryId) return $"{p.Id} is not ours";
                    if (p.Occupier != null) return $"{p.Id} is occupied by {p.Occupier}";
                    if (me.Gold < rules.FortCost) return $"a fort needs {rules.FortCost} gold";
                    if (p.Forts >= rules.MaxFortsPerProvince) return $"{p.Id} already has {p.Forts} forts";
                    if (!world.IsBorder(p)) return $"{p.Id} borders no one";
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

                case Actions.OfferWhitePeace:
                case Actions.DemandOccupied:
                case Actions.CedeOccupied:
                {
                    var other = world.Country(move.Target);
                    if (other == null || other.Id == countryId) return $"{move.Action} needs another country";
                    if (!other.Alive) return $"{other.Id} is out of the game";
                    if (!world.AtWar(countryId, other.Id)) return $"we are not at war with {other.Id}";
                    if (move.Action == Actions.DemandOccupied)
                    {
                        if (!world.ProvinceList.Any(p => p.Owner == other.Id && p.Occupier == countryId)) return $"we occupy nothing of {other.Id}'s";
                        int cost = WarScore.OccupationPoints(world, countryId, other.Id);
                        int score = WarScore.Of(world, countryId, other.Id);
                        if (score < cost) return $"the war score {score} does not pay for {cost}";
                    }
                    if (move.Action == Actions.CedeOccupied && !world.ProvinceList.Any(p => p.Owner == countryId && p.Occupier == other.Id))
                        return $"{other.Id} occupies nothing of ours";
                    return null;
                }

                default:
                    return $"{move.Action} is not a court order";
            }
        }

        /// <summary>Every legal court order, the actions in <see cref="Actions.Court"/>'s order, targets in id order, none last.</summary>
        public static List<Move> CourtMoves(World world, string countryId)
        {
            var legal = new List<Move>();
            foreach (string action in Actions.Court)
            {
                if (action == Actions.None) continue;
                IEnumerable<Move> candidates = Actions.TargetsProvince(action)
                    ? world.ProvinceList.OrderBy(p => p.Id, StringComparer.Ordinal).Select(p => new Move(action, p.Id))
                    : Actions.TargetsCountry(action)
                        ? world.CountryList.OrderBy(c => c.Id, StringComparer.Ordinal).Select(c => new Move(action, c.Id))
                        : new[] { new Move(action) };
                var open = candidates.Where(m => WhyNotCourt(world, countryId, m) == null).ToList();
                // At most six forts offered (a large realm borders many provinces): those facing an
                // enemy first, then capitals, then by id, so the list stays the dozen targets a model reads well.
                if (action == Actions.Fortify)
                    open = open.Select(m => world.Province(m.Target))
                        .OrderByDescending(p => world.Neighbours(p).Any(n => n.Controller != null && n.Controller != countryId && world.AtWar(countryId, n.Controller)))
                        .ThenByDescending(p => p.IsCapital).ThenBy(p => p.Id, StringComparer.Ordinal)
                        .Take(MaxFortTargets).OrderBy(p => p.Id, StringComparer.Ordinal).Select(p => new Move(action, p.Id)).ToList();
                foreach (var m in open)
                    legal.Add(new Move(m.Action, m.Target, DescribeCourtTarget(world, countryId, m)));
            }
            legal.Add(Move.Hold);
            return legal;
        }

        private static string DescribeCourtTarget(World world, string countryId, Move m)
        {
            if (m.Target == null) return null;
            if (world.Country(m.Target) != null && world.AtWar(countryId, m.Target))
                return $"at war, war score {WarScore.Of(world, countryId, m.Target):+0;-0;0}";
            return Legality.Describe(world, countryId, m.Target);
        }

        /// <summary>Where a levy appears: our capital if we still hold it, else our first province we control, by id.</summary>
        public static Province RecruitSite(World world, string countryId)
        {
            var mine = world.ProvinceList.Where(p => p.Owner == countryId && p.Occupier == null).ToList();
            return mine.FirstOrDefault(p => p.IsCapital) ?? mine.OrderBy(p => p.Id, StringComparer.Ordinal).FirstOrDefault();
        }

        // --- Armies ---------------------------------------------------------------------------

        /// <summary>
        /// Whether an army of this country may enter the province: neutral land, land it owns or
        /// occupies, land of a country it is at war with, or land occupied by one. Never land of a
        /// country it is at peace or in truce with.
        /// </summary>
        public static bool Passable(World world, string countryId, Province p)
        {
            if (p.Owner == null || p.Owner == countryId || p.Occupier == countryId) return true;
            if (world.AtWar(countryId, p.Owner)) return true;
            return p.Occupier != null && world.AtWar(countryId, p.Occupier);
        }

        /// <summary>
        /// The shortest path from <paramref name="from"/> to every province within <paramref name="range"/>
        /// steps through passable land, neighbours taken in id order so ties break the same way every time.
        /// </summary>
        public static Dictionary<string, List<string>> Paths(World world, string countryId, string from, int range)
        {
            var paths = new Dictionary<string, List<string>>(StringComparer.Ordinal) { [from] = new List<string>() };
            var queue = new Queue<string>();
            queue.Enqueue(from);
            while (queue.Count > 0)
            {
                string at = queue.Dequeue();
                if (paths[at].Count >= range) continue;
                // A province held against us is where a march fights; it goes no further through it.
                if (at != from && Blocks(world, countryId, world.Province(at))) continue;
                foreach (string n in world.Map.Neighbours(at))
                {
                    if (paths.ContainsKey(n) || !Passable(world, countryId, world.Province(n))) continue;
                    var path = new List<string>(paths[at]) { n };
                    paths[n] = path;
                    queue.Enqueue(n);
                }
            }
            paths.Remove(from);
            return paths;
        }

        /// <summary>Whether a march must stop in this province to fight: neutral land with a garrison, or land held by an enemy.</summary>
        public static bool Blocks(World world, string countryId, Province p)
        {
            if (p.Controller == countryId) return false;
            if (p.Owner == null) return p.Garrison > 0;
            return p.Controller != null && world.AtWar(countryId, p.Controller);
        }

        /// <summary>Null when the army may be given this order now, otherwise why not.</summary>
        public static string WhyNotArmy(World world, string countryId, string armyId, Move move)
        {
            var army = world.FindArmy(armyId);
            if (army == null) return "no such army";
            if (army.Owner != countryId) return $"{armyId} is not ours";
            switch (move.Action)
            {
                case Actions.None:
                    return move.Target == null ? null : "hold takes no target";
                case Actions.March:
                {
                    var p = world.Province(move.Target);
                    if (p == null) return "march needs a province";
                    if (p.Id == army.Location) return $"{armyId} is already at {p.Id}";
                    if (!Passable(world, countryId, p)) return $"{p.Id} belongs to {p.Owner}, at peace with us";
                    if (!Paths(world, countryId, army.Location, world.Rules.MarchRange).ContainsKey(p.Id))
                        return $"{p.Id} is more than {world.Rules.MarchRange} steps away";
                    return null;
                }
                default:
                    return $"{move.Action} is not an army order";
            }
        }

        /// <summary>
        /// The orders offered an army: marches to the nearest provinces it may reach, at most
        /// <see cref="GameRules.MarchTargets"/>, nearest first and then by id, and hold last.
        /// </summary>
        public static List<Move> ArmyMoves(World world, string countryId, string armyId)
        {
            var army = world.FindArmy(armyId);
            var legal = new List<Move>();
            if (army != null && army.Owner == countryId)
            {
                foreach (var kv in Paths(world, countryId, army.Location, world.Rules.MarchRange)
                             .OrderBy(kv => kv.Value.Count).ThenBy(kv => kv.Key, StringComparer.Ordinal)
                             .Take(world.Rules.MarchTargets))
                    legal.Add(new Move(Actions.March, kv.Key, DescribeMarch(world, countryId, kv.Key, kv.Value.Count)));
            }
            legal.Add(Move.Hold);
            return legal;
        }

        private static string DescribeMarch(World world, string countryId, string provinceId, int steps)
        {
            var p = world.Province(provinceId);
            string where = steps == 1 ? "next to us" : $"{steps} steps";
            string what;
            if (p.Owner == null) what = $"neutral, garrison {p.Garrison}";
            else if (p.Owner == countryId) what = p.Occupier == null ? "ours" : $"ours, occupied by {p.Occupier}";
            else what = p.Occupier == countryId ? $"{p.Owner}'s, occupied by us" : $"{p.Owner}'s, at war";
            int armies = world.ArmiesAt(provinceId).Where(a => a.Owner != countryId).Sum(a => a.Soldiers);
            return $"{what}; {where}" + (armies > 0 ? $"; enemy armies {armies}" : "");
        }

        /// <summary>
        /// This country's armies waiting for orders, the most urgent first: a hostile army beside it,
        /// then by size, then by id (v2 1.3; sieges and stores wait for R3).
        /// </summary>
        public static List<Army> Awaiting(World world, string countryId) =>
            world.ArmiesOf(countryId).Where(a => a.Awaiting)
                .OrderByDescending(a => HostileBeside(world, a) ? 1 : 0)
                .ThenByDescending(a => a.Soldiers)
                .ThenBy(a => a.Id, StringComparer.Ordinal)
                .ToList();

        /// <summary>Whether an enemy army stands in the army's province or one bordering it.</summary>
        public static bool HostileBeside(World world, Army a) =>
            world.Armies.Any(o => o.Owner != a.Owner && world.AtWar(a.Owner, o.Owner)
                                  && (o.Location == a.Location || world.Map.AreNeighbours(o.Location, a.Location)));
    }
}

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
                    if (rules.UnitTypes) return "with unit types, recruit pikes, riders or bows";
                    if (move.Target != null) return "recruit takes no target";
                    if (RecruitSite(world, countryId) == null) return "we hold no province of ours to recruit in";
                    return world.Levy(me) >= rules.MinLevy ? null : ShortOfMen(world, me) ?? $"recruiting needs {rules.MinLevy * rules.GoldPerSoldier} gold";

                case Actions.RecruitPikes:
                case Actions.RecruitRiders:
                case Actions.RecruitBows:
                {
                    if (!rules.UnitTypes) return "unit types are off";
                    if (move.Target != null) return $"{move.Action} takes no target";
                    if (RecruitSite(world, countryId) == null) return "we hold no province of ours to recruit in";
                    var type = Actions.RecruitsType(move.Action).Value;
                    return world.Levy(me, type) >= rules.MinLevy ? null
                        : ShortOfMen(world, me) ?? $"recruiting {Units.Name(type)} needs {rules.MinLevy * rules.GoldPerSoldier * Units.CostFactor(rules, type)} gold";
                }

                case Actions.BuildMarket:
                case Actions.BuildBarracks:
                case Actions.BuildFarm:
                {
                    if (!rules.EconomyOn) return "buildings are the economy's";
                    var p = world.Province(move.Target);
                    var what = Actions.BuildsWhat(move.Action).Value;
                    string name = what.ToString().ToLowerInvariant();
                    if (p == null) return $"{move.Action} needs one of our provinces";
                    if (p.Owner != countryId) return $"{p.Id} is not ours";
                    if (p.Occupier != null) return $"{p.Id} is occupied by {p.Occupier}";
                    if (p.Has(what)) return $"{p.Id} already has a {name}";
                    int cost = BuildingCost(rules, what);
                    return me.Spendable >= cost ? null : $"a {name} needs {cost} gold";
                }

                case Actions.TaxesLow:
                case Actions.TaxesNormal:
                case Actions.TaxesHigh:
                {
                    if (!rules.EconomyOn) return "tax levels are the economy's";
                    if (move.Target != null) return $"{move.Action} takes no target";
                    var level = Actions.SetsTaxes(move.Action).Value;
                    return me.Taxes == level ? $"taxes are already {level.ToString().ToLowerInvariant()}" : null;
                }

                case Actions.RaiseTaxes:
                    if (rules.EconomyOn) return "with the economy, taxes are a standing level";
                    if (move.Target != null) return "raise_taxes takes no target";
                    return me.Stability >= rules.MinStabilityToTax ? null : $"taxes need stability {rules.MinStabilityToTax}";

                case Actions.Fortify:
                {
                    var p = world.Province(move.Target);
                    if (p == null) return "fortify needs one of our provinces";
                    if (p.Owner != countryId) return $"{p.Id} is not ours";
                    if (p.Occupier != null) return $"{p.Id} is occupied by {p.Occupier}";
                    if (me.Spendable < rules.FortCost) return $"a fort needs {rules.FortCost} gold";
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

        /// <summary>
        /// Free orders (<see cref="GameRules.FreeOrders"/>): the gold a court order holds back when it is given, paid when
        /// it lands. A levy at its type's price, a fort at its cost, every other order nothing.
        /// </summary>
        public static int CourtOrderCost(World world, string countryId, Move order)
        {
            var me = world.Country(countryId);
            if (me == null || order == null) return 0;
            if (Actions.RecruitsType(order.Action) is UnitType type) return world.Levy(me, type) * world.Rules.GoldPerSoldier * Units.CostFactor(world.Rules, type);
            if (order.Action == Actions.Recruit) return world.Levy(me) * world.Rules.GoldPerSoldier;
            if (order.Action == Actions.Fortify) return world.Rules.FortCost;
            if (Actions.BuildsWhat(order.Action) is Building b) return BuildingCost(world.Rules, b);
            return 0;
        }

        /// <summary>
        /// Free orders: the court orders still offered once <paramref name="given"/> are given this turn: legal with their
        /// gold held back, and none already given offered again (a levy may repeat). What the court's next request would
        /// be offered; <paramref name="world"/> is left as it was.
        /// </summary>
        public static List<Move> CourtMovesAfter(World world, string countryId, IReadOnlyList<Move> given)
        {
            var me = world.Country(countryId);
            if (me == null) return new List<Move> { Move.Hold };
            int held = (given ?? Array.Empty<Move>()).Sum(m => CourtOrderCost(world, countryId, m));
            int men = (given ?? Array.Empty<Move>()).Sum(m => CourtOrderManpower(world, countryId, m));
            me.Reserved += held;
            me.ReservedManpower += men;
            try { return NotGivenAgain(CourtMoves(world, countryId), given); }
            finally { me.Reserved -= held; me.ReservedManpower -= men; }
        }

        /// <summary>Economy, E2: what a building would add in this province: gold for a market or a farm, men for a barracks.</summary>
        public static int BuildingGain(World world, Province p, Building b)
        {
            var r = world.Rules;
            if (b == Building.Market) return p.Info.IsCoastal ? r.MarketCoastYield : r.MarketYield;
            if (b == Building.Farm) return p.Info.Terrain == Terrain.Plains ? r.FarmPlainsYield : r.FarmYield;
            return r.BarracksManpower;
        }

        /// <summary>Economy, E2: what a building costs.</summary>
        public static int BuildingCost(GameRules r, Building b) => b == Building.Market ? r.MarketCost : b == Building.Barracks ? r.BarracksCost : r.FarmCost;

        /// <summary>Economy: the manpower a court order holds back when it is given: a levy's soldiers, nothing else.</summary>
        public static int CourtOrderManpower(World world, string countryId, Move order)
        {
            var me = world.Country(countryId);
            if (!world.Rules.EconomyOn || me == null || order == null) return 0;
            if (Actions.RecruitsType(order.Action) is UnitType type) return world.Levy(me, type);
            if (order.Action == Actions.Recruit) return world.Levy(me);
            return 0;
        }

        /// <summary>Free orders: court moves less those already given this turn; levies and none may repeat.</summary>
        public static List<Move> NotGivenAgain(List<Move> moves, IReadOnlyList<Move> given)
        {
            if (given == null || given.Count == 0) return moves;
            return moves.Where(m => m.Action == Actions.None || m.Action == Actions.Recruit || Actions.RecruitsType(m.Action) != null
                                    || !given.Any(g => g.Action == m.Action && (m.Action == Actions.RaiseTaxes || g.Target == m.Target)))
                .Where(m => Actions.SetsTaxes(m.Action) == null || !given.Any(g => Actions.SetsTaxes(g.Action) != null))
                // Economy, E2: one building order a province a turn, a fort among them.
                .Where(m => !IsBuildOrder(m.Action) || !given.Any(g => IsBuildOrder(g.Action) && g.Target == m.Target && (Actions.BuildsWhat(g.Action) != null || Actions.BuildsWhat(m.Action) != null)))
                .ToList();
        }

        private static bool IsBuildOrder(string action) => action == Actions.Fortify || Actions.BuildsWhat(action) != null;

        /// <summary>
        /// Economy: why a levy is too small when manpower, not gold, is what runs short ("recruiting needs 5 manpower; we
        /// have 2"), or null. The screen shows this reason to the player.
        /// </summary>
        private static string ShortOfMen(World world, Country me)
        {
            var r = world.Rules;
            if (!r.EconomyOn) return null;
            int men = me.Manpower - me.ReservedManpower;
            return men < r.MinLevy ? $"recruiting needs {r.MinLevy} manpower; we have {System.Math.Max(0, men)}" : null;
        }

        /// <summary>Every legal court order, the actions in <see cref="Actions.Court"/>'s order, targets in id order, none last.</summary>
        public static List<Move> CourtMoves(World world, string countryId)
        {
            var legal = new List<Move>();
            foreach (string action in Actions.CourtFor(world.Rules))
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
                // Economy, E2: at most MaxBuildTargets provinces a building, those that would yield the most first.
                if (Actions.BuildsWhat(action) is Building building)
                    open = open.Select(m => world.Province(m.Target))
                        .OrderByDescending(p => BuildingGain(world, p, building)).ThenBy(p => p.Id, StringComparer.Ordinal)
                        .Take(world.Rules.MaxBuildTargets).OrderBy(p => p.Id, StringComparer.Ordinal).Select(p => new Move(action, p.Id)).ToList();
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
                // A fort holds the road: a march stops beside an enemy fort, and an army already there may only
                // step to the fort or back into our land.
                bool held = HeldByFort(world, countryId, at);
                if (at != from && held) continue;
                foreach (string n in world.Map.Neighbours(at))
                {
                    if (paths.ContainsKey(n) || !Passable(world, countryId, world.Province(n))) continue;
                    if (held && world.Province(n).Controller != countryId && !HoldsTheRoad(world, countryId, world.Province(n))) continue;
                    var path = new List<string>(paths[at]) { n };
                    paths[n] = path;
                    queue.Enqueue(n);
                }
            }
            paths.Remove(from);
            return paths;
        }

        /// <summary>With <see cref="GameRules.FortsHoldTheRoad"/>: an enemy's walled province, held by a country we are at war with, that no one besieges.</summary>
        public static bool HoldsTheRoad(World world, string countryId, Province p) =>
            world.Rules.FortsHoldTheRoad && p.Forts > 0 && p.SiegedBy == null && p.Controller != null && p.Controller != countryId
            && world.AtWar(countryId, p.Controller);

        /// <summary>With <see cref="GameRules.FortsHoldTheRoad"/>: whether a province not ours borders a fort that holds the road against us.</summary>
        public static bool HeldByFort(World world, string countryId, string provinceId)
        {
            if (!world.Rules.FortsHoldTheRoad) return false;
            var p = world.Province(provinceId);
            return p.Controller != countryId && world.Neighbours(p).Any(n => HoldsTheRoad(world, countryId, n));
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
            if (world.Rules.Fog)
            {
                string seen = ArmySight.ArmiesThere(world, countryId, provinceId);
                bool scouted = seen != null && p.Info.Terrain != Terrain.Forest && ArmySight.Scouts(world, countryId, provinceId);
                return $"{what}; {where}" + (seen != null ? $"; {seen}" + (scouted ? MostlyThere(world, countryId, provinceId) : "") : "");
            }
            int armies = world.ArmiesAt(provinceId).Where(a => a.Owner != countryId).Sum(a => a.Soldiers);
            return $"{what}; {where}" + (armies > 0 ? $"; enemy armies {armies}" + MostlyThere(world, countryId, provinceId) : "");
        }

        // With the counters line (R3), the largest type among the enemy armies in a march's province: ", mostly pikes".
        private static string MostlyThere(World world, string countryId, string provinceId)
        {
            if (!world.Rules.UnitTypes || !world.Rules.CountersLine) return "";
            var t = Units.Largest(UnitMix.Of(world.ArmiesAt(provinceId).Where(a => a.Owner != countryId)));
            return t is UnitType u ? $", mostly {Units.Name(u)}" : "";
        }

        /// <summary>
        /// This country's armies waiting for orders, the most urgent first: a hostile army beside it,
        /// then by size, then by id (v2 1.3; sieges and stores wait for R3).
        /// </summary>
        public static List<Army> Awaiting(World world, string countryId) =>
            world.ArmiesOf(countryId).Where(a => a.Awaiting)
                .OrderByDescending(a => world.Rules.HungerFirst && world.Rules.Supply ? Hunger(world, a) : 0)
                .ThenByDescending(a => HostileBeside(world, a) ? 1 : 0)
                .ThenByDescending(a => a.Soldiers)
                .ThenBy(a => a.Id, StringComparer.Ordinal)
                .ToList();

        /// <summary>
        /// How hungry an army is where it stands, for <see cref="GameRules.HungerFirst"/>: 2 with no stores left in land
        /// that does not feed it, 1 when its stores last one more turn there or less, else 0.
        /// </summary>
        public static int Hunger(World world, Army a)
        {
            int eats = Game.Eats(world, a);
            if (eats <= 0) return 0;
            if (a.HalfStores <= 0) return 2;
            return a.HalfStores <= eats ? 1 : 0;
        }

        /// <summary>The nearest land that feeds this army (ours, held by us) by steps it may take, and how many; null when none can be reached.</summary>
        public static (string province, int steps)? NearestSupply(World world, Army a)
        {
            var seen = new Dictionary<string, int>(StringComparer.Ordinal) { [a.Location] = 0 };
            var queue = new Queue<string>();
            queue.Enqueue(a.Location);
            while (queue.Count > 0)
            {
                string at = queue.Dequeue();
                if (at != a.Location && Game.Supplied(world.Province(at), a.Owner)) return (at, seen[at]);
                foreach (string n in world.Map.Neighbours(at).OrderBy(x => x, StringComparer.Ordinal))
                {
                    if (seen.ContainsKey(n) || !Passable(world, a.Owner, world.Province(n))) continue;
                    seen[n] = seen[at] + 1;
                    queue.Enqueue(n);
                }
            }
            return null;
        }

        /// <summary>Whether an enemy army stands in the army's province or one bordering it.</summary>
        public static bool HostileBeside(World world, Army a) =>
            world.Armies.Any(o => o.Owner != a.Owner && world.AtWar(a.Owner, o.Owner)
                                  && (o.Location == a.Location || world.Map.AreNeighbours(o.Location, a.Location)));
    }
}

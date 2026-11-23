using System;
using System.Collections.Generic;
using System.Linq;

namespace GreyBoxStrategy.Replay
{
    /// <summary>
    /// Free orders (<see cref="GameRules.FreeOrders"/>): the court orders you build in the council, in order, with the gold
    /// they hold back. It keeps no rules of its own: what is offered after the orders already queued, and what an order
    /// costs, come from the game's queries; the list only keeps the orders in turn and says why a button is shut.
    /// </summary>
    public sealed class CourtList
    {
        private readonly Func<Move, int> cost;
        private readonly Func<IReadOnlyList<Move>, List<Move>> offeredAfter;
        private readonly Func<Move, int> men;
        private readonly Func<IReadOnlyList<Move>, string, string> rulesWhyAfter;

        /// <param name="orders">The plan's court orders (<c>WritsPlan.Courts</c>): the list adds to it and removes from it in place.</param>
        /// <param name="gold">The treasury now.</param>
        /// <param name="cost">The gold an order holds back (the rules' <c>CourtOrderCost</c>).</param>
        /// <param name="offeredAfter">The court's legal orders once these are given (the rules' <c>CourtMovesAfter</c>).</param>
        /// <param name="manpower">The economy: the men who can be raised now (0 without it).</param>
        /// <param name="men">The economy: the men an order holds back (the rules' <c>CourtOrderManpower</c>); null, none.</param>
        /// <param name="rulesWhyAfter">The rules' own reason an order of a kind is not offered with these orders' gold and men
        /// held back, or null when they give none; see <see cref="For"/>.</param>
        public CourtList(List<Move> orders, int gold, Func<Move, int> cost, Func<IReadOnlyList<Move>, List<Move>> offeredAfter,
            int manpower = 0, Func<Move, int> men = null, Func<IReadOnlyList<Move>, string, string> rulesWhyAfter = null)
        {
            Orders = orders ?? throw new ArgumentNullException(nameof(orders));
            Gold = gold;
            this.cost = cost;
            this.offeredAfter = offeredAfter;
            Manpower = manpower;
            this.men = men;
            this.rulesWhyAfter = rulesWhyAfter;
        }

        /// <summary>
        /// Your court's list on a world: gold, costs and the orders offered from the rules' queries; with the economy, the
        /// manpower each order holds back. Its <see cref="WhyShut"/> also asks the rules (<c>WritLegality.WhyNotCourt</c>)
        /// with the queued orders' gold and men held back, as <c>CourtMovesAfter</c> holds them.
        /// </summary>
        public static CourtList For(World w, string you, List<Move> orders)
        {
            var me = w.Country(you);
            bool economy = w.Rules.EconomyOn;
            return new CourtList(orders, me?.Gold ?? 0,
                m => WritLegality.CourtOrderCost(w, you, m), given => WritLegality.CourtMovesAfter(w, you, given),
                economy ? me?.Manpower ?? 0 : 0,
                economy ? m => WritLegality.CourtOrderManpower(w, you, m) : (Func<Move, int>)null,
                (given, action) => Held(w, you, given, () => WhyNotAction(w, you, action)));
        }

        // Asks the rules with the orders' gold and men held back, and gives them back after.
        private static string Held(World w, string you, IReadOnlyList<Move> given, Func<string> ask)
        {
            var me = w.Country(you);
            if (me == null) return ask();
            int gold = given.Sum(m => WritLegality.CourtOrderCost(w, you, m)), menHeld = given.Sum(m => WritLegality.CourtOrderManpower(w, you, m));
            me.Reserved += gold; me.ReservedManpower += menHeld;
            try { return ask(); }
            finally { me.Reserved -= gold; me.ReservedManpower -= menHeld; }
        }

        /// <summary>
        /// The rules' reason no order of this kind is legal now, picked from <c>WritLegality.WhyNotCourt</c>'s answers, or
        /// null when one is (and for a kind it does not pick for: a fort, an order to a country). An order without a target
        /// asks once. A building asks for each province of yours: a reason that is not "already has one" first (its gold),
        /// and when every province has one, says so.
        /// </summary>
        public static string WhyNotAction(World w, string you, string action)
        {
            if (Actions.BuildsWhat(action) is Building b)
            {
                var mine = w.ProvinceList.Where(p => p.Owner == you && p.Occupier == null).OrderBy(p => p.Id, StringComparer.Ordinal).ToList();
                if (mine.Count == 0) return "we hold no province of ours to build in";
                var why = mine.Select(p => WritLegality.WhyNotCourt(w, you, new Move(action, p.Id))).ToList();
                if (why.Any(r => r == null)) return null;
                return why.FirstOrDefault(r => !r.Contains(" already has ")) ?? $"every province of ours already has a {b.ToString().ToLowerInvariant()}";
            }
            if (Actions.TargetsProvince(action) || Actions.TargetsCountry(action)) return null;
            return WritLegality.WhyNotCourt(w, you, new Move(action));
        }

        /// <summary>One province a building order may go to, with what it would add there.</summary>
        public sealed class BuildChoice
        {
            public Move Move;
            public string Province;
            /// <summary>Gold a turn for a market or a farm, men a turn for a barracks (<c>WritLegality.BuildingGain</c>).</summary>
            public int Gain;
            /// <summary>Its land in words, "plains, on the coast".</summary>
            public string Land;
            /// <summary>What it yields now, before taxes (<c>World.Yield</c>).</summary>
            public int Yield;
        }

        /// <summary>
        /// Where a building order may go now, after the queued orders: the provinces the rules offer (the best few), the
        /// most it would add first, then by id. A click on a row gives the order.
        /// </summary>
        public List<BuildChoice> BuildChoices(World w, string action)
        {
            if (!(Actions.BuildsWhat(action) is Building b)) return new List<BuildChoice>();
            return Offered(action).Where(m => w.Province(m.Target) != null)
                .Select(m =>
                {
                    var p = w.Province(m.Target);
                    return new BuildChoice
                    {
                        Move = m, Province = p.Id, Gain = WritLegality.BuildingGain(w, p, b),
                        Land = p.Info.Terrain.ToString().ToLowerInvariant() + (p.Info.IsCoastal ? ", on the coast" : ""), Yield = w.Yield(p),
                    };
                })
                .OrderByDescending(c => c.Gain).ThenBy(c => c.Province, StringComparer.Ordinal).ToList();
        }

        /// <summary>The orders, in the order they will be given.</summary>
        public List<Move> Orders { get; }
        public int Gold { get; }
        public int CostOf(Move m) => m == null ? 0 : cost(m);
        /// <summary>Gold the queued orders will spend when they land.</summary>
        public int Committed => Orders.Sum(CostOf);
        public int Left => Gold - Committed;

        /// <summary>The economy: the men who can be raised now, those the queued orders hold back, and those left.</summary>
        public int Manpower { get; }
        public bool HasManpower => men != null;
        public int MenOf(Move m) => m == null || men == null ? 0 : men(m);
        public int MenCommitted => Orders.Sum(MenOf);
        public int MenLeft => Manpower - MenCommitted;

        /// <summary>What the court may still order after the queued orders (never "none": the list ends where you stop).</summary>
        public List<Move> Offered() => offeredAfter(Orders).Where(m => m.Action != Actions.None).ToList();

        /// <summary>The orders of one kind still offered, a target each.</summary>
        public List<Move> Offered(string action) => Offered().Where(m => m.Action == action).ToList();

        /// <summary>Adds an order at the end when the rules still offer it; false (and nothing added) when they do not.</summary>
        public bool Add(Move m)
        {
            if (m == null || m.Action == Actions.None) return false;
            var offered = Offered().FirstOrDefault(o => o.Action == m.Action && o.Target == m.Target);
            if (offered == null) return false;
            Orders.Add(offered);
            return true;
        }

        /// <summary>
        /// Takes advice: adds each advised court order the list does not hold yet, in order (an order advised twice, a
        /// levy say, is added until the list holds it twice); none is skipped. Returns the advised orders the rules no
        /// longer offer, which are not added.
        /// </summary>
        public List<Move> Take(IEnumerable<Move> advised)
        {
            var refused = new List<Move>();
            var seen = new List<Move>();
            foreach (var m in advised ?? Enumerable.Empty<Move>())
            {
                if (m == null || m.Action == Actions.None) continue;
                seen.Add(m);
                int wanted = seen.Count(x => Same(x, m)), held = Orders.Count(x => Same(x, m));
                if (held >= wanted) continue;
                if (!Add(m)) refused.Add(m);
            }
            return refused;
        }

        /// <summary>Whether the list holds this advised order (as many times as it was advised).</summary>
        public bool Holds(IReadOnlyList<Move> advised, Move m)
        {
            if (m == null || m.Action == Actions.None) return true;
            return Orders.Count(x => Same(x, m)) >= advised.Count(x => Same(x, m));
        }

        private static bool Same(Move a, Move b) => a.Action == b.Action && a.Target == b.Target;

        /// <summary>
        /// Removes an order. The ones after it stay, each kept only if the rules still offer it after those before it (an
        /// order may have relied on gold or a term the removed one set up); returns the orders dropped that way.
        /// </summary>
        public List<Move> RemoveAt(int index)
        {
            var dropped = new List<Move>();
            if (index < 0 || index >= Orders.Count) return dropped;
            var rest = Orders.Skip(index + 1).ToList();
            Orders.RemoveRange(index, Orders.Count - index);
            foreach (var m in rest) if (!Add(m)) dropped.Add(m);
            return dropped;
        }

        /// <summary>
        /// Why no order of this kind can be added now, in words, or null when one can. The reasons the queued orders make:
        /// they spent the gold it needs, it is given once a turn and already queued, or (from <see cref="For"/>) the rules'
        /// own reason once their gold and men are held back, such as a levy short of manpower. With no orders queued, null:
        /// the reason is the rules' own, which the screen asks them for.
        /// </summary>
        public string WhyShut(string action)
        {
            if (Offered(action).Count > 0) return null;
            if (Orders.Count == 0) return null;
            var before = offeredAfter(new List<Move>()).Where(m => m.Action == action).ToList();
            if (before.Count == 0) return null;
            int need = before.Select(CostOf).DefaultIfEmpty(0).Min();
            if (need > Left) return $"needs {need} gold; your orders leave {Left} of {Gold}";
            // The economy: one tax level a turn.
            if (Actions.SetsTaxes(action) != null && Orders.Any(o => Actions.SetsTaxes(o.Action) != null)) return "one tax order a turn, and one is already in your orders";
            // The rules' own reason with the orders' gold and men held back: a levy short of men says so.
            string rules = rulesWhyAfter?.Invoke(Orders, action);
            if (rules != null) return "after your other orders, " + rules;
            // A building: one building order a province a turn.
            if (Actions.BuildsWhat(action) != null) return "one building order a province a turn, and your orders already build where it is offered";
            // Gold would cover it and it is queued already: the rules give it once a turn (a levy may repeat).
            if (Orders.Any(o => o.Action == action) && action != Actions.Recruit && Actions.RecruitsType(action) == null) return "given once a turn, and already in your orders";
            return "not offered after your other orders";
        }

    }
}

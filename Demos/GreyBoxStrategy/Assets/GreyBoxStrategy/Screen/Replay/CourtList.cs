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

        /// <param name="orders">The plan's court orders (<c>WritsPlan.Courts</c>): the list adds to it and removes from it in place.</param>
        /// <param name="gold">The treasury now.</param>
        /// <param name="cost">The gold an order holds back (the rules' <c>CourtOrderCost</c>).</param>
        /// <param name="offeredAfter">The court's legal orders once these are given (the rules' <c>CourtMovesAfter</c>).</param>
        public CourtList(List<Move> orders, int gold, Func<Move, int> cost, Func<IReadOnlyList<Move>, List<Move>> offeredAfter)
        {
            Orders = orders ?? throw new ArgumentNullException(nameof(orders));
            Gold = gold;
            this.cost = cost;
            this.offeredAfter = offeredAfter;
        }

        /// <summary>The orders, in the order they will be given.</summary>
        public List<Move> Orders { get; }
        public int Gold { get; }
        public int CostOf(Move m) => m == null ? 0 : cost(m);
        /// <summary>Gold the queued orders will spend when they land.</summary>
        public int Committed => Orders.Sum(CostOf);
        public int Left => Gold - Committed;

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
        /// Why no order of this kind can be added now, in words, or null when one can. Only the two reasons the list
        /// itself makes: the queued orders spent the gold it needs, or it is given once a turn and already queued. Any
        /// other reason is the rules' own, which the screen asks them for.
        /// </summary>
        public string WhyShut(string action)
        {
            if (Offered(action).Count > 0) return null;
            if (Orders.Count == 0) return null;
            var before = offeredAfter(new List<Move>()).Where(m => m.Action == action).ToList();
            if (before.Count == 0) return null;
            int need = before.Select(CostOf).DefaultIfEmpty(0).Min();
            if (need > Left) return $"needs {need} gold; your orders leave {Left} of {Gold}";
            // Gold would cover it and it is queued already: the rules give it once a turn.
            if (Orders.Any(o => o.Action == action)) return "given once a turn, and already in your orders";
            return "not offered after your other orders";
        }

    }
}

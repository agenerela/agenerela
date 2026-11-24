using System.Collections.Generic;
using System.Linq;
using GreyBoxStrategy.Realm;
using GreyBoxStrategy.Replay;
using NUnit.Framework;

namespace GreyBoxStrategy.Screen.Tests
{
    /// <summary>
    /// The free council's court list (#20, free orders): adding and removing orders, the gold they hold back, the
    /// once-a-turn rule, the reason a button is shut, and what goes into the plan. The two rule queries are stood in for
    /// by a small treasury here; the list itself keeps no rules.
    /// </summary>
    public class CourtListTests
    {
        private const int Recruit = 30, Fort = 20;

        // A stand-in for the rules: 100 gold; recruits 30 and repeat; a fort 20, once a province; taxes free, once a turn.
        private static int Cost(Move m) => m.Action == Actions.Recruit ? Recruit : m.Action == Actions.Fortify ? Fort : 0;

        private static List<Move> OfferedAfter(int gold, IReadOnlyList<Move> given)
        {
            int left = gold - given.Sum(Cost);
            var all = new List<Move> { new Move(Actions.Recruit), new Move(Actions.RaiseTaxes), new Move(Actions.Fortify, "ashford"), new Move(Actions.Fortify, "brindle") };
            var offered = all.Where(m => Cost(m) <= left)
                .Where(m => m.Action == Actions.Recruit || !given.Any(g => g.Action == m.Action && g.Target == m.Target)).ToList();
            offered.Add(Move.Hold);
            return offered;
        }

        private static CourtList List(int gold = 100, List<Move> orders = null) =>
            new CourtList(orders ?? new List<Move>(), gold, Cost, given => OfferedAfter(gold, given));

        [Test]
        public void Orders_are_added_in_turn_and_hold_back_their_gold()
        {
            var plan = new RuleSession.WritsPlan();
            var c = List(orders: plan.Courts);
            Assert.That(c.Add(new Move(Actions.Recruit)), Is.True);
            Assert.That(c.Add(new Move(Actions.Fortify, "ashford")), Is.True);
            Assert.That(c.Add(new Move(Actions.RaiseTaxes)), Is.True);
            Assert.That(plan.Courts.Select(m => m.Action), Is.EqualTo(new[] { Actions.Recruit, Actions.Fortify, Actions.RaiseTaxes }), "into the plan, in order");
            Assert.That((c.Committed, c.Left), Is.EqualTo((50, 50)));
        }

        [Test]
        public void An_order_the_rules_do_not_offer_is_not_added()
        {
            var c = List(gold: 40);
            Assert.That(c.Add(new Move(Actions.Recruit)), Is.True);
            Assert.That(c.Add(new Move(Actions.Recruit)), Is.False, "10 gold left, a levy costs 30");
            Assert.That(c.Add(Move.Hold), Is.False, "none is where the list ends, not an order");
            Assert.That(c.Add(new Move(Actions.Fortify, "nowhere")), Is.False);
            Assert.That(c.Orders.Count, Is.EqualTo(1));
        }

        [Test]
        public void Recruits_repeat_while_gold_lasts_and_taxes_are_given_once()
        {
            var c = List();
            Assert.That(c.Add(new Move(Actions.Recruit)) && c.Add(new Move(Actions.Recruit)) && c.Add(new Move(Actions.Recruit)), Is.True);
            Assert.That(c.Offered(Actions.Recruit), Is.Empty, "90 spent, 10 left");
            Assert.That(c.Add(new Move(Actions.RaiseTaxes)), Is.True);
            Assert.That(c.Offered(Actions.RaiseTaxes), Is.Empty, "once a turn");
        }

        [Test]
        public void A_shut_button_says_why()
        {
            var c = List();
            Assert.That(c.WhyShut(Actions.RaiseTaxes), Is.Null, "open");
            c.Add(new Move(Actions.RaiseTaxes));
            Assert.That(c.WhyShut(Actions.RaiseTaxes), Is.EqualTo("given once a turn, and already in your orders"));
            c.Add(new Move(Actions.Recruit)); c.Add(new Move(Actions.Recruit)); c.Add(new Move(Actions.Recruit));
            Assert.That(c.WhyShut(Actions.Recruit), Is.EqualTo("needs 30 gold; your orders leave 10 of 100"));
            Assert.That(c.WhyShut(Actions.Fortify), Is.EqualTo("needs 20 gold; your orders leave 10 of 100"));
            Assert.That(c.WhyShut(Actions.DeclareWar), Is.Null, "never offered: the rules' own reason, not the list's");
        }

        [Test]
        public void Removing_an_order_frees_its_gold_and_keeps_the_rest_that_still_stand()
        {
            var c = List();
            c.Add(new Move(Actions.Recruit)); c.Add(new Move(Actions.Fortify, "ashford")); c.Add(new Move(Actions.Recruit));
            Assert.That(c.RemoveAt(0), Is.Empty);
            Assert.That(c.Orders.Select(m => m.Action), Is.EqualTo(new[] { Actions.Fortify, Actions.Recruit }));
            Assert.That(c.Left, Is.EqualTo(50));
            Assert.That(c.RemoveAt(7), Is.Empty, "out of range: nothing");
        }

        [Test]
        public void Removing_an_order_drops_the_later_ones_the_rules_no_longer_offer()
        {
            // A stand-in where a fort at brindle is offered only after taxes are raised (as a term needs its war, say).
            var orders = new List<Move>();
            List<Move> after(IReadOnlyList<Move> given)
            {
                var o = OfferedAfter(100, given);
                if (!given.Any(g => g.Action == Actions.RaiseTaxes)) o.RemoveAll(m => m.Target == "brindle");
                return o;
            }
            var c = new CourtList(orders, 100, Cost, after);
            Assert.That(c.Add(new Move(Actions.Fortify, "brindle")), Is.False);
            c.Add(new Move(Actions.RaiseTaxes)); c.Add(new Move(Actions.Fortify, "brindle")); c.Add(new Move(Actions.Recruit));
            var dropped = c.RemoveAt(0);
            Assert.That(dropped.Select(m => m.Target), Is.EqualTo(new[] { "brindle" }));
            Assert.That(c.Orders.Select(m => m.Action), Is.EqualTo(new[] { Actions.Recruit }));
        }

        [Test]
        public void Taking_the_advice_adds_its_court_orders_once_and_says_which_no_longer_stand()
        {
            var c = List();
            var advised = new List<Move> { new Move(Actions.RaiseTaxes), new Move(Actions.Recruit), new Move(Actions.Recruit), Move.Hold };
            Assert.That(c.Take(advised), Is.Empty);
            Assert.That(c.Orders.Select(m => m.Action), Is.EqualTo(new[] { Actions.RaiseTaxes, Actions.Recruit, Actions.Recruit }), "none is skipped; a levy advised twice is added twice");
            Assert.That(c.Take(advised), Is.Empty, "taking it again adds nothing");
            Assert.That(c.Orders.Count, Is.EqualTo(3));
            Assert.That(advised.All(m => c.Holds(advised, m)), Is.True, "every advised order is held: the advice is the plan");
            c.RemoveAt(2);
            Assert.That(c.Holds(advised, new Move(Actions.Recruit)), Is.False, "one levy short");
            var refused = c.Take(new List<Move> { new Move(Actions.Recruit), new Move(Actions.Recruit), new Move(Actions.Recruit), new Move(Actions.Recruit) });
            Assert.That(refused.Count, Is.EqualTo(1), "100 gold: three levies of 30 fit, the fourth is refused and said");
        }

        [Test]
        public void A_first_turn_hint_is_dismissed_once_and_for_good()
        {
            var s = new GameSettings();
            Assert.That(s.HintSeen(ScreenUI.FreeOrdersHint), Is.False);
            s.SeeHint(ScreenUI.FreeOrdersHint); s.SeeHint(ScreenUI.FreeOrdersHint); s.SeeHint("another");
            Assert.That(s.HintsSeen, Is.EqualTo(ScreenUI.FreeOrdersHint + ",another"));
            Assert.That(s.HintSeen("another") && s.HintSeen(ScreenUI.FreeOrdersHint), Is.True);
            Assert.That(s.HintSeen("free"), Is.False, "a hint's id is matched whole");
            var back = Newtonsoft.Json.JsonConvert.DeserializeObject<GameSettings>(Newtonsoft.Json.JsonConvert.SerializeObject(s)).Normalised();
            Assert.That(back.HintSeen(ScreenUI.FreeOrdersHint), Is.True, "kept in settings.json");
            Assert.That(new GameSettings { HintsSeen = null }.Normalised().HintSeen("x"), Is.False);
        }

        // ---- the rules' two queries behind the list, on a real world ----

        private static World FreeWorld()
        {
            var setup = GameSetup.Writs(3);
            var map = setup.BuildMap();
            var rules = setup.BuildRules(map);
            rules.FreeOrders = true;
            return new Game(3, map, rules).World;
        }

        [Test]
        public void The_rules_queries_count_gold_and_repeats_and_leave_the_world_as_it_was()
        {
            var w = FreeWorld();
            string me = w.CountryList.First().Id;
            var c = w.Country(me);
            c.Gold = 1000;
            int reserved = c.Reserved;
            var first = WritLegality.CourtMovesAfter(w, me, new List<Move>());
            var levy = first.First(m => m.Action == Actions.Recruit || Actions.RecruitsType(m.Action) != null);
            Assert.That(WritLegality.CourtOrderCost(w, me, levy), Is.GreaterThan(0), "a levy costs gold");
            Assert.That(WritLegality.CourtOrderCost(w, me, new Move(Actions.RaiseTaxes)), Is.EqualTo(0));
            var taxes = first.FirstOrDefault(m => m.Action == Actions.RaiseTaxes);
            Assert.That(taxes, Is.Not.Null, "taxes are offered on a calm first turn");
            var after = WritLegality.CourtMovesAfter(w, me, new List<Move> { taxes, levy });
            Assert.That(after.Any(m => m.Action == Actions.RaiseTaxes), Is.False, "taxes once a turn");
            Assert.That(after.Any(m => m.Action == levy.Action), Is.True, "a levy may repeat while gold lasts");
            Assert.That(c.Reserved, Is.EqualTo(reserved), "the world is left as it was");
            c.Gold = WritLegality.CourtOrderCost(w, me, levy);
            Assert.That(WritLegality.CourtMovesAfter(w, me, new List<Move> { levy }).Any(m => m.Action == levy.Action), Is.False, "the second levy's gold is held back by the first");
        }

        [Test]
        public void The_list_on_the_real_rules_puts_its_orders_into_the_plan()
        {
            var w = FreeWorld();
            string me = w.CountryList.First().Id;
            w.Country(me).Gold = 1000;
            var plan = new RuleSession.WritsPlan();
            var list = new CourtList(plan.Courts, w.Country(me).Gold, m => WritLegality.CourtOrderCost(w, me, m), g => WritLegality.CourtMovesAfter(w, me, g));
            var levy = list.Offered().First(m => m.Action == Actions.Recruit || Actions.RecruitsType(m.Action) != null);
            Assert.That(list.Add(levy) && list.Add(levy), Is.True);
            Assert.That(list.Add(new Move(Actions.RaiseTaxes)), Is.True);
            Assert.That(list.Add(new Move(Actions.RaiseTaxes)), Is.False);
            Assert.That(plan.Courts.Count, Is.EqualTo(3));
            Assert.That(list.Committed, Is.EqualTo(2 * WritLegality.CourtOrderCost(w, me, levy)));
        }

        // ---- the council itself, built from a real free-orders request ----

        private static void Click(UnityEngine.UIElements.Button b) =>
            typeof(UnityEngine.UIElements.Clickable).GetMethod("Invoke", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance, null,
                new[] { typeof(UnityEngine.UIElements.EventBase) }, null).Invoke(b.clickable, new object[] { null });

        [Test]
        public void In_the_free_council_the_advice_goes_into_the_court_list_and_the_hint_closes_for_good()
        {
            var setup = GameSetup.Writs(3);
            var map = setup.BuildMap();
            var rules = setup.BuildRules(map); rules.FreeOrders = true;
            var game = new Game(3, map, rules);
            var w = game.World;
            string me = w.CountryList.First().Id;
            w.Country(me).Gold = 1000;
            var req = new CountryRequest(Moment.Council, 1, me, w, new List<string>(), new List<string>(), WritLegality.CourtMoves(w, me));
            var ui = new ScreenUI(new UnityEngine.UIElements.VisualElement(), UnityEngine.ScriptableObject.CreateInstance<UnityEngine.UIElements.StyleSheet>(), null,
                w.CountryList.Select(c => c.Id).ToList(), w.ProvinceList.Select(p => p.Id).ToList(), new Dictionary<string, RecordedPlayer>(), "test");
            ui.BuildPanels();
            ui.Fog = new Fog(me, null);
            ui.Writs = true;
            var seen = new HashSet<string>();
            ui.HintSeen = seen.Contains; ui.DismissHint = id => seen.Add(id);
            var offered = WritLegality.CourtMoves(w, me);
            var advice = new Advice { Turn = 1 };
            foreach (var m in new[] { offered.First(x => x.Action == Actions.RaiseTaxes), offered.First(x => x.Action == Actions.Fortify) })
                advice.Items.Add(new AdviceItem { Request = req, Move = m, AsAsked = true });
            ui.AdvisorShown = true; ui.Advice = advice;
            ui.OrdersRequest = req;
            ui.ShowOrders(null);
            UnityEngine.UIElements.Button Find(string cls) { UnityEngine.UIElements.Button b = null; UnityEngine.UIElements.UQueryExtensions.Query<UnityEngine.UIElements.Button>(ui.Root, null, cls).ForEach(x => b = x); return b; }
            Assert.That(UnityEngine.UIElements.UQueryExtensions.Query(ui.Root, null, "hint-line").ToList().Count, Is.EqualTo(1), "the hint shows on a fresh screen");
            Click(Find("advice-all"));
            Assert.That(ui.Plan.Courts.Select(m => m.Action), Is.EqualTo(new[] { Actions.RaiseTaxes, Actions.Fortify }), "the advice's court orders, in the court list");
            Assert.That(ui.Plan.Court, Is.EqualTo(Move.Hold).Or.Null, "the single court order of the writs turn is not used");
            Assert.That(Find("advice-all").text, Is.EqualTo("The advice is your plan"));
            Click(Find("advice-all"));
            Assert.That(ui.Plan.Courts.Count, Is.EqualTo(2), "taking it again adds nothing");
            Click(Find("hint-x"));
            ui.ShowOrders(null);
            Assert.That(seen, Does.Contain(ScreenUI.FreeOrdersHint));
            Assert.That(UnityEngine.UIElements.UQueryExtensions.Query(ui.Root, null, "hint-line").ToList().Count, Is.EqualTo(0), "closed once, gone");
        }
    }
}

using System.Collections.Generic;
using System.Linq;
using GreyBoxStrategy.Realm;
using GreyBoxStrategy.Replay;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace GreyBoxStrategy.Screen.Tests
{
    /// <summary>
    /// The economy on the screen (#20, after #372 and #374): manpower and the tax level on the top bar, a province's yield
    /// and buildings, the council's tax and building orders with what they cost, hold back and add, the rules' reasons a
    /// button is shut, and a building or a tax level in the chronicle and the turn strip. Every number is the rules' own;
    /// with the economy off the screen's models are as before.
    /// </summary>
    public class EconomyScreenTests
    {
        private static (GameSetup setup, MapDefinition map, GameRules rules) Setup(bool economy, int seed = 3)
        {
            var setup = GameSetup.Writs(seed);
            var map = setup.BuildMap();
            var rules = setup.BuildRules(map);
            rules.FreeOrders = true;
            rules.Economy = economy;
            return (setup, map, rules);
        }

        private static World EconomyWorld(bool economy = true)
        {
            var (_, map, rules) = Setup(economy);
            return new Game(3, map, rules).World;
        }

        private static ReplayGame Replay(World w) => new ReplayGame { ProvinceIds = w.ProvinceList.Select(p => p.Id).ToList(), Map = w.Map, Rules = w.Rules };

        private static ReplayState StateOf(World w) => ReplayBuilder.StateOf(w, w.ProvinceList.Select(p => p.Id).ToList());

        // ---- the top bar ----

        [Test]
        public void The_top_bar_shows_manpower_and_the_tax_level_from_the_rules()
        {
            var w = EconomyWorld();
            var me = w.CountryList.First();
            me.Manpower = 25;
            me.Taxes = TaxLevel.High;
            var s = StateOf(w);
            var bar = ScreenModels.TopBar(s, me.Id, 10, true, 25, null, CountryLook.NameOf, x => x, w.Rules).ToDictionary(x => x.Key);
            Assert.That(bar["men"].Value, Is.EqualTo($"25 / {w.ManpowerCap(me)}"), "manpower of its cap, the rules' ManpowerCap");
            Assert.That(bar["men"].Sub, Is.EqualTo($"+{w.ManpowerGrowth(me)} a turn"), "the rules' ManpowerGrowth");
            Assert.That(bar["men"].Tip, Does.Contain("Each soldier recruited takes 1"));
            Assert.That(bar["tax"].Value, Is.EqualTo("High"));
            Assert.That(bar["tax"].Sub, Is.EqualTo($"x{w.TaxFactor(TaxLevel.High):0.0#} income"), "the rules' TaxFactor");
            Assert.That(bar["tax"].Tip, Does.Contain("Low: income x0.8").And.Contain("High: income x1.3; stability falls 1 a turn"));
            Assert.That(bar["gold"].Tip, Does.Contain("at high taxes (x1.3)"));
            me.Manpower = w.ManpowerCap(me);
            Assert.That(ScreenModels.TopBar(StateOf(w), me.Id, 10, true, 25, null, CountryLook.NameOf, x => x, w.Rules).First(x => x.Key == "men").Sub, Is.EqualTo("full"));
        }

        [Test]
        public void Without_the_economy_the_top_bar_is_as_before()
        {
            var w = EconomyWorld(economy: false);
            var me = w.CountryList.First();
            var s = StateOf(w);
            Assert.That(s.Buildings, Is.Null, "no buildings in the state without the economy");
            var keys = ScreenModels.TopBar(s, me.Id, 10, true, 25, null, CountryLook.NameOf, x => x, w.Rules).Select(x => x.Key).ToList();
            Assert.That(keys, Is.EqualTo(new[] { "gold", "army", "land", "stab", "war", "truce" }));
            Assert.That(ScreenModels.RealmRows(s, me.Id, null, true, CountryLook.NameOf, w.Rules).Any(r => r.Key == "men" || r.Key == "tax"), Is.False);
        }

        // ---- the province panel ----

        [Test]
        public void A_province_shows_its_yield_and_buildings_as_the_rules_count_them()
        {
            var w = EconomyWorld();
            var me = w.CountryList.First();
            var p = w.ProvinceList.Where(x => x.Owner == me.Id).OrderByDescending(x => x.Info.IsCoastal).First();
            int index = w.ProvinceList.IndexOf(p);
            var q = new RealmQueries(Replay(w), w.Map, w.Rules);
            var before = ScreenModels.ProvinceRows(StateOf(w), index, q, true, CountryLook.NameOf).ToDictionary(r => r.Key);
            Assert.That(before["income"].Value, Is.EqualTo($"+{w.Yield(p)} a turn"), "World.Yield");
            Assert.That(before["buildings"].Value, Is.EqualTo("none"));

            p.Buildings.Add(Building.Market);
            p.Buildings.Add(Building.Farm);
            var s = StateOf(w);
            Assert.That(ScreenModels.BuildingsAt(s, index), Is.EqualTo(new[] { Building.Market, Building.Farm }));
            var rows = ScreenModels.ProvinceRows(s, index, q, true, CountryLook.NameOf).ToDictionary(r => r.Key);
            Assert.That(q.Yield(s, index), Is.EqualTo(w.Yield(p)), "the panel's yield is the rules' own, buildings included");
            Assert.That(rows["income"].Value, Is.EqualTo($"+{w.Yield(p)} a turn"));
            Assert.That(rows["buildings"].Value, Is.EqualTo("market, farm"));
            var (basis, land, built) = q.YieldParts(s, index);
            Assert.That(basis + land + built.Sum(b => b.gold), Is.EqualTo(w.Yield(p)), "the tooltip's parts add up to the yield");
            Assert.That(built.Select(b => b.gold), Is.EqualTo(new[] { WritLegality.BuildingGain(w, p, Building.Market), WritLegality.BuildingGain(w, p, Building.Farm) }));
            Assert.That(rows["income"].Tip, Does.Contain($"+{WritLegality.BuildingGain(w, p, Building.Market)} for its market"));
            Assert.That(rows["buildings"].Tip, Does.Contain("Market (built)").And.Contain("Barracks: +3 men a turn, +10 to the manpower cap"));
        }

        [Test]
        public void Without_the_economy_a_province_shows_its_income_as_before()
        {
            var w = EconomyWorld(economy: false);
            var me = w.CountryList.First();
            int index = w.ProvinceList.FindIndex(x => x.Owner == me.Id);
            var rows = ScreenModels.ProvinceRows(StateOf(w), index, new RealmQueries(Replay(w), w.Map, w.Rules), true, CountryLook.NameOf);
            Assert.That(rows.Any(r => r.Key == "buildings"), Is.False);
            Assert.That(rows.First(r => r.Key == "income").Value, Is.EqualTo($"+{w.Rules.IncomePerProvince} a turn"));
        }

        [Test]
        public void A_realm_shows_its_manpower_and_taxes_only_where_its_treasury_is_known()
        {
            var w = EconomyWorld();
            var them = w.CountryList[1];
            var s = StateOf(w);
            var known = ScreenModels.RealmRows(s, them.Id, w.CountryList[0].Id, true, CountryLook.NameOf, w.Rules).ToDictionary(r => r.Key);
            Assert.That(known["men"].Value, Does.StartWith($"{them.Manpower} / {w.ManpowerCap(them)}"));
            Assert.That(known["tax"].Value, Does.StartWith("normal"));
            Assert.That(ScreenModels.RealmRows(s, them.Id, w.CountryList[0].Id, false, CountryLook.NameOf, w.Rules).Any(r => r.Key == "men"), Is.False);
        }

        // ---- the council ----

        [Test]
        public void A_levy_holds_back_its_men_and_a_levy_short_of_men_says_so()
        {
            var w = EconomyWorld();
            var me = w.CountryList.First();
            me.Gold = 1000;
            var list = CourtList.For(w, me.Id, new List<Move>());
            Assert.That(list.HasManpower, Is.True);
            Assert.That(list.Manpower, Is.EqualTo(me.Manpower));
            var levy = list.Offered().First(m => m.Action == Actions.Recruit || Actions.RecruitsType(m.Action) != null);
            int men = WritLegality.CourtOrderManpower(w, me.Id, levy);
            Assert.That(men, Is.GreaterThan(0));
            Assert.That(list.Add(levy), Is.True);
            Assert.That(list.MenOf(list.Orders[0]), Is.EqualTo(men), "the rules' CourtOrderManpower");
            Assert.That((list.MenCommitted, list.MenLeft), Is.EqualTo((men, me.Manpower - men)));
            // Levies until the men run short: then the rules' own reason, with the men held back, says manpower.
            while (list.Add(levy) || list.Add(list.Offered().FirstOrDefault(m => m.Action == levy.Action))) { }
            Assert.That(list.WhyShut(levy.Action), Does.Contain("manpower"), "the reason says manpower, not gold: " + list.WhyShut(levy.Action));
            Assert.That(me.Reserved, Is.EqualTo(0), "the world is left as it was");
            Assert.That(me.ReservedManpower, Is.EqualTo(0));
        }

        [Test]
        public void Without_the_economy_the_list_holds_back_no_men()
        {
            var w = EconomyWorld(economy: false);
            var list = CourtList.For(w, w.CountryList[0].Id, new List<Move>());
            Assert.That(list.HasManpower, Is.False);
            Assert.That(list.Offered().Any(m => Actions.SetsTaxes(m.Action) != null || Actions.BuildsWhat(m.Action) != null), Is.False);
        }

        [Test]
        public void One_tax_order_a_turn_and_the_level_in_force_is_not_offered()
        {
            var w = EconomyWorld();
            var me = w.CountryList.First();
            var list = CourtList.For(w, me.Id, new List<Move>());
            Assert.That(list.Offered(Actions.TaxesNormal), Is.Empty, "normal is in force");
            Assert.That(CourtList.WhyNotAction(w, me.Id, Actions.TaxesNormal), Is.EqualTo("taxes are already normal"), "the rules' reason");
            Assert.That(list.Add(new Move(Actions.TaxesHigh)), Is.True);
            Assert.That(list.Offered(Actions.TaxesLow), Is.Empty);
            Assert.That(list.WhyShut(Actions.TaxesLow), Is.EqualTo("one tax order a turn, and one is already in your orders"));
            Assert.That(list.CostOf(list.Orders[0]), Is.EqualTo(0), "a tax level costs no gold");
        }

        [Test]
        public void A_building_order_lists_your_provinces_with_what_each_would_add_the_most_first()
        {
            var w = EconomyWorld();
            var me = w.CountryList.First();
            me.Gold = 1000;
            var list = CourtList.For(w, me.Id, new List<Move>());
            foreach (string action in Actions.Builds)
            {
                var b = Actions.BuildsWhat(action).Value;
                var choices = list.BuildChoices(w, action);
                Assert.That(choices.Select(c => c.Province), Is.EquivalentTo(list.Offered(action).Select(m => m.Target)), "the provinces the rules offer");
                Assert.That(choices.All(c => w.Province(c.Province).Owner == me.Id), Is.True);
                Assert.That(choices.Select(c => c.Gain), Is.Ordered.Descending, "the most it would add first");
                foreach (var c in choices)
                {
                    Assert.That(c.Gain, Is.EqualTo(WritLegality.BuildingGain(w, w.Province(c.Province), b)));
                    Assert.That(c.Yield, Is.EqualTo(w.Yield(w.Province(c.Province))));
                }
            }
            var market = list.BuildChoices(w, Actions.BuildMarket).First();
            Assert.That(list.Add(market.Move), Is.True);
            Assert.That(list.Committed, Is.EqualTo(WritLegality.BuildingCost(w.Rules, Building.Market)), "the rules' BuildingCost, held back");
            Assert.That(list.BuildChoices(w, Actions.BuildFarm).Any(c => c.Province == market.Province), Is.False, "one building order a province a turn");
        }

        [Test]
        public void A_building_the_gold_does_not_cover_says_its_cost_and_a_built_one_says_so()
        {
            var w = EconomyWorld();
            var me = w.CountryList.First();
            me.Gold = 10;
            Assert.That(CourtList.WhyNotAction(w, me.Id, Actions.BuildMarket), Is.EqualTo($"a market needs {w.Rules.MarketCost} gold"),
                "the reason for one of our provinces, never 'is not ours'");
            me.Gold = 1000;
            foreach (var p in w.ProvinceList.Where(p => p.Owner == me.Id)) p.Buildings.Add(Building.Farm);
            Assert.That(CourtList.WhyNotAction(w, me.Id, Actions.BuildFarm), Is.EqualTo("every province of ours already has a farm"));
            Assert.That(CourtList.WhyNotAction(w, me.Id, Actions.BuildMarket), Is.Null);
        }

        // ---- the free council, drawn ----

        private static void Click(Button b) =>
            typeof(Clickable).GetMethod("Invoke", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance, null,
                new[] { typeof(EventBase) }, null).Invoke(b.clickable, new object[] { null });

        private static ScreenUI Council(World w, string me)
        {
            var req = new CountryRequest(Moment.Council, 1, me, w, new List<string>(), new List<string>(), WritLegality.CourtMoves(w, me));
            var ui = new ScreenUI(new VisualElement(), UnityEngine.ScriptableObject.CreateInstance<StyleSheet>(), null,
                w.CountryList.Select(c => c.Id).ToList(), w.ProvinceList.Select(p => p.Id).ToList(), new Dictionary<string, RecordedPlayer>(), "test");
            ui.BuildPanels();
            ui.Fog = new Fog(me, null);
            ui.Writs = true;
            ui.OrdersRequest = req;
            ui.ShowOrders(null);
            return ui;
        }

        private static List<Button> Buttons(ScreenUI ui, string cls) => ui.Root.Query<Button>(null, cls).ToList();

        [Test]
        public void The_council_offers_tax_levels_and_buildings_and_a_building_picks_its_province_from_a_list()
        {
            var w = EconomyWorld();
            var me = w.CountryList.First();
            me.Gold = 1000;
            var ui = Council(w, me.Id);
            var econ = Buttons(ui, "econ");
            Assert.That(econ.Count, Is.EqualTo(6), "three buildings and three tax levels");
            Assert.That(econ.Count(b => b.ClassListContains("current")), Is.EqualTo(1), "the level in force is marked");
            Assert.That(econ.First(b => b.ClassListContains("current")).tooltip, Does.Contain("taxes are already normal"));
            Assert.That(Buttons(ui, "order-move").Any(b => b.text == "Raise taxes"), Is.False, "no raise_taxes with the economy");
            var market = econ.First(b => b.Q<Label>().text == "Market");
            Assert.That(market.Query<Label>().ToList().Select(l => l.text), Has.Some.StartsWith($"{w.Rules.MarketCost} gold · up to +"));
            Click(market);
            var rows = Buttons(ui, "build-choice");
            var best = CourtList.For(w, me.Id, new List<Move>()).BuildChoices(w, Actions.BuildMarket);
            Assert.That(rows.Count, Is.EqualTo(best.Count));
            Assert.That(rows[0].Q<Label>(null, "build-choice-gain").text, Is.EqualTo($"+{best[0].Gain} gold"));
            Click(rows[0]);
            Assert.That(ui.Plan.Courts.Single(), Is.EqualTo(best[0].Move).Using<Move>((a, b) => a.Action == b.Action && a.Target == b.Target ? 0 : 1));
            Assert.That(ui.Root.Query<Label>().ToList().Select(l => l.text), Has.Some.EqualTo($"· {w.Rules.MarketCost} committed"));
            Assert.That(ui.Root.Query<Label>().ToList().Select(l => l.text), Has.Some.EqualTo($"{me.Manpower} manpower"));
        }

        [Test]
        public void Without_the_economy_the_council_is_as_before()
        {
            var w = EconomyWorld(economy: false);
            var me = w.CountryList.First();
            me.Gold = 1000;
            var ui = Council(w, me.Id);
            Assert.That(Buttons(ui, "econ"), Is.Empty);
            Assert.That(Buttons(ui, "order-move").Any(b => b.text == "Raise taxes"), Is.True);
            Assert.That(ui.Root.Query<Label>().ToList().Any(l => l.text.EndsWith(" manpower")), Is.False);
        }

        // ---- the replay: a building and a tax level in the state, the chronicle and the turn strip ----

        [Test]
        public void A_scripted_economy_game_replays_its_buildings_and_tax_levels_into_the_chronicle_and_the_strip()
        {
            var (setup, map, rules) = Setup(economy: true);
            var recording = ScriptedWorldGame.Play(setup, map, rules);
            var g = ReplayBuilder.Build(recording, map, rules);
            Assert.That(g.StoppedBecause, Is.Null);
            var events = g.Turns.SelectMany(t => t.Events).ToList();
            var builds = events.Where(e => e.Kind == ReplayEventKind.Build).ToList();
            var taxes = events.Where(e => e.Kind == ReplayEventKind.Taxes).ToList();
            Assert.That(builds, Is.Not.Empty, "the scripted courts build");
            Assert.That(taxes, Is.Not.Empty, "and set tax levels");
            // Each turn's replayed end holds what the next turn's start, taken from the rules' world, holds.
            for (int i = 1; i < g.Turns.Count; i++)
            {
                Assert.That(g.Turns[i - 1].End.Buildings, Is.EqualTo(g.Turns[i].Start.Buildings), $"turn {g.Turns[i - 1].Turn}'s buildings");
                foreach (var c in g.CountryIds)
                    Assert.That(g.Turns[i - 1].End.Countries[c].Taxes, Is.EqualTo(g.Turns[i].Start.Countries[c].Taxes), $"turn {g.Turns[i - 1].Turn}: {c}'s taxes");
            }
            var b = builds[0];
            Assert.That(ScreenModels.BuildingsAt(b.After, b.ProvinceIndex).Select(ScreenModels.BuildingName), Does.Contain(b.Building), "the map's state has it at once");
            var line = Chronicle.Line(b, CountryLook.NameOf);
            Assert.That(line.Text, Is.EqualTo($"{CountryLook.NameOf(b.Country)} builds a {b.Building} in {ScreenUI.Cap(b.Province)}"));
            Assert.That(line.Category, Is.EqualTo(ChronicleCategory.Building));
            Assert.That(Chronicle.Line(taxes[0], CountryLook.NameOf).Text, Is.EqualTo($"{CountryLook.NameOf(taxes[0].Country)} sets taxes {taxes[0].TaxLevel}"));
            var row = TurnSummary.For(b.Turn, events, g.CountryIds, null, null, CountryLook.NameOf).First(r => r.Country == b.Country);
            Assert.That(row.Kinds.Select(k => k.kind), Does.Contain(b.Building), "the strip's icon says which building");
            Assert.That(ScreenUI.KindIcon(b.Building), Is.EqualTo(ScreenUI.BuildingIcon(Actions.BuildsWhat("build_" + b.Building).Value)));
            Assert.That(TurnSummary.KindOf(taxes[0]), Is.EqualTo("taxes"));
            Assert.That(ScreenUI.KindWords("taxes", 1), Is.EqualTo("set taxes"));
        }

        [Test]
        public void Without_the_economy_a_scripted_game_has_no_building_or_tax_level()
        {
            var (setup, map, rules) = Setup(economy: false);
            var g = ReplayBuilder.Build(ScriptedWorldGame.Play(setup, map, rules), map, rules);
            var events = g.Turns.SelectMany(t => t.Events).ToList();
            Assert.That(events.Any(e => e.Kind == ReplayEventKind.Build || e.TaxLevel != null), Is.False);
            Assert.That(events.Where(e => e.Kind == ReplayEventKind.Taxes).Select(e => Chronicle.Line(e, CountryLook.NameOf).Text), Has.All.EndsWith("raises taxes"));
        }

        [Test]
        public void A_buildings_list_adds_one_in_the_rules_order()
        {
            Assert.That(ScreenModels.WithBuilding("", "farm"), Is.EqualTo("farm"));
            Assert.That(ScreenModels.WithBuilding("farm", "market"), Is.EqualTo("market,farm"));
            Assert.That(ScreenModels.WithBuilding("market,farm", "market"), Is.EqualTo("market,farm"));
            Assert.That(ScreenModels.WithBuilding(null, "barracks"), Is.EqualTo("barracks"));
        }
    }
}

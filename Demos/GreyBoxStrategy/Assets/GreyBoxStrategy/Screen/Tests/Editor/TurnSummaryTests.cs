using System.Collections.Generic;
using System.Linq;
using GreyBoxStrategy.Realm;
using GreyBoxStrategy.Replay;
using NUnit.Framework;

namespace GreyBoxStrategy.Screen.Tests
{
    /// <summary>
    /// The turn strip (#20, the lead's turn summary): what each realm did in a turn, from its recorded events; several
    /// orders a turn (free orders); a decision a guard stopped, said plainly; and what fog lets you know of a rival.
    /// </summary>
    public class TurnSummaryTests
    {
        private static readonly string[] Realms = { "valen", "orsk", "kesh" };
        private static string Name(string id) => char.ToUpperInvariant(id[0]) + id.Substring(1);
        private static string Persona(string id) => id == "kesh" ? "the opportunist" : "the builder";

        private static ReplayEvent E(ReplayEventKind kind, string country, string province = null, string army = null, string said = "", RecordedDecision d = null, int turn = 4) =>
            new ReplayEvent { Turn = turn, Kind = kind, Country = country, Province = province, Army = army, Statement = said, Decision = d, Text = kind + " " + country };

        private static List<ReplayEvent> Turn() => new List<ReplayEvent>
        {
            E(ReplayEventKind.Upkeep, "valen"),
            E(ReplayEventKind.Recruit, "valen", said: "More spears."),
            E(ReplayEventKind.Recruit, "valen"),
            E(ReplayEventKind.Fort, "valen", province: "highmoor"),
            E(ReplayEventKind.ArmyOrder, "valen", province: "ashby", army: "valen_host_1"),
            E(ReplayEventKind.ArmyOrder, "valen", army: "valen_host_2"),
            E(ReplayEventKind.Taxes, "kesh"),
            E(ReplayEventKind.ArmyOrder, "kesh", province: "thornbury", army: "kesh_host_1"),
            E(ReplayEventKind.March, "kesh"),
            E(ReplayEventKind.Recruit, "orsk", turn: 3),
        };

        [Test]
        public void Each_realm_gets_a_row_of_what_it_did_yours_first()
        {
            var rows = TurnSummary.For(4, Turn(), Realms, "kesh", Persona, Name);
            Assert.That(rows.Select(r => r.Country), Is.EqualTo(new[] { "kesh", "valen", "orsk" }), "yours first, the rest in seat order");
            Assert.That(rows[0].Mine, Is.True);
            Assert.That(rows[0].Persona, Is.EqualTo("the opportunist"));
            Assert.That(rows[0].Kinds, Is.EqualTo(new[] { ("tax", 1), ("march", 1) }), "a march is the army's order; the march event itself is not a deed");
            Assert.That(rows.Single(r => r.Country == "orsk").Deeds, Is.Empty, "orsk's recruit was another turn's");
        }

        [Test]
        public void Several_orders_of_a_kind_are_counted_and_each_is_a_line()
        {
            var valen = TurnSummary.For(4, Turn(), Realms, null, Persona, Name).Single(r => r.Country == "valen");
            Assert.That(valen.Kinds, Is.EqualTo(new[] { ("recruit", 2), ("fortify", 1), ("march", 1), ("hold", 1) }), "the upkeep line is not something it did");
            Assert.That(valen.Deeds.Count, Is.EqualTo(5));
            Assert.That(valen.Deeds[0].Quote, Is.EqualTo("More spears."));
            Assert.That(valen.Deeds[1].Quote, Is.Null, "no line said, none shown");
        }

        [Test]
        public void A_guarded_decision_gets_the_shield_and_a_plain_line()
        {
            var d = new RecordedDecision
            {
                Turn = 4, AgentId = "orsk", Action = "none", ModelAction = "declare_war", ModelTarget = "kesh",
                Verdict = new RecordedVerdict { Guard = "SchemaLegalityGuard", Kind = "Contained", Reason = "the target was not offered with this action." },
            };
            d.GuardsFired.Add("SchemaLegalityGuard");
            var events = Turn(); events.Add(E(ReplayEventKind.Hold, "orsk", d: d));
            var orsk = TurnSummary.For(4, events, Realms, null, Persona, Name).Single(r => r.Country == "orsk");
            Assert.That(orsk.Guarded, Is.True);
            Assert.That(orsk.Deeds.Single().GuardLine, Is.EqualTo("The legality guard stopped “declare_war kesh”: the target was not offered with this action. It held instead."));
            Assert.That(TurnSummary.For(4, Turn(), Realms, null, Persona, Name).Any(r => r.Guarded), Is.False, "no guard fired, no shield");
        }

        [Test]
        public void Under_fog_a_rivals_army_out_of_sight_is_counted_not_told()
        {
            // Your court sees ashby (where valen_host_1 marches) and not where valen_host_2 holds.
            bool seen(ReplayEvent e) => e.Army == "valen_host_1";
            var valen = TurnSummary.For(4, Turn(), Realms, "kesh", Persona, Name, seen).Single(r => r.Country == "valen");
            Assert.That(valen.Kinds, Is.EqualTo(new[] { ("recruit", 2), ("fortify", 1), ("march", 1), ("hidden", 1) }), "its court's orders are news; an army out of sight is only counted");
            Assert.That(valen.Deeds.Single(x => x.Kind == "hidden").Event, Is.Null, "nothing of it to open");
            var mine = TurnSummary.For(4, Turn(), Realms, "kesh", Persona, Name, e => false).Single(r => r.Mine);
            Assert.That(mine.Kinds.Select(k => k.kind), Has.No.Member("hidden"), "your own armies are always yours to see");
            var watching = TurnSummary.For(4, Turn(), Realms, null, Persona, Name, e => false).Single(r => r.Country == "valen");
            Assert.That(watching.Kinds.Select(k => k.kind), Has.No.Member("hidden"), "watching, nothing is hidden");
        }

        [Test]
        public void On_a_scripted_game_every_deed_is_that_turns_and_that_realms()
        {
            var setup = GameSetup.Writs(3);
            var map = setup.BuildMap();
            var rules = ReplayDirector.WithArmySettings(setup.BuildRules(map), true, true, true, true);
            var g = ReplayBuilder.Build(Recording.Parse(ScriptedWorldGame.Play(setup, map, rules).ToJson()), map, rules);
            var t = g.Turns[2];
            var rows = TurnSummary.For(t.Turn, g.AllEvents, g.CountryIds, null, c => g.Players.TryGetValue(c, out var p) ? p.Persona : null, Name);
            Assert.That(rows.Count, Is.EqualTo(g.CountryIds.Count));
            Assert.That(rows.Sum(r => r.Deeds.Count), Is.GreaterThan(0), "a turn's realms did something");
            foreach (var r in rows) foreach (var d in r.Deeds) Assert.That((d.Event.Turn, d.Event.Country), Is.EqualTo((t.Turn, r.Country)));
            Assert.That(rows.SelectMany(r => r.Deeds).All(d => !string.IsNullOrEmpty(d.Text)), Is.True, "every deed in the chronicle's words");
        }

        [Test]
        public void The_strip_shows_a_row_a_realm_and_a_click_opens_its_card_and_tells_the_map()
        {
            var setup = GameSetup.Writs(3);
            var map = setup.BuildMap();
            var rules = ReplayDirector.WithArmySettings(setup.BuildRules(map), true, true, true, true);
            var g = ReplayBuilder.Build(Recording.Parse(ScriptedWorldGame.Play(setup, map, rules).ToJson()), map, rules);
            var ui = new ScreenUI(new UnityEngine.UIElements.VisualElement(), UnityEngine.ScriptableObject.CreateInstance<UnityEngine.UIElements.StyleSheet>(), null,
                g.CountryIds, g.ProvinceIds, g.Players, "test");
            ui.BuildPanels();
            var t = g.Turns[2];
            (string, int)? pressed = null;
            ui.TurnRowPressed = (c, n) => pressed = (c, n);
            ui.ShowTurnStrip(t.Turn, t.Events, t.End);
            Assert.That(ui.TurnRows.Count, Is.EqualTo(g.CountryIds.Count(c => t.End.Countries[c].Alive)));
            Assert.That(UnityEngine.UIElements.UQueryExtensions.Query(ui.Root, null, "turn-row").ToList().Count, Is.EqualTo(ui.TurnRows.Count));
            string first = ui.TurnRows.First(r => r.Deeds.Count > 0).Country;
            Assert.That(ui.OpenTurnRow(first), Is.True);
            Assert.That(pressed, Is.EqualTo(((string, int)?)(first, t.Turn)), "the map's hook hears the realm and the turn");
            var card = UnityEngine.UIElements.UQueryExtensions.Query(ui.Root, null, "turn-card").ToList();
            Assert.That(card.Count, Is.EqualTo(1));
            Assert.That(UnityEngine.UIElements.UQueryExtensions.Query(card[0], null, "tc-line").ToList().Count, Is.EqualTo(ui.TurnRows.First(r => r.Country == first).Deeds.Count), "a line a decision");
            ui.ShowTurnStrip(g.Turns[3].Turn, g.Turns[3].Events, g.Turns[3].End);
            Assert.That(UnityEngine.UIElements.UQueryExtensions.Query(ui.Root, null, "turn-card").ToList().Count, Is.EqualTo(1), "an open card follows its realm into the next turn");
            ui.CloseTurnCard();
            Assert.That(UnityEngine.UIElements.UQueryExtensions.Query(ui.Root, null, "turn-card").ToList().Count, Is.EqualTo(0));
        }
    }
}

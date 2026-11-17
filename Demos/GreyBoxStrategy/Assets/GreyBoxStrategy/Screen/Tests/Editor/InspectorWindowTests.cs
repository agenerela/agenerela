using System.Collections.Generic;
using System.Linq;
using GreyBoxStrategy.Realm;
using GreyBoxStrategy.Replay;
using NUnit.Framework;

namespace GreyBoxStrategy.Screen.Tests
{
    /// <summary>
    /// Variant B, slice 3: the inspector's card in five tabs. Each tab holds its own part of the decision, the first is
    /// shown when a card opens on a fresh screen, and what a rival's court was told stays private while you rule.
    /// </summary>
    public class InspectorWindowTests
    {
        private static ReplayGame game;
        private static ReplayGame Game()
        {
            if (game != null) return game;
            var setup = GameSetup.Writs(3);
            var map = setup.BuildMap();
            var rules = ReplayDirector.WithArmySettings(setup.BuildRules(map), true, true, true, true);
            return game = ReplayBuilder.Build(Recording.Parse(ScriptedWorldGame.Play(setup, map, rules).ToJson()), map, rules);
        }

        private static ScreenUI Ui(ReplayGame g)
        {
            var ui = new ScreenUI(new UnityEngine.UIElements.VisualElement(), UnityEngine.ScriptableObject.CreateInstance<UnityEngine.UIElements.StyleSheet>(), null,
                g.CountryIds, g.ProvinceIds, new Dictionary<string, RecordedPlayer>(), "test");
            ui.BuildPanels();
            return ui;
        }

        [Test]
        public void A_card_opens_on_its_answer_and_each_tab_holds_its_part()
        {
            var g = Game();
            var e = g.AllEvents.First(x => x.Decision != null && !x.Decision.IsEnvoy);
            var card = DecisionCard.From(e);
            var ui = Ui(g);
            ui.ShowCard(card);
            var (tabs, on) = ui.InspectorTabs();
            Assert.That(tabs, Is.EqualTo(new[] { "Answer", "Offered", "Told", "Guards", "Model" }));
            Assert.That(on, Is.EqualTo("Answer"));
            Assert.That(ui.InspectorText(), Has.Some.StartsWith("1 · WHAT THE MODEL ANSWERED"));
            Assert.That(ui.InspectorText(), Has.None.StartsWith("2 · "), "only the shown tab is built");
            Assert.That(ui.ShowInspectorTab("Offered"), Is.True);
            var offered = ui.InspectorText();
            Assert.That(offered, Has.Some.StartsWith("2 · THE MOVES IT WAS OFFERED"));
            foreach (var m in card.Offered) Assert.That(offered, Has.Some.EqualTo(m), "every move it was offered");
            ui.ShowInspectorTab("Guards");
            Assert.That(ui.InspectorText(), Has.Some.StartsWith("4 · GUARDS").And.Some.StartsWith("5 · MEMORY"));
            foreach (var guard in card.Guards) Assert.That(ui.InspectorText(), Has.Some.EqualTo(guard.Name));
            ui.ShowInspectorTab("Model");
            Assert.That(ui.InspectorText(), Has.Some.StartsWith("6 · MODEL AND TIME"));
            Assert.That(ui.InspectorTabs().on, Is.EqualTo("Model"));
        }

        [Test]
        public void What_a_rivals_court_was_told_stays_private_while_you_rule_and_opens_while_you_watch()
        {
            var g = Game();
            var e = g.AllEvents.First(x => x.Decision != null && !x.Decision.IsEnvoy && x.Decision.AgentId != g.CountryIds[0]);
            var card = DecisionCard.From(e);
            var watch = Ui(g);
            watch.ShowCard(card);
            watch.ShowInspectorTab("Told");
            Assert.That(watch.InspectorText(), Has.Some.EqualTo("Standing facts (IObservationSource)"), "watching: what it was told");
            var rule = Ui(g);
            rule.Fog = new Fog(g.CountryIds[0], new RealmQueries(g, g.Map, g.Rules));
            rule.ShowCard(card);
            rule.ShowInspectorTab("Told");
            var told = rule.InspectorText();
            Assert.That(told, Has.None.EqualTo("Standing facts (IObservationSource)"), "ruling: a rival's court stays private");
            Assert.That(told, Has.Some.Contains("stays private while the game runs"));
        }
    }
}

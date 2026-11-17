using System.Collections.Generic;
using System.Linq;
using GreyBoxStrategy.Realm;
using GreyBoxStrategy.Replay;
using NUnit.Framework;

namespace GreyBoxStrategy.Screen.Tests
{
    /// <summary>
    /// Variant B's screen logic, without a scene: the top bar's numbers and their breakdowns, the outliner's rows, and the
    /// chronicle feed's filters, each against a scripted writs game with all four army rules on.
    /// </summary>
    public class ScreenModelsTests
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

        private static IEnumerable<ReplayState> Ends(ReplayGame g) => g.Turns.Select(t => t.Events.Count > 0 ? t.Events.Last().After : t.Council);
        private static string Name(string id) => id == null ? "" : char.ToUpperInvariant(id[0]) + id.Substring(1);

        [Test]
        public void The_top_bar_shows_the_rules_numbers_and_breaks_them_down()
        {
            var g = Game();
            foreach (var s in Ends(g).Take(20))
                foreach (var me in g.CountryIds.Where(c => s.Countries[c].Alive))
                {
                    var c = s.Countries[me];
                    var stats = ScreenModels.TopBar(s, me, 10, true, 25, (3, 1), CountryLook.NameOf, Name).ToDictionary(x => x.Key);
                    Assert.That(stats.Keys, Is.EqualTo(new[] { "gold", "army", "land", "writ", "stab", "war", "truce" }), "every number, in the bar's order");
                    Assert.That(stats["gold"].Value, Is.EqualTo(c.Gold.ToString()));
                    int net = c.Income - c.Upkeep;
                    Assert.That(stats["gold"].Tone, Is.EqualTo(System.Math.Sign(net)), "a falling treasury is red, a rising one green");
                    Assert.That(stats["gold"].Tip, Does.Contain($"Income +{c.Income}").And.Contain($"Upkeep -{c.Upkeep}"));
                    var mine = s.Armies.Where(a => a.Owner == me).ToList();
                    Assert.That(stats["army"].Value, Is.EqualTo(c.Army.ToString()));
                    foreach (var a in mine) Assert.That(stats["army"].Tip, Does.Contain(Chronicle.Host(a.Id)), "each army in the breakdown");
                    Assert.That(stats["land"].Value, Is.EqualTo(s.ProvinceCount(me).ToString()));
                    Assert.That(stats["writ"].Value, Is.EqualTo("2"), "3 writs, 1 used: 2 left");
                    int wars = s.Relations.Count(r => r.Kind == "war" && (r.A == me || r.B == me));
                    Assert.That(stats["war"].Value, Is.EqualTo(wars.ToString()));
                    foreach (var r in s.Relations.Where(r => r.Kind == "war" && (r.A == me || r.B == me)))
                    {
                        string o = r.A == me ? r.B : r.A;
                        Assert.That(stats["war"].Tip, Does.Contain($"{CountryLook.NameOf(o)}: {s.WarScore(me, o):+0;-0;0}"), "each war's score from your side");
                    }
                }
            Assert.That(ScreenModels.TopBar(Ends(g).First(), g.CountryIds[0], 10, true, 25, null, CountryLook.NameOf, Name).Any(x => x.Key == "writ"), Is.False,
                "outside your council there are no writs to count");
        }

        [Test]
        public void The_outliner_lists_your_armies_sieges_and_wars_the_most_pressing_first()
        {
            var g = Game();
            int armies = 0, sieges = 0, wars = 0, hungryFirst = 0;
            foreach (var s in Ends(g))
                foreach (var me in g.CountryIds.Where(c => s.Countries[c].Alive))
                {
                    var o = ScreenModels.OutlinerOf(s, me, g.ProvinceIds, Name);
                    var mine = s.Armies.Where(a => a.Owner == me).ToList();
                    Assert.That(o.Armies.Select(a => a.Id).OrderBy(x => x), Is.EqualTo(mine.Select(a => a.Id).OrderBy(x => x)), "every army of yours, no other");
                    foreach (var r in o.Armies)
                    {
                        var a = s.Army(r.Id);
                        Assert.That(r.Soldiers, Is.EqualTo(a.Soldiers));
                        Assert.That(r.Full * 2 + r.Half, Is.EqualTo(a.HalfStores), $"{r.Id}: loaves are its stores");
                        Assert.That(r.Loaves, Is.EqualTo(a.MaxHalfStores / 2));
                        Assert.That(r.Hungry, Is.EqualTo(a.Hungry));
                    }
                    if (o.Armies.Any(a => a.Hungry) && o.Armies.Any(a => !a.Hungry)) { Assert.That(o.Armies[0].Hungry, Is.True, "a hungry army first"); hungryFirst++; }
                    for (int i = 0; i < s.Owners.Length; i++)
                    {
                        bool ours = s.SiegedBy?[i] == me || (s.SiegedBy?[i] != null && s.Controller(i) == me);
                        Assert.That(o.Sieges.Any(x => x.Province == i), Is.EqualTo(ours), $"province {g.ProvinceIds[i]}: a siege you lay or suffer, and no other");
                    }
                    foreach (var x in o.Sieges) Assert.That(x.Side, Is.EqualTo(x.Besieger == me ? 1 : -1));
                    var expected = s.Relations.Where(r => r.Kind == "war" && (r.A == me || r.B == me)).Select(r => r.A == me ? r.B : r.A).OrderBy(x => x);
                    Assert.That(o.Wars.Select(w => w.Other).OrderBy(x => x), Is.EqualTo(expected));
                    foreach (var w in o.Wars) Assert.That(w.Score, Is.EqualTo(s.WarScore(me, w.Other)), "the score from your side");
                    Assert.That(o.Wars.Select(w => System.Math.Abs(w.Score)), Is.Ordered.Descending, "the sharpest war first");
                    armies += o.Armies.Count; sieges += o.Sieges.Count; wars += o.Wars.Count;
                }
            Assert.That(armies, Is.GreaterThan(50));
            Assert.That(sieges, Is.GreaterThan(0), "a siege shows somewhere in the game");
            Assert.That(wars, Is.GreaterThan(0));
            // Watch mode: every siege and every war.
            var last = Ends(g).Last();
            var all = ScreenModels.OutlinerOf(last, null, g.ProvinceIds, Name);
            Assert.That(all.Armies, Is.Empty, "no realm, no armies of yours");
            Assert.That(all.Wars.Count, Is.EqualTo(last.Relations.Count(r => r.Kind == "war")));
        }

        [Test]
        public void The_feed_filters_keep_what_their_names_say()
        {
            var g = Game();
            var lines = g.AllEvents.Select(e => Chronicle.Line(e, CountryLook.NameOf)).Where(l => l != null).ToList();
            string me = g.CountryIds[0];
            Assert.That(lines.All(l => ScreenModels.Keeps(l, ScreenModels.Feed.All, me)), Is.True);
            var mine = lines.Where(l => ScreenModels.Keeps(l, ScreenModels.Feed.Me, me)).ToList();
            Assert.That(mine, Is.Not.Empty);
            Assert.That(mine.All(l => l.Country == me || l.Event.Other == me), Is.True, "Me: yours, or done to you");
            Assert.That(lines.Where(l => l.Country != me && l.Event.Other != me).Any(l => ScreenModels.Keeps(l, ScreenModels.Feed.Me, me)), Is.False);
            var doneToMe = lines.Where(l => l.Country != me && l.Event.Other == me).ToList();
            Assert.That(doneToMe, Is.Not.Empty, "something was done to you in this game");
            Assert.That(doneToMe.All(l => ScreenModels.Keeps(l, ScreenModels.Feed.Me, me)), Is.True, "Me keeps what others did to you");
            Assert.That(lines.Where(l => ScreenModels.Keeps(l, ScreenModels.Feed.War, me)).All(l => l.Category == ChronicleCategory.War || l.Category == ChronicleCategory.Refused), Is.True);
            Assert.That(lines.Where(l => ScreenModels.Keeps(l, ScreenModels.Feed.Land, me)).All(l => l.Category == ChronicleCategory.Land || l.Category == ChronicleCategory.Peace), Is.True);
            Assert.That(lines.Any(l => ScreenModels.Keeps(l, ScreenModels.Feed.War, me)) && lines.Any(l => ScreenModels.Keeps(l, ScreenModels.Feed.Land, me)), Is.True);
            Assert.That(lines.Any(l => ScreenModels.Keeps(l, ScreenModels.Feed.Me, null)), Is.False, "watch mode has no Me");
        }

        [Test]
        public void The_feed_offers_Me_exactly_when_you_rule_a_realm()
        {
            var g = Game();
            var ui = new ScreenUI(new UnityEngine.UIElements.VisualElement(), UnityEngine.ScriptableObject.CreateInstance<UnityEngine.UIElements.StyleSheet>(), null,
                g.CountryIds, g.ProvinceIds, new Dictionary<string, RecordedPlayer>(), "test");
            ui.BuildPanels();
            var s = Ends(g).First();
            ui.RenderOutliner(s);
            Assert.That(ui.FeedFilters(), Is.EqualTo(new[] { "All", "War", "Land" }), "watching: no Me");
            ui.Fog = new Fog(g.CountryIds[0], new RealmQueries(g, g.Map, g.Rules));
            ui.RenderOutliner(s);
            Assert.That(ui.FeedFilters(), Is.EqualTo(new[] { "All", "Me", "War", "Land" }), "ruling a realm: Me, second");
            ui.Fog = new Fog(null, null);
            ui.RenderOutliner(s);
            Assert.That(ui.FeedFilters(), Does.Not.Contain("Me"), "back to watching: Me goes");
        }
    }
}

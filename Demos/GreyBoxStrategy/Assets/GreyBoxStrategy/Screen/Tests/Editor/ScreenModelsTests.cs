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

        [Test]
        public void An_armys_rows_show_yours_in_full_and_a_rivals_as_your_court_sees_it()
        {
            var g = Game();
            int own = 0, rivals = 0, traits = 0;
            foreach (var s in Ends(g).Take(25))
                foreach (var a in s.Armies)
                {
                    var mine = ScreenModels.ArmyRows(a, false, "holding").ToDictionary(r => r.Key);
                    var seen = ScreenModels.ArmyRows(a, true, "holding").ToDictionary(r => r.Key);
                    Assert.That(mine["soldiers"].Value, Is.EqualTo(a.Soldiers.ToString()));
                    var types = mine.Values.Where(r => r.Key.StartsWith("type-")).ToList();
                    Assert.That(types.Count, Is.EqualTo(3), "pikes, riders and bows, each a bar");
                    Assert.That(types.Sum(r => r.Amount), Is.EqualTo(a.Soldiers), $"{a.Id}: the bars add up to its soldiers");
                    Assert.That(types.All(r => r.Max == System.Math.Max(1, a.Soldiers)), Is.True);
                    Assert.That(mine["stores"].Pips * 2, Is.EqualTo(a.MaxHalfStores));
                    Assert.That(mine["stores"].Amount * 2 + mine["stores"].Half, Is.EqualTo(a.HalfStores));
                    Assert.That(mine.Keys.Count(k => k.StartsWith("trait-")), Is.EqualTo(a.General == null ? 0 : a.Traits.Count));
                    traits += a.Traits.Count;
                    // A rival's: its size rounded to ten, what it mostly is, no count of each type, no stores.
                    Assert.That(seen.Keys.Any(k => k.StartsWith("type-") || k == "stores"), Is.False, $"{a.Id}: no rival's counts or stores");
                    Assert.That(seen["soldiers"].Value, Is.EqualTo(ArmySight.About(a.Soldiers)), "the court's own rounding");
                    Assert.That(seen["mostly"].Value, Is.EqualTo(ArmyLook.Mostly(a)));
                    own++; rivals++;
                }
            Assert.That(own, Is.GreaterThan(50));
            Assert.That(traits, Is.GreaterThan(0), "generals with traits in the game");
        }

        [Test]
        public void A_provinces_rows_show_its_holding_and_hide_what_your_court_does_not_know()
        {
            var g = Game();
            var q = new RealmQueries(g, g.Map, g.Rules);
            int sieged = 0, capitals = 0;
            foreach (var s in Ends(g).Take(40))
                for (int p = 0; p < s.Owners.Length; p++)
                {
                    var known = ScreenModels.ProvinceRows(s, p, q, true, CountryLook.NameOf).ToDictionary(r => r.Key);
                    var hidden = ScreenModels.ProvinceRows(s, p, q, false, CountryLook.NameOf).ToDictionary(r => r.Key);
                    Assert.That(known["forts"].Amount, Is.EqualTo(s.Forts[p]), "a pip a fort");
                    Assert.That(known["forts"].Pips, Is.GreaterThanOrEqualTo(s.Forts[p]));
                    Assert.That(known["defence"].Value, Is.EqualTo(q.Defence(s, p).ToString()));
                    Assert.That(hidden["forts"].Value, Is.EqualTo("not known"));
                    Assert.That(hidden["forts"].Pips, Is.EqualTo(0), "no pips to count what is not known");
                    Assert.That(hidden["defence"].Value, Is.EqualTo("not known"));
                    if (s.Owners[p] == null) Assert.That(hidden["garrison"].Value, Is.EqualTo("not known"));
                    Assert.That(known.ContainsKey("capital"), Is.EqualTo(q.IsCapital(p)));
                    Assert.That(known.ContainsKey("occupier"), Is.EqualTo(s.Controller(p) != s.Owners[p]));
                    Assert.That(known.ContainsKey("siege"), Is.EqualTo(s.SiegedBy?[p] != null));
                    if (known.ContainsKey("siege")) sieged++;
                    if (known.ContainsKey("capital")) capitals++;
                }
            Assert.That(sieged, Is.GreaterThan(0));
            Assert.That(capitals, Is.GreaterThan(0));
        }

        [Test]
        public void A_realms_rows_show_its_treasury_only_to_its_own_court_and_the_war_score_from_your_side()
        {
            var g = Game();
            int wars = 0;
            foreach (var s in Ends(g))
                foreach (var you in g.CountryIds.Where(c => s.Countries[c].Alive))
                    foreach (var id in g.CountryIds.Where(c => c != you))
                    {
                        var rows = ScreenModels.RealmRows(s, id, you, false, CountryLook.NameOf).ToDictionary(r => r.Key);
                        Assert.That(rows["gold"].Value, Is.EqualTo("not known"));
                        Assert.That(rows.ContainsKey("stab"), Is.False);
                        Assert.That(rows["army"].Value, Is.EqualTo(s.Countries[id].Army.ToString()), "a ruler is told every rival's army");
                        bool war = s.Relation(you, id)?.Kind == "war";
                        Assert.That(rows.ContainsKey("score"), Is.EqualTo(war));
                        if (war) { Assert.That(rows["score"].Value, Is.EqualTo($"{s.WarScore(you, id):+0;-0;0}")); wars++; }
                        var own = ScreenModels.RealmRows(s, id, you, true, CountryLook.NameOf).ToDictionary(r => r.Key);
                        Assert.That(own["gold"].Value, Does.StartWith(s.Countries[id].Gold.ToString()));
                        Assert.That(own["stab"].Amount, Is.EqualTo(s.Countries[id].Stability));
                    }
            Assert.That(wars, Is.GreaterThan(0));
        }

        [Test]
        public void The_outliner_folds_a_section_keeping_its_count_and_keeps_the_ledgers_at_its_foot()
        {
            var g = Game();
            var s = Ends(g).First(x => x.Armies.Count(a => a.Owner == g.CountryIds[0]) >= 2);
            var ui = new ScreenUI(new UnityEngine.UIElements.VisualElement(), UnityEngine.ScriptableObject.CreateInstance<UnityEngine.UIElements.StyleSheet>(), null,
                g.CountryIds, g.ProvinceIds, new Dictionary<string, RecordedPlayer>(), "test");
            ui.BuildPanels();
            ui.Fog = new Fog(g.CountryIds[0], new RealmQueries(g, g.Map, g.Rules));
            ui.ShowState(s);
            int armies = s.Armies.Count(a => a.Owner == g.CountryIds[0]);
            var open = ui.OutlinerShape();
            Assert.That(open.foot, Is.EqualTo(new[] { "Realms", "Diplomacy" }), "the ledgers at the foot, outside the scroll");
            Assert.That(open.heads.Any(h => h.Contains("ARMIES") && h.Contains(armies.ToString())), Is.True, string.Join(" | ", open.heads));
            ui.FoldOutliner("ARMIES");
            var shut = ui.OutlinerShape();
            Assert.That(shut.rows, Is.EqualTo(open.rows - armies), "folded: its rows go");
            Assert.That(shut.heads.Any(h => h.Contains("ARMIES") && h.Contains(armies.ToString())), Is.True, "folded: its count stays");
            Assert.That(shut.foot, Is.EqualTo(open.foot));
            ui.FoldOutliner("ARMIES");
            Assert.That(ui.OutlinerShape().rows, Is.EqualTo(open.rows), "opened again");
        }
    }
}

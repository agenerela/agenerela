using System.Collections.Generic;
using System.Linq;
using GreyBoxStrategy.Realm;
using GreyBoxStrategy.Replay;
using NUnit.Framework;

namespace GreyBoxStrategy.Screen.Tests
{
    /// <summary>
    /// R3's sieges and supply on screen: New game's two switches reach the rules of every writs game; with both off
    /// the screen has nothing new to show; with both on the replay plays sieges and hunger back as the game did, a
    /// besieged province carries its tag and every army its stores.
    /// </summary>
    public class SiegesSupplyScreenTests
    {
        private static ReplayGame Play(bool sieges, bool supply, int seed)
        {
            var setup = GameSetup.Writs(seed);
            var map = setup.BuildMap();
            var rules = ReplayDirector.WithArmySettings(setup.BuildRules(map), false, false, sieges, supply);
            return ReplayBuilder.Build(Recording.Parse(ScriptedWorldGame.Play(setup, map, rules).ToJson()), map, rules);
        }

        private static List<ReplayState> States(ReplayGame g) =>
            g.Turns.SelectMany(t => new[] { t.Start, t.Council }.Concat(t.Events.Select(e => e.After))).Where(s => s != null).Distinct().ToList();

        [TearDown] public void Reset() { ReplayDirector.NextSieges = false; ReplayDirector.NextSupply = false; }

        [Test]
        public void New_games_sieges_and_supply_reach_the_rules_of_every_writs_game_and_only_those()
        {
            foreach (var setup in new[] { GameSetup.Writs(5), GameSetup.Writs(5, GameSetup.ClassicMapId, GameSetup.DominationPreset) })
            {
                var map = setup.BuildMap();
                var off = ReplayDirector.RulesFor(setup, map);
                Assert.That(off.Sieges || off.Supply, Is.False, "off by default");
                ReplayDirector.NextSieges = true;
                var sieges = ReplayDirector.RulesFor(setup, map);
                Assert.That((sieges.Sieges, sieges.Supply), Is.EqualTo((true, false)));
                ReplayDirector.NextSupply = true;
                var both = ReplayDirector.RulesFor(setup, map);
                Assert.That((both.Sieges, both.Supply), Is.EqualTo((true, true)));
                ReplayDirector.NextSieges = false;
                var supply = ReplayDirector.RulesFor(setup, map);
                Assert.That((supply.Sieges, supply.Supply), Is.EqualTo((false, true)));
                Assert.That(supply.UnitTypes || supply.Generals, Is.False, "the other settings stay as they were");
                Assert.That(GameRules.Standard.Sieges || GameRules.Standard.Supply, Is.False, "the standard rules are not changed");
                ReplayDirector.NextSupply = false;
            }
            var classic = GameSetup.Classic(5);
            ReplayDirector.NextSieges = true; ReplayDirector.NextSupply = true;
            var one = ReplayDirector.RulesFor(classic, classic.BuildMap());
            Assert.That(one.Sieges || one.Supply, Is.False, "the one-move game's rules stay as they are");
        }

        [Test]
        public void With_both_off_the_screen_shows_no_siege_and_no_stores()
        {
            var g = Play(false, false, 3);
            var states = States(g);
            Assert.That(states.All(s => s.SiegedBy == null && s.SiegeTurns == null && s.SiegeNeeded == null), Is.True, "no siege anywhere");
            Assert.That(states.SelectMany(s => Enumerable.Range(0, s.Owners.Length).Select(i => ArmyLook.SiegeTag(s, i))).All(t => t == null), Is.True);
            var armies = states.SelectMany(s => s.Armies).ToList();
            Assert.That(armies.Count, Is.GreaterThan(100));
            Assert.That(armies.All(a => !a.HasStores && !a.Hungry && ArmyLook.Stores(a) == null), Is.True, "no stores");
            Assert.That(armies.All(a => ArmyLook.TooltipLines(a, rival: false).Count == 0), Is.True, "every tooltip as before");
            Assert.That(g.AllEvents.Any(e => e.Kind == ReplayEventKind.Siege || e.Kind == ReplayEventKind.SiegeTaken || e.Kind == ReplayEventKind.Hunger), Is.False,
                "no siege or hunger lines in the chronicle");
        }

        [Test]
        public void With_both_on_sieges_and_hunger_play_back_as_the_game_did()
        {
            var games = Enumerable.Range(1, 4).Select(seed => Play(true, true, seed)).ToList();
            var events = games.SelectMany(g => g.AllEvents).ToList();
            Assert.That(events.Count(e => e.Kind == ReplayEventKind.Siege), Is.GreaterThan(0), "a siege begins");
            var taken = events.Where(e => e.Kind == ReplayEventKind.SiegeTaken).ToList();
            Assert.That(taken, Is.Not.Empty, "a siege ends with the province taken");
            foreach (var e in taken)
            {
                Assert.That(e.After.Occupiers[e.ProvinceIndex], Is.EqualTo(e.Country), $"turn {e.Turn}: {e.Text}");
                Assert.That(ArmyLook.SiegeTag(e.After, e.ProvinceIndex), Is.Null, "a siege that ended has no tag");
            }
            // Every turn's end: the tag says the game's own turns of the siege, and never more than it needs.
            int tagged = 0;
            foreach (var g in games)
                foreach (var t in g.Turns)
                {
                    var end = t.Events.Count > 0 ? t.Events.Last().After : t.Council;
                    for (int i = 0; i < end.Owners.Length; i++)
                    {
                        if (end.SiegedBy[i] == null) { Assert.That(ArmyLook.SiegeTag(end, i), Is.Null); continue; }
                        tagged++;
                        Assert.That(end.SiegeTurns[i], Is.InRange(1, end.SiegeNeeded[i]), $"turn {t.Turn}: {g.ProvinceIds[i]}");
                        Assert.That(ArmyLook.SiegeTag(end, i), Is.EqualTo($"Siege {end.SiegeTurns[i]} of {end.SiegeNeeded[i]}"));
                        Assert.That(end.Armies.Any(a => a.Location == g.ProvinceIds[i] && a.Owner == end.SiegedBy[i]), Is.True, "its besiegers stand there");
                    }
                }
            Assert.That(tagged, Is.GreaterThan(0), "a siege lasts past a turn's end");

            // Every army at every moment has its stores; one the rules starved shows none, and the warning.
            var armies = games.SelectMany(States).SelectMany(s => s.Armies).ToList();
            Assert.That(armies.All(a => a.HasStores && a.HalfStores <= a.MaxHalfStores && a.MaxHalfStores == 8), Is.True,
                string.Join("; ", armies.Where(a => !a.HasStores || a.HalfStores > a.MaxHalfStores || a.MaxHalfStores != 8).Take(3).Select(a => $"{a.Id} {a.HalfStores}/{a.MaxHalfStores}")));
            Assert.That(armies.Any(a => a.HalfStores < a.MaxHalfStores), Is.True, "stores are eaten abroad");
            var hunger = events.Where(e => e.Kind == ReplayEventKind.Hunger && !e.Text.Contains("starved")).ToList();
            Assert.That(hunger, Is.Not.Empty, "an army goes hungry");
            foreach (var e in hunger)
            {
                var a = e.After.Army(e.Army);
                if (a == null) continue;
                Assert.That(a.Hungry, Is.True, $"turn {e.Turn}: {e.Text}");
                Assert.That(ArmyLook.Stores(a), Does.StartWith("No stores"));
                Assert.That(ArmyLook.TooltipLines(a, rival: false), Has.Some.StartsWith("No stores"));
                Assert.That(ArmyLook.TooltipLines(a, rival: true), Has.None.StartsWith("No stores"), "a rival's stores are not yours to know");
            }
        }
    }
}

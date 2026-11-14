using System.Linq;
using GreyBoxStrategy.Realm;
using GreyBoxStrategy.Replay;
using NUnit.Framework;

namespace GreyBoxStrategy.Screen.Tests
{
    /// <summary>
    /// R3 on screen: New game's unit types and generals reach the rules of every writs game, and with both off the
    /// screen has nothing new to show: no mix, no general, no general's line, the same names as before.
    /// </summary>
    public class ArmiesScreenTests
    {
        private static ReplayGame Play(bool units, bool generals, int seed = 3)
        {
            var setup = GameSetup.Writs(seed);
            var map = setup.BuildMap();
            var rules = ReplayDirector.WithArmySettings(setup.BuildRules(map), units, generals);
            return ReplayBuilder.Build(Recording.Parse(ScriptedWorldGame.Play(setup, map, rules).ToJson()), map, rules);
        }

        [TearDown] public void Reset() { ReplayDirector.NextUnitTypes = false; ReplayDirector.NextGenerals = false; }

        [Test]
        public void New_games_switches_reach_the_rules_of_every_writs_game_and_only_those()
        {
            foreach (var setup in new[] { GameSetup.Writs(5), GameSetup.Writs(5, GameSetup.ClassicMapId, GameSetup.DominationPreset) })
            {
                var map = setup.BuildMap();
                ReplayDirector.NextUnitTypes = false; ReplayDirector.NextGenerals = false;
                var off = ReplayDirector.RulesFor(setup, map);
                Assert.That(off.UnitTypes || off.Generals, Is.False, "off by default");
                ReplayDirector.NextUnitTypes = true;
                var units = ReplayDirector.RulesFor(setup, map);
                Assert.That((units.UnitTypes, units.Generals), Is.EqualTo((true, false)));
                ReplayDirector.NextGenerals = true;
                var both = ReplayDirector.RulesFor(setup, map);
                Assert.That((both.UnitTypes, both.Generals), Is.EqualTo((true, true)));
                ReplayDirector.NextUnitTypes = false;
                var gens = ReplayDirector.RulesFor(setup, map);
                Assert.That((gens.UnitTypes, gens.Generals), Is.EqualTo((false, true)));
                Assert.That(GameRules.Standard.UnitTypes || GameRules.Standard.Generals, Is.False, "the standard rules are not changed");
            }
            // The one-move game has no armies to type: its rules stay as they are.
            var classic = GameSetup.Classic(5);
            ReplayDirector.NextUnitTypes = true; ReplayDirector.NextGenerals = true;
            var one = ReplayDirector.RulesFor(classic, classic.BuildMap());
            Assert.That(one.UnitTypes || one.Generals, Is.False);
        }

        [Test]
        public void With_both_switches_off_the_screen_has_nothing_new_to_show()
        {
            var g = Play(false, false);
            var states = g.Turns.SelectMany(t => new[] { t.Start, t.Council }.Concat(t.Events.Select(e => e.After))).Where(s => s != null).ToList();
            Assert.That(states.SelectMany(s => s.Armies).Count(), Is.GreaterThan(100));
            foreach (var a in states.SelectMany(s => s.Armies))
            {
                Assert.That(a.HasMix || a.General != null || a.Traits.Count > 0, Is.False, $"{a.Id}: no mix, no general");
                Assert.That(ArmyLook.Largest(a), Is.Null);
                Assert.That(ArmyLook.TooltipLines(a, rival: false), Is.Empty, "its tooltip as before");
                Assert.That(ArmyLook.TooltipLines(a, rival: true), Is.Empty);
            }
            Assert.That(g.AllEvents.Any(e => e.Kind == ReplayEventKind.General), Is.False, "no general's lines in the chronicle");
            var battles = g.AllEvents.Where(e => e.Kind == ReplayEventKind.Battle).Select(e => e.Battle).ToList();
            Assert.That(battles.Count, Is.GreaterThan(10));
            Assert.That(battles.All(b => b.AttackCounter == 1 && b.DefenceCounter == 1 && b.AttackGeneral == 1 && b.DefenceGeneral == 1 && b.CounterNote == null && b.GeneralNote == null), Is.True,
                "the battle report shows no counter and no general");
            Assert.That(ScreenUI.Describe(new Move(Actions.Recruit)), Is.EqualTo("Recruit"));
        }

        [Test]
        public void With_both_on_every_moment_shows_each_armys_mix_and_general()
        {
            var g = Play(true, true);
            var states = g.Turns.SelectMany(t => new[] { t.Council }.Concat(t.Events.Select(e => e.After))).Where(s => s != null).Distinct().ToList();
            var armies = states.SelectMany(s => s.Armies).ToList();
            Assert.That(armies.Count, Is.GreaterThan(100));
            // An army raised and lost within one turn is in no turn-end state: the rules' snapshots never name its general.
            var known = new System.Collections.Generic.HashSet<string>(g.Turns.SelectMany(t => t.Start.Armies.Concat((t.Events.Count > 0 ? t.Events.Last().After : t.Council).Armies)).Select(a => a.Id));
            foreach (var a in armies)
            {
                Assert.That(a.Pikes + a.Riders + a.Bows, Is.EqualTo(a.Soldiers), $"{a.Id}: its mix adds up to its soldiers");
                if (!known.Contains(a.Id)) { Assert.That(a.General, Is.Null, $"{a.Id}: no general rather than a made-up one"); continue; }
                Assert.That(a.General, Is.Not.Null, $"{a.Id} has a general");
                Assert.That(ArmyLook.Leader(a), Does.StartWith(a.General + " the "));
            }
            Assert.That(armies.Any(a => a.Riders > 0) && armies.Any(a => a.Bows > 0), Is.True, "the scripted rulers raise more than pikes");
            Assert.That(armies.Select(a => ArmyLook.Mostly(a)).Where(m => m != null).Distinct().Count(), Is.GreaterThan(1));
            var countered = g.AllEvents.Where(e => e.Kind == ReplayEventKind.Battle && e.Battle.CounterNote != null).ToList();
            Assert.That(countered, Is.Not.Empty);
            // The report's mix agrees with its counter: "x's bows broke y's pikes" has bows on one side and pikes on the other.
            foreach (var e in countered.Where(e => e.AttackMix != null && e.DefenceMix != null))
            {
                var m = System.Text.RegularExpressions.Regex.Match(e.Battle.CounterNote, @"'s (\w+) broke .*?'s (\w+)");
                Assert.That(m.Success, Is.True, e.Battle.CounterNote);
                string mixes = e.AttackMix + " | " + e.DefenceMix;
                Assert.That(mixes, Does.Contain(m.Groups[1].Value).And.Contain(m.Groups[2].Value), $"turn {e.Turn}: {e.Battle.CounterNote} against {mixes}");
            }
            Assert.That(countered.Count(e => e.AttackMix != null && e.DefenceMix != null), Is.GreaterThan(countered.Count / 2), "most reports know both sides' mix");
            Assert.That(g.AllEvents.Count(e => e.Kind == ReplayEventKind.General), Is.GreaterThan(0), "the chronicle tells a general's trait or fall");
            Assert.That(ScreenUI.Describe(new Move(Actions.RecruitRiders)), Is.EqualTo("Recruit riders"), "an advisor's suggestion names the type");
            // A typed recruit is a recruit on screen, with its army where it was raised, never a hold.
            var raised = g.AllEvents.Where(e => e.Text != null && e.Text.StartsWith("raised ") && !e.Text.StartsWith("raised taxes")).ToList();
            Assert.That(raised, Is.Not.Empty);
            Assert.That(raised.All(e => e.Kind == ReplayEventKind.Recruit && e.ProvinceIndex >= 0), Is.True, string.Join("; ", raised.Where(e => e.Kind != ReplayEventKind.Recruit).Select(e => e.Kind + ": " + e.Text).Take(3)));
        }
    }
}

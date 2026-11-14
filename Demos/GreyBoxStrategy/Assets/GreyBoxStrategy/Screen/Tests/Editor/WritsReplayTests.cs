using System.Collections.Generic;
using System.Linq;
using GreyBoxStrategy.Replay;
using NUnit.Framework;

namespace GreyBoxStrategy.Screen.Tests
{
    /// <summary>
    /// The writs turn played back (rules v2, R1, the screen's half): a scripted writs game rebuilt
    /// from its recording, with every army's piece, battle, occupation and treaty where the rules put them.
    /// </summary>
    public class WritsReplayTests
    {
        private static ReplayGame game;
        private static MapDefinition map;

        [OneTimeSetUp]
        public void Make()
        {
            var setup = GameSetup.Writs(3);
            map = setup.BuildMap();
            var rules = setup.BuildRules(map);
            var rec = ScriptedWorldGame.Play(setup, map, rules);
            // Through JSON, as a saved game is: the army each order is for must survive the round trip.
            game = ReplayBuilder.Build(Recording.Parse(rec.ToJson()), map, rules);
        }

        [Test]
        public void A_writs_game_replays_to_its_end_with_every_piece_where_the_rules_put_it()
        {
            Assert.That(game.Writs, Is.True);
            Assert.That(game.Result, Is.Not.Null);
            // Build throws when an owner or an occupier differs from the game's; the pieces are checked here.
            Assert.That(game.Turns.Sum(t => t.ArmyDrift), Is.EqualTo(0), "every army stands where the rules left it at each turn's end");
            Assert.That(game.Turns.Sum(t => t.End.Armies.Count), Is.GreaterThan(500));
            Assert.That(game.AllEvents.Count(e => e.Kind == ReplayEventKind.ArmyOrder), Is.GreaterThan(0));
            Assert.That(game.AllEvents.Where(e => e.Kind == ReplayEventKind.ArmyOrder).All(e => e.Decision != null && e.Decision.ArmyId == e.Army), Is.True,
                "an army's order carries the marshal's decision for that army");
        }

        [Test]
        public void Each_battle_is_shown_once_with_its_record_and_the_loser_falls_back()
        {
            var battles = game.AllEvents.Where(e => e.Kind == ReplayEventKind.Battle).ToList();
            Assert.That(battles.Count, Is.GreaterThan(20));
            Assert.That(battles.All(b => b.Battle != null && b.Battle.Why != null), Is.True);
            Assert.That(battles.Select(b => b.Battle).Distinct().Count(), Is.EqualTo(battles.Count), "each record once");
            foreach (var b in battles)
                foreach (var kv in b.Battle.Retreats)
                {
                    var a = b.After.Army(kv.Key);
                    if (a != null) Assert.That(a.Location, Is.EqualTo(kv.Value), $"turn {b.Turn}: {kv.Key} fell back to {kv.Value}");
                }
        }

        [Test]
        public void An_army_marches_from_province_to_bordering_province()
        {
            var routes = game.AllEvents.Where(e => e.Kind == ReplayEventKind.March).SelectMany(e => e.Routes).ToList();
            Assert.That(routes.Count(r => r.Steps.Count > 0), Is.GreaterThan(50));
            Assert.That(routes.Count(r => r.Steps.Count > 1), Is.GreaterThan(0), "two steps over open ground");
            foreach (var r in routes)
            {
                string at = r.From;
                foreach (string step in r.Steps)
                {
                    Assert.That(map.AreNeighbours(at, step), Is.True, $"{r.Army}: {at} to {step}");
                    at = step;
                }
                if (r.Towards != null) Assert.That(map.AreNeighbours(at, r.Towards), Is.True, $"{r.Army} waits on the {at}-{r.Towards} border");
            }
        }

        [Test]
        public void Occupation_hatches_without_changing_the_owner_and_a_treaty_settles_it()
        {
            var occupy = game.AllEvents.First(e => e.Kind == ReplayEventKind.Occupy);
            Assert.That(occupy.After.Occupiers[occupy.ProvinceIndex], Is.EqualTo(occupy.Country));
            Assert.That(occupy.After.Owners[occupy.ProvinceIndex], Is.EqualTo(occupy.Other), "the owner stays");

            var treaties = game.AllEvents.Where(e => e.Kind == ReplayEventKind.Treaty).ToList();
            Assert.That(treaties.Count, Is.GreaterThan(0));
            foreach (var t in treaties)
            {
                foreach (string p in t.Ceded.Concat(t.Returned))
                    Assert.That(t.After.Occupiers[game.IndexOf(p)], Is.Null, $"turn {t.Turn}: {p} is no longer occupied");
                string gains = t.Term == Actions.CedeOccupied ? t.Other : t.Country;
                foreach (string p in t.Ceded)
                    Assert.That(t.After.Owners[game.IndexOf(p)], Is.EqualTo(gains), $"turn {t.Turn}: {p} goes to {gains}");
                Assert.That(t.After.Relation(t.Country, t.Other)?.Kind, Is.EqualTo("truce"));
            }
            Assert.That(treaties.Any(t => t.Ceded.Count > 0), Is.True, "land changed hands by treaty");
        }

        [Test]
        public void A_war_carries_its_score_from_both_sides()
        {
            var atWar = game.Turns.Select(t => t.Start).First(s => s.Relations.Any(r => r.Kind == "war") && s.WarScores.Values.Any(v => v != 0));
            var war = atWar.Relations.First(r => r.Kind == "war" && atWar.WarScore(r.A, r.B) != 0);
            Assert.That(atWar.WarScore(war.B, war.A), Is.EqualTo(-atWar.WarScore(war.A, war.B)));
            Assert.That(atWar.WarScore(war.A, "nobody"), Is.EqualTo(0));
        }

        [Test]
        public void A_dev_edit_lands_at_its_turn_and_the_stand_ins_play_on_from_it()
        {
            var setup = GameSetup.Writs(3);
            var rules = setup.BuildRules(map);
            var start = game.Turns.First(t => t.Turn == 5).Start;
            int valenLand = Enumerable.Range(0, start.Owners.Length).First(i => start.Owners[i] == "valen" && start.Occupiers[i] == null && !map.Provinces.First(p => p.Id == game.ProvinceIds[i]).IsCapital);
            int keshLand = Enumerable.Range(0, start.Owners.Length).First(i => start.Controller(i) == "kesh");
            var edits = new List<DevEdit>
            {
                new DevEdit { Turn = 5, Command = $"spawn kesh {game.ProvinceIds[keshLand]} 40" },
                new DevEdit { Turn = 5, Command = "war kesh valen" },
                new DevEdit { Turn = 5, Command = $"occupy {game.ProvinceIds[valenLand]} kesh" },
                new DevEdit { Turn = 5, Command = "warscore kesh valen =12" },
            };
            var rec = ScriptedWorldGame.Play(setup, map, rules, edits);
            var edited = ReplayBuilder.Build(Recording.Parse(rec.ToJson()), map, rules);
            Assert.That(edited.Dev, Is.True, "a dev game");
            var t5 = edited.Turns.First(t => t.Turn == 5).Start;
            Assert.That(t5.Armies.Any(a => a.Owner == "kesh" && a.Location == game.ProvinceIds[keshLand] && a.Soldiers == 40), Is.True, "the new army stands where it was raised");
            Assert.That(t5.Occupiers[valenLand], Is.EqualTo("kesh"));
            Assert.That(t5.WarScore("kesh", "valen"), Is.GreaterThanOrEqualTo(12));
            Assert.That(edited.Turns.Sum(t => t.ArmyDrift), Is.EqualTo(0));
            Assert.That(edited.Turns.Take(4).Select(t => t.End.Owners).SequenceEqual(game.Turns.Take(4).Select(t => t.End.Owners), new Owners()), Is.True, "the turns before the edit are unchanged");
            Assert.That(DevEdits.WhyNot(new Game(3, map, null).World, "spawn kesh keshtar 30"), Does.Contain("writs turn"), "the one-move game has no armies to spawn");
        }

        private sealed class Owners : IEqualityComparer<string[]>
        {
            public bool Equals(string[] a, string[] b) => a.SequenceEqual(b);
            public int GetHashCode(string[] a) => a.Length;
        }

        [Test]
        public void The_one_move_game_has_no_armies_and_no_occupation()
        {
            var classic = ReplayBuilder.Build(ScriptedWorldGame.Play(GameSetup.Classic(3), MapDefinition.Standard(), null), MapDefinition.Standard());
            Assert.That(classic.Writs, Is.False);
            Assert.That(classic.Turns.All(t => t.End.Armies.Count == 0 && t.End.Occupiers.All(o => o == null)), Is.True);
        }
    }
}

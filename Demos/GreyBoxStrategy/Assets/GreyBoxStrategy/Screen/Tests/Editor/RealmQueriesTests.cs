using System.IO;
using System.Linq;
using GreyBoxStrategy.Replay;
using NUnit.Framework;
using UnityEngine;

namespace GreyBoxStrategy.Screen.Tests
{
    /// <summary>
    /// The reach map mode and the panels answer "who can attack what" themselves. These check
    /// their answers against what the game actually offered each ruler in the recorded game.
    /// </summary>
    public class RealmQueriesTests
    {
        private static ReplayGame game;
        private static ReplayGame Game => game ?? (game = ReplayBuilder.Build(Recording.Parse(File.ReadAllText(
            Path.Combine(Application.dataPath, "GreyBoxStrategy/Screen/Replays/seed-0004-personas-4b.json")))));

        [Test]
        public void Reach_matches_the_attacks_the_game_offered_every_council()
        {
            var q = new RealmQueries(Game);
            int checkedDecisions = 0;
            foreach (var turn in Game.Turns)
            {
                foreach (var e in turn.Events.Where(x => x.Decision != null && !x.Decision.IsEnvoy && x.Decision.OfferedMoves.Count > 0))
                {
                    var offered = e.Decision.OfferedMoves.Where(m => m.StartsWith("attack ")).Select(m => m.Substring(7)).OrderBy(x => x).ToList();
                    var reach = q.Attackable(turn.Council, e.Decision.AgentId).Select(p => Game.ProvinceIds[p]).OrderBy(x => x).ToList();
                    Assert.That(reach, Is.EqualTo(offered), $"turn {turn.Turn}, {e.Decision.AgentId}");
                    checkedDecisions++;
                }
            }
            Assert.That(checkedDecisions, Is.GreaterThan(40));
        }

        [Test]
        public void The_win_chance_brackets_what_happened()
        {
            // An attack the game says won must have had a chance above zero on the state it was made in;
            // one that failed, a chance below one.
            var q = new RealmQueries(Game);
            foreach (var turn in Game.Turns)
                foreach (var e in turn.Events.Where(x => x.Kind == ReplayEventKind.Capture || x.Kind == ReplayEventKind.AttackFailed))
                {
                    var before = turn.Events.TakeWhile(x => x != e).LastOrDefault()?.After ?? turn.Council;
                    double p = q.WinChance(before, e.Country, e.ProvinceIndex);
                    if (e.Kind == ReplayEventKind.Capture) Assert.That(p, Is.GreaterThan(0), $"turn {turn.Turn}: {e.Text}");
                    else Assert.That(p, Is.LessThan(1), $"turn {turn.Turn}: {e.Text}");
                }
        }

        [Test]
        public void Capitals_come_from_the_map()
        {
            var q = new RealmQueries(Game);
            Assert.That(q.CapitalOf("kesh"), Is.EqualTo("keshtar"));
            Assert.That(q.IsCapital(q.CapitalIndex("valen")));
            Assert.That(q.IsCapital(Game.IndexOf("harrow")), Is.False);
        }
    }
}

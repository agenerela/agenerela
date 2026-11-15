using System.Collections.Generic;
using System.Linq;
using GreyBoxStrategy.Realm;
using GreyBoxStrategy.Replay;
using NUnit.Framework;

namespace GreyBoxStrategy.Screen.Tests
{
    /// <summary>
    /// A replay's council is the live game's council: with all four of R3's settings on, every army's mix, general,
    /// traits and stores, and every siege, as rule mode showed them when the rulers decided. They come from the rules'
    /// own snapshot (<see cref="ArmySnapshot"/>, <see cref="TurnSnapshot"/>), not from the turn's end.
    /// </summary>
    public class ReplayCouncilTests
    {
        private static GameRules AllOn(GameSetup setup, MapDefinition map) =>
            ReplayDirector.WithArmySettings(setup.BuildRules(map), true, true, true, true);

        // The game as rule mode plays it: the screen's state after each turn's upkeep, before any order lands.
        private static Dictionary<int, ReplayState> LiveCouncils(GameSetup setup, MapDefinition map, GameRules rules)
        {
            var ids = ReplayBuilder.ProvinceOrder(map);
            var game = new Game(setup.Seed, map, rules);
            var seats = Lineups.Seat(ScriptedWorldGame.Lineup, 0, map);
            var councils = new Dictionary<int, ReplayState>();
            int guard = rules.MaxTurns + 1;
            while (!game.IsOver && guard-- > 0)
            {
                var requests = game.BeginTurn();
                councils[game.World.Turn] = ReplayBuilder.StateOf(game.World, ids);
                while (requests.Count > 0) requests = game.Submit(requests.Select(r => new CountryAnswer(r, seats[r.CountryId].Decide(r))).ToList());
                game.Resolve();
            }
            return councils;
        }

        private static string Army(ArmyState a) =>
            $"{a.Id} {a.Soldiers} ({a.Pikes}/{a.Riders}/{a.Bows}) {a.General ?? "-"} [{string.Join(",", a.Traits)}] stores {a.HalfStores}/{a.MaxHalfStores}";

        private static string Sieges(ReplayState s) => s.SiegedBy == null ? "none"
            : string.Join(" ", Enumerable.Range(0, s.SiegedBy.Length).Where(i => s.SiegedBy[i] != null).Select(i => $"{i}:{s.SiegedBy[i]} {s.SiegeTurns[i]}/{s.SiegeNeeded[i]}"));

        [TestCase(1), TestCase(2), TestCase(3)]
        public void With_all_four_on_every_council_of_a_replay_is_the_live_games(int seed)
        {
            var setup = GameSetup.Writs(seed);
            var map = setup.BuildMap();
            var rules = AllOn(setup, map);
            var live = LiveCouncils(setup, map, rules);
            var replay = ReplayBuilder.Build(Recording.Parse(ScriptedWorldGame.Play(setup, map, rules).ToJson()), map, rules);

            int armies = 0;
            foreach (var t in replay.Turns)
            {
                Assert.That(live.TryGetValue(t.Turn, out var want), Is.True, $"turn {t.Turn}");
                var got = t.Council;
                Assert.That(got.Armies.Select(Army).OrderBy(x => x), Is.EqualTo(want.Armies.Select(Army).OrderBy(x => x)), $"turn {t.Turn}: the council's armies");
                Assert.That(Sieges(got), Is.EqualTo(Sieges(want)), $"turn {t.Turn}: the council's sieges");
                armies += got.Armies.Count;
            }
            Assert.That(armies, Is.GreaterThan(50));
        }
    }
}

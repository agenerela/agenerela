using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Agenerela;
using Agenerela.Providers;
using NUnit.Framework;

namespace GreyBoxStrategy.Tests
{
    /// <summary>Rules v2 slice R1b: model rulers for the writs turn, the court and the marshal.</summary>
    public sealed class WritRulerTests
    {
        // A fake model that answers each decision the way a scripted player would, by asking one about
        // the very request the seat is deciding.
        private static ILLMProvider LikeScript(Func<WritRuler> seat, Temperament temperament, List<ScriptedRequest> seen = null)
        {
            var script = new ScriptedPlayer(temperament);
            return new ScriptedProvider
            {
                Callback = req =>
                {
                    seen?.Add(req);
                    var m = script.Decide(seat().Asking);
                    return m.Action == Actions.None
                        ? ScriptedReply.Decide(ActionRegistry.None)
                        : ScriptedReply.Decide(m.Action, m.Target ?? TargetRegistry.NoTarget, $"{m}.");
                },
            };
        }

        private static async Task<WritModelRecord> PlayFakes(GameSetup setup, List<ScriptedRequest> seen = null)
        {
            var seats = new Dictionary<string, WritRuler>();
            return await Await.NotCancelled(WritModelRunner.PlayAsync(setup, Enumerable.Repeat(ModelGameRunner.Model, 5).ToList(),
                (w, id) => seats[id] = new WritRuler(w, id, LikeScript(() => seats[id], Temperament.Expansionist, seen)), "fake"));
        }

        [Test]
        public async Task FakeModels_PlayExactlyTheGameTheScriptsPlay()
        {
            var setup = GameSetup.Writs(5);
            setup.TurnCap = 40;
            var viaAgents = await PlayFakes(setup);
            var map = setup.BuildMap();
            var direct = GameRunner.Play(setup.Seed, map.Countries.ToDictionary(c => c.Id, c => (IPlayer)new ScriptedPlayer(Temperament.Expansionist)), map, setup.BuildRules(map));

            Assert.That(viaAgents.Record.Reports.SelectMany(r => r.Resolution), Is.EqualTo(direct.Reports.SelectMany(r => r.Resolution)));
            Assert.That(viaAgents.Record.Game.World.ControlSignature(), Is.EqualTo(direct.Game.World.ControlSignature()));
            Assert.That(viaAgents.Decisions.Count, Is.GreaterThan(100));
            Assert.That(viaAgents.Decisions.All(d => d.Offered), Is.True, "every answer was one of the offered moves");
            Assert.That(viaAgents.Decisions.Any(d => d.Moment == Moment.Army), Is.True, "the marshal ordered armies");
            Assert.That(viaAgents.Decisions.Any(d => d.Moment == Moment.Envoy), Is.True, "the court answered treaties");
            Assert.That(viaAgents.Decisions.Where(d => d.Moment == Moment.Army).All(d => d.Round != null), Is.True, "each army order names its army");
        }

        [Test]
        public async Task TheCourtAndTheMarshal_ReadLocalFacts_AndTheirOwnMovesLine()
        {
            var seen = new List<ScriptedRequest>();
            var setup = GameSetup.Writs(2);
            setup.TurnCap = 12;
            await PlayFakes(setup, seen);

            var court = seen.Where(r => r.Label == "Court").ToList();
            var army = seen.Where(r => r.Label == "Army").ToList();
            Assert.That(court, Is.Not.Empty);
            Assert.That(army, Is.Not.Empty);
            Assert.That(court.All(r => r.Request.Observations.Contains("Writs: ")), Is.True);
            Assert.That(court.Any(r => r.Request.Observations.Contains("Moves open to us: ")), Is.True);
            Assert.That(army.All(r => r.Request.Observations.Contains("Orders open to ") && r.Stimulus.StartsWith("Orders for ")), Is.True);
            Assert.That(army.Any(r => r.Request.Observations.Contains("Ordered this turn: ")), Is.True, "a later decision sees the earlier ones");
            Assert.That(court.All(r => !r.Request.Observations.Contains("Orders open to ")), Is.True, "the court is offered no march");
        }

        [Test]
        public void StandingFacts_StayLocal_OnTheLargestMap()
        {
            var setup = GameSetup.Writs(3, GameSetup.GeneratedMapId);
            setup.Countries = 10;
            setup.Generator = new MapSettings { Size = MapSize.Huge };
            var map = setup.BuildMap();
            var game = new Game(3, map, setup.BuildRules(map));
            var players = map.Countries.ToDictionary(c => c.Id, c => (IPlayer)new ScriptedPlayer(Temperament.Expansionist));
            int most = 0, mostMoves = 0;
            for (int t = 0; t < 30 && !game.IsOver; t++)
            {
                var requests = game.BeginTurn();
                while (requests.Count > 0)
                {
                    foreach (var r in requests.Where(r => r.Moment != Moment.Envoy))
                    {
                        most = Math.Max(most, r.StandingFacts.Count);
                        mostMoves = Math.Max(mostMoves, r.Legal.Count(m => m.Target != null));
                    }
                    requests = game.Submit(requests.Select(r => new CountryAnswer(r, players[r.CountryId].Decide(r))).ToList());
                }
                game.Resolve();
            }
            Assert.That(most, Is.LessThanOrEqualTo(WritFacts.MaxStanding), "ten countries, still eight lines at most");
            Assert.That(mostMoves, Is.LessThanOrEqualTo(20), "the targeted moves of one decision stay near a dozen");
        }

        // R1c's run, end to end with a fake model: the writs arm and today's game interleaved in blocks,
        // every file written, the per-turn file for the writs arm.
        [Test]
        public async Task ATable_PlaysTheWritsTurn_InterleavedWithTodaysGame()
        {
            ILLMProvider First(string model, World w) => new ScriptedProvider
            {
                Callback = req => ScriptedReply.Decide(req.OfferedActions.FirstOrDefault(a => a != ActionRegistry.None) ?? ActionRegistry.None,
                    req.OfferedTargets.FirstOrDefault(t => t != TargetRegistry.NoTarget) ?? TargetRegistry.NoTarget, "Onward."),
            };
            var writs = Tables.Named("fake,fake,fake,fake,fake", null, personas: true);
            writs.Setup = GameSetup.Writs(1);
            writs.Setup.TurnCap = 8;
            var oneMove = Tables.Named("fake,fake,fake,fake,fake", null, personas: true);
            string folder = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "gbs-writs-table-" + Guid.NewGuid().ToString("N"));
            try
            {
                var cards = await Await.NotCancelled(StrategyTableRuns.RunInterleavedAsync(folder, new[] { ("writs", writs), ("onemove", oneMove) },
                    First, 2, 1, 1));
                Assert.That(cards.Count, Is.EqualTo(2));
                foreach (string file in new[] { "summary.md", "scorecard.md", "model.csv", "games.csv", "writs-turns.csv" })
                    Assert.That(System.IO.File.Exists(System.IO.Path.Combine(folder, "writs", file)), Is.True, file);
                Assert.That(System.IO.File.Exists(System.IO.Path.Combine(folder, "onemove", "writs-turns.csv")), Is.False);
                var rows = System.IO.File.ReadAllLines(System.IO.Path.Combine(folder, "writs", "writs-turns.csv"));
                Assert.That(rows.Length, Is.EqualTo(1 + 2 * 8 * 5), "a row a country a turn, two games of eight turns");
                Assert.That(System.IO.File.ReadAllText(System.IO.Path.Combine(folder, "writs", "model.csv")), Does.Contain(",Army,"));
            }
            finally
            {
                if (System.IO.Directory.Exists(folder)) System.IO.Directory.Delete(folder, true);
            }
        }

        // R1c: the rules let an army march further than the twelve targets its list offers (and fortify reach
        // more than the six provinces its list names), and the seat's availability once followed the rules
        // alone, so a model could name a target the list left out and the game held it as not offered. A
        // model that always tries such a target must now be contained, never reach the game unoffered.
        [Test]
        public async Task ATargetTheListLeftOut_IsContained_NotHeldByTheGame()
        {
            var seats = new Dictionary<string, WritRuler>();
            World world = null;
            int tried = 0;
            ILLMProvider Provider(string id) => new ScriptedProvider
            {
                Callback = req =>
                {
                    var r = seats[id].Asking;
                    foreach (string a in new[] { Actions.March, Actions.Fortify }.Where(req.OfferedActions.Contains))
                        foreach (string t in req.OfferedTargets.Where(t => t != TargetRegistry.NoTarget))
                        {
                            var move = new Move(a, t);
                            string why = a == Actions.March ? WritLegality.WhyNotArmy(world, r.CountryId, r.ArmyId, move) : WritLegality.WhyNotCourt(world, r.CountryId, move);
                            if (why != null || r.Legal.Contains(move)) continue;
                            tried++;
                            return ScriptedReply.Decide(a, t, "Beyond the list.");
                        }
                    var m = new ScriptedPlayer(Temperament.Expansionist).Decide(r);
                    return m.Action == Actions.None ? ScriptedReply.Decide(ActionRegistry.None) : ScriptedReply.Decide(m.Action, m.Target ?? TargetRegistry.NoTarget, $"{m}.");
                },
            };
            var setup = GameSetup.Writs(1);
            setup.TurnCap = 30;
            var record = await Await.NotCancelled(WritModelRunner.PlayAsync(setup, Enumerable.Repeat(ModelGameRunner.Model, 5).ToList(),
                (w, id) => seats[id] = new WritRuler(world = w, id, Provider(id)), "fake"));
            Assert.That(tried, Is.GreaterThan(0), "the game gave the fake model a target beyond a list to try");
            var unoffered = record.Decisions.Where(d => !d.Offered).Select(d => $"{d.Country} {d.Move}").ToList();
            Assert.That(unoffered, Is.Empty, "orders the game held as not offered: " + string.Join("; ", unoffered.Take(5)));
        }

        [Test]
        public void ARuler_IsForTheWritsTurnOnly()
        {
            var game = new Game(1);
            Assert.Throws<ArgumentException>(() => new WritRuler(game.World, "valen", new ScriptedProvider()));
        }
    }
}

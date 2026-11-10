using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using static GreyBoxStrategy.Tests.TestGames;

namespace GreyBoxStrategy.Tests
{
    /// <summary>
    /// Wraps a player and checks every request it is given: the legal list is exactly what
    /// <see cref="Legality.WhyNot"/> allows, and the facts keep to their budgets.
    /// </summary>
    internal sealed class CheckingPlayer : IPlayer
    {
        private readonly IPlayer inner;
        public int Requests;

        public CheckingPlayer(IPlayer inner) { this.inner = inner; }
        public string Kind => inner.Kind;

        public Move Decide(CountryRequest r)
        {
            Requests++;
            Assert.That(r.StandingFacts.Count, Is.InRange(3, 8), "standing facts");
            Assert.That(r.TurnFacts.Count, Is.LessThanOrEqualTo(Facts.MaxTurnFacts), "turn facts");
            if (r.Moment == Moment.Council)
            {
                var ids = new List<string> { null };
                ids.AddRange(r.World.CountryList.Select(c => c.Id));
                ids.AddRange(r.World.ProvinceList.Select(p => p.Id));
                foreach (string action in Actions.Council)
                foreach (string target in ids)
                {
                    var m = new Move(action, target);
                    bool listed = r.Legal.Contains(m);
                    bool legal = Legality.IsLegal(r.World, r.CountryId, m);
                    if (listed != legal)
                        Assert.Fail($"turn {r.Turn} {r.CountryId}: {m} listed={listed} legal={legal} ({Legality.WhyNot(r.World, r.CountryId, m)})");
                }
                Assert.That(r.Legal.Last(), Is.EqualTo(Move.Hold), "hold is always offered, last");
            }
            var choice = inner.Decide(r);
            Assert.That(r.Legal, Does.Contain(choice), $"{Kind} chose a move it was not offered");
            return choice;
        }
    }

    public sealed class WholeGameTests
    {
        private static readonly string[] Mixed = Lineups.Named["mixed"];

        private static GameRecord PlayChecked(int seed, string[] kinds, out int requests)
        {
            var map = MapDefinition.Standard();
            var seats = new Dictionary<string, IPlayer>();
            var checkers = new List<CheckingPlayer>();
            for (int i = 0; i < map.Countries.Count; i++)
            {
                var c = new CheckingPlayer(Lineups.Make(kinds[(i + seed) % kinds.Length]));
                checkers.Add(c);
                seats[map.Countries[i].Id] = c;
            }
            var record = GameRunner.Play(seed, seats, map);
            requests = checkers.Sum(c => c.Requests);
            return record;
        }

        [Test]
        public void Legality_EveryOfferedMoveIsLegalAndEveryLegalMoveIsOffered([Values(1, 2, 3)] int seed)
        {
            PlayChecked(seed, Mixed, out int requests);
            Assert.That(requests, Is.GreaterThan(50));
        }

        [Test]
        public void Legality_HoldsForRandomPlayersToo([Values(4, 5)] int seed)
        {
            PlayChecked(seed, Lineups.Named["random"], out _);
        }

        [Test]
        public void Termination_EveryGameEndsWithAWinnerWithinTheTurnLimit()
        {
            foreach (string lineup in Lineups.Named.Keys)
            {
                var games = GameRunner.PlaySeries(lineup, GameRunner.Seeds(20, 100));
                foreach (var g in games)
                {
                    Assert.That(g.Game.IsOver, Is.True, $"{lineup} seed {g.Seed}");
                    Assert.That(g.Result.Turns, Is.InRange(1, g.Game.Rules.MaxTurns), $"{lineup} seed {g.Seed}");
                    Assert.That(g.Game.World.Country(g.Result.Winner).Alive, Is.True, $"{lineup} seed {g.Seed}");
                    Assert.That(g.Reports.Count, Is.EqualTo(g.Result.Turns));
                }
            }
        }

        [Test]
        public void Outcomes_EveryRequestGetsExactlyOne([Values(7, 8)] int seed)
        {
            var g = PlayWhole(seed, Lineups.Named["mixed"]);
            int aliveAtStart = g.Game.World.CountryList.Count;
            foreach (var report in g.Reports)
            {
                var council = report.Outcomes.Where(o => o.Request.Moment == Moment.Council).Select(o => o.Request.CountryId).ToList();
                Assert.That(council, Is.Unique, $"turn {report.Turn}");
                Assert.That(council.Count, Is.EqualTo(aliveAtStart), $"turn {report.Turn}: one council outcome per living country");
                var envoys = report.Outcomes.Where(o => o.Request.Moment == Moment.Envoy).Select(o => o.Request.OfferId).ToList();
                Assert.That(envoys, Is.Unique, $"turn {report.Turn}");
                foreach (var o in report.Outcomes)
                    Assert.That(o.Refused || o.Applied.Equals(o.Choice) || o.Request.Moment == Moment.Envoy, Is.True, $"turn {report.Turn}: {o.Choice} -> {o.Text}");
                aliveAtStart = report.ProvinceCounts.Count(kv => kv.Value > 0);
            }
        }

        [Test]
        public void Snapshot_NothingChangesBetweenBeginningATurnAndResolvingIt()
        {
            var game = NewGame(3, GameRules.Standard);
            var players = MapDefinition.Standard().Countries.ToDictionary(c => c.Id, c => (IPlayer)new ScriptedPlayer(Temperament.Expansionist));
            for (int turn = 0; turn < 5; turn++)
            {
                var council = game.BeginTurn();
                string before = game.World.OwnerSignature() + Armies(game.World);
                var envoys = game.SubmitCouncil(council.Select(r => new CountryAnswer(r, players[r.CountryId].Decide(r))).ToList());
                envoys.Select(r => players[r.CountryId].Decide(r)).ToList();
                Assert.That(game.World.OwnerSignature() + Armies(game.World), Is.EqualTo(before), $"turn {turn + 1}");
                game.Resolve(envoys.Select(r => new CountryAnswer(r, players[r.CountryId].Decide(r))).ToList());
            }
        }

        private static string Armies(World w) => string.Join(",", w.CountryList.Select(c => c.Army + "/" + c.Gold));

        [Test]
        public void IllegalChoice_IsHeldAndReportedAsRefused()
        {
            var game = NewGame();
            var council = game.BeginTurn();
            game.SubmitCouncil(new[] { new CountryAnswer(For(council, "valen"), new Move(Actions.Attack, "ilvarion")) });
            var report = game.Resolve();
            var o = Outcome(report, "valen");
            Assert.That(o.Refused, Is.True);
            Assert.That(o.Applied, Is.EqualTo(Move.Hold));
            Assert.That(o.Text, Does.Contain("was not offered"));
            Assert.That(report.StaleRefusals, Is.EqualTo(0), "a move never offered is not a stale one");
            Assert.That(game.World.Province("ilvarion").Owner, Is.EqualTo("ilvar"));
        }

        [Test]
        public void TerritoryChangesEveryFewTurns_TheMapKeepsMoving()
        {
            var summary = new SeriesSummary("mixed", GameRunner.PlaySeries("mixed", GameRunner.Seeds(30)));
            Assert.That(summary.TerritoryChangesPerTurn, Is.GreaterThan(1.0), "provinces change hands about once a turn or more");
            Assert.That(summary.TurnsWithNoChange, Is.LessThan(0.3));
            Assert.That(summary.MeanTurns, Is.InRange(12, 30));
        }
    }

    public sealed class DeterminismTests
    {
        [Test]
        public void SameSeed_SameGame_TurnByTurn()
        {
            var a = PlayWhole(42, Lineups.Named["mixed"]);
            var b = PlayWhole(42, Lineups.Named["mixed"]);
            Assert.That(b.Reports.Count, Is.EqualTo(a.Reports.Count));
            for (int i = 0; i < a.Reports.Count; i++)
                Assert.That(b.Reports[i].OwnerGrid, Is.EqualTo(a.Reports[i].OwnerGrid), $"turn {i + 1}");
            Assert.That(GameLog.Write(b), Is.EqualTo(GameLog.Write(a)), "the whole log, every decision and roll");
        }

        [Test]
        public void SameSeed_SameGame_WithRandomPlayers()
        {
            var a = PlayWhole(9, Lineups.Named["random"]);
            var b = PlayWhole(9, Lineups.Named["random"]);
            Assert.That(GameLog.Write(b), Is.EqualTo(GameLog.Write(a)));
        }

        [Test]
        public void DifferentSeeds_DifferentGames()
        {
            var logs = Enumerable.Range(1, 5).Select(s => GameLog.Write(PlayWhole(s, Lineups.Named["mixed"]))).ToList();
            Assert.That(logs.Distinct().Count(), Is.EqualTo(5));
        }

        [Test]
        public void Dice_DependOnlyOnTheirKey_NotOnWhatWasRolledBefore()
        {
            double first = Dice.Unit(5, 3, "valen", "attack:fenmark");
            for (int i = 0; i < 100; i++) Dice.Unit(i, i, "x" + i, "y");
            Assert.That(Dice.Unit(5, 3, "valen", "attack:fenmark"), Is.EqualTo(first));
            Assert.That(Dice.Unit(5, 3, "valen", "attack:harrow"), Is.Not.EqualTo(first));
            Assert.That(Dice.Unit(6, 3, "valen", "attack:fenmark"), Is.Not.EqualTo(first));
        }

        [Test]
        public void Dice_AreUniformEnough()
        {
            var rolls = Enumerable.Range(0, 20000).Select(i => Dice.Unit(1, i, "c", "p")).ToList();
            Assert.That(rolls.All(r => r >= 0 && r < 1), Is.True);
            Assert.That(rolls.Average(), Is.EqualTo(0.5).Within(0.01));
            Assert.That(rolls.Count(r => r < 0.1) / 20000.0, Is.EqualTo(0.1).Within(0.01));
            var ints = Enumerable.Range(0, 6000).Select(i => Dice.Int(2, i, "c", "p", 0, 3)).ToList();
            foreach (int v in new[] { 0, 1, 2 }) Assert.That(ints.Count(x => x == v), Is.InRange(1800, 2200));
        }

        [Test]
        public void ADecisionChangedMidGame_DoesNotChangeTheDiceOfAnotherBattle()
        {
            // The same battle, keyed by seed, turn, attacker and province, rolls the same whatever
            // happened before it. That is what lets stage 2 compare two models on the same luck.
            var r = GameRules.Standard;
            var g1 = NewGame(11, r);
            var g2 = NewGame(11, r);
            g2.World.Country("orsk").Gold = 999; // a different history elsewhere on the map
            foreach (var g in new[] { g1, g2 })
            {
                g.World.Country("valen").Army = 40;
                g.World.Province("westmarch").Garrison = 40;
            }
            var a = PlayTurn(g1, ("valen", Actions.Attack, "westmarch"));
            var b = PlayTurn(g2, ("valen", Actions.Attack, "westmarch"), ("orsk", Actions.Recruit, null));
            Assert.That(Outcome(b, "valen").Text, Is.EqualTo(Outcome(a, "valen").Text));
        }
    }

    public sealed class PlayerTests
    {
        [Test]
        public void Expansionist_BeatsFourRandomPlayers()
        {
            var s = new SeriesSummary("expansionist-vs-random", GameRunner.PlaySeries("expansionist-vs-random", GameRunner.Seeds(40)));
            Assert.That(s.Kind(Lineups.Expansionist).WinRate, Is.GreaterThanOrEqualTo(0.8), s.ToMarkdown());
        }

        [Test]
        public void Cautious_BeatsFourRandomPlayers()
        {
            var s = new SeriesSummary("cautious-vs-random", GameRunner.PlaySeries("cautious-vs-random", GameRunner.Seeds(40)));
            Assert.That(s.Kind(Lineups.Cautious).WinRate, Is.GreaterThanOrEqualTo(0.8), s.ToMarkdown());
        }

        [Test]
        public void InTheMixedLineUp_EachScriptedKindWinsFarMoreOftenThanRandom()
        {
            var s = new SeriesSummary("mixed", GameRunner.PlaySeries("mixed", GameRunner.Seeds(50)));
            double random = s.Kind(Lineups.Random).WinRate;
            Assert.That(random, Is.LessThan(0.08), s.ToMarkdown());
            Assert.That(s.Kind(Lineups.Expansionist).WinRate, Is.GreaterThan(random + 0.1), s.ToMarkdown());
            Assert.That(s.Kind(Lineups.Cautious).WinRate, Is.GreaterThan(random + 0.1), s.ToMarkdown());
        }

        [Test]
        public void TheTwoTemperamentsPlayDifferently()
        {
            var counts = new Dictionary<string, Dictionary<string, int>>();
            foreach (var g in GameRunner.PlaySeries("scripted", GameRunner.Seeds(20)))
            foreach (var o in g.Reports.SelectMany(r => r.Outcomes).Where(o => o.Request.Moment == Moment.Council))
            {
                string kind = g.Kinds[o.Request.CountryId];
                if (!counts.TryGetValue(kind, out var c)) counts[kind] = c = new Dictionary<string, int>();
                c[o.Choice.Action] = (c.TryGetValue(o.Choice.Action, out int n) ? n : 0) + 1;
            }
            int Count(string kind, string action) => counts[kind].TryGetValue(action, out int n) ? n : 0;
            Assert.That(Count(Lineups.Expansionist, Actions.Fortify), Is.EqualTo(0), "the expansionist never fortifies");
            Assert.That(Count(Lineups.Cautious, Actions.Fortify), Is.GreaterThan(0), "the cautious one does");
        }

        [Test]
        public void Scripted_AsksForPeaceWhenOutmatchedAndNotEveryTurn()
        {
            var game = NewGame();
            Give(game.World, "stonereach", "valen");
            game.World.SetRelation("valen", "orsk", Relation.War);
            game.World.Country("orsk").Army = 200;
            var player = new ScriptedPlayer(Temperament.Cautious);
            var c1 = game.BeginTurn();
            Assert.That(player.Decide(For(c1, "valen")), Is.EqualTo(new Move(Actions.OfferPeace, "orsk")));
            Finish(game, c1, r => false, ("valen", Actions.OfferPeace, "orsk"));
            var c2 = game.BeginTurn();
            Assert.That(player.Decide(For(c2, "valen")).Action, Is.Not.EqualTo(Actions.OfferPeace), "it waits after a refusal");
            Finish(game, c2, null);
        }

        [Test]
        public void Scripted_TakesTheWeakNeutralProvinceOnTurnOne()
        {
            var game = NewGame();
            game.World.Province("westmarch").Garrison = 8;
            game.World.Province("fenmark").Garrison = 16;
            game.World.Province("stonereach").Garrison = 16;
            var move = new ScriptedPlayer(Temperament.Expansionist).Decide(For(game.BeginTurn(), "valen"));
            Assert.That(move, Is.EqualTo(new Move(Actions.Attack, "westmarch")));
        }

        [Test]
        public void Random_OnlyChoosesOfferedMoves_AndUsesAllOfThem()
        {
            var seen = new HashSet<string>();
            foreach (var g in GameRunner.PlaySeries("random", GameRunner.Seeds(10)))
            foreach (var o in g.Reports.SelectMany(r => r.Outcomes))
            {
                Assert.That(o.Request.Legal, Does.Contain(o.Choice));
                seen.Add(o.Choice.Action);
            }
            Assert.That(seen, Is.SupersetOf(Actions.Council));
        }
    }

    public sealed class FactsTests
    {
        [Test]
        public void Standing_OpensWithTheScoreAndNamesRivalsByTheirIds()
        {
            var game = NewGame();
            var valen = For(game.BeginTurn(), "valen");
            Assert.That(valen.StandingFacts[0], Is.EqualTo("Turn 1 of 30. We hold 3 of 25 provinces; 15 wins."));
            Assert.That(valen.StandingFacts[1], Is.EqualTo("Treasury: 55 gold; income 30 and upkeep 15 a turn."));
            Assert.That(valen.StandingFacts[2], Is.EqualTo("Army: 30 soldiers. Stability: 4 of 5."));
            Assert.That(valen.StandingFacts[3], Is.EqualTo("orsk: at peace with us; army 30, 3 provinces."));
            Assert.That(valen.StandingFacts.Last(), Is.EqualTo("3 neutral provinces border us."));
            Assert.That(valen.StandingFacts.Count, Is.EqualTo(8));
            Assert.That(valen.TurnFacts, Is.Empty, "nothing has happened yet");
        }

        [Test]
        public void Turn_ALostProvinceComesFirst()
        {
            var game = NewGame();
            game.World.Country("valen").Army = 200;
            Give(game.World, "stonereach", "valen");
            game.World.SetRelation("valen", "orsk", Relation.War);
            var c1 = game.BeginTurn();
            Finish(game, c1, null, ("valen", Actions.Attack, "corwick"), ("orsk", Actions.RaiseTaxes, null));
            var orsk = For(game.BeginTurn(), "orsk");
            Assert.That(orsk.TurnFacts[0], Does.StartWith("valen took corwick from us; we lost"));
            Assert.That(orsk.StandingFacts.Any(l => l.StartsWith("valen: at war with us")), Is.True);
        }

        [Test]
        public void Turn_KeepsAtMostThreeMostImportantFirst()
        {
            var facts = new[]
            {
                new TurnFact(3, "d"), new TurnFact(1, "b"), new TurnFact(0, "a"), new TurnFact(2, "c"), new TurnFact(1, "b2"),
            };
            Assert.That(Facts.Select(facts), Is.EqualTo(new[] { "a", "b", "b2" }));
        }

        [Test]
        public void TargetDetails_DescribeWhatTheTargetIsNow()
        {
            var game = NewGame();
            game.World.Province("westmarch").Garrison = 9;
            var valen = For(game.BeginTurn(), "valen");
            Assert.That(valen.Legal.First(m => m.ToString() == "attack westmarch").Detail, Is.EqualTo("neutral, garrison 9"));
            Assert.That(valen.Legal.First(m => m.ToString() == "fortify ashby").Detail, Is.EqualTo("ours, 0 forts"));
        }
    }

    public sealed class RunnerTests
    {
        [Test]
        public void Run_WritesASummaryACsvAndOneLogPerGame()
        {
            string folder = Path.Combine(Path.GetTempPath(), "gbs-runner-test-" + Guid.NewGuid().ToString("N"));
            try
            {
                var summary = StrategyRuns.Run(folder, "mixed", 5, 1);
                Assert.That(File.ReadAllText(Path.Combine(folder, "summary.md")), Does.Contain("| expansionist |"));
                Assert.That(File.ReadAllLines(Path.Combine(folder, "games.csv")).Length, Is.EqualTo(6));
                Assert.That(Directory.GetFiles(folder, "game-*.md").Length, Is.EqualTo(5));
                string log = File.ReadAllText(Path.Combine(folder, "game-0001.md"));
                Assert.That(log, Does.Contain("Standing facts:"));
                Assert.That(log, Does.Contain("**Chose `"));
                Assert.That(summary.Kinds.Sum(k => k.Wins), Is.EqualTo(5));
                Assert.That(summary.Kinds.Sum(k => k.Seats), Is.EqualTo(25));
            }
            finally
            {
                if (Directory.Exists(folder)) Directory.Delete(folder, true);
            }
        }

        [Test]
        public void Seats_RotateSoEveryKindPlaysEveryCountry()
        {
            var map = MapDefinition.Standard();
            var seen = new HashSet<string>();
            for (int i = 0; i < 5; i++)
                foreach (var kv in Lineups.Seat("mixed", i, map))
                    seen.Add(kv.Key + "=" + kv.Value.Kind);
            foreach (var c in map.Countries)
            foreach (string kind in Lineups.Named["mixed"].Distinct())
                Assert.That(seen, Does.Contain(c.Id + "=" + kind));
        }
    }
}

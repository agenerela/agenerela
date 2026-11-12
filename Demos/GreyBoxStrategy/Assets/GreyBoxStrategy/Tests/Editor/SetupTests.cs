using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;

namespace GreyBoxStrategy.Tests
{
    /// <summary>Rules v2 slice R0: the game's settings as data, and the map loaded from data.</summary>
    public sealed class SetupTests
    {
        // The 200-game mixed series on today's code (6e6c693, before R0), measured in that checkout:
        // SHA-256 of summary.md with line ends as "\n", and of every turn's owner grid and resolution count.
        private const string SummaryBeforeR0 = "62de219944f2ced4c7d8d8fa8878cdd926d22243965c3319e66251b076de1d25";
        private const string GridsBeforeR0 = "f2d84eb36133cf6f9f0f8ab5752e51f65eb7c597574d25a2b1ec7e822220e5fe";

        private static string Sha(string s)
        {
            using (var sha = SHA256.Create())
                return string.Concat(sha.ComputeHash(Encoding.UTF8.GetBytes(s)).Select(b => b.ToString("x2")));
        }

        // --- The Classic preset ---------------------------------------------------------------

        [Test]
        public void Classic_EqualsTheBoardBuiltInCode_InEveryField()
        {
            var data = MapDefinition.Standard();
            var code = LegacyBoard.Standard();

            Assert.That(data.Countries.Count, Is.EqualTo(code.Countries.Count));
            for (int i = 0; i < code.Countries.Count; i++)
            {
                var a = data.Countries[i];
                var b = code.Countries[i];
                Assert.That((a.Id, a.Name, a.Personality, a.StartGold, a.StartArmy),
                    Is.EqualTo((b.Id, b.Name, b.Personality, b.StartGold, b.StartArmy)), $"country {i}");
            }
            Assert.That(data.Provinces.Count, Is.EqualTo(code.Provinces.Count));
            for (int i = 0; i < code.Provinces.Count; i++)
            {
                var a = data.Provinces[i];
                var b = code.Provinces[i];
                Assert.That((a.Id, a.Name, a.Column, a.Row, a.StartOwner, a.IsCapital, a.StartForts),
                    Is.EqualTo((b.Id, b.Name, b.Column, b.Row, b.StartOwner, b.IsCapital, b.StartForts)), $"province {i}");
                Assert.That(data.Neighbours(a.Id), Is.EqualTo(code.Neighbours(b.Id)), $"neighbours of {a.Id}");
            }
            Assert.That((data.Columns, data.Rows), Is.EqualTo((code.Columns, code.Rows)));
        }

        [Test]
        public void Classic_CarriesTerrainCoastsAndPortsForLaterSlices()
        {
            var map = MapDefinition.Standard();
            foreach (var t in TerrainTable.All)
                Assert.That(map.Provinces.Any(p => p.Terrain == t), Is.True, $"no {t} on Classic");
            foreach (var p in map.Provinces)
            {
                bool edge = p.Column == 0 || p.Row == 0 || p.Column == 4 || p.Row == 4;
                Assert.That(p.IsCoastal, Is.EqualTo(edge), $"{p.Id}: every edge province, and only those, is coastal");
                Assert.That(!p.HasPort || p.IsCoastal, Is.True, $"{p.Id}: a port is on the coast");
                Assert.That(p.Population, Is.InRange(1, 12), p.Id);
            }
            Assert.That(map.Provinces.Where(p => p.IsCapital).All(p => p.Terrain != Terrain.Marsh), Is.True, "no capital in marsh");
        }

        [Test]
        public void Classic_200GameSeries_IsByteIdenticalToTheGameBeforeR0()
        {
            var records = GameRunner.PlaySeries("mixed", GameRunner.Seeds(200));
            string summary = new SeriesSummary("mixed", records).ToMarkdown().Replace("\r\n", "\n");
            string grids = string.Join("\n", records.SelectMany(r => r.Reports.Select(x => x.OwnerGrid + x.Resolution.Count)));
            Assert.That(Sha(summary), Is.EqualTo(SummaryBeforeR0), "summary.md of the 200-game mixed series");
            Assert.That(Sha(grids), Is.EqualTo(GridsBeforeR0), "every turn of the 200 games");
        }

        [Test]
        public void Classic_SetupGame_ReplaysTheStandardGameTurnByTurn([Values(4, 11, 37)] int seed)
        {
            var setup = GameSetup.Classic(seed);
            var map = setup.BuildMap();
            var viaSetup = GameRunner.Play(seed, Lineups.Seat("mixed", 0, map), map, setup.BuildRules(map));
            var legacy = LegacyBoard.Standard();
            var before = GameRunner.Play(seed, Lineups.Seat("mixed", 0, legacy), legacy);

            Assert.That(viaSetup.Reports.Select(r => r.OwnerGrid), Is.EqualTo(before.Reports.Select(r => r.OwnerGrid)));
            Assert.That(viaSetup.Reports.SelectMany(r => r.Resolution), Is.EqualTo(before.Reports.SelectMany(r => r.Resolution)));
            Assert.That((viaSetup.Result.Winner, viaSetup.Result.Ending, viaSetup.Result.Turns),
                Is.EqualTo((before.Result.Winner, before.Result.Ending, before.Result.Turns)));
        }

        [Test]
        public void Classic_SetupRules_AreTodaysRules()
        {
            var setup = GameSetup.Classic();
            var rules = setup.BuildRules(setup.BuildMap());
            var today = GameRules.Standard;
            Assert.That((rules.Victory, rules.MaxTurns, rules.ProvincesToWin), Is.EqualTo((today.Victory, today.MaxTurns, today.ProvincesToWin)));
            Assert.That(rules.ProvincesToWin, Is.EqualTo(15), "60% of 25, as today");
        }

        [Test]
        public void Classic_FirstFactReadsAsBefore()
        {
            var game = GameSetup.Classic(1).NewGame();
            var council = game.BeginTurn();
            Assert.That(council[0].StandingFacts[0], Is.EqualTo("Turn 1 of 30. We hold 3 of 25 provinces; 15 wins."));
        }

        // --- Settings round-trip and refusal --------------------------------------------------

        [Test]
        public void Setup_RoundTripsThroughItsSavedForm()
        {
            foreach (var setup in new[]
            {
                GameSetup.Classic(),
                new GameSetup { Seed = -77, Victory = VictoryMode.Conquest, TurnCap = 90, Storyteller = "kingmaker", Advisor = false },
                new GameSetup { Seed = 5, Victory = VictoryMode.TurnLimit, TurnLimit = 45, Storyteller = "saga" },
            })
            {
                string json = setup.ToJson();
                var back = GameSetup.FromJson(json);
                Assert.That(back.ToJson(), Is.EqualTo(json));
                Assert.That((back.Seed, back.Map, back.Countries, back.Victory, back.DominationPercent, back.TurnLimit, back.TurnCap, back.Storyteller, back.Advisor),
                    Is.EqualTo((setup.Seed, setup.Map, setup.Countries, setup.Victory, setup.DominationPercent, setup.TurnLimit, setup.TurnCap, setup.Storyteller, setup.Advisor)));
            }
        }

        [Test]
        public void Setup_SavedForm_IsReadable()
        {
            string json = new GameSetup { Victory = VictoryMode.TurnLimit }.ToJson();
            Assert.That(json, Does.Contain("\"victory\": \"turn_limit\""));
            Assert.That(json, Does.Contain("\"map\": \"classic\""));
        }

        [Test]
        public void Setup_LeftOutNames_KeepTheirDefaults()
        {
            var s = GameSetup.FromJson("{ \"seed\": 9 }");
            Assert.That(s.Seed, Is.EqualTo(9));
            Assert.That(s.ToJson(), Is.EqualTo(GameSetup.Classic(9).ToJson()));
        }

        [TestCase("{ \"map\": \"atlantis\" }", "unknown map 'atlantis'")]
        [TestCase("{ \"countries\": 6 }", "the Classic board has five countries")]
        [TestCase("{ \"countries\": 2 }", "2 countries is outside 3 to 10")]
        [TestCase("{ \"dominationPercent\": 50 }", "domination at 50%")]
        [TestCase("{ \"turnLimit\": 0 }", "turn limit of 0")]
        [TestCase("{ \"victory\": \"conquest\", \"turnCap\": 0 }", "turn cap of 0")]
        [TestCase("{ \"storyteller\": \"loud\" }", "unknown storyteller preset 'loud'")]
        [TestCase("{ \"victory\": \"kingmaker\" }", "cannot be read")]
        [TestCase("{ \"turnLimitt\": 20 }", "cannot be read")]
        [TestCase("{ \"version\": 2 }", "setup version 2")]
        [TestCase("", "empty")]
        [TestCase("not json", "cannot be read")]
        public void Setup_Invalid_IsRefusedWithAMessage(string json, string message)
        {
            var e = Assert.Throws<FormatException>(() => GameSetup.FromJson(json));
            Assert.That(e.Message, Does.Contain(message));
        }

        [Test]
        public void Setup_InvalidInCode_IsRefusedBeforeAGameStarts()
        {
            var s = new GameSetup { Countries = 7 };
            Assert.That(s.WhyInvalid(), Does.Contain("five countries"));
            var e = Assert.Throws<ArgumentException>(() => s.NewGame());
            Assert.That(e.Message, Does.Contain("five countries"));
        }

        // --- The map as data -------------------------------------------------------------------

        [Test]
        public void Map_RoundTripsThroughJson()
        {
            var map = ClassicMap.Load();
            string json = map.ToJson();
            Assert.That(WorldMap.FromJson(json).ToJson(), Is.EqualTo(json));
            Assert.That(json, Does.Contain("\"terrain\": \"mountains\""));
        }

        [TestCase("dup", "the id ashby is used twice")]
        [TestCase("no-capital", "valen has 0 capitals")]
        [TestCase("bad-border", "names an unknown province")]
        [TestCase("bad-owner", "owned by unknown country")]
        [TestCase("bad-terrain", "cannot be read")]
        public void Map_Invalid_IsRefusedWithAMessage(string breakage, string message)
        {
            var map = ClassicMap.Load();
            string json;
            switch (breakage)
            {
                case "dup": map.Provinces[0].Id = "ashby"; json = map.ToJson(); break;
                case "no-capital": map.Provinces.First(p => p.Id == "highmoor").Capital = false; json = map.ToJson(); break;
                case "bad-border": map.Borders.Add(new[] { "ashby", "lyonesse" }); json = map.ToJson(); break;
                case "bad-owner": map.Provinces[2].Owner = "atlantis"; json = map.ToJson(); break;
                default: json = map.ToJson().Replace("\"mountains\"", "\"volcano\""); break;
            }
            var e = Assert.Throws<FormatException>(() => WorldMap.FromJson(json));
            Assert.That(e.Message, Does.Contain(message));
        }

        // --- Victory modes ----------------------------------------------------------------------

        [Test]
        public void Conquest_EndsWithOneCountryLeft_OrUnfinishedAtTheCap()
        {
            int lastStanding = 0;
            foreach (int seed in GameRunner.Seeds(20, 300))
            {
                var setup = new GameSetup { Seed = seed, Victory = VictoryMode.Conquest, TurnCap = 150 };
                var g = GameRunner.Play(setup, "scripted", seed);
                var alive = g.Game.World.Alive.ToList();
                Assert.That(g.Result.Ending, Is.EqualTo(Ending.LastStanding).Or.EqualTo(Ending.Unfinished), $"seed {seed}");
                if (g.Result.Ending == Ending.LastStanding)
                {
                    lastStanding++;
                    Assert.That(alive.Select(c => c.Id), Is.EqualTo(new[] { g.Result.Winner }), $"seed {seed}");
                    Assert.That(g.Game.World.ProvinceCount(g.Result.Winner), Is.EqualTo(25), $"seed {seed}: the winner holds everything");
                }
                else
                {
                    Assert.That(g.Result.Turns, Is.EqualTo(150), $"seed {seed}: unfinished only at the cap");
                    Assert.That(alive.Count, Is.GreaterThan(1));
                }
            }
            Assert.That(lastStanding, Is.GreaterThan(0), "some conquest games end with one country left");
        }

        [Test]
        public void Conquest_NobodyWinsByHolding15()
        {
            var rules = new GameSetup { Victory = VictoryMode.Conquest }.BuildRules(MapDefinition.Standard());
            var game = new Game(3, MapDefinition.Standard(), rules);
            var players = game.World.CountryList.ToDictionary(c => c.Id, c => (IPlayer)new ScriptedPlayer(Temperament.Expansionist));
            while (!game.IsOver)
            {
                var council = game.BeginTurn();
                var envoys = game.SubmitCouncil(council.Select(r => new CountryAnswer(r, players[r.CountryId].Decide(r))).ToList());
                game.Resolve(envoys.Select(r => new CountryAnswer(r, players[r.CountryId].Decide(r))).ToList());
            }
            Assert.That(game.Result.Ending, Is.Not.EqualTo(Ending.Domination));
            Assert.That(game.Reports.Count(r => r.ProvinceCounts.Values.Max() >= 15), Is.GreaterThan(1),
                "a country held 15 and the game went on");
            var first = new GameSetup { Victory = VictoryMode.Conquest }.NewGame().BeginTurn()[0].StandingFacts[0];
            Assert.That(first, Is.EqualTo("Turn 1. We hold 3 of 25 provinces; the last country standing wins."));
        }

        [Test]
        public void TurnLimit_PlaysToTheLastTurnUnlessOneCountryIsLeft()
        {
            foreach (int seed in GameRunner.Seeds(10, 400))
            {
                var g = GameRunner.Play(new GameSetup { Seed = seed, Victory = VictoryMode.TurnLimit, TurnLimit = 30 }, "scripted", seed);
                if (g.Result.Ending == Ending.LastStanding) continue;
                Assert.That(g.Result.Ending, Is.EqualTo(Ending.TurnLimit), $"seed {seed}");
                Assert.That(g.Result.Turns, Is.EqualTo(30), $"seed {seed}");
                int most = g.Result.Provinces.Values.Max();
                Assert.That(g.Result.Provinces[g.Result.Winner], Is.EqualTo(most), $"seed {seed}");
            }
        }
    }
}

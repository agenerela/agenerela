using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using static GreyBoxStrategy.Tests.TestGames;

namespace GreyBoxStrategy.Tests
{
    /// <summary>Rules v2 slice R1a: the writs turn, armies as pieces, occupation and the first treaty terms.</summary>
    public sealed class WritTests
    {
        private static GameRules WritRules()
        {
            var r = Calm();
            r.Turn = TurnStyle.Writs;
            r.Victory = VictoryMode.Conquest;
            r.MaxTurns = 150;
            r.StalemateTurns = 20;
            return r;
        }

        private static Game NewWrits(int seed = 1, GameRules rules = null) => new Game(seed, MapDefinition.Standard(), rules ?? WritRules());

        /// <summary>Every request of the last turn played by <see cref="Turn"/>.</summary>
        private static List<CountryRequest> asked = new List<CountryRequest>();

        /// <summary>Plays one writs turn: <paramref name="decide"/> answers what it wants (null holds), the rest hold.</summary>
        private static TurnReport Turn(Game g, Func<CountryRequest, Move> decide = null)
        {
            asked = new List<CountryRequest>();
            var requests = g.BeginTurn();
            while (requests.Count > 0)
            {
                asked.AddRange(requests);
                requests = g.Submit(requests.Select(r => new CountryAnswer(r, decide?.Invoke(r) ?? (r.Moment == Moment.Envoy ? new Move(Actions.RefusePeace) : Move.Hold))).ToList());
            }
            return g.Resolve();
        }

        private static Army ArmyOf(Game g, string country) => g.World.ArmiesOf(country).OrderBy(a => a.Id).First();

        private static Func<CountryRequest, Move> Orders(params (string country, string action, string target)[] orders) => r =>
        {
            foreach (var (country, action, target) in orders)
            {
                if (r.CountryId != country) continue;
                bool army = action == Actions.March || action == "hold";
                if (army != (r.Moment == Moment.Army)) continue;
                if (r.Moment == Moment.Council && r.Ordered.Count > 0) continue; // one court order a turn here
                return action == "hold" ? Move.Hold : new Move(action, target);
            }
            return null;
        };

        /// <summary>Marks a country's armies as waiting for orders, so the next turn asks about them.</summary>
        private static void Ready(Game g, string country)
        {
            foreach (var a in g.World.ArmiesOf(country)) if (!a.Awaiting) a.Wake.Add("test");
        }

        private static void War(Game g, string a, string b)
        {
            g.World.SetRelation(a, b, Relation.War);
            g.World.War(a, b).Reset(g.World.Turn);
        }

        // --- Armies, writs and orders ----------------------------------------------------------

        [Test]
        public void Armies_StandAtTheCapitals_AndAskForOrders()
        {
            var g = NewWrits();
            Turn(g);
            foreach (var c in g.World.CountryList)
            {
                var armies = g.World.ArmiesOf(c.Id).ToList();
                Assert.That(armies.Count, Is.EqualTo(1), c.Id);
                Assert.That(g.World.Province(armies[0].Location).IsCapital, Is.True, c.Id);
                Assert.That(armies[0].Soldiers, Is.EqualTo(30));
                Assert.That(armies[0].Id, Does.EndWith("_host"));
            }
            Assert.That(asked.Count(r => r.Moment == Moment.Army), Is.EqualTo(5), "every new army is asked once on the first turn");
            Assert.That(asked.Where(r => r.Moment == Moment.Council).All(r => !r.Legal.Any(m => m.Action == Actions.Attack)), Is.True, "armies attack, not the court");
        }

        [Test]
        public void Writs_ARulerIsNeverAskedMoreThanItsWrits()
        {
            var g = NewWrits(5);
            var players = g.World.CountryList.ToDictionary(c => c.Id, c => (IPlayer)new ScriptedPlayer(Temperament.Expansionist));
            for (int t = 0; t < 40 && !g.IsOver; t++)
            {
                Turn(g, r => players[r.CountryId].Decide(r));
                foreach (var grp in asked.Where(r => r.Moment != Moment.Envoy).GroupBy(r => r.CountryId))
                {
                    int writs = grp.First().Writs;
                    Assert.That(writs, Is.InRange(2, 5));
                    Assert.That(grp.Count(), Is.LessThanOrEqualTo(writs), $"turn {g.World.Turn} {grp.Key}");
                    Assert.That(grp.Count(r => r.Moment == Moment.Army), Is.LessThanOrEqualTo(writs - 1), "one writ is kept for the court");
                    Assert.That(grp.First().Moment, Is.EqualTo(Moment.Council), "the court is asked first");
                    Assert.That(grp.Select(r => r.Ordered.Count), Is.Ordered, "each decision sees the ones before it");
                }
            }
        }

        [Test]
        public void Writs_CapTheArmiesAsked_OneWritKeptForTheCourt()
        {
            var g = NewWrits();
            Turn(g);
            var host = ArmyOf(g, "valen");
            for (int i = 0; i < 3; i++)
                g.World.Armies.Add(new Army("extra" + i + "_host", "valen", "highmoor", 10, g.World.Turn));
            g.World.Country("valen").Army = g.World.ArmiesOf("valen").Sum(a => a.Soldiers);
            Ready(g, "valen");
            Turn(g, r => r.Moment == Moment.Council && r.CountryId == "valen" && r.Ordered.Count == 0 ? new Move(Actions.RaiseTaxes) : null);
            var valen = asked.Where(r => r.CountryId == "valen" && r.Moment != Moment.Envoy).ToList();
            Assert.That(valen.Count(r => r.Moment == Moment.Army), Is.EqualTo(1), "2 writs: the court and one army of four waiting");
            Assert.That(valen.Count, Is.EqualTo(2));
            Assert.That(g.World.ArmiesOf("valen").Count(a => a.Awaiting), Is.EqualTo(3), "the others keep waiting");
        }

        [Test]
        public void Writs_GrowWithTheRealm()
        {
            var g = NewWrits();
            Assert.That(g.World.Writs("valen"), Is.EqualTo(2));
            foreach (var id in new[] { "stonereach", "fenmark", "westmarch", "harrow", "templegate" }) Give(g.World, id, "valen");
            Assert.That(g.World.Writs("valen"), Is.EqualTo(3), "8 provinces");
            foreach (var p in g.World.ProvinceList) p.Owner = "valen";
            Assert.That(g.World.Writs("valen"), Is.EqualTo(5), "25 provinces: the cap");
        }

        // --- Movement ---------------------------------------------------------------------------

        [Test]
        public void Movement_RoughGroundTakesTwoTurns_OpenGroundOne()
        {
            var g = NewWrits();
            Turn(g);
            var host = ArmyOf(g, "valen");
            Assert.That(g.World.Province("ashby").Info.Terrain, Is.EqualTo(Terrain.Hills));
            Assert.That(g.World.Province("brindle").Info.Terrain, Is.EqualTo(Terrain.Forest));

            Ready(g, "valen");
            Turn(g, Orders(("valen", Actions.March, "ashby")));
            Assert.That(host.Location, Is.EqualTo("highmoor"), "hills: still on the way after one turn");
            Turn(g);
            Assert.That(host.Location, Is.EqualTo("ashby"), "and there after two");
            Assert.That(host.Wake, Does.Contain("arrived"));

            Turn(g, Orders(("valen", Actions.March, "highmoor")));
            Turn(g);
            Turn(g, Orders(("valen", Actions.March, "brindle")));
            Assert.That(host.Location, Is.EqualTo("brindle"), "forest: one turn");
        }

        [Test]
        public void Movement_EveryArmyStepsAtTheSameMoment()
        {
            // Orsk's army leaves corwick as valen's walks in: it never meets it, and fights only the militia.
            var g = NewWrits();
            Turn(g);
            Give(g.World, "stonereach", "valen");
            var mine = ArmyOf(g, "valen");
            var theirs = ArmyOf(g, "orsk");
            mine.Location = "stonereach";
            mine.Soldiers = 60;
            g.World.Country("valen").Army = 60;
            theirs.Location = "corwick";
            theirs.Soldiers = 100;
            g.World.Country("orsk").Army = 100;
            War(g, "valen", "orsk");
            Ready(g, "valen");
            Ready(g, "orsk");
            var report = Turn(g, Orders(("valen", Actions.March, "corwick"), ("orsk", Actions.March, "saltmere")));
            Assert.That(theirs.Location, Is.EqualTo("saltmere"));
            Assert.That(mine.Location, Is.EqualTo("corwick"));
            Assert.That(report.Resolution.Any(l => l.Contains("Battle at corwick") && l.Contains("orsk's militia")), Is.True, string.Join("\n", report.Resolution));
            Assert.That(report.Resolution.Any(l => l.Contains("Battle at corwick") && l.Contains("against orsk ")), Is.False);
        }

        [Test]
        public void Movement_EnemiesCrossingOneBorderMeetOnIt()
        {
            var g = NewWrits();
            Turn(g);
            Give(g.World, "fenmark", "orsk");
            var mine = ArmyOf(g, "valen");
            var theirs = ArmyOf(g, "orsk");
            mine.Location = "brindle";
            theirs.Location = "fenmark";
            // Both provinces are open ground (forest, plains): one turn to enter, so both step this turn.
            War(g, "valen", "orsk");
            Ready(g, "valen");
            Ready(g, "orsk");
            var report = Turn(g, Orders(("valen", Actions.March, "fenmark"), ("orsk", Actions.March, "brindle")));
            Assert.That(report.Resolution.Count(l => l.Contains("Battle at the brindle-fenmark border") || l.Contains("Battle at the fenmark-brindle border")), Is.EqualTo(1), string.Join("\n", report.Resolution));
            bool mineCrossed = mine.Location == "fenmark", theirsCrossed = theirs.Location == "brindle";
            Assert.That(mineCrossed ^ theirsCrossed, Is.True, "the winner completes its step");
            var loser = mineCrossed ? theirs : mine;
            var winner = mineCrossed ? mine : theirs;
            if (g.World.FindArmy(loser.Id) != null)
            {
                Assert.That(loser.Location, Is.Not.EqualTo(loser == mine ? "fenmark" : "brindle"), "the loser never crosses");
                Assert.That(loser.Location, Is.Not.EqualTo(winner.Location), "and is beaten off the province it was leaving");
            }
        }

        [Test]
        public void Movement_AMarchIntoLandAtPeaceIsNeverOffered_AndIsRefusedWhenPeaceLandsFirst()
        {
            var g = NewWrits();
            Turn(g);
            Give(g.World, "stonereach", "valen");
            ArmyOf(g, "valen").Location = "stonereach";
            Ready(g, "valen");
            Turn(g);
            var valenArmyAsk = asked.FirstOrDefault(r => r.CountryId == "valen" && r.Moment == Moment.Army);
            if (valenArmyAsk != null) Assert.That(valenArmyAsk.Legal.Any(m => m.Target == "corwick"), Is.False, "corwick is orsk's, at peace");

            // At war, the march is offered; the same turn orsk's white peace is accepted, and the march is refused as it lands.
            War(g, "valen", "orsk");
            ArmyOf(g, "valen").Wake.Add("test");
            var report = Turn(g, r =>
                r.Moment == Moment.Army && r.CountryId == "valen" ? new Move(Actions.March, "corwick")
                : r.Moment == Moment.Council && r.CountryId == "orsk" && r.Ordered.Count == 0 ? new Move(Actions.OfferWhitePeace, "valen")
                : r.Moment == Moment.Envoy ? new Move(Actions.AcceptPeace) : null);
            var march = report.Outcomes.Single(o => o.Request.Moment == Moment.Army && o.Request.CountryId == "valen");
            Assert.That(march.Refused, Is.True, march.Text);
            Assert.That(ArmyOf(g, "valen").Location, Is.EqualTo("stonereach"));
        }

        // --- Occupation and treaties ------------------------------------------------------------

        private static Game AtWarWithOccupations(out Army valenHost)
        {
            var g = NewWrits();
            Turn(g);
            War(g, "valen", "orsk");
            g.World.Province("corwick").Occupier = "valen";
            valenHost = ArmyOf(g, "valen");
            return g;
        }

        [Test]
        public void Occupation_ChangesTheControllerAndNeverTheOwner()
        {
            var g = NewWrits();
            Turn(g);
            Give(g.World, "stonereach", "valen");
            var host = ArmyOf(g, "valen");
            host.Location = "stonereach";
            host.Soldiers = 80;
            g.World.Country("valen").Army = 80;
            War(g, "valen", "orsk");
            Ready(g, "valen");
            int orskIncome = g.World.Income(g.World.Country("orsk"));
            Turn(g, Orders(("valen", Actions.March, "corwick")));
            var corwick = g.World.Province("corwick");
            Assert.That(corwick.Owner, Is.EqualTo("orsk"));
            Assert.That(corwick.Occupier, Is.EqualTo("valen"));
            Assert.That(corwick.Controller, Is.EqualTo("valen"));
            Assert.That(g.World.Income(g.World.Country("orsk")), Is.EqualTo(orskIncome - 10), "no income from occupied land");
            Assert.That(WarScore.Of(g.World, "valen", "orsk"), Is.GreaterThan(0));
            Assert.That(WarScore.Of(g.World, "orsk", "valen"), Is.EqualTo(-WarScore.Of(g.World, "valen", "orsk")));

            // Freed when its owner's army walks in with no occupier's army there.
            host.Location = "stonereach";
            var orskHost = ArmyOf(g, "orsk");
            orskHost.Location = "orskhaven";
            Ready(g, "orsk");
            Turn(g, Orders(("orsk", Actions.March, "corwick")));
            Assert.That(corwick.Occupier, Is.Null);
        }

        [Test]
        public void Treaty_DemandOccupied_TransfersExactlyWhatIsOccupied()
        {
            var g = AtWarWithOccupations(out _);
            g.World.War("valen", "orsk").BattlesWon["valen"] = 5;
            g.World.Province("ashby").Occupier = null;
            var before = g.World.ProvinceList.ToDictionary(p => p.Id, p => (p.Owner, p.Occupier));
            Turn(g, r => r.CountryId == "valen" && r.Moment == Moment.Council && r.Ordered.Count == 0 ? new Move(Actions.DemandOccupied, "orsk")
                : r.Moment == Moment.Envoy ? new Move(Actions.AcceptPeace) : null);
            Assert.That(g.World.Province("corwick").Owner, Is.EqualTo("valen"));
            Assert.That(g.World.Province("corwick").Occupier, Is.Null);
            Assert.That(g.World.RelationBetween("valen", "orsk"), Is.EqualTo(Relation.Truce));
            var changed = g.World.ProvinceList.Where(p => before[p.Id] != (p.Owner, p.Occupier)).Select(p => p.Id).ToList();
            Assert.That(changed, Is.EquivalentTo(new[] { "corwick" }), "only the occupied province changed");
        }

        [Test]
        public void Treaty_WhitePeace_ReturnsEverything()
        {
            var g = AtWarWithOccupations(out _);
            g.World.Province("ashby").Occupier = "orsk";
            Turn(g, r => r.CountryId == "orsk" && r.Moment == Moment.Council && r.Ordered.Count == 0 ? new Move(Actions.OfferWhitePeace, "valen")
                : r.Moment == Moment.Envoy ? new Move(Actions.AcceptPeace) : null);
            Assert.That(g.World.ProvinceList.All(p => p.Occupier == null), Is.True);
            Assert.That(g.World.Province("corwick").Owner, Is.EqualTo("orsk"));
            Assert.That(g.World.Province("ashby").Owner, Is.EqualTo("valen"));
            Assert.That(g.World.RelationBetween("valen", "orsk"), Is.EqualTo(Relation.Truce));
        }

        [Test]
        public void Treaty_CedeOccupied_GivesTheOccupierWhatItHolds_AndARefusalChangesNothing()
        {
            var g = AtWarWithOccupations(out _);
            Turn(g, r => r.CountryId == "orsk" && r.Moment == Moment.Council && r.Ordered.Count == 0 ? new Move(Actions.CedeOccupied, "valen")
                : r.Moment == Moment.Envoy ? new Move(Actions.RefusePeace) : null);
            Assert.That(g.World.Province("corwick").Occupier, Is.EqualTo("valen"), "refused: nothing changes");
            Assert.That(g.World.AtWar("valen", "orsk"), Is.True);

            Turn(g, r => r.CountryId == "orsk" && r.Moment == Moment.Council && r.Ordered.Count == 0 ? new Move(Actions.CedeOccupied, "valen")
                : r.Moment == Moment.Envoy ? new Move(Actions.AcceptPeace) : null);
            Assert.That(g.World.Province("corwick").Owner, Is.EqualTo("valen"));
        }

        [Test]
        public void Treaty_ADemandAtNinetyIsEnforcedWithoutAsking()
        {
            var rules = WritRules();
            rules.EnforcedScore = 20;
            var g = NewWrits(1, rules);
            Turn(g);
            War(g, "valen", "orsk");
            g.World.Province("corwick").Occupier = "valen";
            g.World.War("valen", "orsk").BattlesWon["valen"] = 5;
            var report = Turn(g, r => r.CountryId == "valen" && r.Moment == Moment.Council && r.Ordered.Count == 0 ? new Move(Actions.DemandOccupied, "orsk") : null);
            Assert.That(asked.Any(r => r.Moment == Moment.Envoy), Is.False, "nobody was asked");
            Assert.That(g.World.Province("corwick").Owner, Is.EqualTo("valen"));
            Assert.That(report.Resolution.Any(l => l.StartsWith("Enforced treaty")), Is.True);
        }

        [Test]
        public void Treaty_TermsAreOfferedOnlyWhenTheyMayBeAsked()
        {
            var g = NewWrits();
            Turn(g);
            Turn(g);
            Assert.That(asked.Where(r => r.Moment == Moment.Council).SelectMany(r => r.Legal).Any(m => Actions.IsTerm(m.Action)), Is.False, "no war, no terms");
            War(g, "valen", "orsk");
            Turn(g);
            var valen = asked.First(r => r.CountryId == "valen" && r.Moment == Moment.Council);
            Assert.That(valen.Legal.Select(m => m.ToString()), Does.Contain("offer_white_peace orsk"));
            Assert.That(valen.Legal.Any(m => m.Action == Actions.DemandOccupied), Is.False, "nothing occupied, nothing to demand");
            Assert.That(valen.Legal.Any(m => m.Action == Actions.CedeOccupied), Is.False);
        }

        [Test]
        public void BrokenRealm_ItsOccupiersTakeWhatTheyHold()
        {
            var g = NewWrits();
            Turn(g);
            War(g, "valen", "orsk");
            War(g, "merrow", "orsk");
            g.World.Province("orskhaven").Occupier = "valen";
            g.World.Province("corwick").Occupier = "valen";
            g.World.Province("redcliff").Occupier = "merrow";
            var orskArmy = ArmyOf(g, "orsk");
            g.World.Armies.Remove(orskArmy);
            g.World.Country("orsk").Army = 0;
            var report = Turn(g);
            Assert.That(g.World.Country("orsk").Alive, Is.False);
            Assert.That(g.World.Province("orskhaven").Owner, Is.EqualTo("valen"));
            Assert.That(g.World.Province("corwick").Owner, Is.EqualTo("valen"));
            Assert.That(g.World.Province("redcliff").Owner, Is.EqualTo("merrow"));
            Assert.That(report.Resolution.Any(l => l.Contains("realm is broken")), Is.True);
        }

        // --- Wake conditions ------------------------------------------------------------------

        [Test]
        public void Wake_EveryConditionBuiltInR1Fires()
        {
            var seen = new HashSet<string>();
            for (int seed = 1; seed <= 6; seed++)
            {
                var g = NewWrits(seed);
                var players = g.World.CountryList.Select((c, i) => (c.Id, p: (IPlayer)new ScriptedPlayer(i % 2 == 0 ? Temperament.Expansionist : Temperament.Cautious)))
                    .ToDictionary(x => x.Id, x => x.p);
                for (int t = 0; t < 60 && !g.IsOver; t++)
                {
                    Turn(g, r => players[r.CountryId].Decide(r));
                    foreach (var a in g.World.Armies) foreach (string why in a.Wake) seen.Add(why);
                }
            }
            foreach (string why in new[] { "raised", "arrived", "fought a battle", "an enemy army came near", "war declared", "peace made", "reinforced" })
                Assert.That(seen, Does.Contain(why), string.Join(", ", seen));
            Assert.That(seen.Any(s => s.StartsWith("four turns")), Is.True, string.Join(", ", seen));
        }

        [Test]
        public void Wake_AHoldingArmyAsksAgainAfterFourTurns()
        {
            var g = NewWrits();
            Turn(g, Orders(("valen", "hold", null)));
            var host = ArmyOf(g, "valen");
            Assert.That(host.Awaiting, Is.False);
            for (int i = 0; i < 3; i++) Turn(g);
            Assert.That(host.Awaiting, Is.False);
            Turn(g);
            Assert.That(asked.Any(r => r.ArmyId == host.Id), Is.True, "asked again on the fourth turn after its order");
        }

        // --- Whole games ------------------------------------------------------------------------

        [Test]
        public void WholeGames_EndAndEveryOfferIsLegal([Values("mixed", "random")] string lineup)
        {
            for (int seed = 1; seed <= 4; seed++)
            {
                var setup = GameSetup.Writs(seed);
                var map = setup.BuildMap();
                var g = new Game(seed, map, setup.BuildRules(map));
                var players = Lineups.Seat(lineup, seed, map);
                while (!g.IsOver)
                {
                    Assert.That(g.World.Turn, Is.LessThan(setup.TurnCap), "ends at the cap at the latest");
                    // Every offer is checked when it is made: the world moves on once the turn lands.
                    Turn(g, r =>
                    {
                        if (r.Moment == Moment.Council || r.Moment == Moment.Army)
                            foreach (var m in r.Legal)
                            {
                                string why = r.Moment == Moment.Army ? WritLegality.WhyNotArmy(r.World, r.CountryId, r.ArmyId, m) : WritLegality.WhyNotCourt(r.World, r.CountryId, m);
                                Assert.That(why, Is.Null, $"turn {r.Turn} {r.CountryId} offered {m}");
                            }
                        return players[r.CountryId].Decide(r);
                    });
                    foreach (var c in g.World.CountryList.Where(c => c.Alive))
                        Assert.That(c.Army, Is.EqualTo(g.World.ArmiesOf(c.Id).Sum(a => a.Soldiers)), "the soldier count is the armies'");
                    Assert.That(g.World.Armies.All(a => a.Soldiers > 0 && a.Soldiers <= setup.BuildRules(map).ArmyMax), Is.True);
                }
                Assert.That(new[] { Ending.LastStanding, Ending.Stalemate, Ending.Unfinished }, Does.Contain(g.Result.Ending));
            }
        }

        [Test]
        public void WholeGames_ScriptedBeatsRandom()
        {
            int wins = 0;
            double scripted = 0, random = 0;
            for (int seed = 1; seed <= 10; seed++)
            {
                var g = GameRunner.Play(GameSetup.Writs(seed), "expansionist-vs-random", seed);
                if (g.Kinds[g.Result.Winner] == Lineups.Expansionist) wins++;
                foreach (var kv in g.Kinds)
                    if (kv.Value == Lineups.Expansionist) scripted += g.Result.Provinces[kv.Key];
                    else random += g.Result.Provinces[kv.Key] / 4.0;
            }
            Assert.That(wins, Is.GreaterThanOrEqualTo(8), "one expansionist against four random players");
            Assert.That(scripted, Is.GreaterThan(3 * random), "and it holds far more land than a random player");
        }

        [Test]
        public void WholeGames_AreDeterministicPerSeed()
        {
            var a = GameRunner.Play(GameSetup.Writs(9), "mixed", 0);
            var b = GameRunner.Play(GameSetup.Writs(9), "mixed", 0);
            Assert.That(b.Reports.SelectMany(r => r.Resolution), Is.EqualTo(a.Reports.SelectMany(r => r.Resolution)));
            Assert.That(b.Game.World.ControlSignature(), Is.EqualTo(a.Game.World.ControlSignature()));
        }

        [Test]
        public void WholeGames_PlayOnAGeneratedWorld()
        {
            var setup = GameSetup.Writs(3, GameSetup.GeneratedMapId);
            setup.Countries = 4;
            setup.Generator = new MapSettings { Size = MapSize.Small };
            var g = GameRunner.Play(setup, "mixed");
            Assert.That(g.Game.IsOver, Is.True);
            Assert.That(g.Reports.Sum(r => r.Battles), Is.GreaterThan(0));
        }

        [Test]
        public void Stalemate_EndsAGameWhereNothingChangesHands()
        {
            var rules = WritRules();
            rules.StalemateTurns = 3;
            var g = NewWrits(1, rules);
            while (!g.IsOver) Turn(g);
            Assert.That(g.Result.Ending, Is.EqualTo(Ending.Stalemate));
            Assert.That(g.Result.Turns, Is.EqualTo(3));
        }

        // Soldiers lost to desertion in this turn's upkeep (none while the treasury holds).
        private static int UpkeepLoss(Game g, string country) =>
            g.Reports.Last().Upkeep.Where(l => l.StartsWith(country + ": bankrupt")).Select(l => int.Parse(l.Split(' ')[2])).Sum();

        [Test]
        public void Events_ChangeTheArmiesThroughTheSoldierCount()
        {
            var g = NewWrits();
            Turn(g);
            int before = g.World.ArmiesOf("valen").Sum(a => a.Soldiers);
            g.World.Country("valen").Army -= 12; // as a plague would
            Turn(g);
            Assert.That(g.World.ArmiesOf("valen").Sum(a => a.Soldiers), Is.EqualTo(before - 12 - UpkeepLoss(g, "valen")));
            Assert.That(g.World.ArmiesOf("valen").Sum(a => a.Soldiers), Is.EqualTo(g.World.Country("valen").Army));
            before = g.World.ArmiesOf("valen").Sum(a => a.Soldiers);
            g.World.Country("valen").Army += 25; // as volunteers would
            Turn(g);
            Assert.That(g.World.ArmiesOf("valen").Sum(a => a.Soldiers), Is.EqualTo(before + 25 - UpkeepLoss(g, "valen")), "the gain reaches the armies");
            Assert.That(g.World.ArmiesOf("valen").Sum(a => a.Soldiers), Is.EqualTo(g.World.Country("valen").Army));
        }

        [Test]
        public void Setup_TheWritsTurnIsASetting_TodaysGameStaysTheDefault()
        {
            Assert.That(new GameSetup().Turn, Is.EqualTo(TurnStyle.OneMove));
            var writs = GameSetup.Writs(4);
            var back = GameSetup.FromJson(writs.ToJson());
            Assert.That(back.ToJson(), Does.Contain("\"turn\": \"writs\""));
            var rules = back.BuildRules(back.BuildMap());
            Assert.That((rules.Turn, rules.Victory, rules.StalemateTurns), Is.EqualTo((TurnStyle.Writs, VictoryMode.Conquest, 20)));
        }
    }
}

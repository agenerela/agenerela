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
            Assert.That(valen.Count(r => r.Moment == Moment.Army), Is.EqualTo(2), "3 writs: the court and two armies of four waiting");
            Assert.That(valen.Count, Is.EqualTo(3));
            Assert.That(g.World.ArmiesOf("valen").Count(a => a.Awaiting), Is.EqualTo(2), "the others keep waiting");
        }

        // The screen's "unlimited writs" for the person alone (#286): one country's own number, the others
        // on the formula.
        [Test]
        public void Writs_AnOverride_GivesOneCountryItsOwnNumber_TheOthersKeepTheFormula()
        {
            var rules = WritRules();
            rules.WritsOverride["valen"] = GameRules.UnlimitedWrits;
            rules.WritsOverride["orsk"] = 2;
            var g = NewWrits(1, rules);
            Assert.That(g.World.Writs("valen"), Is.EqualTo(GameRules.UnlimitedWrits));
            Assert.That(g.World.Writs("orsk"), Is.EqualTo(2));
            Assert.That(g.World.Writs("merrow"), Is.EqualTo(3), "no override: the formula");
            Assert.That(WritFacts.Standing(g.World, "valen")[0], Does.Contain("Writs: no limit this turn."));
            Turn(g);
            foreach (var c in new[] { "valen", "merrow" })
                for (int i = 0; i < 4; i++)
                    g.World.Armies.Add(new Army($"extra{i}_{c}_host", c, ArmyOf(g, c).Location, 10, g.World.Turn));
            foreach (var c in g.World.CountryList) c.Army = g.World.ArmiesOf(c.Id).Sum(a => a.Soldiers);
            Ready(g, "valen");
            Ready(g, "merrow");
            Turn(g, r => r.Moment == Moment.Council && r.Ordered.Count == 0 ? new Move(Actions.RaiseTaxes) : null);
            var valen = asked.Where(r => r.CountryId == "valen" && r.Moment != Moment.Envoy).ToList();
            var merrow = asked.Where(r => r.CountryId == "merrow" && r.Moment != Moment.Envoy).ToList();
            Assert.That(valen.Count(r => r.Moment == Moment.Army), Is.EqualTo(5), "no limit: every army waiting is asked");
            Assert.That(valen.Count(r => r.Moment == Moment.Council), Is.EqualTo(2), "and the second court order");
            Assert.That(g.World.ArmiesOf("valen").Count(a => a.Awaiting), Is.EqualTo(0));
            Assert.That(merrow.Count(r => r.Moment == Moment.Army), Is.EqualTo(2), "3 writs: two armies of five");
            Assert.That(merrow.Count, Is.EqualTo(3));
        }

        [Test]
        public void Writs_AnOverride_IsCopiedWithTheRules_NotShared()
        {
            var rules = WritRules();
            rules.WritsOverride["valen"] = 9;
            var copy = rules.Clone();
            copy.WritsOverride["valen"] = 1;
            copy.WritsOverride["orsk"] = 4;
            Assert.That(rules.WritsOverride["valen"], Is.EqualTo(9));
            Assert.That(rules.WritsOverride.ContainsKey("orsk"), Is.False);
        }

        [Test]
        public void Writs_GrowWithTheRealm()
        {
            var g = NewWrits();
            Assert.That(g.World.Writs("valen"), Is.EqualTo(3));
            foreach (var id in new[] { "stonereach", "fenmark", "westmarch", "harrow", "templegate" }) Give(g.World, id, "valen");
            Assert.That(g.World.Writs("valen"), Is.EqualTo(4), "8 provinces");
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

        // --- Pace, battle reports and snapshots (R1b's first part) --------------------------------

        [Test]
        public void Movement_OpenGroundTakesTwoStepsATurn_RoughGroundOne()
        {
            var g = NewWrits();
            Turn(g);
            foreach (var id in new[] { "fenmark", "harrow" }) Give(g.World, id, "valen");
            var host = ArmyOf(g, "valen");
            host.Location = "brindle";
            Ready(g, "valen");
            // brindle -> fenmark (plains) -> harrow (forest): both open, both ours.
            Turn(g, Orders(("valen", Actions.March, "harrow")));
            Assert.That(host.Location, Is.EqualTo("harrow"), "two open steps in one turn");

            // Rough ground ends the turn's steps: brindle -> fenmark (plains), then ashby (hills) waits.
            host.Location = "brindle";
            Ready(g, "valen");
            Turn(g, r => r.Moment == Moment.Army && r.CountryId == "valen" ? new Move(Actions.March, "ashby") : null);
            Assert.That(host.Path.Count == 0 || host.Location == "fenmark", Is.True, $"at {host.Location}");
            Assert.That(host.Location, Is.Not.EqualTo("ashby"), "hills take their two turns");
        }

        [Test]
        public void Battles_AreRecordedWithEveryTerm_AndWhyTheyWentAsTheyDid()
        {
            var records = new List<BattleRecord>();
            for (int seed = 1; seed <= 3; seed++)
            {
                var played = GameRunner.Play(GameSetup.Writs(seed), "mixed", seed);
                foreach (var report in played.Reports)
                {
                    Assert.That(report.BattleRecords.Count, Is.EqualTo(report.Battles), $"turn {report.Turn}: one record a battle");
                    records.AddRange(report.BattleRecords);
                }
            }
            Assert.That(records.Count, Is.GreaterThan(50));
            foreach (var b in records)
            {
                Assert.That(b.AttackStrength, Is.EqualTo(b.AttackerSoldiers * b.AttackRoll).Within(1e-6));
                Assert.That(b.DefenceStrength, Is.EqualTo(b.DefenderSoldiers * b.DefenceRoll + b.FortTerm + b.CapitalTerm).Within(1e-6));
                Assert.That(b.AttackerWon, Is.EqualTo(b.AttackStrength > b.DefenceStrength));
                Assert.That(b.AttackRoll, Is.InRange(0.8, 1.2));
                Assert.That(b.Province != null ^ b.Border != null, Is.True, "a province or a border");
                Assert.That(new[] { "army", "militia", "garrison" }, Does.Contain(b.DefenderKind));
                Assert.That(b.Why, Does.StartWith(b.AttackerWon ? b.Attacker + " won" : (b.Defender == null ? "the garrison held" : b.Defender)));
                Assert.That(b.Why, Does.Contain($"{b.AttackerSoldiers} against {b.DefenderSoldiers}"));
                Assert.That(b.AttackerLost, Is.InRange(0, b.AttackerSoldiers));
            }
            Assert.That(records.Any(b => b.Retreats.Count > 0), Is.True, "beaten armies fall back, and the record says where");
            Assert.That(records.Any(b => b.FortTerm > 0 && b.Why.Contains("fort")), Is.True);
        }

        // --- R3: unit types and the counter rule (rules v2 3.3), off by default ------------------

        private static GameRules Typed()
        {
            var r = WritRules();
            r.UnitTypes = true;
            return r;
        }

        [Test]
        public void Counter_PikesFacingAllRiders_FightAtOneAndAHalf_AndRidersFacingPikesAtAHalf()
        {
            var r = Typed();
            var riders = new UnitMix { Riders = 10 };
            var pikes = new UnitMix { Pikes = 10 };
            Assert.That(Units.Multiplier(r, UnitType.Pikes, riders), Is.EqualTo(1.5).Within(1e-9));
            Assert.That(Units.Multiplier(r, UnitType.Riders, pikes), Is.EqualTo(0.5).Within(1e-9));
            Assert.That(Units.Multiplier(r, UnitType.Bows, pikes), Is.EqualTo(1.5).Within(1e-9), "bows counter pikes");
            Assert.That(Units.Multiplier(r, UnitType.Riders, new UnitMix { Bows = 10 }), Is.EqualTo(1.5).Within(1e-9), "riders counter bows");
            Assert.That(Units.Multiplier(r, UnitType.Pikes, pikes), Is.EqualTo(1.0).Within(1e-9), "a type against itself");
            Assert.That(Units.Strength(r, pikes, riders), Is.EqualTo(10 * 1.0 * 1.5).Within(1e-9));
            Assert.That(Units.Strength(r, riders, pikes), Is.EqualTo(10 * 1.4 * 0.5).Within(1e-9));
            var mixed = new UnitMix { Pikes = 5, Riders = 3, Bows = 2 };
            Assert.That(Units.Multiplier(r, UnitType.Pikes, mixed), Is.EqualTo(1 + 0.5 * 0.3 - 0.5 * 0.2).Within(1e-9));
            Assert.That(Units.Describe(r, "valen", pikes, "orsk", riders), Is.EqualTo("valen's pikes broke orsk's riders (+50%)"));
        }

        [Test]
        public void Losses_ComeFromEachTypeInProportion_AndAPikesOnlyArmyStaysPikes()
        {
            var a = new Army("maren_host", "valen", "stonereach", 0, 1);
            a.Add(UnitType.Pikes, 50);
            a.Add(UnitType.Riders, 30);
            a.Add(UnitType.Bows, 20);
            a.Soldiers = 50;
            Assert.That((a.Pikes, a.Riders, a.Bows), Is.EqualTo((25, 15, 10)));
            a.Soldiers = 7;
            Assert.That(a.Pikes + a.Riders + a.Bows, Is.EqualTo(7));
            Assert.That(a.Pikes, Is.GreaterThanOrEqualTo(a.Riders));
            var old = new Army("tolly_host", "valen", "stonereach", 40, 1);
            old.Soldiers -= 15;
            Assert.That((old.Pikes, old.Riders, old.Bows), Is.EqualTo((25, 0, 0)), "with unit types off every soldier is a pike");
        }

        [Test]
        public void Riders_CostTwiceAPike_ToRecruitAndToKeep()
        {
            var g = NewWrits(1, Typed());
            var valen = g.World.Country("valen");
            int gps = g.World.Rules.GoldPerSoldier;
            valen.Gold = 100000;
            int cap = g.World.Levy(valen, UnitType.Pikes);
            Assert.That(g.World.Levy(valen, UnitType.Riders), Is.EqualTo(cap), "a full treasury raises the whole levy of either");
            valen.Gold = cap * gps;
            Assert.That(g.World.Levy(valen, UnitType.Pikes), Is.EqualTo(cap));
            Assert.That(g.World.Levy(valen, UnitType.Riders), Is.EqualTo(cap / 2), "the same gold buys half as many riders");
            ArmyOf(g, "valen").Add(UnitType.Riders, 20);
            valen.Army = g.World.ArmiesOf("valen").Sum(a => a.Soldiers);
            int ridersNow = g.World.ArmiesOf("valen").Sum(a => a.Riders);
            Assert.That(g.World.Upkeep(valen), Is.EqualTo((valen.Army + ridersNow) / g.World.Rules.SoldiersPerUpkeepGold), "a rider is kept as two soldiers");
            g.World.Rules.UnitTypes = false;
            Assert.That(g.World.Upkeep(valen), Is.EqualTo(valen.Army / g.World.Rules.SoldiersPerUpkeepGold));
            g.World.Rules.UnitTypes = true;

            valen.Gold = 200;
            var report = Turn(g, r => r.CountryId == "valen" && r.Moment == Moment.Council && r.Ordered.Count == 0 ? new Move(Actions.RecruitRiders) : null);
            int raised = g.World.ArmiesOf("valen").Sum(a => a.Riders) - 20;
            Assert.That(raised, Is.GreaterThan(0), "the levy joined as riders");
            Assert.That(report.Outcomes.Any(o => (o.Text ?? "").Contains($"raised {raised} riders")), Is.True,
                string.Join("; ", report.Outcomes.Select(o => o.Text)));
        }

        [Test]
        public void RecruitByType_IsOfferedOnlyWithUnitTypes_AndRecruitOnlyWithout()
        {
            bool Offers(Game g, string action) => WritLegality.CourtMoves(g.World, "valen").Any(m => m.Action == action);
            var off = NewWrits();
            off.World.Country("valen").Gold = 200;
            Assert.That(Offers(off, Actions.Recruit), Is.True);
            Assert.That(Actions.RecruitTypes.Any(a => Offers(off, a)), Is.False);
            Assert.That(WritLegality.WhyNotCourt(off.World, "valen", new Move(Actions.RecruitRiders)), Is.EqualTo("unit types are off"));

            var on = NewWrits(1, Typed());
            on.World.Country("valen").Gold = 200;
            Assert.That(Offers(on, Actions.Recruit), Is.False);
            Assert.That(Actions.RecruitTypes.All(a => Offers(on, a)), Is.True);
            on.World.Country("valen").Gold = on.World.Rules.MinLevy * on.World.Rules.GoldPerSoldier;
            Assert.That(Offers(on, Actions.RecruitPikes), Is.True);
            Assert.That(Offers(on, Actions.RecruitRiders), Is.False, "riders cost more than the treasury holds");
        }

        [Test]
        public void ABattle_CountsTheCounter_AndTheReportNamesIt()
        {
            var g = NewWrits(1, Typed());
            Turn(g);
            var host = ArmyOf(g, "valen");
            host.Soldiers = 0;
            host.Add(UnitType.Riders, 60);
            g.World.Country("valen").Army = 60;
            var target = g.World.ProvincesOf("orsk").OrderBy(p => p.Id).First();
            var orsk = ArmyOf(g, "orsk");
            orsk.Soldiers = 0;
            orsk.Add(UnitType.Pikes, 50);
            g.World.Country("orsk").Army = 50;
            orsk.Location = target.Id;
            host.Location = target.Id;
            War(g, "valen", "orsk");
            var report = Turn(g);
            var battle = report.BattleRecords.FirstOrDefault(b => b.Province == target.Id);
            Assert.That(battle, Is.Not.Null, string.Join("\n", report.Resolution));
            Assert.That(battle.CounterNote, Does.Contain("pikes broke").And.Contain("riders"));
            double ridersSide = battle.Attacker == "valen" ? battle.AttackCounter : battle.DefenceCounter;
            double pikesSide = battle.Attacker == "valen" ? battle.DefenceCounter : battle.AttackCounter;
            Assert.That(ridersSide, Is.LessThan(1), "riders facing pikes");
            Assert.That(pikesSide, Is.GreaterThan(1), "pikes facing riders");
            Assert.That(battle.Why, Does.Contain("pikes broke"));
            Assert.That(report.Resolution.Any(l => l.StartsWith("Battle at") && l.Contains("pikes broke")), Is.True);
        }

        [Test]
        public void UnitTypes_ScriptedPlayersRecruitEveryType_AndOnlyLegalMoves()
        {
            int riders = 0, bows = 0, pikes = 0;
            for (int seed = 1; seed <= 3; seed++)
            {
                var setup = GameSetup.Writs(seed);
                var map = setup.BuildMap();
                var rules = setup.BuildRules(map);
                rules.UnitTypes = true;
                var g = new Game(seed, map, rules);
                var players = map.Countries.Select((c, i) => (c.Id, p: new ScriptedPlayer(i % 2 == 0 ? Temperament.Expansionist : Temperament.Cautious)))
                    .ToDictionary(x => x.Id, x => x.p);
                for (int t = 0; t < 60 && !g.IsOver; t++)
                {
                    Turn(g, r => players[r.CountryId].Decide(r));
                    foreach (var r in asked)
                        Assert.That(r.Legal.Any(m => m.Action == Actions.Recruit), Is.False, "no plain recruit with unit types");
                    foreach (var a in g.World.Armies) { riders += a.Riders; bows += a.Bows; pikes += a.Pikes; }
                    foreach (var o in g.Reports.Last().Outcomes)
                        Assert.That(o.Text ?? "", Does.Not.Contain("not offered"));
                }
            }
            Assert.That((pikes > 0, riders > 0, bows > 0), Is.EqualTo((true, true, true)), $"pikes {pikes}, riders {riders}, bows {bows}");
        }

        [Test]
        public void UnitTypes_OffByDefault_AndOffPlaysTheGameAsBefore()
        {
            Assert.That(new GameRules().UnitTypes, Is.False);
            Assert.That(GameSetup.Writs(1).BuildRules(GameSetup.Writs(1).BuildMap()).UnitTypes, Is.False);
            var played = GameRunner.Play(GameSetup.Writs(2), "mixed", 2);
            Assert.That(played.Game.World.Armies.All(a => a.Riders == 0 && a.Bows == 0), Is.True);
            Assert.That(played.Reports.SelectMany(r => r.BattleRecords).All(b => b.CounterNote == null && b.AttackCounter == 1 && b.DefenceCounter == 1), Is.True);
        }

        [Test]
        public void Snapshots_HoldEveryArmyAndOccupation_AsTheCouncilSawThem()
        {
            var g = GameRunner.Play(GameSetup.Writs(2), "mixed", 2).Game;
            Assert.That(g.Snapshots.Count, Is.EqualTo(g.Reports.Count));
            Assert.That(g.Snapshots.Skip(1).All(s => s.Armies.Count > 0), Is.True);
            Assert.That(g.Snapshots.Any(s => s.Occupiers.Count > 0), Is.True);

            var live = NewWrits(4);
            for (int i = 0; i < 6; i++) Turn(live, r => r.Moment == Moment.Army ? r.Legal[0] : null);
            live.World.Country("valen").Army -= 7; // as an event would, before the next upkeep
            live.BeginTurn();
            var snap = live.Snapshots.Last();
            Assert.That(snap.Armies.Select(a => (a.Id, a.Location, a.Soldiers)), Is.EqualTo(live.World.Armies.Select(a => (a.Id, a.Location, a.Soldiers))));
            Assert.That(snap.Armies.Sum(a => a.Soldiers), Is.EqualTo(live.World.Alive.Sum(c => c.Army)), "taken after the armies are brought in step");
        }

        [Test]
        public void Pace_AClassicGameEndsWithinAboutAnHourOfPlay()
        {
            // Scripted players, 12 November: 78 turns on average over 40 games (123 before the tuning).
            var turns = new List<int>();
            for (int seed = 1; seed <= 12; seed++) turns.Add(GameRunner.Play(GameSetup.Writs(seed), "mixed", seed).Result.Turns);
            Assert.That(turns.Average(), Is.LessThan(100), string.Join(", ", turns));
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

        // The storyteller's dilemmas (S2) under the writs turn: offered between turns, answered by the
        // seat, landing at the next upkeep, and the soldiers it brings reaching an army.
        [Test]
        public void Storyteller_ADilemmaIsOfferedAndAnswered_InAWritsGame()
        {
            var settings = StorytellerSettings.EvenHand;
            settings.ChoiceChance = 1;
            var g = NewWrits();
            g.EventTable = false;
            g.Story = new StoryRules { Settings = settings, Choices = true };
            Turn(g);
            Turn(g);
            g.World.Country("valen").Gold = 100;
            Assert.That(g.WhyNotEvent("sellswords", "valen"), Is.Null);
            g.QueueEvent(new StoryEvent("sellswords", "valen", "Sellswords came over the hills to valen's gate.", null));

            var request = g.ChoiceRequests().Single();
            Assert.That((request.Moment, request.CountryId), Is.EqualTo((Moment.Choice, "valen")));
            Assert.That(request.Legal.Select(m => m.Action), Is.EqualTo(new[] { "hire_sellswords", "send_sellswords_away" }));
            g.SubmitChoices(new[] { new CountryAnswer(request, new Move("hire_sellswords")) });

            int before = g.World.ArmiesOf("valen").Sum(a => a.Soldiers);
            var report = Turn(g);
            var e = report.Events.Single();
            Assert.That(e.Choices["valen"], Is.EqualTo("hire_sellswords"));
            Assert.That(g.World.ArmiesOf("valen").Sum(a => a.Soldiers), Is.EqualTo(before + 14), "the sellswords join valen's armies");
            Assert.That(g.World.Country("valen").Army, Is.EqualTo(before + 14));
            Assert.That(report.Events.All(x => x.Source != EventRecord.FromTable), Is.True, "with the table off, no table events");
        }

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
        public void Setup_DominationAt75_IsANamedChoice_ConquestStaysTheDefault()
        {
            Assert.That(GameSetup.Writs(1).Victory, Is.EqualTo(VictoryMode.Conquest));
            var setup = GameSetup.Writs(6, victory: GameSetup.DominationPreset);
            var back = GameSetup.FromJson(setup.ToJson());
            var rules = back.BuildRules(back.BuildMap());
            Assert.That((rules.Victory, rules.ProvincesToWin, rules.MaxTurns), Is.EqualTo((VictoryMode.Domination, 19, 150)), "75% of 25 provinces, rounded up");
            var g = GameRunner.Play(back, "scripted");
            Assert.That(g.Result.Ending, Is.EqualTo(Ending.Domination).Or.EqualTo(Ending.LastStanding).Or.EqualTo(Ending.Stalemate));
            if (g.Result.Ending == Ending.Domination) Assert.That(g.Result.Provinces[g.Result.Winner], Is.GreaterThanOrEqualTo(19));
            Assert.Throws<ArgumentException>(() => GameSetup.Writs(1, victory: "kingmaker"));
        }

        [Test]
        public void Setup_TheWritsTurnIsASetting_TodaysGameStaysTheDefault()
        {
            Assert.That(new GameSetup().Turn, Is.EqualTo(TurnStyle.OneMove));
            var writs = GameSetup.Writs(4);
            var back = GameSetup.FromJson(writs.ToJson());
            Assert.That(back.ToJson(), Does.Contain("\"turn\": \"writs\""));
            var rules = back.BuildRules(back.BuildMap());
            Assert.That((rules.Turn, rules.Victory, rules.StalemateTurns), Is.EqualTo((TurnStyle.Writs, VictoryMode.Conquest, 12)));
            Assert.That(rules.TruceTurns, Is.EqualTo(3), "the writs turn's shorter truce");
            Assert.That(new GameSetup().BuildRules(MapDefinition.Standard()).TruceTurns, Is.EqualTo(5), "the one-move game keeps its own");
        }
    }
}

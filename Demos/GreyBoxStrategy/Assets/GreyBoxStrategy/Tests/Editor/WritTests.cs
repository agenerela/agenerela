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
        public void Recall_OfTheArmiesAGameAskedAnyway_ChangesNoRequestAndNoResult()
        {
            // What a replay now does for every turn of a model's recording: recall each army the recording shows was
            // asked, in its order. For armies the rules asked anyway, that must change nothing at all.
            List<string> Play(Dictionary<int, List<(string country, string army)>> recall, Dictionary<int, List<(string country, string army)>> asked)
            {
                var map = GameSetup.Writs(3).BuildMap();
                var g = new Game(3, map, GameSetup.Writs(3).BuildRules(map));
                var players = map.Countries.ToDictionary(c => c.Id, c => (IPlayer)new ScriptedPlayer(Temperament.Expansionist));
                var trace = new List<string>();
                for (int t = 0; t < 25 && g.Result == null; t++)
                {
                    var requests = g.BeginTurn();
                    int turn = g.World.Turn;
                    if (recall != null && recall.TryGetValue(turn, out var list))
                        foreach (var (country, army) in list) Assert.That(g.Recall(country, army), Is.Null);
                    while (requests.Count > 0)
                    {
                        foreach (var r in requests)
                        {
                            trace.Add($"{turn} {r.CountryId} {r.Moment} {r.ArmyId} | {string.Join(" / ", r.StandingFacts.Concat(r.TurnFacts))} | {string.Join(",", r.Legal)}");
                            if (asked != null && r.Moment == Moment.Army)
                            {
                                if (!asked.TryGetValue(turn, out var a)) asked[turn] = a = new List<(string, string)>();
                                a.Add((r.CountryId, r.ArmyId));
                            }
                        }
                        requests = g.Submit(requests.Select(r => new CountryAnswer(r, players[r.CountryId].Decide(r))).ToList());
                    }
                    var report = g.Resolve();
                    trace.AddRange(report.Resolution.Select(l => $"{turn} = {l}"));
                }
                return trace;
            }
            var firstAsked = new Dictionary<int, List<(string country, string army)>>();
            var plain = Play(null, firstAsked);
            Assert.That(firstAsked.Values.Sum(l => l.Count), Is.GreaterThan(20), "armies were asked");
            var again = Play(firstAsked, null);
            Assert.That(again, Is.EqualTo(plain), "the same requests, facts, offers and results");
        }

        [Test]
        public void Recall_AsksAHoldingArmyFirst_WithItsReason_OnlyBeforeTheCourtsFirstOrder()
        {
            CountryRequest FirstArmyAsk(Game g, string recall)
            {
                var first = g.BeginTurn();
                if (recall != null) Assert.That(g.Recall("valen", recall), Is.Null);
                var round = g.Submit(first.Select(r => new CountryAnswer(r, Move.Hold)).ToList());
                var ask = round.FirstOrDefault(r => r.CountryId == "valen" && r.Moment == Moment.Army);
                while (round.Count > 0) round = g.Submit(round.Select(r => new CountryAnswer(r, r.Moment == Moment.Envoy ? new Move(Actions.RefusePeace) : Move.Hold)).ToList());
                g.Resolve();
                return ask;
            }
            var g = NewWrits();
            Turn(g); // every army asked once and held: now holding, nothing wakes it
            var host = ArmyOf(g, "valen");
            Assert.That(host.Awaiting, Is.False);
            Assert.That(FirstArmyAsk(g, null), Is.Null, "a holding army is not asked");
            var asked = FirstArmyAsk(g, host.Id);
            Assert.That(asked?.ArmyId, Is.EqualTo(host.Id), "recalled, it is asked");
            Assert.That(string.Join(" ", asked.StandingFacts.Concat(asked.TurnFacts)), Does.Contain(Game.RecalledReason));

            var h = NewWrits();
            Turn(h);
            var requests = h.BeginTurn();
            Assert.That(h.Recall("valen", ArmyOf(h, "orsk").Id), Does.Contain("not one of valen's armies"));
            Assert.That(h.ArmyWrits("valen"), Is.EqualTo(h.World.Writs("valen") - 1));
            h.Submit(requests.Select(r => new CountryAnswer(r, Move.Hold)).ToList());
            Assert.That(h.Recall("valen", ArmyOf(h, "valen").Id), Does.Contain("first order").Or.Contain("not sitting"), "too late once the court has spoken");
        }


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

        // --- Pacing candidates (rules v2 page, "Why Classic stalls"), off by default ---------------

        private static int HeldUntilOwned(GameRules rules, int turns, out Game g)
        {
            g = NewWrits(1, rules);
            Turn(g);
            War(g, "valen", "orsk");
            g.World.Province("corwick").Occupier = "valen";
            for (int t = 1; t <= turns; t++)
            {
                var report = Turn(g);
                if (g.World.Province("corwick").Owner == "valen")
                {
                    Assert.That(report.Resolution.Any(l => l.Contains("has held corwick")), Is.True);
                    return t;
                }
            }
            return 0;
        }

        [Test]
        public void Occupation_HeldItsTurns_PassesToTheOccupier_AndNeverWhenOff()
        {
            var rules = WritRules();
            rules.OccupationBecomesOwnership = 3;
            Assert.That(HeldUntilOwned(rules, 6, out var g), Is.EqualTo(3), "the third turn end held makes it valen's");
            Assert.That(g.World.Province("corwick").Occupier, Is.Null);
            Assert.That(g.World.Province("corwick").OccupiedTurns, Is.Zero);

            Assert.That(WritRules().OccupationBecomesOwnership, Is.Zero, "off by default");
            Assert.That(HeldUntilOwned(WritRules(), 6, out var off), Is.Zero);
            Assert.That((off.World.Province("corwick").Owner, off.World.Province("corwick").OccupiedTurns), Is.EqualTo(("orsk", 6)), "still counted, never owned");
        }

        [Test]
        public void Occupation_ANewOccupier_StartsTheCountAgain()
        {
            var p = NewWrits().World.Province("corwick");
            p.Occupier = "valen";
            p.OccupiedTurns = 2;
            p.Occupier = "valen";
            Assert.That(p.OccupiedTurns, Is.EqualTo(2), "the same occupier keeps its count");
            p.Occupier = "merrow";
            Assert.That(p.OccupiedTurns, Is.Zero);
            p.OccupiedTurns = 2;
            p.Occupier = null;
            Assert.That(p.OccupiedTurns, Is.Zero, "freed land counts nothing");
        }

        private static Game EveryProvinceOccupied_ArmyAbroad(GameRules rules)
        {
            var g = NewWrits(1, rules);
            Turn(g);
            War(g, "valen", "orsk");
            War(g, "merrow", "orsk");
            foreach (var p in g.World.ProvincesOf("orsk").ToList()) p.Occupier = "valen";
            // Abroad in an enemy's land, so it is not sent home (it would find no home and disband) and meets no army.
            ArmyOf(g, "orsk").Location = g.World.ProvincesOf("merrow").OrderBy(p => p.IsCapital).ThenBy(p => p.Id)
                .First(p => !g.World.Armies.Any(x => x.Location == p.Id)).Id;
            Turn(g);
            return g;
        }

        [Test]
        public void BreakWhenAllOccupied_BreaksARealmWithAnArmyLeft_AndNotWhenOff()
        {
            var rules = WritRules();
            rules.BreakWhenAllOccupied = true;
            var on = EveryProvinceOccupied_ArmyAbroad(rules);
            Assert.That(on.World.Country("orsk").Alive, Is.False);
            Assert.That(on.World.ArmiesOf("orsk"), Is.Empty, "its armies are disbanded with it");
            Assert.That(on.World.ProvinceList.Where(p => p.Owner == "orsk"), Is.Empty);

            Assert.That(WritRules().BreakWhenAllOccupied, Is.False, "off by default");
            var off = EveryProvinceOccupied_ArmyAbroad(WritRules());
            Assert.That(off.World.Country("orsk").Alive, Is.True, "an army left saves the realm, as v2 5.1 has it");
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
            Turn(g); // the armies stand from the first turn
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
        public void ABattleRecord_KeepsEachSidesMixAndCommander_AsTheBattleBegan()
        {
            BattleRecord Fight(bool on)
            {
                var r = on ? Typed() : WritRules();
                r.Generals = on;
                var g = NewWrits(1, r);
                Turn(g);
                var host = ArmyOf(g, "valen");
                host.Soldiers = 0;
                host.Add(UnitType.Riders, 60);
                var target = g.World.ProvincesOf("orsk").OrderBy(p => p.Id).First();
                var orsk = ArmyOf(g, "orsk");
                orsk.Soldiers = 0;
                orsk.Add(UnitType.Pikes, 30);
                orsk.Add(UnitType.Bows, 20);
                g.World.Country("valen").Army = g.World.ArmiesOf("valen").Sum(x => x.Soldiers);
                g.World.Country("orsk").Army = g.World.ArmiesOf("orsk").Sum(x => x.Soldiers);
                orsk.Location = target.Id;
                host.Location = target.Id;
                War(g, "valen", "orsk");
                var report = Turn(g);
                return report.BattleRecords.First(b => b.Province == target.Id);
            }
            var battle = Fight(on: true);
            var valen = battle.Attacker == "valen" ? battle.AttackerMix : battle.DefenderMix;
            var orsk = battle.Attacker == "valen" ? battle.DefenderMix : battle.AttackerMix;
            Assert.That(valen.HasValue && orsk.HasValue, Is.True, "both mixes kept");
            Assert.That((valen.Value.Riders, valen.Value.Total), Is.EqualTo((60, 60)), "before the losses");
            Assert.That((orsk.Value.Pikes, orsk.Value.Bows), Is.EqualTo((30 + (battle.Attacker == "valen" ? battle.Militia : 0), 20)));
            Assert.That(battle.AttackerCommander, Does.Contain(" the "));
            Assert.That(battle.DefenderCommander, Does.Contain(" the "));
            var off = Fight(on: false);
            Assert.That((off.AttackerMix, off.DefenderMix, off.AttackerCommander, off.DefenderCommander), Is.EqualTo(((UnitMix?)null, (UnitMix?)null, (string)null, (string)null)), "off");
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

        // --- R3: generals with traits (rules v2 3.5), off by default --------------------------------

        private static GameRules Led(bool types = false)
        {
            var r = WritRules();
            r.Generals = true;
            r.UnitTypes = types;
            return r;
        }

        private static void Lead(Army a, params string[] traits)
        {
            a.General = new General { Name = "Test" };
            a.General.Traits.AddRange(traits);
        }

        [Test]
        public void Generals_EveryArmyHasOne_NamedForIt_WithATrait_AndNoneWhenOff()
        {
            Assert.That(new GameRules().Generals, Is.False, "off by default");
            var g = NewWrits(1, Led());
            Turn(g);
            Assert.That(g.World.Armies, Is.Not.Empty);
            foreach (var a in g.World.Armies)
            {
                Assert.That(a.General, Is.Not.Null, a.Id);
                Assert.That(a.Id.StartsWith(a.General.Name.ToLowerInvariant()), Is.True, $"{a.General.Name} leads {a.Id}");
                Assert.That(a.General.Traits.Count, Is.EqualTo(1));
                Assert.That(GeneralTraits.All, Does.Contain(a.General.Traits[0]));
            }
            var off = NewWrits(1);
            Turn(off);
            Assert.That(off.World.Armies.All(a => a.General == null), Is.True);
            Assert.That(WritFacts.Standing(g.World, "valen").Any(l => l.StartsWith("Our armies:") && l.Contains(", led by ")), Is.True, "the court reads its generals");
            Assert.That(WritFacts.Standing(off.World, "valen").Any(l => l.Contains("led by")), Is.False);
        }

        [Test]
        public void Traits_CountInStrengthAndCasualties_AsThePageHasThem()
        {
            var r = Led(types: true);
            var g = new General { Name = "Test" };
            var mix = new UnitMix { Pikes = 50, Riders = 50 };
            Assert.That(GeneralTraits.Strength(r, null, true, mix), Is.EqualTo(1));
            g.Traits.Add(GeneralTraits.Bold);
            Assert.That(GeneralTraits.Strength(r, g, true, mix), Is.EqualTo(1.10).Within(1e-9), "bold attacking");
            Assert.That(GeneralTraits.Strength(r, g, false, mix), Is.EqualTo(1.0).Within(1e-9), "bold defending gains nothing");
            Assert.That(GeneralTraits.Casualties(r, g, true), Is.EqualTo(1.10).Within(1e-9), "bold loses more attacking");
            var wary = new General { Name = "Test", Traits = { GeneralTraits.Wary } };
            Assert.That(GeneralTraits.Strength(r, wary, false, mix), Is.EqualTo(1.10).Within(1e-9));
            Assert.That(GeneralTraits.Strength(r, wary, true, mix), Is.EqualTo(1.0).Within(1e-9));
            var horse = new General { Name = "Test", Traits = { GeneralTraits.HorseBorn } };
            double riderShare = 50 * r.RiderStrength / (50 * r.PikeStrength + 50 * r.RiderStrength);
            Assert.That(GeneralTraits.Strength(r, horse, true, mix), Is.EqualTo(1 + 0.15 * riderShare).Within(1e-9), "horse-born: riders +15%");
            var inspiring = new General { Name = "Test", Traits = { GeneralTraits.Inspiring } };
            Assert.That(GeneralTraits.Casualties(r, inspiring, false), Is.EqualTo(0.8).Within(1e-9));
            Assert.That(new General { Name = "Maren", Traits = { GeneralTraits.Wary, GeneralTraits.Bold } }.Title, Is.EqualTo("Maren the Wary and Bold"));
        }

        [Test]
        public void ABattle_CountsTheGenerals_AndTheReportNamesThem()
        {
            var g = NewWrits(1, Led());
            Turn(g);
            var target = g.World.ProvincesOf("orsk").OrderBy(p => p.Id).First();
            var host = ArmyOf(g, "valen");
            var orsk = ArmyOf(g, "orsk");
            host.Soldiers = 60;
            orsk.Soldiers = 50;
            g.World.Country("valen").Army = 60;
            g.World.Country("orsk").Army = 50;
            Lead(host, GeneralTraits.Bold);
            Lead(orsk, GeneralTraits.Wary);
            orsk.Location = target.Id;
            host.Location = target.Id;
            War(g, "valen", "orsk");
            var report = Turn(g);
            var battle = report.BattleRecords.FirstOrDefault(b => b.Province == target.Id);
            Assert.That(battle, Is.Not.Null, string.Join("\n", report.Resolution));
            double valen = battle.Attacker == "valen" ? battle.AttackGeneral : battle.DefenceGeneral;
            double them = battle.Attacker == "valen" ? battle.DefenceGeneral : battle.AttackGeneral;
            Assert.That((valen, them), Is.EqualTo(battle.Attacker == "valen" ? (1.10, 1.10) : (1.0, 1.0)).Within(1e-9),
                "bold counts attacking, wary defending");
            if (battle.Attacker == "valen")
            {
                Assert.That(battle.GeneralNote, Does.Contain("Test the "));
                Assert.That(report.Resolution.Any(l => l.StartsWith("Battle at") && l.Contains("Test the ")), Is.True);
            }
        }

        [Test]
        public void Stubborn_IsNeverRouted_ButRetreats()
        {
            bool Routed(bool stubborn)
            {
                var g = NewWrits(1, Led());
                Turn(g);
                var target = g.World.ProvincesOf("orsk").Where(p => !p.IsCapital && p.Forts == 0).OrderBy(p => p.Id).First();
                var host = ArmyOf(g, "valen");
                var orsk = ArmyOf(g, "orsk");
                g.World.Rules.RoutRatio = 1.0; // any beaten army is routed, so the test needs no exact odds
                host.Soldiers = 300; // wins, and takes at most 30% of 345 (104) from the 120
                orsk.Soldiers = 120;
                g.World.Country("valen").Army = 300;
                g.World.Country("orsk").Army = 120;
                Lead(host);
                if (stubborn) Lead(orsk, GeneralTraits.Stubborn); else Lead(orsk);
                orsk.Location = target.Id;
                host.Location = target.Id;
                War(g, "valen", "orsk");
                var report = Turn(g);
                var battle = report.BattleRecords.FirstOrDefault(b => b.Province == target.Id);
                Assert.That(battle, Is.Not.Null, string.Join("\n", report.Resolution));
                if (stubborn) Assert.That(battle.Retreats.ContainsKey(orsk.Id) && g.World.FindArmy(orsk.Id) != null, Is.True, "it fell back: " + string.Join("\n", report.Resolution));
                else Assert.That(g.World.FindArmy(orsk.Id), Is.Null);
                return battle.Routed;
            }
            Assert.That(Routed(false), Is.True, "a beaten army is routed");
            Assert.That(Routed(true), Is.False, "a stubborn general retreats instead");
        }

        [Test]
        public void Swift_TakesATurnOffAMarchsFirstStep()
        {
            for (int seed = 1; seed <= 10; seed++)
            {
                var probe = NewWrits(seed, Led());
                Turn(probe);
                var army = ArmyOf(probe, "valen");
                var slow = WritLegality.ArmyMoves(probe.World, "valen", army.Id).Where(m => m.Action == Actions.March)
                    .FirstOrDefault(m => probe.World.Rules.TurnsToEnter(probe.World.Province(WritLegality.Paths(probe.World, "valen", army.Location, probe.World.Rules.MarchRange)[m.Target][0]).Info.Terrain) >= 2);
                if (slow == null) continue;

                string first = WritLegality.Paths(probe.World, "valen", army.Location, probe.World.Rules.MarchRange)[slow.Target][0];
                string After(bool swift)
                {
                    var g = NewWrits(seed, Led());
                    Turn(g);
                    var a = ArmyOf(g, "valen");
                    if (swift) Lead(a, GeneralTraits.Swift); else Lead(a);
                    a.Stop("ready for the test"); // so the marshal is asked this turn
                    Turn(g, r => r.CountryId == "valen" && r.Moment == Moment.Army && r.ArmyId == a.Id ? slow : null);
                    Assert.That(a.Location == army.Location ? a.Destination : slow.Target, Is.EqualTo(slow.Target), "the march was ordered");
                    return a.Location;
                }
                Assert.That(After(false), Is.EqualTo(army.Location), $"seed {seed}: rough ground takes two turns");
                Assert.That(After(true), Is.EqualTo(first), $"seed {seed}: a swift general's first step takes one");
                return;
            }
            Assert.Inconclusive("no rough first step for valen on seeds 1 to 10");
        }

        [Test]
        public void Generals_InScriptedGames_GainASecondTrait_AndSomeFall()
        {
            int second = 0, fell = 0, notes = 0;
            for (int seed = 1; seed <= 6; seed++)
            {
                var played = GameRunner.Play(seed, GameSetup.Writs(seed).BuildMap().Countries.ToDictionary(c => c.Id, c => (IPlayer)new ScriptedPlayer(Temperament.Expansionist)),
                    GameSetup.Writs(seed).BuildMap(), Led());
                fell += played.Reports.SelectMany(r => r.Resolution).Count(l => l.Contains(" fell; "));
                notes += played.Reports.SelectMany(r => r.BattleRecords).Count(b => b.GeneralNote != null);
                second += played.Game.World.Armies.Count(a => a.General != null && a.General.Traits.Count == 2);
            }
            Assert.That((second > 0, fell > 0, notes > 0), Is.EqualTo((true, true, true)), $"second traits {second}, fallen {fell}, battles with a general's note {notes}");
        }

        // --- R3: sieges at forts (rules v2 3.7), off by default ------------------------------------

        private static GameRules Walled(bool on = true)
        {
            var r = WritRules();
            r.Sieges = on;
            r.SiegeBreachChance = 0; // no early breach, so the turns are exact
            return r;
        }

        // valen at war with orsk, a 20-soldier army of valen alone in an orsk province with two forts.
        private static (Game g, Province p, Army a) Besieging(GameRules rules)
        {
            var g = NewWrits(1, rules);
            Turn(g);
            var p = g.World.ProvincesOf("orsk").Where(x => !x.IsCapital).OrderBy(x => x.Id).First();
            p.Forts = 2;
            foreach (var o in g.World.ArmiesOf("orsk").ToList()) o.Location = g.World.ProvincesOf("orsk").First(x => x.IsCapital).Id;
            var a = ArmyOf(g, "valen");
            a.Soldiers = 20;
            g.World.Country("valen").Army = g.World.ArmiesOf("valen").Sum(x => x.Soldiers);
            a.Location = p.Id;
            War(g, "valen", "orsk");
            return (g, p, a);
        }

        [Test]
        public void Siege_AWalledProvince_TakesTwoTurnsAFort_ThenIsOccupied_OneFortLess()
        {
            var (g, p, a) = Besieging(Walled());
            Assert.That(Game.SiegeTurnsNeeded(g.World.Rules, p, new[] { a }), Is.EqualTo(4), "2 turns a fort, 20 soldiers do not outnumber 20 three to one");
            for (int t = 1; t <= 3; t++)
            {
                var report = Turn(g);
                Assert.That((p.SiegedBy, p.SiegeTurns, p.Occupier, p.Forts), Is.EqualTo(("valen", t, (string)null, 2)), $"turn {t} of the siege: " + string.Join("\n", report.Resolution));
                Assert.That(report.BattleRecords.Any(b => b.Province == p.Id), Is.False, "no storm of the militia");
            }
            Assert.That(WritFacts.Standing(g.World, "valen").Any(l => l.Contains($"besieging {p.Id}, turn 3 of 4")), Is.True, "the besieger reads its siege");
            Assert.That(WritFacts.Standing(g.World, "orsk").Any(l => l.StartsWith("valen:") && l.Contains($"it besieges our {p.Id}")), Is.True, "and the besieged");
            var last = Turn(g);
            Assert.That((p.SiegedBy, p.Occupier, p.Forts), Is.EqualTo(((string)null, "valen", 1)), string.Join("\n", last.Resolution));
            Assert.That(last.Resolution.Any(l => l.Contains($"took {p.Id} after a siege")), Is.True);

            var (off, op, _) = Besieging(Walled(false));
            Turn(off);
            Assert.That(op.SiegedBy, Is.Null, "off: the militia is stormed, as before");
        }

        [Test]
        public void Siege_NeverFallsTheTurnItBegins_HoweverLargeTheBesiegers_OrHoweverLikelyABreach()
        {
            var r = Walled();
            r.SiegeBreachChance = 1; // a breach every turn it may come
            var (g, p, a) = Besieging(r);
            p.Forts = 1;
            a.Soldiers = 200;
            g.World.Country("valen").Army = g.World.ArmiesOf("valen").Sum(x => x.Soldiers);
            var first = Turn(g);
            Assert.That((p.SiegedBy, p.Occupier), Is.EqualTo(("valen", (string)null)), "the turn it begins, the defender keeps it: " + string.Join("\n", first.Resolution));
            var second = Turn(g);
            Assert.That((p.SiegedBy, p.Occupier), Is.EqualTo(((string)null, "valen")), "it falls the turn after: " + string.Join("\n", second.Resolution));
        }

        [Test]
        public void Siege_IsBroken_WhenTheBesiegersLeave()
        {
            var (g, p, a) = Besieging(Walled());
            Turn(g);
            Assert.That(p.SiegedBy, Is.EqualTo("valen"));
            a.Location = g.World.ProvincesOf("valen").First().Id;
            var report = Turn(g);
            Assert.That((p.SiegedBy, p.SiegeTurns, p.Occupier), Is.EqualTo(((string)null, 0, (string)null)));
            Assert.That(report.Resolution.Any(l => l.Contains($"siege of {p.Id} by valen is broken")), Is.True, string.Join("\n", report.Resolution));
        }

        [Test]
        public void SiegeTurns_ASiegewrightAndThreeToOne_EachTakeATurnOff_NeverBelowTwo()
        {
            var r = Walled();
            r.Generals = true;
            var p = new Province(NewWrits().World.ProvinceList[0].Info) { Forts = 2 };
            var a = new Army("maren_host", "valen", p.Id, 20, 1);
            Assert.That(Game.SiegeTurnsNeeded(r, p, new[] { a }), Is.EqualTo(4));
            a.General = new General { Name = "Maren", Traits = { GeneralTraits.Siegewright } };
            Assert.That(Game.SiegeTurnsNeeded(r, p, new[] { a }), Is.EqualTo(3), "siegewright");
            a.Soldiers = 60;
            Assert.That(Game.SiegeTurnsNeeded(r, p, new[] { a }), Is.EqualTo(2), "and three to one against 20");
            p.Forts = 1;
            Assert.That(Game.SiegeTurnsNeeded(r, p, new[] { a }), Is.EqualTo(2), "never below 2: the turn it begins and one more");
            Assert.That(GeneralTraits.Pool(r), Does.Contain(GeneralTraits.Siegewright));
            Assert.That(GeneralTraits.Pool(WritRules()), Does.Not.Contain(GeneralTraits.Siegewright), "only with sieges");
        }

        [Test]
        public void Sieges_InScriptedGames_AreLaidAndWon()
        {
            int laid = 0, won = 0;
            for (int seed = 1; seed <= 6; seed++)
            {
                var map = GameSetup.Writs(seed).BuildMap();
                var rules = WritRules();
                rules.Sieges = true;
                var played = GameRunner.Play(seed, map.Countries.ToDictionary(c => c.Id, c => (IPlayer)new ScriptedPlayer(Temperament.Expansionist)), map, rules);
                var lines = played.Reports.SelectMany(r => r.Resolution).ToList();
                laid += lines.Count(l => l.Contains(" besieges "));
                won += lines.Count(l => l.Contains(" after a siege"));
            }
            Assert.That((laid > 0, won > 0), Is.EqualTo((true, true)), $"sieges laid {laid}, won {won}");
        }

        // --- R3: supply, model B (rules v2 3.6), off by default ------------------------------------

        private static GameRules Supplied()
        {
            var r = WritRules();
            r.Supply = true;
            return r;
        }

        // valen at war with orsk, valen's army alone in an orsk province valen already occupies: not supplied, no battle.
        private static (Game g, Province p, Army a) Abroad(GameRules rules)
        {
            var g = NewWrits(1, rules);
            Turn(g);
            var p = g.World.ProvincesOf("orsk").Where(x => !x.IsCapital).OrderBy(x => x.Id).First();
            War(g, "valen", "orsk");
            p.Occupier = "valen";
            foreach (var o in g.World.ArmiesOf("orsk").ToList()) o.Location = g.World.ProvincesOf("orsk").First(x => x.IsCapital).Id;
            var a = ArmyOf(g, "valen");
            a.Location = p.Id;
            return (g, p, a);
        }

        [Test]
        public void Supply_AnArmyHoldingAbroadIsAskedAgain_AsItsStoresRunLow_AndEveryTurnItStarves()
        {
            var (g, p, a) = Abroad(Supplied());
            int eat = Game.Eats(g.World, a);
            a.HalfStores = 2 * eat;
            a.Wake.Clear();
            Turn(g); // eats one turn's food: one turn left
            Assert.That(a.Wake, Does.Contain("stores running low"));
            a.Wake.Clear();
            a.HalfStores = 0;
            Turn(g);
            Assert.That(a.Wake, Does.Contain("starving"), "asked again while it starves");
            a.Wake.Clear();
            Turn(g);
            Assert.That(a.Wake, Does.Contain("starving"), "and the next turn too");
            var (h, _, full) = Abroad(Supplied());
            full.Wake.Clear();
            Turn(h);
            Assert.That(full.Wake, Is.Empty, "full stores: the hold stands");
        }

        [Test]
        public void Supply_WithGenerals_AnArmyReadsItsGeneralAndStoresWithOneComma()
        {
            var r = Supplied();
            r.Generals = true;
            var g = NewWrits(1, r);
            Turn(g);
            var line = WritFacts.Standing(g.World, "valen").First(l => l.StartsWith("Our armies:"));
            Assert.That(line, Does.Contain(", stores 4 of 4, at ").And.Not.Contain(",,"));
            Assert.That(asked.Where(q => q.Moment == Moment.Army).SelectMany(q => q.StandingFacts.Concat(q.TurnFacts)).Any(f => f.Contains(",,")), Is.False, "the army's own line too");
        }

        private static GameRules HungerFirst(bool on)
        {
            var r = Supplied();
            r.HungerFirst = on;
            return r;
        }

        [Test]
        public void HungerFirst_AStarvingArmyIsAskedFirst_ThenOneWithStoresRunningLow()
        {
            List<string> oldOrder = null;
            List<string> Order(bool on)
            {
                var (g, p, a) = Abroad(HungerFirst(on));
                var w = g.World;
                var home = w.ProvincesOf("valen").OrderBy(x => x.Id).First();
                var big = new Army("big_host", "valen", home.Id, 80, 1);
                var low = new Army("low_host", "valen", p.Id, 30, 1);
                w.Armies.Add(big);
                w.Armies.Add(low);
                foreach (var x in new[] { a, big, low }) { x.Wake.Clear(); x.Wake.Add("test"); }
                a.HalfStores = 0;                       // starving
                low.HalfStores = Game.Eats(w, low);     // one turn left
                a.Soldiers = 10;
                string Name(Army x) => x == a ? "starving" : x == low ? "low" : "big";
                var mine = new[] { a, big, low };
                // The order before hunger first: a hostile army beside it, then size, then id.
                oldOrder = mine.OrderByDescending(x => WritLegality.HostileBeside(w, x) ? 1 : 0).ThenByDescending(x => x.Soldiers)
                    .ThenBy(x => x.Id, StringComparer.Ordinal).Select(Name).ToList();
                return WritLegality.Awaiting(w, "valen").Where(mine.Contains).Select(Name).ToList();
            }
            Assert.That(Order(on: true), Is.EqualTo(new[] { "starving", "low", "big" }));
            var off = Order(on: false);
            Assert.That(off, Is.EqualTo(oldOrder), "off: the order as before");
            Assert.That(off[0], Is.Not.EqualTo("starving"), "and the setup tells them apart");
        }

        [Test]
        public void HungerFirst_AHungryArmyReadsWhatHungerCostsAndTheNearestLandThatFeedsIt()
        {
            List<string> ArmyFacts(bool on, int halfStores)
            {
                var (g, p, a) = Abroad(HungerFirst(on));
                a.HalfStores = halfStores;
                a.Wake.Clear();
                a.Wake.Add("test");
                Turn(g);
                return asked.Where(q => q.Moment == Moment.Army && q.ArmyId == a.Id).SelectMany(q => q.StandingFacts.Concat(q.TurnFacts)).ToList();
            }
            var (h, hp, ha) = Abroad(HungerFirst(true));
            ha.HalfStores = 0;
            var near = WritLegality.NearestSupply(h.World, ha);
            Assert.That(near, Is.Not.Null);
            Assert.That(h.World.Province(near.Value.province).Owner, Is.EqualTo("valen"));
            int loss = Math.Max(h.World.Rules.HungerMinLoss, (int)Math.Round(ha.Soldiers * h.World.Rules.HungerLoss));
            Assert.That(Game.HungerFact(h.World, ha), Does.StartWith($"Starving: losing about {loss} soldiers a turn")
                .And.Contain($"nearest {near.Value.province}, {near.Value.steps} step").And.Contain("land it occupies does not"));

            Assert.That(ArmyFacts(on: true, halfStores: 0), Has.Some.StartsWith("Starving: losing about"), "a starving army reads it in its request");
            var (lg, lp, la) = Abroad(HungerFirst(true));
            Assert.That(ArmyFacts(on: true, halfStores: Game.Eats(lg.World, la)), Has.Some.StartsWith("Stores for 1 more turn here"));
            Assert.That(ArmyFacts(on: true, halfStores: 8), Has.None.Contains("Starving").And.None.Contains("more turn here"), "full stores: nothing");
            Assert.That(ArmyFacts(on: false, halfStores: 0), Has.None.Contains("Starving: losing"), "off: no word of it");
        }

        [Test]
        public void Supply_StoresAreFullAtHome_AndEatenAbroad_TwiceAsFastInPoorLand()
        {
            var g = NewWrits(1, Supplied());
            Turn(g);
            Assert.That(g.World.Armies.All(a => a.Stores == 4), Is.True, "raised full, kept full at home");
            Assert.That(WritFacts.Standing(g.World, "valen").Any(l => l.StartsWith("Our armies:") && l.Contains("stores 4 of 4")), Is.True);
            var a = ArmyOf(g, "valen");
            var rich = g.World.ProvinceList.First(p => p.Owner != "valen" && (p.Info.Terrain == Terrain.Plains || p.Info.Terrain == Terrain.Forest));
            var poor = g.World.ProvinceList.First(p => p.Owner != "valen" && p.Info.Terrain != Terrain.Plains && p.Info.Terrain != Terrain.Forest);
            a.Location = rich.Id;
            Assert.That(Game.Eats(g.World, a), Is.EqualTo(2), "a turn of stores in rich land");
            a.Location = poor.Id;
            Assert.That(Game.Eats(g.World, a), Is.EqualTo(4), "two in poor land");
            g.World.Rules.Generals = true;
            a.General = new General { Name = "Test", Traits = { GeneralTraits.Forager } };
            Assert.That(Game.Eats(g.World, a), Is.EqualTo(2), "a forager eats half");
            a.Location = g.World.ProvincesOf("valen").First().Id;
            Assert.That(Game.Eats(g.World, a), Is.EqualTo(0), "nothing at home");

            var (h, p2, b) = Abroad(Supplied());
            int before = b.HalfStores;
            Turn(h);
            Assert.That(b.HalfStores, Is.EqualTo(before - Game.Eats(h.World, b)), "a turn abroad eats its share");
            int abroad = b.HalfStores;
            b.Location = h.World.ProvincesOf("valen").First().Id;
            Turn(h);
            Assert.That(b.HalfStores, Is.EqualTo(Math.Min(8, abroad + 4)), "home refills two a turn");
            Assert.That(WritFacts.Standing(NewWrits(1).World, "valen").Any(l => l.Contains("stores")), Is.False, "off: no word of stores");
        }

        [Test]
        public void Hunger_AnArmyWithNoStores_LosesATenth_AndFightsWeaker()
        {
            var (g, p, a) = Abroad(Supplied());
            a.Soldiers = 50;
            g.World.Country("valen").Army = g.World.ArmiesOf("valen").Sum(x => x.Soldiers);
            a.HalfStores = 0;
            var report = Turn(g);
            Assert.That(a.Soldiers, Is.EqualTo(45), string.Join("\n", report.Resolution));
            Assert.That(g.World.Country("valen").Army, Is.EqualTo(g.World.ArmiesOf("valen").Sum(x => x.Soldiers)), "the country's count follows");
            Assert.That(report.Resolution.Any(l => l.Contains($"{a.Id} has no stores at {p.Id}: 5 lost to hunger")), Is.True);
            a.Soldiers = 10;
            g.World.Country("valen").Army = g.World.ArmiesOf("valen").Sum(x => x.Soldiers);
            Turn(g);
            Assert.That(a.Soldiers, Is.EqualTo(8), "at least 2");

            var r = Supplied();
            var fed = new Army("fed_host", "valen", p.Id, 50, 1) { HalfStores = 8 };
            var hungry = new Army("hungry_host", "valen", p.Id, 50, 1) { HalfStores = 0 };
            Assert.That(Game.Hunger(r, new List<Army> { fed }), Is.EqualTo(1));
            Assert.That(Game.Hunger(r, new List<Army> { fed, hungry }), Is.EqualTo(1 - 0.15 / 2).Within(1e-9), "half the side hungry");
            Assert.That(GeneralTraits.Pool(r), Does.Contain(GeneralTraits.Forager));
            Assert.That(GeneralTraits.Pool(WritRules()), Does.Not.Contain(GeneralTraits.Forager), "only with supply");
        }

        [Test]
        public void Supply_InScriptedGames_HungryArmiesGoHome()
        {
            int homeward = 0, hunger = 0, stayed = 0;
            for (int seed = 1; seed <= 6; seed++)
            {
                var g = NewWrits(seed, Supplied());
                var players = g.World.CountryList.ToDictionary(c => c.Id, c => new ScriptedPlayer(Temperament.Expansionist));
                for (int t = 0; t < 60 && !g.IsOver; t++)
                {
                    var report = Turn(g, r =>
                    {
                        var m = players[r.CountryId].Decide(r);
                        var army = r.Moment == Moment.Army ? r.World.FindArmy(r.ArmyId) : null;
                        var at = army == null ? null : r.World.Province(army.Location);
                        bool hungryAbroad = army != null && army.Stores <= 1 && !Game.Supplied(at, r.CountryId) && at.SiegedBy != r.CountryId;
                        bool canGoHome = r.Legal.Any(x => x.Action == Actions.March && Game.Supplied(r.World.Province(x.Target), r.CountryId));
                        if (hungryAbroad && canGoHome)
                        {
                            if (m.Action == Actions.March && Game.Supplied(r.World.Province(m.Target), r.CountryId)) homeward++;
                            else stayed++;
                        }
                        return m;
                    });
                    hunger += report.Resolution.Count(l => l.Contains(" lost to hunger"));
                    Assert.That(g.World.Armies.All(a => a.HalfStores >= 0 && a.HalfStores <= 8), Is.True);
                }
            }
            Assert.That((homeward > 0, stayed), Is.EqualTo((true, 0)), $"hungry armies abroad that could go home: went {homeward}, stayed {stayed}; hunger losses {hunger}");
        }

        // --- R3: what a country sees of a rival army (rules v2 3.8), off by default ------------------

        private static Game Fogged(bool types = false)
        {
            var r = WritRules();
            r.Fog = true;
            r.Generals = true;
            r.UnitTypes = types;
            var g = NewWrits(1, r);
            Turn(g);
            return g;
        }

        [Test]
        public void Fog_RoundsToTen_AndSaysAFewUnderFive()
        {
            Assert.That((ArmySight.About(44), ArmySight.About(45), ArmySight.About(5), ArmySight.About(3), ArmySight.About(120)),
                Is.EqualTo(("about 40", "about 50", "about 10", "a few", "about 120")));
        }

        [Test]
        public void Fog_ARulerSeesArmiesNearItsLand_RoundedAndLed_AndNothingDeeperIn()
        {
            var g = Fogged();
            var w = g.World;
            var seen = ArmySight.Seen(w, "valen");
            var orsk = ArmyOf(g, "orsk");
            var near = w.ProvinceList.First(p => p.Controller != "valen" && seen.Contains(p.Id) && p.Info.Terrain != Terrain.Forest);
            var far = w.ProvinceList.First(p => !seen.Contains(p.Id));
            orsk.Soldiers = 44;
            orsk.Location = near.Id;
            string sighting = ArmySight.Sighting(w, "valen", orsk);
            Assert.That(sighting, Is.EqualTo($"orsk's {orsk.Id}, about 40, led by {orsk.General.Title}, at {near.Id}"));
            var facts = WritFacts.Standing(w, "valen");
            Assert.That(facts.Any(l => l.StartsWith("Rival armies we see: ") && l.Contains(orsk.Id)), Is.True, string.Join("\n", facts));
            orsk.Location = far.Id;
            Assert.That(ArmySight.Sighting(w, "valen", orsk), Is.Null, "nothing deeper in");
            Assert.That(WritFacts.Standing(w, "valen").Any(l => l.Contains(orsk.Id)), Is.False);
            Assert.That(ArmySight.ArmiesThere(w, "valen", far.Id), Is.Null, "a march order's detail hides it too");

            var forest = w.ProvinceList.FirstOrDefault(p => p.Controller != "valen" && seen.Contains(p.Id) && p.Info.Terrain == Terrain.Forest);
            if (forest != null)
            {
                orsk.Location = forest.Id;
                Assert.That(ArmySight.Sighting(w, "valen", orsk), Is.EqualTo($"an army of orsk's at {forest.Id}"), "in forest, only that an army is there");
            }

            var off = NewWrits(1);
            Turn(off);
            var plain = WritFacts.Standing(off.World, "valen");
            Assert.That(plain.Any(l => l.StartsWith("Rival armies we see")), Is.False, "off: as before");
        }

        [Test]
        public void Fog_TheMixShowsOnlyWhenOurRidersScout()
        {
            var g = Fogged(types: true);
            var w = g.World;
            var seen = ArmySight.Seen(w, "valen");
            var orsk = ArmyOf(g, "orsk");
            var near = w.ProvinceList.First(p => p.Controller != "valen" && seen.Contains(p.Id) && p.Info.Terrain != Terrain.Forest);
            orsk.Location = near.Id;
            orsk.Soldiers = 0;
            orsk.Add(UnitType.Riders, 30);
            Assert.That(ArmySight.Sighting(w, "valen", orsk), Does.Not.Contain("riders"), "no riders of ours beside it");
            var host = ArmyOf(g, "valen");
            host.Add(UnitType.Riders, 5);
            host.Location = near.Id;
            Assert.That(ArmySight.Sighting(w, "valen", orsk), Does.Contain("(30 riders)"), "our riders scout it");
        }

        // --- R3: the counters line, off by default ---------------------------------------------------

        private static Game Countered(bool on, bool types = true)
        {
            var r = WritRules();
            r.UnitTypes = types;
            r.CountersLine = on;
            var g = NewWrits(1, r);
            Turn(g);
            War(g, "valen", "orsk");
            foreach (var a in g.World.ArmiesOf("orsk")) { a.Soldiers = 0; a.Add(UnitType.Riders, 30); }
            return g;
        }

        [Test]
        public void CountersLine_SaysWhatBeatsWhat_AndWhatBeatsEachRivalItNames()
        {
            Assert.That(WritFacts.CountersLine(Countered(true).World, "valen"),
                Does.StartWith("Pikes beat riders, riders beat bows, and bows beat pikes;").And.Contain("orsk is mostly riders, which pikes beat"));
            var mixed = Countered(true);
            foreach (var a in mixed.World.ArmiesOf("orsk")) { a.Soldiers = 0; a.Add(UnitType.Riders, 10); a.Add(UnitType.Bows, 10); a.Add(UnitType.Pikes, 10); }
            Assert.That(WritFacts.CountersLine(mixed.World, "valen"), Does.Not.Contain("orsk"), "a mixed rival is not named");
            Assert.That(WritFacts.CountersLine(Countered(false).World, "valen"), Is.Null, "off");
            Assert.That(WritFacts.CountersLine(Countered(true, types: false).World, "valen"), Is.Null, "no unit types, no line");
        }

        [Test]
        public void CountersLine_AMarchNamesTheLargestTypeAmongTheEnemyArmiesThere()
        {
            string Detail(Game g)
            {
                var w = g.World;
                var host = ArmyOf(g, "valen");
                var orsk = ArmyOf(g, "orsk");
                string next = WritLegality.ArmyMoves(w, "valen", host.Id).First(m => m.Action == Actions.March && !m.Detail.StartsWith("ours")).Target;
                orsk.Location = next;
                return WritLegality.ArmyMoves(w, "valen", host.Id).Single(m => m.Action == Actions.March && m.Target == next).Detail;
            }
            Assert.That(Detail(Countered(true)), Does.Contain("enemy armies 30, mostly riders"));
            Assert.That(Detail(Countered(false)), Does.Contain("enemy armies 30").And.Not.Contain("mostly"), "off");
        }

        // --- R3: a fort holds the road, and a siege stops income; both off by default ------------------

        // A small road beside an orsk fort F: valen's Y, then X (neutral, no garrison) bordering F, then Z (neutral,
        // no garrison) beyond X. valen's host stands at Y; valen and orsk are at war.
        private static (Game g, Army host, Province f, Province x, Province y, Province z) Road(bool on)
        {
            var r = WritRules();
            r.Sieges = true;
            r.FortsHoldTheRoad = on;
            var g = NewWrits(1, r);
            Turn(g);
            War(g, "valen", "orsk");
            var w = g.World;
            foreach (var f in w.ProvincesOf("orsk").OrderBy(p => p.Id).ToList())
            foreach (var x in w.Neighbours(f).Where(n => n.Owner != "orsk").OrderBy(p => p.Id))
            {
                var rest = w.Neighbours(x).Where(n => n != f && !w.Neighbours(f).Contains(n) && n.Owner != "orsk" && !n.IsCapital).OrderBy(p => p.Id).ToList();
                if (rest.Count < 2 || x.IsCapital) continue;
                Province y = rest[0], z = rest[1];
                f.Forts = Math.Max(1, f.Forts);
                x.Owner = null; x.Occupier = null; x.Garrison = 0;
                y.Owner = "valen"; y.Occupier = null; y.Garrison = 0;
                z.Owner = null; z.Occupier = null; z.Garrison = 0;
                var host = ArmyOf(g, "valen");
                host.Location = y.Id;
                return (g, host, f, x, y, z);
            }
            Assert.Fail("no road beside an orsk province on this map");
            return default;
        }

        [Test]
        public void FortsHoldTheRoad_NoMarchIsOfferedPastAnEnemyFort_AndBesideItOnlyTheFortOrOurLand()
        {
            var (g, host, f, x, y, z) = Road(on: true);
            var w = g.World;
            var paths = WritLegality.Paths(w, "valen", y.Id, 3);
            Assert.That(paths.ContainsKey(x.Id), Is.True, "the land beside the fort can be reached");
            Assert.That(paths.Values.Any(p => p.Count > 1 && p[0] == x.Id), Is.False, "no march goes on through it");
            host.Location = x.Id;
            var beside = WritLegality.Paths(w, "valen", x.Id, 3);
            Assert.That(beside.Values.All(p => p[0] == f.Id || w.Province(p[0]).Controller == "valen"), Is.True, "only the fort, or back into our land");
            Assert.That(beside.ContainsKey(f.Id), Is.True, "the fort can be besieged");

            var (offGame, _, _, offX, offY, offZ) = Road(on: false);
            Assert.That(WritLegality.Paths(offGame.World, "valen", offY.Id, 3).Values.Any(p => p.Count > 1 && p[0] == offX.Id), Is.True,
                "off: the road through the land beside the fort is open");
            Assert.That(WritLegality.Paths(offGame.World, "valen", offX.Id, 3).ContainsKey(offZ.Id), Is.True, "off: from beside it, anywhere");
        }

        [Test]
        public void FortsHoldTheRoad_AMarchUnderWayStopsBesideAnEnemyFort_AndABesiegedFortHoldsNothing()
        {
            Army Marched(bool on, bool besieged)
            {
                var (g, host, f, x, y, z) = Road(on);
                if (besieged) f.SiegedBy = "kesh";
                host.Wake.Clear();
                host.Destination = z.Id;
                host.Path.Clear();
                host.Path.AddRange(new[] { x.Id, z.Id });
                host.StepTurnsLeft = 1;
                Turn(g);
                return host;
            }
            var held = Marched(on: true, besieged: false);
            Assert.That((held.Marching, held.Wake.Any(r => r.StartsWith("held up by "))), Is.EqualTo((false, true)), "stopped beside the fort, waiting for orders");
            Assert.That(Marched(on: false, besieged: false).Wake.Any(r => r.StartsWith("held up")), Is.False, "off: the march goes on");
            Assert.That(Marched(on: true, besieged: true).Wake.Any(r => r.StartsWith("held up")), Is.False, "a fort under siege holds no road");
        }

        [Test]
        public void SiegeStopsIncome_ABesiegedProvinceGivesItsOwnerNothing()
        {
            int Income(bool on, bool besieged)
            {
                var r = WritRules();
                r.Sieges = true;
                r.SiegeStopsIncome = on;
                var g = NewWrits(1, r);
                var p = g.World.ProvincesOf("valen").OrderBy(q => q.Id).First();
                if (besieged) p.SiegedBy = "orsk";
                return g.World.Income(g.World.Country("valen"));
            }
            int full = Income(on: true, besieged: false);
            Assert.That(Income(on: true, besieged: true), Is.EqualTo(full - WritRules().IncomePerProvince));
            Assert.That(Income(on: false, besieged: true), Is.EqualTo(full), "off: income as before");
        }

        // --- Pacing: a stalemate counter that reads war score and land by treaty, off by default -----

        // valen at war with orsk from turn 2; orsk's two other provinces pass to valen by holding on turns 2 and 3.
        // Returns the turn the game ended in a stalemate, or 0 if it had not by turn 5.
        private static int StalemateTurn(bool byWarScore, int step = 1000, bool battles = false)
        {
            var r = WritRules();
            r.StalemateTurns = 3;
            r.OccupationBecomesOwnership = 1;
            r.StalemateByWarScore = byWarScore;
            r.StalemateWarScoreStep = step;
            var g = NewWrits(1, r);
            Turn(g);
            War(g, "valen", "orsk");
            var taken = g.World.ProvincesOf("orsk").Where(p => !p.IsCapital).OrderBy(p => p.Id).Take(2).ToList();
            for (int t = 0; t < 4 && g.Result == null; t++)
            {
                if (t < taken.Count) taken[t].Occupier = "valen";
                if (battles) { var war = g.World.War("valen", "orsk"); war.BattlesWon["valen"] = war.Won("valen") + 1; } // a battle won each turn moves the score
                Turn(g);
            }
            return g.Result?.Ending == Ending.Stalemate ? g.Result.Turns : 0;
        }

        [Test]
        public void StalemateByWarScore_LandTakenByForce_NoLongerRestartsTheCounter_ButANewWarDoes()
        {
            Assert.That(new GameRules().StalemateByWarScore, Is.False, "off by default");
            Assert.That(StalemateTurn(false), Is.Zero, "off: land passing by holding on turn 3 keeps the game going past turn 5");
            Assert.That(StalemateTurn(true), Is.EqualTo(5), "on: only the war, from turn 2, restarted it");
        }

        [Test]
        public void StalemateByWarScore_AWarScoreThatMoves_RestartsTheCounter()
        {
            Assert.That(StalemateTurn(true, step: 1000, battles: true), Is.EqualTo(5), "a battle a turn moves the score less than the step");
            Assert.That(StalemateTurn(true, step: 1, battles: true), Is.Zero, "moving it by the step restarts the counter each turn");
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
        public void CallChoices_TheCourtGetsADilemmasOptionsWithTheCall_AndTheDefaultRegistersThem()
        {
            var settings = StorytellerSettings.EvenHand;
            settings.ChoiceChance = 1;
            var g = NewWrits();
            g.EventTable = false;
            g.Story = new StoryRules { Settings = settings, Choices = true };
            Turn(g);
            Turn(g);
            g.World.Country("valen").Gold = 100;
            g.QueueEvent(new StoryEvent("sellswords", "valen", "Sellswords came over the hills to valen's gate.", null));
            var request = g.ChoiceRequests().Single();
            var options = new[] { "hire_sellswords", "send_sellswords_away" };
            bool Registers(Agenerela.Agent a) => a.Actions.Definitions.Any(d => options.Contains(d.Id));

            var called = new WritRuler(g.World, "valen", new Agenerela.Providers.ScriptedProvider(), ModelSeatOptions.Named("default+storyteller+choices+call-choices"));
            Assert.That(called.OptionsFor(request).Choices?.Select(c => c.Id), Is.EqualTo(options), "the options come with the call (DR-022)");
            Assert.That(Registers(called.Court), Is.False);
            var registered = new WritRuler(g.World, "valen", new Agenerela.Providers.ScriptedProvider(), ModelSeatOptions.Named("default+storyteller+choices"));
            Assert.That(registered.OptionsFor(request).Choices, Is.Null);
            Assert.That(Registers(registered.Court), Is.True, "the default registers them, as before");
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

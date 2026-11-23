using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;
using static GreyBoxStrategy.Tests.TestGames;

namespace GreyBoxStrategy.Tests
{
    /// <summary>
    /// Merging and splitting armies (#20, 16 November): the human player's council orders, never a model court's. Each
    /// rule of <see cref="ArmyOrders"/> has a test here; the replay's are in the screen's tests.
    /// </summary>
    public sealed class ArmyOrderTests
    {
        // The writs turn with every army rule on, so types, generals, stores and sieges all count.
        private static GameRules Rules()
        {
            var r = Calm();
            r.Turn = TurnStyle.Writs;
            r.Victory = VictoryMode.Conquest;
            r.MaxTurns = 150;
            r.StalemateTurns = 20;
            r.UnitTypes = r.Generals = r.Supply = r.Sieges = true;
            r.SiegeBreachChance = 0;
            return r;
        }

        private static Game New(GameRules rules = null) => new Game(1, MapDefinition.Standard(), rules ?? Rules());

        private static void Answer(Game g, IReadOnlyList<CountryRequest> requests)
        {
            while (requests.Count > 0)
                requests = g.Submit(requests.Select(r => new CountryAnswer(r, r.Moment == Moment.Envoy ? new Move(Actions.RefusePeace) : Move.Hold)).ToList());
        }

        private static TurnReport Turn(Game g, Action<Game> inCouncil = null)
        {
            var requests = g.BeginTurn();
            inCouncil?.Invoke(g);
            Answer(g, requests);
            return g.Resolve();
        }

        // Two more armies of valen standing with its first, each with its own mix, general and stores.
        private static (Army big, Army mid, Army small) ThreeAt(Game g)
        {
            var w = g.World;
            var first = w.ArmiesOf("valen").OrderBy(a => a.Id).First();
            string at = first.Location;
            foreach (var a in w.ArmiesOf("valen").ToList()) w.Armies.Remove(a);
            Army Make(string id, int pikes, int riders, int bows, int halfStores, string trait)
            {
                var a = new Army(id, "valen", at, 0, w.Turn);
                a.Add(UnitType.Pikes, pikes); a.Add(UnitType.Riders, riders); a.Add(UnitType.Bows, bows);
                a.HalfStores = halfStores;
                a.General = new General { Name = char.ToUpperInvariant(id[0]) + id.Substring(1, id.IndexOf('_') - 1), Traits = { trait } };
                w.Armies.Add(a);
                return a;
            }
            var big = Make("toma_host", 30, 10, 0, 8, GeneralTraits.Bold);
            var mid = Make("milo_host", 10, 0, 15, 3, GeneralTraits.Wary);
            var small = Make("bea_host", 5, 5, 0, 0, GeneralTraits.Swift);
            w.Country("valen").Army = w.ArmiesOf("valen").Sum(a => a.Soldiers);
            return (big, mid, small);
        }

        // --- Merge ---------------------------------------------------------------------------------------------

        [Test]
        public void Merge_AddsSoldiersAndEachType_KeepsTheLargestAndItsGeneral_DismissesTheOthers()
        {
            var g = New();
            Turn(g);
            var requests = g.BeginTurn();
            var (big, mid, small) = ThreeAt(g);
            Assert.That(g.CanMerge("valen", new[] { small.Id, big.Id, mid.Id }), Is.Null);
            Assert.That(g.Merge("valen", new[] { small.Id, big.Id, mid.Id }), Is.Null);

            var left = g.World.ArmiesOf("valen").ToList();
            Assert.That(left.Select(a => a.Id), Is.EqualTo(new[] { "toma_host" }), "the largest stays; the others are gone");
            Assert.That((big.Soldiers, big.Pikes, big.Riders, big.Bows), Is.EqualTo((75, 45, 15, 15)), "soldiers and each type add up");
            Assert.That(big.General.Title, Is.EqualTo("Toma the Bold"), "the largest army's general leads");
            Assert.That(g.World.RetiredArmyIds, Does.Contain("milo_host").And.Contain("bea_host"), "their names are not given out again");
            Assert.That(g.World.Country("valen").Army, Is.EqualTo(75), "no soldier made or lost");

            var record = g.ArmyOrdersGiven.Single();
            Assert.That(record.Dismissed, Is.EqualTo(new[] { "Bea the Swift", "Milo the Wary" }));
            Assert.That(record.Text, Does.Contain("Bea the Swift and Milo the Wary are dismissed"), "the chronicle says so: " + record.Text);
            Answer(g, requests);
            var report = g.Resolve();
            Assert.That(report.ArmyOrders.Single(), Is.SameAs(record), "the turn's report keeps it");
        }

        [Test]
        public void Merge_StoresAreTheSoldierWeightedAverage_RoundedDown()
        {
            var g = New();
            Turn(g);
            g.BeginTurn();
            var (big, mid, small) = ThreeAt(g);
            // (40 x 8 + 25 x 3 + 10 x 0) / 75 = 395 / 75 = 5.27, rounded down to 5.
            Assert.That(g.Merge("valen", new[] { big.Id, mid.Id, small.Id }), Is.Null);
            Assert.That(big.HalfStores, Is.EqualTo(5));
        }

        [Test]
        public void Merge_OnATie_TheLowerIdLeads()
        {
            var g = New();
            Turn(g);
            g.BeginTurn();
            var (big, mid, _) = ThreeAt(g);
            mid.Soldiers = 0; mid.Add(UnitType.Pikes, 40);
            Assert.That(g.Merge("valen", new[] { big.Id, mid.Id }), Is.Null);
            Assert.That(g.World.ArmiesOf("valen").Select(a => a.Id), Has.Member("milo_host").And.No.Member("toma_host"), "milo_host < toma_host");
            Assert.That(g.ArmyOrdersGiven.Single().Army, Is.EqualTo("milo_host"));
        }

        [Test]
        public void Merge_IsRefusedWithAReason_AndChangesNothing()
        {
            var g = New();
            Turn(g);
            g.BeginTurn();
            var (big, mid, small) = ThreeAt(g);
            var orsk = g.World.ArmiesOf("orsk").First();
            string Why(params string[] ids) => g.CanMerge("valen", ids);
            Assert.That(Why(big.Id), Does.Contain("two armies or more"));
            Assert.That(Why(big.Id, big.Id), Does.Contain("two armies or more"), "the same army twice is one");
            Assert.That(Why(big.Id, orsk.Id), Does.Contain("not one of valen's armies"));
            Assert.That(Why(big.Id, "nobody_host"), Does.Contain("not one of valen's armies"));
            small.Destination = "somewhere";
            Assert.That(Why(big.Id, small.Id), Does.Contain("bea_host is marching"));
            small.Destination = null;
            string elsewhere = g.World.Neighbours(g.World.Province(mid.Location)).First().Id;
            string home = mid.Location;
            mid.Location = elsewhere;
            Assert.That(Why(big.Id, mid.Id), Does.Contain("do not stand in one province"));
            mid.Location = home;
            mid.Add(UnitType.Pikes, g.World.Rules.ArmyMax - 40);
            Assert.That(Why(big.Id, mid.Id), Does.Contain($"more than an army's {g.World.Rules.ArmyMax}"), "an army's cap holds");

            Assert.That(g.Merge("valen", new[] { big.Id, mid.Id }), Is.Not.Null);
            Assert.That(g.World.ArmiesOf("valen").Count(), Is.EqualTo(3), "a refused merge changes nothing");
            Assert.That(g.ArmyOrdersGiven, Is.Empty);
        }

        [Test]
        public void MergeAndSplit_OnlyWhileTheCouncilSits_BeforeTheCourtsFirstOrder()
        {
            var g = New();
            Turn(g);
            var (big, mid, _) = ThreeAt(g);
            Assert.That(g.CanMerge("valen", new[] { big.Id, mid.Id }), Is.EqualTo("the council is not sitting"), "between turns");
            Assert.That(g.CanSplit("valen", big.Id, 5), Is.EqualTo("the council is not sitting"));
            var requests = g.BeginTurn();
            var next = g.Submit(requests.Select(r => new CountryAnswer(r, Move.Hold)).ToList());
            Assert.That(g.CanMerge("valen", new[] { big.Id, mid.Id }), Does.Contain("first order"), "after the court's first order the orders are in");
            Assert.That(g.CanSplit("valen", big.Id, 5), Does.Contain("first order"));
            Answer(g, next);
            g.Resolve();

            var oneMove = new Game(1, MapDefinition.Standard(), Calm());
            oneMove.BeginTurn();
            Assert.That(oneMove.CanMerge("valen", new[] { "a", "b" }), Is.EqualTo("the council is not sitting"), "the one-move turn has no armies to merge");
        }

        [Test]
        public void Merge_ABesiegingArmy_WithANewcomer_KeepsTheSiegesProgress()
        {
            var r = Rules();
            r.Supply = false;
            var g = New(r);
            Turn(g);
            var p = g.World.ProvincesOf("orsk").Where(x => !x.IsCapital).OrderBy(x => x.Id).First();
            p.Forts = 2;
            foreach (var o in g.World.ArmiesOf("orsk").ToList()) o.Location = g.World.ProvincesOf("orsk").First(x => x.IsCapital).Id;
            var a = g.World.ArmiesOf("valen").OrderBy(x => x.Id).First();
            foreach (var o in g.World.ArmiesOf("valen").Where(x => x != a).ToList()) g.World.Armies.Remove(o);
            a.Soldiers = 20;
            a.Location = p.Id;
            g.World.SetRelation("valen", "orsk", Relation.War);
            g.World.War("valen", "orsk").Reset(g.World.Turn);
            var newcomer = new Army("edda_host", "valen", p.Id, 5, g.World.Turn) { General = new General { Name = "Edda", Traits = { GeneralTraits.Wary } } };
            g.World.Country("valen").Army = 20;
            Turn(g);
            Turn(g);
            Assert.That((p.SiegedBy, p.SiegeTurns), Is.EqualTo(("valen", 2)), "two turns of the siege");

            g.World.Armies.Add(newcomer);
            g.World.Country("valen").Army = 25;
            var report = Turn(g, x => Assert.That(x.Merge("valen", new[] { a.Id, newcomer.Id }), Is.Null));
            Assert.That((p.SiegedBy, p.SiegeTurns), Is.EqualTo(("valen", 3)), "the siege goes on from where it was: " + string.Join("\n", report.Resolution));
            Assert.That(g.World.ArmiesAt(p.Id).Single().Soldiers, Is.EqualTo(25));
        }

        // --- Split ---------------------------------------------------------------------------------------------

        [Test]
        public void Split_TakesNSoldiers_EachTypeInProportion_TotalsHold_SameStores_GeneralStays()
        {
            var g = New();
            Turn(g);
            g.BeginTurn();
            var (big, _, _) = ThreeAt(g);
            int before = g.World.ArmiesOf("valen").Count();
            // 30 pikes and 10 riders, 15 taken: 11.25 pikes and 3.75 riders, so 11 and 4.
            Assert.That(g.Split("valen", big.Id, 15), Is.Null);
            var record = g.ArmyOrdersGiven.Single();
            var fresh = g.World.FindArmy(record.Others.Single());
            Assert.That(fresh, Is.Not.Null);
            Assert.That((fresh.Soldiers, fresh.Pikes, fresh.Riders, fresh.Bows), Is.EqualTo((15, 11, 4, 0)));
            Assert.That((big.Soldiers, big.Pikes, big.Riders, big.Bows), Is.EqualTo((25, 19, 6, 0)), "the rest stays, type by type");
            Assert.That(fresh.Location, Is.EqualTo(big.Location), "in the same province");
            Assert.That(fresh.HalfStores, Is.EqualTo(big.HalfStores).And.EqualTo(8), "the same stores level for both");
            Assert.That(big.General.Title, Is.EqualTo("Toma the Bold"), "the general stays with the original");
            var raised = GeneralTraits.For(g.World, fresh);
            Assert.That(fresh.General, Is.Not.Null, "every army has a general: the new one gets its own");
            Assert.That((fresh.General.Title, fresh.General.CommandSince), Is.EqualTo((raised.Title, g.World.Turn)), "named as a newly raised army's is, in command from this turn");
            Assert.That(fresh.Marching || fresh.Awaiting, Is.False, "it holds where it stands");
            Assert.That(g.World.ArmiesOf("valen").Count(), Is.EqualTo(before + 1));
            Assert.That(g.World.Country("valen").Army, Is.EqualTo(g.World.ArmiesOf("valen").Sum(a => a.Soldiers)), "no soldier made or lost");
            Assert.That(record.Text, Does.Contain($"as {fresh.Id}, led by {fresh.General.Title}"));
        }

        [Test]
        public void Split_WithGeneralsOff_TheNewArmyHasNone()
        {
            var r = Rules();
            r.Generals = false;
            var g = New(r);
            Turn(g);
            g.BeginTurn();
            var a = g.World.ArmiesOf("valen").OrderByDescending(x => x.Soldiers).First();
            Assert.That(g.Split("valen", a.Id, a.Soldiers / 2), Is.Null);
            Assert.That(g.World.FindArmy(g.ArmyOrdersGiven.Single().Others.Single()).General, Is.Null);
        }

        [Test]
        public void Merge_TheSeniorGeneralLeads_LongestInCommand_ThenTheLarger_ThenTheLowerId()
        {
            var g = New();
            Turn(g);
            g.BeginTurn();
            var (big, mid, small) = ThreeAt(g);
            big.General.CommandSince = 5; mid.General.CommandSince = 2; small.General.CommandSince = 2;
            Assert.That(g.Merge("valen", new[] { big.Id, mid.Id, small.Id }), Is.Null);
            var kept = g.World.ArmiesOf("valen").Single();
            Assert.That(kept.Id, Is.EqualTo("milo_host"), "in command since turn 2, and larger than bea_host, also since 2");
            Assert.That(kept.General.Title, Is.EqualTo("Milo the Wary"));
            Assert.That(kept.Soldiers, Is.EqualTo(75));
            Assert.That(g.ArmyOrdersGiven.Single().Dismissed, Is.EqualTo(new[] { "Bea the Swift", "Toma the Bold" }));
        }

        [Test]
        public void SplittingOffTheMajority_AndMergingBack_NeverRerollsTheGeneral()
        {
            var g = New();
            Turn(g);
            Turn(g);
            var a = g.World.ArmiesOf("valen").OrderByDescending(x => x.Soldiers).First();
            a.Soldiers = 40;
            g.World.Country("valen").Army = g.World.ArmiesOf("valen").Sum(x => x.Soldiers);
            string before = a.General.Title;
            int since = a.General.CommandSince;
            Assert.That(since, Is.LessThan(g.World.Turn + 1), "in command before this turn");

            // Split off the larger part, so the new army is the bigger one with a fresh general, then merge back.
            var report = Turn(g, x =>
            {
                Assert.That(x.Split("valen", a.Id, 30), Is.Null);
                string fresh = x.ArmyOrdersGiven.Last().Others.Single();
                Assert.That(x.World.FindArmy(fresh).Soldiers, Is.GreaterThan(a.Soldiers), "the new army is the larger");
                Assert.That(x.World.FindArmy(fresh).General, Is.Not.Null, "the new army has its own general");
                Assert.That(x.Merge("valen", new[] { a.Id, fresh }), Is.Null);
            });
            var back = g.World.FindArmy(a.Id);
            Assert.That(back, Is.Not.Null, "the original army stays");
            Assert.That((back.General.Title, back.General.CommandSince), Is.EqualTo((before, since)), "its general leads, as before");
            Assert.That(back.Soldiers, Is.EqualTo(40));
            Assert.That(report.ArmyOrders.Last().Dismissed.Count, Is.EqualTo(1), "the split-off general is dismissed");
        }

        [Test]
        public void Split_IsRefusedWithAReason_OutsideOneToSizeLessOne_OrWhileMarching()
        {
            var g = New();
            Turn(g);
            g.BeginTurn();
            var (big, _, _) = ThreeAt(g);
            Assert.That(g.CanSplit("valen", big.Id, 0), Does.Contain("1 to 39"));
            Assert.That(g.CanSplit("valen", big.Id, 40), Does.Contain("1 to 39"));
            Assert.That(g.CanSplit("valen", big.Id, 1), Is.Null);
            Assert.That(g.CanSplit("valen", big.Id, 39), Is.Null);
            Assert.That(g.CanSplit("valen", g.World.ArmiesOf("orsk").First().Id, 5), Does.Contain("not one of valen's armies"));
            big.Destination = "somewhere";
            Assert.That(g.CanSplit("valen", big.Id, 5), Does.Contain("marching"));
            Assert.That(g.Split("valen", big.Id, 5), Is.Not.Null);
            Assert.That(g.ArmyOrdersGiven, Is.Empty, "a refused split changes nothing");
        }

        [Test]
        public void MergeAndSplit_CostNoWrit_AndOrderingTheNewArmyCostsItsWritAsUsual()
        {
            var g = New();
            Turn(g);
            var requests = g.BeginTurn();
            var (big, mid, small) = ThreeAt(g);
            int writs = g.ArmyWrits("valen");
            Assert.That(g.Merge("valen", new[] { mid.Id, small.Id }), Is.Null);
            Assert.That(g.Split("valen", big.Id, 10), Is.Null);
            Assert.That(g.ArmyWrits("valen"), Is.EqualTo(writs), "neither costs a writ");
            string fresh = g.ArmyOrdersGiven.Last().Others.Single();
            Assert.That(g.Recall("valen", fresh), Is.Null, "the new army can be given orders this turn, as any holding army");

            var asked = new List<CountryRequest>();
            while (requests.Count > 0)
            {
                asked.AddRange(requests);
                requests = g.Submit(requests.Select(r => new CountryAnswer(r, r.Moment == Moment.Army && r.ArmyId == fresh
                    ? r.Legal.First(m => m.Action == Actions.March) : r.Moment == Moment.Envoy ? new Move(Actions.RefusePeace) : Move.Hold)).ToList());
            }
            var mine = asked.Where(r => r.CountryId == "valen").ToList();
            Assert.That(mine.First(r => r.Moment == Moment.Army).ArmyId, Is.EqualTo(fresh), "asked first, as a recalled army is");
            Assert.That(mine.Last().Writ, Is.LessThanOrEqualTo(mine.Last().Writs), "every order within the turn's writs");
            Assert.That(mine.Count(r => r.Moment == Moment.Army), Is.LessThanOrEqualTo(writs), "the army orders spend the writs as always");
            g.Resolve();
            Assert.That(g.World.FindArmy(fresh)?.Marching == true || g.World.FindArmy(fresh)?.Location != big.Location, Is.True, "its march order landed");
        }

        // --- Nothing a model reads changes -------------------------------------------------------------------------

        // Every request a writs game asks, with every army rule on, as one hash: its facts and the moves it offers.
        private static string RequestsHash(int seed, int turns)
        {
            var g = new Game(seed, MapDefinition.Standard(), Rules());
            var sha = SHA256.Create();
            var text = new StringBuilder();
            var rng = new Random(seed);
            for (int t = 0; t < turns && !g.IsOver; t++)
            {
                var requests = g.BeginTurn();
                while (requests.Count > 0)
                {
                    foreach (var r in requests)
                    {
                        text.Append(r.Moment).Append('|').Append(r.CountryId).Append('|').Append(r.ArmyId).Append('\n');
                        foreach (var f in r.StandingFacts) text.Append(f).Append('\n');
                        foreach (var f in r.TurnFacts) text.Append(f).Append('\n');
                        foreach (var m in r.Legal) text.Append(m).Append(';');
                        text.Append('\n');
                        Assert.That(r.Legal.Any(m => m.Action.Contains("merge") || m.Action.Contains("split")), Is.False, "no model is offered a merge or a split");
                    }
                    requests = g.Submit(requests.Select(r => new CountryAnswer(r, r.Moment == Moment.Envoy ? new Move(Actions.RefusePeace)
                        : r.Legal.Where(m => m.Action != Actions.None).OrderBy(m => m.ToString(), StringComparer.Ordinal).Skip(rng.Next(3)).FirstOrDefault() ?? Move.Hold)).ToList());
                }
                g.Resolve();
            }
            return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text.ToString()))).Replace("-", "").ToLowerInvariant();
        }

        /// <summary>
        /// The requests of a 30-turn game, facts and offered moves, hashed before merge and split were built (test at
        /// 1e5ee33): any change to what a model court or marshal reads changes this hash.
        /// </summary>
        private const string RequestsBefore = "7b03b4fee209eb2a9f0f00e5982b9df3fa4662b44f92fe4bc33c5911287dd879";

        [Test]
        public void ModelCourts_ReadTheSameBytes_AsBeforeMergeAndSplit()
        {
            Assert.That(RequestsHash(3, 30), Is.EqualTo(RequestsBefore), "a model's facts or offered moves changed");
        }
    }
}

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using static GreyBoxStrategy.Tests.TestGames;

namespace GreyBoxStrategy.Tests
{
    /// <summary>Each rule of the design, one at a time, on the standard map with events switched off.</summary>
    public sealed class RulesTests
    {
        [Test]
        public void Map_HasFiveCountriesOfThreeProvincesAndTenNeutral()
        {
            var map = MapDefinition.Standard();
            Assert.That(map.Countries.Count, Is.EqualTo(5));
            Assert.That(map.Provinces.Count, Is.EqualTo(25));
            Assert.That(map.Provinces.Count(p => p.StartOwner == null), Is.EqualTo(10));
            foreach (var c in map.Countries)
            {
                Assert.That(map.Provinces.Count(p => p.StartOwner == c.Id), Is.EqualTo(3), c.Id);
                Assert.That(map.Provinces.Count(p => p.StartOwner == c.Id && p.IsCapital), Is.EqualTo(1), c.Id);
            }
        }

        [Test]
        public void Map_BordersAreSymmetricAndOrthogonal()
        {
            var map = MapDefinition.Standard();
            foreach (var p in map.Provinces)
            foreach (string n in map.Neighbours(p.Id))
            {
                Assert.That(map.Neighbours(n), Does.Contain(p.Id), $"{p.Id}-{n}");
                var q = map.Province(n);
                Assert.That(System.Math.Abs(p.Column - q.Column) + System.Math.Abs(p.Row - q.Row), Is.EqualTo(1), $"{p.Id}-{n}");
            }
            Assert.That(map.Neighbours("highmoor"), Is.EquivalentTo(new[] { "ashby", "brindle" }));
            Assert.That(map.Neighbours("merrowdeep"), Is.EquivalentTo(new[] { "templegate", "pilgrimsrest", "harrow", "glimmerford" }));
        }

        [Test]
        public void Upkeep_AddsIncomeAndTakesUpkeep()
        {
            var game = NewGame();
            game.BeginTurn();
            var valen = game.World.Country("valen");
            // 40 gold + 3 provinces × 10 − 30 soldiers / 2.
            Assert.That(valen.Gold, Is.EqualTo(40 + 30 - 15));
            Assert.That(valen.Stability, Is.EqualTo(4), "stability recovers one after a turn without taxes");
        }

        [Test]
        public void Recruit_RaisesALevyThatGrowsWithProvincesAndIsCutToTheTreasury()
        {
            var game = NewGame();
            var report = PlayTurn(game, ("valen", Actions.Recruit, null));
            var valen = game.World.Country("valen");
            // Levy 5 + 5 × 3 = 20 soldiers at 2 gold each, from 55 gold.
            Assert.That(valen.Army, Is.EqualTo(50));
            Assert.That(valen.Gold, Is.EqualTo(15));
            Assert.That(Outcome(report, "valen").Text, Does.Contain("recruited 20"));

            game.World.Country("orsk").Gold = 0;
            game.World.Country("orsk").Army = 0;
            Give(game.World, "stonereach", "orsk");
            Give(game.World, "saltmere", "orsk");
            var council = game.BeginTurn();
            // Orsk has 5 provinces now, so its levy would be 30, but it has 50 gold: 25 soldiers.
            Assert.That(game.World.Levy(game.World.Country("orsk")), Is.EqualTo(25));
            Finish(game, council, null);
        }

        [Test]
        public void Recruit_IsNotOfferedWithoutGoldForTheSmallestLevy()
        {
            var game = NewGame();
            game.World.Country("valen").Gold = -100;
            game.World.Country("valen").Army = 0;
            var council = game.BeginTurn();
            Assert.That(Offers(For(council, "valen"), Actions.Recruit), Is.False);
            Assert.That(Offers(For(council, "orsk"), Actions.Recruit), Is.True);
        }

        [Test]
        public void RaiseTaxes_GivesGoldCostsStabilityAndStopsItRecoveringNextTurn()
        {
            var game = NewGame();
            PlayTurn(game, ("valen", Actions.RaiseTaxes, null));
            var valen = game.World.Country("valen");
            Assert.That(valen.Gold, Is.EqualTo(55 + 30));
            Assert.That(valen.Stability, Is.EqualTo(3));
            game.BeginTurn();
            Assert.That(valen.Stability, Is.EqualTo(3), "no recovery in the upkeep after a tax rise");
        }

        [Test]
        public void RaiseTaxes_IsNotOfferedBelowStabilityTwo()
        {
            var game = NewGame();
            var valen = game.World.Country("valen");
            valen.Stability = 1;
            valen.RaisedTaxes = true;
            var council = game.BeginTurn();
            Assert.That(Offers(For(council, "valen"), Actions.RaiseTaxes), Is.False);
        }

        [Test]
        public void Fortify_IsOfferedOnlyForOurBorderProvinces()
        {
            var game = NewGame();
            var valen = For(game.BeginTurn(), "valen");
            Assert.That(Offers(valen, Actions.Fortify, "ashby"), Is.True, "ashby borders neutral land");
            Assert.That(Offers(valen, Actions.Fortify, "highmoor"), Is.False, "highmoor touches only valen");
            Assert.That(Offers(valen, Actions.Fortify, "corwick"), Is.False, "not ours");
        }

        [Test]
        public void Fortify_AddsAFortUpToTwo()
        {
            var game = NewGame();
            game.World.Country("valen").Gold = 500;
            PlayTurn(game, ("valen", Actions.Fortify, "ashby"));
            PlayTurn(game, ("valen", Actions.Fortify, "ashby"));
            Assert.That(game.World.Province("ashby").Forts, Is.EqualTo(2));
            Assert.That(Offers(For(game.BeginTurn(), "valen"), Actions.Fortify, "ashby"), Is.False);
        }

        [Test]
        public void Attack_OnANeutralProvinceThatFallsTakesIt()
        {
            var game = NewGame();
            game.World.Country("valen").Army = 200;
            var report = PlayTurn(game, ("valen", Actions.Attack, "westmarch"));
            Assert.That(game.World.Province("westmarch").Owner, Is.EqualTo("valen"));
            Assert.That(game.World.Country("valen").Army, Is.LessThan(200), "the attacker loses soldiers even when it wins");
            Assert.That(report.TerritoryChanges, Is.EqualTo(1));
            Assert.That(Outcome(report, "valen").Text, Does.StartWith("took westmarch"));
        }

        [Test]
        public void Attack_ThatFailsLeavesTheProvinceAndCostsSoldiers()
        {
            var game = NewGame();
            game.World.Province("westmarch").Garrison = 100;
            var report = PlayTurn(game, ("valen", Actions.Attack, "westmarch"));
            Assert.That(game.World.Province("westmarch").Owner, Is.Null);
            Assert.That(game.World.Province("westmarch").Garrison, Is.LessThan(100));
            Assert.That(game.World.Country("valen").Army, Is.LessThan(30));
            Assert.That(report.TerritoryChanges, Is.EqualTo(0));
            Assert.That(Outcome(report, "valen").Text, Does.Contain("failed"));
        }

        [Test]
        public void Attack_IsOfferedOnlyNextToOurLand()
        {
            var game = NewGame();
            var valen = For(game.BeginTurn(), "valen");
            Assert.That(Offers(valen, Actions.Attack, "fenmark"), Is.True);
            Assert.That(Offers(valen, Actions.Attack, "harrow"), Is.False, "harrow is two provinces away");
        }

        [Test]
        public void Attack_OnACountryNeedsWarAndWarNeedsABorder()
        {
            var game = NewGame();
            game.World.Country("valen").Army = 200;
            Give(game.World, "stonereach", "valen");
            var council = game.BeginTurn();
            var valen = For(council, "valen");
            Assert.That(Offers(valen, Actions.Attack, "corwick"), Is.False, "at peace with orsk");
            Assert.That(Offers(valen, Actions.DeclareWar, "orsk"), Is.True, "stonereach borders corwick");
            Assert.That(Offers(valen, Actions.DeclareWar, "ilvar"), Is.False, "no border with ilvar");

            Finish(game, council, null, ("valen", Actions.DeclareWar, "orsk"));
            Assert.That(game.World.AtWar("valen", "orsk"), Is.True);

            var next = game.BeginTurn();
            Assert.That(Offers(For(next, "valen"), Actions.Attack, "corwick"), Is.True);
            Assert.That(For(next, "orsk").TurnFacts, Does.Contain("valen declared war on us."));
            var report = Finish(game, next, null, ("valen", Actions.Attack, "corwick"));
            Assert.That(game.World.Province("corwick").Owner, Is.EqualTo("valen"));
            Assert.That(game.World.Country("orsk").Army, Is.LessThan(30), "the defender loses soldiers too");
            Assert.That(report.Battles, Is.EqualTo(1));
        }

        [Test]
        public void Peace_AcceptedMakesATruceThatBlocksWarAndThenRunsOut()
        {
            var game = NewGame();
            Give(game.World, "stonereach", "valen");
            game.World.SetRelation("valen", "orsk", Relation.War);
            var report = PlayTurn(game, r => true, ("orsk", Actions.OfferPeace, "valen"));
            Assert.That(game.World.RelationBetween("valen", "orsk"), Is.EqualTo(Relation.Truce));
            Assert.That(report.PeaceAccepted, Is.EqualTo(1));
            Assert.That(Outcome(report, "valen", Moment.Envoy).Text, Does.Contain("accepted"));

            for (int turn = 0; turn < game.Rules.TruceTurns - 1; turn++)
            {
                var council = game.BeginTurn();
                Assert.That(Offers(For(council, "valen"), Actions.DeclareWar, "orsk"), Is.False, $"truce turn {turn}");
                Finish(game, council, null);
            }
            var after = game.BeginTurn();
            Assert.That(game.World.RelationBetween("valen", "orsk"), Is.EqualTo(Relation.Peace));
            Assert.That(For(after, "valen").TurnFacts, Does.Contain("Our truce with orsk has ended."));
            Assert.That(Offers(For(after, "valen"), Actions.DeclareWar, "orsk"), Is.True);
        }

        [Test]
        public void Peace_RefusedKeepsTheWarAndTellsTheOfferer()
        {
            var game = NewGame();
            game.World.SetRelation("valen", "orsk", Relation.War);
            var report = PlayTurn(game, r => false, ("orsk", Actions.OfferPeace, "valen"));
            Assert.That(game.World.AtWar("valen", "orsk"), Is.True);
            Assert.That(report.PeaceAccepted, Is.EqualTo(0));
            var next = game.BeginTurn();
            Assert.That(For(next, "orsk").TurnFacts, Does.Contain("valen refused our offer of peace."));
        }

        [Test]
        public void Peace_OfferedBothWaysIsMadeWithoutAnEnvoy()
        {
            var game = NewGame();
            game.World.SetRelation("valen", "orsk", Relation.War);
            var council = game.BeginTurn();
            var envoys = game.SubmitCouncil(new[]
            {
                new CountryAnswer(For(council, "valen"), new Move(Actions.OfferPeace, "orsk")),
                new CountryAnswer(For(council, "orsk"), new Move(Actions.OfferPeace, "valen")),
            });
            Assert.That(envoys, Is.Empty);
            game.Resolve();
            Assert.That(game.World.RelationBetween("valen", "orsk"), Is.EqualTo(Relation.Truce));
        }

        private sealed class RefusingGate : IMoveGate
        {
            public int Asked;
            public string Admit(CountryRequest request, Move move) { Asked++; return "the agent refused it"; }
        }

        private sealed class AdmittingGate : IMoveGate
        {
            public readonly List<Move> Admitted = new List<Move>();
            public string Admit(CountryRequest request, Move move) { Admitted.Add(move); return null; }
        }

        [Test]
        public void Gate_APartnerRefusedInMutualPeaceMakesNoTruce()
        {
            var game = NewGame();
            game.World.SetRelation("valen", "orsk", Relation.War);
            var gate = new RefusingGate();
            game.Gates["orsk"] = gate;
            var council = game.BeginTurn();
            game.SubmitCouncil(new[]
            {
                new CountryAnswer(For(council, "valen"), new Move(Actions.OfferPeace, "orsk")),
                new CountryAnswer(For(council, "orsk"), new Move(Actions.OfferPeace, "valen")),
            });
            var report = game.Resolve();
            Assert.That(gate.Asked, Is.EqualTo(1));
            Assert.That(game.World.AtWar("valen", "orsk"), Is.True);
            Assert.That(Outcome(report, "orsk").Refused, Is.True);
            Assert.That(report.PeaceAccepted, Is.EqualTo(0));
        }

        [Test]
        public void Gate_IsAskedOnceForEveryMoveThatLandsAndNeverForARefusedOne()
        {
            var game = NewGame();
            game.World.Country("valen").Army = 200;
            game.World.Country("merrow").Army = 200;
            var valen = new AdmittingGate();
            var merrow = new RefusingGate();
            game.Gates["valen"] = valen;
            game.Gates["merrow"] = merrow;
            // Valen lands first and takes stonereach; merrow's attack is then stale, refused by the game.
            PlayTurn(game, ("valen", Actions.Attack, "stonereach"), ("merrow", Actions.Attack, "stonereach"));
            Assert.That(valen.Admitted, Is.EqualTo(new[] { new Move(Actions.Attack, "stonereach") }));
            Assert.That(merrow.Asked, Is.EqualTo(0), "the game refused it first, so the gate is never asked");
            // A move never offered is held without asking either; a hold is asked once.
            var council = game.BeginTurn();
            game.SubmitCouncil(new[] { new CountryAnswer(For(council, "valen"), new Move(Actions.Attack, "ilvarion")) });
            game.Resolve();
            Assert.That(valen.Admitted.Count, Is.EqualTo(1));
            PlayTurn(game);
            Assert.That(valen.Admitted.Last(), Is.EqualTo(Move.Hold));
            Assert.That(valen.Admitted.Count, Is.EqualTo(2));
        }

        [Test]
        public void Gate_ARefusalHoldsTheMoveWithItsReason()
        {
            var game = NewGame();
            game.World.Country("valen").Army = 200;
            game.Gates["valen"] = new RefusingGate();
            var report = PlayTurn(game, ("valen", Actions.Attack, "westmarch"));
            Assert.That(game.World.Province("westmarch").Owner, Is.Null);
            Assert.That(Outcome(report, "valen").Text, Does.Contain("the agent refused it"));
        }

        [Test]
        public void Envoy_IsOfferedOnlyAcceptOrRefuse()
        {
            var game = NewGame();
            game.World.SetRelation("valen", "orsk", Relation.War);
            var council = game.BeginTurn();
            var envoys = game.SubmitCouncil(new[] { new CountryAnswer(For(council, "orsk"), new Move(Actions.OfferPeace, "valen")) });
            Assert.That(envoys.Count, Is.EqualTo(1));
            var envoy = envoys[0];
            Assert.That(envoy.Moment, Is.EqualTo(Moment.Envoy));
            Assert.That(envoy.CountryId, Is.EqualTo("valen"));
            Assert.That(envoy.OfferedBy, Is.EqualTo("orsk"));
            Assert.That(envoy.Legal.Select(m => m.ToString()), Is.EqualTo(new[] { Actions.AcceptPeace, Actions.RefusePeace }));
            Assert.That(envoy.TurnFacts[0], Does.StartWith("orsk offers us peace"));
            game.Resolve();
        }

        [Test]
        public void Envoy_WithNoAnswerCountsAsRefused()
        {
            var game = NewGame();
            game.World.SetRelation("valen", "orsk", Relation.War);
            var council = game.BeginTurn();
            game.SubmitCouncil(new[] { new CountryAnswer(For(council, "orsk"), new Move(Actions.OfferPeace, "valen")) });
            game.Resolve();
            Assert.That(game.World.AtWar("valen", "orsk"), Is.True);
        }

        [Test]
        public void Stale_AnAttackOnACountryThatJustMadePeaceIsRefused()
        {
            var game = NewGame();
            game.World.Country("valen").Army = 200;
            Give(game.World, "stonereach", "valen");
            game.World.SetRelation("valen", "orsk", Relation.War);
            // Valen attacks; orsk offers peace in the same turn; valen, asked as envoy, accepts.
            // Peace lands first, so valen's attack, legal when it chose it, is refused.
            var report = PlayTurn(game, r => true, ("valen", Actions.Attack, "corwick"), ("orsk", Actions.OfferPeace, "valen"));
            var attack = Outcome(report, "valen");
            Assert.That(attack.Refused, Is.True);
            Assert.That(attack.Applied, Is.EqualTo(Move.Hold));
            Assert.That(attack.Text, Does.Contain("not at war with orsk"));
            Assert.That(report.StaleRefusals, Is.EqualTo(1));
            Assert.That(game.World.Province("corwick").Owner, Is.EqualTo("orsk"));
        }

        [Test]
        public void Stale_TwoAttacksOnOneNeutralProvince_TheSecondIsRefused()
        {
            var game = NewGame();
            game.World.Country("valen").Army = 200;
            game.World.Country("merrow").Army = 200;
            // Turn 1 resolves valen first (the order rotates from seat 0).
            var report = PlayTurn(game, ("valen", Actions.Attack, "stonereach"), ("merrow", Actions.Attack, "stonereach"));
            Assert.That(game.World.Province("stonereach").Owner, Is.EqualTo("valen"));
            Assert.That(Outcome(report, "merrow").Refused, Is.True);
            Assert.That(Outcome(report, "merrow").Text, Does.Contain("valen took stonereach first"));
        }

        [Test]
        public void ResolveOrder_RotatesEveryTurn()
        {
            var game = NewGame();
            PlayTurn(game);
            game.World.Country("valen").Army = 200;
            game.World.Country("merrow").Army = 200;
            // Turn 2 starts from seat 1 (orsk), so merrow (seat 2) lands before valen (seat 0).
            PlayTurn(game, ("valen", Actions.Attack, "stonereach"), ("merrow", Actions.Attack, "stonereach"));
            Assert.That(game.World.Province("stonereach").Owner, Is.EqualTo("merrow"));
        }

        [Test]
        public void Revolt_AtStabilityOneFreesAProvinceThatIsNotTheCapital()
        {
            var rules = Calm();
            rules.RevoltChance = 1;
            var game = NewGame(rules: rules);
            var valen = game.World.Country("valen");
            valen.Stability = 1;
            valen.RaisedTaxes = true;
            var council = game.BeginTurn();
            Assert.That(game.World.ProvinceCount("valen"), Is.EqualTo(2));
            Assert.That(valen.Stability, Is.EqualTo(rules.StabilityAfterRevolt));
            Assert.That(For(council, "valen").TurnFacts[0], Does.Contain("revolted"));
            Finish(game, council, null);
            Assert.That(game.Reports[0].TerritoryChanges, Is.EqualTo(1));
        }

        [Test]
        public void Revolt_NeverTakesTheCapital()
        {
            var rules = Calm();
            rules.RevoltChance = 1;
            for (int seed = 1; seed <= 20; seed++)
            {
                var game = NewGame(seed, rules);
                Give(game.World, "brindle", null);
                var valen = game.World.Country("valen");
                valen.Stability = 1;
                valen.RaisedTaxes = true;
                game.BeginTurn();
                Assert.That(game.World.Province("highmoor").Owner, Is.EqualTo("valen"), $"seed {seed}");
                Assert.That(game.World.Province("ashby").Owner, Is.Null, $"seed {seed}");
            }
        }

        [Test]
        public void Bankruptcy_MakesSoldiersDesert()
        {
            var game = NewGame();
            var valen = game.World.Country("valen");
            valen.Gold = 0;
            valen.Army = 200;
            var council = game.BeginTurn();
            // Income 30 − upkeep 100 leaves the treasury below zero: 15% of 200 desert.
            Assert.That(valen.Army, Is.EqualTo(170));
            Assert.That(valen.Gold, Is.EqualTo(0));
            Assert.That(For(council, "valen").TurnFacts[0], Does.Contain("deserted"));
        }

        [Test]
        public void Elimination_ACountryWithNoProvinceIsOutAndIsNoLongerAsked()
        {
            var game = NewGame();
            game.World.Country("valen").Army = 500;
            Give(game.World, "thornbury", "valen");
            Give(game.World, "marrowfield", "valen");
            game.World.SetRelation("valen", "kesh", Relation.War);
            var report = PlayTurn(game, ("valen", Actions.Attack, "keshtar"));
            Assert.That(game.World.Country("kesh").Alive, Is.False);
            Assert.That(game.World.Country("kesh").EliminatedOnTurn, Is.EqualTo(1));
            Assert.That(report.Resolution.Last(), Does.Contain("kesh has lost its last province"));
            var next = game.BeginTurn();
            Assert.That(next.Select(r => r.CountryId), Does.Not.Contain("kesh"));
            Assert.That(For(next, "valen").StandingFacts.Any(l => l.StartsWith("kesh")), Is.False);
        }

        [Test]
        public void Victory_FifteenProvincesWinsAtTheEndOfTheTurn()
        {
            var game = NewGame();
            foreach (var p in game.World.ProvinceList.Where(p => p.Owner == null).Take(10)) Give(game.World, p.Id, "valen");
            Give(game.World, "corwick", "valen");
            Give(game.World, "redcliff", "valen");
            Assert.That(game.World.ProvinceCount("valen"), Is.EqualTo(15));
            PlayTurn(game);
            Assert.That(game.IsOver, Is.True);
            Assert.That(game.Result.Winner, Is.EqualTo("valen"));
            Assert.That(game.Result.Ending, Is.EqualTo(Ending.Domination));
            Assert.Throws<System.InvalidOperationException>(() => game.BeginTurn());
        }

        [Test]
        public void Victory_LastCountryStanding()
        {
            var game = NewGame();
            foreach (var p in game.World.ProvinceList) Give(game.World, p.Id, "orsk");
            PlayTurn(game);
            Assert.That(game.Result.Winner, Is.EqualTo("orsk"));
            Assert.That(game.Result.Ending, Is.EqualTo(Ending.LastStanding));
        }

        [Test]
        public void Victory_AtTheTurnLimitMostProvincesWins()
        {
            var rules = Calm();
            rules.MaxTurns = 2;
            var game = NewGame(rules: rules);
            Give(game.World, "eastwatch", "orsk");
            PlayTurn(game);
            Assert.That(game.IsOver, Is.False);
            PlayTurn(game);
            Assert.That(game.Result.Winner, Is.EqualTo("orsk"));
            Assert.That(game.Result.Ending, Is.EqualTo(Ending.TurnLimit));
            Assert.That(game.Result.Turns, Is.EqualTo(2));
        }

        [Test]
        public void Phases_MustBeCalledInOrder()
        {
            var game = NewGame();
            Assert.Throws<System.InvalidOperationException>(() => game.Resolve());
            Assert.Throws<System.InvalidOperationException>(() => game.SubmitCouncil(null));
            game.BeginTurn();
            Assert.Throws<System.InvalidOperationException>(() => game.BeginTurn());
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Agenerela;
using Agenerela.Providers;
using NUnit.Framework;
using static GreyBoxStrategy.Tests.TestGames;

namespace GreyBoxStrategy.Tests
{
    /// <summary>
    /// The economy, E2 (18 November): a market, a barracks and a farm, bought at once with gold by a court order in one of
    /// our held provinces, one of each a province and one building order a province a turn. The fort stays fortify.
    /// </summary>
    public sealed class EconomyBuildingTests
    {
        private static GameRules Rules(bool economy = true)
        {
            var r = ArmyRulesOff(Calm());
            r.Turn = TurnStyle.Writs;
            r.Victory = VictoryMode.Conquest;
            r.MaxTurns = 150;
            r.StalemateTurns = 20;
            r.FreeOrders = true;
            r.Economy = economy;
            return r;
        }

        private static List<CountryRequest> Turn(Game g, Func<CountryRequest, Move> decide = null, Action inCouncil = null)
        {
            var asked = new List<CountryRequest>();
            var requests = g.BeginTurn();
            inCouncil?.Invoke();
            while (requests.Count > 0)
            {
                asked.AddRange(requests);
                requests = g.Submit(requests.Select(r => new CountryAnswer(r, r.Moment == Moment.Envoy ? new Move(Actions.RefusePeace) : decide?.Invoke(r) ?? Move.Hold)).ToList());
            }
            g.Resolve();
            return asked;
        }

        // The court's orders in this order, each the first legal one of its action (or at the given province).
        private static Func<CountryRequest, Move> Court(string country, params (string action, string at)[] orders) => r =>
        {
            if (r.CountryId != country || r.Moment != Moment.Council) return null;
            foreach (var (action, at) in orders)
                if (r.Legal.FirstOrDefault(m => m.Action == action && (at == null || m.Target == at)) is Move m) return m;
            return Move.Hold;
        };

        [Test]
        public void AMarket_IsBoughtAtOnce_InOurHeldProvince_YieldsItsGold_AndIsNotOfferedThereAgain()
        {
            var g = new Game(1, MapDefinition.Standard(), Rules());
            Turn(g);
            var me = g.World.Country("valen");
            var at = g.World.ProvincesOf("valen").OrderBy(p => p.Id).First();
            int before = g.World.Yield(at);
            int gold = 0;
            var asked = Turn(g, Court("valen", (Actions.BuildMarket, at.Id)), () => { me.Gold = 1000; gold = me.Gold; });
            var first = asked.First(r => r.CountryId == "valen" && r.Moment == Moment.Council);
            var offered = first.Legal.Where(m => m.Action == Actions.BuildMarket).ToList();
            Assert.That(offered.Select(m => m.Target), Has.Member(at.Id));
            Assert.That(offered.All(m => g.World.Province(m.Target).Owner == "valen"), Is.True, "only our provinces");
            Assert.That(offered.Count, Is.LessThanOrEqualTo(g.Rules.MaxBuildTargets));
            Assert.That(at.Has(Building.Market), Is.True, "it stands from the turn it lands");
            Assert.That(g.World.Yield(at), Is.EqualTo(before + (at.Info.IsCoastal ? g.Rules.MarketCoastYield : g.Rules.MarketYield)));
            var next = Turn(g, Court("valen"), () => me.Gold = 1000).First(r => r.CountryId == "valen" && r.Moment == Moment.Council);
            Assert.That(next.Legal.Any(m => m.Action == Actions.BuildMarket && m.Target == at.Id), Is.False, "one market a province");
            Assert.That(next.Legal.Any(m => m.Action == Actions.BuildFarm && m.Target == at.Id), Is.True, "a farm still may be built there");
        }

        [Test]
        public void OneBuildingOrderAProvinceATurn_AFortAmongThem_AndTheGoldIsHeldBack()
        {
            var g = new Game(1, MapDefinition.Standard(), Rules());
            Turn(g);
            var me = g.World.Country("valen");
            var at = g.World.ProvincesOf("valen").OrderBy(p => p.Id).First();
            var asked = Turn(g, Court("valen", (Actions.BuildFarm, at.Id), (Actions.BuildMarket, null)), () => me.Gold = 1000);
            var courts = asked.Where(r => r.CountryId == "valen" && r.Moment == Moment.Council).ToList();
            Assert.That(courts.Count, Is.GreaterThanOrEqualTo(2));
            Assert.That(courts[1].Legal.Any(m => (Actions.BuildsWhat(m.Action) != null || m.Action == Actions.Fortify) && m.Target == at.Id), Is.False,
                "after the farm, no other building or fort there this turn");
            Assert.That(courts[1].Legal.Any(m => m.Action == Actions.BuildMarket && m.Target != at.Id), Is.True, "another province still may build");

            // Gold for one farm only: after it, no building is offered.
            var g2 = new Game(1, MapDefinition.Standard(), Rules());
            Turn(g2);
            var me2 = g2.World.Country("valen");
            var asked2 = Turn(g2, Court("valen", (Actions.BuildFarm, null)), () => me2.Gold = g2.Rules.FarmCost + g2.Rules.FarmCost - 1);
            var c2 = asked2.Where(r => r.CountryId == "valen" && r.Moment == Moment.Council).ToList();
            Assert.That(c2[0].Legal.Any(m => m.Action == Actions.BuildFarm), Is.True);
            Assert.That(c2.Count < 2 || !c2[1].Legal.Any(m => m.Action == Actions.BuildFarm), Is.True, "the first farm's gold is held back");
        }

        [Test]
        public void ABarracks_RaisesManpowerAndItsCap_FarmsYield_AndThreeFarmsSteadyTheRealm()
        {
            var g = new Game(1, MapDefinition.Standard(), Rules());
            var me = g.World.Country("valen");
            var mine = g.World.ProvincesOf("valen").OrderBy(p => p.Id).ToList();
            int growth = g.World.ManpowerGrowth(me), cap = g.World.ManpowerCap(me);
            mine[0].Buildings.Add(Building.Barracks);
            Assert.That((g.World.ManpowerGrowth(me), g.World.ManpowerCap(me)), Is.EqualTo((growth + g.Rules.BarracksManpower, cap + g.Rules.BarracksCap)));
            int y = g.World.Yield(mine[1]);
            mine[1].Buildings.Add(Building.Farm);
            Assert.That(g.World.Yield(mine[1]), Is.EqualTo(y + (mine[1].Info.Terrain == Terrain.Plains ? g.Rules.FarmPlainsYield : g.Rules.FarmYield)));

            Assume.That(mine.Count, Is.GreaterThanOrEqualTo(3));
            foreach (var p in mine.Take(3)) p.Buildings.Add(Building.Farm);
            me.Taxes = TaxLevel.High;
            me.Stability = 3;
            Turn(g);
            Assert.That(me.Stability, Is.EqualTo(3), "high taxes take 1, three farms give it back");
        }

        [Test]
        public void ABuildingStaysWithItsProvince_AndAnOccupiedOneYieldsNothingToItsOwner()
        {
            var g = new Game(1, MapDefinition.Standard(), Rules());
            var p = g.World.ProvincesOf("valen").OrderBy(x => x.Id).First();
            p.Buildings.Add(Building.Market);
            int income = g.World.Income(g.World.Country("valen"));
            p.Occupier = "orsk";
            Assert.That(g.World.Income(g.World.Country("valen")), Is.LessThan(income - g.Rules.MarketYield + 1), "the market's gold goes with the province");
            p.Occupier = null;
            p.Owner = "orsk";
            Assert.That(p.Has(Building.Market), Is.True);
            Assert.That(g.World.Yield(p), Is.GreaterThan(g.Rules.IncomePerProvince), "the new owner gets its gold");
        }

        [Test]
        public void ACourt_ReadsItsProvincesLandGoldAndBuildings_OnlyWithTheEconomy()
        {
            var g = new Game(1, MapDefinition.Standard(), Rules());
            var p = g.World.ProvincesOf("valen").OrderBy(x => x.Id).First();
            p.Buildings.Add(Building.Farm);
            string d = Legality.Describe(g.World, "valen", p.Id);
            Assert.That(d, Does.Contain($"{g.World.Yield(p)} gold a turn").And.Contain("farm").And.Contain(p.Info.Terrain.ToString().ToLowerInvariant()));
            var off = new Game(1, MapDefinition.Standard(), Rules(economy: false));
            Assert.That(Legality.Describe(off.World, "valen", p.Id), Does.Not.Contain("gold a turn"), "off: as before");
            Assert.That(WritLegality.CourtMoves(off.World, "valen").Any(m => Actions.BuildsWhat(m.Action) != null), Is.False);
        }

        [Test]
        public async Task AModelCourt_IsOfferedTheBuildings_AndItsOrdersLand()
        {
            var seats = new Dictionary<string, WritRuler>();
            ILLMProvider Provider(string id) => new ScriptedProvider
            {
                Callback = req =>
                {
                    var asking = seats[id].Asking;
                    var build = asking?.Legal.FirstOrDefault(m => Actions.BuildsWhat(m.Action) != null);
                    if (build != null) return ScriptedReply.Decide(build.Action, build.Target, "Build.");
                    return ScriptedReply.Decide(req.OfferedActions.Contains(Actions.RefusePeace) ? Actions.RefusePeace : ActionRegistry.None);
                },
            };
            var rules = GameRules.Standard;
            rules.FreeOrders = true;
            rules.Economy = true;
            var setup = GameSetup.Writs(3);
            setup.TurnCap = 4;
            setup.Numbers = rules;
            WritModelRecord record = null;
            record = await Await.NotCancelled(WritModelRunner.PlayAsync(setup, Enumerable.Repeat(ModelGameRunner.Model, 5).ToList(),
                (w, id) => seats[id] = new WritRuler(w, id, Provider(id)), "fake"));
            var builds = record.Decisions.Where(d => Actions.BuildsWhat(d.Move.Action) != null).ToList();
            Assert.That(builds, Is.Not.Empty, "courts built");
            Assert.That(record.Decisions.Where(d => !d.Offered), Is.Empty, "every building order was offered");
            Assert.That(record.Record.Reports.SelectMany(r => r.Outcomes).Count(o => Actions.BuildsWhat(o.Choice.Action) != null && !o.Refused), Is.GreaterThan(0), "and landed");
        }
    }
}

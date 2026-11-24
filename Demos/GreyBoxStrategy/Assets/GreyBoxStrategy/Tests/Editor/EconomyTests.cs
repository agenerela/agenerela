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
    /// The turn rework, slice 2, E1 (18 November): <see cref="GameRules.Economy"/> with free orders. Manpower limits
    /// recruiting, provinces yield by terrain, and taxes are a standing level. Off, nothing changes (the request hash test).
    /// </summary>
    public sealed class EconomyTests
    {
        private static GameRules Rules(bool economy = true, bool free = true)
        {
            var r = ArmyRulesOff(Calm());
            r.Turn = TurnStyle.Writs;
            r.Victory = VictoryMode.Conquest;
            r.MaxTurns = 150;
            r.StalemateTurns = 20;
            r.FreeOrders = free;
            r.Economy = economy;
            return r;
        }

        private static Game New(GameRules r = null) => new Game(1, MapDefinition.Standard(), r ?? Rules());

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

        private static Func<CountryRequest, Move> Court(string country, params string[] actions) => r =>
        {
            if (r.CountryId != country || r.Moment != Moment.Council) return null;
            foreach (string a in actions)
                if (r.Legal.FirstOrDefault(m => m.Action == a) is Move m) return m;
            return Move.Hold;
        };

        [Test]
        public void Economy_NeedsFreeOrders_AndEveryRealmStartsAtItsFullManpower()
        {
            Assert.That(Rules(free: false).EconomyOn, Is.False, "without free orders the economy does not apply");
            Assert.That(Rules(economy: false).EconomyOn, Is.False);
            var g = New();
            foreach (var c in g.World.CountryList)
                Assert.That(c.Manpower, Is.EqualTo(g.World.HeldProvinces(c.Id) * g.Rules.ManpowerCapPerProvince).And.GreaterThan(0), c.Id);
            Assert.That(New(Rules(economy: false)).World.CountryList.All(c => c.Manpower == 0), Is.True, "off: no manpower");
        }

        [Test]
        public void Manpower_CutsALevy_IsSpentByIt_HeldBackForALaterOrder_AndGrowsToItsCap()
        {
            var g = New();
            Turn(g);
            var me = g.World.Country("valen");
            me.Gold = 10000;
            int full = g.World.Levy(me); // a full levy: gold and men to spare
            var asked = Turn(g, Court("valen", Actions.Recruit), () =>
            {
                me.Gold = 10000;
                me.Manpower = full + 2; // one full levy, then too few men for another
            });
            var courts = asked.Where(r => r.CountryId == "valen" && r.Moment == Moment.Council).ToList();
            Assert.That(courts[0].Legal.Any(m => m.Action == Actions.Recruit), Is.True);
            Assert.That(courts[1].Legal.Any(m => m.Action == Actions.Recruit), Is.False, "2 men left, under the smallest levy: no recruit offered");
            Assert.That(me.ReservedManpower, Is.EqualTo(0), "nothing held once the turn resolves");
            // The levy landed and spent its men; then the upkeep of the next turn adds the growth, to the cap.
            int after = me.Manpower;
            Assert.That(after, Is.EqualTo(2));
            g.BeginTurn();
            Assert.That(me.Manpower, Is.EqualTo(Math.Min(g.World.ManpowerCap(me), after + g.World.ManpowerGrowth(me))));
            me.Manpower = 7;
            Assert.That(g.World.Levy(me), Is.EqualTo(Math.Min(7, full)), "the levy is cut to the men left");
        }

        [Test]
        public void ARecruitShortOfMen_SaysManpower_AndOneShortOfGold_SaysGold()
        {
            var g = New();
            var me = g.World.Country("valen");
            me.Gold = 10000;
            me.Manpower = 2;
            Assert.That(WritLegality.WhyNotCourt(g.World, "valen", new Move(Actions.Recruit)), Is.EqualTo($"recruiting needs {g.Rules.MinLevy} manpower; we have 2"));
            me.Manpower = 100;
            me.Gold = 1;
            Assert.That(WritLegality.WhyNotCourt(g.World, "valen", new Move(Actions.Recruit)), Does.Contain("gold"), "gold short: the reason says gold");
        }

        [Test]
        public void Income_IsEachHeldProvincesYield_ByTerrainAndCoast_ThenTheTaxLevel()
        {
            var g = New();
            var me = g.World.Country("valen");
            var r = g.Rules;
            int expected = 0;
            foreach (var p in g.World.ProvincesOf("valen"))
            {
                int y = r.IncomePerProvince;
                if (p.Info.Terrain == Terrain.Plains) y += r.PlainsYield;
                if (p.Info.Terrain == Terrain.Hills || p.Info.Terrain == Terrain.Mountains) y += r.MineYield;
                if (p.Info.IsCoastal) y += r.CoastYield;
                Assert.That(g.World.Yield(p), Is.EqualTo(y), p.Id);
                expected += y;
            }
            Assert.That(g.World.Income(me), Is.EqualTo(expected));
            Assert.That(expected, Is.GreaterThan(g.World.ProvincesOf("valen").Count() * r.IncomePerProvince), "the land adds something on Classic");
            me.Taxes = TaxLevel.High;
            Assert.That(g.World.Income(me), Is.EqualTo((int)Math.Round(expected * r.HighTaxIncome, MidpointRounding.AwayFromZero)));
            me.Taxes = TaxLevel.Low;
            Assert.That(g.World.Income(me), Is.EqualTo((int)Math.Round(expected * r.LowTaxIncome, MidpointRounding.AwayFromZero)));
            var off = New(Rules(economy: false));
            Assert.That(off.World.Income(off.World.Country("valen")), Is.EqualTo(off.World.ProvincesOf("valen").Count() * r.IncomePerProvince), "off: as before");
        }

        [Test]
        public void Taxes_AreAStandingLevel_SetByACourtOrder_InPlaceOfRaiseTaxes()
        {
            var g = New();
            var asked = Turn(g, Court("valen", Actions.TaxesHigh, Actions.TaxesLow));
            var courts = asked.Where(r => r.CountryId == "valen" && r.Moment == Moment.Council).ToList();
            Assert.That(courts[0].Legal.Any(m => m.Action == Actions.RaiseTaxes), Is.False, "no one-off raise under the economy");
            Assert.That(courts[0].Legal.Any(m => m.Action == Actions.TaxesNormal), Is.False, "the level it already has is not offered");
            Assert.That(courts[0].Legal.Any(m => m.Action == Actions.TaxesHigh) && courts[0].Legal.Any(m => m.Action == Actions.TaxesLow), Is.True);
            Assert.That(courts.Skip(1).All(r => !r.Legal.Any(m => Actions.SetsTaxes(m.Action) != null)), Is.True, "one tax order a turn");
            var me = g.World.Country("valen");
            Assert.That(me.Taxes, Is.EqualTo(TaxLevel.High), "it stands from the turn it lands");
            Assert.That(WritFacts.Standing(g.World, "valen").Any(l => l.Contains("Taxes: high") && l.Contains("Manpower: ")), Is.True, "the court reads both");

            me.Stability = 4;
            g.BeginTurn();
            Assert.That(me.Stability, Is.EqualTo(3), "high: stability falls 1 a turn");
            Answer(g);
            me.Taxes = TaxLevel.Low;
            me.Stability = 1;
            g.BeginTurn();
            Assert.That(me.Stability, Is.EqualTo(3), "low, below 3: +2");
            Answer(g);
            me.Taxes = TaxLevel.High;
            me.Stability = 1;
            g.BeginTurn();
            Assert.That(me.Stability, Is.EqualTo(1), "high never takes it below 1");
        }

        private static void Answer(Game g)
        {
            var requests = g.OpenRequests;
            while (requests.Count > 0) requests = g.Submit(requests.Select(r => new CountryAnswer(r, r.Moment == Moment.Envoy ? new Move(Actions.RefusePeace) : Move.Hold)).ToList());
            g.Resolve();
        }

        [Test]
        public void AnEconomyGame_PlaysToItsEnd_WithEveryOrderOffered_AndManpowerInBounds()
        {
            var r = Rules();
            r.MaxTurns = 40;
            var g = new Game(4, MapDefinition.Standard(), r);
            var rng = new Random(4);
            int taxes = 0;
            while (!g.IsOver)
            {
                var requests = g.BeginTurn();
                while (requests.Count > 0)
                    requests = g.Submit(requests.Select(x => new CountryAnswer(x, x.Moment == Moment.Envoy ? new Move(Actions.RefusePeace)
                        : x.Legal[rng.Next(x.Legal.Count)])).ToList());
                var report = g.Resolve();
                taxes += report.Outcomes.Count(o => Actions.SetsTaxes(o.Choice.Action) != null && !o.Refused);
                Assert.That(report.Outcomes.Where(o => o.Refused && o.Text.Contains("not offered")), Is.Empty, $"turn {report.Turn}");
                foreach (var c in g.World.Alive)
                    Assert.That(c.Manpower, Is.GreaterThanOrEqualTo(0), $"turn {report.Turn}: {c.Id}");
            }
            Assert.That(taxes, Is.GreaterThan(0), "tax levels were set");
        }

        [Test]
        public async Task AModelCourt_IsOfferedTheTaxLevels_NotRaiseTaxes()
        {
            var seen = new List<ScriptedRequest>();
            var provider = new ScriptedProvider
            {
                Callback = req =>
                {
                    seen.Add(req);
                    if (req.OfferedActions.Contains(Actions.TaxesHigh)) return ScriptedReply.Decide(Actions.TaxesHigh, TargetRegistry.NoTarget, "More.");
                    return ScriptedReply.Decide(req.OfferedActions.Contains(Actions.RefusePeace) ? Actions.RefusePeace : ActionRegistry.None);
                },
            };
            var rules = GameRules.Standard;
            rules.FreeOrders = true;
            rules.Economy = true;
            var setup = GameSetup.Writs(3);
            setup.TurnCap = 3;
            setup.Numbers = rules;
            var record = await Await.NotCancelled(WritModelRunner.PlayAsync(setup, Enumerable.Repeat(ModelGameRunner.Model, 5).ToList(),
                (w, id) => new WritRuler(w, id, provider), "fake"));
            Assert.That(seen.Any(r => r.OfferedActions.Contains(Actions.TaxesHigh)), Is.True);
            Assert.That(seen.Any(r => r.OfferedActions.Contains(Actions.RaiseTaxes)), Is.False);
            Assert.That(record.Decisions.Where(d => !d.Offered), Is.Empty);
            Assert.That(record.Decisions.Count(d => d.Move.Action == Actions.TaxesHigh), Is.EqualTo(5), "each realm once: then it is high");
        }
    }
}

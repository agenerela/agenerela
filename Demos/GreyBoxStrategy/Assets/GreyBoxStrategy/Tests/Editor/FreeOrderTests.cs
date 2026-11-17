using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using static GreyBoxStrategy.Tests.TestGames;

namespace GreyBoxStrategy.Tests
{
    /// <summary>
    /// The turn rework, slice 1 (17 November): <see cref="GameRules.FreeOrders"/>, the writs turn without writs. Each
    /// realm gives court orders until it answers none, its gold held back as it goes, then orders every army with no
    /// standing order. Off, the writs turn is unchanged (<c>ArmyOrderTests</c>' request hash holds that).
    /// </summary>
    public sealed class FreeOrderTests
    {
        private static GameRules Rules(bool free = true)
        {
            var r = ArmyRulesOff(Calm());
            r.Turn = TurnStyle.Writs;
            r.Victory = VictoryMode.Conquest;
            r.MaxTurns = 150;
            r.StalemateTurns = 20;
            r.FreeOrders = free;
            return r;
        }

        private static Game New(GameRules rules = null) => new Game(1, MapDefinition.Standard(), rules ?? Rules());

        private static Move Refuse(CountryRequest r) => new Move(Actions.RefusePeace);

        /// <summary>Plays a turn: <paramref name="decide"/> answers (null holds); returns every request asked, in order.</summary>
        private static List<CountryRequest> Turn(Game g, Func<CountryRequest, Move> decide = null, Action inCouncil = null)
        {
            var asked = new List<CountryRequest>();
            var requests = g.BeginTurn();
            // After upkeep, as the council sits: the requests after the first are built on the world as the test sets it.
            inCouncil?.Invoke();
            while (requests.Count > 0)
            {
                asked.AddRange(requests);
                requests = g.Submit(requests.Select(r => new CountryAnswer(r, r.Moment == Moment.Envoy ? Refuse(r) : decide?.Invoke(r) ?? Move.Hold)).ToList());
            }
            g.Resolve();
            return asked;
        }

        // A court that gives the first legal order of these actions each time, else none.
        private static Func<CountryRequest, Move> Court(string country, params string[] actions) => r =>
        {
            if (r.CountryId != country || r.Moment != Moment.Council) return null;
            foreach (string a in actions)
                if (r.Legal.FirstOrDefault(m => m.Action == a) is Move m) return m;
            return Move.Hold;
        };

        [Test]
        public void TheCourt_GivesOrdersUntilItAnswersNone_AtMostTheCap_AndReadsNoWrits()
        {
            var g = New();
            Turn(g);
            g.World.Country("valen").Gold = 500;
            var asked = Turn(g, Court("valen", Actions.Recruit));
            var courts = asked.Where(r => r.CountryId == "valen" && r.Moment == Moment.Council).ToList();
            Assert.That(courts.Count, Is.EqualTo(g.Rules.MaxCourtOrders), "asked again after each order, up to the cap of three");
            Assert.That(courts[1].Ordered, Has.Count.GreaterThan(0), "each later request tells its earlier orders");
            Assert.That(courts.SelectMany(r => r.StandingFacts).Any(f => f.Contains("Writs:")), Is.False, "no writs to read");

            var held = asked.Where(r => r.CountryId == "orsk" && r.Moment == Moment.Council).ToList();
            Assert.That(held.Count, Is.EqualTo(1), "a court that answers none at once is asked once");
        }

        [Test]
        public void TheCap_IsPerRealm_AndAnOverrideLiftsIt()
        {
            var r = Rules();
            r.CourtOrdersOverride["valen"] = 5;
            var g = New(r);
            Turn(g);
            g.World.Country("valen").Gold = 2000;
            var asked = Turn(g, Court("valen", Actions.Recruit, Actions.RaiseTaxes));
            Assert.That(asked.Count(x => x.CountryId == "valen" && x.Moment == Moment.Council), Is.EqualTo(5));
        }

        [Test]
        public void GoldIsHeldBack_ALaterOrderIsOfferedOnlyWhatIsLeft_AndAllOfItLands()
        {
            var g = New();
            Turn(g);
            var me = g.World.Country("valen");
            me.Gold = 100000;
            int company = g.World.Levy(me); // a full company
            // One company, and less than the smallest levy or a fort over, set as the council sits.
            int left = Math.Min(g.Rules.MinLevy * g.Rules.GoldPerSoldier, g.Rules.FortCost) - 1;
            int armyBefore = 0;
            var asked = Turn(g, Court("valen", Actions.Recruit, Actions.Fortify), () =>
            {
                me.Gold = company * g.Rules.GoldPerSoldier + left;
                armyBefore = me.Army;
            });
            var courts = asked.Where(r => r.CountryId == "valen" && r.Moment == Moment.Council).ToList();
            Assert.That(courts[0].Legal.Any(m => m.Action == Actions.Recruit), Is.True);
            Assert.That(courts.Count, Is.EqualTo(2), "nothing left to order after the recruit, so the court answers none");
            Assert.That(courts[1].Legal.Any(m => m.Action == Actions.Recruit), Is.False, "the company's gold is held back");
            Assert.That(courts[1].Legal.Any(m => m.Action == Actions.Fortify), Is.False, "and what is left does not pay for a fort");
            Assert.That(me.Army, Is.EqualTo(armyBefore + company), "the recruit landed");
            Assert.That(me.Reserved, Is.EqualTo(0), "nothing is held back once the turn resolves");
        }

        [Test]
        public void AnOrderIsNotOfferedTwice_ButRecruitsMayRepeat()
        {
            var g = New();
            Turn(g);
            g.World.Country("valen").Gold = 2000;
            var asked = Turn(g, Court("valen", Actions.RaiseTaxes, Actions.Recruit));
            var courts = asked.Where(r => r.CountryId == "valen" && r.Moment == Moment.Council).ToList();
            Assert.That(courts[0].Legal.Any(m => m.Action == Actions.RaiseTaxes), Is.True);
            Assert.That(courts[1].Legal.Any(m => m.Action == Actions.RaiseTaxes), Is.False, "taxes once a turn");
            Assert.That(courts[2].Legal.Any(m => m.Action == Actions.Recruit), Is.True, "a second company may be raised");
        }

        [Test]
        public void AHoldingArmy_KeepsItsOrder_AndIsAskedWhenWoken_OrAfterThreeTurnsUnasked()
        {
            var g = New();
            Turn(g);
            int next = g.World.Turn + 1;
            var armies = g.World.ArmiesOf("valen").OrderBy(a => a.Id).ToList();
            Assume.That(armies.Count, Is.GreaterThanOrEqualTo(1));
            foreach (var a in armies) { a.Wake.Clear(); a.OrderedOnTurn = next - 1; } // all holding since last turn
            var due = armies[0];
            due.OrderedOnTurn = next - g.Rules.AskHoldingArmyEvery; // held three turns unasked
            var woken = new Army("zed_host", "valen", due.Location, 10, next - 1);
            woken.Wake.Clear();
            woken.Wake.Add("war declared");
            var step = g.World.Neighbours(g.World.Province(due.Location)).First(p => WritLegality.Passable(g.World, "valen", p));
            var marching = new Army("yan_host", "valen", due.Location, 10, next - 1) { Destination = step.Id, StepTurnsLeft = 1 };
            marching.Wake.Clear();
            marching.Path.Add(step.Id);
            var recent = new Army("xan_host", "valen", due.Location, 10, next - 1); // held since last turn: keeps its order
            recent.Wake.Clear();
            g.World.Armies.Add(recent);
            g.World.Armies.Add(woken);
            g.World.Armies.Add(marching);
            g.World.Country("valen").Army += 30;

            var asked = Turn(g).Where(r => r.CountryId == "valen" && r.Moment == Moment.Army).Select(r => r.ArmyId).ToList();
            Assert.That(asked, Is.EquivalentTo(new[] { due.Id, "zed_host" }),
                "the army three turns unasked and the woken one; the recent holders and the marching one keep their orders");
        }

        [Test]
        public void AskHoldingArmyEvery_IsASetting_OneAsksEveryHoldingArmy()
        {
            var r = Rules();
            r.AskHoldingArmyEvery = 1;
            var g = New(r);
            Turn(g);
            var armies = g.World.ArmiesOf("valen").ToList();
            foreach (var a in armies) { a.Wake.Clear(); a.OrderedOnTurn = g.World.Turn; }
            var asked = Turn(g).Where(x => x.CountryId == "valen" && x.Moment == Moment.Army).Select(x => x.ArmyId).ToList();
            Assert.That(asked, Is.EquivalentTo(armies.Select(a => a.Id)));
        }

        [Test]
        public void ABesiegingArmy_KeepsItsOrder_AndIsNotAsked()
        {
            var g = New();
            Turn(g);
            var a = g.World.ArmiesOf("valen").First();
            a.Wake.Clear();
            a.OrderedOnTurn = g.World.Turn - 10; // long past the holding timer: only the siege keeps it from being asked
            g.World.Province(a.Location).SiegedBy = "valen";
            Assert.That(Game.AskedFreely(g.World, a), Is.False);
            a.Wake.Add("war declared");
            Assert.That(Game.AskedFreely(g.World, a), Is.True, "woken, it is asked");
        }

        [Test]
        public void AFreeGame_PlaysToItsEnd_WithEveryOrderOneThatWasOffered()
        {
            var r = Rules();
            r.MaxTurns = 40;
            var g = new Game(4, MapDefinition.Standard(), r);
            var rng = new Random(4);
            int asked = 0;
            while (!g.IsOver)
            {
                var requests = g.BeginTurn();
                while (requests.Count > 0)
                {
                    asked += requests.Count;
                    requests = g.Submit(requests.Select(x => new CountryAnswer(x, x.Moment == Moment.Envoy ? Refuse(x)
                        : x.Legal[rng.Next(x.Legal.Count)])).ToList());
                }
                var report = g.Resolve();
                Assert.That(report.Outcomes.Where(o => o.Refused).Select(o => o.Text).Where(t => t.Contains("not offered")), Is.Empty, $"turn {report.Turn}");
                Assert.That(g.World.CountryList.All(c => c.Reserved == 0), Is.True);
            }
            Assert.That(asked, Is.GreaterThan(40 * 5), "more than one decision a realm a turn");
        }
    }
}

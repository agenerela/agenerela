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
    /// The advisor with free orders (17 November): it advises a free turn the way a model court plays one, court orders
    /// one at a time on the moves left (<see cref="WritLegality.CourtMovesAfter"/>) until none or the model seats'
    /// pacing cap, then every army <see cref="Game.AskedFreely"/> would ask. On the writs turn it is unchanged.
    /// </summary>
    public sealed class FreeAdvisorTests
    {
        private static GameRules Rules(bool free)
        {
            var r = ArmyRulesOff(Calm());
            r.Turn = TurnStyle.Writs;
            r.Victory = VictoryMode.Conquest;
            r.FreeOrders = free;
            return r;
        }

        // Taxes when listed, then a recruit when listed, then none; a marshal marches to the first target it is offered.
        private static ScriptedProvider Fake(List<ScriptedRequest> seen) => new ScriptedProvider
        {
            Callback = req =>
            {
                seen.Add(req);
                if (req.OfferedActions.Contains(Actions.March))
                    return ScriptedReply.Decide(Actions.March, req.OfferedTargets.First(t => t != TargetRegistry.NoTarget), "Forward.");
                if (req.OfferedActions.Contains(Actions.RaiseTaxes)) return ScriptedReply.Decide(Actions.RaiseTaxes, TargetRegistry.NoTarget, "Gold.");
                if (req.OfferedActions.Contains(Actions.Recruit)) return ScriptedReply.Decide(Actions.Recruit, TargetRegistry.NoTarget, "Soldiers.");
                return ScriptedReply.Decide(ActionRegistry.None);
            },
        };

        private static async Task<(Advice advice, Game game)> Advise(bool free)
        {
            var g = new Game(1, MapDefinition.Standard(), Rules(free));
            var requests = g.BeginTurn();
            g.World.Country("valen").Gold = 1000;
            foreach (var a in g.World.ArmiesOf("valen")) if (!a.Awaiting) a.Wake.Add("test");
            var court = requests.First(r => r.CountryId == "valen");
            var advisor = new Advisor(g.World, "valen", Fake(new List<ScriptedRequest>()));
            var advice = await Await.NotCancelled(advisor.AdviseAsync(court));
            return (advice, g);
        }

        [Test]
        public async Task Free_TheAdvisorGivesSeveralCourtOrders_OnTheMovesLeft_UpToTheModelsCap()
        {
            var (advice, g) = await Advise(free: true);
            var courts = advice.Items.Where(i => i.Request.Moment == Moment.Council).ToList();
            Assert.That(courts.Count, Is.EqualTo(g.Rules.MaxCourtOrders), "taxes, then recruits, up to the model seats' cap");
            Assert.That(advice.Courts.Select(m => m.Action), Is.EqualTo(new[] { Actions.RaiseTaxes, Actions.Recruit, Actions.Recruit }));
            Assert.That(advice.Court, Is.EqualTo(advice.Courts[0]));
            Assert.That(courts.Skip(1).All(i => !i.Request.Legal.Any(m => m.Action == Actions.RaiseTaxes)), Is.True, "taxes are not offered again");
            Assert.That(courts.All(i => i.AsAsked), Is.True, "every court order was one its request offered");
            var given = new List<Move>();
            foreach (var i in courts)
            {
                Assert.That(i.Request.Legal.Select(m => m.ToString()), Is.EqualTo(WritLegality.CourtMovesAfter(g.World, "valen", given).Select(m => m.ToString())));
                given.Add(i.Move);
            }
            var asked = g.World.ArmiesOf("valen").Where(a => Game.AskedFreely(g.World, a)).Select(a => a.Id).OrderBy(x => x).ToList();
            Assert.That(advice.Armies.Keys.OrderBy(x => x), Is.EqualTo(asked), "every army a model realm would be asked about, no writ cap");
            Assert.That(asked, Is.Not.Empty);
        }

        [Test]
        public async Task Writs_TheAdvisorIsAsBefore_OneCourtOrder_ArmiesWithinTheWrits()
        {
            var (advice, g) = await Advise(free: false);
            Assert.That(advice.Items.Count(i => i.Request.Moment == Moment.Council), Is.EqualTo(1));
            Assert.That(advice.Courts, Is.Empty);
            Assert.That(advice.Armies.Count, Is.LessThanOrEqualTo(System.Math.Max(0, g.World.Writs("valen") - 1)));
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using static GreyBoxStrategy.Tests.TestGames;

namespace GreyBoxStrategy.Tests
{
    /// <summary>
    /// The economy's tax lock (#372): a tax change sticks for <see cref="GameRules.TaxLockTurns"/> turns. Offered every turn
    /// and free, the tax level was toggled high and normal 79 times in 448 council answers (run request 002).
    /// </summary>
    public sealed class EconomyTaxLockTests
    {
        private static GameRules Rules()
        {
            var r = ArmyRulesOff(Calm());
            r.Turn = TurnStyle.Writs;
            r.Victory = VictoryMode.Conquest;
            r.MaxTurns = 150;
            r.StalemateTurns = 20;
            r.FreeOrders = true;
            r.Economy = true;
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

        private static Func<CountryRequest, Move> Court(string country, string action) => r =>
            r.CountryId == country && r.Moment == Moment.Council && r.Legal.FirstOrDefault(m => m.Action == action) is Move m ? m : null;

        private static CountryRequest FirstCourt(List<CountryRequest> asked, string country) =>
            asked.First(r => r.CountryId == country && r.Moment == Moment.Council);

        private static bool OffersTaxes(CountryRequest r) => r.Legal.Any(m => Actions.SetsTaxes(m.Action) != null);

        [Test]
        public void ATaxChange_IsNotOfferedAgain_ForTaxLockTurns_AndTheCourtReadsWhenItCan()
        {
            var g = new Game(1, MapDefinition.Standard(), Rules());
            int lockTurns = g.Rules.TaxLockTurns;
            Assume.That(lockTurns, Is.GreaterThan(1));
            var me = g.World.Country("valen");
            var first = Turn(g, Court("valen", Actions.TaxesHigh));
            Assert.That(OffersTaxes(FirstCourt(first, "valen")), Is.True, "never set: offered");
            Assert.That(me.Taxes, Is.EqualTo(TaxLevel.High));
            int setOn = g.World.Turn;

            string why = null;
            List<string> facts = null;
            var second = Turn(g, Court("valen", Actions.TaxesNormal), () =>
            {
                why = WritLegality.WhyNotCourt(g.World, "valen", new Move(Actions.TaxesNormal));
                facts = WritFacts.Standing(g.World, "valen");
            });
            Assert.That(OffersTaxes(FirstCourt(second, "valen")), Is.False, "the turn after: not offered");
            Assert.That(why, Is.EqualTo($"taxes were set 1 turn ago; they can change in {lockTurns - 1}"));
            Assert.That(facts.Any(l => l.Contains("Taxes: high") && l.Contains($"can change in {lockTurns - 1} turns.")), Is.True, string.Join("\n", facts));
            Assert.That(me.Taxes, Is.EqualTo(TaxLevel.High), "it stays");
            Assert.That(OffersTaxes(FirstCourt(second, "orsk")), Is.True, "only the realm that set its taxes waits");

            while (g.World.Turn < setOn + lockTurns - 1)
                Assert.That(OffersTaxes(FirstCourt(Turn(g), "valen")), Is.False, $"turn {g.World.Turn}");
            var open = Turn(g, Court("valen", Actions.TaxesNormal), () => facts = WritFacts.Standing(g.World, "valen"));
            Assert.That(g.World.Turn, Is.EqualTo(setOn + lockTurns));
            Assert.That(OffersTaxes(FirstCourt(open, "valen")), Is.True, $"{lockTurns} turns after the change: offered again");
            Assert.That(facts.Any(l => l.Contains("can change in")), Is.False, "no wait to read");
            Assert.That(me.Taxes, Is.EqualTo(TaxLevel.Normal));
        }
    }
}

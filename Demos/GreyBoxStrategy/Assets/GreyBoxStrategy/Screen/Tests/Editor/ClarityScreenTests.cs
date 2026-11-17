using GreyBoxStrategy.Realm;
using GreyBoxStrategy.Replay;
using NUnit.Framework;

namespace GreyBoxStrategy.Screen.Tests
{
    /// <summary>The council's plain-words lines: when the treasury runs dry, and a general's title read back.</summary>
    public class ClarityScreenTests
    {
        [Test]
        public void The_treasury_line_counts_the_turns_until_gold_falls_below_zero()
        {
            Assert.That(ScreenUI.TreasuryLine(55, 30, 60), Does.StartWith("The treasury runs dry in 2 turns"), "55, then 25, then -5");
            Assert.That(ScreenUI.TreasuryLine(60, 30, 60), Does.StartWith("The treasury runs dry in 3 turns"), "60, 30, 0, then -30: zero is not below zero");
            Assert.That(ScreenUI.TreasuryLine(5, 30, 40), Does.StartWith("The treasury runs dry in 1 turn ("));
            Assert.That(ScreenUI.TreasuryLine(-5, 30, 40), Does.StartWith("The treasury is empty"));
            Assert.That(ScreenUI.TreasuryLine(100, 40, 30), Is.Null, "a treasury that grows says nothing");
            Assert.That(ScreenUI.TreasuryLine(1000, 30, 31), Is.Null, "nor one far from running dry");
        }

        [Test]
        public void A_generals_title_reads_back_into_name_and_traits()
        {
            var (name, traits) = ReplayBuilder.ParseTitle("Edda the Inspiring and Wary");
            Assert.That(name, Is.EqualTo("Edda"));
            Assert.That(traits, Is.EqualTo(new[] { GeneralTraits.Inspiring, GeneralTraits.Wary }));
            Assert.That(ReplayBuilder.ParseTitle("Jory the Horse-born").traits, Is.EqualTo(new[] { GeneralTraits.HorseBorn }));
            Assert.That(ReplayBuilder.ParseTitle("Kael the Siegewright and Forager").traits, Is.EqualTo(new[] { GeneralTraits.Siegewright, GeneralTraits.Forager }));
        }
    }
}

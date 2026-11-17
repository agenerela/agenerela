using System;
using System.Linq;
using Agenerela.Editor.UI;
using NUnit.Framework;

namespace Agenerela.Tests.UI
{
    /// <summary>The four outcome icons of the Decision Log (screen 3 ③, #167).</summary>
    public sealed class OutcomeIconTests
    {
        private static readonly Outcome[] Outcomes = (Outcome[])Enum.GetValues(typeof(Outcome));

        // Boxed: Outcome is internal, so a public test method cannot take it as a parameter.
        private static readonly object[] EachOutcome = Outcomes.Cast<object>().ToArray();

        [TestCaseSource(nameof(EachOutcome))]
        public void EachOutcomeHasABuiltInIconATooltipAndItsClass(object boxed)
        {
            var outcome = (Outcome)boxed;
            var icon = new OutcomeIcon(outcome);

            Assert.That(icon.image, Is.Not.Null, $"Unity has no built-in icon named {OutcomeIcon.IconName(outcome)}");
            Assert.That(icon.tooltip, Does.StartWith(OutcomeIcon.DisplayName(outcome) + ":"));
            Assert.That(icon.ClassListContains(OutcomeIcon.UssClassName), Is.True);
            Assert.That(icon.ClassListContains(OutcomeIcon.ModifierClass(outcome)), Is.True);
        }

        [Test]
        public void TheFourOutcomesLookDifferent()
        {
            var images = Outcomes.Select(OutcomeIcon.Icon).ToList();

            Assert.That(images, Is.Unique);
            Assert.That(Outcomes.Select(OutcomeIcon.ModifierClass), Is.Unique);
        }

        [Test]
        public void ChangingTheOutcomeSwapsTheIconAndClass()
        {
            var icon = new OutcomeIcon(Outcome.Executed) { Outcome = Outcome.Contained };

            Assert.That(icon.image, Is.SameAs(OutcomeIcon.Icon(Outcome.Contained)));
            Assert.That(icon.ClassListContains(OutcomeIcon.ModifierClass(Outcome.Contained)), Is.True);
            Assert.That(icon.ClassListContains(OutcomeIcon.ModifierClass(Outcome.Executed)), Is.False);
        }
    }
}

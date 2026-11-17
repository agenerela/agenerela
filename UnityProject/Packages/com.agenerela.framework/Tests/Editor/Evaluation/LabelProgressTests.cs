using Agenerela.Editor.Evaluation;
using Agenerela.Evaluation;
using NUnit.Framework;

namespace Agenerela.Tests.Evaluation
{
    /// <summary>Label mode's "12 of 40 left to label", and what it says once nothing is left (#158).</summary>
    public sealed class LabelProgressTests
    {
        private EvalFixtures fixture;

        [SetUp]
        public void MakeTheFixture() => fixture = new EvalFixtures();

        [TearDown]
        public void CleanUp() => fixture.Dispose();

        [Test]
        public void AnAnnotatorIsAskedEveryPromptTheyDidNotWrite()
        {
            var set = fixture.Load();

            // Maxim wrote all five; Ana is on situation_002, a settled split.
            Assert.That(LabelProgress.TotalFor(set, "Maxim"), Is.EqualTo(0));
            Assert.That(LabelProgress.TotalFor(set, "Ana"), Is.EqualTo(4));
            Assert.That(LabelProgress.TotalFor(set, "Hero"), Is.EqualTo(5));
        }

        [Test]
        public void TheCountGoesDownAsAnswersAreSaved()
        {
            var set = fixture.Load();
            Assert.That(Progress(set, "Hero").Heading, Is.EqualTo("5 of 5 left to label"));

            set.RecordLabel("Hero", "target_001", new LabelAnswer("move_to", "bridge"));
            set.RecordLabel("Hero", "situation_001", new LabelAnswer("none", TargetRegistry.NoTarget));

            Assert.That(Progress(set, "Hero").Heading, Is.EqualTo("3 of 5 left to label"));
        }

        [Test]
        public void ALabelViewCountsWhatItWasGiven()
        {
            // One answered before the view opens: its queue is shorter than the total.
            var set = fixture.Load();
            set.RecordLabel("Hero", "target_001", new LabelAnswer("move_to", "bridge"));
            var view = new PromptSetLabelView("Hero", BlindPrompt.Queue(set, set.LoadLabels(), "Hero"), LabelChoices.From(set),
                (prompt, answer) => true, LabelProgress.TotalFor(set, "Hero"));

            Assert.That(view.Progress.Heading, Is.EqualTo("4 of 5 left to label"));
        }

        [Test]
        public void NothingLeftSaysWhy()
        {
            Assert.That(new LabelProgress(0, 0, 0).NothingLeft("Maxim"), Does.Contain("every prompt in the set is one you wrote"));
            Assert.That(new LabelProgress(40, 0, 0).NothingLeft("Hero"), Does.StartWith("All done").And.Contain("all 40"));
            Assert.That(new LabelProgress(40, 3, 3).NothingLeft("Hero"), Does.StartWith("3 of 40 left").And.Contain("skipped"));
        }

        private static LabelProgress Progress(PromptSet set, string annotator)
        {
            return new LabelProgress(LabelProgress.TotalFor(set, annotator), BlindPrompt.Queue(set, set.LoadLabels(), annotator).Count, 0);
        }
    }
}

using System.IO;
using System.Linq;
using Agenerela.Evaluation;
using NUnit.Framework;

namespace Agenerela.Tests.Evaluation
{
    /// <summary>
    /// Agreement is computed from each person's own answer (findings.md, 8 October), and a split is
    /// settled only by a call a person makes, keeping both answers (#134).
    /// </summary>
    public sealed class AgreementTests
    {
        private EvalFixtures fixture;

        [SetUp]
        public void MakeTheFixture() => fixture = new EvalFixtures();

        [TearDown]
        public void DeleteTheFixture() => fixture.Dispose();

        [Test]
        public void TwoAnnotatorsFilesGiveTheRightCountAndSplitList()
        {
            fixture.WriteLabels("Bo", "command_with_target",
                "{ \"target_001\": \"move_to/bridge\", \"target_002\": \"move_to/wolf\", \"target_003\": \"move_to/docks\" }");
            fixture.WriteLabels("Bo", "situation", "{ \"situation_001\": \"none/no_target\" }");
            var set = fixture.Load();

            var report = AgreementReport.Compute(set, set.LoadLabels());

            // target_001 and situation_001 agree with Maxim. target_002 splits. target_003 is an
            // open choice with no labels, so only Bo's answer counts. situation_002 was settled
            // between Maxim and Ana, who split.
            Assert.That(report.AnsweredByTwoOrMore, Is.EqualTo(4));
            Assert.That(report.Unanimous, Is.EqualTo(2));
            Assert.That(report.PercentAgreement, Is.EqualTo(50.0));
            Assert.That(report.Splits.Select(s => s.PromptId), Is.EqualTo(new[] { "target_002", "situation_002" }));

            var split = report.For("target_002");
            Assert.That(split.Answered, Is.EqualTo(2));
            Assert.That(split.Agreeing, Is.EqualTo(1));
            Assert.That(split.Answers["Maxim"].ToString(), Is.EqualTo("attack_target/wolf"));
            Assert.That(split.Answers["Bo"].ToString(), Is.EqualTo("move_to/wolf"));

            Assert.That(report.For("target_003").Answered, Is.EqualTo(1));
        }

        [Test]
        public void AThirdAnnotatorIsCountedOnTheSamePrompt()
        {
            fixture.WriteLabels("Bo", "command_with_target", "{ \"target_002\": \"move_to/wolf\" }");
            fixture.WriteLabels("Cy", "command_with_target", "{ \"target_002\": \"attack_target/wolf\" }");
            var set = fixture.Load();

            var prompt = AgreementReport.Compute(set, set.LoadLabels()).For("target_002");

            Assert.That(prompt.Answered, Is.EqualTo(3));
            Assert.That(prompt.Agreeing, Is.EqualTo(2));
            Assert.That(prompt.IsSplit, Is.True);
        }

        [Test]
        public void ASettledPromptsLabelsWinOverALaterFile()
        {
            fixture.WriteLabels("Ana", "situation", "{ \"situation_002\": \"none/no_target\" }");
            var set = fixture.Load();

            var prompt = AgreementReport.Compute(set, set.LoadLabels()).For("situation_002");

            Assert.That(prompt.Answers["Ana"].ToString(), Is.EqualTo("stop_following/no_target"));
        }

        [Test]
        public void NobodysAnswerMeansNoPromptToReport()
        {
            var set = fixture.Load();
            set.Categories[0].Prompts[0].LabelledBy.Clear();

            Assert.That(AgreementReport.Compute(set, set.LoadLabels()).For("target_001"), Is.Null);
        }

        [Test]
        public void SettlingKeepsEveryAnswerAndChangesOnlyThePrompt()
        {
            fixture.WriteLabels("Bo", "command_with_target", "{ \"target_002\": \"move_to/wolf\" }");
            var set = fixture.Load();
            string boFile = set.LabelPath("Bo", "command_with_target");
            string boBefore = File.ReadAllText(boFile);
            var split = AgreementReport.Compute(set, set.LoadLabels()).For("target_002");

            set.Settle(split, "move_to", new ExpectedTarget("wolf"), "Dealing with it means walking over first.");

            var reloaded = fixture.Load();
            reloaded.TryFind("target_002", out var prompt, out _);
            Assert.That(prompt.ExpectedAction, Is.EqualTo("move_to"));
            Assert.That(prompt.ExpectedTarget.Ids, Is.EqualTo(new[] { "wolf" }));
            Assert.That(prompt.Note, Is.EqualTo("Dealing with it means walking over first."));
            Assert.That(prompt.Labels["Maxim"], Is.EqualTo("attack_target/wolf"), "The writer's first answer is kept.");
            Assert.That(prompt.Labels["Bo"], Is.EqualTo("move_to/wolf"));
            Assert.That(prompt.LabelledBy, Is.EqualTo(new[] { "Maxim", "Bo" }));
            Assert.That(File.ReadAllText(boFile), Is.EqualTo(boBefore), "A label file is never edited.");

            var after = AgreementReport.Compute(reloaded, reloaded.LoadLabels()).For("target_002");
            Assert.That(after.IsSplit, Is.True, "Agreement is computed from the answers, so settling does not erase the split.");
        }

        [Test]
        public void SettlingAPromptWhoseLabelledByIsNullStillKeepsEveryAnswer()
        {
            fixture.WriteLabels("Bo", "command_with_target", "{ \"target_002\": \"move_to/wolf\" }");
            var set = fixture.Load();
            set.TryFind("target_002", out var prompt, out _);
            var split = AgreementReport.Compute(set, set.LoadLabels()).For("target_002");
            prompt.LabelledBy = null;

            set.Settle(split, "move_to", new ExpectedTarget("wolf"));

            Assert.That(prompt.LabelledBy, Is.EqualTo(new[] { "Bo", "Maxim" }));
        }

        [Test]
        public void SettlingAsAnOpenChoiceWritesAList()
        {
            fixture.WriteLabels("Bo", "command_with_target", "{ \"target_001\": \"move_to/docks\" }");
            var set = fixture.Load();
            var split = AgreementReport.Compute(set, set.LoadLabels()).For("target_001");

            set.Settle(split, "move_to", new ExpectedTarget("bridge", "docks"));

            string saved = File.ReadAllText(set.CategoryPath(set.FindCategory("command_with_target")));
            Assert.That(saved, Does.Contain("\"bridge\",").And.Contain("\"docks\""));
            fixture.Load().TryFind("target_001", out var prompt, out _);
            Assert.That(prompt.ExpectedTarget.IsOpenChoice, Is.True);
            Assert.That(prompt.Note, Is.Null, "No note given, none written.");
        }
    }
}

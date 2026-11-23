using System.Collections.Generic;
using System.IO;
using System.Linq;
using Agenerela.Editor.Evaluation;
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
        public void KappaUsesEachPersonsMarginalsOnCompleteActionTargetAnswers()
        {
            // 6 a/a, 2 a/b, 1 b/a, 1 b/b. Both choices have the same action, different targets.
            // p_o = .7, p_e = .8*.7 + .2*.3 = .62; kappa = (.7-.62)/(1-.62) = 4/19.
            var pair = FromAnswers("aaaaaaaabb", "aaaaaabbab").Pairwise.Single();

            Assert.That(pair.First, Is.EqualTo("Ana"));
            Assert.That(pair.Second, Is.EqualTo("Bo"));
            Assert.That(pair.AnsweredByBoth, Is.EqualTo(10));
            Assert.That(pair.Agreed, Is.EqualTo(7));
            Assert.That(pair.PercentAgreement, Is.EqualTo(70));
            Assert.That(pair.ChanceAgreement, Is.EqualTo(.62).Within(1e-12));
            Assert.That(pair.CohensKappa, Is.EqualTo(4.0 / 19).Within(1e-12));
        }

        [TestCase("abca", "abca", 1.0)]
        [TestCase("aabb", "abab", 0.0)]
        [TestCase("aabb", "bbaa", -1.0)]
        [TestCase("aaaa", "bbbb", 0.0)]
        public void KappaCanBePerfectChanceOrNegative(string first, string second, double expected)
        {
            Assert.That(FromAnswers(first, second).Pairwise.Single().CohensKappa,
                Is.EqualTo(expected).Within(1e-12));
        }

        [Test]
        public void ConstantIdenticalAnswersGivePerfectRawAgreementButUndefinedKappa()
        {
            var pair = FromAnswers("aaaa", "aaaa").Pairwise.Single();

            Assert.That(pair.PercentAgreement, Is.EqualTo(100));
            Assert.That(pair.ChanceAgreement, Is.EqualTo(1));
            Assert.That(pair.CohensKappa, Is.Null);
        }

        [Test]
        public void MissingOrMalformedAnswersDoNotEnterEitherMarginal()
        {
            var pair = FromAnswers("aabb--?", "ababcc?").Pairwise.Single();

            Assert.That(pair.AnsweredByBoth, Is.EqualTo(4));
            Assert.That(pair.PercentAgreement, Is.EqualTo(50));
            Assert.That(pair.CohensKappa, Is.EqualTo(0).Within(1e-12));
        }

        [TestCase("", "")]
        [TestCase("aabb", "----")]
        [TestCase("aa--", "--bb")]
        public void NoSharedPromptsMeansNoPairToReport(string first, string second)
        {
            Assert.That(FromAnswers(first, second).Pairwise, Is.Empty);
        }

        [Test]
        public void EachPairUsesOnlyItsOwnOverlapInStableNameOrder()
        {
            fixture.WriteLabels("Bo", "command_with_target", "{ \"target_001\": \"move_to/bridge\", \"target_002\": \"move_to/wolf\" }");
            fixture.WriteLabels("Cy", "command_with_target", "{ \"target_002\": \"attack_target/wolf\" }");
            var set = fixture.Load();

            var pairs = AgreementReport.Compute(set, set.LoadLabels()).Pairwise;

            Assert.That(pairs.Select(p => p.First + "/" + p.Second),
                Is.EqualTo(new[] { "Ana/Maxim", "Bo/Cy", "Bo/Maxim", "Cy/Maxim" }));
            Assert.That(pairs.Single(p => p.First == "Bo" && p.Second == "Maxim").AnsweredByBoth, Is.EqualTo(2));
            Assert.That(pairs.Single(p => p.First == "Bo" && p.Second == "Cy").PercentAgreement, Is.Zero);
            Assert.That(pairs.Single(p => p.First == "Cy" && p.Second == "Maxim").PercentAgreement, Is.EqualTo(100));
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
            var beforePair = AgreementReport.Compute(set, set.LoadLabels()).Pairwise.Single(p => p.First == "Bo");

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
            var afterPair = AgreementReport.Compute(reloaded, reloaded.LoadLabels()).Pairwise.Single(p => p.First == "Bo");
            Assert.That(afterPair.PercentAgreement, Is.EqualTo(beforePair.PercentAgreement));
            Assert.That(afterPair.CohensKappa, Is.EqualTo(beforePair.CohensKappa));
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

        [Test]
        public void RecordingUnanimousAnswersPreservesTheExpectedAnswerAndLeavesSplitsAlone()
        {
            fixture.WriteLabels("Bo", "command_with_target",
                "{ \"target_001\": \"move_to/bridge\", \"target_002\": \"move_to/wolf\" }");
            var set = fixture.Load();
            string file = set.LabelPath("Bo", "command_with_target");
            string before = File.ReadAllText(file);
            var report = AgreementReport.Compute(set, set.LoadLabels());

            Assert.That(PromptSetAgreementView.RecordAgreedAnswers(set, report), Is.EqualTo(1));

            var reloaded = fixture.Load();
            reloaded.TryFind("target_001", out var agreed, out _);
            Assert.That(agreed.ExpectedAction, Is.EqualTo("move_to"));
            Assert.That(agreed.ExpectedTarget.Ids, Is.EqualTo(new[] { "bridge" }));
            Assert.That(agreed.Labels["Maxim"], Is.EqualTo("move_to/bridge"));
            Assert.That(agreed.Labels["Bo"], Is.EqualTo("move_to/bridge"));
            reloaded.TryFind("target_002", out var split, out _);
            Assert.That(split.Labels, Is.Null, "Splits must be discussed before settling.");
            Assert.That(File.ReadAllText(file), Is.EqualTo(before));
            var after = AgreementReport.Compute(reloaded, reloaded.LoadLabels());
            Assert.That(after.Pairwise.Single(p => p.First == "Bo").CohensKappa,
                Is.EqualTo(report.Pairwise.Single(p => p.First == "Bo").CohensKappa));
            Assert.That(PromptSetAgreementView.RecordAgreedAnswers(reloaded, after), Is.Zero);
        }

        private static AgreementReport FromAnswers(string first, string second)
        {
            var category = new PromptCategory { Id = "test" };
            for (int i = 0; i < first.Length; i++)
            {
                var labels = new Dictionary<string, string>();
                Add("Ana", first[i]);
                Add("Bo", second[i]);
                category.Prompts.Add(new EvalPrompt { Id = "test_" + i, Labels = labels });

                void Add(string name, char answer)
                {
                    if (answer != '-')
                    {
                        labels[name] = answer == 'a' ? "move_to/bridge" : answer == 'b' ? "move_to/docks"
                            : answer == 'c' ? "none/no_target" : "malformed";
                    }
                }
            }

            var set = new PromptSet();
            set.Categories.Add(category);
            return AgreementReport.Compute(set, new AnnotatorLabels[0]);
        }
    }
}

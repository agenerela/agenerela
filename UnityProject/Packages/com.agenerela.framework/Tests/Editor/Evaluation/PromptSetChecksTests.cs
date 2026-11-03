using System.IO;
using System.Linq;
using Agenerela.Evaluation;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Agenerela.Tests.Evaluation
{
    /// <summary>
    /// Each rule for writing a prompt (the README in <c>Assets/Evaluation/</c>, #134) has a test
    /// that breaks it once, from a prompt that otherwise breaks nothing. Each was seen to fail with
    /// its check switched off.
    /// </summary>
    public sealed class PromptSetChecksTests
    {
        private EvalFixtures fixture;
        private PromptSet set;
        private PromptCategory commands;
        private AgentProfile profile;

        [SetUp]
        public void LoadTheFixture()
        {
            fixture = new EvalFixtures();
            set = fixture.Load();
            commands = set.FindCategory("command_with_target");
            profile = Fixtures.VillageGuardProfile();
        }

        [TearDown]
        public void CleanUp()
        {
            fixture.Dispose();
            Fixtures.DestroyCreated();
        }

        [Test]
        public void TheFixtureBreaksNoRule()
        {
            Assert.That(PromptSetChecks.Check(set, profile), Is.Empty);
        }

        [Test]
        public void OurSetBreaksNoRule()
        {
            var ours = PromptSet.Load(Path.Combine(Application.dataPath, "Evaluation", "agent.json"));

            Assert.That(PromptSetChecks.Check(ours).Select(p => p.ToString()), Is.Empty);
        }

        [Test]
        public void ATargetMustBeRegistered()
        {
            var prompt = Valid();
            prompt.ExpectedTarget = new ExpectedTarget("north_tower");

            AssertBreaks(prompt, PromptRule.TargetNotRegistered);
        }

        [Test]
        public void EveryTargetOfAnOpenChoiceMustBeRegistered()
        {
            var prompt = Valid();
            prompt.ExpectedTarget = new ExpectedTarget("bridge", "north_tower");

            AssertBreaks(prompt, PromptRule.TargetNotRegistered);
        }

        [Test]
        public void NoneTakesNoTarget()
        {
            var prompt = Valid();
            prompt.ExpectedAction = ActionRegistry.None;
            prompt.ExpectedTarget = new ExpectedTarget("bridge");

            AssertBreaks(prompt, PromptRule.NoneTakesNoTarget);
        }

        [Test]
        public void AnActionThatNeedsATargetDoesNotTakeNoTarget()
        {
            var prompt = Valid();
            prompt.ExpectedTarget = ExpectedTarget.NoTarget;

            AssertBreaks(prompt, PromptRule.ActionNeedsTarget);
        }

        [Test]
        public void AnActionThatTakesNoTargetIsNotGivenOne()
        {
            var prompt = Valid();
            prompt.ExpectedAction = "follow_player";

            AssertBreaks(prompt, PromptRule.ActionTakesNoTarget);
        }

        [Test]
        public void TheStateMustMatchTheActionsOnlyWhen()
        {
            var prompt = Valid();
            prompt.ExpectedAction = "stop_following";
            prompt.ExpectedTarget = ExpectedTarget.NoTarget;

            AssertBreaks(prompt, PromptRule.PreconditionMismatch);

            prompt.PreconditionState["isFollowing"] = true;
            Assert.That(PromptSetChecks.CheckPrompt(set, commands, prompt, profile), Is.Empty, "isFollowing = true satisfies it.");
        }

        [TestCase("Proceed to the bridge.")]
        [TestCase("proceed  to the Wolf!")]
        [TestCase("Accompany me on my rounds")]
        public void TheStimulusIsNotAnActionsExample(string stimulus)
        {
            var prompt = Valid();
            prompt.Stimulus = stimulus;

            AssertBreaks(prompt, PromptRule.StimulusIsExample);
        }

        [Test]
        public void TheStimulusIsNotTheIdleExample()
        {
            var prompt = Valid();
            prompt.Stimulus = "You seem well rested today.";

            AssertBreaks(prompt, PromptRule.StimulusIsExample);
        }

        [Test]
        public void ThePlaceholderStandsForAnyText()
        {
            Assert.That(PromptSetChecks.MatchesExample("Proceed to the bridge and wait for me there", "Proceed to the {0}."), Is.True,
                "{0} is a target filled in, so anything after the example's start can be one.");
        }

        [Test]
        public void AStimulusThatOnlyResemblesAnExampleIsFine()
        {
            Assert.That(PromptSetChecks.MatchesExample("Please proceed to the bridge.", "Proceed to the {0}."), Is.False);
            Assert.That(PromptSetChecks.MatchesExample("Accompany me", "Accompany me on my rounds."), Is.False);
        }

        [Test]
        public void IdsFollowTheCategoryPrefix()
        {
            var prompt = Valid();
            prompt.Id = "command_004";

            AssertBreaks(prompt, PromptRule.IdMalformed);
        }

        [TestCase("target_4")]
        [TestCase("target_")]
        [TestCase("target_00a")]
        public void IdsEndInThreeDigits(string id)
        {
            var prompt = Valid();
            prompt.Id = id;

            AssertBreaks(prompt, PromptRule.IdMalformed);
        }

        [Test]
        public void IdsAreUniqueAcrossTheSet()
        {
            var prompt = Valid();
            prompt.Id = "target_001";

            AssertBreaks(prompt, PromptRule.IdDuplicate);
        }

        [Test]
        public void TheActionIsOneTheSetDeclares()
        {
            var prompt = Valid();
            prompt.ExpectedAction = "open_gate";

            AssertBreaks(prompt, PromptRule.ActionNotDeclared);
        }

        [Test]
        public void AnEmptyStimulusIsIncomplete()
        {
            var prompt = Valid();
            prompt.Stimulus = " ";

            AssertBreaks(prompt, PromptRule.Incomplete);
        }

        [Test]
        public void WithoutAProfileTheExamplesAreNotChecked()
        {
            var prompt = Valid();
            prompt.Stimulus = "Proceed to the bridge.";

            Assert.That(PromptSetChecks.CheckPrompt(set, commands, prompt), Is.Empty);
        }

        [Test]
        public void AProfileThatMatchesTheSetHasNoDifferences()
        {
            Assert.That(PromptSetChecks.CompareWithProfile(set, profile), Is.Empty);
        }

        [Test]
        public void AnActionOnlyTheProfileHasIsReported()
        {
            set.Actions.RemoveAll(a => a.Id == "attack_target");

            Assert.That(PromptSetChecks.CompareWithProfile(set, profile), Has.Some.Contains("attack_target"));
        }

        [Test]
        public void AnActionOnlyTheSetHasIsReported()
        {
            set.Actions.Add(new DeclaredAction { Id = "open_gate" });

            Assert.That(PromptSetChecks.CompareWithProfile(set, profile), Has.Some.Contains("open_gate"));
        }

        [Test]
        public void ADifferentNeedForATargetIsReportedAndNotFixed()
        {
            set.FindAction("move_to").NeedsTarget = false;

            Assert.That(PromptSetChecks.CompareWithProfile(set, profile), Has.Some.Contains("move_to"));
            Assert.That(set.FindAction("move_to").NeedsTarget, Is.False, "The set is left as it was.");
        }

        // A new prompt in the commands category that breaks no rule, so each test breaks exactly one.
        private EvalPrompt Valid()
        {
            var prompt = new EvalPrompt
            {
                Id = "target_004",
                Label = "Player",
                Stimulus = "Could you head down to the wolf?",
                ExpectedAction = "move_to",
                ExpectedTarget = new ExpectedTarget("wolf"),
                PreconditionState = new JObject { ["isFollowing"] = false },
            };
            prompt.LabelledBy.Add("Maxim");
            commands.Prompts.Add(prompt);

            Assert.That(PromptSetChecks.CheckPrompt(set, commands, prompt, profile), Is.Empty, "The valid prompt is not valid.");
            return prompt;
        }

        private void AssertBreaks(EvalPrompt prompt, PromptRule rule)
        {
            var problems = PromptSetChecks.CheckPrompt(set, commands, prompt, profile);
            Assert.That(problems.Select(p => p.Rule), Does.Contain(rule), string.Join("\n", problems));
            Assert.That(PromptSetChecks.Check(set, profile).Select(p => p.Rule), Does.Contain(rule), "Check covers the whole set.");
        }
    }
}

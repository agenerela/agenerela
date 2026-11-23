using System.Collections.Generic;
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
            string path = Path.Combine(EvalFixtures.OurSetFolder(), "agent.json");

            // With the guard's profile, so hard rule 4 is checked too: no prompt of ours is one of
            // its few-shot examples.
            var ours = PromptSet.Load(path);

            Assert.That(PromptSetChecks.Check(ours, profile).Select(p => p.ToString()), Is.Empty);
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
            set.FindAction("follow_player").TakesNoTarget = true;
            var prompt = Valid();
            prompt.ExpectedAction = "follow_player";

            AssertBreaks(prompt, PromptRule.ActionTakesNoTarget);
        }

        [Test]
        public void AnActionWhoseTargetIsOptionalTakesEitherAnswer()
        {
            // DR-020: needsTarget false on its own leaves the target optional. Reading it as
            // "takes no target" is what this check used to do, and it flagged a correct prompt.
            var prompt = Valid();
            prompt.ExpectedAction = "follow_player";

            Assert.That(Rules(prompt), Has.No.Member(PromptRule.ActionTakesNoTarget), "A target is allowed.");

            prompt.ExpectedTarget = ExpectedTarget.NoTarget;

            Assert.That(Rules(prompt), Has.No.Member(PromptRule.ActionNeedsTarget), "So is no_target.");
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

        [TestCase("The wolf looks lovely at dusk.")]
        [TestCase("you walk too FAST")]
        public void TheStimulusIsNotAnActionsCounterExample(string stimulus)
        {
            // Shown answered with none, so scoring on it measures nothing (hard rule 4, #190).
            profile.Actions.Find(a => a.Action.Id == "move_to").Action.CounterExampleStimulus = "The {0} looks lovely at dusk.";
            profile.Actions.Find(a => a.Action.Id == "stop_following").Action.CounterExampleStimulus = "You walk too fast.";
            var prompt = Valid();
            prompt.Stimulus = stimulus;

            AssertBreaks(prompt, PromptRule.StimulusIsExample);
        }

        [TestCase("On my way to the wolf.")]
        [TestCase("it does, AT that!")]
        [TestCase("Rested and READY.")]
        public void TheStimulusIsNotAnExamplesStatement(string stimulus)
        {
            // A statement is shown in the prompt too, so scoring on one measures nothing (#190, G14).
            var moveTo = profile.Actions.Find(a => a.Action.Id == "move_to").Action;
            moveTo.ExampleStatement = "On my way to the {0}.";
            moveTo.CounterExampleStatement = "It does, at that.";
            profile.IdleExampleStatement = "Rested and ready.";
            var prompt = Valid();
            prompt.Stimulus = stimulus;

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

        [Test]
        public void ADifferenceInTakingNoTargetIsReported()
        {
            set.FindAction("follow_player").TakesNoTarget = true;

            Assert.That(PromptSetChecks.CompareWithProfile(set, profile), Has.Some.Contains("follow_player"));
        }

        // ---- An example that carries its situation (#216 item 1) ----------------------------------

        [Test]
        public void AnExampleWithASituationMatchesOnlyItsStimulusAndSituationTogether()
        {
            var move = profile.Actions.Find(a => a.Action.Id == "move_to").Action;
            move.ExampleObservations = new[] { "A storm is coming from the {0}." };
            var prompt = Valid();
            prompt.Stimulus = "Proceed to the bridge.";

            Assert.That(PromptSetChecks.CheckPrompt(set, commands, prompt, profile).Select(p => p.Rule),
                Has.No.Member(PromptRule.StimulusIsExample), "Another situation is another prompt.");

            prompt.Observations.Add("A storm is coming from the wolf.");
            AssertBreaks(prompt, PromptRule.StimulusIsExample);
            Assert.That(PromptSetChecks.CheckPrompt(set, commands, prompt, profile).Single().Message, Does.Contain("in its situation"));
        }

        [Test]
        public void ShownWithoutItsSituationAnExampleIsJudgedOnItsStimulusAlone()
        {
            profile.Actions.Find(a => a.Action.Id == "move_to").Action.ExampleObservations = new[] { "A storm is coming." };
            var prompt = Valid();
            prompt.Stimulus = "Proceed to the bridge.";
            var found = new System.Collections.Generic.List<PromptRule>();

            PromptSetChecks.CheckExamples(prompt, profile, (rule, message) => found.Add(rule), withSituations: false);

            Assert.That(found, Does.Contain(PromptRule.StimulusIsExample));
        }

        [Test]
        public void MatchesSituationNeedsEveryLine()
        {
            Assert.That(PromptSetChecks.MatchesSituation(new[] { "A storm.", "Night falls." }, new[] { "a storm" }), Is.True);
            Assert.That(PromptSetChecks.MatchesSituation(new[] { "A storm." }, new[] { "A storm.", "Night falls." }), Is.False);
            Assert.That(PromptSetChecks.MatchesSituation(null, null), Is.True, "No situation: the stimulus alone decides.");
        }

        // ---- History turns (#147) -----------------------------------------------------------------

        private static HistoryTurn Turn(string action = "follow_player", string target = "no_target", string stimulus = "Walk with me a while.")
            => new HistoryTurn { Stimulus = stimulus, Action = action, Target = target };

        [Test]
        public void AValidHistoryBreaksNoRule()
        {
            var prompt = Valid();
            prompt.History = new List<HistoryTurn> { Turn(), Turn(ActionRegistry.None, "no_target", "Nice weather.") };

            Assert.That(Rules(prompt), Is.Empty);
        }

        [Test]
        public void AHistoryTurnIsJudgedOnNoState()
        {
            // stop_following needs isFollowing, which the prompt's state (after the turns) need not have.
            var prompt = Valid();
            prompt.History = new List<HistoryTurn> { Turn(), Turn("stop_following") };

            Assert.That(Rules(prompt), Is.Empty);
        }

        [Test]
        public void AHistoryTurnsTargetMustBeRegistered()
        {
            var prompt = Valid();
            prompt.History = new List<HistoryTurn> { Turn("move_to", "north_tower") };

            AssertBreaks(prompt, PromptRule.TargetNotRegistered);
            Assert.That(PromptSetChecks.CheckPrompt(set, commands, prompt, profile)[0].Message, Does.Contain("History turn 1"));
        }

        [Test]
        public void AHistoryTurnsNoneTakesNoTarget()
        {
            var prompt = Valid();
            prompt.History = new List<HistoryTurn> { Turn(), Turn(ActionRegistry.None, "bridge") };

            AssertBreaks(prompt, PromptRule.NoneTakesNoTarget);
            Assert.That(PromptSetChecks.CheckPrompt(set, commands, prompt, profile)[0].Message, Does.Contain("History turn 2"));
        }

        [Test]
        public void AHistoryTurnsActionMustBeDeclared()
        {
            var prompt = Valid();
            prompt.History = new List<HistoryTurn> { Turn("dance") };

            AssertBreaks(prompt, PromptRule.ActionNotDeclared);
        }

        [Test]
        public void AHistoryTurnNeedsAStimulusAnActionAndATarget()
        {
            var prompt = Valid();
            prompt.History = new List<HistoryTurn> { Turn(stimulus: " ") };
            AssertBreaks(prompt, PromptRule.Incomplete);

            prompt.History = new List<HistoryTurn> { Turn(target: null) };
            AssertBreaks(prompt, PromptRule.Incomplete);

            prompt.History = new List<HistoryTurn> { null };
            AssertBreaks(prompt, PromptRule.Incomplete);
        }

        [Test]
        public void AHistoryTurnsMoveNeedsATarget()
        {
            var prompt = Valid();
            prompt.History = new List<HistoryTurn> { Turn("move_to", "no_target") };

            AssertBreaks(prompt, PromptRule.ActionNeedsTarget);
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

        /// <summary>The rules <paramref name="prompt"/> breaks, for asserting that it breaks none.</summary>
        private IEnumerable<PromptRule> Rules(EvalPrompt prompt)
            => PromptSetChecks.CheckPrompt(set, commands, prompt, profile).Select(p => p.Rule);

        private void AssertBreaks(EvalPrompt prompt, PromptRule rule)
        {
            var problems = PromptSetChecks.CheckPrompt(set, commands, prompt, profile);
            Assert.That(problems.Select(p => p.Rule), Does.Contain(rule), string.Join("\n", problems));
            Assert.That(PromptSetChecks.Check(set, profile).Select(p => p.Rule), Does.Contain(rule), "Check covers the whole set.");
        }
    }
}

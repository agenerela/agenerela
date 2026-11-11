using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Agenerela.Evaluation;
using Agenerela.Providers;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using static Agenerela.Tests.Evaluation.RunnerFixtures;

namespace Agenerela.Tests.Evaluation
{
    // The A/B runner through FakeProvider (#145): the five outcomes, the staged preconditions, the
    // interleaving and the warm-up. Every run is awaited through Fixtures.NotCancelled, since the
    // Test Framework counts a cancelled Task as passed.
    public sealed class EvalRunnerTests
    {
        private RunnerFixtures fixtures;

        [SetUp]
        public void SetUp()
        {
            fixtures = new RunnerFixtures();
        }

        [TearDown]
        public void TearDown()
        {
            fixtures.Dispose();
            Fixtures.DestroyCreated();
        }

        private static readonly string Bridge = Prompt("target_001", "Meet me over at the bridge.", "move_to", "bridge", false);
        private static readonly string Escort = Prompt("target_002", "Walk with me a while.", "follow_player", "no_target", false);
        private static readonly string Quiet = Prompt("situation_001", "The square is empty.", "none", "no_target", false, "Situation");

        // ---- The five outcomes -------------------------------------------------------------------

        [Test]
        public async Task TheLabelledActionAndTargetIsCorrect()
        {
            var report = await Run(new[] { Bridge }, null, Reply("move_to", "bridge"));

            Assert.That(report.Answers.Select(a => a.Outcome), Is.All.EqualTo(DecisionOutcome.Correct));
            Assert.That(report.Arms[0].Overall.Correct, Is.EqualTo(1));
        }

        [Test]
        public async Task AnyTargetOfAnOpenChoiceIsCorrect()
        {
            var open = Prompt("target_001", "Go wait by the water.", "move_to", "[\"bridge\", \"docks\"]", false);

            var report = await Run(new[] { open }, null, Reply("move_to", "docks"));

            Assert.That(report.Answers[0].Outcome, Is.EqualTo(DecisionOutcome.Correct));
        }

        [Test]
        public async Task AnotherLegalAnswerIsAWrongLegalAction()
        {
            var report = await Run(new[] { Bridge }, null, Reply("attack_target", "wolf"));

            Assert.That(report.Answers.Select(a => a.Outcome), Is.All.EqualTo(DecisionOutcome.WrongLegalAction));
            Assert.That(report.Answers[0].Guard, Is.Null);
        }

        [Test]
        public async Task TheRightActionOnTheWrongTargetIsAWrongLegalAction()
        {
            var report = await Run(new[] { Bridge }, null, Reply("move_to", "docks"));

            Assert.That(report.Answers[0].Outcome, Is.EqualTo(DecisionOutcome.WrongLegalAction));
        }

        [Test]
        public async Task ChoosingNoneForAnActionIsAWrongLegalActionNotARejection()
        {
            var report = await Run(new[] { Escort }, null, Reply("none", "no_target"));

            Assert.That(report.Answers[0].Outcome, Is.EqualTo(DecisionOutcome.WrongLegalAction));
        }

        [Test]
        public async Task AGuardStoppingAnAnswerToNoneIsContained()
        {
            // stop_following is not offered to an agent that is not following: the legality guard
            // stops it, and the label was none.
            var report = await Run(null, new[] { Quiet }, Reply("stop_following", "no_target"));

            var answer = report.Answers[0];
            Assert.That(answer.Outcome, Is.EqualTo(DecisionOutcome.ContainedByGuard));
            Assert.That(answer.Guard, Is.EqualTo(nameof(SchemaLegalityGuard)));
            Assert.That(answer.GuardReason, Is.Not.Empty);
            Assert.That(answer.ModelAction, Is.EqualTo("stop_following"), "The model's own answer is kept.");
            Assert.That(answer.Action, Is.EqualTo(ActionRegistry.None));
        }

        [Test]
        public async Task AGuardStoppingAnAnswerToAnActionIsARejection()
        {
            var report = await Run(new[] { Escort }, null, Reply("stop_following", "no_target"));

            Assert.That(report.Answers.Select(a => a.Outcome), Is.All.EqualTo(DecisionOutcome.RejectedWhenActionExpected));
        }

        [Test]
        public async Task NoDecisionIsAPipelineError()
        {
            var report = await Run(new[] { Bridge }, null, "this is not JSON", warmUp: 0);

            var answer = report.Answers[0];
            Assert.That(answer.Outcome, Is.EqualTo(DecisionOutcome.PipelineError));
            Assert.That(answer.ErrorKind, Is.EqualTo(nameof(PipelineErrorKind.UnreadableReply)));
            Assert.That(answer.ModelAction, Is.Null);
        }

        [Test]
        public async Task TheOutcomeIsWrittenToTheDecisionsTelemetry()
        {
            var report = await Run(new[] { Bridge, Escort }, new[] { Quiet }, Reply("stop_following", "no_target"));

            foreach (var answer in report.Answers)
            {
                Assert.That(answer.Result.Telemetry.Outcome, Is.EqualTo(answer.Outcome), answer.PromptId);
            }
        }

        // Classify on its own, for the verdict kind no FakeProvider reply can produce here.
        [TestCase(GuardVerdictKind.RewriteToNone, "none", DecisionOutcome.ContainedByGuard)]
        [TestCase(GuardVerdictKind.Reject, "none", DecisionOutcome.ContainedByGuard)]
        [TestCase(GuardVerdictKind.RewriteToNone, "move_to", DecisionOutcome.RejectedWhenActionExpected)]
        [TestCase(GuardVerdictKind.Reject, "move_to", DecisionOutcome.RejectedWhenActionExpected)]
        public void AnyVerdictCountsAsAGuardFiring(GuardVerdictKind kind, string label, DecisionOutcome expected)
        {
            var prompt = new EvalPrompt { ExpectedAction = label, ExpectedTarget = label == "none" ? ExpectedTarget.NoTarget : new ExpectedTarget("bridge") };
            var verdict = (kind == GuardVerdictKind.Reject ? GuardVerdict.Reject("no") : GuardVerdict.RewriteToNone("no")).From("SomeGuard");
            var none = new AgentDecision(ActionRegistry.None, TargetRegistry.NoTarget, "");
            var result = new DecisionResult(none, new DecisionTelemetry(), new AgentDecision("move_to", "bridge", ""), verdict, null, null);

            Assert.That(EvalRunner.Classify(prompt, result), Is.EqualTo(expected));
        }

        // ---- Staging -----------------------------------------------------------------------------

        [Test]
        public async Task AStagedPreconditionChangesWhatIsOffered()
        {
            var hold = Prompt("target_001", "Hold on, stay right there.", "stop_following", "no_target", true);
            var provider = new FakeProvider(Reply("none", "no_target"));

            await Run(new[] { hold, Escort }, null, provider);

            // The set declares onlyWhen for stop_following alone, so follow_player is offered in both.
            var following = Request(provider, "Hold on, stay right there.").Schema.Fields[0].AllowedValues;
            var notFollowing = Request(provider, "Walk with me a while.").Schema.Fields[0].AllowedValues;
            Assert.That(following, Does.Contain("stop_following").And.Contain("follow_player"));
            Assert.That(notFollowing, Does.Not.Contain("stop_following").And.Contain("follow_player"));
        }

        [Test]
        public async Task TheStateTheTargetsAndThePromptsObservationsReachTheModelInOrder()
        {
            var seen = Prompt("target_001", "Keep an eye on that animal.", "attack_target", "wolf", false, observations: "[\"A wolf is near the sheep\"]");
            var provider = new FakeProvider(Reply("attack_target", "wolf"));
            var armA = new DecideOptions();
            armA.Observations.Add("Arm line.");

            var report = await Run(new[] { seen }, null, provider, provider, armA);

            string observations = Request(provider, "Keep an eye on that animal.").Observations;
            int arm = observations.IndexOf("- Arm line.", StringComparison.Ordinal);
            int state = observations.IndexOf("- isFollowing: false.", StringComparison.Ordinal);
            int target = observations.IndexOf("- bridge: The wooden bridge.", StringComparison.Ordinal);
            int wolf = observations.IndexOf("- wolf: A wolf.", StringComparison.Ordinal);
            int own = observations.IndexOf("- A wolf is near the sheep", StringComparison.Ordinal);
            Assert.That(new[] { arm, state, target, wolf, own }, Is.All.GreaterThanOrEqualTo(0), observations);
            Assert.That(new[] { arm, state, target, wolf, own }, Is.Ordered, observations);
            Assert.That(report.Answers[0].StateLines, Is.EqualTo(new[] { "isFollowing: false." }));
        }

        [Test]
        public async Task TheDefaultPlacementSendsTheBlockTheRunnerSentBefore()
        {
            // #151: the standing lines now come from an observation source; with the default
            // placement the block is the one written when every line was the call's own.
            var seen = Prompt("target_001", "Keep an eye on that animal.", "attack_target", "wolf", false, observations: "[\"A wolf is near the sheep\"]");
            var provider = new FakeProvider(Reply("attack_target", "wolf"));
            var armA = new DecideOptions();
            armA.Observations.Add("Arm line.");

            await Run(new[] { seen }, null, provider, provider, armA);

            string observations = Request(provider, "Keep an eye on that animal.").Observations;
            Assert.That(observations, Is.EqualTo(string.Join("\n",
                "What you know right now:",
                "- Arm line.",
                "- isFollowing: false.",
                "- bridge: The wooden bridge.",
                "- docks: The docks.",
                "- wolf: A wolf.",
                "- A wolf is near the sheep")));
            Assert.That(Request(provider, "Keep an eye on that animal.").Stimulus, Is.EqualTo("Player: \"Keep an eye on that animal.\""));
        }

        [Test]
        public async Task AnArmsPlacementMovesThePromptsOwnLinesAlone()
        {
            var seen = Prompt("target_001", "Keep an eye on that animal.", "attack_target", "wolf", false, observations: "[\"A wolf is near the sheep\"]");
            var a = new FakeProvider(Reply("attack_target", "wolf"));
            var b = new FakeProvider(Reply("attack_target", "wolf"));
            var after = new DecideOptions();
            after.Observations.Add("Arm line.");
            var with = new DecideOptions { ObservationPlacement = ObservationPlacement.WithStimulus };

            var report = await Fixtures.NotCancelled(Runner(new[] { seen }, null, a, b, after, with).RunAsync());

            var inA = Request(a, "Keep an eye on that animal.");
            Assert.That(inA.Observations, Does.EndWith("- wolf: A wolf.\n- A wolf is near the sheep"),
                "The prompt's line closes the block, after the arm's, the state's and the targets'.");
            Assert.That(inA.Stimulus, Is.EqualTo("Player: \"Keep an eye on that animal.\""));
            var inB = Request(b, "Keep an eye on that animal.");
            Assert.That(inB.Stimulus, Is.EqualTo("Player: \"Keep an eye on that animal.\" A wolf is near the sheep."));
            Assert.That(inB.Observations, Does.Not.Contain("A wolf is near the sheep"));
            Assert.That(inB.Observations, Does.Contain("- isFollowing: false."), "The standing lines stay in the block.");
            Assert.That(report.Conditions.Arms.Select(c => c.ObservationPlacement), Is.EqualTo(new[] { "AfterSources", "WithStimulus" }));
        }

        [Test]
        public async Task AnArmCanSendItsStandingLinesWithTheCallSoThePlacementMovesThemAll()
        {
            // #151: how a game that passes every fact with its call is sent. With AfterSources the
            // request is the same as through the source; with WithStimulus the whole block moves.
            var seen = Prompt("target_001", "Keep an eye on that animal.", "attack_target", "wolf", false, observations: "[\"A wolf is near the sheep\"]");
            var viaSource = new FakeProvider(Reply("attack_target", "wolf"));
            var withCall = new FakeProvider(Reply("attack_target", "wolf"));
            var moved = new FakeProvider(Reply("attack_target", "wolf"));
            var none = new FakeProvider(Reply("attack_target", "wolf"));

            await Fixtures.NotCancelled(Runner(new[] { seen }, null, viaSource, none).RunAsync());
            var runner = Runner(new[] { seen }, null, withCall, moved, null,
                new DecideOptions { ObservationPlacement = ObservationPlacement.WithStimulus });
            runner.ArmA.StandingLinesAsCallObservations = true;
            runner.ArmB.StandingLinesAsCallObservations = true;
            var report = await Fixtures.NotCancelled(runner.RunAsync());

            var source = Request(viaSource, "Keep an eye on that animal.");
            var call = Request(withCall, "Keep an eye on that animal.");
            Assert.That(call.Observations, Is.EqualTo(source.Observations));
            Assert.That(call.Stimulus, Is.EqualTo(source.Stimulus));

            var all = Request(moved, "Keep an eye on that animal.");
            Assert.That(all.Observations, Is.Null, "Every line was the call's, so the block is empty and left out.");
            Assert.That(all.Stimulus, Does.StartWith("Player: \"Keep an eye on that animal.\" isFollowing: false. bridge: The wooden bridge."));
            Assert.That(all.Stimulus, Does.EndWith(" wolf: A wolf. A wolf is near the sheep."));
            Assert.That(report.Conditions.Arms.Select(c => c.StandingLinesAsCallObservations), Is.EqualTo(new[] { true, true }));
        }

        [Test]
        public async Task TheSetsTargetsAreOfferedThroughATargetSource()
        {
            var provider = new FakeProvider(Reply("move_to", "bridge"));

            await Run(new[] { Bridge }, null, provider);

            Assert.That(Request(provider, "Meet me over at the bridge.").Schema.Fields[1].AllowedValues,
                Is.EquivalentTo(new[] { "no_target", "bridge", "docks", "wolf" }));
        }

        [Test]
        public async Task ADescribeStateOfTheCallersOwnWordsTheState()
        {
            var provider = new FakeProvider(Reply("move_to", "bridge"));
            var runner = Runner(new[] { Bridge }, null, provider, provider);
            runner.DescribeState = s => new[] { (bool)s["isFollowing"] ? "You are following the player." : "You are not following the player." };

            var report = await Fixtures.NotCancelled(runner.RunAsync());

            Assert.That(Request(provider, "Meet me over at the bridge.").Observations, Does.Contain("- You are not following the player."));
            Assert.That(Request(provider, "Meet me over at the bridge.").Observations, Does.Not.Contain("isFollowing"));
            Assert.That(report.Answers[0].StateLines, Is.EqualTo(new[] { "You are not following the player." }));
            Assert.That(report.Conditions.State, Does.Contain("of the caller's own"));
        }

        [TestCase(true, true, true)]
        [TestCase(true, false, false)]
        [TestCase(false, true, false)]
        public async Task TheTargetIsCheckedOnlyWhenTheArmAndThePromptBothCheckIt(bool arm, bool prompt, bool expected)
        {
            var bridge = Prompt("target_001", "Meet me over at the bridge.", "move_to", "bridge", false,
                extra: prompt ? "" : ", \"checkTarget\": false");
            var provider = new FakeProvider(Reply("move_to", "bridge"));

            var report = await Run(new[] { bridge }, null, provider, provider, new DecideOptions { CheckTarget = arm });

            var answer = report.Answers[0];
            Assert.That(answer.CheckTarget, Is.EqualTo(expected));
            Assert.That(answer.Result.Telemetry.TargetGroundingStatus == GroundingStatus.Off, Is.EqualTo(!expected));
        }

        [Test]
        public async Task EachPromptGetsAFreshAgentWithNoMemory()
        {
            var provider = new FakeProvider(Reply("move_to", "bridge"));

            await Run(new[] { Bridge, Escort }, null, provider);

            Assert.That(provider.Requests.Select(r => r.History.Count), Is.All.EqualTo(0));
        }

        // ---- Order and warm-up -------------------------------------------------------------------

        [Test]
        public async Task TheArmsInterleaveAfterEachArmsWarmUp()
        {
            var provider = new FakeProvider(Reply("none", "no_target"));
            var a = new DecideOptions();
            a.Observations.Add("Arm A asks.");
            var b = new DecideOptions();
            b.Observations.Add("Arm B asks.");
            var runner = Runner(new[] { Bridge, Escort }, null, provider, provider, a, b);

            var report = await Fixtures.NotCancelled(runner.RunAsync());

            var sent = provider.Requests.Select(r => (r.Observations?.Contains("Arm A asks.") == true ? "A" : "B") + " " + r.Stimulus).ToList();
            string[] warmUps = { "Good evening.", "Is the road quiet tonight?", "Stay sharp." };
            Assert.That(sent, Is.EqualTo(
                warmUps.Select(w => "A Player: \"" + w + "\"")
                    .Concat(warmUps.Select(w => "B Player: \"" + w + "\""))
                    .Concat(new[]
                    {
                        "A Player: \"Meet me over at the bridge.\"", "B Player: \"Meet me over at the bridge.\"",
                        "A Player: \"Walk with me a while.\"", "B Player: \"Walk with me a while.\"",
                    })));
            Assert.That(report.Answers.Select(x => x.Arm + " " + x.PromptId),
                Is.EqualTo(new[] { "A target_001", "B target_001", "A target_002", "B target_002" }));
        }

        [Test]
        public async Task SequentialArmsRunOneAfterTheOther()
        {
            var provider = new FakeProvider(Reply("none", "no_target"));
            var a = new DecideOptions();
            a.Observations.Add("Arm A asks.");
            var runner = Runner(new[] { Bridge, Escort }, null, provider, provider, a);
            runner.Order = EvalArmOrder.Sequential;
            runner.WarmUpRequests = 2;

            var report = await Fixtures.NotCancelled(runner.RunAsync());

            var arms = provider.Requests.Select(r => r.Observations?.Contains("Arm A asks.") == true ? "A" : "B");
            Assert.That(string.Concat(arms), Is.EqualTo("AAAABBBB"));
            Assert.That(report.Conditions.Order, Is.EqualTo(EvalArmOrder.Sequential));
        }

        [Test]
        public async Task TheWarmUpNeverReachesTheReport()
        {
            var provider = new FakeProvider(Reply("move_to", "bridge"));

            var report = await Run(new[] { Bridge, Escort }, new[] { Quiet }, provider);

            Assert.That(provider.Requests, Has.Count.EqualTo(2 * 3 + 2 * 3), "three warm-ups and three prompts per arm");
            Assert.That(report.Answers, Has.Count.EqualTo(6));
            Assert.That(report.Arms.Select(a => a.Overall.Total), Is.All.EqualTo(3));
            Assert.That(report.Answers.Select(a => a.Stimulus), Has.None.Contains("Good evening").And.None.Contains("Stay sharp"));
            Assert.That(report.Conditions.Arms.Select(a => a.WarmUpRequests), Is.All.EqualTo(3));
        }

        [Test]
        public async Task TheWarmUpIsShapedLikeAPromptWithoutTheState()
        {
            var provider = new FakeProvider(Reply("move_to", "bridge"));
            var a = new DecideOptions();
            a.Observations.Add("Arm A asks.");
            var runner = Runner(new[] { Bridge }, null, provider, provider, a);
            runner.DescribeState = s => new[] { (bool)s["isFollowing"] ? "Following." : "Not following." };

            await Fixtures.NotCancelled(runner.RunAsync());

            string warmUp = Request(provider, "Good evening.").Observations;
            Assert.That(warmUp, Does.Contain("- Arm A asks.").And.Contain("- bridge: The wooden bridge."));
            Assert.That(warmUp, Does.Not.Contain("following"), "A DescribeState reading a value no warm-up has would throw.");
        }

        [Test]
        public async Task AWarmUpStimulusThatIsAlsoAPromptIsNotSent()
        {
            var evening = Prompt("situation_001", "Good evening.", "none", "no_target", false, "Situation");
            var provider = new FakeProvider(Reply("none", "no_target"));

            await Run(null, new[] { evening }, provider);

            Assert.That(provider.Requests.Count(r => r.Stimulus.Contains("Good evening.")), Is.EqualTo(2), "once per arm, as the prompt");
        }

        [Test]
        public async Task AWarmUpWithNoDecisionStopsTheRun()
        {
            var provider = new FakeProvider("this is not JSON");
            var runner = Runner(new[] { Bridge }, null, provider, provider);

            var e = await Refused(runner);

            Assert.That(e.Message, Does.Contain("warm-up"));
            Assert.That(provider.Requests, Has.Count.EqualTo(3), "only arm A's warm-up was sent");
        }

        // ---- Refusing to start -------------------------------------------------------------------

        [Test]
        public async Task ASetThatBreaksARuleIsRefusedBeforeAnythingIsSent()
        {
            var unregistered = Prompt("target_001", "Meet me over at the tower.", "move_to", "tower", false);
            var provider = new FakeProvider(Reply("move_to", "bridge"));
            var runner = Runner(new[] { unregistered }, null, provider, provider);

            var e = await Refused(runner);

            Assert.That(e.Message, Does.Contain("target_001").And.Contain("tower"));
            Assert.That(provider.Requests, Is.Empty);
        }

        [Test]
        public async Task ASetThatDisagreesWithTheProfileIsRefused()
        {
            var set = fixtures.Set(new[] { Bridge });
            set.Actions.RemoveAll(a => a.Id == "attack_target");
            var provider = new FakeProvider(Reply("move_to", "bridge"));
            var runner = new EvalRunner(set, Fixtures.VillageGuardProfile(),
                new EvalArm("A", fixtures.Config(provider, "Fake")), new EvalArm("B", fixtures.Config(provider, "Fake")));

            var e = await Refused(runner);

            Assert.That(e.Message, Does.Contain("attack_target"));
            Assert.That(provider.Requests, Is.Empty);
        }

        [Test]
        public async Task AnArmCanListTheActionsAndTargetsAndTheReportSaysSo()
        {
            // #151's A/B for #190: the same set, one arm with the list and without counter-examples.
            var a = new FakeProvider(Reply("move_to", "bridge"));
            var b = new FakeProvider(Reply("move_to", "bridge"));
            var runner = Runner(new[] { Bridge }, null, a, b,
                optionsB: new DecideOptions { ListActionsAndTargets = true, MaxListedTargets = 3, CounterExamples = false });

            var report = await Fixtures.NotCancelled(runner.RunAsync());

            var armA = report.Conditions.Arms[0];
            var armB = report.Conditions.Arms[1];
            Assert.That((armA.ListActionsAndTargets, armA.MaxListedTargets, armA.CounterExamples), Is.EqualTo((false, 12, true)));
            Assert.That((armB.ListActionsAndTargets, armB.MaxListedTargets, armB.CounterExamples), Is.EqualTo((true, 3, false)));
            Assert.That(a.Requests.Select(r => r.SystemPrompt), Has.None.Contains("Allowed actions:"));
            Assert.That(b.Requests.Select(r => r.SystemPrompt), Has.All.Contains("Allowed actions:"),
                "Every call of the arm, warm-ups included, starts from its options.");
        }

        [Test]
        public async Task AnArmsKnowledgeReachesOnlyItsOwnSystemPromptAndLeavesTheProfileAlone()
        {
            // #190, Living Vale #14's A/B: the same lines as knowledge in one arm and as
            // observations in the other.
            var a = new FakeProvider(Reply("move_to", "bridge"));
            var b = new FakeProvider(Reply("move_to", "bridge"));
            var line = "When trouble is reported at a place you can reach, go there.";
            var set = fixtures.Set(new[] { Bridge }, null);
            var profile = Fixtures.VillageGuardProfile();
            var armA = new EvalArm("as-observations", fixtures.Config(a, "FakeA"), new DecideOptions { Observations = new[] { line } });
            var armB = new EvalArm("as-knowledge", fixtures.Config(b, "FakeB")) { Knowledge = new List<string> { line } };
            var runner = new EvalRunner(set, profile, armA, armB);

            var report = await Fixtures.NotCancelled(runner.RunAsync());

            Assert.That(a.Requests.Select(r => r.SystemPrompt), Has.None.Contains(line));
            Assert.That(a.Requests.Select(r => r.Observations), Has.All.Contains("- " + line));
            Assert.That(b.Requests.Select(r => r.SystemPrompt), Has.All.Contains("What you know:\n- " + line),
                "Every call of the arm, warm-ups included.");
            Assert.That(b.Requests.Select(r => r.Observations ?? ""), Has.None.Contains(line));
            Assert.That(report.Conditions.Arms[0].Knowledge, Is.Empty);
            Assert.That(report.Conditions.Arms[1].Knowledge, Is.EqualTo(new[] { line }));
            Assert.That(profile.Identity.Knowledge, Is.Empty, "The shared profile is never changed.");
        }

        [Test]
        public void AnArmsKnowledgeIsCopiedWhenSet()
        {
            var lines = new List<string> { "The well is dry." };
            var arm = new EvalArm("A", fixtures.Config(new FakeProvider(Reply("none", "no_target")), "FakeA")) { Knowledge = lines };
            lines.Add("Added later.");

            Assert.That(arm.Knowledge, Is.EqualTo(new[] { "The well is dry." }));
            Assert.That(new EvalArm("B", fixtures.Config(new FakeProvider(Reply("none", "no_target")), "FakeB")) { Knowledge = null }.Knowledge, Is.Empty);
        }

        [Test]
        public async Task AnArmsIdleExampleThatIsAPromptIsRefused()
        {
            // The arm's idle example replaces the profile's, so the model would be shown "The
            // square is empty." -> none and then scored on it (hard rule 4).
            var provider = new FakeProvider(Reply("none", "no_target"));
            var runner = Runner(new[] { Bridge }, new[] { Quiet }, provider, provider,
                optionsB: new DecideOptions { IdleExampleStimulus = "the square is empty!" });

            var e = await Refused(runner);

            Assert.That(e.Message, Does.Contain("situation_001").And.Contain("arm 'B'").And.Contain("hard rule 4"));
            Assert.That(provider.Requests, Is.Empty);
        }

        [Test]
        public async Task AnArmsIdleStatementThatIsAPromptIsRefused()
        {
            // A statement is shown in the prompt as an example's is (#190, G14), so the same rule.
            var provider = new FakeProvider(Reply("none", "no_target"));
            var runner = Runner(new[] { Bridge }, new[] { Quiet }, provider, provider,
                optionsB: new DecideOptions { IdleExampleStimulus = "Nothing new.", IdleExampleStatement = "The square is empty." });

            var e = await Refused(runner);

            Assert.That(e.Message, Does.Contain("situation_001").And.Contain("arm 'B'").And.Contain("statement").And.Contain("hard rule 4"));
            Assert.That(provider.Requests, Is.Empty);
        }

        [Test]
        public async Task TheReportSaysWhichArmShowedStatementsAndTheFallback()
        {
            var a = new FakeProvider(Reply("move_to", "bridge"));
            var b = new FakeProvider(Reply("move_to", "bridge"));
            var runner = Runner(new[] { Bridge }, null, a, b,
                optionsB: new DecideOptions { IdleExampleStatement = "Quiet as ever.", ExampleStatements = false, ExampleTargetFallback = true });

            var report = await Fixtures.NotCancelled(runner.RunAsync());

            var armA = report.Conditions.Arms[0];
            var armB = report.Conditions.Arms[1];
            Assert.That((armA.IdleExampleStatement, armA.ExampleStatements, armA.ExampleTargetFallback),
                Is.EqualTo(((string)null, true, false)));
            Assert.That((armB.IdleExampleStatement, armB.ExampleStatements, armB.ExampleTargetFallback),
                Is.EqualTo(("Quiet as ever.", false, true)));
        }

        // ---- An arm's own legality (#151) ---------------------------------------------------------

        private static EvalLegality PlacesAndCreatures()
        {
            var legality = new EvalLegality { Name = "places-and-creatures" };
            legality.TargetCategories["bridge"] = "place";
            legality.TargetCategories["docks"] = "place";
            legality.TargetCategories["wolf"] = "creature";
            legality.ActionCategories["move_to"] = new[] { "place" };
            legality.ActionCategories["attack_target"] = new[] { "creature" };
            // Their target is the player, who is not a registered target.
            legality.ActionCategories["follow_player"] = new[] { "player" };
            legality.ActionCategories["stop_following"] = new[] { "player" };
            legality.ExtraTargets.Add(new EvalExtraTarget { Id = "trader", Description = "A travelling trader", Category = "person" });
            return legality;
        }

        [Test]
        public async Task AnArmsLegalityReachesOnlyItsOwnAgentsAndLeavesTheSetAndProfileAlone()
        {
            var a = new FakeProvider(Reply("attack_target", "bridge"));
            var b = new FakeProvider(Reply("attack_target", "bridge"));
            var set = fixtures.Set(new[] { Bridge });
            var profile = Fixtures.VillageGuardProfile();
            var options = new DecideOptions { LegalTargets = LegalTargetListing.PerTarget };
            var armA = new EvalArm("plain", fixtures.Config(a, "FakeA"), options);
            var armB = new EvalArm("legal", fixtures.Config(b, "FakeB"), options) { Legality = PlacesAndCreatures() };

            var report = await Fixtures.NotCancelled(new EvalRunner(set, profile, armA, armB).RunAsync());

            Assert.That(Request(a, "Meet me over at the bridge.").SystemPrompt, Does.Match(@"\n- bridge \(for [^)]*attack_target"),
                "Plain, the set's targets declare no categories, so an attack may take the bridge.");
            var legal = Request(b, "Meet me over at the bridge.");
            Assert.That(legal.SystemPrompt, Does.Contain("\n- wolf (for attack_target)"));
            Assert.That(legal.SystemPrompt, Does.Contain("\n- bridge (for move_to)"));
            Assert.That(legal.SystemPrompt, Does.Not.Contain("- trader"), "Nothing may take the trader, so the list leaves it out.");
            Assert.That(legal.Observations, Does.Contain("- trader: A travelling trader."), "It is still told as a standing line, as the set's targets are.");

            var answers = report.Answers.Where(x => x.PromptId == "target_001").ToList();
            Assert.That(answers.Single(x => x.Arm == "plain").Outcome, Is.EqualTo(DecisionOutcome.WrongLegalAction));
            var refused = answers.Single(x => x.Arm == "legal");
            Assert.That((refused.ModelAction, refused.ModelTarget), Is.EqualTo(("attack_target", "bridge")), "What the model chose is kept.");
            Assert.That(refused.Guard, Is.EqualTo("TargetCategoryGuard"), "And the category guard refused it.");
            Assert.That(report.Conditions.Arms[0].Legality, Is.Null);
            Assert.That(report.Conditions.Arms[1].Legality.Name, Is.EqualTo("places-and-creatures"));
            Assert.That(report.Conditions.Arms[1].LegalTargets, Is.EqualTo("PerTarget"));
            Assert.That(profile.Actions.Select(x => x.Action.TargetCategories), Has.All.Null.Or.Empty, "The shared profile is never changed.");
        }

        [Test]
        public async Task AnArmsGroundingThresholdIsRecordedAndNamedInTheReport()
        {
            var set = fixtures.Set(new[] { Bridge });
            var armA = new EvalArm("control", fixtures.Config(new FakeProvider(Reply("move_to", "bridge")), "FakeA"));
            var armB = new EvalArm("lower", fixtures.Config(new FakeProvider(Reply("move_to", "bridge")), "FakeB"),
                new DecideOptions { TargetGroundingThreshold = 0.6f });

            var report = await Fixtures.NotCancelled(new EvalRunner(set, Fixtures.VillageGuardProfile(), armA, armB).RunAsync());

            Assert.That(report.Conditions.Arms.Select(x => x.TargetGroundingThreshold), Is.EqualTo(new float?[] { 0.8f, 0.6f }));
            Assert.That(report.Conditions.Arms.Select(x => x.TokenAlternatives), Has.All.Null, "A provider other than Ollama asks for none.");
            string markdown = report.ToMarkdown();
            Assert.That(markdown, Does.Contain("| on, threshold 0.6 |"));
            Assert.That(markdown, Does.Contain("| on |"), "A default arm's cell reads as it did before.");
        }

        [Test]
        public async Task AnArmsLegalityThatMakesALabelIllegalIsRefused()
        {
            var legality = PlacesAndCreatures();
            legality.TargetCategories["bridge"] = "creature";
            var provider = new FakeProvider(Reply("move_to", "bridge"));
            var set = fixtures.Set(new[] { Bridge });
            var runner = new EvalRunner(set, Fixtures.VillageGuardProfile(),
                new EvalArm("A", fixtures.Config(provider, "FakeA")), new EvalArm("B", fixtures.Config(provider, "FakeB")) { Legality = legality });

            var e = await Refused(runner);

            Assert.That(e.Message, Does.Contain("target_001").And.Contain("move_to/bridge").And.Contain("illegal"));
            Assert.That(provider.Requests, Is.Empty);
        }

        [Test]
        public async Task AnArmsLegalityIsCheckedAgainstTheProfilesOwnCategoriesWhereItGivesNone()
        {
            // The profile restricts move_to to places, and the arm gives move_to no categories of its
            // own, so the run would keep the profile's: the bridge as a creature makes the label illegal.
            var profile = Fixtures.VillageGuardProfile();
            profile.Actions.Single(a => a.Action.Id == "move_to").Action.TargetCategories = new[] { "place" };
            var legality = new EvalLegality { Name = "bridge-as-creature" };
            legality.TargetCategories["bridge"] = "creature";
            var provider = new FakeProvider(Reply("move_to", "bridge"));
            var runner = new EvalRunner(fixtures.Set(new[] { Bridge }), profile,
                new EvalArm("A", fixtures.Config(provider, "FakeA")), new EvalArm("B", fixtures.Config(provider, "FakeB")) { Legality = legality });

            var e = await Refused(runner);

            Assert.That(e.Message, Does.Contain("target_001").And.Contain("move_to/bridge").And.Contain("illegal"));
            Assert.That(provider.Requests, Is.Empty);
        }

        [Test]
        public async Task AnArmsCopyOfAnActionKeepsThatItTakesNoTarget()
        {
            // The arm gives follow_player an empty category list, so the run registers its own copy
            // of the profile's action; that copy must still drop a target the model names (#244 item 2).
            var profile = Fixtures.VillageGuardProfile();
            profile.Actions.Single(a => a.Action.Id == "follow_player").Action.TakesNoTarget = true;
            var legality = new EvalLegality { Name = "empty-categories" };
            legality.ActionCategories["follow_player"] = new string[0];
            var provider = new FakeProvider(Reply("follow_player", "bridge"));
            var runner = new EvalRunner(fixtures.Set(new[] { Escort }), profile,
                new EvalArm("A", fixtures.Config(provider, "FakeA")), new EvalArm("B", fixtures.Config(provider, "FakeB")) { Legality = legality });

            var report = await Fixtures.NotCancelled(runner.RunAsync());

            Assert.That(report.Answers.Select(a => a.Arm).Distinct(), Is.EquivalentTo(new[] { "A", "B" }));
            Assert.That(report.Answers.Select(a => a.ModelTarget), Is.All.EqualTo("bridge"));
            Assert.That(report.Answers.Select(a => a.Target), Is.All.EqualTo("no_target"));
            Assert.That(report.Answers.Select(a => a.Outcome), Is.All.EqualTo(DecisionOutcome.Correct));
        }

        [Test]
        public async Task AnArmsExtraTargetMayNotBeTheSetsOwn()
        {
            var legality = PlacesAndCreatures();
            legality.ExtraTargets.Add(new EvalExtraTarget { Id = "wolf", Description = "Another wolf", Category = "creature" });
            var provider = new FakeProvider(Reply("move_to", "bridge"));
            var runner = new EvalRunner(fixtures.Set(new[] { Bridge }), Fixtures.VillageGuardProfile(),
                new EvalArm("A", fixtures.Config(provider, "FakeA")) { Legality = legality }, new EvalArm("B", fixtures.Config(provider, "FakeB")));

            var e = await Refused(runner);

            Assert.That(e.Message, Does.Contain("'wolf'").And.Contain("the set's own"));
        }

        [Test]
        public void TwoArmsCannotShareAName()
        {
            var config = fixtures.Config(new FakeProvider(), "Fake");

            Assert.Throws<ArgumentException>(() =>
                new EvalRunner(fixtures.Set(new[] { Bridge }), Fixtures.VillageGuardProfile(), new EvalArm("same", config), new EvalArm("Same", config)));
        }

        // ---- Helpers -----------------------------------------------------------------------------

        private Task<EvalReport> Run(string[] commands, string[] situations, string reply, int warmUp = 3)
        {
            var provider = new FakeProvider(reply);
            var runner = Runner(commands, situations, provider, provider);
            runner.WarmUpRequests = warmUp;
            return Fixtures.NotCancelled(runner.RunAsync());
        }

        private Task<EvalReport> Run(string[] commands, string[] situations, FakeProvider provider)
        {
            return Fixtures.NotCancelled(Runner(commands, situations, provider, provider).RunAsync());
        }

        private Task<EvalReport> Run(string[] commands, string[] situations, ILLMProvider a, ILLMProvider b, DecideOptions optionsA)
        {
            return Fixtures.NotCancelled(Runner(commands, situations, a, b, optionsA).RunAsync());
        }

        private EvalRunner Runner(string[] commands, string[] situations, ILLMProvider a, ILLMProvider b,
                                  DecideOptions optionsA = null, DecideOptions optionsB = null)
        {
            var set = fixtures.Set(commands ?? new string[0], situations);
            return new EvalRunner(set, Fixtures.VillageGuardProfile(),
                new EvalArm("A", fixtures.Config(a, "FakeA"), optionsA),
                new EvalArm("B", fixtures.Config(b, "FakeB"), optionsB));
        }

        /// <summary>
        /// Awaits a run that must refuse to start or go on, through <see cref="Fixtures.NotCancelled{T}"/>,
        /// and returns why. Fails on a run that finishes or ends cancelled.
        /// </summary>
        internal static async Task<InvalidOperationException> Refused(EvalRunner runner)
        {
            try
            {
                await Fixtures.NotCancelled(runner.RunAsync());
            }
            catch (InvalidOperationException e)
            {
                return e;
            }

            Assert.Fail("The run finished instead of refusing.");
            return null;
        }

        private static DecisionRequest Request(FakeProvider provider, string stimulus)
        {
            var found = provider.Requests.Where(r => r.Stimulus.Contains("\"" + stimulus + "\"")).ToList();
            Assert.That(found, Is.Not.Empty, $"No request was sent for \"{stimulus}\".");
            return found[0];
        }
    }
}

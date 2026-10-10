using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Agenerela.Providers;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Agenerela.Tests
{
    // The whole pipeline, against FakeProvider: what reaches the provider, what comes back, and
    // what Execute lets through. The async tests are [Test] methods returning Task, which the Unity
    // Test Framework awaits, failing the test on an exception; FakeProvider answers within the call
    // unless told to hold. One trap: the framework counts a Task that ends cancelled as passed, so
    // every await of DecideAsync goes through Fixtures.NotCancelled, and the tests about
    // cancellation catch the OperationCanceledException themselves and fail or pass explicitly.
    public sealed class AgentTests
    {
        private const string GoToTheTower = "{\"action\":\"move_to\",\"target\":\"tower\",\"statement\":\"On my way.\"}";

        [TearDown]
        public void DestroyFixtures()
        {
            Fixtures.DestroyCreated();
        }

        // ---- The pipeline, end to end -------------------------------------------------------

        [Test]
        public async Task FollowingAgentRequestOmitsFollowPlayerAllTheWayToTheProvider()
        {
            var fake = new FakeProvider(reply: "{\"action\":\"stop_following\",\"target\":\"no_target\",\"statement\":\"As you wish.\"}");
            var agent = new Agent(Fixtures.VillageGuardProfile(), fake);
            agent.Bind("follow_player",  new FakeHandler(available: ctx => !(bool)ctx.State["isFollowing"]));
            agent.Bind("stop_following", new FakeHandler(available: ctx =>  (bool)ctx.State["isFollowing"]));
            agent.Bind("move_to",        new FakeHandler());
            agent.Targets.Register("tower", new object());
            agent.State["isFollowing"] = true;

            var result = await Fixtures.NotCancelled(agent.DecideAsync("Wait here."));

            var actionField = fake.LastRequest.Schema.Fields[0];
            Assert.That(actionField.Name, Is.EqualTo(DecisionSchema.ActionFieldName));
            Assert.That(actionField.AllowedValues, Does.Not.Contain("follow_player"),
                "State masking must survive the whole pipeline, not only the unit test in #7.");
            Assert.That(fake.LastRequest.FewShotBlock, Does.Not.Contain("follow_player"),
                "Nor may an example demonstrate the masked action: the examples come from the same available set.");
            Assert.That(result.Decision.ActionId, Is.EqualTo("stop_following"));
            Assert.That(result.Telemetry.ProviderName, Is.EqualTo(fake.Name));
        }

        [Test]
        public async Task AReplyBecomesADecisionWithItsTelemetry()
        {
            var fake = new FakeProvider(GoToTheTower) { PromptTokens = 143, CompletionTokens = 21, DelayMilliseconds = 20 };
            var agent = Fixtures.VillageGuard(fake);

            var result = await Fixtures.NotCancelled(agent.DecideAsync("Go to the tower."));

            Assert.That(result.Decision.ActionId, Is.EqualTo("move_to"));
            Assert.That(result.Decision.TargetId, Is.EqualTo("tower"));
            Assert.That(result.Decision.Statement, Is.EqualTo("On my way."));
            Assert.That(result.Telemetry.ProviderName, Is.EqualTo("fake"));
            Assert.That(result.Telemetry.FrameworkVersion, Is.EqualTo(AgenerelaInfo.Version));
            Assert.That(result.Telemetry.LatencySeconds, Is.GreaterThanOrEqualTo(0.015f).And.LessThan(5f),
                "Latency is measured around the provider's request, which took 20 ms.");
            Assert.That(result.Telemetry.PromptTokens, Is.EqualTo(143));
            Assert.That(result.Telemetry.CompletionTokens, Is.EqualTo(21));
            Assert.That(result.Telemetry.Outcome, Is.Null,
                "Only a pipeline error is classified when deciding; whether the answer was right is the eval harness's call.");
            Assert.That(fake.Requests, Has.Count.EqualTo(1));
        }

        [Test]
        public async Task TheRequestIsTheGoldenVillageGuardRequest()
        {
            var fake = new FakeProvider(GoToTheTower);
            var agent = Fixtures.VillageGuard(fake);

            await Fixtures.NotCancelled(agent.DecideAsync("Go to the tower."));
            var request = fake.LastRequest;

            // #16's golden text, now produced by the agent rather than by calling the builders by hand.
            Assert.That(request.SystemPrompt, Is.EqualTo(string.Join("\n",
                "You are Village Guard, a guard at the town gate. Loyal, brief, a little suspicious of strangers.",
                "Your goals: keep the gate safe; help the player when asked.",
                "Choose exactly one action from the allowed list. Use no_target when none of the listed targets applies.")));
            Assert.That(request.FewShotBlock, Is.EqualTo(string.Join("\n",
                "Examples of correct decisions:",
                "Player: \"Accompany me on my rounds.\" -> action: follow_player, target: no_target",
                "Player: \"Proceed to the Tower.\" -> action: move_to, target: tower",
                "Player: \"Engage the TrainingDummy.\" -> action: attack_target, target: training_dummy",
                "Player: \"You seem well rested today.\" -> action: none, target: no_target",
                "Player: \"Proceed to the Atlantis.\" -> action: none, target: no_target (not an available target)")));
            Assert.That(request.Observations, Is.Null);
            Assert.That(request.History, Is.Not.Null.And.Empty, "No memory until Phase 2.");
            Assert.That(request.Stimulus, Is.EqualTo("Player: \"Go to the tower.\""));
            Assert.That(request.Schema.Fields.Select(f => f.Name), Is.EqualTo(new[] { "action", "target", "statement" }));
            Assert.That(request.Schema.Fields[0].AllowedValues,
                Is.EqualTo(new[] { "follow_player", "move_to", "attack_target", "none" }));
            Assert.That(request.Schema.Fields[1].AllowedValues, Is.EqualTo(new[] { "no_target", "tower", "training_dummy" }));
        }

        [Test]
        public async Task ALabelAndTwoObservationsReachTheRequestTheProviderReceived()
        {
            var fake = new FakeProvider(GoToTheTower);
            var agent = Fixtures.VillageGuard(fake);
            var options = new DecideOptions
            {
                StimulusLabel = "Captain",
                Observations = new List<string> { "It is night", "The gate is open" },
            };

            await Fixtures.NotCancelled(agent.DecideAsync("Go to the tower.", options));
            var request = fake.LastRequest;

            Assert.That(request.Stimulus, Is.EqualTo("Captain: \"Go to the tower.\""));
            Assert.That(request.FewShotBlock.Split('\n').Skip(1), Has.All.StartWith("Captain: \""),
                "The examples are labelled the way the question is.");
            Assert.That(request.FewShotBlock + request.Stimulus, Does.Not.Contain("Player:"));
            Assert.That(request.Observations, Is.EqualTo("What you know right now:\n- It is night\n- The gate is open"));

            var seen = Fixtures.Handler(agent, "move_to").AvailabilityChecks.Last();
            Assert.That(seen.Observations, Is.EqualTo(new[] { "It is night", "The gate is open" }),
                "The observations are copied into the AgentContext the handlers read (#2).");
            Assert.That(seen.Stimulus, Is.EqualTo("Go to the tower."));
        }

        [Test]
        public async Task TheIdleExampleIsTheProfilesUnlessTheCallSaysOtherwise()
        {
            var fake = new FakeProvider(GoToTheTower);
            var agent = Fixtures.VillageGuard(fake);
            const string Idle = "-> action: none, target: no_target";

            await Fixtures.NotCancelled(agent.DecideAsync("Go to the tower."));
            string fromProfile = fake.LastRequest.FewShotBlock;
            await Fixtures.NotCancelled(agent.DecideAsync("Go to the tower.", new DecideOptions { IdleExampleStimulus = "The bell has not rung." }));
            string overridden = fake.LastRequest.FewShotBlock;
            await Fixtures.NotCancelled(agent.DecideAsync("Go to the tower.", new DecideOptions { IdleExampleStimulus = "" }));
            string omitted = fake.LastRequest.FewShotBlock;

            Assert.That(fromProfile, Does.Contain("Player: \"You seem well rested today.\" " + Idle));
            Assert.That(overridden, Does.Contain("Player: \"The bell has not rung.\" " + Idle));
            Assert.That(overridden, Does.Not.Contain("well rested"));
            Assert.That(omitted, Does.Not.Contain("well rested"));
            Assert.That(omitted.Split('\n').Count(line => line.EndsWith(Idle)), Is.EqualTo(0),
                "An empty idle example leaves the example out of this call.");
        }

        [Test]
        public async Task TheEnumAndTheExamplesComeFromOneAvailabilityAnswer()
        {
            var fake = new FakeProvider(GoToTheTower);
            var agent = new Agent(Fixtures.VillageGuardProfile(), fake);

            // Not a pure function of the context, like a cooldown that counts calls: asked twice in
            // one decision, it would offer follow_player in one place and not the other.
            int asked = 0;
            agent.Bind("follow_player", new FakeHandler(available: ctx => asked++ % 2 == 0));
            agent.Bind("move_to", new FakeHandler());
            agent.Targets.Register("tower", new object());

            await Fixtures.NotCancelled(agent.DecideAsync("Go to the tower."));

            bool inEnum = fake.LastRequest.Schema.Fields[0].AllowedValues.Contains("follow_player");
            bool inExamples = fake.LastRequest.FewShotBlock.Contains("action: follow_player");
            Assert.That(inExamples, Is.EqualTo(inEnum),
                "The action enum and the few-shot examples must come from one availability answer.");
        }

        [Test]
        public async Task StateNeverReachesTheModel()
        {
            var fake = new FakeProvider(GoToTheTower);
            var agent = Fixtures.VillageGuard(fake);
            agent.State["vaultCombination"] = "swordfish-7731";

            await Fixtures.NotCancelled(agent.DecideAsync("Go to the tower.", new DecideOptions { Observations = { "It is night" } }));

            string everything = RequestText(fake.LastRequest);
            Assert.That(everything, Does.Not.Contain("swordfish-7731"));
            Assert.That(everything, Does.Not.Contain("vaultCombination"));
            Assert.That(everything, Does.Not.Contain("isFollowing"));
            Assert.That(everything, Does.Contain("It is night"), "Observations are what the model reads instead.");

            var seen = Fixtures.Handler(agent, "move_to").AvailabilityChecks.Last().State;
            agent.State["vaultCombination"] = "changed";
            Assert.That(seen["vaultCombination"], Is.EqualTo("swordfish-7731"),
                "State is for the handlers: a copy, taken when the decision was made, is in the context they read.");
        }

        [Test]
        public async Task ANullStimulusIsSentEmpty()
        {
            var fake = new FakeProvider(GoToTheTower);
            var agent = Fixtures.VillageGuard(fake);

            await Fixtures.NotCancelled(agent.DecideAsync(null));

            Assert.That(fake.LastRequest.Stimulus, Is.EqualTo("Player: \"\""));
        }

        [Test]
        public async Task ACodeOnlyCountryDecidesFromAReportWithNoSceneAndNoPlayer()
        {
            // DR-011's code front door and DR-008's open stimulus: no asset, no scene, no player.
            var fake = new FakeProvider("{\"action\":\"ration_grain\",\"target\":\"no_target\",\"statement\":\"\"}");
            var actions = new[]
            {
                new ActionDefinition { Id = "ration_grain", Description = "Cut the grain ration.", ExampleStimulus = "The granaries are half empty." },
                new ActionDefinition
                {
                    Id = "declare_war", Description = "Declare war on a neighbour.", RequiresTarget = true,
                    ExampleStimulus = "{0} massed troops on the border.", PreferredExampleTargets = new[] { "eastmarch" },
                },
            };
            var country = new Agent(new AgentIdentity { Name = "Northmark", Role = "a small kingdom", Goals = "survive the winter" }, actions, fake);
            var rationing = new FakeHandler();
            country.Bind("ration_grain", rationing);
            country.Bind("declare_war", new FakeHandler());
            country.TargetSources.Add(new ExplicitTargetSource().Add("eastmarch", new object()));

            var result = await Fixtures.NotCancelled(country.DecideAsync("Grain stores fell 12% this winter.",
                new DecideOptions { StimulusLabel = "Report", Observations = { "eastmarch has 3 armies" } }));

            var request = fake.LastRequest;
            Assert.That(request.Stimulus, Is.EqualTo("Report: \"Grain stores fell 12% this winter.\""));
            Assert.That(request.Schema.Fields[1].AllowedValues, Is.EqualTo(new[] { "no_target", "eastmarch" }));
            Assert.That(request.FewShotBlock.Split('\n'), Is.EqualTo(new[]
            {
                "Examples of correct decisions:",
                "Report: \"The granaries are half empty.\" -> action: ration_grain, target: no_target",
                "Report: \"Eastmarch massed troops on the border.\" -> action: declare_war, target: eastmarch",
                "Report: \"Atlantis massed troops on the border.\" -> action: none, target: no_target (not an available target)",
            }), "A code-only agent has no idle example unless a call passes one.");
            Assert.That(RequestText(request).ToLowerInvariant(), Does.Not.Contain("player"));

            Assert.That(country.Execute(result), Is.True);
            Assert.That(rationing.Executions, Has.Count.EqualTo(1));
        }

        // ---- What becomes a PipelineError, and what throws ----------------------------------

        [TestCase(null, "empty")]
        [TestCase("", "empty")]
        [TestCase("  \n ", "empty")]
        [TestCase("Sure, heading to the tower now.", "not one JSON object")]
        [TestCase("{\"action\":\"move_to\",\"target\":\"tower\"", "not one JSON object")]
        [TestCase("```json\n{\"action\":\"move_to\",\"target\":\"tower\",\"statement\":\"\"}\n```", "not one JSON object")]
        [TestCase("{\"action\":\"move_to\",\"target\":\"tower\"} {\"action\":\"none\",\"target\":\"no_target\"}", "not one JSON object|more after its JSON object")]
        [TestCase("{\"action\":\"move_to\",\"action\":\"attack_target\",\"target\":\"tower\"}", "not one JSON object")]
        [TestCase("[\"move_to\",\"tower\"]", "Array rather than an object")]
        [TestCase("\"move_to\"", "String rather than an object")]
        [TestCase("{}", "no 'action'")]
        [TestCase("{\"target\":\"tower\",\"statement\":\"On my way.\"}", "no 'action'")]
        [TestCase("{\"action\":null,\"target\":\"tower\"}", "'action' is not a string")]
        [TestCase("{\"action\":3,\"target\":\"tower\"}", "'action' is not a string")]
        [TestCase("{\"action\":\" \",\"target\":\"tower\"}", "'action' is empty")]
        [TestCase("{\"action\":\"move_to\",\"statement\":\"On my way.\"}", "no 'target'")]
        [TestCase("{\"action\":\"move_to\",\"target\":\"\"}", "'target' is empty")]
        [TestCase("{\"action\":\"move_to\",\"target\":{\"id\":\"tower\"}}", "'target' is not a string")]
        [TestCase("{\"action\":\"move_to\",\"target\":\"tower\",\"statement\":[\"On\",\"my\",\"way\"]}", "'statement' is not a string")]
        public async Task AReplyThatCannotBeReadIsAPipelineErrorNotAnException(string reply, string reason)
        {
            var fake = new FakeProvider(reply);
            var agent = Fixtures.VillageGuard(fake);
            LogAssert.Expect(LogType.Warning, new Regex("could not decide\\. .*(" + reason + ")"));

            var result = await Fixtures.NotCancelled(agent.DecideAsync("Go to the tower."));

            Assert.That(result.Decision, Is.Null);
            Assert.That(result.Telemetry.Outcome, Is.EqualTo(DecisionOutcome.PipelineError));
            Assert.That(result.Telemetry.ProviderName, Is.EqualTo("fake"));
            Assert.That(result.Telemetry.FrameworkVersion, Is.EqualTo(AgenerelaInfo.Version));
            Assert.That(agent.Execute(result), Is.False);
            Assert.That(Executions(agent), Is.Empty);
        }

        [TestCase("{\"action\":\"move_to\",\"target\":\"tower\"}", "")]
        [TestCase("{\"action\":\"move_to\",\"target\":\"tower\",\"statement\":null}", "")]
        [TestCase("  \n{\"statement\":\"On my way.\",\"target\":\"tower\",\"action\":\"move_to\"}\n ", "On my way.")]
        [TestCase("{\"action\":\"move_to\",\"target\":\"tower\",\"statement\":\"  On my way.  \",\"mood\":\"calm\"}", "  On my way.  ")]
        [TestCase("{\"action\":\"move_to\",\"target\":\"tower\",\"statement\":\"2026-10-10T08:00:00\"}", "2026-10-10T08:00:00")]
        public async Task AReplyIsReadByFieldNameAndItsStatementKeptAsWritten(string reply, string statement)
        {
            var fake = new FakeProvider(reply);
            var agent = Fixtures.VillageGuard(fake);

            var result = await Fixtures.NotCancelled(agent.DecideAsync("Go to the tower."));

            Assert.That(result.Decision.ActionId, Is.EqualTo("move_to"));
            Assert.That(result.Decision.TargetId, Is.EqualTo("tower"));
            Assert.That(result.Decision.Statement, Is.EqualTo(statement));
        }

        [TestCase("{\"action\":\"follow_player\",\"statement\":\"Right behind you.\"}")]
        [TestCase("{\"action\":\"follow_player\",\"target\":null,\"statement\":\"Right behind you.\"}")]
        public async Task WithNoTargetsTheReplyNeedsNoTargetAndTheDecisionHasNone(string reply)
        {
            var fake = new FakeProvider(reply);
            var agent = new Agent(Fixtures.VillageGuardProfile(), fake);
            agent.Bind("follow_player", new FakeHandler());
            agent.Bind("move_to", new FakeHandler());

            var result = await Fixtures.NotCancelled(agent.DecideAsync("Come along, then."));

            Assert.That(fake.LastRequest.Schema.Fields.Select(f => f.Name), Is.EqualTo(new[] { "action", "statement" }));
            Assert.That(fake.LastRequest.Schema.Fields[0].AllowedValues, Is.EqualTo(new[] { "follow_player", "none" }));
            Assert.That(result.Decision.TargetId, Is.EqualTo(TargetRegistry.NoTarget));
            Assert.That(agent.Execute(result), Is.True);
        }

        [Test]
        public async Task AnActionTheRequestDidNotOfferIsStillADecisionForExecuteToRefuse()
        {
            // An untrusted provider can answer outside the schema. That answer was read fine, it is
            // just wrong, so it is a decision rather than a pipeline error, and Execute stops it.
            var fake = new FakeProvider("{\"action\":\"follow_player\",\"target\":\"no_target\",\"statement\":\"Lead on.\"}");
            var agent = Fixtures.VillageGuard(fake, isFollowing: true);

            var result = await Fixtures.NotCancelled(agent.DecideAsync("Keep me company."));

            Assert.That(fake.LastRequest.Schema.Fields[0].AllowedValues, Does.Not.Contain("follow_player"));
            Assert.That(result.Decision.ActionId, Is.EqualTo("follow_player"));
            Assert.That(result.Telemetry.Outcome, Is.Null);

            LogAssert.Expect(LogType.Warning, new Regex("did not execute 'follow_player' on 'no_target': the action is not available right now"));
            Assert.That(agent.Execute(result), Is.False);
            Assert.That(Executions(agent), Is.Empty);
        }

        [TestCase(false)]
        [TestCase(true)]
        public async Task AProviderThatFailsGivesAPipelineErrorNotAnException(bool failsImmediately)
        {
            var fake = new FakeProvider(GoToTheTower)
            {
                Failure = new InvalidOperationException("model 'qwen3.5:2b' not found"),
                FailsImmediately = failsImmediately,
            };
            var agent = Fixtures.VillageGuard(fake);
            LogAssert.Expect(LogType.Warning,
                new Regex("could not decide\\. The provider failed\\. InvalidOperationException: model 'qwen3\\.5:2b' not found"));

            var result = await Fixtures.NotCancelled(agent.DecideAsync("Go to the tower."));

            Assert.That(result.Decision, Is.Null);
            Assert.That(result.Telemetry.Outcome, Is.EqualTo(DecisionOutcome.PipelineError));
            Assert.That(result.Telemetry.ProviderName, Is.EqualTo("fake"));
            Assert.That(fake.Requests, Has.Count.EqualTo(1));
        }

        [Test]
        public async Task AProviderTimeoutIsAPipelineErrorNotACancellation()
        {
            // A cancellation nobody asked for, such as a transport's own timeout, is a failure.
            var fake = new FakeProvider(GoToTheTower) { Failure = new OperationCanceledException("The request timed out.") };
            var agent = Fixtures.VillageGuard(fake);
            LogAssert.Expect(LogType.Warning, new Regex("could not decide\\. The provider failed\\."));

            // Caught here because the test framework counts an async test whose Task ends cancelled
            // as passed: a rethrown cancellation would otherwise skip the assertion below unseen.
            DecisionResult result;
            try
            {
                result = await agent.DecideAsync("Go to the tower.");
            }
            catch (OperationCanceledException)
            {
                Assert.Fail("The provider's own cancellation escaped as if the caller had cancelled.");
                return;
            }

            Assert.That(result.Telemetry.Outcome, Is.EqualTo(DecisionOutcome.PipelineError));
        }

        [Test]
        public async Task AFailureWhilePreparingTheRequestIsAPipelineErrorAndNothingIsSent()
        {
            // The availability check reads a state key the game never set.
            var fake = new FakeProvider(GoToTheTower);
            var agent = new Agent(Fixtures.VillageGuardProfile(), fake);
            agent.Bind("follow_player", new FakeHandler(ctx => !(bool)ctx.State["isFollowing"]));
            LogAssert.Expect(LogType.Warning, new Regex("could not decide\\. Preparing the request failed\\. KeyNotFoundException"));

            var result = await Fixtures.NotCancelled(agent.DecideAsync("Go to the tower."));

            Assert.That(result.Telemetry.Outcome, Is.EqualTo(DecisionOutcome.PipelineError));
            Assert.That(fake.Requests, Is.Empty);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("  ")]
        public void AnEmptyLabelThrowsAtTheCallAndSendsNothing(string label)
        {
            var fake = new FakeProvider(GoToTheTower);
            var agent = Fixtures.VillageGuard(fake);

            Assert.Throws<ArgumentException>(() => agent.DecideAsync("Go to the tower.", new DecideOptions { StimulusLabel = label }));
            Assert.That(fake.Requests, Is.Empty);
        }

        [Test]
        public async Task AnAlreadyCancelledCallSendsNothing()
        {
            var fake = new FakeProvider(GoToTheTower);
            var agent = Fixtures.VillageGuard(fake);

            using (var cts = new CancellationTokenSource())
            {
                cts.Cancel();
                try
                {
                    await agent.DecideAsync("Go to the tower.", null, cts.Token);
                    Assert.Fail("A cancelled decision has no result.");
                }
                catch (OperationCanceledException)
                {
                }
            }

            Assert.That(fake.Requests, Is.Empty);
        }

        [Test]
        public async Task ACancellationTheProviderReportsAsAnotherErrorStillEndsCancelled()
        {
            var fake = new FakeProvider(GoToTheTower) { Hold = true, CancelledWith = new InvalidOperationException("Request aborted") };
            var agent = Fixtures.VillageGuard(fake);

            using (var cts = new CancellationTokenSource())
            {
                var pending = agent.DecideAsync("Go to the tower.", null, cts.Token);
                cts.Cancel();
                try
                {
                    await pending;
                    Assert.Fail("The caller cancelled, so there is no result, not a pipeline error.");
                }
                catch (OperationCanceledException)
                {
                }
            }
        }

        [Test]
        public async Task CancellingWhileTheProviderThinksThrowsRatherThanReturning()
        {
            var fake = new FakeProvider(GoToTheTower) { Hold = true };
            var agent = Fixtures.VillageGuard(fake);

            using (var cts = new CancellationTokenSource())
            {
                var pending = agent.DecideAsync("Go to the tower.", null, cts.Token);
                Assert.That(fake.LastToken, Is.EqualTo(cts.Token), "The caller's token reaches the provider.");
                Assert.That(pending.GetAwaiter().IsCompleted, Is.False);

                cts.Cancel();
                try
                {
                    await pending;
                    Assert.Fail("A cancelled decision has no result.");
                }
                catch (OperationCanceledException)
                {
                }
            }
        }

        [Test]
        public async Task AReplyThatArrivesLaterStillBecomesADecision()
        {
            var fake = new FakeProvider(GoToTheTower) { Hold = true };
            var agent = Fixtures.VillageGuard(fake);

            var pending = agent.DecideAsync("Go to the tower.");
            Assert.That(pending.GetAwaiter().IsCompleted, Is.False, "The provider has not answered yet.");
            fake.Release();
            var result = await Fixtures.NotCancelled(pending);

            Assert.That(result.Decision.ActionId, Is.EqualTo("move_to"));
        }

        // ---- Targets (DR-014) ---------------------------------------------------------------

        [Test]
        public async Task TargetSourcesAreResolvedOnEveryDecisionAfterTheHandRegisteredTargets()
        {
            var fake = new FakeProvider(GoToTheTower);
            var agent = Fixtures.VillageGuard(fake);
            var scouted = new ExplicitTargetSource().Add("bridge", new object());
            agent.TargetSources.Add(scouted);

            await Fixtures.NotCancelled(agent.DecideAsync("Go to the tower."));
            var first = fake.LastRequest.Schema.Fields[1].AllowedValues;
            scouted.Add("north_gate", new object());
            await Fixtures.NotCancelled(agent.DecideAsync("Go to the tower."));
            var second = fake.LastRequest.Schema.Fields[1].AllowedValues;

            Assert.That(first, Is.EqualTo(new[] { "no_target", "tower", "training_dummy", "bridge" }));
            Assert.That(second, Is.EqualTo(new[] { "no_target", "tower", "training_dummy", "bridge", "north_gate" }));
            Assert.That(agent.Targets.Ids, Is.EqualTo(new[] { "tower", "training_dummy" }),
                "A source fills each decision's own registry, never the hand-registered one.");
        }

        [Test]
        public async Task AnIdOfferedTwiceIsAPipelineErrorAndNothingIsSent()
        {
            var fake = new FakeProvider(GoToTheTower);
            var agent = Fixtures.VillageGuard(fake);
            agent.TargetSources.Add(new ExplicitTargetSource().Add("tower", new object()));
            LogAssert.Expect(LogType.Warning, new Regex("could not decide\\. .*Target id 'tower'"));

            var result = await Fixtures.NotCancelled(agent.DecideAsync("Go to the tower."));

            Assert.That(result.Telemetry.Outcome, Is.EqualTo(DecisionOutcome.PipelineError));
            Assert.That(fake.Requests, Is.Empty);
        }

        // ---- Execute ------------------------------------------------------------------------

        [TestCase("move_to", "tower")]
        [TestCase("attack_target", "training_dummy")]
        [TestCase("follow_player", "no_target")]
        [TestCase("follow_player", "tower")]
        public void ExecuteRunsTheBoundHandlerWithTheDecisionAndItsTarget(string action, string target)
        {
            var agent = Fixtures.VillageGuard(new FakeProvider());
            var decided = Decided(action, target);

            Assert.That(agent.Execute(decided), Is.True);

            var handler = Fixtures.Handler(agent, action);
            Assert.That(handler.Executions, Has.Count.EqualTo(1));
            Assert.That(Executions(agent), Has.Count.EqualTo(1), "Only the decision's own handler runs.");

            var (ctx, decision) = handler.Executions[0];
            Assert.That(decision, Is.SameAs(decided.Decision));
            Assert.That(ctx.State["isFollowing"], Is.EqualTo(false));
            if (target != TargetRegistry.NoTarget)
            {
                agent.Targets.TryGet(target, out var registered);
                Assert.That(ctx.Targets.TryGet(target, out var given), Is.True);
                Assert.That(given, Is.SameAs(registered), "The handler finds the object registered under the id.");
            }
        }

        [TestCase("dance", "no_target", "the action is not registered")]
        [TestCase("move_to", "godzilla", "the target is not registered")]
        [TestCase("move_to", "no_target ", "the target is not registered")]
        [TestCase("stop_following", "no_target", "the action is not available right now")]
        public void ExecuteRefusesAndRunsNothing(string action, string target, string reason)
        {
            var agent = Fixtures.VillageGuard(new FakeProvider());
            LogAssert.Expect(LogType.Warning, new Regex($"did not execute '{action}' on '{target}': {reason}"));

            Assert.That(agent.Execute(Decided(action, target)), Is.False);
            Assert.That(Executions(agent), Is.Empty);
        }

        [Test]
        public async Task ExecuteChecksAvailabilityNowNotWhenTheDecisionWasMade()
        {
            var fake = new FakeProvider("{\"action\":\"follow_player\",\"target\":\"no_target\",\"statement\":\"Lead the way.\"}");
            var agent = Fixtures.VillageGuard(fake);
            var result = await Fixtures.NotCancelled(agent.DecideAsync("Keep me company."));
            Assert.That(fake.LastRequest.Schema.Fields[0].AllowedValues, Does.Contain("follow_player"));

            agent.State["isFollowing"] = true;
            LogAssert.Expect(LogType.Warning, new Regex("the action is not available right now"));

            Assert.That(agent.Execute(result), Is.False);
            Assert.That(Executions(agent), Is.Empty);
        }

        [Test]
        public async Task ExecuteResolvesTheTargetsAgain()
        {
            var fake = new FakeProvider("{\"action\":\"move_to\",\"target\":\"bridge\",\"statement\":\"\"}");
            var agent = Fixtures.VillageGuard(fake);
            var bridge = new object();
            var scouted = new ExplicitTargetSource().Add("bridge", bridge);
            agent.TargetSources.Add(scouted);
            var result = await Fixtures.NotCancelled(agent.DecideAsync("Cross over the stream."));

            Assert.That(agent.Execute(result), Is.True);
            Assert.That(Fixtures.Handler(agent, "move_to").Executions[0].Context.Targets.TryGet("bridge", out var given), Is.True);
            Assert.That(given, Is.SameAs(bridge));

            scouted.Remove("bridge");
            LogAssert.Expect(LogType.Warning, new Regex("the target is not registered"));
            Assert.That(agent.Execute(result), Is.False, "A target its source no longer offers is refused.");
            Assert.That(Executions(agent), Has.Count.EqualTo(1));
        }

        [Test]
        public void ExecutingNoneRunsNoHandler()
        {
            var agent = Fixtures.VillageGuard(new FakeProvider());

            Assert.That(agent.Execute(Decided(ActionRegistry.None, TargetRegistry.NoTarget)), Is.True);
            Assert.That(Executions(agent), Is.Empty);
        }

        [Test]
        public void ExecutingAPipelineErrorRunsNothing()
        {
            var agent = Fixtures.VillageGuard(new FakeProvider());
            var failed = new DecisionResult(null, new DecisionTelemetry { Outcome = DecisionOutcome.PipelineError });
            var failedWithADecision = new DecisionResult(
                new AgentDecision("move_to", "tower", ""), new DecisionTelemetry { Outcome = DecisionOutcome.PipelineError });

            Assert.That(agent.Execute(failed), Is.False);
            Assert.That(agent.Execute(failedWithADecision), Is.False);
            Assert.That(Executions(agent), Is.Empty);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void ExecuteRejectsANullResult()
        {
            var agent = Fixtures.VillageGuard(new FakeProvider());

            Assert.Throws<ArgumentNullException>(() => agent.Execute(null));
        }

        // ---- Construction and Bind ----------------------------------------------------------

        [Test]
        public void BindRegistersTheProfilesOwnDefinition()
        {
            var profile = Fixtures.VillageGuardProfile();
            var agent = new Agent(profile, new FakeProvider());
            var handler = new FakeHandler();

            agent.Bind("move_to", handler);

            Assert.That(agent.Identity, Is.SameAs(profile.Identity));
            Assert.That(agent.Actions.TryGet("move_to", out var definition, out var bound), Is.True);
            Assert.That(definition, Is.SameAs(profile.Actions[2].Action));
            Assert.That(bound, Is.SameAs(handler));
            Assert.That(agent.Actions.Definitions, Has.Count.EqualTo(1), "An action nobody binds is not registered.");
        }

        [Test]
        public void BindRejectsAnIdTheProfileDoesNotListOrListsTwice()
        {
            var profile = Fixtures.VillageGuardProfile();
            profile.Actions.Add(profile.Actions[2]);
            var agent = new Agent(profile, new FakeProvider());

            var missing = Assert.Throws<ArgumentException>(() => agent.Bind("dance", new FakeHandler()));
            var twice = Assert.Throws<ArgumentException>(() => agent.Bind("move_to", new FakeHandler()));

            Assert.That(missing.Message, Does.Contain("no action 'dance'"));
            Assert.That(twice.Message, Does.Contain("'move_to' twice"));
            Assert.That(agent.Actions.Definitions, Is.Empty);
        }

        [Test]
        public void BindLeavesTheRegistrysOwnRulesInPlace()
        {
            var agent = new Agent(Fixtures.VillageGuardProfile(), new FakeProvider());
            agent.Bind("move_to", new FakeHandler());

            Assert.Throws<InvalidOperationException>(() => agent.Bind("move_to", new FakeHandler()));
            Assert.Throws<ArgumentNullException>(() => agent.Bind("follow_player", null));
        }

        [Test]
        public void ConstructorsRejectMissingParts()
        {
            var profile = Fixtures.VillageGuardProfile();
            var identity = Fixtures.VillageGuardIdentity();
            var actions = new[] { new ActionDefinition { Id = "move_to" } };
            var fake = new FakeProvider();

            Assert.Throws<ArgumentNullException>(() => new Agent(null, fake));
            Assert.Throws<ArgumentNullException>(() => new Agent(profile, null));
            Assert.Throws<ArgumentNullException>(() => new Agent(null, actions, fake));
            Assert.Throws<ArgumentNullException>(() => new Agent(identity, null, fake));
            Assert.Throws<ArgumentNullException>(() => new Agent(identity, actions, null));
            Assert.Throws<ArgumentException>(() => new Agent(identity, new ActionDefinition[] { null }, fake));

            profile.Identity = null;
            Assert.Throws<ArgumentException>(() => new Agent(profile, fake));
        }

        [Test]
        public void AgentIsPlainCSharpNotAUnityObject()
        {
            // Build plan §2.1 and DR-011: a country with no GameObject owns one in code.
            Assert.That(typeof(UnityEngine.Object).IsAssignableFrom(typeof(Agent)), Is.False);
        }

        // ---- Helpers ------------------------------------------------------------------------

        private static DecisionResult Decided(string action, string target)
        {
            return new DecisionResult(new AgentDecision(action, target, ""), new DecisionTelemetry());
        }

        // Every execution of every FakeHandler bound to the agent.
        private static List<AgentDecision> Executions(Agent agent)
        {
            var all = new List<AgentDecision>();
            foreach (var definition in agent.Actions.Definitions)
            {
                all.AddRange(Fixtures.Handler(agent, definition.Id).Executions.Select(e => e.Decision));
            }

            return all;
        }

        // Every string in the request, schema included: all a provider could possibly send.
        private static string RequestText(DecisionRequest request)
        {
            var parts = new List<string> { request.SystemPrompt, request.FewShotBlock, request.Observations, request.Stimulus };
            parts.AddRange(request.History);
            foreach (var field in request.Schema.Fields)
            {
                parts.Add(field.Name);
                parts.Add(field.Description);
                if (field.AllowedValues != null)
                {
                    parts.AddRange(field.AllowedValues);
                }
            }

            return string.Join("\n", parts.Where(part => part != null));
        }
    }
}

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
using static Agenerela.Tests.Tokens;

namespace Agenerela.Tests
{
    // What the agent says when a guard stops it (#109), through Agent.DecideAsync and FakeProvider:
    // empty by default, the developer's line, or the model's from a second request. A FakeGuard's
    // function runs between the two requests, so it scripts the second one's reply. No test needs a
    // model, and none says anything about how good the model's line would be: that wording is
    // unmeasured. Every await of DecideAsync goes through Fixtures.NotCancelled (see AgentTests)
    // unless cancellation is what the test is about.
    public sealed class RefusalStatementTests
    {
        private const string GoToTheTower = "{\"action\":\"move_to\",\"target\":\"tower\",\"statement\":\"On my way.\"}";
        private const string NoTowerLine = "{\"statement\":\"I will not go there.\"}";
        private const string Reason = "the tower was the wrong one.";

        private static DecideOptions AskForLine => new DecideOptions { AskForRefusalLine = true };

        [TearDown]
        public void DestroyFixtures()
        {
            Fixtures.DestroyCreated();
        }

        // ---- The default --------------------------------------------------------------------

        [Test]
        public async Task ARewriteLeavesTheStatementEmptyAndTheModelsWordsOnModelDecision()
        {
            var fake = new FakeProvider(GoToTheTower);
            var agent = Fixtures.VillageGuard(fake);
            agent.Guards.Add(FakeGuard.Rewriting("Stopper", Reason));

            var result = await Fixtures.NotCancelled(agent.DecideAsync("Go to the tower."));

            Assert.That(result.Decision.ActionId, Is.EqualTo(ActionRegistry.None));
            Assert.That(result.Decision.Statement, Is.Empty);
            Assert.That(result.ModelDecision.Statement, Is.EqualTo("On my way."));
            Assert.That(fake.Requests, Has.Count.EqualTo(1), "Nobody asked for a line.");
            Assert.That(agent.Execute(result), Is.True);
        }

        [Test]
        public async Task ARejectLeavesTheStatementEmptyAndExecuteStillRefusesIt()
        {
            var fake = new FakeProvider(GoToTheTower);
            var agent = Fixtures.VillageGuard(fake);
            agent.Guards.Add(FakeGuard.Rejecting("Stopper"));
            LogAssert.Expect(LogType.Warning, new Regex("Stopper rejected 'move_to'"));

            var result = await Fixtures.NotCancelled(agent.DecideAsync("Go to the tower."));

            Assert.That(result.Decision.Statement, Is.Empty);
            Assert.That(result.ModelDecision.Statement, Is.EqualTo("On my way."));
            Assert.That(agent.Execute(result), Is.False);
        }

        [Test]
        public async Task ADecisionThatPassedKeepsItsStatementAndAsksForNothing()
        {
            var fake = new FakeProvider(GoToTheTower);
            var agent = Fixtures.VillageGuard(fake);
            bool called = false;
            agent.RefusalStatement = (verdict, ctx) =>
            {
                called = true;
                return "Never said.";
            };

            var result = await Fixtures.NotCancelled(agent.DecideAsync("Go to the tower.", AskForLine));

            Assert.That(result.Decision.Statement, Is.EqualTo("On my way."));
            Assert.That(fake.Requests, Has.Count.EqualTo(1), "A decision that passed costs no second request.");
            Assert.That(called, Is.False);
            Assert.That(result.Telemetry.RefusalLineSeconds, Is.EqualTo(0f));
            Assert.That(result.Telemetry.RefusalLineError, Is.Null);
        }

        // ---- The developer's line -----------------------------------------------------------

        [Test]
        public async Task RefusalStatementBecomesTheStatementAndGetsTheVerdictAndContext()
        {
            var fake = new FakeProvider(GoToTheTower);
            var agent = Fixtures.VillageGuard(fake);
            agent.Guards.Add(FakeGuard.Rewriting("Stopper", Reason));
            GuardVerdict seen = default;
            AgentContext seenContext = null;
            agent.RefusalStatement = (verdict, ctx) =>
            {
                seen = verdict;
                seenContext = ctx;
                return "There's no such tower here.";
            };

            var result = await Fixtures.NotCancelled(agent.DecideAsync("Go to the tower."));

            Assert.That(result.Decision.Statement, Is.EqualTo("There's no such tower here."));
            Assert.That(result.Decision.ActionId, Is.EqualTo(ActionRegistry.None));
            Assert.That(result.Decision.TargetId, Is.EqualTo(TargetRegistry.NoTarget));
            Assert.That(result.ModelDecision.Statement, Is.EqualTo("On my way."));
            Assert.That(result.Verdict.Value.Guard, Is.EqualTo("Stopper"));

            Assert.That(seen.Guard, Is.EqualTo("Stopper"));
            Assert.That(seen.Kind, Is.EqualTo(GuardVerdictKind.RewriteToNone));
            Assert.That(seen.Reason, Is.EqualTo(Reason));
            Assert.That(seenContext.Stimulus, Is.EqualTo("Go to the tower."));
        }

        [Test]
        public async Task TheDevelopersLineWinsAndNoRequestFollowsIt()
        {
            var fake = new FakeProvider(GoToTheTower);
            var agent = Fixtures.VillageGuard(fake);
            agent.Guards.Add(FakeGuard.Rewriting("Stopper"));
            agent.RefusalStatement = (verdict, ctx) => "My post is here.";

            var result = await Fixtures.NotCancelled(agent.DecideAsync("Go to the tower.", AskForLine));

            Assert.That(result.Decision.Statement, Is.EqualTo("My post is here."));
            Assert.That(fake.Requests, Has.Count.EqualTo(1));
        }

        [Test]
        public async Task AnEmptyLineIsALineSoNoRequestFollowsIt()
        {
            var fake = new FakeProvider(GoToTheTower);
            var agent = Fixtures.VillageGuard(fake);
            agent.Guards.Add(FakeGuard.Rewriting("Stopper"));
            agent.RefusalStatement = (verdict, ctx) => "";

            var result = await Fixtures.NotCancelled(agent.DecideAsync("Go to the tower.", AskForLine));

            Assert.That(result.Decision.Statement, Is.Empty);
            Assert.That(fake.Requests, Has.Count.EqualTo(1));
        }

        [TestCase(false, 1, "")]
        [TestCase(true, 2, "I will not go there.")]
        public async Task ANullLineMeansTheDefault(bool ask, int requests, string statement)
        {
            var fake = new FakeProvider(GoToTheTower);
            var agent = Fixtures.VillageGuard(fake);
            agent.Guards.Add(ScriptingTheLine(fake, NoTowerLine));
            agent.RefusalStatement = (verdict, ctx) => null;

            var result = await Fixtures.NotCancelled(agent.DecideAsync("Go to the tower.", ask ? AskForLine : null));

            Assert.That(result.Decision.Statement, Is.EqualTo(statement));
            Assert.That(fake.Requests, Has.Count.EqualTo(requests));
        }

        [TestCase(false, 1, "")]
        [TestCase(true, 2, "I will not go there.")]
        public async Task ACallbackThatThrowsWarnsAndFallsBackToTheDefault(bool ask, int requests, string statement)
        {
            var fake = new FakeProvider(GoToTheTower);
            var agent = Fixtures.VillageGuard(fake);
            agent.Guards.Add(ScriptingTheLine(fake, NoTowerLine));
            agent.RefusalStatement = (verdict, ctx) => throw new InvalidOperationException("no such line");
            LogAssert.Expect(LogType.Warning, new Regex("RefusalStatement failed. InvalidOperationException: no such line"));

            var result = await Fixtures.NotCancelled(agent.DecideAsync("Go to the tower.", ask ? AskForLine : null));

            Assert.That(result.Decision.ActionId, Is.EqualTo(ActionRegistry.None));
            Assert.That(result.Decision.Statement, Is.EqualTo(statement));
            Assert.That(fake.Requests, Has.Count.EqualTo(requests));
            Assert.That(result.Telemetry.Outcome, Is.Null, "The decision still stands.");
            Assert.That(agent.Execute(result), Is.True);
            LogAssert.NoUnexpectedReceived();
        }

        // ---- The model's line: one extra request, only when a guard fired ---------------------

        [Test]
        public async Task WithAskForRefusalLineARewriteSendsOneMoreRequestAndUsesItsLine()
        {
            var fake = new FakeProvider(GoToTheTower);
            var agent = Fixtures.VillageGuard(fake);
            agent.Guards.Add(ScriptingTheLine(fake, NoTowerLine));

            var result = await Fixtures.NotCancelled(agent.DecideAsync("Go to the tower.", AskForLine));

            Assert.That(fake.Requests, Has.Count.EqualTo(2));
            Assert.That(result.Decision.Statement, Is.EqualTo("I will not go there."));
            Assert.That(result.Decision.ActionId, Is.EqualTo(ActionRegistry.None), "The line never changes which action runs.");
            Assert.That(result.Decision.TargetId, Is.EqualTo(TargetRegistry.NoTarget));
            Assert.That(result.ModelDecision.Statement, Is.EqualTo("On my way."));
            Assert.That(result.Telemetry.RefusalLineError, Is.Null);
            Assert.That(agent.Execute(result), Is.True);
        }

        [Test]
        public async Task WithAskForRefusalLineARejectSendsOneToo()
        {
            var fake = new FakeProvider(GoToTheTower);
            var agent = Fixtures.VillageGuard(fake);
            agent.Guards.Add(new FakeGuard("Stopper", _ =>
            {
                fake.Reply = NoTowerLine;
                return GuardVerdict.Reject("the fake rejected it.");
            }));
            LogAssert.Expect(LogType.Warning, new Regex("Stopper rejected 'move_to'"));

            var result = await Fixtures.NotCancelled(agent.DecideAsync("Go to the tower.", AskForLine));

            Assert.That(fake.Requests, Has.Count.EqualTo(2));
            Assert.That(result.Decision.Statement, Is.EqualTo("I will not go there."));
            Assert.That(agent.Execute(result), Is.False, "A line does not make a rejected answer one to carry out.");
        }

        [Test]
        public async Task TheSwitchIsReadWhenTheCallIsMade()
        {
            var fake = new FakeProvider(GoToTheTower) { Hold = true };
            var agent = Fixtures.VillageGuard(fake);
            agent.Guards.Add(FakeGuard.Rewriting("Stopper"));
            var options = new DecideOptions();

            var pending = agent.DecideAsync("Go to the tower.", options);
            options.AskForRefusalLine = true;
            fake.Release();
            await Fixtures.NotCancelled(pending);

            Assert.That(fake.Requests, Has.Count.EqualTo(1));
        }

        [Test]
        public async Task TheSecondRequestIsTheSameSystemPromptOneTurnAndOnlyAStatement()
        {
            var fake = new FakeProvider(GoToTheTower);
            var agent = Fixtures.VillageGuard(fake);
            await Fixtures.NotCancelled(agent.DecideAsync("Keep watch."));
            agent.Guards.Add(ScriptingTheLine(fake, NoTowerLine));
            var options = AskForLine;
            options.Observations.Add("The gate is shut.");

            await Fixtures.NotCancelled(agent.DecideAsync("Go to the tower.", options));

            var decided = fake.Requests[1];
            var line = fake.Requests[2];
            Assert.That(decided.History, Is.Not.Empty, "The decision had a history, examples and observations to leave out.");
            Assert.That(decided.FewShotBlock, Is.Not.Empty);
            Assert.That(decided.Observations, Is.Not.Empty);

            Assert.That(line.SystemPrompt, Is.EqualTo(decided.SystemPrompt));
            Assert.That(line.History, Is.Empty);
            Assert.That(line.FewShotBlock, Is.Null);
            Assert.That(line.Observations, Is.Null);
            Assert.That(line.Stimulus, Does.Contain("\"Go to the tower.\""));
            Assert.That(line.Stimulus, Does.Contain(Reason));

            var field = line.Schema.Fields.Single();
            Assert.That(field.Name, Is.EqualTo(DecisionSchema.StatementFieldName));
            Assert.That(field.Required, Is.True);
            Assert.That(field.AllowedValues, Is.Null);
            Assert.That(new JsonSchemaSerializer().Serialize(line.Schema), Does.Contain("\"statement\"").And.Not.Contain("\"action\""));
        }

        [Test]
        public async Task TheRequestTakesTheStimulusVerbatimAndEndsTheReasonAsASentence()
        {
            var fake = new FakeProvider(GoToTheTower);
            var agent = Fixtures.VillageGuard(fake);
            agent.Guards.Add(FakeGuard.Rewriting("Stopper", "the tower was the wrong one"));

            await Fixtures.NotCancelled(agent.DecideAsync("Go to {0} and {tower}.", AskForLine));

            Assert.That(fake.LastRequest.Stimulus, Does.Contain("\"Go to {0} and {tower}.\""));
            Assert.That(fake.LastRequest.Stimulus, Does.Contain("the tower was the wrong one. "));
        }

        [Test]
        public async Task TheGroundingGuardsRequestSaysNothingAboutProbabilities()
        {
            var fake = new FakeProvider();
            fake.ReplyWith(Scarecrow());
            var agent = Fixtures.VillageGuard(fake);

            var result = await Fixtures.NotCancelled(agent.DecideAsync("Attack the scarecrow.", AskForLine));

            Assert.That(result.Verdict.Value.Guard, Is.EqualTo("TargetGroundingGuard"));
            Assert.That(fake.Requests, Has.Count.EqualTo(2));
            Assert.That(fake.LastRequest.Stimulus, Does.Contain("nothing like that is here."));
            Assert.That(fake.LastRequest.Stimulus, Does.Not.Contain("probability"));
            Assert.That(result.Telemetry.GuardReason, Does.Contain("probability"), "Telemetry keeps the reason word for word.");
            Assert.That(result.Verdict.Value.Reason, Does.Contain("probability"));
        }

        // ---- The queue ----------------------------------------------------------------------

        [TestCase(DecisionPriority.PlayerFacing, "player", "line", "ambient")]
        [TestCase(DecisionPriority.Background, "player", "ambient", "line")]
        public async Task TheSecondRequestWaitsInTheQueueAtTheCallsPriority(
            DecisionPriority priority, string second, string third, string fourth)
        {
            var fake = new FakeProvider(GoToTheTower) { Hold = true };
            var stopped = Fixtures.VillageGuard(fake);
            stopped.Guards.Add(FakeGuard.Rewriting("Stopper", Reason));
            var others = Fixtures.VillageGuard(fake);

            var first = stopped.DecideAsync("stopped", new DecideOptions { AskForRefusalLine = true, Priority = priority });
            var ambient = others.DecideAsync("ambient", new DecideOptions { Priority = DecisionPriority.Background });
            var player = others.DecideAsync("player");

            // Each reply can be read as a decision or as a line, so one script serves them all.
            while (fake.HeldCount > 0)
            {
                fake.Release();
            }

            var result = await Fixtures.NotCancelled(first);
            await Fixtures.NotCancelled(ambient);
            await Fixtures.NotCancelled(player);

            var order = fake.Requests.Select(r => r.Stimulus.Contains(Reason) ? "line" : r.Stimulus.Contains("ambient") ? "ambient" : r.Stimulus.Contains("player") ? "player" : "stopped");
            Assert.That(order, Is.EqualTo(new[] { "stopped", second, third, fourth }));
            Assert.That(result.Telemetry.QueuedAhead, Is.EqualTo(0), "The decision's place in the queue, not the line's.");
        }

        // ---- When it fails ------------------------------------------------------------------

        [TestCase(ProviderErrorKind.Unreachable, PipelineErrorKind.ProviderUnreachable)]
        [TestCase(ProviderErrorKind.Timeout, PipelineErrorKind.ProviderTimeout)]
        public async Task AProviderErrorLeavesTheDecisionStanding(ProviderErrorKind kind, PipelineErrorKind recorded)
        {
            var fake = new FakeProvider(GoToTheTower);
            var agent = Fixtures.VillageGuard(fake);
            agent.Guards.Add(new FakeGuard("Stopper", _ =>
            {
                fake.FailWith(kind);
                return GuardVerdict.RewriteToNone(Reason);
            }));
            LogAssert.Expect(LogType.Warning, new Regex("still decided, but has no refusal line"));

            var result = await Fixtures.NotCancelled(agent.DecideAsync("Go to the tower.", AskForLine));

            AssertStandsWithNoLine(fake, agent, result, recorded);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public async Task AProviderThatThrowsSomethingElseOrReturnsNothingLeavesItStandingToo()
        {
            var fake = new FakeProvider(GoToTheTower);
            var agent = Fixtures.VillageGuard(fake);
            agent.Guards.Add(new FakeGuard("Stopper", _ =>
            {
                fake.Failure = new InvalidOperationException("broken");
                fake.FailsImmediately = true;
                return GuardVerdict.RewriteToNone(Reason);
            }));
            LogAssert.Expect(LogType.Warning, new Regex("no refusal line. The provider failed. InvalidOperationException: broken"));

            var result = await Fixtures.NotCancelled(agent.DecideAsync("Go to the tower.", AskForLine));
            AssertStandsWithNoLine(fake, agent, result, PipelineErrorKind.ProviderFailed);

            fake = new FakeProvider(GoToTheTower);
            agent = Fixtures.VillageGuard(fake);
            var silent = fake;
            agent.Guards.Add(new FakeGuard("Stopper", _ =>
            {
                silent.ReturnsNoResult = true;
                return GuardVerdict.RewriteToNone(Reason);
            }));
            LogAssert.Expect(LogType.Warning, new Regex("no refusal line. The provider returned no result."));

            result = await Fixtures.NotCancelled(agent.DecideAsync("Go to the tower.", AskForLine));
            AssertStandsWithNoLine(fake, agent, result, PipelineErrorKind.ProviderFailed);
        }

        [TestCase("Sorry, no.")]
        [TestCase("")]
        [TestCase(null)]
        [TestCase("{}")]
        [TestCase("{\"statement\":null}")]
        [TestCase("{\"statement\":3}")]
        [TestCase("{\"statement\":\"No.\"} and more")]
        [TestCase("{\"statement\":\"No.\",\"statement\":\"Yes.\"}")]
        public async Task AnUnreadableReplyLeavesTheDecisionStanding(string reply)
        {
            var fake = new FakeProvider(GoToTheTower);
            var agent = Fixtures.VillageGuard(fake);
            agent.Guards.Add(ScriptingTheLine(fake, reply));
            LogAssert.Expect(LogType.Warning, new Regex("still decided, but has no refusal line"));

            var result = await Fixtures.NotCancelled(agent.DecideAsync("Go to the tower.", AskForLine));

            AssertStandsWithNoLine(fake, agent, result, PipelineErrorKind.UnreadableReply);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public async Task AnEmptyLineFromTheModelIsAnAnswerAndAnyOtherFieldIsIgnored()
        {
            var fake = new FakeProvider(GoToTheTower);
            var agent = Fixtures.VillageGuard(fake);
            agent.Guards.Add(ScriptingTheLine(fake, "{\"action\":\"attack_target\",\"statement\":\"\"}"));

            var result = await Fixtures.NotCancelled(agent.DecideAsync("Go to the tower.", AskForLine));

            Assert.That(result.Decision.ActionId, Is.EqualTo(ActionRegistry.None), "An action in the reply changes nothing.");
            Assert.That(result.Decision.Statement, Is.Empty);
            Assert.That(result.Telemetry.RefusalLineError, Is.Null);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public async Task CancellingDuringTheSecondRequestCancelsTheDecisionAndRemembersNothing()
        {
            var fake = new FakeProvider(GoToTheTower) { Hold = true };
            var agent = Fixtures.VillageGuard(fake);
            agent.Guards.Add(FakeGuard.Rewriting("Stopper"));

            using (var cts = new CancellationTokenSource())
            {
                var pending = agent.DecideAsync("Go to the tower.", AskForLine, cts.Token);
                fake.Release();
                Assert.That(fake.Requests, Has.Count.EqualTo(2));
                Assert.That(fake.HeldCount, Is.EqualTo(1));

                cts.Cancel();

                bool cancelled = false;
                try
                {
                    await pending;
                }
                catch (OperationCanceledException)
                {
                    cancelled = true;
                }

                Assert.That(cancelled, Is.True);
            }

            Assert.That(History(agent), Is.Empty);
        }

        // ---- Memory -------------------------------------------------------------------------

        [Test]
        public async Task MemoryHoldsTheDevelopersLine()
        {
            var fake = new FakeProvider(GoToTheTower);
            var agent = Fixtures.VillageGuard(fake);
            agent.Guards.Add(FakeGuard.Rewriting("Stopper"));
            agent.RefusalStatement = (verdict, ctx) => "My post is here.";

            await Fixtures.NotCancelled(agent.DecideAsync("Go to the tower."));

            AssertRemembered(agent, "My post is here.");
        }

        [Test]
        public async Task MemoryHoldsTheModelsLineAndTheNextRequestReadsIt()
        {
            var fake = new FakeProvider(GoToTheTower);
            var agent = Fixtures.VillageGuard(fake);
            agent.Guards.Add(ScriptingTheLine(fake, NoTowerLine));

            await Fixtures.NotCancelled(agent.DecideAsync("Go to the tower.", AskForLine));

            AssertRemembered(agent, "I will not go there.");

            await Fixtures.NotCancelled(agent.DecideAsync("Go to the tower."));
            Assert.That(fake.LastRequest.History[1], Does.Not.Contain("On my way."));
            Assert.That(fake.LastRequest.History[1], Does.Contain("I will not go there."));
        }

        [Test]
        public async Task MemoryHoldsAnEmptyStatementByDefaultAndWhenTheLineFailed()
        {
            var fake = new FakeProvider(GoToTheTower);
            var agent = Fixtures.VillageGuard(fake);
            agent.Guards.Add(FakeGuard.Rewriting("Stopper"));

            await Fixtures.NotCancelled(agent.DecideAsync("Go to the tower."));
            AssertRemembered(agent, "");

            fake = new FakeProvider(GoToTheTower);
            agent = Fixtures.VillageGuard(fake);
            agent.Guards.Add(ScriptingTheLine(fake, "Sorry, no."));
            LogAssert.Expect(LogType.Warning, new Regex("no refusal line"));

            await Fixtures.NotCancelled(agent.DecideAsync("Go to the tower.", AskForLine));
            AssertRemembered(agent, "");
        }

        // ---- Telemetry ----------------------------------------------------------------------

        [Test]
        public async Task TheSecondRequestsTimeAndTokensAreRecordedApartFromTheDecisions()
        {
            var fake = new FakeProvider(GoToTheTower) { PromptTokens = 100, CompletionTokens = 20 };
            var agent = Fixtures.VillageGuard(fake);
            agent.Guards.Add(new FakeGuard("Stopper", _ =>
            {
                fake.Reply = NoTowerLine;
                fake.PromptTokens = 7;
                fake.CompletionTokens = 3;
                fake.DelayMilliseconds = 200;
                return GuardVerdict.RewriteToNone(Reason);
            }));

            var result = await Fixtures.NotCancelled(agent.DecideAsync("Go to the tower.", AskForLine));

            var telemetry = result.Telemetry;
            Assert.That(telemetry.PromptTokens, Is.EqualTo(100), "The decision's own count, for Phase 4 to compare.");
            Assert.That(telemetry.CompletionTokens, Is.EqualTo(20));
            Assert.That(telemetry.RefusalLinePromptTokens, Is.EqualTo(7));
            Assert.That(telemetry.RefusalLineCompletionTokens, Is.EqualTo(3));
            Assert.That(telemetry.RefusalLineSeconds, Is.GreaterThanOrEqualTo(0.15f));
            Assert.That(telemetry.LatencySeconds, Is.LessThan(telemetry.RefusalLineSeconds), "LatencySeconds is the decision alone.");
        }

        [Test]
        public async Task ADevelopersLineCostsNoRequestAndRecordsNone()
        {
            var fake = new FakeProvider(GoToTheTower) { PromptTokens = 100 };
            var agent = Fixtures.VillageGuard(fake);
            agent.Guards.Add(FakeGuard.Rewriting("Stopper"));
            agent.RefusalStatement = (verdict, ctx) => "My post is here.";

            var result = await Fixtures.NotCancelled(agent.DecideAsync("Go to the tower.", AskForLine));

            Assert.That(result.Telemetry.RefusalLineSeconds, Is.EqualTo(0f));
            Assert.That(result.Telemetry.RefusalLinePromptTokens, Is.EqualTo(0));
            Assert.That(result.Telemetry.RefusalLineError, Is.Null);
        }

        // ---- Helpers ------------------------------------------------------------------------

        // A guard that stops every decision, having scripted the reply the second request gets.
        private static FakeGuard ScriptingTheLine(FakeProvider fake, string reply)
        {
            return new FakeGuard("Stopper", _ =>
            {
                fake.Reply = reply;
                return GuardVerdict.RewriteToNone(Reason);
            });
        }

        private static void AssertStandsWithNoLine(FakeProvider fake, Agent agent, DecisionResult result, PipelineErrorKind recorded)
        {
            Assert.That(fake.Requests, Has.Count.EqualTo(2));
            Assert.That(result.Decision.ActionId, Is.EqualTo(ActionRegistry.None));
            Assert.That(result.Decision.Statement, Is.Empty);
            Assert.That(result.ModelDecision.Statement, Is.EqualTo("On my way."));
            Assert.That(result.Telemetry.RefusalLineError, Is.EqualTo(recorded));
            Assert.That(result.Telemetry.Outcome, Is.Null, "Never a pipeline error of the decision itself.");
            Assert.That(result.Telemetry.ErrorKind, Is.Null);
            Assert.That(result.Telemetry.ErrorMessage, Is.Null);
            Assert.That(agent.Execute(result), Is.True);
        }

        private static IReadOnlyList<string> History(Agent agent)
        {
            var ctx = new AgentContext(agent.Identity, "", null, null, agent.Targets, agent.Actions);
            return ((RollingHistory)agent.Memory).Recall(ctx);
        }

        private static void AssertRemembered(Agent agent, string statement)
        {
            Assert.That(History(agent), Is.EqualTo(new[]
            {
                "Player: \"Go to the tower.\"",
                "{\"action\":\"none\",\"target\":\"no_target\",\"statement\":\"" + statement + "\"}",
            }));
        }

        // "Attack the scarecrow.": the mask wrote training_dummy, but the model wanted "sc".
        private static IReadOnlyList<TokenProbability> Scarecrow()
        {
            return Reply("attack_target",
                T("training", -2.95, ("sc", -0.06), ("str", -3.4), ("training", -2.95), ("the", -4.1), ("dummy", -4.8)),
                T("_dummy", -0.01, ("_dummy", -0.01)));
        }
    }
}

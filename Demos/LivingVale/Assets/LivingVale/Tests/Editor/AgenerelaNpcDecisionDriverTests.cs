using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Agenerela;
using Agenerela.Providers;
using LivingVale.Agenerela;
using LivingVale.Interaction;
using NUnit.Framework;

namespace LivingVale.Tests
{
    public sealed class AgenerelaNpcDecisionDriverTests
    {
        private static readonly NpcDescriptor Mara = new NpcDescriptor("npc_mara", "Mara", "General trader");
        private static readonly NpcDescriptor Iven = new NpcDescriptor("npc_iven", "Iven", "Healer");

        private FakeLlmProvider provider;
        private AgenerelaNpcDecisionDriver driver;
        private long nextId;

        [SetUp]
        public void SetUp()
        {
            provider = new FakeLlmProvider();
            driver = new AgenerelaNpcDecisionDriver(provider, "qwen3.5:2b");
        }

        private NpcTurn Turn(NpcDescriptor npc, string line) => new NpcTurn(++nextId, npc, line, Array.Empty<ChatLine>());

        // A normal turn, which must not end cancelled: the Test Framework counts a cancelled test as passed.
        private async Task<NpcReply> Decide(NpcTurn turn)
        {
            var decide = driver.DecideAsync(turn, CancellationToken.None);
            await Run.NotCancelled(decide);
            return decide.Result;
        }

        [Test]
        public void LabelNamesTheFrameworkAndTheModel()
        {
            Assert.AreEqual("Agenerela · qwen3.5:2b", driver.Label);
        }

        [Test]
        public async Task ADecisionIsReturnedWithItsResultAsDriverState()
        {
            provider.Reply = FakeLlmProvider.Json("none", "no_target", "Morning, traveler.");

            var reply = await Decide(Turn(Mara, "Hello there."));

            Assert.AreEqual(NpcReplyFailure.None, reply.Failure);
            Assert.AreEqual("Morning, traveler.", reply.Decision.Statement);
            Assert.AreEqual(ActionRegistry.None, reply.Decision.ActionId);
            Assert.IsInstanceOf<DecisionResult>(reply.DriverState);
            Assert.AreSame(reply.Decision, ((DecisionResult)reply.DriverState).Decision);
        }

        [Test]
        public async Task ThePlayersLineIsTheStimulusAndThePersonaReachesThePrompt()
        {
            await Decide(Turn(Mara, "What happened to the wagon?"));

            var request = provider.LastRequest;
            StringAssert.Contains("What happened to the wagon?", request.Stimulus);
            StringAssert.StartsWith(AgenerelaNpcDecisionDriver.StimulusLabel, request.Stimulus);
            StringAssert.Contains("Mara", request.SystemPrompt);
            StringAssert.Contains("healing potions", request.SystemPrompt);
            StringAssert.DoesNotContain("healing potions", request.Observations ?? "");
        }

        [Test]
        public async Task ThePersonasKnowledgeIsInTheSystemPromptAndTurnObservationsAreObservations()
        {
            driver.TurnObservations = turn => new[] { "The traveler carries 40 gold." };

            await Decide(Turn(Iven, "Hello."));

            var request = provider.LastRequest;
            StringAssert.Contains("The traveler carries 40 gold.", request.Observations);
            StringAssert.Contains("Three patients", request.SystemPrompt);
            StringAssert.DoesNotContain("Three patients", request.Observations ?? "");
            StringAssert.Contains(ValeNpcPersonas.SpeakingRules, request.SystemPrompt);
        }

        [Test]
        public async Task AnUnreadableReplyIsMalformed()
        {
            provider.Reply = "I am not JSON.";

            var reply = await Decide(Turn(Mara, "Hello."));

            Assert.AreEqual(NpcReplyFailure.Malformed, reply.Failure);
            Assert.IsNull(reply.Decision);
        }

        [Test]
        public async Task AnUnreachableServerIsAProviderError()
        {
            provider.Failure = new ProviderException(ProviderErrorKind.Unreachable, provider.Name, "No server.");

            var reply = await Decide(Turn(Mara, "Hello."));

            Assert.AreEqual(NpcReplyFailure.ProviderError, reply.Failure);
        }

        [Test]
        public void ToReply_MapsEachPipelineErrorKind()
        {
            foreach (PipelineErrorKind kind in Enum.GetValues(typeof(PipelineErrorKind)))
            {
                var telemetry = new DecisionTelemetry { Outcome = DecisionOutcome.PipelineError, ErrorKind = kind };
                var result = new DecisionResult(null, telemetry);
                var expected = kind == PipelineErrorKind.UnreadableReply || kind == PipelineErrorKind.ProviderMalformedResponse
                    ? NpcReplyFailure.Malformed
                    : NpcReplyFailure.ProviderError;

                Assert.AreEqual(expected, AgenerelaNpcDecisionDriver.ToReply(result).Failure, kind.ToString());
            }

            Assert.AreEqual(NpcReplyFailure.ProviderError, AgenerelaNpcDecisionDriver.ToReply(null).Failure);
        }

        [Test]
        public async Task CancellingTheTokenCancelsTheDecisionAndTheRequest()
        {
            provider.Hold = true;
            using (var cts = new CancellationTokenSource())
            {
                var decide = driver.DecideAsync(Turn(Mara, "Hello."), cts.Token);
                Assert.AreEqual(1, provider.Requests.Count, "The request should have been sent.");

                cts.Cancel();

                bool cancelled = false;
                try
                {
                    await decide;
                }
                catch (OperationCanceledException)
                {
                    cancelled = true;
                }

                Assert.IsTrue(cancelled, "A cancelled turn must end cancelled, never as a reply.");
                Assert.IsTrue(provider.LastToken.IsCancellationRequested, "The provider's request must see the cancellation.");
            }
        }

        [Test]
        public async Task AnAlreadyCancelledTokenSendsNothing()
        {
            bool cancelled = false;
            try
            {
                await driver.DecideAsync(Turn(Mara, "Hello."), new CancellationToken(true));
            }
            catch (OperationCanceledException)
            {
                cancelled = true;
            }

            Assert.IsTrue(cancelled);
            Assert.AreEqual(0, provider.Requests.Count);
        }

        [Test]
        public async Task ExecuteCallsTheAgentsExecute()
        {
            int waved = 0;
            bool available = true;
            driver = new AgenerelaNpcDecisionDriver(provider, "fake", null, (npc, agent) =>
                agent.Actions.Register(new ActionDefinition { Id = "wave", Description = "Wave at the traveler." },
                    new DelegateActionHandler(ctx => available, (ctx, decision) => waved++)));
            provider.Reply = FakeLlmProvider.Json("wave", "no_target", "Hello!");

            var turn = Turn(Mara, "Wave at me.");
            var reply = await Decide(turn);
            Assert.AreEqual("wave", reply.Decision.ActionId);

            Assert.IsTrue(driver.Execute(turn, reply));
            Assert.AreEqual(1, waved, "The bound handler must run through Agent.Execute.");

            // Agent.Execute re-checks availability now, not as it was when deciding (hard rule 5).
            available = false;
            Assert.IsFalse(driver.Execute(turn, reply));
            Assert.AreEqual(1, waved);
        }

        [Test]
        public async Task ExecuteRunsNoneAndRefusesAReplyItDidNotMake()
        {
            var turn = Turn(Mara, "Hello.");
            var reply = await Decide(turn);

            Assert.IsTrue(driver.Execute(turn, reply));
            Assert.AreEqual(1, driver.ExecutedCount);

            var foreign = NpcReply.Decided(new AgentDecision(ActionRegistry.None, TargetRegistry.NoTarget, "Hi."));
            Assert.IsFalse(driver.Execute(turn, foreign), "A reply with no DecisionResult cannot be executed.");
            Assert.IsFalse(driver.Execute(Turn(Iven, "Hello."), reply), "Iven's agent was never built, so it decided nothing.");
        }

        [Test]
        public async Task AnActionNoAgentRegisteredIsContainedAndNeverClaimed()
        {
            provider.Reply = FakeLlmProvider.Json("sell_potion", "no_target", "Here is your potion.");

            var turn = Turn(Mara, "Sell me a potion.");
            var reply = await Decide(turn);

            // The guards stop it; the model's line, which claims the sale, is not what is shown.
            Assert.AreEqual(ActionRegistry.None, reply.Decision.ActionId);
            Assert.AreNotEqual("Here is your potion.", reply.Decision.Statement);
            Assert.IsNotNull(((DecisionResult)reply.DriverState).Verdict);
        }

        [Test]
        public async Task EachNpcRemembersItsOwnEarlierTurns()
        {
            provider.Reply = FakeLlmProvider.Json("none", "no_target", "Ten gold a potion.");
            var first = Turn(Mara, "How much is a potion?");
            Assert.IsTrue(driver.Execute(first, await Decide(first)), "Only an executed decision is remembered.");

            provider.Reply = FakeLlmProvider.Json("none", "no_target", "Fine.");
            await Decide(Turn(Mara, "And two of them?"));

            var history = provider.LastRequest.History;
            Assert.AreEqual(2, history.Count, "One earlier exchange: its stimulus and its decision.");
            StringAssert.Contains("How much is a potion?", history[0]);
            StringAssert.Contains("Ten gold a potion.", history[1]);

            await Decide(Turn(Iven, "Hello."));
            Assert.AreEqual(0, provider.LastRequest.History.Count, "Iven must not remember Mara's conversation.");
        }

        [Test]
        public async Task ACancelledTurnIsNotRemembered()
        {
            provider.Hold = true;
            using (var cts = new CancellationTokenSource())
            {
                var decide = driver.DecideAsync(Turn(Mara, "Forget this."), cts.Token);
                cts.Cancel();
                try
                {
                    await decide;
                }
                catch (OperationCanceledException)
                {
                }
            }

            provider.Hold = false;
            await Decide(Turn(Mara, "Hello."));
            Assert.AreEqual(0, provider.LastRequest.History.Count);
        }

        [Test]
        public async Task ThroughTheCoordinator_TheStatementIsShown()
        {
            provider.Reply = FakeLlmProvider.Json("none", "no_target", "Mind the mud.");
            var coordinator = new ConversationCoordinator(driver);
            coordinator.Open(Mara);

            Assert.IsTrue(coordinator.Submit("Hello."));
            await Run.NotCancelled(coordinator.LastTurn);

            var history = coordinator.HistoryFor(Mara.Id);
            Assert.AreEqual(ChatSpeaker.Npc, history[history.Count - 1].Speaker);
            Assert.AreEqual("Mind the mud.", history[history.Count - 1].Text);
            Assert.AreEqual(1, driver.ExecutedCount);
        }

        [Test]
        public async Task ThroughTheCoordinator_AnUnreachableServerFailsTheTurnWithTheGamesLine()
        {
            provider.Failure = new ProviderException(ProviderErrorKind.Unreachable, provider.Name, "No server.");
            var coordinator = new ConversationCoordinator(driver);
            var failures = new List<TurnFailure>();
            coordinator.Changed += e =>
            {
                if (e.Kind == ConversationEventKind.TurnFailed)
                {
                    failures.Add(e.Failure);
                }
            };
            coordinator.Open(Mara);

            Assert.IsTrue(coordinator.Submit("Hello."));
            await Run.NotCancelled(coordinator.LastTurn);

            CollectionAssert.AreEqual(new[] { TurnFailure.ProviderError }, failures);
            Assert.IsFalse(coordinator.IsAwaitingReply, "The player can try again.");
            Assert.AreEqual(0, driver.ExecutedCount);
        }

        [Test]
        public void EveryAuthoredPersonaHasKnowledgeAndTheHouseStatementRules()
        {
            foreach (var persona in ValeNpcPersonas.All)
            {
                var identity = persona.ToIdentity();
                Assert.That(identity.Knowledge.Count, Is.InRange(1, 7), persona.NpcId);
                Assert.AreEqual(ValeNpcPersonas.SpeakingRules, identity.StatementRules, persona.NpcId);
                StringAssert.DoesNotContain(ValeNpcPersonas.SpeakingRules, identity.Personality, persona.NpcId + ": the rules are no longer appended to the personality.");
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Agenerela.Tests
{
    // The decision feed (#166): one entry each time DecideAsync returns a result and each time
    // Execute returns, for every agent, with or without a GameObject; and the lookup from an agent
    // back to the AgentBehaviour that built it.
    public sealed class DecisionFeedTests
    {
        private const string GoToTheTower = "{\"action\":\"move_to\",\"target\":\"tower\",\"statement\":\"On my way.\"}";

        private readonly List<DecisionFeedEntry> entries = new List<DecisionFeedEntry>();
        private readonly List<Object> created = new List<Object>();

        [SetUp]
        public void Subscribe()
        {
            entries.Clear();
            DecisionFeed.Recorded += entries.Add;
        }

        [TearDown]
        public void Unsubscribe()
        {
            DecisionFeed.Recorded -= entries.Add;
            foreach (var obj in created)
            {
                Object.DestroyImmediate(obj);
            }

            created.Clear();
            Fixtures.DestroyCreated();
        }

        // ---- Deciding ------------------------------------------------------------------------

        [Test]
        public async Task EachResultIsRecordedOnceWithItsAgentTimeAndSequence()
        {
            var agent = Fixtures.VillageGuard(new FakeProvider(GoToTheTower));
            var before = DateTime.UtcNow;

            var first = await Fixtures.NotCancelled(agent.DecideAsync("Go to the tower."));
            var second = await Fixtures.NotCancelled(agent.DecideAsync("Go to the tower again."));

            Assert.That(entries, Has.Count.EqualTo(2), "One entry per result, no more.");
            Assert.That(entries[0].Result, Is.SameAs(first), "The caller's own result, not a copy.");
            Assert.That(entries[1].Result, Is.SameAs(second));
            Assert.That(entries[0].Agent, Is.SameAs(agent));
            Assert.That(entries[0].Stage, Is.EqualTo(DecisionStage.Decided));
            Assert.That(entries[0].Execution, Is.Null, "Nothing was executed.");
            Assert.That(entries[0].Time, Is.InRange(before, DateTime.UtcNow));
            Assert.That(entries[0].Time.Kind, Is.EqualTo(DateTimeKind.Utc));
            Assert.That(entries[1].Sequence, Is.EqualTo(entries[0].Sequence + 1));
        }

        [Test]
        public async Task APipelineErrorIsRecorded()
        {
            var fake = new FakeProvider(GoToTheTower) { Failure = new InvalidOperationException("the backend is gone") };
            var agent = Fixtures.VillageGuard(fake);
            LogAssert.Expect(LogType.Warning, new Regex("could not decide"));

            var result = await Fixtures.NotCancelled(agent.DecideAsync("Go to the tower."));

            Assert.That(result.Decision, Is.Null, "The provider failed.");
            Assert.That(entries, Has.Count.EqualTo(1));
            Assert.That(entries[0].Result, Is.SameAs(result));
        }

        [Test]
        public async Task AFailureBeforeTheRequestIsSentIsRecorded()
        {
            var fake = new FakeProvider(GoToTheTower);
            var agent = Fixtures.VillageGuard(fake);
            agent.ObservationSources.Add(new FakeObservationSource("x") { Failure = new InvalidOperationException("the source broke") });
            LogAssert.Expect(LogType.Warning, new Regex("Preparing the request failed"));

            var result = await Fixtures.NotCancelled(agent.DecideAsync("Go to the tower."));

            Assert.That(result.Telemetry.ErrorKind, Is.EqualTo(PipelineErrorKind.RequestPreparation));
            Assert.That(fake.Requests, Is.Empty);
            Assert.That(entries, Has.Count.EqualTo(1));
            Assert.That(entries[0].Result, Is.SameAs(result));
        }

        [Test]
        public async Task AGuardStoppedDecisionIsRecorded()
        {
            var agent = Fixtures.VillageGuard(new FakeProvider(GoToTheTower));
            agent.Guards.Add(FakeGuard.Rewriting("NoTowers"));

            var result = await Fixtures.NotCancelled(agent.DecideAsync("Go to the tower."));

            Assert.That(result.Verdict.HasValue, Is.True, "The guard stopped it.");
            Assert.That(entries, Has.Count.EqualTo(1));
            Assert.That(entries[0].Result, Is.SameAs(result));
            Assert.That(entries[0].Result.ModelDecision.ActionId, Is.EqualTo("move_to"), "The model's own answer is on it.");
        }

        [Test]
        public async Task ACallTheBacklogRefusesIsRecorded()
        {
            var fake = new FakeProvider(GoToTheTower) { Hold = true };
            var agent = Fixtures.VillageGuard(fake);
            agent.Backlog = DecisionBacklog.QueueWithCap(0);

            var first = agent.DecideAsync("Go to the tower.");
            var refused = await Fixtures.NotCancelled(agent.DecideAsync("And now the bridge."));

            Assert.That(refused.Telemetry.ErrorKind, Is.EqualTo(PipelineErrorKind.BacklogFull));
            Assert.That(entries, Has.Count.EqualTo(1), "The refused call returned a result; the first has not yet.");
            Assert.That(entries[0].Result, Is.SameAs(refused));

            fake.Release();
            await Fixtures.NotCancelled(first);
            Assert.That(entries, Has.Count.EqualTo(2));
        }

        [Test]
        public async Task ACancelledCallIsNeverRecorded()
        {
            var fake = new FakeProvider(GoToTheTower) { Hold = true };
            var agent = Fixtures.VillageGuard(fake);

            using (var cts = new CancellationTokenSource())
            {
                var byCaller = agent.DecideAsync("Go to the tower.", null, cts.Token);
                cts.Cancel();
                await EndsCancelled(byCaller);
            }

            var byReset = agent.DecideAsync("Go to the bridge.");
            agent.Reset();
            await EndsCancelled(byReset);

            Assert.That(entries, Is.Empty, "A call that throws has no result to record, whoever cancelled it.");
        }

        // ---- Executing -----------------------------------------------------------------------

        [Test]
        public async Task ExecuteIsRecordedSayingWhetherTheHandlerRan()
        {
            var agent = Fixtures.VillageGuard(new FakeProvider(GoToTheTower));
            var result = await Fixtures.NotCancelled(agent.DecideAsync("Go to the tower."));

            Assert.That(agent.Execute(result), Is.True);

            Assert.That(entries, Has.Count.EqualTo(2));
            var ran = entries[1];
            Assert.That(ran.Stage, Is.EqualTo(DecisionStage.Executed));
            Assert.That(ran.Result, Is.SameAs(result));
            Assert.That(ran.Agent, Is.SameAs(agent));
            Assert.That(ran.Execution.HasValue, Is.True);
            Assert.That(ran.Execution.Value.HandlerRan, Is.True);
            Assert.That(ran.Sequence, Is.GreaterThan(entries[0].Sequence));

            // The tower is gone by the time the game acts again: the re-check refuses.
            agent.Targets.Unregister("tower");
            LogAssert.Expect(LogType.Warning, new Regex("did not execute"));

            Assert.That(agent.Execute(result), Is.False);

            Assert.That(entries, Has.Count.EqualTo(3), "Each Execute is recorded, a refusal included.");
            var refused = entries[2];
            Assert.That(refused.Stage, Is.EqualTo(DecisionStage.Executed));
            Assert.That(refused.Execution.Value.HandlerRan, Is.False);
            Assert.That(refused.Execution.Value.Status, Is.EqualTo(ExecutionStatus.Refused));
            Assert.That(refused.Execution.Value.Reason, Is.Not.Empty);
            Assert.That(refused.Result.Telemetry.Execution, Is.EqualTo(ExecutionStatus.Refused),
                "Raised after the telemetry is written, so a subscriber reads what this Execute did.");
        }

        [Test]
        public async Task AnExecuteWhoseHandlerThrowsIsNotRecorded()
        {
            var agent = Country(new DelegateActionHandler(_ => true, (_, __) => throw new InvalidOperationException("the handler broke")));
            var result = await Fixtures.NotCancelled(agent.DecideAsync("The treasury is empty."));

            Assert.Throws<InvalidOperationException>(() => agent.Execute(result));

            Assert.That(entries, Has.Count.EqualTo(1), "Only the decision: the Execute threw, as telemetry records nothing for it.");
        }

        // ---- Subscribers ---------------------------------------------------------------------

        [Test]
        public async Task ASubscriberThatThrowsIsLoggedAndTheDecisionComesBackUnchanged()
        {
            void Throws(DecisionFeedEntry entry) => throw new InvalidOperationException("the subscriber broke");
            var after = new List<DecisionFeedEntry>();
            DecisionFeed.Recorded += Throws;
            DecisionFeed.Recorded += after.Add;
            try
            {
                var agent = Fixtures.VillageGuard(new FakeProvider(GoToTheTower));
                LogAssert.Expect(LogType.Warning, new Regex("subscriber.*threw.*the subscriber broke"));

                var result = await Fixtures.NotCancelled(agent.DecideAsync("Go to the tower."));

                Assert.That(result.Decision.ActionId, Is.EqualTo("move_to"));
                Assert.That(result.Decision.TargetId, Is.EqualTo("tower"));
                Assert.That(result.Decision.Statement, Is.EqualTo("On my way."));
                Assert.That(result.Telemetry.ErrorKind, Is.Null, "A subscriber's failure is not the decision's.");
                Assert.That(after, Has.Count.EqualTo(1), "A subscriber after the one that threw still hears of it.");
                Assert.That(entries, Has.Count.EqualTo(1));
            }
            finally
            {
                DecisionFeed.Recorded -= Throws;
                DecisionFeed.Recorded -= after.Add;
            }
        }

        [Test]
        public async Task ResettingForAPlaySessionDropsSubscribersAndRestartsTheSequence()
        {
            var agent = Fixtures.VillageGuard(new FakeProvider(GoToTheTower));
            await Fixtures.NotCancelled(agent.DecideAsync("Go to the tower."));
            await Fixtures.NotCancelled(agent.DecideAsync("Go to the tower again."));
            Assert.That(entries, Has.Count.EqualTo(2));

            DecisionFeed.ResetForPlaySession();
            await Fixtures.NotCancelled(agent.DecideAsync("Once more."));
            Assert.That(entries, Has.Count.EqualTo(2), "The subscriber from before the reset is gone.");

            var next = new List<DecisionFeedEntry>();
            DecisionFeed.Recorded += next.Add;
            try
            {
                await Fixtures.NotCancelled(agent.DecideAsync("And again."));
                Assert.That(next, Has.Count.EqualTo(1));
                Assert.That(next[0].Sequence, Is.EqualTo(1), "The sequence starts again.");
            }
            finally
            {
                DecisionFeed.Recorded -= next.Add;
            }
        }

        // ---- Which agent, which GameObject ---------------------------------------------------

        [Test]
        public async Task APlainAgentWithNoGameObjectIsInTheFeed()
        {
            var agent = Country(new FakeHandler());

            var result = await Fixtures.NotCancelled(agent.DecideAsync("The treasury is empty."));

            Assert.That(entries, Has.Count.EqualTo(1));
            Assert.That(entries[0].Agent, Is.SameAs(agent));
            Assert.That(entries[0].Result, Is.SameAs(result));
            Assert.That(AgentBehaviour.Find(agent), Is.Null, "It has no GameObject.");
        }

        [Test]
        public async Task TheLookupFindsTheBehaviourThatBuiltAnAgentAndNeverBuildsOne()
        {
            var config = ScriptableObject.CreateInstance<FakeProviderConfig>();
            config.Reply = "{\"action\":\"none\",\"statement\":\"\"}";
            created.Add(config);
            var profile = Fixtures.VillageGuardProfile();
            profile.Actions.Clear();
            var obj = new GameObject("Guard");
            created.Add(obj);
            var guard = obj.AddComponent<AgentBehaviour>();
            guard.FindTargetsNearby = false;
            guard.NoticeNearby = false;
            guard.Profile = profile;
            guard.Provider = config;

            Assert.That(guard.HasAgent, Is.False, "Not built yet: EditMode runs no Awake.");
            Assert.That(guard.HasAgent, Is.False, "Asking does not build it.");

            var agent = guard.Agent;
            Assert.That(guard.HasAgent, Is.True);
            Assert.That(AgentBehaviour.Find(agent), Is.SameAs(guard));

            var result = await Fixtures.NotCancelled(guard.DecideAsync("Hello."));
            Assert.That(entries, Has.Count.EqualTo(1));
            Assert.That(AgentBehaviour.Find(entries[0].Agent), Is.SameAs(guard), "From the feed back to the GameObject.");
            Assert.That(entries[0].Result, Is.SameAs(result));

            Object.DestroyImmediate(obj);
            Assert.That(AgentBehaviour.Find(agent), Is.Null, "A destroyed component is no answer.");
            Assert.That(AgentBehaviour.Find(null), Is.Null);
        }

        // A country, with no GameObject and no asset: one action, raising the taxes, run by handler.
        private static Agent Country(IActionHandler handler)
        {
            var actions = new[] { new ActionDefinition { Id = "raise_taxes", Description = "Raise the taxes." } };
            var agent = new Agent(new AgentIdentity { Name = "France" }, actions,
                new FakeProvider("{\"action\":\"raise_taxes\",\"statement\":\"The treasury needs it.\"}"));
            agent.Bind("raise_taxes", handler);
            return agent;
        }

        // The call ended cancelled. Every way these tests cancel ends it at once.
        private static async Task EndsCancelled(Awaitable<DecisionResult> decision)
        {
            try
            {
                await decision;
            }
            catch (OperationCanceledException)
            {
                return;
            }

            Assert.Fail("The call returned a result; it should have ended cancelled.");
        }
    }
}

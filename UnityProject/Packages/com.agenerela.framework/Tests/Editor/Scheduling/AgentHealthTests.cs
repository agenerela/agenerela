using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Agenerela.Providers;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Agenerela.Tests
{
    // Agents reporting to their provider's health, and its back-off holding background requests
    // (#190, G1; DR-015 as amended). The health's clock is replaced, so the retry time passes when
    // the test says. Every await of DecideAsync goes through Fixtures.NotCancelled unless
    // cancellation is what the test is about.
    public sealed class AgentHealthTests
    {
        private const string GoToTheTower = "{\"action\":\"move_to\",\"target\":\"tower\",\"statement\":\"On my way.\"}";
        private const string ProviderFailed = "could not decide\\. The provider failed\\.";

        private static readonly DateTime Start = new DateTime(2026, 11, 5, 12, 0, 0, DateTimeKind.Utc);
        private static readonly DecideOptions Background = new DecideOptions { Priority = DecisionPriority.Background };

        private DateTime now;

        [TearDown]
        public void DestroyFixtures()
        {
            Fixtures.DestroyCreated();
        }

        private ProviderHealth HealthOf(ILLMProvider provider)
        {
            now = Start;
            var health = ProviderHealth.For(provider);
            health.Clock = () => now;
            return health;
        }

        // The agent's provider made to fail once, as a stopped server does, leaving it down.
        private async Task GoDown(FakeProvider fake, Agent agent, ProviderErrorKind kind = ProviderErrorKind.Unreachable)
        {
            var failure = fake.FailWith(kind, "Nothing answered at http://127.0.0.1:11434.");
            LogAssert.Expect(LogType.Warning, new Regex(ProviderFailed));
            await Fixtures.NotCancelled(agent.DecideAsync("Go to the tower."));
            Assert.That(ProviderHealth.For(fake).IsDown, Is.True, "Set-up: the failure marked it down.");
            fake.Failure = null;
            Assert.That(failure, Is.Not.Null);
        }

        [Test]
        public async Task AProviderFailureMarksItsHealthDownAndTheNextReplyMarksItUp()
        {
            var fake = new FakeProvider(GoToTheTower);
            var agent = Fixtures.VillageGuard(fake);
            var health = HealthOf(fake);
            var seen = new List<bool>();
            health.Changed += h => seen.Add(h.IsDown);

            await GoDown(fake, agent);

            Assert.That(health.Reason, Is.EqualTo(PipelineErrorKind.ProviderUnreachable));
            Assert.That(health.Message, Is.EqualTo("Nothing answered at http://127.0.0.1:11434."), "The provider's own words.");
            Assert.That(health.RetryAtUtc, Is.EqualTo(Start.AddSeconds(5)));

            var result = await Fixtures.NotCancelled(agent.DecideAsync("Go to the tower."));

            Assert.That(result.Decision.ActionId, Is.EqualTo("move_to"));
            Assert.That(health.IsDown, Is.False);
            Assert.That(seen, Is.EqualTo(new[] { true, false }));
        }

        [Test]
        public async Task AReplyThatCannotBeReadStillMarksItUpBecauseAModelAnswered()
        {
            var fake = new FakeProvider(GoToTheTower);
            var agent = Fixtures.VillageGuard(fake);
            var health = HealthOf(fake);
            await GoDown(fake, agent);
            fake.Reply = "Sure, I'll go to the tower!";
            LogAssert.Expect(LogType.Warning, new Regex("could not decide\\."));

            var result = await Fixtures.NotCancelled(agent.DecideAsync("Go to the tower."));

            Assert.That(result.Telemetry.ErrorKind, Is.EqualTo(PipelineErrorKind.UnreadableReply));
            Assert.That(health.IsDown, Is.False);
        }

        [Test]
        public async Task AProviderThatReturnsNoResultOrThrowsSomethingElseIsDownAsProviderFailed()
        {
            var fake = new FakeProvider(GoToTheTower) { ReturnsNoResult = true };
            var agent = Fixtures.VillageGuard(fake);
            var health = HealthOf(fake);
            LogAssert.Expect(LogType.Warning, new Regex("could not decide\\. The provider returned no result\\."));

            await Fixtures.NotCancelled(agent.DecideAsync("Go to the tower."));

            Assert.That(health.Reason, Is.EqualTo(PipelineErrorKind.ProviderFailed));
            Assert.That(health.Message, Is.EqualTo("The provider returned no result."));

            fake.ReturnsNoResult = false;
            fake.Failure = new InvalidOperationException("The socket broke.");
            LogAssert.Expect(LogType.Warning, new Regex(ProviderFailed));
            await Fixtures.NotCancelled(agent.DecideAsync("Go to the tower."));

            Assert.That(health.Reason, Is.EqualTo(PipelineErrorKind.ProviderFailed));
            Assert.That(health.Message, Is.EqualTo("InvalidOperationException: The socket broke."));
            Assert.That(health.ConsecutiveFailures, Is.EqualTo(2));
        }

        [Test]
        public async Task ACancelledRequestReportsNothing()
        {
            var fake = new FakeProvider(GoToTheTower) { Hold = true };
            var agent = Fixtures.VillageGuard(fake);
            var health = HealthOf(fake);
            int changes = 0;
            health.Changed += _ => changes++;

            using (var cts = new CancellationTokenSource())
            {
                var pending = agent.DecideAsync("Go to the tower.", null, cts.Token);
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

            Assert.That(health.IsDown, Is.False);
            Assert.That(changes, Is.Zero);
        }

        [Test]
        public async Task WithTheBackOffOffABackgroundRequestIsStillSentWhileTheProviderIsDown()
        {
            var fake = new FakeProvider(GoToTheTower);
            var agent = Fixtures.VillageGuard(fake);
            var health = HealthOf(fake);
            await GoDown(fake, agent);

            var result = await Fixtures.NotCancelled(agent.DecideAsync("A cart rolls past.", Background));

            Assert.That(fake.Requests, Has.Count.EqualTo(2), "Today's behaviour: every request is sent.");
            Assert.That(result.Decision.ActionId, Is.EqualTo("move_to"));
            Assert.That(health.IsDown, Is.False);
        }

        [Test]
        public async Task WithTheBackOffOnABackgroundRequestIsNotSentWhileTheProviderIsDown()
        {
            var fake = new FakeProvider(GoToTheTower);
            var agent = Fixtures.VillageGuard(fake);
            var health = HealthOf(fake);
            health.BackOff = true;
            await GoDown(fake, agent);
            now = Start.AddSeconds(1);

            var result = await Fixtures.NotCancelled(agent.DecideAsync("A cart rolls past.", Background));

            Assert.That(fake.Requests, Has.Count.EqualTo(1), "Only the request that failed was sent.");
            Assert.That(result.Decision, Is.Null);
            Assert.That(result.Request, Is.Null, "Nothing was sent.");
            Assert.That(result.Reply, Is.Null);
            Assert.That(result.Telemetry.Outcome, Is.EqualTo(DecisionOutcome.PipelineError));
            Assert.That(result.Telemetry.ErrorKind, Is.EqualTo(PipelineErrorKind.ProviderDown));
            Assert.That(result.Telemetry.ErrorKind.Value.IsProviderFault(), Is.True);
            Assert.That(result.Telemetry.ErrorMessage, Is.EqualTo(
                "Agent 'Village Guard' did not decide. Its provider is down (ProviderUnreachable), so background requests "
              + "are not sent for another 4 s. Nothing answered at http://127.0.0.1:11434."));
            Assert.That(result.Telemetry.Priority, Is.EqualTo(DecisionPriority.Background));
            Assert.That(result.Telemetry.ProviderName, Is.EqualTo("fake"));
            Assert.That(result.Telemetry.LatencySeconds, Is.Zero);
            Assert.That(health.ConsecutiveFailures, Is.EqualTo(1), "Holding back is not another failure.");
            Assert.That(agent.Execute(result), Is.False);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public async Task WithTheBackOffOnAPlayerFacingRequestIsAlwaysSent()
        {
            var fake = new FakeProvider(GoToTheTower);
            var agent = Fixtures.VillageGuard(fake);
            var health = HealthOf(fake);
            health.BackOff = true;
            await GoDown(fake, agent);

            var result = await Fixtures.NotCancelled(agent.DecideAsync("Go to the tower."));

            Assert.That(fake.Requests, Has.Count.EqualTo(2));
            Assert.That(result.Decision.ActionId, Is.EqualTo("move_to"));
            Assert.That(health.IsDown, Is.False, "Its reply brought the provider back for everyone.");
        }

        [Test]
        public async Task AtTheRetryTimeTheNextBackgroundRequestIsSentAndAFailureWaitsLonger()
        {
            var fake = new FakeProvider(GoToTheTower);
            var agent = Fixtures.VillageGuard(fake);
            var health = HealthOf(fake);
            health.BackOff = true;
            await GoDown(fake, agent);

            now = Start.AddSeconds(5);
            fake.FailWith(ProviderErrorKind.Unreachable, "Still nothing.");
            LogAssert.Expect(LogType.Warning, new Regex(ProviderFailed));
            var retried = await Fixtures.NotCancelled(agent.DecideAsync("A cart rolls past.", Background));

            Assert.That(fake.Requests, Has.Count.EqualTo(2), "Sent at the retry time.");
            Assert.That(retried.Telemetry.ErrorKind, Is.EqualTo(PipelineErrorKind.ProviderUnreachable));
            Assert.That(health.RetryAtUtc, Is.EqualTo(now.AddSeconds(10)), "The second failure in a row waits twice as long.");

            now = now.AddSeconds(9);
            var held = await Fixtures.NotCancelled(agent.DecideAsync("A cart rolls past.", Background));
            Assert.That(held.Telemetry.ErrorKind, Is.EqualTo(PipelineErrorKind.ProviderDown));
            Assert.That(fake.Requests, Has.Count.EqualTo(2));
        }

        [Test]
        public async Task ABackgroundRequestQueuedBeforeTheProviderWentDownIsHeldBackAtItsTurn()
        {
            var fake = new FakeProvider(GoToTheTower) { Hold = true };
            var agent = Fixtures.VillageGuard(fake);
            var health = HealthOf(fake);
            health.BackOff = true;
            var inFlight = agent.DecideAsync("Go to the tower.");
            var ambient = agent.DecideAsync("A cart rolls past.", Background);

            fake.FailWith(ProviderErrorKind.Unreachable, "Nothing answered.");
            LogAssert.Expect(LogType.Warning, new Regex(ProviderFailed));
            fake.Release();
            await Fixtures.NotCancelled(inFlight);

            Assert.That(ambient.GetAwaiter().IsCompleted, Is.True, "Held back at its turn, not sent.");
            var held = await Fixtures.NotCancelled(ambient);
            Assert.That(held.Telemetry.ErrorKind, Is.EqualTo(PipelineErrorKind.ProviderDown), "Judged when its turn came, not when it joined.");
            Assert.That(fake.Requests, Has.Count.EqualTo(1));
            Assert.That(RequestScheduler.For(fake).InFlight, Is.Zero, "Its slot was freed.");

            fake.Failure = null;
            var next = agent.DecideAsync("Go to the tower.");
            Assert.That(fake.Requests, Has.Count.EqualTo(2), "The queue moved on.");
            fake.Release();
            await Fixtures.NotCancelled(next);
        }

        [Test]
        public async Task AHeldBackRefusalLineLeavesTheDecisionStandingWithoutAWarning()
        {
            // The decision is answered while the provider is up; a second agent's failure then puts
            // it down before the refusal line's turn, which is background like its decision.
            var fake = new FakeProvider("{\"action\":\"attack_target\",\"target\":\"tower\",\"statement\":\"Charge!\"}") { Hold = true };
            var agent = Fixtures.VillageGuard(fake);
            var other = Fixtures.VillageGuard(fake);
            var health = HealthOf(fake);
            health.BackOff = true;
            agent.Guards.Add(new DelegateGuard("NoTowers", input => input.Decision.TargetId == "tower"
                ? GuardVerdict.RewriteToNone("Never attack the tower.")
                : GuardVerdict.Pass));

            var stopped = agent.DecideAsync("Attack the tower.", new DecideOptions { Priority = DecisionPriority.Background, AskForRefusalLine = true });
            var failing = other.DecideAsync("Go to the tower.");

            fake.Release();
            Assert.That(fake.Requests, Has.Count.EqualTo(2), "The other agent's player-facing request went before the refusal line.");
            fake.FailWith(ProviderErrorKind.Unreachable, "Nothing answered.");
            LogAssert.Expect(LogType.Warning, new Regex(ProviderFailed));
            fake.Release();
            await Fixtures.NotCancelled(failing);

            Assert.That(stopped.GetAwaiter().IsCompleted, Is.True, "Its refusal line was held back at its turn, not sent.");
            var result = await Fixtures.NotCancelled(stopped);

            Assert.That(fake.Requests, Has.Count.EqualTo(2), "The refusal line was never sent.");
            Assert.That(result.Decision.ActionId, Is.EqualTo(ActionRegistry.None));
            Assert.That(result.Decision.Statement, Is.Empty);
            Assert.That(result.Telemetry.RefusalLineError, Is.EqualTo(PipelineErrorKind.ProviderDown));
            Assert.That(result.Telemetry.ErrorKind, Is.Null, "The decision still stands.");
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public async Task ChangedIsRaisedOnTheMainThreadWhenTheProviderAnswersOnAnother()
        {
            Assert.That(SynchronizationContext.Current, Is.Not.Null,
                "The test needs Unity's main-thread context to prove anything.");
            int main = Thread.CurrentThread.ManagedThreadId;
            var fake = new FakeProvider(GoToTheTower) { Hold = true };
            var agent = Fixtures.VillageGuard(fake);
            var health = HealthOf(fake);
            var raisedOn = new List<int>();
            health.Changed += _ => raisedOn.Add(Thread.CurrentThread.ManagedThreadId);

            var failing = agent.DecideAsync("Go to the tower.");
            fake.FailWith(ProviderErrorKind.Unreachable, "Nothing answered.");
            LogAssert.Expect(LogType.Warning, new Regex(ProviderFailed));
            int failedOn = OnAnotherThread(fake.Release);
            await Fixtures.NotCancelled(failing);

            Assert.That(failedOn, Is.Not.EqualTo(main), "The failure must arrive off the main thread for this to test anything.");
            Assert.That(raisedOn, Is.EqualTo(new[] { main }));
        }

        // Runs action on a thread of its own and waits for it, returning that thread's id, as
        // AgentQueueTests does.
        private static int OnAnotherThread(Action action)
        {
            int id = 0;
            Exception failure = null;
            var thread = new Thread(() =>
            {
                id = Thread.CurrentThread.ManagedThreadId;
                try
                {
                    action();
                }
                catch (Exception e)
                {
                    failure = e;
                }
            });
            thread.Start();
            thread.Join();

            if (failure != null)
            {
                throw new AggregateException(failure);
            }

            return id;
        }
    }
}

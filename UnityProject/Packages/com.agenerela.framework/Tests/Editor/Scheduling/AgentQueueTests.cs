using System;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Agenerela.Providers;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Agenerela.Tests
{
    // Agents sending through their provider's queue (#89, DR-015), against a FakeProvider that
    // holds every request until the test releases it. So each test decides exactly when a slot
    // frees, and none depends on timing. As in AgentTests, every await of DecideAsync goes through
    // Fixtures.NotCancelled unless cancellation is what the test is about.
    public sealed class AgentQueueTests
    {
        private const string GoToTheTower = "{\"action\":\"move_to\",\"target\":\"tower\",\"statement\":\"On my way.\"}";

        private static readonly DecideOptions Background = new DecideOptions { Priority = DecisionPriority.Background };

        [TearDown]
        public void DestroyFixtures()
        {
            Fixtures.DestroyCreated();
        }

        [Test]
        public async Task APlayerFacingRequestIsSentBeforeABackgroundOneQueuedEarlier()
        {
            var fake = new FakeProvider(GoToTheTower) { Hold = true };
            var agent = Fixtures.VillageGuard(fake);

            var inFlight = agent.DecideAsync("The bell rings once.", Background);
            var ambient = agent.DecideAsync("A cart rolls past.", Background);
            var question = agent.DecideAsync("Go to the tower.");

            Assert.That(fake.Requests, Has.Count.EqualTo(1), "One request at a time reaches the provider.");

            fake.Release();
            Assert.That(fake.Requests, Has.Count.EqualTo(2));
            Assert.That(fake.LastRequest.Stimulus, Does.Contain("Go to the tower."), "The player's question goes next.");

            fake.Release();
            Assert.That(fake.LastRequest.Stimulus, Does.Contain("A cart rolls past."));

            fake.Release();
            var first = await Fixtures.NotCancelled(inFlight);
            var behind = await Fixtures.NotCancelled(ambient);
            var asked = await Fixtures.NotCancelled(question);

            Assert.That(first.Telemetry.Priority, Is.EqualTo(DecisionPriority.Background));
            Assert.That(first.Telemetry.QueuedAhead, Is.EqualTo(0));
            Assert.That(behind.Telemetry.Priority, Is.EqualTo(DecisionPriority.Background));
            Assert.That(behind.Telemetry.QueuedAhead, Is.EqualTo(1));
            Assert.That(asked.Telemetry.Priority, Is.EqualTo(DecisionPriority.PlayerFacing));
            Assert.That(asked.Telemetry.QueuedAhead, Is.EqualTo(1), "Only the request in flight was ahead of it.");
            Assert.That(asked.Decision.ActionId, Is.EqualTo("move_to"));
        }

        [Test]
        public async Task WithinATierTheFirstAskedIsTheFirstSent()
        {
            var fake = new FakeProvider(GoToTheTower) { Hold = true };
            var agent = Fixtures.VillageGuard(fake);
            string[] stimuli = { "First.", "Second.", "Third.", "Fourth." };
            var pending = new Awaitable<DecisionResult>[stimuli.Length];

            pending[0] = agent.DecideAsync(stimuli[0], Background);
            pending[1] = agent.DecideAsync(stimuli[1]);
            pending[2] = agent.DecideAsync(stimuli[2], Background);
            pending[3] = agent.DecideAsync(stimuli[3]);
            for (int i = 0; i < stimuli.Length; i++)
            {
                fake.Release();
            }

            for (int i = 0; i < pending.Length; i++)
            {
                await Fixtures.NotCancelled(pending[i]);
            }

            Assert.That(fake.Requests, Has.Count.EqualTo(4));
            Assert.That(fake.Requests[0].Stimulus, Does.Contain("First."), "Sent at once: nothing was queued.");
            Assert.That(fake.Requests[1].Stimulus, Does.Contain("Second."));
            Assert.That(fake.Requests[2].Stimulus, Does.Contain("Fourth."));
            Assert.That(fake.Requests[3].Stimulus, Does.Contain("Third."));
        }

        [Test]
        public async Task TwoAgentsOnOneProviderShareItsQueue()
        {
            var fake = new FakeProvider(GoToTheTower) { Hold = true };
            var guard = Fixtures.VillageGuard(fake);
            var other = Fixtures.VillageGuard(fake);

            var first = guard.DecideAsync("Go to the tower.");
            var second = other.DecideAsync("Go to the tower.");

            Assert.That(fake.Requests, Has.Count.EqualTo(1), "The second agent waits behind the first.");

            fake.Release();
            Assert.That(fake.Requests, Has.Count.EqualTo(2));
            fake.Release();

            await Fixtures.NotCancelled(first);
            var result = await Fixtures.NotCancelled(second);
            Assert.That(result.Telemetry.QueuedAhead, Is.EqualTo(1));
        }

        [Test]
        public async Task AgentsOnDifferentProvidersNeverWaitOnEachOther()
        {
            var held = new FakeProvider(GoToTheTower) { Hold = true };
            var other = new FakeProvider(GoToTheTower) { Hold = true };
            var guard = Fixtures.VillageGuard(held);
            var neighbour = Fixtures.VillageGuard(other);

            var waiting = guard.DecideAsync("Go to the tower.");
            var sent = neighbour.DecideAsync("Go to the tower.");

            Assert.That(other.Requests, Has.Count.EqualTo(1), "Another provider's request in flight is no reason to wait.");

            other.Release();
            var result = await Fixtures.NotCancelled(sent);
            Assert.That(result.Telemetry.QueuedAhead, Is.EqualTo(0));
            Assert.That(waiting.GetAwaiter().IsCompleted, Is.False, "Still with its own provider.");

            held.Release();
            await Fixtures.NotCancelled(waiting);
        }

        [Test]
        public async Task ARequestCancelledWhileQueuedEndsCancelledAndNeverReachesTheProvider()
        {
            var fake = new FakeProvider(GoToTheTower) { Hold = true };
            var agent = Fixtures.VillageGuard(fake);
            var inFlight = agent.DecideAsync("The bell rings once.");

            using (var cts = new CancellationTokenSource())
            {
                var cancelled = agent.DecideAsync("Walk away from me.", null, cts.Token);
                var behind = agent.DecideAsync("Go to the tower.");

                cts.Cancel();
                try
                {
                    await cancelled;
                    Assert.Fail("A cancelled decision has no result.");
                }
                catch (OperationCanceledException)
                {
                }

                Assert.That(fake.Requests, Has.Count.EqualTo(1), "Only the request in flight reached the provider.");
                Assert.That(RequestScheduler.For(fake).Queued, Is.EqualTo(1), "Out of the queue at once.");

                fake.Release();
                Assert.That(fake.Requests, Has.Count.EqualTo(2), "The queue moved on.");
                Assert.That(fake.LastRequest.Stimulus, Does.Contain("Go to the tower."));
                fake.Release();

                await Fixtures.NotCancelled(inFlight);
                await Fixtures.NotCancelled(behind);
                foreach (var request in fake.Requests)
                {
                    Assert.That(request.Stimulus, Does.Not.Contain("Walk away from me."));
                }
            }
        }

        [Test]
        public async Task AFailedRequestFreesTheSlotForTheNext()
        {
            var fake = new FakeProvider(GoToTheTower) { Hold = true };
            var agent = Fixtures.VillageGuard(fake);
            var failing = agent.DecideAsync("The bell rings once.");
            var behind = agent.DecideAsync("Go to the tower.");
            LogAssert.Expect(LogType.Warning, new Regex("could not decide\\. The provider failed\\."));

            fake.FailWith(ProviderErrorKind.ServerError);
            fake.Release();

            var failed = await Fixtures.NotCancelled(failing);
            Assert.That(failed.Telemetry.ErrorKind, Is.EqualTo(PipelineErrorKind.ProviderServerError));
            Assert.That(fake.Requests, Has.Count.EqualTo(2), "Its slot went to the next request; nothing was retried.");

            fake.Failure = null;
            fake.Release();
            var result = await Fixtures.NotCancelled(behind);
            Assert.That(result.Decision.ActionId, Is.EqualTo("move_to"));
            Assert.That(RequestScheduler.For(fake).InFlight, Is.EqualTo(0));
        }

        [Test]
        public async Task AProviderThatThrowsAtOnceFreesTheSlotToo()
        {
            var fake = new FakeProvider(GoToTheTower) { FailsImmediately = true };
            fake.FailWith(ProviderErrorKind.Unreachable);
            var agent = Fixtures.VillageGuard(fake);
            LogAssert.Expect(LogType.Warning, new Regex("could not decide\\. The provider failed\\."));

            var failed = await Fixtures.NotCancelled(agent.DecideAsync("The bell rings once."));
            Assert.That(failed.Telemetry.ErrorKind, Is.EqualTo(PipelineErrorKind.ProviderUnreachable));
            Assert.That(RequestScheduler.For(fake).InFlight, Is.EqualTo(0));

            fake.Failure = null;
            var result = await Fixtures.NotCancelled(agent.DecideAsync("Go to the tower."));
            Assert.That(result.Decision.ActionId, Is.EqualTo("move_to"));
        }

        [Test]
        public async Task ACancellationInFlightReachesTheProviderAndFreesTheSlot()
        {
            var fake = new FakeProvider(GoToTheTower) { Hold = true };
            var agent = Fixtures.VillageGuard(fake);

            using (var cts = new CancellationTokenSource())
            {
                var cancelled = agent.DecideAsync("The bell rings once.", null, cts.Token);
                var behind = agent.DecideAsync("Go to the tower.");
                Assert.That(fake.LastToken, Is.EqualTo(cts.Token), "Once sent, the caller's token is the provider's.");

                cts.Cancel();
                try
                {
                    await cancelled;
                    Assert.Fail("A cancelled decision has no result.");
                }
                catch (OperationCanceledException)
                {
                }

                Assert.That(fake.Requests, Has.Count.EqualTo(2), "The slot went to the next request.");
                fake.Release();
                await Fixtures.NotCancelled(behind);
            }
        }

        // ---- Threads -------------------------------------------------------------------------
        // A token can be cancelled, and a provider can answer, on any thread. The game's code after
        // `await agent.DecideAsync(...)` and every provider call still happen on the main thread.

        [Test]
        public async Task ACancellationFromAnotherThreadWhileQueuedReachesTheCallerOnTheMainThread()
        {
            Assert.That(SynchronizationContext.Current, Is.Not.Null,
                "The test needs Unity's main-thread context to prove anything.");
            int main = Thread.CurrentThread.ManagedThreadId;
            var fake = new FakeProvider(GoToTheTower) { Hold = true };
            var agent = Fixtures.VillageGuard(fake);
            var inFlight = agent.DecideAsync("The bell rings once.");

            using (var cts = new CancellationTokenSource())
            {
                // Awaiting before the cancellation, so the caller really is suspended when it comes.
                var caller = Outcome(agent.DecideAsync("Walk away from me.", null, cts.Token));
                int cancelledOn = OnAnotherThread(cts.Cancel);
                var (cancelled, resumedOn) = await caller;

                Assert.That(cancelledOn, Is.Not.EqualTo(main), "The cancellation must come from another thread for this to test anything.");
                Assert.That(cancelled, Is.True, "Cancelled while queued, the decision ends cancelled.");
                Assert.That(resumedOn, Is.EqualTo(main), "The caller carries on where it called from.");
                Assert.That(fake.Requests, Has.Count.EqualTo(1), "It never reached the provider.");
            }

            fake.Release();
            await Fixtures.NotCancelled(inFlight);
        }

        [Test]
        public async Task AQueuedRequestFreedByAnAnswerFromAnotherThreadIsSentFromTheMainThread()
        {
            // As a provider of the developer's own might answer, completing its Awaitable on a
            // worker thread. Its slot frees there, and the request queued behind it must still
            // reach the provider from the main thread: a provider built on UnityWebRequest or
            // LLMUnity can only be called there.
            Assert.That(SynchronizationContext.Current, Is.Not.Null,
                "The test needs Unity's main-thread context to prove anything.");
            int main = Thread.CurrentThread.ManagedThreadId;
            var fake = new FakeProvider(GoToTheTower) { Hold = true };
            var agent = Fixtures.VillageGuard(fake);
            var first = agent.DecideAsync("The bell rings once.");
            var caller = Outcome(agent.DecideAsync("Go to the tower."));

            int answeredOn = OnAnotherThread(fake.Release);
            await Fixtures.NotCancelled(first);

            Assert.That(answeredOn, Is.Not.EqualTo(main), "The answer must arrive off the main thread for this to test anything.");
            Assert.That(fake.Requests, Has.Count.EqualTo(2), "The slot went to the request behind it.");
            Assert.That(fake.RequestThreads[1], Is.EqualTo(main), "Sent from the main thread, not the provider's.");

            fake.Release();
            var (cancelled, resumedOn) = await caller;
            Assert.That(cancelled, Is.False);
            Assert.That(resumedOn, Is.EqualTo(main));
        }

        [Test]
        public async Task ThePriorityIsReadWhenTheCallIsMade()
        {
            var fake = new FakeProvider(GoToTheTower) { Hold = true };
            var agent = Fixtures.VillageGuard(fake);
            var options = new DecideOptions { Priority = DecisionPriority.Background };
            var inFlight = agent.DecideAsync("The bell rings once.");
            var ambient = agent.DecideAsync("A cart rolls past.", options);

            options.Priority = DecisionPriority.PlayerFacing;
            var question = agent.DecideAsync("Go to the tower.");
            fake.Release();

            Assert.That(fake.LastRequest.Stimulus, Does.Contain("Go to the tower."),
                "Changing the options after the call changes nothing, its place in the queue included.");
            fake.Release();
            fake.Release();
            await Fixtures.NotCancelled(inFlight);
            await Fixtures.NotCancelled(question);
            var result = await Fixtures.NotCancelled(ambient);
            Assert.That(result.Telemetry.Priority, Is.EqualTo(DecisionPriority.Background));
        }

        // The caller's side of a decision: whether it ended cancelled, and the thread the caller's
        // own code resumed on. Awaits the Awaitable directly, since awaiting a Task would bring the
        // test back to the main thread whatever happened inside.
        private static async Awaitable<(bool cancelled, int thread)> Outcome(Awaitable<DecisionResult> decision)
        {
            try
            {
                await decision;
                return (false, Thread.CurrentThread.ManagedThreadId);
            }
            catch (OperationCanceledException)
            {
                return (true, Thread.CurrentThread.ManagedThreadId);
            }
        }

        // Runs action on a thread of its own and waits for it, returning that thread's id. Whatever
        // the action posts back to the main thread runs once the test awaits again.
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

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
    // An agent's backlog (#190, G5; DR-015 as amended): with one set, the agent decides one call at
    // a time, the others waiting, player-facing first, within its capacity. Against a FakeProvider
    // that holds every request until the test releases it, so each test decides exactly when a
    // decision ends. Every await of DecideAsync goes through Fixtures.NotCancelled unless
    // cancellation is what the test is about.
    public sealed class AgentBacklogTests
    {
        private const string GoToTheTower = "{\"action\":\"move_to\",\"target\":\"tower\",\"statement\":\"On my way.\"}";

        private static readonly DecideOptions Background = new DecideOptions { Priority = DecisionPriority.Background };

        [TearDown]
        public void DestroyFixtures()
        {
            Fixtures.DestroyCreated();
        }

        private static (FakeProvider fake, Agent agent) Guard(DecisionBacklog backlog)
        {
            var fake = new FakeProvider(GoToTheTower) { Hold = true };
            var agent = Fixtures.VillageGuard(fake);
            agent.Backlog = backlog;
            return (fake, agent);
        }

        [Test]
        public async Task WithNoBacklogCallsDecideSideBySideAsBefore()
        {
            var (fake, agent) = Guard(null);
            Assert.That(agent.Backlog, Is.Null, "None by default.");

            var first = agent.DecideAsync("The bell rings once.");
            var second = agent.DecideAsync("Go to the tower.");

            Assert.That(RequestScheduler.For(fake).InFlight + RequestScheduler.For(fake).Queued, Is.EqualTo(2),
                "Both went to the provider's queue at once.");
            Assert.That(agent.Waiting, Is.Zero);
            Assert.That(agent.IsDeciding, Is.True);

            fake.Release();
            fake.Release();
            await Fixtures.NotCancelled(first);
            await Fixtures.NotCancelled(second);
            Assert.That(agent.IsDeciding, Is.False);
        }

        [Test]
        public async Task WithABacklogTheAgentDecidesOneCallAtATimeEachSeeingTheMemoryOfTheOneBefore()
        {
            var (fake, agent) = Guard(DecisionBacklog.QueueWithCap(3));

            var first = agent.DecideAsync("The bell rings once.");
            var second = agent.DecideAsync("Go to the tower.");

            Assert.That(fake.Requests, Has.Count.EqualTo(1));
            Assert.That(RequestScheduler.For(fake).Queued, Is.Zero, "The second waits in the agent, not in the provider's queue.");
            Assert.That(agent.Waiting, Is.EqualTo(1));
            Assert.That(agent.IsDeciding, Is.True);

            fake.Release();
            await Fixtures.NotCancelled(first);

            Assert.That(fake.Requests, Has.Count.EqualTo(2), "Its turn came when the first ended.");
            Assert.That(agent.Waiting, Is.Zero);
            Assert.That(fake.Requests[1].History, Has.Count.EqualTo(2), "Prepared after the first was remembered.");
            Assert.That(fake.Requests[1].History[0], Does.Contain("The bell rings once."));

            fake.Release();
            var result = await Fixtures.NotCancelled(second);
            Assert.That(result.Decision.ActionId, Is.EqualTo("move_to"));
            Assert.That(agent.IsDeciding, Is.False);
        }

        [Test]
        public async Task AWaitingCallIsPreparedWhenItsTurnComesSoItSeesTheStateThen()
        {
            var (fake, agent) = Guard(DecisionBacklog.LatestWins);
            var first = agent.DecideAsync("Follow me.");
            var second = agent.DecideAsync("You can go now.");

            agent.State["isFollowing"] = true;
            fake.Release();
            await Fixtures.NotCancelled(first);

            var actions = fake.Requests[1].Schema.Fields[0].AllowedValues;
            Assert.That(actions, Does.Contain("stop_following").And.Not.Contain("follow_player"),
                "Its actions come from the state when it was prepared, not when it was called.");
            fake.Release();
            await Fixtures.NotCancelled(second);
        }

        [Test]
        public async Task LatestWinsANewerCallTakesTheWaitingOnesPlaceAndTheOneUnderWayFinishes()
        {
            var (fake, agent) = Guard(DecisionBacklog.LatestWins);
            var underWay = agent.DecideAsync("The bell rings once.", Background);
            var older = agent.DecideAsync("A cart rolls past.", Background);
            var newer = agent.DecideAsync("A wolf howls.", Background);

            Assert.That(older.GetAwaiter().IsCompleted, Is.True, "Dropped at once, not when its turn would have come.");
            var dropped = await Fixtures.NotCancelled(older);

            Assert.That(dropped.Decision, Is.Null);
            Assert.That(dropped.Request, Is.Null, "Never prepared or sent.");
            Assert.That(dropped.Telemetry.Outcome, Is.EqualTo(DecisionOutcome.PipelineError));
            Assert.That(dropped.Telemetry.ErrorKind, Is.EqualTo(PipelineErrorKind.Superseded));
            Assert.That(dropped.Telemetry.ErrorKind.Value.IsProviderFault(), Is.False);
            Assert.That(dropped.Telemetry.ErrorMessage, Is.EqualTo(
                "Agent 'Village Guard' did not decide: a newer call took this one's place in its backlog (1 waiting, DropOldest when full)."));
            Assert.That(dropped.Telemetry.Priority, Is.EqualTo(DecisionPriority.Background));
            Assert.That(agent.Waiting, Is.EqualTo(1));

            fake.Release();
            var first = await Fixtures.NotCancelled(underWay);
            Assert.That(first.Decision.ActionId, Is.EqualTo("move_to"), "The decision under way is never dropped.");

            fake.Release();
            await Fixtures.NotCancelled(newer);
            Assert.That(fake.Requests, Has.Count.EqualTo(2));
            Assert.That(fake.Requests[1].Stimulus, Does.Contain("A wolf howls."));
            foreach (var request in fake.Requests)
            {
                Assert.That(request.Stimulus, Does.Not.Contain("A cart rolls past."));
            }

            Assert.That(agent.Execute(dropped), Is.False);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public async Task ABackgroundCallNeverPushesOutAWaitingPlayerFacingOne()
        {
            var (fake, agent) = Guard(DecisionBacklog.LatestWins);
            var underWay = agent.DecideAsync("The bell rings once.", Background);
            var question = agent.DecideAsync("Go to the tower.");

            var refusing = agent.DecideAsync("A cart rolls past.", Background);
            Assert.That(refusing.GetAwaiter().IsCompleted, Is.True, "Refused at once.");
            var refused = await Fixtures.NotCancelled(refusing);

            Assert.That(refused.Telemetry.ErrorKind, Is.EqualTo(PipelineErrorKind.BacklogFull));
            Assert.That(refused.Telemetry.ErrorMessage, Is.EqualTo(
                "Agent 'Village Guard' did not decide: it was deciding already, and its backlog (1 waiting, DropOldest when full) was full."));
            Assert.That(agent.Waiting, Is.EqualTo(1));

            fake.Release();
            fake.Release();
            await Fixtures.NotCancelled(underWay);
            var answered = await Fixtures.NotCancelled(question);
            Assert.That(answered.Decision.ActionId, Is.EqualTo("move_to"));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public async Task APlayerFacingCallPushesOutAWaitingBackgroundOne()
        {
            var (fake, agent) = Guard(DecisionBacklog.LatestWins);
            var underWay = agent.DecideAsync("The bell rings once.", Background);
            var ambient = agent.DecideAsync("A cart rolls past.", Background);
            var question = agent.DecideAsync("Go to the tower.");

            Assert.That(ambient.GetAwaiter().IsCompleted, Is.True, "Dropped at once.");
            var dropped = await Fixtures.NotCancelled(ambient);
            Assert.That(dropped.Telemetry.ErrorKind, Is.EqualTo(PipelineErrorKind.Superseded));

            fake.Release();
            fake.Release();
            await Fixtures.NotCancelled(underWay);
            await Fixtures.NotCancelled(question);
            Assert.That(fake.Requests[1].Stimulus, Does.Contain("Go to the tower."));
        }

        [Test]
        public async Task PlayerFacingCallsGoFirstInTheBacklogThenEachTierInTheOrderMade()
        {
            var (fake, agent) = Guard(DecisionBacklog.QueueWithCap(4));
            var pending = new List<Awaitable<DecisionResult>>
            {
                agent.DecideAsync("The bell rings once.", Background),
                agent.DecideAsync("A cart rolls past.", Background),
                agent.DecideAsync("Go to the tower."),
                agent.DecideAsync("A wolf howls.", Background),
                agent.DecideAsync("Follow me."),
            };

            Assert.That(agent.Waiting, Is.EqualTo(4));

            // Each release ends one decision, and the next call's turn comes inside it.
            for (int i = 0; i < pending.Count; i++)
            {
                fake.Release();
            }

            foreach (var decision in pending)
            {
                await Fixtures.NotCancelled(decision);
            }

            Assert.That(fake.Requests, Has.Count.EqualTo(5));
            string[] order = { "The bell rings once.", "Go to the tower.", "Follow me.", "A cart rolls past.", "A wolf howls." };
            for (int i = 0; i < order.Length; i++)
            {
                Assert.That(fake.Requests[i].Stimulus, Does.Contain(order[i]), $"Request {i}.");
            }
        }

        [Test]
        public async Task QueueWithCapRefusesTheNewestWhenFull()
        {
            var (fake, agent) = Guard(DecisionBacklog.QueueWithCap(1));
            var underWay = agent.DecideAsync("The bell rings once.");
            var waiting = agent.DecideAsync("Go to the tower.");

            var refused = agent.DecideAsync("Follow me.");
            Assert.That(refused.GetAwaiter().IsCompleted, Is.True, "Refused at once, not after the others.");
            var result = await Fixtures.NotCancelled(refused);
            Assert.That(result.Telemetry.ErrorKind, Is.EqualTo(PipelineErrorKind.BacklogFull));

            fake.Release();
            fake.Release();
            await Fixtures.NotCancelled(underWay);
            var kept = await Fixtures.NotCancelled(waiting);
            Assert.That(kept.Decision.ActionId, Is.EqualTo("move_to"), "The one already waiting keeps its place.");
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public async Task DropOldestDropsTheOldestWaitingCallWhenFull()
        {
            var (fake, agent) = Guard(DecisionBacklog.DropOldest(2));
            var underWay = agent.DecideAsync("The bell rings once.");
            var oldest = agent.DecideAsync("A cart rolls past.");
            var middle = agent.DecideAsync("Go to the tower.");
            var newest = agent.DecideAsync("Follow me.");

            Assert.That(oldest.GetAwaiter().IsCompleted, Is.True, "Dropped at once.");
            Assert.That((await Fixtures.NotCancelled(oldest)).Telemetry.ErrorKind, Is.EqualTo(PipelineErrorKind.Superseded));
            Assert.That(agent.Waiting, Is.EqualTo(2));

            fake.Release();
            fake.Release();
            fake.Release();
            await Fixtures.NotCancelled(underWay);
            await Fixtures.NotCancelled(middle);
            await Fixtures.NotCancelled(newest);
            Assert.That(fake.Requests[1].Stimulus, Does.Contain("Go to the tower."));
            Assert.That(fake.Requests[2].Stimulus, Does.Contain("Follow me."));
        }

        [TestCase(BacklogOverflow.DropOldest)]
        [TestCase(BacklogOverflow.RefuseNewest)]
        public async Task ACapacityOfZeroTurnsEveryCallAwayWhileTheAgentDecides(BacklogOverflow whenFull)
        {
            var (fake, agent) = Guard(new DecisionBacklog(0, whenFull));
            var underWay = agent.DecideAsync("The bell rings once.");

            var refusing = agent.DecideAsync("Go to the tower.");
            Assert.That(refusing.GetAwaiter().IsCompleted, Is.True, "Refused at once.");
            var refused = await Fixtures.NotCancelled(refusing);
            Assert.That(refused.Telemetry.ErrorKind, Is.EqualTo(PipelineErrorKind.BacklogFull));

            fake.Release();
            await Fixtures.NotCancelled(underWay);
            var next = agent.DecideAsync("Go to the tower.");
            Assert.That(fake.Requests, Has.Count.EqualTo(2), "Once it is free, the next call starts at once.");
            fake.Release();
            await Fixtures.NotCancelled(next);
        }

        [Test]
        public async Task ACallCancelledWhileWaitingLeavesTheBacklogAndIsNeverSent()
        {
            var (fake, agent) = Guard(DecisionBacklog.QueueWithCap(2));
            var underWay = agent.DecideAsync("The bell rings once.");

            using (var cts = new CancellationTokenSource())
            {
                var cancelled = agent.DecideAsync("Walk away from me.", null, cts.Token);
                var behind = agent.DecideAsync("Go to the tower.");

                cts.Cancel();
                Assert.That(await EndsCancelled(cancelled), Is.True);
                Assert.That(agent.Waiting, Is.EqualTo(1), "Out of the backlog at once.");

                fake.Release();
                await Fixtures.NotCancelled(underWay);
                fake.Release();
                await Fixtures.NotCancelled(behind);
            }

            Assert.That(fake.Requests, Has.Count.EqualTo(2));
            Assert.That(fake.Requests[1].Stimulus, Does.Contain("Go to the tower."));
        }

        [Test]
        public async Task CancellingTheDecisionUnderWayLetsTheNextCallRun()
        {
            var (fake, agent) = Guard(DecisionBacklog.LatestWins);
            using (var cts = new CancellationTokenSource())
            {
                var underWay = agent.DecideAsync("The bell rings once.", null, cts.Token);
                var next = agent.DecideAsync("Go to the tower.");

                cts.Cancel();
                Assert.That(await EndsCancelled(underWay), Is.True, "Its own token is how to stop it.");

                Assert.That(fake.Requests, Has.Count.EqualTo(2), "Its turn went to the next call.");
                fake.Release();
                await Fixtures.NotCancelled(next);
            }

            Assert.That(agent.IsDeciding, Is.False);
            Assert.That(agent.Waiting, Is.Zero);
        }

        [Test]
        public async Task AFailedDecisionFreesTheTurnLikeAnAnsweredOne()
        {
            var (fake, agent) = Guard(DecisionBacklog.LatestWins);
            var failing = agent.DecideAsync("The bell rings once.");
            var next = agent.DecideAsync("Go to the tower.");
            fake.FailWith(ProviderErrorKind.ServerError);
            LogAssert.Expect(LogType.Warning, new Regex("could not decide\\. The provider failed\\."));

            fake.Release();
            var failed = await Fixtures.NotCancelled(failing);

            Assert.That(failed.Telemetry.ErrorKind, Is.EqualTo(PipelineErrorKind.ProviderServerError));
            Assert.That(fake.Requests, Has.Count.EqualTo(2));
            fake.Failure = null;
            fake.Release();
            Assert.That((await Fixtures.NotCancelled(next)).Decision.ActionId, Is.EqualTo("move_to"));
        }

        [Test]
        public async Task ADecisionThatCannotBePreparedFreesTheTurnToo()
        {
            var (fake, agent) = Guard(DecisionBacklog.LatestWins);
            var source = new ThrowingOnceTargetSource();
            var underWay = agent.DecideAsync("The bell rings once.");
            agent.TargetSources.Add(source);
            var failing = agent.DecideAsync("Go to the tower.");
            var last = agent.DecideAsync("Follow me.");
            Assert.That(failing.GetAwaiter().IsCompleted, Is.True, "Set-up: dropped at once.");
            Assert.That((await Fixtures.NotCancelled(failing)).Telemetry.ErrorKind, Is.EqualTo(PipelineErrorKind.Superseded),
                "Set-up: the latest call took the waiting one's place.");

            LogAssert.Expect(LogType.Warning, new Regex("Preparing the request failed\\."));
            fake.Release();
            await Fixtures.NotCancelled(underWay);
            Assert.That(last.GetAwaiter().IsCompleted, Is.True, "Its preparation failed inside the first one's end.");
            var failed = await Fixtures.NotCancelled(last);

            Assert.That(failed.Telemetry.ErrorKind, Is.EqualTo(PipelineErrorKind.RequestPreparation));
            Assert.That(agent.IsDeciding, Is.False);
            var after = agent.DecideAsync("Go to the tower.");
            Assert.That(fake.Requests, Has.Count.EqualTo(2), "The agent is free again.");
            fake.Release();
            await Fixtures.NotCancelled(after);
        }

        [Test]
        public async Task TheTimeACallWaitedForItsTurnIsRecordedApartFromTheQueuesWait()
        {
            var (fake, agent) = Guard(DecisionBacklog.LatestWins);
            var underWay = agent.DecideAsync("The bell rings once.");
            var waiting = agent.DecideAsync("Go to the tower.");

            Thread.Sleep(60);
            fake.Release();
            var first = await Fixtures.NotCancelled(underWay);
            fake.Release();
            var second = await Fixtures.NotCancelled(waiting);

            Assert.That(first.Telemetry.BacklogWaitSeconds, Is.Zero, "It never waited.");
            Assert.That(second.Telemetry.BacklogWaitSeconds, Is.GreaterThanOrEqualTo(0.05f));
            Assert.That(second.Telemetry.QueueWaitSeconds, Is.LessThan(0.05f), "The provider's queue was empty by then.");
        }

        [Test]
        public async Task ASmallerBacklogSetBetweenCallsHoldsTheNextCallToItsOwnCapacity()
        {
            // Codex review, F2: one drop is not always enough once the backlog has been made smaller.
            var (fake, agent) = Guard(DecisionBacklog.QueueWithCap(3));
            var underWay = agent.DecideAsync("The bell rings once.");
            var waiting = new[]
            {
                agent.DecideAsync("A cart rolls past."),
                agent.DecideAsync("A wolf howls."),
                agent.DecideAsync("Go to the tower."),
            };

            agent.Backlog = DecisionBacklog.DropOldest(0);
            var turnedAway = agent.DecideAsync("Follow me.");
            Assert.That(turnedAway.GetAwaiter().IsCompleted, Is.True, "Room for none while it decides.");
            Assert.That((await Fixtures.NotCancelled(turnedAway)).Telemetry.ErrorKind, Is.EqualTo(PipelineErrorKind.BacklogFull));
            Assert.That(agent.Waiting, Is.EqualTo(3), "The calls already waiting keep their places.");

            agent.Backlog = DecisionBacklog.LatestWins;
            var latest = agent.DecideAsync("Stand your ground.");
            foreach (var older in waiting)
            {
                Assert.That(older.GetAwaiter().IsCompleted, Is.True, "Every older call made way at once.");
                Assert.That((await Fixtures.NotCancelled(older)).Telemetry.ErrorKind, Is.EqualTo(PipelineErrorKind.Superseded));
            }

            Assert.That(agent.Waiting, Is.EqualTo(1));
            fake.Release();
            fake.Release();
            await Fixtures.NotCancelled(underWay);
            await Fixtures.NotCancelled(latest);
            Assert.That(fake.Requests, Has.Count.EqualTo(2));
            Assert.That(fake.Requests[1].Stimulus, Does.Contain("Stand your ground."));
        }

        [Test]
        public async Task TheBacklogIsReadWhenEachCallIsMadeAndCountsOnlyCallsMadeUnderIt()
        {
            var (fake, agent) = Guard(null);
            var before = agent.DecideAsync("The bell rings once.");

            agent.Backlog = DecisionBacklog.QueueWithCap(0);
            var under = agent.DecideAsync("Go to the tower.");

            Assert.That(RequestScheduler.For(fake).Queued, Is.EqualTo(1),
                "The call made before it was set is not counted, so this one went straight to the provider's queue.");
            fake.Release();
            fake.Release();
            await Fixtures.NotCancelled(before);
            await Fixtures.NotCancelled(under);
        }

        // ---- Races ----------------------------------------------------------------------------
        // A newer call and a cancellation can reach one waiting call in either order. Exactly one
        // of them wins, under the queue's lock, and the call ends the way the winner says.

        [Test]
        public async Task ACallCancelledBeforeANewerOneArrivesIsCancelledAndTheNewerOneTakesNoOtherPlace()
        {
            var (fake, agent) = Guard(DecisionBacklog.LatestWins);
            var underWay = agent.DecideAsync("The bell rings once.");
            using (var cts = new CancellationTokenSource())
            {
                var older = agent.DecideAsync("A cart rolls past.", null, cts.Token);
                OnAnotherThread(cts.Cancel);
                var newer = agent.DecideAsync("A wolf howls.");

                Assert.That(await EndsCancelled(older), Is.True, "Cancelled first, so cancelled, never superseded.");
                Assert.That(agent.Waiting, Is.EqualTo(1), "The newer one waits in the room the cancellation left.");

                fake.Release();
                fake.Release();
                await Fixtures.NotCancelled(underWay);
                await Fixtures.NotCancelled(newer);
            }
        }

        [Test]
        public async Task ACallSupersededBeforeItsTokenIsCancelledStaysSuperseded()
        {
            var (fake, agent) = Guard(DecisionBacklog.LatestWins);
            var underWay = agent.DecideAsync("The bell rings once.");
            using (var cts = new CancellationTokenSource())
            {
                var older = agent.DecideAsync("A cart rolls past.", null, cts.Token);
                var newer = agent.DecideAsync("A wolf howls.");
                OnAnotherThread(cts.Cancel);

                Assert.That(older.GetAwaiter().IsCompleted, Is.True, "Dropped at once.");
                var dropped = await Fixtures.NotCancelled(older);
                Assert.That(dropped.Telemetry.ErrorKind, Is.EqualTo(PipelineErrorKind.Superseded),
                    "Already out of the backlog when the token fired, so the cancellation changes nothing.");
                Assert.That(agent.Waiting, Is.EqualTo(1));

                fake.Release();
                fake.Release();
                await Fixtures.NotCancelled(underWay);
                await Fixtures.NotCancelled(newer);
            }
        }

        [Test]
        public async Task ACancellationFromAnotherThreadWhileWaitingReachesTheCallerOnTheMainThread()
        {
            Assert.That(SynchronizationContext.Current, Is.Not.Null,
                "The test needs Unity's main-thread context to prove anything.");
            int main = Thread.CurrentThread.ManagedThreadId;
            var (fake, agent) = Guard(DecisionBacklog.LatestWins);
            var underWay = agent.DecideAsync("The bell rings once.");

            using (var cts = new CancellationTokenSource())
            {
                var caller = Outcome(agent.DecideAsync("Walk away from me.", null, cts.Token));
                int cancelledOn = OnAnotherThread(cts.Cancel);
                var (cancelled, resumedOn) = await caller;

                Assert.That(cancelledOn, Is.Not.EqualTo(main), "The cancellation must come from another thread for this to test anything.");
                Assert.That(cancelled, Is.True);
                Assert.That(resumedOn, Is.EqualTo(main), "The caller carries on where it called from.");
            }

            fake.Release();
            await Fixtures.NotCancelled(underWay);
        }

        [Test]
        public async Task ATurnFreedByAnAnswerFromAnotherThreadIsPreparedAndSentOnTheMainThread()
        {
            Assert.That(SynchronizationContext.Current, Is.Not.Null,
                "The test needs Unity's main-thread context to prove anything.");
            int main = Thread.CurrentThread.ManagedThreadId;
            var (fake, agent) = Guard(DecisionBacklog.LatestWins);
            var source = new ThreadRecordingTargetSource();
            agent.TargetSources.Add(source);
            var underWay = agent.DecideAsync("The bell rings once.");
            var waiting = agent.DecideAsync("Go to the tower.");

            int answeredOn = OnAnotherThread(fake.Release);
            await Fixtures.NotCancelled(underWay);
            for (int i = 0; i < 100 && fake.Requests.Count < 2; i++)
            {
                await Task.Yield();
            }

            Assert.That(answeredOn, Is.Not.EqualTo(main), "The answer must arrive off the main thread for this to test anything.");
            Assert.That(fake.Requests, Has.Count.EqualTo(2));
            Assert.That(source.CollectedOn, Is.EqualTo(new[] { main, main }), "Both prepared on the main thread.");
            Assert.That(fake.RequestThreads[1], Is.EqualTo(main));
            fake.Release();
            await Fixtures.NotCancelled(waiting);
        }

        // ---- The policy itself ----------------------------------------------------------------

        [Test]
        public void ThePresetsAreTheThreePoliciesTheDemosNeeded()
        {
            Assert.That(DecisionBacklog.LatestWins.Capacity, Is.EqualTo(1));
            Assert.That(DecisionBacklog.LatestWins.WhenFull, Is.EqualTo(BacklogOverflow.DropOldest));
            Assert.That(DecisionBacklog.DropOldest(3).Capacity, Is.EqualTo(3));
            Assert.That(DecisionBacklog.DropOldest(3).WhenFull, Is.EqualTo(BacklogOverflow.DropOldest));
            Assert.That(DecisionBacklog.QueueWithCap(2).Capacity, Is.EqualTo(2));
            Assert.That(DecisionBacklog.QueueWithCap(2).WhenFull, Is.EqualTo(BacklogOverflow.RefuseNewest));
        }

        [Test]
        public void ABacklogOutOfRangeIsRefused()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new DecisionBacklog(-1, BacklogOverflow.DropOldest));
            Assert.Throws<ArgumentOutOfRangeException>(() => DecisionBacklog.QueueWithCap(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new DecisionBacklog(1, (BacklogOverflow)7));
        }

        // ---- Helpers --------------------------------------------------------------------------

        private static async Task<bool> EndsCancelled(Awaitable<DecisionResult> decision)
        {
            try
            {
                await decision;
                return false;
            }
            catch (OperationCanceledException)
            {
                return true;
            }
        }

        // Awaits the Awaitable directly, as AgentQueueTests does, so the thread is the caller's own.
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

        // Fails the first decision it is asked for, then offers nothing.
        private sealed class ThrowingOnceTargetSource : ITargetSource
        {
            private bool thrown;

            public void Collect(AgentContext ctx, TargetRegistry targets)
            {
                if (!thrown)
                {
                    thrown = true;
                    throw new InvalidOperationException("The scene is not loaded yet.");
                }
            }
        }

        // Records the thread each decision is prepared on, and offers nothing.
        private sealed class ThreadRecordingTargetSource : ITargetSource
        {
            public readonly List<int> CollectedOn = new List<int>();

            public void Collect(AgentContext ctx, TargetRegistry targets)
            {
                CollectedOn.Add(Thread.CurrentThread.ManagedThreadId);
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

namespace Agenerela.Tests
{
    // The scheduler on its own (#89): which ticket gets the next slot, what it records, and what
    // cancelling and disposing do. No provider is involved, so every test controls the order of
    // events by hand, never by timing.
    public sealed class RequestSchedulerTests
    {
        private const DecisionPriority PlayerFacing = DecisionPriority.PlayerFacing;
        private const DecisionPriority Background = DecisionPriority.Background;

        // ---- One queue per provider instance (DR-013) ---------------------------------------

        [Test]
        public void OneProviderInstanceHasOneScheduler()
        {
            var provider = new FakeProvider();

            Assert.That(RequestScheduler.For(provider), Is.SameAs(RequestScheduler.For(provider)));
        }

        [Test]
        public void TwoProviderInstancesHaveTwoSchedulers()
        {
            // Same name, same settings: still two backends' worth of requests.
            Assert.That(RequestScheduler.For(new FakeProvider()), Is.Not.SameAs(RequestScheduler.For(new FakeProvider())));
        }

        [Test]
        public void ANullProviderHasNoScheduler()
        {
            Assert.Throws<ArgumentNullException>(() => RequestScheduler.For(null));
        }

        [Test]
        public void OneRequestIsInFlightByDefault()
        {
            Assert.That(new RequestScheduler().MaxInFlight, Is.EqualTo(1));
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void MaxInFlightBelowOneThrows(int value)
        {
            var scheduler = new RequestScheduler();

            Assert.Throws<ArgumentOutOfRangeException>(() => scheduler.MaxInFlight = value);
            Assert.That(scheduler.MaxInFlight, Is.EqualTo(1));
        }

        // ---- Order ---------------------------------------------------------------------------

        [Test]
        public void AnIdleSchedulerSendsAtOnce()
        {
            var scheduler = new RequestScheduler();

            var ticket = scheduler.Enqueue(Background, CancellationToken.None);

            Assert.That(ticket.IsReady, Is.True);
            Assert.That(ticket.Ready.GetAwaiter().IsCompleted, Is.True, "Nothing to wait for, so the caller does not.");
            Assert.That(ticket.QueuedAhead, Is.EqualTo(0));
            Assert.That(scheduler.InFlight, Is.EqualTo(1));
        }

        [Test]
        public void ASecondRequestWaitsForTheFirstToFinish()
        {
            var scheduler = new RequestScheduler();
            var first = scheduler.Enqueue(PlayerFacing, CancellationToken.None);

            var second = scheduler.Enqueue(PlayerFacing, CancellationToken.None);

            Assert.That(second.IsQueued, Is.True);
            Assert.That(second.Ready.GetAwaiter().IsCompleted, Is.False);
            Assert.That(second.QueuedAhead, Is.EqualTo(1), "The one in flight is ahead of it.");

            first.Dispose();

            Assert.That(second.IsReady, Is.True);
            Assert.That(scheduler.InFlight, Is.EqualTo(1));
        }

        [Test]
        public void PlayerFacingGoesBeforeBackgroundQueuedEarlier()
        {
            var scheduler = new RequestScheduler();
            var inFlight = scheduler.Enqueue(Background, CancellationToken.None);
            var ambient = scheduler.Enqueue(Background, CancellationToken.None);
            var question = scheduler.Enqueue(PlayerFacing, CancellationToken.None);

            inFlight.Dispose();

            Assert.That(question.IsReady, Is.True, "The player's question jumps the queue.");
            Assert.That(ambient.IsQueued, Is.True);

            question.Dispose();

            Assert.That(ambient.IsReady, Is.True);
        }

        [Test]
        public void ARequestAlreadySentIsNeverAbortedForAPlayerFacingOne()
        {
            var scheduler = new RequestScheduler();
            var ambient = scheduler.Enqueue(Background, CancellationToken.None);

            var question = scheduler.Enqueue(PlayerFacing, CancellationToken.None);

            Assert.That(ambient.IsReady, Is.True, "Preempting means jumping the queue, not taking a slot back.");
            Assert.That(question.IsQueued, Is.True);
        }

        [Test]
        public void FirstComeFirstServedWithinEachTier()
        {
            var scheduler = new RequestScheduler();
            var inFlight = scheduler.Enqueue(PlayerFacing, CancellationToken.None);
            var b1 = scheduler.Enqueue(Background, CancellationToken.None);
            var p1 = scheduler.Enqueue(PlayerFacing, CancellationToken.None);
            var b2 = scheduler.Enqueue(Background, CancellationToken.None);
            var p2 = scheduler.Enqueue(PlayerFacing, CancellationToken.None);

            var sent = new List<string>();
            var tickets = new Dictionary<string, RequestScheduler.Ticket> { { "b1", b1 }, { "p1", p1 }, { "b2", b2 }, { "p2", p2 } };
            var current = inFlight;
            for (int i = 0; i < 4; i++)
            {
                current.Dispose();
                foreach (var pair in tickets)
                {
                    if (pair.Value.IsReady)
                    {
                        sent.Add(pair.Key);
                        current = pair.Value;
                    }
                }
            }

            Assert.That(sent, Is.EqualTo(new[] { "p1", "p2", "b1", "b2" }));
        }

        [Test]
        public void QueuedAheadCountsWhatGoesBeforeItWhenItJoins()
        {
            var scheduler = new RequestScheduler();
            var inFlight = scheduler.Enqueue(Background, CancellationToken.None);
            var b1 = scheduler.Enqueue(Background, CancellationToken.None);
            var p1 = scheduler.Enqueue(PlayerFacing, CancellationToken.None);
            var b2 = scheduler.Enqueue(Background, CancellationToken.None);
            var p2 = scheduler.Enqueue(PlayerFacing, CancellationToken.None);

            Assert.That(inFlight.QueuedAhead, Is.EqualTo(0));
            Assert.That(b1.QueuedAhead, Is.EqualTo(1), "The one in flight.");
            Assert.That(p1.QueuedAhead, Is.EqualTo(1), "The one in flight; b1 goes after it.");
            Assert.That(b2.QueuedAhead, Is.EqualTo(3), "In flight, b1 and p1.");
            Assert.That(p2.QueuedAhead, Is.EqualTo(2), "In flight and p1.");
            Assert.That(b1.QueuedAhead, Is.EqualTo(1), "Fixed when it joined: p1 and p2 jumping ahead later do not change it.");
        }

        // ---- Slots ---------------------------------------------------------------------------

        [Test]
        public void MaxInFlightTwoSendsTwoAtOnce()
        {
            var scheduler = new RequestScheduler { MaxInFlight = 2 };

            var first = scheduler.Enqueue(PlayerFacing, CancellationToken.None);
            var second = scheduler.Enqueue(PlayerFacing, CancellationToken.None);
            var third = scheduler.Enqueue(PlayerFacing, CancellationToken.None);

            Assert.That(first.IsReady && second.IsReady, Is.True);
            Assert.That(third.IsQueued, Is.True);
            Assert.That(third.QueuedAhead, Is.EqualTo(2));
        }

        [Test]
        public void RaisingMaxInFlightSendsWhatIsWaiting()
        {
            var scheduler = new RequestScheduler();
            scheduler.Enqueue(PlayerFacing, CancellationToken.None);
            var waiting = scheduler.Enqueue(PlayerFacing, CancellationToken.None);

            scheduler.MaxInFlight = 2;

            Assert.That(waiting.IsReady, Is.True);
        }

        [Test]
        public void LoweringMaxInFlightLetsTheRequestsInFlightFinish()
        {
            var scheduler = new RequestScheduler { MaxInFlight = 2 };
            var first = scheduler.Enqueue(PlayerFacing, CancellationToken.None);
            var second = scheduler.Enqueue(PlayerFacing, CancellationToken.None);
            var third = scheduler.Enqueue(PlayerFacing, CancellationToken.None);

            scheduler.MaxInFlight = 1;
            first.Dispose();

            Assert.That(second.IsReady, Is.True, "Nothing in flight is taken back.");
            Assert.That(third.IsQueued, Is.True, "One is still in flight, which is the new limit.");

            second.Dispose();

            Assert.That(third.IsReady, Is.True);
        }

        [Test]
        public void DisposingTwiceFreesOneSlot()
        {
            var scheduler = new RequestScheduler();
            var first = scheduler.Enqueue(PlayerFacing, CancellationToken.None);
            var second = scheduler.Enqueue(PlayerFacing, CancellationToken.None);
            var third = scheduler.Enqueue(PlayerFacing, CancellationToken.None);

            first.Dispose();
            first.Dispose();

            Assert.That(second.IsReady, Is.True);
            Assert.That(third.IsQueued, Is.True, "A second Dispose must not free a slot it no longer holds.");
            Assert.That(scheduler.InFlight, Is.EqualTo(1));
        }

        [Test]
        public async Task AWaiterThatFinishesAtOnceHandsOnWithoutRecursing()
        {
            // Each waiter finishes as soon as its turn comes, which frees its slot from inside the
            // call that gave it. A thousand of them would overflow the stack if that call recursed.
            const int Count = 1000;
            var scheduler = new RequestScheduler();
            var head = scheduler.Enqueue(PlayerFacing, CancellationToken.None);
            var order = new List<int>();
            var waiters = new List<Awaitable>();
            for (int i = 0; i < Count; i++)
            {
                waiters.Add(TakeTurn(scheduler.Enqueue(PlayerFacing, CancellationToken.None), i, order));
            }

            head.Dispose();

            // Caught, since the test framework counts an async test whose Task ends cancelled as
            // passed, which would skip the assertions below unseen (see Fixtures.NotCancelled).
            try
            {
                foreach (var waiter in waiters)
                {
                    await waiter;
                }
            }
            catch (OperationCanceledException e)
            {
                Assert.Fail($"A waiter ended cancelled, though nothing cancelled it: {e.Message}");
            }

            Assert.That(order, Is.EqualTo(System.Linq.Enumerable.Range(0, Count)));
            Assert.That(scheduler.InFlight, Is.EqualTo(0));
            Assert.That(scheduler.Queued, Is.EqualTo(0));
        }

        // ---- Cancellation --------------------------------------------------------------------

        [Test]
        public void AnAlreadyCancelledTokenQueuesNothing()
        {
            var scheduler = new RequestScheduler();
            scheduler.Enqueue(PlayerFacing, CancellationToken.None);

            Assert.Throws<OperationCanceledException>(() => scheduler.Enqueue(PlayerFacing, new CancellationToken(true)));
            Assert.That(scheduler.Queued, Is.EqualTo(0));
        }

        [Test]
        public async Task ARequestCancelledWhileQueuedLeavesAtOnceAndTheQueueMovesOn()
        {
            var scheduler = new RequestScheduler();
            var inFlight = scheduler.Enqueue(PlayerFacing, CancellationToken.None);

            using (var cts = new CancellationTokenSource())
            {
                var cancelled = scheduler.Enqueue(PlayerFacing, cts.Token);
                var behind = scheduler.Enqueue(PlayerFacing, CancellationToken.None);

                cts.Cancel();

                Assert.That(cancelled.IsQueued || cancelled.IsReady, Is.False);
                Assert.That(scheduler.Queued, Is.EqualTo(1), "Out of the queue at once, not when its turn would have come.");
                try
                {
                    await cancelled.Ready;
                    Assert.Fail("A request cancelled while queued never gets a turn.");
                }
                catch (OperationCanceledException e)
                {
                    Assert.That(e.CancellationToken, Is.EqualTo(cts.Token));
                }

                Assert.That(scheduler.InFlight, Is.EqualTo(1), "Leaving the queue frees no slot: the one in flight still holds it.");
                inFlight.Dispose();

                Assert.That(behind.IsReady, Is.True);
                cancelled.Dispose();
                Assert.That(scheduler.InFlight, Is.EqualTo(1), "Disposing a cancelled ticket frees nothing.");
            }
        }

        [Test]
        public void CancellingAfterItsTurnCameLeavesTheSlotToTheCaller()
        {
            var scheduler = new RequestScheduler();

            using (var cts = new CancellationTokenSource())
            {
                var ticket = scheduler.Enqueue(PlayerFacing, cts.Token);
                var behind = scheduler.Enqueue(PlayerFacing, CancellationToken.None);

                cts.Cancel();

                Assert.That(ticket.IsReady, Is.True, "Once sent, the token is the provider's to honour.");
                Assert.That(behind.IsQueued, Is.True);

                ticket.Dispose();

                Assert.That(behind.IsReady, Is.True);
            }
        }

        [Test]
        public async Task DisposingAQueuedTicketWithdrawsIt()
        {
            var scheduler = new RequestScheduler();
            var inFlight = scheduler.Enqueue(PlayerFacing, CancellationToken.None);
            var withdrawn = scheduler.Enqueue(PlayerFacing, CancellationToken.None);
            var behind = scheduler.Enqueue(PlayerFacing, CancellationToken.None);

            withdrawn.Dispose();
            inFlight.Dispose();

            Assert.That(behind.IsReady, Is.True);
            Assert.That(scheduler.InFlight, Is.EqualTo(1));
            try
            {
                await withdrawn.Ready;
                Assert.Fail("A withdrawn request never gets a turn.");
            }
            catch (OperationCanceledException)
            {
            }
        }

        private static async Awaitable TakeTurn(RequestScheduler.Ticket ticket, int index, List<int> order)
        {
            using (ticket)
            {
                await ticket.Ready;
                order.Add(index);
            }
        }
    }
}

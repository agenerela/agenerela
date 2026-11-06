using System;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace Agenerela.Tests
{
    // The scheduler's capacity-aware Enqueue on its own, which an agent's backlog is built on
    // (#190, G5). AgentBacklogTests covers the same rules through an agent.
    public sealed class RequestSchedulerOverflowTests
    {
        private const DecisionPriority PlayerFacing = DecisionPriority.PlayerFacing;
        private const DecisionPriority Background = DecisionPriority.Background;

        [Test]
        public void TheCapacityCountsWaitingRequestsNotTheOnesInFlight()
        {
            var scheduler = new RequestScheduler { MaxInFlight = 2 };

            var first = scheduler.Enqueue(PlayerFacing, CancellationToken.None, 0, BacklogOverflow.RefuseNewest);
            var second = scheduler.Enqueue(PlayerFacing, CancellationToken.None, 0, BacklogOverflow.RefuseNewest);
            var third = scheduler.Enqueue(PlayerFacing, CancellationToken.None, 0, BacklogOverflow.RefuseNewest);

            Assert.That(first.IsReady && second.IsReady, Is.True, "Two free slots, so neither waits.");
            Assert.That(third, Is.Null, "It would have had to wait, and none may.");
        }

        [Test]
        public void ARefusedRequestLeavesTheQueueAsItWas()
        {
            var scheduler = new RequestScheduler();
            var inFlight = scheduler.Enqueue(PlayerFacing, CancellationToken.None);
            var waiting = scheduler.Enqueue(PlayerFacing, CancellationToken.None);

            using (var cts = new CancellationTokenSource())
            {
                Assert.That(scheduler.Enqueue(PlayerFacing, cts.Token, 1, BacklogOverflow.RefuseNewest), Is.Null);
                Assert.That(scheduler.Queued, Is.EqualTo(1));
                Assert.DoesNotThrow(cts.Cancel, "Nothing was registered on its token.");
            }

            inFlight.Dispose();
            Assert.That(waiting.IsReady, Is.True);
        }

        [Test]
        public async Task ADroppedRequestsTurnThrowsSupersededAndDisposingItFreesNothing()
        {
            var scheduler = new RequestScheduler();
            var inFlight = scheduler.Enqueue(Background, CancellationToken.None);
            var older = scheduler.Enqueue(Background, CancellationToken.None, 1, BacklogOverflow.DropOldest);

            var newer = scheduler.Enqueue(Background, CancellationToken.None, 1, BacklogOverflow.DropOldest);

            Assert.That(older.IsQueued, Is.False);
            Assert.That(newer.IsQueued, Is.True);
            try
            {
                await older.Ready;
                Assert.Fail("A dropped request never gets its turn.");
            }
            catch (SupersededException)
            {
            }
            catch (OperationCanceledException)
            {
                Assert.Fail("Dropped is not cancelled: nobody cancelled it.");
            }

            older.Dispose();
            older.Dispose();
            Assert.That(scheduler.InFlight, Is.EqualTo(1), "It held no slot to give back.");
            Assert.That(scheduler.Queued, Is.EqualTo(1));

            inFlight.Dispose();
            Assert.That(newer.IsReady, Is.True);
        }

        [Test]
        public void DroppingTakesABackgroundRequestBeforeAnyPlayerFacingOne()
        {
            var scheduler = new RequestScheduler();
            var inFlight = scheduler.Enqueue(PlayerFacing, CancellationToken.None);
            var question = scheduler.Enqueue(PlayerFacing, CancellationToken.None, 2, BacklogOverflow.DropOldest);
            var ambient = scheduler.Enqueue(Background, CancellationToken.None, 2, BacklogOverflow.DropOldest);

            var another = scheduler.Enqueue(PlayerFacing, CancellationToken.None, 2, BacklogOverflow.DropOldest);

            Assert.That(ambient.IsQueued, Is.False, "The background one made way, though the question is older.");
            Assert.That(question.IsQueued && another.IsQueued, Is.True);
            Assert.That(scheduler.Enqueue(Background, CancellationToken.None, 2, BacklogOverflow.DropOldest), Is.Null,
                "A background request never pushes out a player-facing one.");
            inFlight.Dispose();
        }

        [Test]
        public void AnAlreadyCancelledTokenThrowsBeforeAnythingIsDropped()
        {
            var scheduler = new RequestScheduler();
            scheduler.Enqueue(PlayerFacing, CancellationToken.None);
            var waiting = scheduler.Enqueue(PlayerFacing, CancellationToken.None, 1, BacklogOverflow.DropOldest);

            Assert.Throws<OperationCanceledException>(() =>
                scheduler.Enqueue(PlayerFacing, new CancellationToken(true), 1, BacklogOverflow.DropOldest));
            Assert.That(waiting.IsQueued, Is.True);
        }
    }
}

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
    // Starting an agent afresh without destroying it (#190, G12). Agent.Reset cancels this agent's
    // decisions wherever they wait, and no other agent's. It drops the decisions held for OnExecute
    // memory, and clears memory through IClearableMemory. Against a FakeProvider that holds every
    // request until the test releases it, so each test decides when anything ends. Every await of
    // DecideAsync goes through Fixtures.NotCancelled, except where cancellation is what the test is
    // about: those go through Cancelled below, which first checks the call ended at once, so a
    // reset that cancels nothing fails the test rather than hanging it.
    public sealed class AgentResetTests
    {
        private const string GoToTheTower = "{\"action\":\"move_to\",\"target\":\"tower\",\"statement\":\"On my way.\"}";

        private static readonly DecideOptions Background = new DecideOptions { Priority = DecisionPriority.Background };

        [TearDown]
        public void DestroyFixtures()
        {
            Fixtures.DestroyCreated();
        }

        // ---- Cancelling ----------------------------------------------------------------------

        [Test]
        public async Task ADecisionWithTheProviderEndsCancelledAndTheProviderIsToldToStop()
        {
            // Without a caller's token the call uses the reset's own; with one, a token linked to both.
            foreach (bool callerToken in new[] { false, true })
            {
                var fake = Held();
                var agent = Fixtures.VillageGuard(fake);
                using (var cts = new CancellationTokenSource())
                {
                    var inFlight = agent.DecideAsync("Go to the tower.", null, callerToken ? cts.Token : default);
                    Assert.That(fake.HeldCount, Is.EqualTo(1));
                    Assert.That(fake.LastToken.IsCancellationRequested, Is.False);

                    agent.Reset();

                    await Cancelled(inFlight);
                    Assert.That(fake.LastToken.IsCancellationRequested, Is.True, "The provider's request was stopped.");
                    Assert.That(fake.HeldCount, Is.Zero);
                    Assert.That(RequestScheduler.For(fake).InFlight, Is.Zero, "Its slot is free.");
                    Assert.That(agent.IsDeciding, Is.False);
                    Assert.That(cts.IsCancellationRequested, Is.False, "The caller's own token is not touched.");
                }
            }
        }

        [Test]
        public async Task ACallWaitingInTheProvidersQueueEndsCancelledAndIsNeverSent()
        {
            foreach (bool callerToken in new[] { false, true })
            {
                var fake = Held();
                var agent = Fixtures.VillageGuard(fake);
                using (var cts = new CancellationTokenSource())
                {
                    var token = callerToken ? cts.Token : default;
                    var inFlight = agent.DecideAsync("The bell rings once.", null, token);
                    var queued = agent.DecideAsync("Walk away from me.", null, token);
                    Assert.That(RequestScheduler.For(fake).Queued, Is.EqualTo(1));

                    agent.Reset();

                    await Cancelled(inFlight);
                    await Cancelled(queued);
                    Assert.That(fake.Requests, Has.Count.EqualTo(1), "Only the call already with the provider was ever sent.");
                    Assert.That(RequestScheduler.For(fake).Queued, Is.Zero);
                    Assert.That(RequestScheduler.For(fake).InFlight, Is.Zero);
                }
            }
        }

        [Test]
        public async Task ACallWaitingInTheBacklogEndsCancelledAndIsNeverSent()
        {
            foreach (bool callerToken in new[] { false, true })
            {
                var fake = Held();
                var agent = Fixtures.VillageGuard(fake);
                agent.Backlog = DecisionBacklog.QueueWithCap(3);
                using (var cts = new CancellationTokenSource())
                {
                    var token = callerToken ? cts.Token : default;
                    var deciding = agent.DecideAsync("The bell rings once.", null, token);
                    var waiting = agent.DecideAsync("Walk away from me.", null, token);
                    Assert.That(agent.Waiting, Is.EqualTo(1));

                    agent.Reset();

                    await Cancelled(deciding);
                    await Cancelled(waiting);
                    Assert.That(agent.Waiting, Is.Zero);
                    Assert.That(agent.IsDeciding, Is.False);
                    Assert.That(fake.Requests, Has.Count.EqualTo(1), "The waiting call was never prepared or sent.");
                }
            }
        }

        [Test]
        public async Task ACallHandedATurnWhileTheResetRunsIsStillNeverSent()
        {
            // The guard's player-facing call is newer than its background one but is sent first, so
            // stopping it frees the slot for the older call while the reset is still cancelling.
            // That call must see the reset at its turn, even before its own token has heard of it.
            var fake = Held();
            var other = Fixtures.VillageGuard(fake);
            var guard = Fixtures.VillageGuard(fake);
            using (var cts = new CancellationTokenSource())
            {
                var othersCall = other.DecideAsync("The bell rings once.");
                var older = guard.DecideAsync("A cart rolls past.", Background, cts.Token);
                var newer = guard.DecideAsync("Walk away from me.", null, cts.Token);
                fake.Release();
                Assert.That(fake.LastRequest.Stimulus, Does.Contain("Walk away from me."), "The newer call is with the provider.");

                guard.Reset();

                await Cancelled(newer);
                await Cancelled(older);
                Assert.That(fake.Requests, Has.Count.EqualTo(2), "The older call was handed the slot but never sent.");
                foreach (var request in fake.Requests)
                {
                    Assert.That(request.Stimulus, Does.Not.Contain("A cart rolls past."));
                }

                await Fixtures.NotCancelled(othersCall);
            }
        }

        [Test]
        public async Task AResetNeverCancelsAnotherAgentsDecisionsOnTheSameProvider()
        {
            var fake = Held();
            var guard = Fixtures.VillageGuard(fake);
            var other = Fixtures.VillageGuard(fake);
            using (var others = new CancellationTokenSource())
            {
                var guardsFirst = guard.DecideAsync("Walk away from me.");
                var othersFirst = other.DecideAsync("The bell rings once.", null, others.Token);
                var guardsSecond = guard.DecideAsync("Walk away from me, again.", Background);
                var othersSecond = other.DecideAsync("Go to the tower.", Background, others.Token);
                Assert.That(RequestScheduler.For(fake).Queued, Is.EqualTo(3));

                guard.Reset();

                await Cancelled(guardsFirst);
                await Cancelled(guardsSecond);
                Assert.That(fake.Requests, Has.Count.EqualTo(2), "The slot the guard gave up went to the other agent.");
                Assert.That(fake.LastRequest.Stimulus, Does.Contain("The bell rings once."));
                Assert.That(RequestScheduler.For(fake).Queued, Is.EqualTo(1), "The other agent's second call still waits.");
                Assert.That(other.IsDeciding, Is.True);
                Assert.That(fake.LastToken.IsCancellationRequested, Is.False, "The other agent's request is not stopped.");

                fake.Release();
                fake.Release();
                var first = await Fixtures.NotCancelled(othersFirst);
                var second = await Fixtures.NotCancelled(othersSecond);
                Assert.That(first.Decision.ActionId, Is.EqualTo("move_to"));
                Assert.That(second.Decision.ActionId, Is.EqualTo("move_to"));
                foreach (var request in fake.Requests)
                {
                    Assert.That(request.Stimulus, Does.Not.Contain("Walk away from me, again."));
                }
            }
        }

        // ---- What a caller sees --------------------------------------------------------------

        [Test]
        public async Task ACallerTellsAResetFromItsOwnCancellationByTheExceptionsToken()
        {
            var fake = Held();
            var agent = Fixtures.VillageGuard(fake);

            using (var mine = new CancellationTokenSource())
            {
                var call = agent.DecideAsync("Go to the tower.", null, mine.Token);
                agent.Reset();

                var byReset = await Cancelled(call);
                Assert.That(byReset, Is.TypeOf<OperationCanceledException>(), "The plain cancellation, no subclass.");
                Assert.That(mine.IsCancellationRequested, Is.False);
                Assert.That(byReset.CancellationToken, Is.Not.EqualTo(mine.Token), "A reset names the agent's token.");
                Assert.That(byReset.CancellationToken.IsCancellationRequested, Is.True);
            }

            // The caller's own cancellation names its own token, as it always did: with the provider,
            // and waiting in its queue.
            using (var mine = new CancellationTokenSource())
            {
                var inFlight = agent.DecideAsync("Go to the tower.", null, mine.Token);
                var queued = agent.DecideAsync("Walk away from me.", null, mine.Token);
                mine.Cancel();

                Assert.That((await Cancelled(inFlight)).CancellationToken, Is.EqualTo(mine.Token));
                Assert.That((await Cancelled(queued)).CancellationToken, Is.EqualTo(mine.Token));
            }
        }

        [Test]
        public async Task ACallMadeAfterAResetDecidesAsUsual()
        {
            var fake = new FakeProvider(GoToTheTower);
            var agent = Fixtures.VillageGuard(fake);
            await Fixtures.NotCancelled(agent.DecideAsync("The bell rings once."));

            agent.Reset();
            agent.Reset();

            var result = await Fixtures.NotCancelled(agent.DecideAsync("Go to the tower."));
            Assert.That(result.Decision.ActionId, Is.EqualTo("move_to"));
            Assert.That(fake.LastRequest.History, Is.Empty, "The memory was cleared.");

            await Fixtures.NotCancelled(agent.DecideAsync("Still there?"));
            Assert.That(fake.LastRequest.History, Has.Count.EqualTo(2), "And remembers again.");
        }

        [Test]
        public async Task ACallMadeByACancelledCallerWhileTheResetRunsIsNotCancelledAndRecallsTheClearedMemory()
        {
            var fake = new FakeProvider(GoToTheTower);
            var agent = Fixtures.VillageGuard(fake);
            await Fixtures.NotCancelled(agent.DecideAsync("The bell rings once."));
            fake.Hold = true;

            bool resetting = false;
            bool askedDuringReset = false;
            Awaitable<DecisionResult> again = null;

            // A caller that asks again as soon as its decision is cancelled, as a game's retry would.
            async Task AskAgainOnCancel()
            {
                try
                {
                    await agent.DecideAsync("Walk away from me.");
                }
                catch (OperationCanceledException)
                {
                    askedDuringReset = resetting;
                    again = agent.DecideAsync("Go to the tower.");
                }
            }

            var caller = AskAgainOnCancel();
            resetting = true;
            agent.Reset();
            resetting = false;
            await caller;

            Assert.That(askedDuringReset, Is.True, "The caller resumed inside Reset, the case this test is about.");
            Assert.That(fake.LastRequest.Stimulus, Does.Contain("Go to the tower."), "Sent, not cancelled.");
            Assert.That(fake.LastRequest.History, Is.Empty, "It recalled the memory the reset had already cleared.");

            fake.Release();
            var result = await Fixtures.NotCancelled(again);
            Assert.That(result.Decision.ActionId, Is.EqualTo("move_to"));
        }

        // ---- Memory --------------------------------------------------------------------------

        [Test]
        public async Task CommitAndDiscardReturnFalseForADecisionHeldAcrossAResetAndNothingIsRemembered()
        {
            var (agent, _, memory) = OnExecuteGuard();
            var committed = await Fixtures.NotCancelled(agent.DecideAsync("Go to the tower."));
            var discarded = await Fixtures.NotCancelled(agent.DecideAsync("Still there?"));

            agent.Reset();

            Assert.That(agent.Commit(committed), Is.False, "No longer waiting: the reset dropped it.");
            Assert.That(agent.Discard(discarded), Is.False);
            Assert.That(agent.Commit(discarded), Is.False);
            Assert.That(memory.Remembered, Is.Empty);
        }

        [Test]
        public async Task ExecuteStillCarriesOutADecisionHeldAcrossAResetButRemembersNothing()
        {
            var (agent, _, memory) = OnExecuteGuard();
            var held = await Fixtures.NotCancelled(agent.DecideAsync("Go to the tower."));

            agent.Reset();

            Assert.That(agent.Execute(held, out var outcome), Is.True);
            Assert.That(outcome.Status, Is.EqualTo(ExecutionStatus.Done));
            Assert.That(Fixtures.Handler(agent, "move_to").Executions, Has.Count.EqualTo(1), "The handler ran, re-check and all.");
            Assert.That(memory.Remembered, Is.Empty);

            // A decision made after the reset is held and remembered as before.
            var after = await Fixtures.NotCancelled(agent.DecideAsync("Go to the tower."));
            agent.Execute(after);
            Assert.That(memory.Remembered, Is.EqualTo(new[] { after }));
        }

        [Test]
        public async Task AReplyThatArrivesAfterTheResetIsNeverRemembered()
        {
            var fake = new FakeProvider(GoToTheTower) { Hold = true, IgnoresCancellation = true };
            var agent = Fixtures.VillageGuard(fake);
            var memory = new RollingHistory();
            agent.Memory = memory;
            var pending = agent.DecideAsync("Go to the tower.");

            agent.Reset();
            Assert.That(agent.IsDeciding, Is.True, "Still with a provider that ignores cancellation.");

            fake.Release();
            await Cancelled(pending);
            Assert.That(memory.Count, Is.Zero, "The answer came, but for an agent that has started afresh.");
            Assert.That(agent.IsDeciding, Is.False);
        }

        [Test]
        public async Task AResetFromInsideADecisionCancelsItBeforeItIsRemembered()
        {
            var fake = new FakeProvider(GoToTheTower);
            var agent = Fixtures.VillageGuard(fake);
            var memory = new RollingHistory();
            agent.Memory = memory;
            agent.Guards.Add(new FakeGuard("resets", _ =>
            {
                agent.Reset();
                return GuardVerdict.Pass;
            }));

            await Cancelled(agent.DecideAsync("Go to the tower."));
            Assert.That(memory.Count, Is.Zero, "Nothing was written into the memory the reset cleared.");
        }

        [Test]
        public async Task AMemoryWithoutIClearableMemoryKeepsWhatItHoldsAndAWarningSaysSo()
        {
            var fake = new FakeProvider(GoToTheTower);
            var agent = Fixtures.VillageGuard(fake);
            var memory = new UnclearableMemory();
            agent.Memory = memory;
            await Fixtures.NotCancelled(agent.DecideAsync("The bell rings once."));
            fake.Hold = true;
            var inFlight = agent.DecideAsync("Still there?");
            LogAssert.Expect(LogType.Warning,
                new Regex("Agent 'Village Guard' was reset, but its memory, a UnclearableMemory, still holds what it remembered: it does not implement IClearableMemory\\."));

            agent.Reset();

            await Cancelled(inFlight);
            Assert.That(agent.Memory, Is.SameAs(memory), "Not replaced.");
            fake.Hold = false;
            await Fixtures.NotCancelled(agent.DecideAsync("Anything else?"));
            Assert.That(fake.LastRequest.History, Has.Count.EqualTo(2), "It still recalls what it held.");
        }

        [Test]
        public async Task AClearThatThrowsStillCancelsEverything()
        {
            var fake = Held();
            var agent = Fixtures.VillageGuard(fake);
            agent.Memory = new ClearableSpy { ClearThrows = true };
            var inFlight = agent.DecideAsync("The bell rings once.");
            var queued = agent.DecideAsync("Walk away from me.");

            Assert.Throws<InvalidOperationException>(() => agent.Reset());

            await Cancelled(inFlight);
            await Cancelled(queued);
            Assert.That(fake.Requests, Has.Count.EqualTo(1));

            var after = agent.DecideAsync("Go to the tower.");
            fake.Release();
            await Fixtures.NotCancelled(after);
        }

        [Test]
        public async Task AMemoryTwoAgentsShareIsClearedForBoth()
        {
            var fake = new FakeProvider(GoToTheTower);
            var guard = Fixtures.VillageGuard(fake);
            var other = Fixtures.VillageGuard(fake);
            var shared = new RollingHistory();
            guard.Memory = shared;
            other.Memory = shared;
            await Fixtures.NotCancelled(other.DecideAsync("The bell rings once."));
            Assert.That(shared.Count, Is.EqualTo(1));

            guard.Reset();

            await Fixtures.NotCancelled(other.DecideAsync("Go to the tower."));
            Assert.That(fake.LastRequest.History, Is.Empty, "The other agent's history went with it.");
        }

        [Test]
        public async Task AnAgentWithNoMemoryResetsWithoutAWarning()
        {
            var fake = new FakeProvider(GoToTheTower);
            var agent = Fixtures.VillageGuard(fake);
            agent.Memory = null;

            agent.Reset();

            LogAssert.NoUnexpectedReceived();
            await Fixtures.NotCancelled(agent.DecideAsync("Go to the tower."));
        }

        // ---- Helpers -------------------------------------------------------------------------

        private static FakeProvider Held()
        {
            return new FakeProvider(GoToTheTower) { Hold = true };
        }

        private static (Agent Agent, FakeProvider Fake, ClearableSpy Memory) OnExecuteGuard()
        {
            var fake = new FakeProvider(GoToTheTower);
            var agent = Fixtures.VillageGuard(fake);
            var memory = new ClearableSpy();
            agent.Memory = memory;
            agent.MemoryCommit = MemoryCommit.OnExecute;
            return (agent, fake, memory);
        }

        // The cancellation a call ended with. It must have ended already: everything a reset
        // cancels here ends within Reset, so a reset that cancelled nothing fails rather than hangs.
        private static async Task<OperationCanceledException> Cancelled(Awaitable<DecisionResult> decision)
        {
            Assert.That(decision.GetAwaiter().IsCompleted, Is.True, "The call has not ended.");
            try
            {
                await decision;
            }
            catch (OperationCanceledException e)
            {
                return e;
            }

            Assert.Fail("The call returned a result; it should have ended cancelled.");
            return null;
        }

        // Remembers what it is given, and can be cleared, or made to fail at it.
        private sealed class ClearableSpy : IMemoryStrategy, IClearableMemory
        {
            public bool ClearThrows;
            public readonly List<DecisionResult> Remembered = new List<DecisionResult>();

            public IReadOnlyList<string> Recall(AgentContext ctx)
            {
                return Array.Empty<string>();
            }

            public void Remember(AgentContext ctx, DecisionRequest request, DecisionResult result)
            {
                Remembered.Add(result);
            }

            public void Clear()
            {
                if (ClearThrows)
                {
                    throw new InvalidOperationException("The spy refuses to forget.");
                }

                Remembered.Clear();
            }
        }

        // A strategy written before IClearableMemory: a RollingHistory behind the plain interface.
        private sealed class UnclearableMemory : IMemoryStrategy
        {
            private readonly RollingHistory inner = new RollingHistory();

            public IReadOnlyList<string> Recall(AgentContext ctx)
            {
                return inner.Recall(ctx);
            }

            public void Remember(AgentContext ctx, DecisionRequest request, DecisionResult result)
            {
                inner.Remember(ctx, request, result);
            }
        }
    }
}

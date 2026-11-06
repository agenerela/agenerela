using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

namespace Agenerela.Tests
{
    // The optional conversation helper (#190, Living Vale #8): one turn waiting at a time, a
    // deadline the caller sets, and a stale result refused at Execute and kept out of memory. The
    // first two tests are the two races Codex found in Living Vale's own ConversationCoordinator.
    // A turn's Completion never ends cancelled, so every await of one goes through
    // Fixtures.NotCancelled, the cancellation tests' included; a test that must not hang on a
    // broken deadline checks IsCompleted after a delay before awaiting.
    public sealed class ConversationTests
    {
        private const string GoToTheTower = "{\"action\":\"move_to\",\"target\":\"tower\",\"statement\":\"On my way.\"}";
        private const string AttackTheDummy = "{\"action\":\"attack_target\",\"target\":\"training_dummy\",\"statement\":\"Right away.\"}";

        private static readonly TimeSpan Short = TimeSpan.FromMilliseconds(50);

        [TearDown]
        public void DestroyFixtures()
        {
            Fixtures.DestroyCreated();
        }

        // ---- The two races in Living Vale's version ------------------------------------------

        // The first: a timeout that waited for a backend which ignores cancellation, so the turn hung
        // past its deadline. The turn ends at the deadline, and the answer that comes later neither
        // runs, nor enters memory, nor ends the turn started after it.
        [Test]
        public async Task ATurnEndsAtItsDeadlineEvenWhenTheProviderIgnoresCancellation()
        {
            var (agent, fake, chat) = Guard(hold: true);
            fake.IgnoresCancellation = true;
            chat.Timeout = Short;

            var first = chat.Start("Go to the tower.");
            await Task.Delay(300);

            Assert.That(first.Completion.IsCompleted, Is.True, "The turn ends at its deadline, without waiting for the provider.");
            Assert.That(await Fixtures.NotCancelled(first.Completion), Is.EqualTo(TurnStatus.TimedOut));
            Assert.That(chat.IsWaiting, Is.False);
            Assert.That(chat.Current, Is.Null);
            Assert.That(fake.HeldCount, Is.EqualTo(1), "The provider still has the request.");

            chat.Timeout = System.Threading.Timeout.InfiniteTimeSpan;
            var next = chat.Start("Are you there?");
            Assert.That(next, Is.Not.Null, "A turn that timed out waits no more, so the next one starts.");

            // The first request's late answer, which also frees the queue for the next request.
            fake.Release();
            await Task.Delay(100);

            Assert.That(Fixtures.Handler(agent, "move_to").Executions, Is.Empty, "The late answer never runs.");
            Assert.That(Remembered(agent), Is.Zero, "The late answer never enters memory.");
            Assert.That(first.Status, Is.EqualTo(TurnStatus.TimedOut));
            Assert.That(first.Result, Is.Null);
            Assert.That(next.Status, Is.EqualTo(TurnStatus.Waiting), "The late answer does not end the turn after it.");
            Assert.That(chat.Current, Is.SameAs(next));
            Assert.That(fake.Requests, Has.Count.EqualTo(2), "The next request reached the provider once the late one let go of the queue.");

            fake.Reply = AttackTheDummy;
            fake.Release();
            Assert.That(await Fixtures.NotCancelled(next.Completion), Is.EqualTo(TurnStatus.Answered));
            Assert.That(chat.Execute(next.Result), Is.True);
            Assert.That(Fixtures.Handler(agent, "attack_target").Executions, Has.Count.EqualTo(1));
            Assert.That(Remembered(agent), Is.EqualTo(1), "Only the answer that ran is remembered.");
        }

        // The second: a turn started again from inside the first one's start, before the first
        // reached the backend, was given the first's cancellation source, so the two turns' ends
        // crossed. Here the game's own code that the agent runs while preparing the first request,
        // an observation source, replaces the turn being started.
        [Test]
        public async Task ATurnStartedFromInsideAnotherKeepsItsOwnDeadline()
        {
            var (agent, fake, chat) = Guard(hold: true);
            chat.Timeout = Short;
            bool replaced = false;
            ConversationTurn retry = null;
            agent.ObservationSources.Add(new OnObserve(() =>
            {
                if (!replaced)
                {
                    replaced = true;
                    retry = chat.Replace("Go to the tower, now.");
                }
            }));

            var first = chat.Start("Go to the tower.");

            Assert.That(first.Status, Is.EqualTo(TurnStatus.Replaced));
            Assert.That(retry.Status, Is.EqualTo(TurnStatus.Waiting), "The first turn's cancelled request does not end the turn that replaced it.");
            Assert.That(chat.Current, Is.SameAs(retry));
            Assert.That(fake.Requests, Has.Count.EqualTo(1), "The replaced turn never reaches the provider.");
            Assert.That(fake.Requests[0].Stimulus, Does.Contain("now"));

            await Task.Delay(300);

            Assert.That(retry.Completion.IsCompleted, Is.True);
            Assert.That(await Fixtures.NotCancelled(retry.Completion), Is.EqualTo(TurnStatus.TimedOut), "The retry ends at its own deadline.");
            Assert.That(await Fixtures.NotCancelled(first.Completion), Is.EqualTo(TurnStatus.Replaced), "The first turn keeps its own ending.");
            Assert.That(chat.IsWaiting, Is.False);
            Assert.That(fake.HeldCount, Is.Zero, "The retry's request was cancelled at its deadline.");
        }

        // ---- One turn at a time -------------------------------------------------------------

        [Test]
        public async Task ATurnIsAnsweredAndItsResultRunsWithTheCallersOwnCorrelation()
        {
            var (agent, _, chat) = Guard();
            var quote = new object();

            var turn = chat.Start("Go to the tower.", new DecideOptions { Correlation = quote });

            Assert.That(await Fixtures.NotCancelled(turn.Completion), Is.EqualTo(TurnStatus.Answered));
            Assert.That(turn.Number, Is.EqualTo(1));
            Assert.That(turn.Result.Decision.ActionId, Is.EqualTo("move_to"));
            Assert.That(turn.Result.Correlation, Is.SameAs(quote), "The conversation never replaces the caller's correlation.");
            Assert.That(turn.IsCurrent, Is.True);
            Assert.That(chat.Current, Is.SameAs(turn));
            Assert.That(chat.IsCurrent(turn.Result), Is.True);
            Assert.That(chat.IsWaiting, Is.False);
            Assert.That(Remembered(agent), Is.Zero, "Held until it is executed.");

            Assert.That(chat.Execute(turn.Result, out var outcome), Is.True);
            Assert.That(outcome.Status, Is.EqualTo(ExecutionStatus.Done));
            var executions = Fixtures.Handler(agent, "move_to").Executions;
            Assert.That(executions, Has.Count.EqualTo(1));
            Assert.That(executions[0].Context.Correlation, Is.SameAs(quote));
            Assert.That(Remembered(agent), Is.EqualTo(1));

            var next = chat.Start("Go to the tower again.");
            Assert.That(next, Is.Not.Null, "An answered turn waits no more.");
            Assert.That(next.Number, Is.EqualTo(2));
        }

        [Test]
        public async Task StartIsRefusedWhileATurnWaits_AndReplaceEndsIt()
        {
            var (agent, fake, chat) = Guard(hold: true);

            var first = chat.Start("Go to the tower.");
            Assert.That(chat.IsWaiting, Is.True);
            Assert.That(chat.Start("Attack the dummy."), Is.Null, "One turn at a time, never queued.");
            Assert.That(fake.Requests, Has.Count.EqualTo(1), "The refused turn asked nothing.");
            Assert.That(chat.Current, Is.SameAs(first));

            var second = chat.Replace("Attack the dummy.");

            Assert.That(await Fixtures.NotCancelled(first.Completion), Is.EqualTo(TurnStatus.Replaced));
            Assert.That(first.IsCurrent, Is.False);
            Assert.That(chat.Current, Is.SameAs(second));
            Assert.That(fake.HeldCount, Is.EqualTo(1), "The replaced request was cancelled; only the new one is out.");

            fake.Reply = AttackTheDummy;
            fake.Release();
            Assert.That(await Fixtures.NotCancelled(second.Completion), Is.EqualTo(TurnStatus.Answered));
            Assert.That(chat.Execute(second.Result), Is.True);
            Assert.That(Fixtures.Handler(agent, "move_to").Executions, Is.Empty);
            Assert.That(Fixtures.Handler(agent, "attack_target").Executions, Has.Count.EqualTo(1));
        }

        // ---- Stale results ------------------------------------------------------------------

        [Test]
        public async Task ANewTurnMakesTheAnsweredResultStale_AndExecuteRefusesIt()
        {
            var (agent, fake, chat) = Guard();
            var first = chat.Start("Go to the tower.");
            Assert.That(await Fixtures.NotCancelled(first.Completion), Is.EqualTo(TurnStatus.Answered));

            fake.Reply = AttackTheDummy;
            var second = chat.Start("Attack the dummy.");
            Assert.That(await Fixtures.NotCancelled(second.Completion), Is.EqualTo(TurnStatus.Answered));

            Assert.That(first.Status, Is.EqualTo(TurnStatus.Answered), "How its wait ended does not change.");
            Assert.That(first.IsCurrent, Is.False);
            Assert.That(chat.IsCurrent(first.Result), Is.False);

            Assert.That(chat.Execute(first.Result, out var outcome), Is.False);
            Assert.That(outcome.Status, Is.EqualTo(ExecutionStatus.Refused));
            Assert.That(outcome.Reason, Does.Contain("current turn"));
            Assert.That(first.Result.Telemetry.Execution, Is.EqualTo(ExecutionStatus.Refused));
            Assert.That(first.Result.Telemetry.ExecutionReason, Is.EqualTo(outcome.Reason));
            Assert.That(Fixtures.Handler(agent, "move_to").Executions, Is.Empty, "The stale result never runs.");
            Assert.That(agent.Commit(first.Result), Is.False, "It was discarded when it went stale, so not even the agent can remember it.");

            Assert.That(chat.Execute(second.Result), Is.True);
            Assert.That(Remembered(agent), Is.EqualTo(1), "Only the current answer is remembered.");

            // The agent called directly knows nothing of turns, as documented: it runs the stale
            // result's handler, but its memory stays out of reach.
            Assert.That(agent.Execute(first.Result), Is.True);
            Assert.That(Fixtures.Handler(agent, "move_to").Executions, Has.Count.EqualTo(1));
            Assert.That(Remembered(agent), Is.EqualTo(1));
        }

        [Test]
        public async Task CancelEndsAWaitingTurn_AndMakesAnAnsweredOneStale()
        {
            var (agent, fake, chat) = Guard(hold: true);
            Assert.That(chat.Cancel(), Is.False, "Nothing to end.");

            var waiting = chat.Start("Go to the tower.");
            Assert.That(chat.Cancel(), Is.True);
            Assert.That(await Fixtures.NotCancelled(waiting.Completion), Is.EqualTo(TurnStatus.Cancelled));
            Assert.That(chat.Current, Is.Null);
            Assert.That(fake.HeldCount, Is.Zero, "Its request was cancelled.");
            Assert.That(chat.Cancel(), Is.False);

            fake.Hold = false;
            var answered = chat.Start("Go to the tower.");
            Assert.That(await Fixtures.NotCancelled(answered.Completion), Is.EqualTo(TurnStatus.Answered));
            Assert.That(chat.Cancel(), Is.True);

            Assert.That(answered.Status, Is.EqualTo(TurnStatus.Answered));
            Assert.That(chat.Current, Is.Null);
            Assert.That(chat.Execute(answered.Result), Is.False);
            Assert.That(agent.Commit(answered.Result), Is.False);
            Assert.That(Fixtures.Handler(agent, "move_to").Executions, Is.Empty);
            Assert.That(Remembered(agent), Is.Zero);
        }

        // The handler is the game's code, and may start the next turn from inside Execute. The
        // answer being carried out then is remembered all the same; one whose handler failed goes
        // stale with its turn, as any other.
        [TestCase(true)]
        [TestCase(false)]
        public async Task AHandlerThatStartsTheNextTurnDoesNotLoseTheAnswerItCarriesOut(bool succeeds)
        {
            var fake = new FakeProvider(GoToTheTower);
            var move = new ActionDefinition
            {
                Id = "move_to",
                Description = "Walk to a named place.",
                RequiresTarget = true,
                ExampleStimulus = "Proceed to the {0}.",
                PreferredExampleTargets = new[] { "tower" },
            };
            var agent = new Agent(Fixtures.VillageGuardIdentity(), new[] { move }, fake) { MemoryCommit = MemoryCommit.OnExecute };
            agent.Targets.Register("tower", new object());
            var chat = new Conversation(agent);
            ConversationTurn next = null;
            agent.Bind("move_to", DelegateActionHandler.WithResult(ctx => true, (ctx, decision) =>
            {
                next = chat.Start("Anything else?");
                return succeeds ? ActionResult.Done() : ActionResult.Failed("The gate is shut.");
            }));

            var first = chat.Start("Go to the tower.");
            Assert.That(await Fixtures.NotCancelled(first.Completion), Is.EqualTo(TurnStatus.Answered));

            Assert.That(chat.Execute(first.Result), Is.EqualTo(succeeds));
            Assert.That(next, Is.Not.Null, "The handler started the next turn.");
            Assert.That(first.IsCurrent, Is.False);
            Assert.That(Remembered(agent), Is.EqualTo(succeeds ? 1 : 0),
                succeeds ? "Carried out, so remembered, though its turn went stale while it ran." : "Not carried out, and stale.");
            Assert.That(agent.Commit(first.Result), Is.False, "Settled either way, so nothing remembers it again.");
        }

        [Test]
        public async Task ExecuteRefusesAResultTheConversationNeverAskedFor()
        {
            var (agent, _, chat) = Guard();
            var outside = await Fixtures.NotCancelled(agent.DecideAsync("Go to the tower."));

            Assert.That(chat.IsCurrent(outside), Is.False);
            Assert.That(chat.IsCurrent(null), Is.False);
            Assert.That(chat.Execute(outside, out var outcome), Is.False);
            Assert.That(outcome.Status, Is.EqualTo(ExecutionStatus.Refused));
            Assert.That(Fixtures.Handler(agent, "move_to").Executions, Is.Empty);
            Assert.Throws<ArgumentNullException>(() => chat.Execute(null));
        }

        // An answer the agent returns after its turn ended is never handed out, and is discarded
        // from memory. Agent itself never returns a result once its token is cancelled, so the
        // conversation is given a decide of its own that ignores the token.
        [Test]
        public async Task AnAnswerAfterItsTurnEndedIsNeverHandedOut()
        {
            var (agent, _, _) = Guard();
            var source = new AwaitableCompletionSource<DecisionResult>();
            var chat = new Conversation(agent, (stimulus, options, ct) => source.Awaitable);
            var late = await Fixtures.NotCancelled(agent.DecideAsync("Go to the tower."));

            var turn = chat.Start("Go to the tower.");
            chat.Cancel();
            source.TrySetResult(late);
            await Task.Delay(100);

            Assert.That(turn.Status, Is.EqualTo(TurnStatus.Cancelled));
            Assert.That(turn.Result, Is.Null);
            Assert.That(chat.Current, Is.Null);
            Assert.That(agent.Commit(late), Is.False, "Discarded as it arrived, so nothing can remember it.");
        }

        [Test]
        public async Task AnAnswerThatLandsAfterTheCallerCancelledIsNotHandedOut()
        {
            var (agent, _, _) = Guard();
            var source = new AwaitableCompletionSource<DecisionResult>();
            var chat = new Conversation(agent, (stimulus, options, ct) => source.Awaitable);
            var late = await Fixtures.NotCancelled(agent.DecideAsync("Go to the tower."));

            using (var cts = new CancellationTokenSource())
            {
                var turn = chat.Start("Go to the tower.", ct: cts.Token);
                cts.Cancel();
                source.TrySetResult(late);

                Assert.That(await Fixtures.NotCancelled(turn.Completion), Is.EqualTo(TurnStatus.Cancelled));
                await Task.Delay(100);
                Assert.That(turn.Result, Is.Null);
                Assert.That(agent.Commit(late), Is.False);
            }
        }

        // ---- The caller's token, the timeout and mistakes -----------------------------------

        [Test]
        public async Task TheCallersTokenEndsTheTurnEvenWhenTheProviderIgnoresIt()
        {
            var (agent, fake, chat) = Guard(hold: true);
            fake.IgnoresCancellation = true;

            using (var cts = new CancellationTokenSource())
            {
                var turn = chat.Start("Go to the tower.", ct: cts.Token);
                cts.Cancel();
                await Task.Delay(100);

                Assert.That(turn.Completion.IsCompleted, Is.True, "Raced against the token, not left to the provider.");
                Assert.That(await Fixtures.NotCancelled(turn.Completion), Is.EqualTo(TurnStatus.Cancelled));
                Assert.That(chat.IsWaiting, Is.False);

                fake.Release();
                await Task.Delay(100);
                Assert.That(turn.Result, Is.Null);
                Assert.That(Fixtures.Handler(agent, "move_to").Executions, Is.Empty);
                Assert.That(Remembered(agent), Is.Zero);
            }
        }

        [Test]
        public async Task ATurnAnsweredBeforeItsDeadlineIsNotTimedOutLater()
        {
            var (agent, fake, chat) = Guard(hold: true);
            chat.Timeout = Short;

            var turn = chat.Start("Go to the tower.");
            fake.Release();
            Assert.That(await Fixtures.NotCancelled(turn.Completion), Is.EqualTo(TurnStatus.Answered));
            await Task.Delay(300);

            Assert.That(turn.Status, Is.EqualTo(TurnStatus.Answered));
            Assert.That(chat.Current, Is.SameAs(turn), "Still current after its deadline would have passed.");
            Assert.That(chat.Execute(turn.Result), Is.True);
        }

        [Test]
        public void TimeoutIsInfiniteByDefaultAndMustBePositive()
        {
            var (_, _, chat) = Guard();

            Assert.That(chat.Timeout, Is.EqualTo(System.Threading.Timeout.InfiniteTimeSpan));
            Assert.Throws<ArgumentOutOfRangeException>(() => chat.Timeout = TimeSpan.Zero);
            Assert.Throws<ArgumentOutOfRangeException>(() => chat.Timeout = TimeSpan.FromMilliseconds(-5));
            Assert.Throws<ArgumentOutOfRangeException>(() => chat.Timeout = TimeSpan.FromDays(30));

            chat.Timeout = TimeSpan.FromSeconds(8);
            Assert.That(chat.Timeout, Is.EqualTo(TimeSpan.FromSeconds(8)));
            chat.Timeout = System.Threading.Timeout.InfiniteTimeSpan;
        }

        [Test]
        public void StartThrowsForAnAgentThatRemembersAsItDecides()
        {
            var fake = new FakeProvider(GoToTheTower);
            var agent = Fixtures.VillageGuard(fake);
            var chat = new Conversation(agent);
            Assert.That(agent.MemoryCommit, Is.EqualTo(MemoryCommit.OnDecide));

            var e = Assert.Throws<InvalidOperationException>(() => chat.Start("Go to the tower."));
            Assert.That(e.Message, Does.Contain("MemoryCommit.OnExecute"));
            Assert.Throws<InvalidOperationException>(() => chat.Replace("Go to the tower."));
            Assert.That(fake.Requests, Is.Empty);
            Assert.That(chat.Current, Is.Null);

            agent.Memory = null;
            Assert.That(chat.Start("Go to the tower."), Is.Not.Null, "An agent with no memory has nothing to keep stale replies out of.");
        }

        [Test]
        public async Task AMistakeInTheCallThrowsAndLeavesThePreviousTurnAsItWas()
        {
            var (agent, fake, chat) = Guard();
            var noLabel = new DecideOptions { StimulusLabel = "" };

            var answered = chat.Start("Go to the tower.");
            Assert.That(await Fixtures.NotCancelled(answered.Completion), Is.EqualTo(TurnStatus.Answered));
            Assert.Throws<ArgumentException>(() => chat.Start("Attack the dummy.", noLabel));
            Assert.That(chat.Current, Is.SameAs(answered));
            Assert.That(chat.Execute(answered.Result), Is.True, "Its result is still current.");

            fake.Hold = true;
            var waiting = chat.Start("Go to the tower again.");
            Assert.Throws<ArgumentException>(() => chat.Replace("Attack the dummy.", noLabel));
            Assert.That(waiting.Status, Is.EqualTo(TurnStatus.Waiting));
            Assert.That(chat.Current, Is.SameAs(waiting));
            Assert.That(fake.HeldCount, Is.EqualTo(1));

            fake.Release();
            Assert.That(await Fixtures.NotCancelled(waiting.Completion), Is.EqualTo(TurnStatus.Answered));
            Assert.That(chat.Start("Once more.").Number, Is.EqualTo(3), "The refused calls took no turn number.");
        }

        [Test]
        public async Task ADecideThatFaultsEndsTheTurnAsFailed()
        {
            var (agent, _, _) = Guard();
            var source = new AwaitableCompletionSource<DecisionResult>();
            var chat = new Conversation(agent, (stimulus, options, ct) => source.Awaitable);

            var turn = chat.Start("Go to the tower.");
            source.TrySetException(new InvalidOperationException("A bug."));
            await Task.Delay(100);

            Assert.That(turn.Status, Is.EqualTo(TurnStatus.Failed));
            Assert.That(chat.IsWaiting, Is.False, "A fault never leaves the conversation waiting.");
            Assert.That(turn.Completion.IsFaulted, Is.True);
            Assert.That(turn.Completion.Exception.GetBaseException(), Is.TypeOf<InvalidOperationException>());
            Assert.That(chat.Start("Go to the tower."), Is.Not.Null);
        }

        [Test]
        public void TheConstructorNeedsAnAgent()
        {
            Assert.Throws<ArgumentNullException>(() => new Conversation(null));
        }

        // ---- Helpers ------------------------------------------------------------------------

        // The village guard, remembering under OnExecute, as a conversation requires.
        private static (Agent Agent, FakeProvider Fake, Conversation Chat) Guard(bool hold = false)
        {
            var fake = new FakeProvider(GoToTheTower) { Hold = hold };
            var agent = Fixtures.VillageGuard(fake);
            agent.MemoryCommit = MemoryCommit.OnExecute;
            Assert.That(agent.Memory, Is.InstanceOf<RollingHistory>());
            return (agent, fake, new Conversation(agent));
        }

        private static int Remembered(Agent agent)
        {
            return ((RollingHistory)agent.Memory).Count;
        }

        // Runs the game's code inside the agent's request preparation, as a real source would.
        private sealed class OnObserve : IObservationSource
        {
            private readonly Action observed;

            public OnObserve(Action observed)
            {
                this.observed = observed;
            }

            public IEnumerable<string> Observe(AgentContext ctx)
            {
                observed();
                return Array.Empty<string>();
            }
        }
    }
}

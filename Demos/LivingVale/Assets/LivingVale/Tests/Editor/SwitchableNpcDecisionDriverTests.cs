using System;
using System.Threading;
using System.Threading.Tasks;
using Agenerela;
using LivingVale.Agenerela;
using LivingVale.Interaction;
using NUnit.Framework;

namespace LivingVale.Tests
{
    public sealed class SwitchableNpcDecisionDriverTests
    {
        private static readonly NpcTurn Turn = new NpcTurn(1,
            new NpcDescriptor("npc_mara", "Mara", "General trader"), "hello", Array.Empty<ChatLine>());

        private StubDriver scripted;
        private StubDriver model;
        private SwitchableNpcDecisionDriver drivers;

        [SetUp]
        public void SetUp()
        {
            scripted = new StubDriver("Scripted");
            model = new StubDriver("Agenerela");
            drivers = new SwitchableNpcDecisionDriver(scripted, model, NpcDriverChoice.Scripted);
        }

        [Test]
        public void LabelAndCurrentFollowSelectionAndCycling()
        {
            Assert.That(drivers.Current, Is.SameAs(scripted));
            Assert.That(drivers.Label, Is.EqualTo(scripted.Label));

            drivers.Select(NpcDriverChoice.Agenerela);
            Assert.That(drivers.Current, Is.SameAs(model));
            Assert.That(drivers.Label, Is.EqualTo(model.Label));

            drivers.Next();
            Assert.That(drivers.Current, Is.SameAs(scripted));
            Assert.That(drivers.Label, Is.EqualTo(scripted.Label));
            drivers.Next();
            Assert.That(drivers.Current, Is.SameAs(model));
            Assert.That(drivers.Label, Is.EqualTo(model.Label));
        }

        [Test]
        public void ConstructorHonoursInitialSelection()
        {
            var initiallyModel = new SwitchableNpcDecisionDriver(scripted, model, NpcDriverChoice.Agenerela);
            Assert.That(initiallyModel.Current, Is.SameAs(model));
            Assert.That(initiallyModel.Label, Is.EqualTo(model.Label));
        }

        [Test]
        public async Task PendingReplyExecutesOnTheDriverThatDecidedItAfterSwitching()
        {
            var completion = new TaskCompletionSource<NpcReply>();
            scripted.OnDecide = (turn, ct) => completion.Task;
            var inner = Reply(new object());
            var pending = drivers.DecideAsync(Turn, CancellationToken.None);
            Assert.That(pending.IsCompleted, Is.False);

            drivers.Next();
            completion.SetResult(inner);
            await Run.NotCancelled(pending);
            var routed = pending.Result;

            Assert.That(routed, Is.Not.SameAs(inner));
            Assert.That(routed.Decision, Is.SameAs(inner.Decision));
            Assert.That(drivers.Execute(Turn, routed), Is.True);
            Assert.That(scripted.ExecutedCount, Is.EqualTo(1));
            Assert.That(scripted.ExecutedTurn, Is.SameAs(Turn));
            Assert.That(scripted.ExecutedReply, Is.SameAs(inner), "preserve the driver's original payload");
            Assert.That(model.ExecutedCount, Is.Zero);

            var next = drivers.DecideAsync(Turn, CancellationToken.None);
            await Run.NotCancelled(next);
            Assert.That(drivers.Execute(Turn, next.Result), Is.True);
            Assert.That(model.ExecutedCount, Is.EqualTo(1), "future turns use the new selection");
            Assert.That(scripted.ExecutedCount, Is.EqualTo(1));
        }

        [TestCase(NpcReplyFailure.Malformed)]
        [TestCase(NpcReplyFailure.ProviderError)]
        public async Task FailuresPassThroughWithoutRouting(NpcReplyFailure failure)
        {
            scripted.OnDecide = (turn, ct) => Task.FromResult(NpcReply.Failed(failure));
            var pending = drivers.DecideAsync(Turn, CancellationToken.None);
            await Run.NotCancelled(pending);

            Assert.That(pending.Result.Failure, Is.EqualTo(failure));
            Assert.That(pending.Result.Decision, Is.Null);
            Assert.That(pending.Result.DriverState, Is.Null);
            Assert.That(drivers.Execute(Turn, pending.Result), Is.False);
            Assert.That(scripted.ExecutedCount, Is.Zero);
        }

        [Test]
        public void ForeignAndNullRepliesAreRefused()
        {
            Assert.That(drivers.Execute(Turn, Reply(new object())), Is.False);
            Assert.That(drivers.Execute(Turn, null), Is.False);
            Assert.That(scripted.ExecutedCount, Is.Zero);
            Assert.That(model.ExecutedCount, Is.Zero);
        }

        [Test]
        public async Task ReplyWrappedByAnotherSwitchIsRefused()
        {
            var other = new SwitchableNpcDecisionDriver(scripted, model, NpcDriverChoice.Scripted);
            var pending = other.DecideAsync(Turn, CancellationToken.None);
            await Run.NotCancelled(pending);

            Assert.That(drivers.Execute(Turn, pending.Result), Is.False);
            Assert.That(scripted.ExecutedCount, Is.Zero);
        }

        [Test]
        public async Task ExecutionRefusalPassesThrough()
        {
            scripted.ExecutionAllowed = false;
            var pending = drivers.DecideAsync(Turn, CancellationToken.None);
            await Run.NotCancelled(pending);

            Assert.That(drivers.Execute(Turn, pending.Result), Is.False);
            Assert.That(scripted.ExecutedCount, Is.EqualTo(1));
        }

        [Test]
        public async Task OriginalTurnAndCancellationTokenArePassedThrough()
        {
            using (var cts = new CancellationTokenSource())
            {
                var pending = drivers.DecideAsync(Turn, cts.Token);
                await Run.NotCancelled(pending);

                Assert.That(scripted.DecidedTurn, Is.SameAs(Turn));
                Assert.That(scripted.Token, Is.EqualTo(cts.Token));
                cts.Cancel();
                Assert.That(scripted.Token.IsCancellationRequested, Is.True);
            }
        }

        [Test]
        public void NullTaskStaysNull()
        {
            scripted.OnDecide = (turn, ct) => null;
            Assert.That(drivers.DecideAsync(Turn, CancellationToken.None), Is.Null);
        }

        [Test]
        public async Task NullReplyStaysNull()
        {
            scripted.OnDecide = (turn, ct) => Task.FromResult<NpcReply>(null);
            var pending = drivers.DecideAsync(Turn, CancellationToken.None);
            await Run.NotCancelled(pending);
            Assert.That(pending.Result, Is.Null);
        }

        [Test]
        public void SynchronousExceptionPropagatesUnchanged()
        {
            var error = new InvalidOperationException("backend down");
            scripted.OnDecide = (turn, ct) => throw error;
            var caught = Assert.Throws<InvalidOperationException>(() => drivers.DecideAsync(Turn, CancellationToken.None));
            Assert.That(caught, Is.SameAs(error));
        }

        [Test]
        public void TaskExceptionPropagatesUnchanged()
        {
            var error = new InvalidOperationException("backend down");
            scripted.OnDecide = (turn, ct) => Task.FromException<NpcReply>(error);
            var pending = drivers.DecideAsync(Turn, CancellationToken.None);
            var caught = Assert.ThrowsAsync<InvalidOperationException>(async () => await Run.NotCancelled(pending));
            Assert.That(caught, Is.SameAs(error));
        }

        [Test]
        public void CancellationPropagatesWithoutEndingTheTestCancelled()
        {
            using (var cts = new CancellationTokenSource())
            {
                cts.Cancel();
                scripted.OnDecide = (turn, ct) => Task.FromCanceled<NpcReply>(ct);
                var pending = drivers.DecideAsync(Turn, cts.Token);
                var caught = Assert.ThrowsAsync<TaskCanceledException>(async () => { await pending; });
                Assert.That(caught.CancellationToken, Is.EqualTo(cts.Token));
            }
        }

        private static NpcReply Reply(object state = null) => NpcReply.Decided(
            new AgentDecision(ActionRegistry.None, TargetRegistry.NoTarget, "Morning."), state);

        private sealed class StubDriver : INpcDecisionDriver
        {
            public string Label { get; }
            public Func<NpcTurn, CancellationToken, Task<NpcReply>> OnDecide =
                (turn, ct) => Task.FromResult(Reply());
            public NpcTurn DecidedTurn;
            public CancellationToken Token;
            public NpcTurn ExecutedTurn;
            public NpcReply ExecutedReply;
            public int ExecutedCount;
            public bool ExecutionAllowed = true;

            public StubDriver(string label) => Label = label;

            public Task<NpcReply> DecideAsync(NpcTurn turn, CancellationToken ct)
            {
                DecidedTurn = turn;
                Token = ct;
                return OnDecide(turn, ct);
            }

            public bool Execute(NpcTurn turn, NpcReply reply)
            {
                ExecutedCount++;
                ExecutedTurn = turn;
                ExecutedReply = reply;
                return ExecutionAllowed;
            }
        }
    }
}

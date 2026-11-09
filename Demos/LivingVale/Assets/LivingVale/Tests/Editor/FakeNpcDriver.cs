using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Agenerela;
using LivingVale.Interaction;
using NUnit.Framework;

namespace LivingVale.Tests
{
    /// <summary>
    /// A driver the test answers by hand, turn by turn. With <see cref="IgnoresCancellation"/> it
    /// behaves like a backend that finishes anyway after being cancelled, the case that makes
    /// cancellation alone insufficient (design §12).
    /// </summary>
    internal sealed class FakeNpcDriver : INpcDecisionDriver
    {
        public readonly List<NpcTurn> Turns = new List<NpcTurn>();
        private readonly List<TaskCompletionSource<NpcReply>> pending = new List<TaskCompletionSource<NpcReply>>();

        public bool IgnoresCancellation;
        public Func<NpcTurn, NpcReply, bool> OnExecute = (turn, reply) => true;
        public int ExecutedCount;

        public string Label => "Fake";

        public Task<NpcReply> DecideAsync(NpcTurn turn, CancellationToken ct)
        {
            var tcs = new TaskCompletionSource<NpcReply>();
            Turns.Add(turn);
            pending.Add(tcs);
            if (!IgnoresCancellation)
            {
                ct.Register(() => tcs.TrySetCanceled(ct));
            }

            return tcs.Task;
        }

        public bool Execute(NpcTurn turn, NpcReply reply)
        {
            ExecutedCount++;
            return OnExecute(turn, reply);
        }

        public void Say(int turnIndex, string statement) =>
            pending[turnIndex].TrySetResult(NpcReply.Decided(new AgentDecision(ActionRegistry.None, TargetRegistry.NoTarget, statement)));

        public void Answer(int turnIndex, NpcReply reply) => pending[turnIndex].TrySetResult(reply);

        public void Throw(int turnIndex, Exception e) => pending[turnIndex].TrySetException(e);
    }

    internal static class Run
    {
        /// <summary>
        /// Awaits a turn and fails the test if it ended cancelled: the Test Framework counts a
        /// cancelled <see cref="Task"/> as passed (AGENTS.md, Lessons).
        /// </summary>
        public static async Task NotCancelled(Task turn)
        {
            try
            {
                await turn;
            }
            catch (OperationCanceledException e)
            {
                Assert.Fail($"The turn ended cancelled: {e.Message}");
            }
        }
    }
}

using System;
using System.Threading;
using UnityEngine;

namespace Agenerela
{
    /// <summary>
    /// Every decision every agent makes, as it happens (#166): what an Editor tool, such as the
    /// Decision Log (#172), or a game's own log reads, so no game has to pass its results on. It
    /// covers every <see cref="Agent"/>, one on a GameObject or one with none.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="Recorded"/> is raised once when <see cref="Agent.DecideAsync"/> returns a result,
    /// pipeline errors and guard-stopped decisions included, and once each time
    /// <see cref="Agent.Execute(DecisionResult, out ActionResult)"/> returns, saying what it did. A
    /// call that ends cancelled throws instead of returning a result, so it is never in the feed,
    /// and neither is a call <see cref="Agent.Reset"/> cancelled, or an <c>Execute</c> whose
    /// handler threw. A decision is raised on the main thread; an <c>Execute</c> on the thread
    /// that called it, which is the main thread unless the game chose otherwise.
    /// </para>
    /// <para>
    /// With no subscriber, raising costs a null check. A subscriber that throws is logged as a
    /// warning, the others are still called, and the decision is returned unchanged. The feed keeps
    /// nothing: a subscriber that wants a history keeps its own, and should copy what it needs,
    /// since a result's telemetry changes when it is executed.
    /// </para>
    /// <para>
    /// As Play mode starts, every subscriber is removed and the sequence starts again: by the
    /// domain reload, or with it off by a reset (#190, G8). An Editor window that listens across
    /// Play sessions subscribes again then, from <c>EditorApplication.playModeStateChanged</c>.
    /// </para>
    /// </remarks>
    public static class DecisionFeed
    {
        // The last sequence number given out. Interlocked, since Execute may run on any thread.
        private static long sequence;

        /// <summary>
        /// Raised for each decision returned and each one executed. See <see cref="DecisionFeed"/>
        /// for when, and on which thread.
        /// </summary>
        public static event Action<DecisionFeedEntry> Recorded;

        internal static void RaiseDecided(Agent agent, DecisionResult result)
        {
            if (Recorded != null)
            {
                Raise(agent, result, DecisionStage.Decided, null);
            }
        }

        internal static void RaiseExecuted(Agent agent, DecisionResult result, ActionResult execution)
        {
            if (Recorded != null)
            {
                Raise(agent, result, DecisionStage.Executed, execution);
            }
        }

        private static void Raise(Agent agent, DecisionResult result, DecisionStage stage, ActionResult? execution)
        {
            // Read once: a subscriber added or removed meanwhile counts from the next entry.
            var subscribers = Recorded;
            if (subscribers == null)
            {
                return;
            }

            var entry = new DecisionFeedEntry(agent, result, stage, execution, DateTime.UtcNow, Interlocked.Increment(ref sequence));

            // One at a time, so a subscriber that throws cannot keep the entry from the rest.
            foreach (var subscriber in subscribers.GetInvocationList())
            {
                try
                {
                    ((Action<DecisionFeedEntry>)subscriber)(entry);
                }
                catch (Exception e)
                {
                    Debug.LogWarning(
                        $"A DecisionFeed.Recorded subscriber, {subscriber.Method.DeclaringType?.Name}.{subscriber.Method.Name}, " +
                        $"threw on entry {entry.Sequence}. The decision is unchanged. {e.GetType().Name}: {e.Message}");
                }
            }
        }

        // Before anything else as Play mode starts. Without a domain reload, statics keep their
        // values from the session before, so a game's subscriber from the last Play session would
        // still be called, on agents that no longer exist (#190, G8).
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        internal static void ResetForPlaySession()
        {
            Recorded = null;
            Interlocked.Exchange(ref sequence, 0);
        }
    }

    /// <summary>Which of the two moments a <see cref="DecisionFeedEntry"/> records.</summary>
    public enum DecisionStage
    {
        /// <summary><see cref="Agent.DecideAsync"/> returned the result.</summary>
        Decided,

        /// <summary>
        /// <see cref="Agent.Execute(DecisionResult, out ActionResult)"/> carried the result out, or
        /// refused it: <see cref="DecisionFeedEntry.Execution"/> says which.
        /// </summary>
        Executed,
    }

    /// <summary>One entry of the <see cref="DecisionFeed"/>: which agent, which result, at which moment.</summary>
    public sealed class DecisionFeedEntry
    {
        internal DecisionFeedEntry(Agent agent, DecisionResult result, DecisionStage stage, ActionResult? execution, DateTime time, long sequence)
        {
            Agent = agent;
            Result = result;
            Stage = stage;
            Execution = execution;
            Time = time;
            Sequence = sequence;
        }

        /// <summary>
        /// The agent that decided, or that executed the result: for <see cref="DecisionStage.Executed"/>,
        /// the agent <c>Execute</c> was called on. <c>AgentBehaviour.Find</c> gives its GameObject,
        /// when it has one.
        /// </summary>
        public Agent Agent { get; }

        /// <summary>
        /// The result itself, the same object the caller holds, not a copy. Its
        /// <see cref="DecisionResult.Telemetry"/> changes when the result is executed.
        /// </summary>
        public DecisionResult Result { get; }

        /// <summary>Whether this entry is the decision or its execution.</summary>
        public DecisionStage Stage { get; }

        /// <summary>
        /// For <see cref="DecisionStage.Executed"/>, what <c>Execute</c> did: whether the handler
        /// ran (<see cref="ActionResult.HandlerRan"/>), or why it refused, in
        /// <see cref="ActionResult.Status"/> and <see cref="ActionResult.Reason"/>. Null for
        /// <see cref="DecisionStage.Decided"/>.
        /// </summary>
        public ActionResult? Execution { get; }

        /// <summary>When the entry was raised, in UTC.</summary>
        public DateTime Time { get; }

        /// <summary>
        /// Rises by one with each entry raised to a subscriber, across every agent, from 1 at the
        /// start of each Play session. Two entries raised at once on different threads may reach a
        /// subscriber out of order.
        /// </summary>
        public long Sequence { get; }
    }
}

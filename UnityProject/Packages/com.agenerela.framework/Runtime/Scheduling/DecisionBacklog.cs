using System;

namespace Agenerela
{
    /// <summary>
    /// What <see cref="DecisionBacklog"/> does with a call that arrives while it is full.
    /// </summary>
    public enum BacklogOverflow
    {
        /// <summary>
        /// The oldest waiting call that is not more urgent than the new one makes way: a background
        /// one first, then, for a player-facing newcomer, a player-facing one. It ends with no
        /// decision, as <see cref="PipelineErrorKind.Superseded"/>. A background call never pushes
        /// out a player-facing one; when only those wait, it is refused instead. After the backlog
        /// was made smaller, as many make way as it is over, or, if too few may, none do and the
        /// new call is refused.
        /// </summary>
        DropOldest,

        /// <summary>The new call is refused at once, as <see cref="PipelineErrorKind.BacklogFull"/>.</summary>
        RefuseNewest,
    }

    /// <summary>
    /// How an agent treats calls to <see cref="Agent.DecideAsync"/> made while it is already
    /// deciding (#190, G5), set in <see cref="Agent.Backlog"/>. Null there, the default, keeps
    /// today's behaviour: every call starts at once and they decide side by side.
    /// </summary>
    /// <remarks>
    /// <para>
    /// With a backlog, the agent decides one call at a time. A call made meanwhile waits, up to
    /// <see cref="Capacity"/> of them, player-facing before background and first come, first
    /// served within each, as the provider's queue orders them (DR-015). It is prepared only when
    /// its turn comes, so it sees the targets and state of that moment, and the memory of the
    /// decision before it. When the backlog is full, <see cref="WhenFull"/> drops a waiting call or
    /// refuses the new one; either ends with no decision, before anything is sent, and logs
    /// nothing. Replacing the backlog with a smaller one keeps the calls already waiting; the next
    /// call made finds it over capacity, and the rule applies to all of them at once. The decision under way is never dropped: its own token is how to stop it, while it
    /// waits in the provider's queue or once it is sent.
    /// </para>
    /// <para>
    /// The three policies the demos needed are the presets: <see cref="LatestWins"/>, an event
    /// that replaces the one waiting (GreyBoxVillage's pending event); <see cref="DropOldest"/>,
    /// several waiting, the oldest making way; and <see cref="QueueWithCap"/>, several waiting, the
    /// newest refused when full. A capacity of 0 turns every call away while the agent decides.
    /// </para>
    /// <para>
    /// It counts only calls made while it is set. It never asks why the agent is called (DR-008):
    /// what it keeps or drops follows only the order of the calls and their
    /// <see cref="DecideOptions.Priority"/>.
    /// </para>
    /// </remarks>
    public sealed class DecisionBacklog
    {
        /// <summary>One call may wait, and a newer one takes its place: the latest wins.</summary>
        public static readonly DecisionBacklog LatestWins = new DecisionBacklog(1, BacklogOverflow.DropOldest);

        /// <param name="capacity">How many calls may wait behind the decision under way. 0 turns every one away.</param>
        /// <param name="whenFull">What a call that finds it full does.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="capacity"/> is below 0, or <paramref name="whenFull"/> is not a defined value.</exception>
        public DecisionBacklog(int capacity, BacklogOverflow whenFull)
        {
            if (capacity < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "A backlog cannot hold fewer than 0 calls.");
            }

            if (whenFull != BacklogOverflow.DropOldest && whenFull != BacklogOverflow.RefuseNewest)
            {
                throw new ArgumentOutOfRangeException(nameof(whenFull), whenFull, "Not a BacklogOverflow value.");
            }

            Capacity = capacity;
            WhenFull = whenFull;
        }

        /// <summary>How many calls may wait behind the decision under way.</summary>
        public int Capacity { get; }

        /// <summary>What a call that finds the backlog full does.</summary>
        public BacklogOverflow WhenFull { get; }

        /// <summary>Up to <paramref name="capacity"/> calls wait; when full, the oldest makes way for the new one.</summary>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="capacity"/> is below 0.</exception>
        public static DecisionBacklog DropOldest(int capacity)
        {
            return new DecisionBacklog(capacity, BacklogOverflow.DropOldest);
        }

        /// <summary>Up to <paramref name="capacity"/> calls wait; when full, the new one is refused.</summary>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="capacity"/> is below 0.</exception>
        public static DecisionBacklog QueueWithCap(int capacity)
        {
            return new DecisionBacklog(capacity, BacklogOverflow.RefuseNewest);
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return $"{Capacity} waiting, {WhenFull} when full";
        }
    }
}

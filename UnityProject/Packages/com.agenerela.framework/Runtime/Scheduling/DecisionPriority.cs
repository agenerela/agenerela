namespace Agenerela
{
    /// <summary>
    /// Whether anyone is waiting on a decision, which the caller declares in
    /// <see cref="DecideOptions.Priority"/> (DR-015). The queue in front of each provider sends a
    /// player-facing request before any background one, and keeps the order of arrival within each.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The framework never asks why an agent is being asked (DR-008). A player's question, an
    /// ambient tick, a behaviour-tree node and a turn report all look alike to it; this is the one
    /// thing the caller tells it about the call, so that a local model answering one request at a
    /// time answers the one a human is waiting for first.
    /// </para>
    /// <para>
    /// Going first means jumping the queue, never aborting a request already sent. Recorded in
    /// <see cref="DecisionTelemetry.Priority"/>, and serialized as its position, so a new tier goes
    /// at the end. More tiers are DR-015's revisit clause, if two prove too coarse.
    /// </para>
    /// </remarks>
    public enum DecisionPriority
    {
        /// <summary>
        /// Someone is waiting on the answer, such as a player who just spoke to the agent. The
        /// default, so a developer who never sets a priority gets the responsive behaviour.
        /// </summary>
        PlayerFacing,

        /// <summary>
        /// Nobody is waiting: ambient behaviour, a timer, a turn the game can finish later. Sent
        /// only when no player-facing request is queued.
        /// </summary>
        Background,
    }
}

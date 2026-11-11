using System;

namespace Agenerela
{
    /// <summary>
    /// What carrying out one decision did (DR-018): whether the game's code did it, why not, and
    /// what it made. A handler returns one through <see cref="IActionHandlerWithResult"/>, a
    /// <see cref="DelegateActionHandler.WithResult"/> function or an <c>[AgentAction]</c> method,
    /// and <see cref="Agent.Execute(DecisionResult, out ActionResult)"/> hands it to the caller.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A handler makes only <see cref="ExecutionStatus.Done"/>, with <see cref="Done"/>, and
    /// <see cref="ExecutionStatus.Failed"/>, with <see cref="Failed"/>. <c>default</c> is
    /// <see cref="ExecutionStatus.Done"/> with no payload, which is also what a handler that
    /// reports nothing counts as. The agent adds the other two: <see cref="ExecutionStatus.NoAction"/>
    /// for <c>none</c>, and <see cref="ExecutionStatus.Refused"/> when no handler ran.
    /// </para>
    /// <para>
    /// <see cref="Reason"/> goes into the decision's telemetry, word for word, and the payload does
    /// not: it is the game's own object, and telemetry is plain data.
    /// </para>
    /// </remarks>
    public readonly struct ActionResult
    {
        private ActionResult(ExecutionStatus status, string reason, object payload, string availabilityReason = null)
        {
            Status = status;
            Reason = reason;
            Payload = payload;
            AvailabilityReason = availabilityReason;
        }

        /// <summary>What happened. <see cref="ExecutionStatus.Done"/> for <c>default</c>.</summary>
        public ExecutionStatus Status { get; }

        /// <summary>
        /// Why it did not happen: the handler's own words for <see cref="ExecutionStatus.Failed"/>,
        /// the agent's for <see cref="ExecutionStatus.Refused"/>. Null for
        /// <see cref="ExecutionStatus.Done"/> and <see cref="ExecutionStatus.NoAction"/>.
        /// </summary>
        public string Reason { get; }

        /// <summary>
        /// For a <see cref="ExecutionStatus.Refused"/> that the handler's per-target check caused
        /// (<see cref="ITargetAvailability"/>, <see cref="AvailableForAttribute"/> or
        /// <see cref="DelegateActionHandler.IsAvailableFor"/>), the reason that check gave in its
        /// <see cref="Availability.No(string)"/>, word for word, such as <c>"the ford is flooded"</c>
        /// (#244 item 6). Null for every other refusal and status, and when the check said no without
        /// a reason.
        /// </summary>
        /// <remarks>
        /// <see cref="Reason"/> is the agent's sentence for a log, with the action and target ids in
        /// it. This is the game's own words alone, so a game can write its own line from it: "That
        /// offer no longer stands: the ford is flooded." A refusal with no such reason, an action no
        /// longer available at all say, leaves the game to write a line of its own.
        /// </remarks>
        public string AvailabilityReason { get; }

        /// <summary>
        /// What the handler made or wants the caller to have, such as the quote it offered or the
        /// gold it was short of. Null when it gave none, and always for a result the agent made.
        /// </summary>
        public object Payload { get; }

        /// <summary>
        /// Whether the decision was carried out: <see cref="ExecutionStatus.Done"/>, or
        /// <see cref="ExecutionStatus.NoAction"/>, which had nothing to run.
        /// <see cref="Agent.Execute(DecisionResult)"/> returns this.
        /// </summary>
        public bool Succeeded => Status == ExecutionStatus.Done || Status == ExecutionStatus.NoAction;

        /// <summary>
        /// Whether a handler ran: <see cref="ExecutionStatus.Done"/> or
        /// <see cref="ExecutionStatus.Failed"/>.
        /// </summary>
        public bool HandlerRan => Status == ExecutionStatus.Done || Status == ExecutionStatus.Failed;

        /// <summary>The handler did what the decision asked.</summary>
        /// <param name="payload">What it made, for the caller; null for nothing.</param>
        public static ActionResult Done(object payload = null)
        {
            return new ActionResult(ExecutionStatus.Done, null, payload);
        }

        /// <summary>
        /// The handler ran but could not do it, for a reason of the game's: the quote went stale,
        /// the purse is short. Nothing it should have changed has changed.
        /// </summary>
        /// <param name="reason">Why, in words a log can show. Recorded in the decision's telemetry.</param>
        /// <param name="payload">Anything the caller needs to explain it; null for nothing.</param>
        /// <exception cref="ArgumentException"><paramref name="reason"/> is null, empty or whitespace.</exception>
        public static ActionResult Failed(string reason, object payload = null)
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                throw new ArgumentException(
                    "A failed action needs a reason: it is what the telemetry and the caller read.", nameof(reason));
            }

            return new ActionResult(ExecutionStatus.Failed, reason, payload);
        }

        // The two the agent makes, which a handler cannot.
        internal static ActionResult NoAction()
        {
            return new ActionResult(ExecutionStatus.NoAction, null, null);
        }

        internal static ActionResult Refused(string reason, string availabilityReason = null)
        {
            return new ActionResult(ExecutionStatus.Refused, reason, null, availabilityReason);
        }

        // A handler's answer as its agent reports it. A handler can only make Done or Failed, but
        // it can pass on a result it was handed, such as another agent's Execute: a handler that
        // ran and passed on a refusal failed, and one that passed on none did nothing, which is done.
        internal static ActionResult FromHandler(ActionResult result)
        {
            switch (result.Status)
            {
                case ExecutionStatus.Refused:
                    return new ActionResult(ExecutionStatus.Failed, result.Reason, result.Payload);
                case ExecutionStatus.NoAction:
                    return new ActionResult(ExecutionStatus.Done, null, result.Payload);
                default:
                    return result;
            }
        }

        /// <summary>
        /// The payload as a <typeparamref name="T"/>, when it is one. False, with
        /// <paramref name="payload"/> left default, when there is none or it is another type.
        /// </summary>
        public bool TryGetPayload<T>(out T payload)
        {
            if (Payload is T typed)
            {
                payload = typed;
                return true;
            }

            payload = default;
            return false;
        }

        /// <summary>The status, then the reason when there is one: <c>Failed: the quote went stale</c>.</summary>
        public override string ToString()
        {
            return Reason == null ? Status.ToString() : $"{Status}: {Reason}";
        }
    }

    /// <summary>
    /// What <see cref="Agent.Execute(DecisionResult, out ActionResult)"/> did with a decision, in
    /// <see cref="ActionResult.Status"/> and <see cref="DecisionTelemetry.Execution"/>. Serialized as
    /// its position, like <see cref="DecisionOutcome"/>, so a new status goes at the end.
    /// </summary>
    public enum ExecutionStatus
    {
        /// <summary>The handler ran and did it. Every handler that reports nothing counts as this.</summary>
        Done,

        /// <summary>The handler ran and said it could not, with its reason.</summary>
        Failed,

        /// <summary>The decision was <c>none</c>, a guard's rewrite included, so nothing ran. It counts as carried out.</summary>
        NoAction,

        /// <summary>
        /// No handler ran: the result has no decision (a pipeline error), a guard rejected it inside
        /// <see cref="Agent.DecideAsync"/>, or <see cref="Agent.Execute(DecisionResult)"/>'s re-check refused it.
        /// </summary>
        Refused,
    }
}

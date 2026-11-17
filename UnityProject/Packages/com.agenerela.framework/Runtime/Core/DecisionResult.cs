using System;
using System.Threading;
using Agenerela.Providers;

namespace Agenerela
{
    /// <summary>
    /// What <see cref="Agent.DecideAsync"/> returns: the decision the agent will act on, what the
    /// model itself answered, the guard verdict between the two, the request and reply they came
    /// from, and the telemetry of the request.
    /// </summary>
    public sealed class DecisionResult
    {
        /// <summary>
        /// What the agent will do, after the guards. <c>none</c> with <c>no_target</c> when a guard
        /// rewrote or rejected the model's answer. Null only for a pipeline error.
        /// </summary>
        public AgentDecision Decision { get; }

        /// <summary>
        /// What the model said, as parsed, before any guard. The same object as
        /// <see cref="Decision"/> when every guard passed and no stray target was dropped
        /// (<see cref="ActionDefinition.TakesNoTarget"/>, <see cref="DecisionTelemetry.TargetIgnored"/>)
        /// and no guard replaced the statement (<see cref="GuardVerdict.ReplaceStatement"/>,
        /// <see cref="DecisionTelemetry.StatementsReplaced"/>); null for a pipeline error.
        /// </summary>
        public AgentDecision ModelDecision { get; }

        /// <summary>
        /// The verdict that stopped the model's answer inside <see cref="Agent.DecideAsync"/>: the
        /// guard's name, the kind and the reason. Null when every guard passed, and for a pipeline
        /// error. Never changed afterwards: a refusal by <see cref="Agent.Execute"/>'s re-check is
        /// recorded in <see cref="Telemetry"/> instead.
        /// </summary>
        public GuardVerdict? Verdict { get; }

        /// <summary>The request's telemetry, guard verdicts included.</summary>
        public DecisionTelemetry Telemetry { get; }

        /// <summary>
        /// The request this decision sent to the provider, so a game can tell its own request
        /// from another agent's on a shared queue (#142). Set whenever the request was sent, a
        /// pipeline error after it included; null when it never was, because preparing it
        /// failed, and for a result made with the public constructor. A call cancelled before
        /// the send has no result at all.
        /// </summary>
        public DecisionRequest Request { get; }

        /// <summary>
        /// What the provider answered <see cref="Request"/> with: the model's original reply,
        /// before any guard, even when one stopped it, and also when it could not be parsed or a
        /// guard failed. Null when the provider failed or returned nothing, and whenever
        /// <see cref="Request"/> is null. Never the reply to the refusal-line request
        /// (<see cref="DecideOptions.AskForRefusalLine"/>).
        /// </summary>
        public ProviderResult Reply { get; }

        /// <summary>
        /// The call's <see cref="DecideOptions.Correlation"/>, the caller's own object, so a result
        /// says which call it answers wherever it travels (DR-018). Set on every result
        /// <see cref="Agent.DecideAsync"/> returns, pipeline errors included; null when the call set
        /// none, and for a result made with the public constructor. <see cref="Agent.Execute(DecisionResult)"/>
        /// sets it on the context it hands the handler.
        /// </summary>
        public object Correlation { get; }

        // The decision waiting to be remembered, under MemoryCommit.OnExecute; null otherwise, and
        // once settled it stays, marked, so nothing remembers it twice (DR-018).
        internal PendingMemory Pending { get; set; }

        // Whether the call chose among DecideOptions.Choices (DR-022): Execute then runs no handler
        // and no re-check, since the choices were the call's, not the agent's.
        internal bool FromChoices { get; set; }

        /// <summary>
        /// A result no guard stopped: <see cref="ModelDecision"/> is <paramref name="decision"/>
        /// and <see cref="Verdict"/> is null.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="telemetry"/> is null.</exception>
        /// <exception cref="ArgumentException">
        /// <paramref name="decision"/> is null and the outcome is not a pipeline error.
        /// </exception>
        public DecisionResult(
            AgentDecision decision,
            DecisionTelemetry telemetry)
            : this(decision, telemetry, decision, null, null, null)
        {
        }

        // A result a guard stopped, or none did when verdict is null, with the exchange it came
        // from. The guards' own rule, that a stopped decision becomes none, is Agent's; this only
        // keeps the parts consistent.
        internal DecisionResult(
            AgentDecision decision,
            DecisionTelemetry telemetry,
            AgentDecision modelDecision,
            GuardVerdict? verdict,
            DecisionRequest request,
            ProviderResult reply,
            object correlation = null)
        {
            if (telemetry == null)
            {
                throw new ArgumentNullException(nameof(telemetry));
            }

            if (decision == null &&
                telemetry.Outcome != DecisionOutcome.PipelineError)
            {
                throw new ArgumentException(
                    "Decision can be null only when the outcome is PipelineError.",
                    nameof(decision));
            }

            if (verdict.HasValue && (verdict.Value.Kind == GuardVerdictKind.Pass || decision == null || modelDecision == null))
            {
                throw new ArgumentException(
                    "A verdict is recorded only for a decision a guard stopped.",
                    nameof(verdict));
            }

            if (reply != null && request == null)
            {
                throw new ArgumentException("A reply is recorded only with the request it answered.", nameof(reply));
            }

            Decision = decision;
            ModelDecision = modelDecision;
            Verdict = verdict;
            Telemetry = telemetry;
            Request = request;
            Reply = reply;
            Correlation = correlation;
        }
    }

    // A decision held back from memory until the game settles it (MemoryCommit.OnExecute,
    // DR-018): everything IMemoryStrategy.Remember needs, as the decision had it, and the memory
    // it recalled from, so replacing Agent.Memory meanwhile changes nothing. The agent that made
    // it is named by a token of that agent's, never the agent itself, so a result kept in a log
    // keeps no agent alive. A reset of that agent drops it (#190, G12): Reset is the agent's reset
    // token from when the decision began, so a decision held across a reset is never remembered.
    internal sealed class PendingMemory
    {
        public PendingMemory(object owner, string ownerName, IMemoryStrategy memory, AgentContext context, DecisionRequest request,
            CancellationToken reset)
        {
            Owner = owner;
            OwnerName = ownerName;
            Memory = memory;
            Context = context;
            Request = request;
            Reset = reset;
        }

        // The deciding agent's token, compared by reference, and its name for messages.
        public object Owner { get; }
        public string OwnerName { get; }

        // Cancelled once the agent has been reset since the decision began.
        public CancellationToken Reset { get; }
        public IMemoryStrategy Memory { get; private set; }
        public AgentContext Context { get; private set; }
        public DecisionRequest Request { get; private set; }

        // What came of it, in the game's words, from Agent.SetOutcome or a failed Execute's reason
        // (#216 item 3): handed to an IRemembersOutcomes memory when it is remembered.
        public string Outcome { get; set; }

        // Whether Outcome is a failed Execute's reason rather than the game's own line: a later
        // Execute replaces it, and one that succeeds clears it, while the game's line stays.
        public bool OutcomeFromExecute { get; set; }

        // Committed or discarded: either way, never remembered again.
        public bool Settled { get; private set; }

        // Marks it settled and lets go of the context, whose targets may be scene objects, so a
        // result the game keeps, in a log say, does not keep them alive.
        public void Settle()
        {
            Settled = true;
            Memory = null;
            Context = null;
            Request = null;
        }
    }
}

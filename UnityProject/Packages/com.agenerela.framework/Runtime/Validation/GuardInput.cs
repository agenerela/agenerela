using System;
using Agenerela.Providers;

namespace Agenerela
{
    /// <summary>
    /// Everything a guard may read about one decision. One object rather than a list of
    /// parameters, so a guard that needs something more later adds a property here instead of
    /// changing every guard's signature.
    /// </summary>
    /// <remarks>
    /// <see cref="Agent.DecideAsync"/> fills every property. <see cref="Agent.Execute"/>'s re-check
    /// has no request and no reply, only the agent as it is when it runs, so there
    /// <see cref="Request"/> and <see cref="Reply"/> are null and <see cref="Options"/> holds the
    /// defaults. The constructor is public so a developer can test a guard of their own without an
    /// agent.
    /// </remarks>
    public sealed class GuardInput
    {
        /// <exception cref="ArgumentNullException"><paramref name="context"/> or <paramref name="decision"/> is null.</exception>
        public GuardInput(
            AgentContext context,
            DecisionRequest request,
            ProviderResult reply,
            AgentDecision decision,
            DecideOptions options,
            DecisionTelemetry telemetry)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (decision == null)
            {
                throw new ArgumentNullException(nameof(decision));
            }

            Context = context;
            Request = request;
            Reply = reply;
            Decision = decision;
            Options = options ?? new DecideOptions();
            Telemetry = telemetry ?? new DecisionTelemetry();
        }

        /// <summary>
        /// The snapshot the decision was made from: its state, its targets and its stimulus. In
        /// <see cref="Agent.Execute"/>, a snapshot taken then.
        /// </summary>
        public AgentContext Context { get; }

        /// <summary>
        /// What was sent, schema included, so <c>Request.Schema</c> says which actions and targets
        /// the model was offered. Null in <see cref="Agent.Execute"/>'s re-check.
        /// </summary>
        public DecisionRequest Request { get; }

        /// <summary>
        /// The provider's reply: its text, and its token probabilities when the provider reports
        /// them. Null in <see cref="Agent.Execute"/>'s re-check.
        /// </summary>
        public ProviderResult Reply { get; }

        /// <summary>
        /// The decision being judged, as parsed from the reply. Never the rewritten one: the
        /// pipeline stops at the first guard that does not pass it.
        /// </summary>
        public AgentDecision Decision { get; }

        /// <summary>
        /// The call's options, copied when <see cref="Agent.DecideAsync"/> was called, so changing
        /// the caller's object afterwards changes nothing here. Never null: the defaults when the
        /// caller passed none, and in <see cref="Agent.Execute"/>.
        /// </summary>
        public DecideOptions Options { get; }

        /// <summary>
        /// The telemetry the result will carry. A guard may record its own numbers here, as the
        /// grounding guard does (#105). Never null.
        /// </summary>
        public DecisionTelemetry Telemetry { get; }
    }
}

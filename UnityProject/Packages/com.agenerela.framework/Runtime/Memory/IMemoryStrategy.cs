using System.Collections.Generic;
using Agenerela.Providers;

namespace Agenerela
{
    /// <summary>
    /// What an agent remembers between decisions, and which of it goes into the next request
    /// (build plan §2.9). <c>Agent</c> asks <see cref="Recall"/> while it prepares each request,
    /// and calls <see cref="Remember"/> once a decision is final. The default is
    /// <see cref="RollingHistory"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// One instance per agent, never shared (§2.9, rule 2): ten guards share one
    /// <see cref="AgentProfile"/>, never their memories, which is why a strategy is set on
    /// <c>Agent.Memory</c> and not on the profile.
    /// </para>
    /// <para>
    /// What <see cref="Recall"/> returns is <see cref="DecisionRequest.History"/>, in the encoding
    /// written there: two entries per exchange, the stimulus as it was sent, then the decision as
    /// JSON. <c>Agent</c> rejects an odd number of entries, or a null one, as a pipeline error,
    /// because a provider with chat roles alternates <c>user</c> and <c>assistant</c> from them.
    /// </para>
    /// <para>
    /// <c>Agent</c> calls <see cref="Recall"/> on the thread that called <c>DecideAsync</c>, the
    /// main thread in a game, and <see cref="Remember"/> on the main thread, even when the
    /// provider answered on another. A strategy used that way needs no locking and may use
    /// Unity's objects.
    /// </para>
    /// </remarks>
    public interface IMemoryStrategy
    {
        /// <summary>
        /// The history for the request being prepared now, oldest first. Empty, or null, when
        /// there is none.
        /// </summary>
        /// <param name="ctx">The context the request is built from.</param>
        IReadOnlyList<string> Recall(AgentContext ctx);

        /// <summary>
        /// Records one finished decision. Called only for a decision: never for a pipeline error
        /// or a cancelled call, which did not happen.
        /// </summary>
        /// <param name="ctx">The context the request was built from.</param>
        /// <param name="request">
        /// The request that was sent, whose <see cref="DecisionRequest.Stimulus"/> is the stimulus
        /// as the model read it, label included, and whose <see cref="DecisionRequest.Schema"/>
        /// gives the field order the decision is written in.
        /// </param>
        /// <param name="result">
        /// What <c>DecideAsync</c> returns, never the provider's raw text (§2.9, rule 1), so a
        /// decision a Phase 3 guard rewrote is remembered as rewritten.
        /// </param>
        void Remember(AgentContext ctx, DecisionRequest request, DecisionResult result);
    }
}

using System.Collections.Generic;

namespace Agenerela.Providers
{
    /// <summary>
    /// What came back from one <see cref="ILLMProvider.RequestAsync"/>: the model's reply
    /// as text, plus whatever the backend was able to say about producing it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The split is deliberate. A provider knows about transport and text; it does not know
    /// what an action is. <c>Agent</c> (#17) parses <see cref="Text"/> into an
    /// <c>AgentDecision</c> and folds the rest of this type into <c>DecisionTelemetry</c>
    /// (#3) alongside the guard verdicts and the framework version. So this type carries no
    /// notion of success or correctness — a reply that parses to nonsense is still a
    /// <see cref="ProviderResult"/>, and classifying it is the pipeline's job.
    /// </para>
    /// <para>
    /// A result means a reply arrived. Transport and protocol failures throw instead
    /// (see <see cref="ILLMProvider.RequestAsync"/>), so nothing downstream has to
    /// distinguish "no result" from "empty result".
    /// </para>
    /// </remarks>
    public sealed class ProviderResult
    {
        /// <summary>
        /// The model's reply, exactly as received and not parsed, trimmed or repaired.
        /// Expected to be the JSON object the schema asked for, but a provider must hand
        /// over whatever actually arrived: the parse failures are data. A provider whose
        /// envelope wraps the reply (Ollama's <c>response</c>, a cloud vendor's candidate
        /// list) unwraps to the text and no further.
        /// </summary>
        public string Text;

        /// <summary>
        /// Wall-clock seconds from sending to reply, measured by the provider — the number
        /// a player would feel. Includes queueing and network time, and for a local backend
        /// includes loading the model if it was not resident.
        /// </summary>
        /// <remarks>
        /// Timing is only comparable between runs when one model is resident (hard rule 3):
        /// two models sharing an 8GB card flatten every latency measurement.
        /// </remarks>
        public float LatencySeconds;

        /// <summary>
        /// Tokens the prompt consumed, as counted by the backend. 0 when it does not report
        /// them. Worth having beyond cost: the prototype's largest win came from a prompt
        /// that got shorter (332 to 143 tokens) while accuracy rose, which is only visible
        /// if prompt size is recorded per decision.
        /// </summary>
        public int PromptTokens;

        /// <summary>
        /// Tokens generated, as counted by the backend. 0 when it does not report them.
        /// </summary>
        public int CompletionTokens;

        /// <summary>
        /// The model's probabilities for the reply, one entry per generated token of
        /// <see cref="Text"/>, in order. Null when the provider does not report them, which
        /// is always the case when <see cref="ProviderCapabilities.ReportsTokenProbabilities"/>
        /// is false. Read by the grounding guard (Phase 3, DR-016), which locates the target in
        /// the reply token by token.
        /// </summary>
        /// <remarks>
        /// <see cref="Text"/> stays exactly as received; this list sits beside it, never
        /// instead of it. Joined in order, the tokens' texts equal <see cref="Text"/>, except
        /// where a character was split across two tokens and is missing from both (see
        /// <see cref="TokenProbability.Text"/>). Anything that maps a token to a position in
        /// <see cref="Text"/> checks the join first. A provider leaves out entries for
        /// anything its backend generated but did not put in <see cref="Text"/>.
        /// </remarks>
        public IReadOnlyList<TokenProbability> TokenProbabilities;

        // Extension point, not now: provider-specific extras (Ollama's eval_duration,
        // a cloud vendor's request id) belong in a string-keyed bag rather than as vendor
        // fields on this type or on DecisionTelemetry (#3). Add one when a provider
        // actually needs it — Phase 2 at the earliest.
    }
}

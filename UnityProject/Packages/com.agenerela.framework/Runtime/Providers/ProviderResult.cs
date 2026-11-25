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
        /// envelope wraps the reply (Ollama's <c>message.content</c>, a cloud vendor's candidate
        /// list) unwraps to the text and no further.
        /// </summary>
        public string Text;

        /// <summary>
        /// Wall-clock seconds from sending to reply, measured by the provider. Includes queueing
        /// and network time, and for a local backend includes loading the model if it was not
        /// resident. For a request sent more than once (<see cref="Retries"/>), the attempt that
        /// answered alone: the time before it is <see cref="RetryWaitSeconds"/>.
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

        /// <summary>
        /// The model that produced the reply, as the backend names it, such as
        /// <c>"qwen3.5:2b"</c>. Null when the provider cannot say. <c>Agent</c> copies it into
        /// <c>DecisionTelemetry.Model</c> (#88).
        /// </summary>
        /// <remarks>
        /// Separate from <see cref="ILLMProvider.Name"/>, which names the provider: two providers
        /// may run the same weights, one provider runs whichever model it was configured with, and
        /// no measurement means anything without the model.
        /// </remarks>
        public string Model;

        /// <summary>
        /// How many times the request was sent again because the server said it was busy, under
        /// the provider's <see cref="RetryPolicy"/> (DR-026). 0 when the first send answered, and
        /// for a provider that never retries.
        /// </summary>
        public int Retries;

        /// <summary>
        /// The seconds from the first send to the start of the attempt that answered: the failed
        /// attempts and the pauses between them (DR-026). 0 when the first send answered.
        /// <c>Agent</c> keeps it out of <see cref="DecisionTelemetry.LatencySeconds"/>, so a busy
        /// server never reads as a slow model.
        /// </summary>
        public float RetryWaitSeconds;

        /// <summary>
        /// How many characters of thinking came back before the reply, for a provider asked to
        /// let its model think (<c>OllamaSettings.Think</c>, DR-028). 0 when there was none or the
        /// provider does not think. A length, not the text, so it can be kept and compared freely.
        /// </summary>
        public int ThinkingCharacters;

        /// <summary>
        /// The model's thinking before the reply, exactly as received, or null. A provider fills it
        /// only when asked to keep it (<c>OllamaSettings.KeepThinking</c>), so by default the text
        /// never reaches a result, a log or telemetry. Never parsed: the decision is
        /// <see cref="Text"/> alone.
        /// </summary>
        public string Thinking;

        // Extension point, not now: provider-specific extras (Ollama's eval_duration,
        // a cloud vendor's request id) belong in a string-keyed bag rather than as vendor
        // fields on this type or on DecisionTelemetry (#3). Add one when a provider
        // actually needs it — Phase 2 at the earliest.
    }
}

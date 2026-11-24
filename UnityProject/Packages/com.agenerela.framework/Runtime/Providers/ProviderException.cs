using System;

namespace Agenerela.Providers
{
    /// <summary>
    /// Why a provider's request failed, as <see cref="ProviderException.Kind"/> reports it: what a
    /// game or a tool needs to know to tell the developer what to do next.
    /// </summary>
    /// <remarks>
    /// Never a cancellation the caller asked for: that propagates as
    /// <see cref="OperationCanceledException"/> (see <see cref="ILLMProvider.RequestAsync"/>).
    /// </remarks>
    public enum ProviderErrorKind
    {
        /// <summary>No server answered: Ollama is not running, or is on another port.</summary>
        Unreachable,

        /// <summary>The server is up but does not have the model asked for.</summary>
        ModelNotFound,

        /// <summary>
        /// No reply arrived within the provider's own timeout, and the caller did not cancel.
        /// </summary>
        Timeout,

        /// <summary>
        /// Any other refusal by the server, such as an HTTP 4xx. The message gives the status code.
        /// </summary>
        Rejected,

        /// <summary>The server failed while answering, such as an HTTP 5xx.</summary>
        ServerError,

        /// <summary>
        /// A reply arrived but its envelope could not be read: not the JSON the backend's protocol
        /// promises, or missing the field the model's text is in. Never the model's own JSON
        /// failing to parse, which is <c>Agent</c>'s to read and report.
        /// </summary>
        MalformedResponse,

        /// <summary>
        /// The server kept saying it is busy (HTTP 429 or 503) until the provider's
        /// <see cref="RetryPolicy"/> ran out: its attempts, its time for pauses, or its deadline
        /// (DR-026). Nothing is broken; the server, or the gateway in front of it, is serving
        /// others. Added at the end, so no kind already logged changes number.
        /// </summary>
        RateLimited,
    }

    /// <summary>
    /// A provider's request failed, for a reason <see cref="Kind"/> names. What
    /// <see cref="ILLMProvider.RequestAsync"/> throws for a transport or protocol failure, so a
    /// model that was never pulled is told apart from a server that is not running.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>Agent.DecideAsync</c> catches it and returns a result whose telemetry records the kind,
    /// so it never reaches the game. Any other exception from a provider is caught the same way,
    /// recorded as a provider failure with no more detail; throwing this one is how a provider
    /// says why.
    /// </para>
    /// <para>
    /// The message says what to do next where it can: "Run <c>ollama pull qwen3.5:2b</c>", "Is
    /// <c>ollama serve</c> running at http://127.0.0.1:11434?". It must never contain an API key
    /// (hard rule 1): a provider redacts it before throwing, since the message is logged and
    /// stored in telemetry.
    /// </para>
    /// </remarks>
    public sealed class ProviderException : Exception
    {
        /// <param name="kind">Why the request failed.</param>
        /// <param name="providerName">The failing provider's <see cref="ILLMProvider.Name"/>. Null is recorded as empty.</param>
        /// <param name="message">What failed, and what to do about it where the provider can tell.</param>
        /// <param name="innerException">The transport's own exception, if there was one.</param>
        public ProviderException(ProviderErrorKind kind, string providerName, string message, Exception innerException = null)
            : base(message, innerException)
        {
            Kind = kind;
            ProviderName = providerName ?? "";
        }

        /// <summary>Why the request failed.</summary>
        public ProviderErrorKind Kind { get; }

        /// <summary>The <see cref="ILLMProvider.Name"/> of the provider that failed, such as <c>"ollama"</c>.</summary>
        public string ProviderName { get; }

        /// <summary>
        /// How many times the request was sent again before it failed, as
        /// <see cref="ProviderResult.Retries"/> counts them for a reply (DR-026); 0 when it was sent once.
        /// </summary>
        public int Retries { get; set; }

        /// <summary>
        /// The seconds before the last attempt began, failed attempts and pauses together, as
        /// <see cref="ProviderResult.RetryWaitSeconds"/> counts them for a reply (DR-026); 0 when it was sent once.
        /// </summary>
        public float RetryWaitSeconds { get; set; }
    }
}

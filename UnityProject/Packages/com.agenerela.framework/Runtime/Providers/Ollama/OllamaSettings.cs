namespace Agenerela.Providers
{
    /// <summary>
    /// Where <see cref="OllamaProvider"/> finds its server and how it asks: the endpoint, the
    /// model, the context size, the temperature, the timeout and any key. A plain class, so a provider can
    /// be built in code with no asset. <see cref="OllamaProviderConfig"/> holds the same fields as an
    /// asset, and builds a provider from them (#92, DR-013).
    /// </summary>
    /// <remarks>
    /// The provider copies these values when it is built, so changing a field afterwards changes
    /// nothing for a provider that already exists. Build a new provider to switch models.
    /// </remarks>
    public sealed class OllamaSettings
    {
        /// <summary>
        /// The server's base address, without <c>/api/chat</c>. <c>127.0.0.1</c>, not
        /// <c>localhost</c>: on Windows <c>localhost</c> tries IPv6 first, Ollama listens on IPv4
        /// only, and the prototype's probes waited about 2 s a request for the failed attempt
        /// (findings, 5 October).
        /// </summary>
        public string Endpoint = "http://127.0.0.1:11434";

        /// <summary>
        /// The model to ask, as <c>ollama list</c> names it. The server must already have it:
        /// a model it lacks fails with <see cref="ProviderErrorKind.ModelNotFound"/>, whose
        /// message gives the <c>ollama pull</c> command.
        /// </summary>
        /// <remarks>
        /// <c>qwen3.5:4b</c> since 9 November 2026. On the 389-prompt interim set it chose right 330
        /// times against 263 for <c>qwen3.5:2b</c>, the default until then, for about 0.08 s more a
        /// decision on an RTX 5080 and about 0.7 GB more memory (findings, 9 November, the
        /// thirteen-model sweep).
        /// </remarks>
        public string Model = "qwen3.5:4b";

        /// <summary>
        /// The context window in tokens, sent as <c>options.num_ctx</c>. 4096 is what the
        /// prototype's probes ran with.
        /// </summary>
        public int ContextSize = 4096;

        /// <summary>
        /// Sampling temperature, sent as <c>options.temperature</c>. 0 is greedy decoding, which
        /// every number in the build plan's Appendix A was measured with.
        /// </summary>
        public float Temperature = 0f;

        /// <summary>
        /// Seconds to wait for a whole reply before failing with
        /// <see cref="ProviderErrorKind.Timeout"/>. Counts from sending, so it includes loading
        /// the model when it was not resident: about 8 s for <c>qwen3.5:2b</c> on an 8 GB laptop
        /// GPU, checked on Ollama 0.34.2.
        /// </summary>
        public float TimeoutSeconds = 30f;

        /// <summary>
        /// How many alternatives each generated token's probabilities carry, sent as
        /// <c>top_logprobs</c>: 20 unless set, from 1 to 20, the most Ollama accepts (#244).
        /// </summary>
        /// <remarks>
        /// <see cref="TargetGroundingGuard"/> counts what the alternatives do not cover as off the
        /// target list. DR-016 measured it with five, and five was enough for a handful of targets;
        /// with many right at once, such as a strategy game's provinces, a model's probability
        /// spreads past five and read as doubt. Twenty since 11 November 2026: on the evaluation
        /// sets it changed no reply and no verdict on <c>qwen3.5:4b</c> or <c>qwen3.5:2b</c>, at no
        /// measurable latency, and in GreyBoxStrategy it cut the guard's containments of legal moves
        /// from 72 to 19 and from 94 to 58 (findings, 11 November). The reply's token probabilities
        /// grow from about 5 KB to 17 KB; nothing the framework writes to disk keeps them. Set 5 for
        /// what every request sent before then.
        /// </remarks>
        public int TokenAlternatives = DefaultTokenAlternatives;

        /// <summary><see cref="TokenAlternatives"/>' default, 20 (#244); DR-016 measured with 5.</summary>
        public const int DefaultTokenAlternatives = 20;

        /// <summary>The most <see cref="TokenAlternatives"/> Ollama accepts, 20.</summary>
        public const int MaxTokenAlternatives = 20;

        /// <summary>
        /// A key for a server that wants one, sent as <c>Authorization: Bearer &lt;key&gt;</c> on
        /// every request, or empty to send none. A local Ollama wants none. The team's RTX 5080
        /// server, behind the gateway in <c>tools/ollama-server/</c>, does.
        /// </summary>
        /// <remarks>
        /// Read it from somewhere outside the project, such as an environment variable, never from
        /// a committed file (hard rule 1). <see cref="OllamaProviderConfig"/> stores only the
        /// variable's name. No message the provider writes contains the key.
        /// </remarks>
        public string ApiKey = "";
    }
}

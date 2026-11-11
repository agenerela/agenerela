using System;
using UnityEngine;

namespace Agenerela.Providers
{
    /// <summary>
    /// An <see cref="OllamaProvider"/> as an asset: <see cref="OllamaSettings"/>' fields, with the
    /// same defaults, plus <see cref="ProviderConfig.MaxInFlight"/>. Made with
    /// <b>Create → Agenerela → Ollama Provider</b>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Nothing here is secret. A server that wants a key, such as the team's RTX 5080 behind
    /// <c>tools/ollama-server/</c>, gets it from the environment variable
    /// <see cref="ApiKeyVariable"/> names, read when the provider is built. The asset stores only
    /// that name (hard rule 1, <see cref="ProviderConfig"/>).
    /// </para>
    /// <para>
    /// <see cref="ProviderConfig"/> says when an edit reaches the provider. A variable set after
    /// the Editor started does not reach it: the Editor, and Unity Hub if it launched the Editor,
    /// read their environment once, when they start.
    /// </para>
    /// </remarks>
    [CreateAssetMenu(menuName = "Agenerela/Ollama Provider", fileName = "OllamaLocal")]
    public sealed class OllamaProviderConfig : ProviderConfig
    {
        // The defaults come from OllamaSettings, so a config and a provider built in code start alike.
        private static readonly OllamaSettings Defaults = new OllamaSettings();

        [Tooltip("The server's base address, without /api/chat. Use 127.0.0.1 rather than " +
                 "localhost: on Windows, localhost tries IPv6 first and costs about 2 s a request.")]
        public string Endpoint = Defaults.Endpoint;

        [Tooltip("The model to ask, as `ollama list` names it. The server must already have it " +
                 "(`ollama pull <model>`).")]
        public string Model = Defaults.Model;

        [Min(1)]
        [Tooltip("The context window in tokens, sent as options.num_ctx.")]
        public int ContextSize = Defaults.ContextSize;

        [Min(0f)]
        [Tooltip("Sampling temperature. 0 is greedy decoding, which every accuracy figure in the " +
                 "build plan was measured with.")]
        public float Temperature = Defaults.Temperature;

        [Min(0.001f)]
        [Tooltip("Seconds to wait for a whole reply, counted from sending, so it includes loading " +
                 "a model that was not resident.")]
        public float TimeoutSeconds = Defaults.TimeoutSeconds;

        [Range(1, OllamaSettings.MaxTokenAlternatives)]
        [Tooltip("How many alternatives each token's probabilities carry (top_logprobs), which the " +
                 "grounding guard reads. 20, the most Ollama accepts, unless set; 5 is what every request " +
                 "sent before 11 November 2026.")]
        public int TokenAlternatives = Defaults.TokenAlternatives;

        [Tooltip("For a server that wants a key: the name of the environment variable holding it, " +
                 "such as AGENERELA_OLLAMA_API_KEY. Empty for a local Ollama. The key itself is never " +
                 "stored in the asset. Restart Unity, and Unity Hub, after setting the variable.")]
        public string ApiKeyVariable = "";

        /// <summary>
        /// This config's values as <see cref="OllamaSettings"/>, a new object each call, with
        /// <see cref="OllamaSettings.ApiKey"/> read from <see cref="ApiKeyVariable"/> when it names one.
        /// </summary>
        /// <exception cref="ArgumentException">
        /// <see cref="ApiKeyVariable"/> names a variable that is not set, or is empty.
        /// </exception>
        public OllamaSettings ToSettings()
        {
            return new OllamaSettings
            {
                Endpoint = Endpoint,
                Model = Model,
                ContextSize = ContextSize,
                Temperature = Temperature,
                TimeoutSeconds = TimeoutSeconds,
                // Clamped, so an asset edited by hand or by code to a value Ollama refuses still
                // builds a provider, rather than failing at the first decision (#244).
                TokenAlternatives = Mathf.Clamp(TokenAlternatives, 1, OllamaSettings.MaxTokenAlternatives),
                ApiKey = ReadKey(),
            };
        }

        /// <summary>A new <see cref="OllamaProvider"/> from <see cref="ToSettings"/>.</summary>
        /// <exception cref="ArgumentException">
        /// A setting is out of range, as <see cref="OllamaProvider"/> checks, or
        /// <see cref="ApiKeyVariable"/> names a variable that is not set.
        /// </exception>
        protected override ILLMProvider CreateProvider()
        {
            return new OllamaProvider(ToSettings());
        }

        // A named variable that is missing fails here rather than as a 401 on the first decision,
        // which could not say which variable to set.
        private string ReadKey()
        {
            string variable = (ApiKeyVariable ?? "").Trim();
            if (variable.Length == 0)
            {
                return "";
            }

            string key = Environment.GetEnvironmentVariable(variable);
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException(
                    $"API Key Variable names {variable}, but that environment variable is not set here. " +
                    "Set it, then restart Unity and Unity Hub, which read their environment when they start. " +
                    "Clear the field for a server that wants no key.");
            }

            return key.Trim();
        }
    }
}

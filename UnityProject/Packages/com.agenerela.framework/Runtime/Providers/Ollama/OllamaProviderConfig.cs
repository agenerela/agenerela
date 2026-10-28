using UnityEngine;

namespace Agenerela.Providers
{
    /// <summary>
    /// An <see cref="OllamaProvider"/> as an asset: <see cref="OllamaSettings"/>' fields, with the
    /// same defaults, plus <see cref="ProviderConfig.MaxInFlight"/>. Made with
    /// <b>Create → Agenerela → Ollama Provider</b>.
    /// </summary>
    /// <remarks>
    /// Ollama needs no API key, so nothing here is secret. <see cref="ProviderConfig"/> says when an
    /// edit reaches the provider.
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

        /// <summary>This config's values as <see cref="OllamaSettings"/>, a new object each call.</summary>
        public OllamaSettings ToSettings()
        {
            return new OllamaSettings
            {
                Endpoint = Endpoint,
                Model = Model,
                ContextSize = ContextSize,
                Temperature = Temperature,
                TimeoutSeconds = TimeoutSeconds,
            };
        }

        /// <summary>A new <see cref="OllamaProvider"/> from <see cref="ToSettings"/>.</summary>
        /// <exception cref="System.ArgumentException">A setting is out of range, as <see cref="OllamaProvider"/> checks.</exception>
        protected override ILLMProvider CreateProvider()
        {
            return new OllamaProvider(ToSettings());
        }
    }
}

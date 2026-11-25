using System;
using System.Globalization;
using Agenerela.Providers;

namespace GreyBoxStrategy
{
    /// <summary>
    /// The run switch for the framework's native thinking (DR-028): with <c>AGENERELA_OLLAMA_THINK=1</c> in the
    /// environment, every provider the game builds for a model seat sends <c>think: true</c>, for the off-vs-on
    /// game-table A/B. Unset, nothing changes. Per process, so the two arms run as separate blocks.
    /// </summary>
    /// <remarks>
    /// Thinking is the model's own channel beside the reply, not a field in the schema. On qwen3.5:4b it thought
    /// until a 4096 context ran out in 18 of 19 probe requests (findings, 24 November), so with it on the context
    /// is at least 16384 and each try may take 300 s. <c>AGENERELA_OLLAMA_CTX</c> sets the context for both arms,
    /// so the control can match it.
    /// </remarks>
    public static class ThinkSwitch
    {
        public const string ThinkVariable = "AGENERELA_OLLAMA_THINK";
        public const string ContextVariable = "AGENERELA_OLLAMA_CTX";

        /// <summary>True when <see cref="ThinkVariable"/> is <c>1</c> or <c>true</c>.</summary>
        public static bool On
        {
            get
            {
                string v = (Environment.GetEnvironmentVariable(ThinkVariable) ?? "").Trim();
                return v == "1" || string.Equals(v, "true", StringComparison.OrdinalIgnoreCase);
            }
        }

        /// <summary>The settings, changed as the two variables ask; the same object.</summary>
        public static OllamaSettings Apply(OllamaSettings settings)
        {
            if (int.TryParse(Environment.GetEnvironmentVariable(ContextVariable), NumberStyles.Integer, CultureInfo.InvariantCulture, out int ctx) && ctx > 0)
                settings.ContextSize = ctx;
            if (!On) return settings;
            settings.Think = true;
            settings.ContextSize = Math.Max(settings.ContextSize, 16384);
            settings.TimeoutSeconds = Math.Max(settings.TimeoutSeconds, 300f);
            return settings;
        }
    }
}

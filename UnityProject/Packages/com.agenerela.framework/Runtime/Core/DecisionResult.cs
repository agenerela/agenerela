using System;
using System.Collections.Generic;
using Agenerela;

namespace Agenerela
{
    /// <summary>
    /// Holds metrics, provider details, and guard execution history for a decision request.
    /// </summary>
    [Serializable]
    public sealed class DecisionTelemetry
    {
        public float LatencySeconds;
        public int PromptTokens;
        public int CompletionTokens;
        public string ProviderName = string.Empty;
        public string FrameworkVersion = string.Empty;
        public List<string> GuardsFired = new List<string>();

        /// <summary>
        /// Null at runtime. Assigned during evaluation harness (Phase 4) runs.
        /// </summary>
        public DecisionOutcome? Outcome;

        /// <summary>
        /// Provider-specific overflow bag (e.g., Ollama's eval_duration, cloud request IDs).
        /// </summary>
        public Dictionary<string, string> Extras = new Dictionary<string, string>();
    }
}
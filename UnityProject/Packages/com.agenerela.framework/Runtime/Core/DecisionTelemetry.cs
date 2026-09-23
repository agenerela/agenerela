using System;
using System.Collections.Generic;


namespace Agenerela{
    [System.Serializable]
    public sealed class DecisionTelemetry
    {
        public float LatencySeconds; // Measured around proviced request
        public int PromptTokens; // Come from provider response (when available)
        public int CompletionTokens; 
        public string ProviderName = string.Empty; // Checks provider name, such as Ollama 
        public string FrameworkVersion = string.Empty; // AgenerelaInfo.version
        public List<string> GuardsFired = new List<string>();
        public DecisionOutcome? Outcome; // Outcome == null during normal runtime orperations

        public DecisionTelemetry()
        {
            LatencySeconds = 0f;
            PromptTokens = 0;
            CompletionTokens = 0;
            ProviderName = string.Empty;
            FrameworkVersion = string.Empty;
            GuardsFired = new List<string>();
            GuardsFired = new List<string>();
            Outcome = null;


        }
    }
}
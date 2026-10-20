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
        public string Model = string.Empty; // The model that answered, as the provider reported it, such as qwen3.5:2b; empty when it did not say (#88)
        public string FrameworkVersion = string.Empty; // AgenerelaInfo.version
        public List<string> GuardsFired = new List<string>();
        public List<string> TargetsDropped = new List<string>(); // Target ids a source found but left out because of its cap (#32)
        public DecisionOutcome? Outcome; // Outcome == null during normal runtime orperations
        public PipelineErrorKind? ErrorKind; // Why there is no decision: null unless Outcome is PipelineError (#87)
        public string ErrorMessage; // The warning Agent logged for that error, word for word; null unless Outcome is PipelineError

        public DecisionTelemetry()
        {
            LatencySeconds = 0f;
            PromptTokens = 0;
            CompletionTokens = 0;
            ProviderName = string.Empty;
            Model = string.Empty;
            FrameworkVersion = string.Empty;
            GuardsFired = new List<string>();
            TargetsDropped = new List<string>();
            Outcome = null;
            ErrorKind = null;
            ErrorMessage = null;


        }
    }

    // Why a decision ended as a PipelineError, recorded in DecisionTelemetry.ErrorKind (#87).
    // Serialized as its position, like DecisionOutcome, so a new kind goes at the end.
    public enum PipelineErrorKind
    {
        // Building the request failed before anything was sent: a target source offering an id
        // twice, or an availability check that threw
        RequestPreparation,

        // The provider reached no server (ProviderErrorKind.Unreachable)
        ProviderUnreachable,

        // The server does not have the model (ProviderErrorKind.ModelNotFound)
        ModelNotFound,

        // No reply within the provider's own timeout (ProviderErrorKind.Timeout)
        ProviderTimeout,

        // The server refused the request (ProviderErrorKind.Rejected)
        ProviderRejected,

        // The server failed while answering (ProviderErrorKind.ServerError)
        ProviderServerError,

        // A reply arrived but its envelope could not be read (ProviderErrorKind.MalformedResponse)
        ProviderMalformedResponse,

        // The provider failed with an exception that is not a ProviderException, or no result at all
        ProviderFailed,

        // A reply arrived, but it is empty, not one JSON object, or lacks a field the schema asked for
        UnreadableReply
    }
}

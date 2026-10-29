using System;
using System.Collections.Generic;


namespace Agenerela{
    [System.Serializable]
    public sealed class DecisionTelemetry
    {
        public float LatencySeconds; // Measured around the provider's request, from the send: its time alone, never the queue's (#89)
        public int PromptTokens; // Come from provider response (when available)
        public int CompletionTokens; 
        public string ProviderName = string.Empty; // Checks provider name, such as Ollama 
        public string Model = string.Empty; // The model that answered, as the provider reported it, such as qwen3.5:2b; empty when it did not say (#88)
        public string ProviderConfig = string.Empty; // The name of the provider config asset whose provider answered, such as OllamaLocal; empty when the provider was built in code (#92, DR-013)
        public string FrameworkVersion = string.Empty; // AgenerelaInfo.version
        public List<string> GuardsFired = new List<string>(); // Each guard that did not pass the decision, in order: at most one from DecideAsync, then "SchemaLegalityGuard@Execute" for each refusal by Execute's re-check (#106)
        public GuardVerdictKind? Verdict; // The verdict of the guard that stopped the decision, or of Execute's re-check when it refused; null when every guard passed (#106)
        public string GuardReason; // That guard's reason, word for word; null when every guard passed (#106)
        public List<string> TargetsDropped = new List<string>(); // Target ids a source found but left out because of its cap (#32)
        public List<string> ObservationsDropped = new List<string>(); // Observation lines a source had but the request does not carry, in source order: for each source, its lines Agent.MaxObservations cut, then those its own cap left out, from IReportsDroppedObservations (#91)
        public int HistoryTurnsSent; // Exchanges of memory sent in DecisionRequest.History, two entries each (#90)
        public int HistoryTurnsDropped; // Exchanges the memory still held but left out of this request, from IReportsDroppedTurns; 0 when it cannot say (#90)
        public int HistoryTurnsForgotten; // Exchanges the memory has let go of since it was made, a rising total, from IReportsDroppedTurns; 0 when it cannot say (#90)
        public DecisionOutcome? Outcome; // Outcome == null during normal runtime orperations
        public PipelineErrorKind? ErrorKind; // Why there is no decision: null unless Outcome is PipelineError (#87)
        public string ErrorMessage; // The warning Agent logged for that error, word for word; null unless Outcome is PipelineError
        public DecisionPriority Priority; // DecideOptions.Priority, as the call declared it (DR-015)
        public float QueueWaitSeconds; // From joining the provider's queue to being sent; 0 when it never reached the queue (#89)
        public int QueuedAhead; // Requests ahead when it joined: those in flight, and those queued that go before it (#89)
        public float? TargetGrounding; // The lowest share of the model's probability at the target that stayed on the target list, from TargetGroundingGuard; null unless TargetGroundingStatus is Scored (#105)
        public GroundingStatus? TargetGroundingStatus; // Whether that share was read, or why not, Off included; null when the decision never reached the guard (#105)
        public bool TargetRuleSent; // Whether the system prompt carried the target rule, the fallback sent when CheckTarget is on and the provider reports no token probabilities; false for a decision made under the guard (#107)

        public DecisionTelemetry()
        {
            LatencySeconds = 0f;
            PromptTokens = 0;
            CompletionTokens = 0;
            ProviderName = string.Empty;
            Model = string.Empty;
            ProviderConfig = string.Empty;
            FrameworkVersion = string.Empty;
            GuardsFired = new List<string>();
            Verdict = null;
            GuardReason = null;
            TargetsDropped = new List<string>();
            ObservationsDropped = new List<string>();
            HistoryTurnsSent = 0;
            HistoryTurnsDropped = 0;
            HistoryTurnsForgotten = 0;
            Outcome = null;
            ErrorKind = null;
            ErrorMessage = null;
            Priority = DecisionPriority.PlayerFacing;
            QueueWaitSeconds = 0f;
            QueuedAhead = 0;
            TargetGrounding = null;
            TargetGroundingStatus = null;
            TargetRuleSent = false;

        }
    }

    // Why a decision ended as a PipelineError, recorded in DecisionTelemetry.ErrorKind (#87).
    // Serialized as its position, like DecisionOutcome, so a new kind goes at the end.
    public enum PipelineErrorKind
    {
        // Building the request failed before anything was sent: a target source offering an id
        // twice, or an observation source or an availability check that threw
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

using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;

namespace Agenerela.Evaluation
{
    /// <summary>
    /// One prompt asked in one arm: what was asked, what was expected, what the model said, what
    /// the guards made of it, and the outcome it was scored as.
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    public sealed class EvalAnswer
    {
        /// <summary>The arm's name.</summary>
        [JsonProperty("arm")] public string Arm;

        [JsonProperty("promptId")] public string PromptId;
        [JsonProperty("category")] public string Category;
        [JsonProperty("label")] public string Label;
        [JsonProperty("stimulus")] public string Stimulus;

        /// <summary>The prompt's <c>preconditionState</c>, as it was set on <see cref="Agent.State"/>.</summary>
        [JsonProperty("state")] public JObject State;

        /// <summary>The lines the state was told to the model as, from <see cref="EvalRunner.DescribeState"/>.</summary>
        [JsonProperty("stateLines")] public List<string> StateLines = new List<string>();

        /// <summary>Whether this call checked its target: the arm's setting and the prompt's, both on.</summary>
        [JsonProperty("checkTarget")] public bool CheckTarget;

        [JsonProperty("expectedAction")] public string ExpectedAction;

        /// <summary>The expected target, an open choice as its ids joined with <c>|</c>.</summary>
        [JsonProperty("expectedTarget")] public string ExpectedTarget;

        [JsonProperty("outcome"), JsonConverter(typeof(StringEnumConverter))] public DecisionOutcome Outcome;

        /// <summary>What the model answered, before any guard; null for a pipeline error.</summary>
        [JsonProperty("modelAction")] public string ModelAction;
        [JsonProperty("modelTarget")] public string ModelTarget;
        [JsonProperty("modelStatement")] public string ModelStatement;

        /// <summary>What the agent would do, after the guards; null for a pipeline error.</summary>
        [JsonProperty("action")] public string Action;
        [JsonProperty("target")] public string Target;

        /// <summary>The guard that stopped the model's answer, its verdict and its reason; null when none did.</summary>
        [JsonProperty("guard")] public string Guard;
        [JsonProperty("guardVerdict")] public string GuardVerdict;
        [JsonProperty("guardReason")] public string GuardReason;

        [JsonProperty("targetGrounding")] public float? TargetGrounding;
        [JsonProperty("targetGroundingStatus")] public string TargetGroundingStatus;

        /// <summary>Why there was no decision; null unless the outcome is a pipeline error.</summary>
        [JsonProperty("errorKind")] public string ErrorKind;
        [JsonProperty("errorMessage")] public string ErrorMessage;

        /// <summary>The model that answered, as the provider reported it.</summary>
        [JsonProperty("model")] public string Model;

        [JsonProperty("latencySeconds")] public float LatencySeconds;

        /// <summary>
        /// The decision itself, its request, reply and telemetry included, whose
        /// <see cref="DecisionTelemetry.Outcome"/> is <see cref="Outcome"/>. Not written to JSON, so
        /// null in a report read back with <see cref="EvalReport.FromJson"/>.
        /// </summary>
        public DecisionResult Result;
    }
}

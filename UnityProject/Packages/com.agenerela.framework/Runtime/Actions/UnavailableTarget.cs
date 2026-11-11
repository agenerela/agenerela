using System;
using Newtonsoft.Json;

namespace Agenerela
{
    /// <summary>
    /// One action and target pair an <see cref="ITargetAvailability"/> check rejected for a decision,
    /// and why (#190 LV #19, #216 item 5). Recorded in <see cref="DecisionTelemetry.TargetsUnavailable"/>
    /// and shown by <see cref="AgentPreview.TargetsUnavailable"/>, so a tool can say "left out, because ...".
    /// </summary>
    [Serializable]
    public sealed class UnavailableTarget
    {
        /// <summary>The action's id.</summary>
        [JsonProperty("action")] public string Action;

        /// <summary>The target's id.</summary>
        [JsonProperty("target")] public string Target;

        /// <summary>The check's reason, word for word; null when it gave none.</summary>
        [JsonProperty("reason", NullValueHandling = NullValueHandling.Ignore)] public string Reason;

        public UnavailableTarget()
        {
        }

        public UnavailableTarget(string action, string target, string reason)
        {
            Action = action;
            Target = target;
            Reason = reason;
        }

        /// <summary>"action -> target: reason", or without the reason when there is none.</summary>
        public override string ToString()
        {
            string pair = $"{Action} -> {Target}";
            return Reason == null ? pair : pair + ": " + Reason;
        }
    }
}

using System;
using Newtonsoft.Json;

namespace Agenerela
{
    /// <summary>
    /// One value of one action's parameter that a per-value check (<see cref="IValueAvailability"/>)
    /// rejected when a decision was prepared, with its reason (DR-023). Recorded in
    /// <see cref="DecisionTelemetry.ValuesUnavailable"/> and the decision log's <c>valuesUnavailable</c>.
    /// </summary>
    [Serializable]
    public sealed class UnavailableValue
    {
        /// <summary>The action's id.</summary>
        [JsonProperty("action")] public string Action;

        /// <summary>The parameter's name.</summary>
        [JsonProperty("parameter")] public string Parameter;

        /// <summary>The value rejected.</summary>
        [JsonProperty("value")] public string Value;

        /// <summary>The check's reason, or null when it gave none.</summary>
        [JsonProperty("reason", NullValueHandling = NullValueHandling.Ignore)] public string Reason;

        /// <summary>For deserialization.</summary>
        public UnavailableValue()
        {
        }

        /// <summary>A rejected value.</summary>
        public UnavailableValue(string action, string parameter, string value, string reason)
        {
            Action = action;
            Parameter = parameter;
            Value = value;
            Reason = reason;
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            string what = $"{Action}.{Parameter} = {Value}";
            return Reason == null ? what : what + ": " + Reason;
        }
    }
}

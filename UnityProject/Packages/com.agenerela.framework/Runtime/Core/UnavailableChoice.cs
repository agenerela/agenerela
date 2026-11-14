using System;
using Newtonsoft.Json;

namespace Agenerela
{
    /// <summary>
    /// One option a call gave as unavailable (<see cref="Choice.Available"/>, DR-022), and why:
    /// recorded in <see cref="DecisionTelemetry.ChoicesUnavailable"/> and the decision log, so a tool
    /// can say "not offered, because ...". The reason never reaches the model.
    /// </summary>
    [Serializable]
    public sealed class UnavailableChoice
    {
        /// <summary>The choice's id.</summary>
        [JsonProperty("choice")] public string Choice;

        /// <summary>The reason the call gave, word for word; null when it gave none.</summary>
        [JsonProperty("reason", NullValueHandling = NullValueHandling.Ignore)] public string Reason;

        public UnavailableChoice()
        {
        }

        public UnavailableChoice(string choice, string reason)
        {
            Choice = choice;
            Reason = reason;
        }

        /// <summary>"choice: reason", or the choice alone when there is no reason.</summary>
        public override string ToString()
        {
            return Reason == null ? Choice : Choice + ": " + Reason;
        }
    }
}

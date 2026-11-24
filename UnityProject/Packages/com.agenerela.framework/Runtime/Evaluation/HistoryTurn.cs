using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Agenerela.Evaluation
{
    /// <summary>
    /// One earlier exchange a multi-turn prompt stages before its stimulus (#147): what the agent
    /// was told then, and what it answered. The runner puts every turn of
    /// <see cref="EvalPrompt.History"/> into the agent's memory, oldest first, exactly as a
    /// <see cref="RollingHistory"/> would have remembered it had the agent really decided it, and only
    /// then asks the prompt.
    /// </summary>
    /// <remarks>
    /// A turn is history, not something scored: its answer is what the agent did, whether or not it
    /// was right. A prompt's <see cref="EvalPrompt.PreconditionState"/> is the state after the last
    /// turn, the one the prompt is asked in. Like <see cref="EvalPrompt"/>, a field this type does
    /// not know is kept in <see cref="Extra"/>, so the format grows without forking (DR-017).
    /// </remarks>
    [JsonObject(MemberSerialization.OptIn)]
    public sealed class HistoryTurn
    {
        /// <summary>The label the turn's stimulus arrived with. Null or blank is <c>Player</c>.</summary>
        [JsonProperty("label", NullValueHandling = NullValueHandling.Ignore)] public string Label;

        /// <summary>What the agent was told or shown on that turn.</summary>
        [JsonProperty("stimulus")] public string Stimulus;

        /// <summary>The action the agent answered with.</summary>
        [JsonProperty("action")] public string Action;

        /// <summary>The target it answered with: a registered target id, or <c>no_target</c>.</summary>
        [JsonProperty("target")] public string Target;

        /// <summary>
        /// Optional. What it said. Staged only when the arm asks for a statement
        /// (<see cref="DecideOptions.AskForStatement"/>), so a remembered answer has the fields the
        /// model is asked for now.
        /// </summary>
        [JsonProperty("statement", NullValueHandling = NullValueHandling.Ignore)] public string Statement;

        /// <summary>Fields this version of the format does not know, kept so a save loses nothing.</summary>
        [JsonExtensionData] public IDictionary<string, JToken> Extra = new Dictionary<string, JToken>();

        /// <summary>The default label of a turn that names none.</summary>
        public const string DefaultLabel = "Player";

        /// <summary><see cref="Label"/>, or <see cref="DefaultLabel"/> when it is blank.</summary>
        public string LabelOrDefault => string.IsNullOrWhiteSpace(Label) ? DefaultLabel : Label;
    }
}

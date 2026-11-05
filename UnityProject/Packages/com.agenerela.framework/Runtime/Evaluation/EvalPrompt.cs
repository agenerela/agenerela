using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Agenerela.Evaluation
{
    /// <summary>
    /// One labelled prompt: what an agent is told or shown, and the action and target it should
    /// choose. The fields are the ones <c>Assets/Evaluation/README.md</c> documents, under the
    /// same JSON names.
    /// </summary>
    /// <remarks>
    /// A field this type does not know is kept in <see cref="Extra"/> and written back after the
    /// known ones, so a file written by a newer format loses nothing when an older one saves it.
    /// </remarks>
    [JsonObject(MemberSerialization.OptIn)]
    public sealed class EvalPrompt
    {
        /// <summary>Unique across the set, <c>&lt;category prefix&gt;_&lt;nnn&gt;</c>.</summary>
        [JsonProperty("id")] public string Id;

        /// <summary>The free-form label the stimulus arrives with: <c>Player</c>, <c>Event</c>, <c>Situation</c>.</summary>
        [JsonProperty("label")] public string Label;

        /// <summary>What the agent is told or shown.</summary>
        [JsonProperty("stimulus")] public string Stimulus;

        /// <summary>Short facts about the world given alongside the stimulus. Empty for most player commands.</summary>
        [JsonProperty("observations")] public List<string> Observations = new List<string>();

        /// <summary>The action id the agent should choose.</summary>
        [JsonProperty("expectedAction")] public string ExpectedAction;

        /// <summary>The registered target id it should choose, <c>no_target</c>, or an open choice.</summary>
        [JsonProperty("expectedTarget")] public ExpectedTarget ExpectedTarget;

        /// <summary>
        /// The agent's state the prompt assumes, such as <c>{ "isFollowing": true }</c>. Set as
        /// <see cref="Agent.State"/> before the prompt is asked.
        /// </summary>
        [JsonProperty("preconditionState")] public JObject PreconditionState = new JObject();

        /// <summary>
        /// Null when the file leaves it out, which means true. False only for a case that tests a
        /// decision with the grounding guard off (DR-016).
        /// </summary>
        [JsonProperty("checkTarget", NullValueHandling = NullValueHandling.Ignore)] public bool? CheckTarget;

        /// <summary>Optional. Why the answer is what it is, for annotators and reviewers.</summary>
        [JsonProperty("note", NullValueHandling = NullValueHandling.Ignore)] public string Note;

        /// <summary>Who labelled the prompt. Two or more names once it has been settled.</summary>
        [JsonProperty("labelledBy")] public List<string> LabelledBy = new List<string>();

        /// <summary>
        /// Each annotator's own answer, as <c>action/target</c>, from before a split was settled.
        /// Null until the prompt is settled; never edited after.
        /// </summary>
        [JsonProperty("labels", NullValueHandling = NullValueHandling.Ignore)] public Dictionary<string, string> Labels;

        /// <summary>Fields this version of the format does not know, kept so a save loses nothing.</summary>
        [JsonExtensionData] public IDictionary<string, JToken> Extra = new Dictionary<string, JToken>();

        /// <summary>True unless <see cref="CheckTarget"/> is set to false.</summary>
        public bool ChecksTarget => CheckTarget ?? true;

        /// <summary>The expected answer as an annotator would write it, <c>action/target</c>.</summary>
        /// <remarks>An open choice gives its first target.</remarks>
        public LabelAnswer ExpectedAnswer =>
            new LabelAnswer(ExpectedAction, ExpectedTarget != null ? ExpectedTarget.Ids[0] : null);
    }
}

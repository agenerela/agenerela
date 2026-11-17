using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Agenerela.Evaluation
{
    /// <summary>
    /// One category of prompts and the file that holds them, such as <c>situation</c> in
    /// <c>prompts/situations.json</c>. One file per category, so two people writing different
    /// categories never edit the same file.
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    public sealed class PromptCategory
    {
        /// <summary>The category id, which the category file repeats as its <c>category</c>.</summary>
        [JsonProperty("id")] public string Id;

        /// <summary>The category file, relative to the set file's folder.</summary>
        [JsonProperty("file")] public string File;

        /// <summary>
        /// Optional. What every prompt id in the category starts with, before <c>_&lt;nnn&gt;</c>.
        /// The category id when left out; <c>command_with_target</c>'s prompts are <c>target_001</c>.
        /// </summary>
        [JsonProperty("idPrefix", NullValueHandling = NullValueHandling.Ignore)] public string IdPrefix;

        [JsonExtensionData] public IDictionary<string, JToken> Extra = new Dictionary<string, JToken>();

        /// <summary>The category's prompts, read from <see cref="File"/>.</summary>
        public List<EvalPrompt> Prompts { get; } = new List<EvalPrompt>();

        /// <summary>Fields of the category file other than <c>category</c> and <c>prompts</c>, kept for its next save.</summary>
        internal IDictionary<string, JToken> FileExtra { get; set; } = new Dictionary<string, JToken>();

        /// <summary><see cref="IdPrefix"/>, or the category id when there is none.</summary>
        public string Prefix => string.IsNullOrEmpty(IdPrefix) ? Id : IdPrefix;
    }
}

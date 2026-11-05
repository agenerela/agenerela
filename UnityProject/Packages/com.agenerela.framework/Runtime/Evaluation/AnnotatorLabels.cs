using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Agenerela.Evaluation
{
    /// <summary>
    /// One annotator's blind answers to one category, kept in
    /// <c>labels/&lt;annotator&gt;/&lt;category&gt;.json</c> beside the set file. Nobody else writes
    /// the file, so two people labelling at once never touch the same one.
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    public sealed class AnnotatorLabels
    {
        /// <summary>Who answered. The folder the file is in decides it; this copy is for a person reading the file.</summary>
        [JsonProperty("annotator")] public string Annotator;

        /// <summary>The category id. The file's name decides it; this copy is for a person reading the file.</summary>
        [JsonProperty("category")] public string Category;

        /// <summary>Each answered prompt's id, and the answer as <c>action/target</c>, in the order given.</summary>
        [JsonProperty("labels")] public Dictionary<string, string> Labels = new Dictionary<string, string>();

        [JsonExtensionData] public IDictionary<string, JToken> Extra = new Dictionary<string, JToken>();
    }
}

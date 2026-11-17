using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Agenerela.Evaluation
{
    /// <summary>
    /// A target the set's agent has registered. Declared in the set rather than read from a
    /// profile, because targets are found in a scene (DR-014) and a set has none.
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    public sealed class DeclaredTarget
    {
        /// <summary>A registered target id, the only value besides <c>no_target</c> an expected target may hold.</summary>
        [JsonProperty("id")] public string Id;

        /// <summary>One line saying what the target is. It may reach the prompt, so it describes the world, not the test.</summary>
        [JsonProperty("description", NullValueHandling = NullValueHandling.Ignore)] public string Description;

        [JsonExtensionData] public IDictionary<string, JToken> Extra = new Dictionary<string, JToken>();
    }
}

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

        /// <summary>
        /// The category the target is registered in (DR-020), such as <c>place</c>, matched against
        /// each action's <see cref="ActionDefinition.TargetCategories"/>; null, the default, for none.
        /// So the paired schema (<see cref="DecideOptions.PairTargetsWithActions"/>) and
        /// <see cref="TargetCategoryGuard"/> know which action takes which target (#151).
        /// <see cref="PromptSetChecks.CompareWithProfile"/> refuses a label these make illegal.
        /// </summary>
        [JsonProperty("category", NullValueHandling = NullValueHandling.Ignore)] public string Category;

        [JsonExtensionData] public IDictionary<string, JToken> Extra = new Dictionary<string, JToken>();
    }
}

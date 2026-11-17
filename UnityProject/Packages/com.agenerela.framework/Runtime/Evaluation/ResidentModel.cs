using Newtonsoft.Json;

namespace Agenerela.Evaluation
{
    /// <summary>One model a server held in memory.</summary>
    [JsonObject(MemberSerialization.OptIn)]
    public sealed class ResidentModel
    {
        [JsonProperty("name")] public string Name;

        /// <summary>Its whole size in memory, in bytes.</summary>
        [JsonProperty("size")] public long Size;

        /// <summary>How much of it is in GPU memory, in bytes.</summary>
        [JsonProperty("sizeVram")] public long SizeVram;

        /// <summary>
        /// True when all of it is in GPU memory. A model that does not fit does not fail: the rest
        /// runs from system memory, and every latency after that is quietly wrong (build plan §4).
        /// </summary>
        [JsonProperty("fullyOnGpu")] public bool FullyOnGpu => Size > 0 && SizeVram >= Size;
    }
}

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Agenerela.Evaluation
{
    /// <summary>One reply check an answer's statement failed, and what it found.</summary>
    [JsonObject(MemberSerialization.OptIn)]
    public sealed class ReplyFinding
    {
        [JsonProperty("flag"), JsonConverter(typeof(StringEnumConverter))] public ReplyFlag Flag;

        /// <summary>
        /// What was found, for a person: the example line it repeats, the length and the limit, or
        /// every id, label, brace and phrase it leaks.
        /// </summary>
        [JsonProperty("detail")] public string Detail;

        public override string ToString() => string.IsNullOrEmpty(Detail) ? Flag.ToString() : $"{Flag}: {Detail}";
    }
}

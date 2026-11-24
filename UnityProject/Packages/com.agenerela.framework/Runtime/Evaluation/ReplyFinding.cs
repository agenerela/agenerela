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

        /// <summary>
        /// For <see cref="ReplyFlag.Echo"/>: the statement repeats an example's stimulus, the line the
        /// example answers (an action's, a counter-example's or the idle example's) (#151, #218).
        /// </summary>
        [JsonProperty("echoesStimulus", DefaultValueHandling = DefaultValueHandling.Ignore)] public bool EchoesStimulus;

        /// <summary>
        /// For <see cref="ReplyFlag.Echo"/>: the statement repeats an example's statement, the line the
        /// example says (<see cref="ActionDefinition.ExampleStatement"/> and the like): a parrot rather
        /// than an echo of the request (#151, #218). Both can be true, when one line is both.
        /// </summary>
        [JsonProperty("echoesStatement", DefaultValueHandling = DefaultValueHandling.Ignore)] public bool EchoesStatement;

        public override string ToString() => string.IsNullOrEmpty(Detail) ? Flag.ToString() : $"{Flag}: {Detail}";
    }
}

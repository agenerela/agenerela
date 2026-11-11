using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace Agenerela.Evaluation
{
    /// <summary>One arm's outcomes: over the whole set, and per category in the set's order.</summary>
    [JsonObject(MemberSerialization.OptIn)]
    public sealed class EvalArmResult
    {
        [JsonProperty("name")] public string Name;

        [JsonProperty("overall")] public OutcomeCounts Overall = new OutcomeCounts();

        /// <summary>Each category's counts, keyed by category id, in the set's order.</summary>
        [JsonProperty("categories")] public Dictionary<string, OutcomeCounts> Categories = new Dictionary<string, OutcomeCounts>();

        /// <summary>
        /// The reply checks' counts over the whole set (<see cref="ReplyChecks"/>): a second score,
        /// beside <see cref="Overall"/> and never part of it.
        /// </summary>
        [JsonProperty("replies")] public ReplyCounts Replies = new ReplyCounts();

        /// <summary>The counts for category <paramref name="id"/>, or null.</summary>
        public OutcomeCounts Category(string id) => Categories.TryGetValue(id, out var counts) ? counts : null;

        internal void Add(string category, DecisionOutcome outcome)
        {
            Overall.Add(outcome);
            if (!Categories.TryGetValue(category, out var counts))
            {
                Categories[category] = counts = new OutcomeCounts();
            }

            counts.Add(outcome);
        }

        internal IEnumerable<string> CategoryIds => Categories.Keys.ToList();
    }
}

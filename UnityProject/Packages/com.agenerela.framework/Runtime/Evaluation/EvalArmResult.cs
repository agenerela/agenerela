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

        /// <summary>
        /// The answers whose reply came with its fields in the schema's order, those that came in
        /// another (<see cref="DecisionTelemetry.OrderLost"/>, #180), and those whose order was not
        /// checked because no reply was read. A report written before the check existed reads back
        /// with all three 0.
        /// </summary>
        [JsonProperty("orderKept")] public int OrderKept;
        [JsonProperty("orderLost")] public int OrderLost;
        [JsonProperty("orderNotChecked")] public int OrderNotChecked;

        /// <summary>
        /// True when any answer's fields came out of the schema's order. Its accuracy then measures
        /// the model under an order the settled field-order rule says costs accuracy, as well as the
        /// model, so it is not a model result; the report warns.
        /// </summary>
        public bool OrderFlagged => OrderLost > 0;

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

        // Null when the order was not checked.
        internal void AddOrder(bool? lost)
        {
            if (lost == null) OrderNotChecked++;
            else if (lost.Value) OrderLost++;
            else OrderKept++;
        }

        internal IEnumerable<string> CategoryIds => Categories.Keys.ToList();
    }
}

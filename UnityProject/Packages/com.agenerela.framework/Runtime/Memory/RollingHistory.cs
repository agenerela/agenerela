using System;
using System.Collections.Generic;
using System.IO;
using Agenerela.Providers;
using Newtonsoft.Json;

namespace Agenerela
{
    /// <summary>
    /// Short-term memory, the default (build plan §2.9): the last few exchanges, oldest first,
    /// within a token budget. Each exchange is the stimulus as it was sent and the decision the
    /// agent made, written as <see cref="DecisionRequest.History"/> says.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Two caps, and the oldest exchange goes first under both, a whole exchange at a time. It
    /// holds at most <see cref="Turns"/> exchanges, forgetting the oldest when a new one would
    /// make one too many. A recall then sends the newest of those that fit in
    /// <see cref="MaxTokens"/> together, and stops at the first that does not, so what is sent is
    /// always an unbroken run ending at the latest exchange. One exchange bigger than the whole
    /// budget sends nothing.
    /// </para>
    /// <para>
    /// There is no tokenizer here, so tokens are estimated: an exchange's characters, both
    /// entries, divided by four and rounded up, the usual rough rule for English. It is a guess on
    /// purpose, cheap and the same for every model, and is why the cap is a budget rather than a
    /// guarantee about <c>num_ctx</c>.
    /// </para>
    /// <para>
    /// Two counts say what a request lacks, and never count one exchange twice.
    /// <see cref="Dropped"/> is the exchanges still held that the last recall left out for the
    /// token cap, never more than <see cref="Turns"/>, so anything above 0 means the budget is what
    /// cut the history short. <see cref="Forgotten"/> is the exchanges the turn cap has let go of
    /// since this memory was made, and keeps rising over a long conversation. Their sum is all of
    /// the agent's past the request does not carry.
    /// </para>
    /// <para>
    /// Not thread-safe, like the rest of <c>Agent</c>, which calls it only on the main thread. Two
    /// decisions in flight at once on one agent both recall before either remembers, and are
    /// remembered in the order they finish.
    /// </para>
    /// </remarks>
    public sealed class RollingHistory : IMemoryStrategy, IReportsDroppedTurns
    {
        /// <summary>How many exchanges are kept by default: six, as §2.1 sets.</summary>
        public const int DefaultTurns = 6;

        /// <summary>
        /// The default token budget for the history of one request: 256 estimated tokens.
        /// </summary>
        /// <remarks>
        /// A starting point for Phase 4 to measure, since history is unmeasured (§2.9). An
        /// ordinary exchange, <c>Player: "Follow me."</c> and a one-line answer, is about 25 to 40
        /// estimated tokens, so six of them fit, and a run of long statements is cut before the
        /// history outweighs the rest of the prompt by much: Appendix A's good arm was 143 tokens
        /// in all, and every token here is paid on every request.
        /// </remarks>
        public const int DefaultMaxTokens = 256;

        private readonly LinkedList<Exchange> exchanges = new LinkedList<Exchange>();

        /// <param name="turns">The most exchanges kept, at least one.</param>
        /// <param name="maxTokens">The most estimated tokens one request's history may hold, at least one.</param>
        /// <exception cref="ArgumentOutOfRangeException">Either is below one.</exception>
        public RollingHistory(int turns = DefaultTurns, int maxTokens = DefaultMaxTokens)
        {
            if (turns < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(turns), turns, "Keep at least one exchange, or set Agent.Memory to null for none.");
            }

            if (maxTokens < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maxTokens), maxTokens, "The token budget must be at least one.");
            }

            Turns = turns;
            MaxTokens = maxTokens;
        }

        /// <summary>The most exchanges kept.</summary>
        public int Turns { get; }

        /// <summary>The most estimated tokens one request's history may hold.</summary>
        public int MaxTokens { get; }

        /// <summary>How many exchanges are held now, at most <see cref="Turns"/>.</summary>
        public int Count => exchanges.Count;

        /// <summary>
        /// The exchanges held now that the last <see cref="Recall"/> left out for the token cap. 0
        /// before the first.
        /// </summary>
        public int Dropped { get; private set; }

        /// <summary>
        /// Every exchange the turn cap has forgotten since this memory was made, which no recall
        /// can send again.
        /// </summary>
        public int Forgotten { get; private set; }

        /// <summary>
        /// The newest exchanges that fit in <see cref="MaxTokens"/>, oldest first, two entries
        /// each. Never null.
        /// </summary>
        public IReadOnlyList<string> Recall(AgentContext ctx)
        {
            var sent = new List<Exchange>(exchanges.Count);
            int tokens = 0;

            for (var node = exchanges.Last; node != null; node = node.Previous)
            {
                if (tokens + node.Value.Tokens > MaxTokens)
                {
                    break;
                }

                tokens += node.Value.Tokens;
                sent.Add(node.Value);
            }

            Dropped = exchanges.Count - sent.Count;

            var history = new List<string>(sent.Count * 2);
            for (int i = sent.Count - 1; i >= 0; i--)
            {
                history.Add(sent[i].Stimulus);
                history.Add(sent[i].Decision);
            }

            return history;
        }

        /// <summary>
        /// Keeps <paramref name="request"/>'s stimulus and <paramref name="result"/>'s decision as
        /// the newest exchange, forgetting the oldest if that makes one more than
        /// <see cref="Turns"/>. A result with no decision, a pipeline error, is not remembered.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="request"/> or <paramref name="result"/> is null.</exception>
        public void Remember(AgentContext ctx, DecisionRequest request, DecisionResult result)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            if (result.Decision == null)
            {
                return;
            }

            string stimulus = request.Stimulus ?? "";
            string decision = Write(result.Decision, request.Schema);
            exchanges.AddLast(new Exchange(stimulus, decision, EstimateTokens(stimulus.Length + decision.Length)));

            if (exchanges.Count > Turns)
            {
                exchanges.RemoveFirst();
                Forgotten++;
            }
        }

        // Characters over four, rounded up: the rough rule the class remarks describe.
        internal static int EstimateTokens(int characters)
        {
            return (characters + 3) / 4;
        }

        // The decision as the JSON object the schema asks for, in its field order, on one line,
        // so the model reads its earlier answers in exactly the form it must give now. A field
        // the schema did not ask for is left out, as the model's own answer had it; with no schema
        // to go by, all three are written in DR-004's order.
        internal static string Write(AgentDecision decision, DecisionSchema schema)
        {
            var text = new StringWriter();
            using (var json = new JsonTextWriter(text) { Formatting = Formatting.None })
            {
                json.WriteStartObject();

                if (schema == null)
                {
                    Field(json, DecisionSchema.ActionFieldName, decision);
                    Field(json, DecisionSchema.TargetFieldName, decision);
                    Field(json, DecisionSchema.StatementFieldName, decision);
                }
                else
                {
                    foreach (var field in schema.Fields)
                    {
                        Field(json, field.Name, decision);
                    }
                }

                json.WriteEndObject();
            }

            return text.ToString();
        }

        private static void Field(JsonTextWriter json, string name, AgentDecision decision)
        {
            string value;
            switch (name)
            {
                case DecisionSchema.ActionFieldName:
                    value = decision.ActionId;
                    break;
                case DecisionSchema.TargetFieldName:
                    value = decision.TargetId;
                    break;
                case DecisionSchema.StatementFieldName:
                    value = decision.Statement;
                    break;
                default:
                    // A field a later schema adds before this knows how to fill it.
                    return;
            }

            json.WritePropertyName(name);
            json.WriteValue(value);
        }

        private readonly struct Exchange
        {
            public Exchange(string stimulus, string decision, int tokens)
            {
                Stimulus = stimulus;
                Decision = decision;
                Tokens = tokens;
            }

            public string Stimulus { get; }
            public string Decision { get; }
            public int Tokens { get; }
        }
    }
}

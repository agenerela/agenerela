using System;
using System.Collections.Generic;
using System.IO;
using Agenerela.Providers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

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
    /// <para>
    /// <see cref="Remembers"/> says what each remembered decision holds when it is sent (#244 item 4,
    /// #216 item 3): the whole decision, the default and how it always was; the move alone; or the
    /// move and what came of it, a line the game attaches through <see cref="Agent.SetOutcome"/>
    /// (<see cref="IRemembersOutcomes"/>). Everything is kept whichever is set, and written out at
    /// each recall, so the token cap counts what is sent.
    /// </para>
    /// </remarks>
    public sealed class RollingHistory : IMemoryStrategy, IReportsDroppedTurns, IExportsMemory, IClearableMemory, IRemembersOutcomes
    {
        /// <summary>How many exchanges are kept by default: six, as §2.1 sets.</summary>
        public const int DefaultTurns = 6;

        /// <summary>The <see cref="MemorySnapshot.Format"/> this strategy writes.</summary>
        public const string SnapshotFormat = "agenerela.rolling-history";

        /// <summary>
        /// The <see cref="MemorySnapshot.Version"/> this strategy writes: 2 since outcomes are saved
        /// (<see cref="MemoryExchange.Outcome"/>, #216 item 3). A version 1 snapshot still imports,
        /// with no outcomes.
        /// </summary>
        public const int SnapshotVersion = 2;

        /// <summary>
        /// The field a decision's outcome is sent in under <see cref="RememberedDecision.MoveAndOutcome"/>,
        /// after the decision's own fields: <c>{"action":"attack","target":"fenmark","outcome":"took fenmark"}</c>.
        /// </summary>
        /// <remarks>
        /// Inside the remembered decision rather than an entry of its own, so the history keeps two
        /// entries an exchange and its <c>user</c>/<c>assistant</c> alternation (#216 item 3).
        /// </remarks>
        public const string OutcomeFieldName = "outcome";

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
        private RememberedDecision remembers = RememberedDecision.Whole;

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

        /// <summary>
        /// What each remembered decision holds when it is sent: <see cref="RememberedDecision.Whole"/>,
        /// the default, as it always was; <see cref="RememberedDecision.Move"/> without its statement;
        /// <see cref="RememberedDecision.MoveAndOutcome"/> without its statement and with the outcome
        /// the game attached. Read at each <see cref="Recall"/>, so it may change at any time.
        /// </summary>
        /// <remarks>
        /// Off by default because it changes what the model reads (hard rule 2). Measured in
        /// GreyBoxStrategy on 11 November 2026 (<c>docs/llm-wiki/findings.md</c>).
        /// </remarks>
        /// <exception cref="ArgumentOutOfRangeException">A value <see cref="RememberedDecision"/> does not define.</exception>
        public RememberedDecision Remembers
        {
            get => remembers;
            set
            {
                if (!Enum.IsDefined(typeof(RememberedDecision), value))
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "Remembers is Whole, Move or MoveAndOutcome.");
                }

                remembers = value;
            }
        }

        /// <summary>How many exchanges are held now, at most <see cref="Turns"/>.</summary>
        public int Count => exchanges.Count;

        /// <summary>
        /// The exchanges held now that the last <see cref="Recall"/> left out for the token cap. 0
        /// before the first.
        /// </summary>
        public int Dropped { get; private set; }

        /// <summary>
        /// Every exchange the turn cap has forgotten since this memory was made, or last cleared,
        /// which no recall can send again.
        /// </summary>
        public int Forgotten { get; private set; }

        /// <summary>
        /// The newest exchanges that fit in <see cref="MaxTokens"/>, oldest first, two entries
        /// each. Never null.
        /// </summary>
        public IReadOnlyList<string> Recall(AgentContext ctx)
        {
            var sent = new List<(string Stimulus, string Decision)>(exchanges.Count);
            int tokens = 0;

            for (var node = exchanges.Last; node != null; node = node.Previous)
            {
                // Written out as Remembers says now, and counted as written, so the cap holds what is sent.
                string decision = Sent(node.Value);
                int cost = EstimateTokens(node.Value.Stimulus.Length + decision.Length);
                if (tokens + cost > MaxTokens)
                {
                    break;
                }

                tokens += cost;
                sent.Add((node.Value.Stimulus, decision));
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
            exchanges.AddLast(new Exchange(stimulus, decision, result.Decision, null));

            if (exchanges.Count > Turns)
            {
                exchanges.RemoveFirst();
                Forgotten++;
            }
        }

        /// <summary>
        /// Sets what came of the remembered decision <paramref name="result"/> carries, replacing an
        /// outcome set before; null or blank clears it. Returns false, changing nothing, when this
        /// memory does not hold that decision: never remembered, still waiting under
        /// <see cref="MemoryCommit.OnExecute"/>, forgotten by the turn cap, or imported (an import
        /// keeps outcomes, but not which result each belongs to). A game usually calls
        /// <see cref="Agent.SetOutcome"/>, which also reaches a decision still waiting.
        /// </summary>
        /// <remarks>
        /// The decision is found by its <see cref="DecisionResult.Decision"/> object, so it must be the
        /// result <see cref="Remember"/> was given. The outcome is kept whatever <see cref="Remembers"/>
        /// is, saved by <see cref="Export"/>, and sent only under <see cref="RememberedDecision.MoveAndOutcome"/>.
        /// </remarks>
        /// <exception cref="ArgumentNullException"><paramref name="result"/> is null.</exception>
        public bool SetOutcome(DecisionResult result, string outcome)
        {
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            if (result.Decision == null)
            {
                return false;
            }

            for (var node = exchanges.Last; node != null; node = node.Previous)
            {
                if (ReferenceEquals(node.Value.Source, result.Decision))
                {
                    node.Value.Outcome = string.IsNullOrWhiteSpace(outcome) ? null : outcome;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Forgets every exchange, as <see cref="Agent.Reset"/> asks (#190, G12), and sets
        /// <see cref="Dropped"/> and <see cref="Forgotten"/> back to 0, leaving this memory as a new
        /// one with the same <see cref="Turns"/> and <see cref="MaxTokens"/> would be.
        /// </summary>
        /// <remarks>
        /// The counts go too because they describe the past a request lacks, and after a clear the
        /// agent has none. Telemetry then reads 0 rather than counting an earlier conversation.
        /// </remarks>
        public void Clear()
        {
            exchanges.Clear();
            Dropped = 0;
            Forgotten = 0;
        }

        /// <summary>
        /// Every exchange held now, oldest first, the ones the token cap leaves out of a request
        /// included, as <see cref="SnapshotFormat"/> version <see cref="SnapshotVersion"/>, with no
        /// <see cref="MemorySnapshot.Data"/>.
        /// </summary>
        /// <remarks>
        /// <see cref="Dropped"/> and <see cref="Forgotten"/> are not saved: they count what this
        /// object did, for telemetry, not what the agent remembers.
        /// </remarks>
        public MemorySnapshot Export()
        {
            var snapshot = new MemorySnapshot { Format = SnapshotFormat, Version = SnapshotVersion };

            foreach (var exchange in exchanges)
            {
                snapshot.Exchanges.Add(new MemoryExchange(exchange.Stimulus, exchange.Decision) { Outcome = exchange.Outcome });
            }

            return snapshot;
        }

        /// <summary>
        /// Whether <see cref="Import"/> would take <paramref name="snapshot"/>, without changing
        /// anything: for a game that checks a whole save before it loads any of it. False, with
        /// <paramref name="reason"/> saying why in words a log can show, for every snapshot
        /// <see cref="Import"/> would throw for; true, with a null reason, otherwise.
        /// </summary>
        /// <remarks>
        /// It takes only <see cref="SnapshotFormat"/>, at a version from 1 to
        /// <see cref="SnapshotVersion"/>, whose exchanges each have a stimulus and a decision. A null
        /// <see cref="MemorySnapshot.Exchanges"/> counts as none. <see cref="MemorySnapshot.Data"/>
        /// is not read.
        /// </remarks>
        public bool CanImport(MemorySnapshot snapshot, out string reason)
        {
            return Read(snapshot, out _, out reason);
        }

        /// <summary>
        /// Replaces every exchange held with <paramref name="snapshot"/>'s, oldest first, keeping the
        /// newest <see cref="Turns"/> of them. Takes only a snapshot this strategy wrote:
        /// <see cref="SnapshotFormat"/>, at a version it can read. <see cref="CanImport"/> checks one
        /// first without changing anything.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The exchanges past <see cref="Turns"/> are let go of as the turn cap always does, and count
        /// in <see cref="Forgotten"/>. A null <see cref="MemorySnapshot.Exchanges"/> imports none,
        /// which leaves the memory empty. <see cref="MemorySnapshot.Data"/> is not read.
        /// </para>
        /// <para>
        /// Another strategy's snapshot is refused even though its exchanges are written the same
        /// way: a save that names another format, or a newer version of this one, is not one this
        /// memory can vouch for. To move memory between strategies, copy the exchanges into a
        /// snapshot of the target's format on purpose.
        /// </para>
        /// </remarks>
        /// <exception cref="ArgumentNullException"><paramref name="snapshot"/> is null.</exception>
        /// <exception cref="ArgumentException">
        /// <see cref="CanImport"/> refuses the snapshot: another format, a version this strategy
        /// cannot read, or an exchange, stimulus or decision that is null. The message is
        /// <see cref="CanImport"/>'s reason. Nothing is imported, and the memory keeps what it held.
        /// </exception>
        public void Import(MemorySnapshot snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            if (!Read(snapshot, out var imported, out string reason))
            {
                throw new ArgumentException(reason, nameof(snapshot));
            }

            exchanges.Clear();
            foreach (var exchange in imported)
            {
                exchanges.AddLast(exchange);
                if (exchanges.Count > Turns)
                {
                    exchanges.RemoveFirst();
                    Forgotten++;
                }
            }
        }

        // The exchanges of a snapshot Import can take, or why it cannot, without changing anything.
        // One check for both CanImport and Import, so they never disagree.
        private static bool Read(MemorySnapshot snapshot, out List<Exchange> imported, out string reason)
        {
            imported = null;

            if (snapshot == null)
            {
                reason = "There is no snapshot.";
                return false;
            }

            if (!string.Equals(snapshot.Format, SnapshotFormat, StringComparison.Ordinal))
            {
                string format = string.IsNullOrEmpty(snapshot.Format) ? "no format" : $"format '{snapshot.Format}'";
                reason = $"The snapshot has {format}, and RollingHistory reads only '{SnapshotFormat}'.";
                return false;
            }

            if (snapshot.Version < 1 || snapshot.Version > SnapshotVersion)
            {
                reason = $"The snapshot is version {snapshot.Version} of '{SnapshotFormat}', and RollingHistory reads versions 1 to {SnapshotVersion}.";
                return false;
            }

            var read = new List<Exchange>();
            var source = snapshot.Exchanges ?? new List<MemoryExchange>();
            for (int i = 0; i < source.Count; i++)
            {
                var exchange = source[i];
                if (exchange == null || exchange.Stimulus == null || exchange.Decision == null)
                {
                    // A null would reach DecisionRequest.History, which Agent refuses as a pipeline
                    // error on every later decision, so it is refused here, before anything changes.
                    reason = $"Exchanges[{i}] is null or has a null stimulus or decision, so the snapshot cannot be imported.";
                    return false;
                }

                // JsonUtility writes a null string as an empty one, and version 1 had no outcomes.
                read.Add(new Exchange(exchange.Stimulus, exchange.Decision, null,
                    string.IsNullOrWhiteSpace(exchange.Outcome) ? null : exchange.Outcome));
            }

            imported = read;
            reason = null;
            return true;
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

        // The decision as Remembers says to send it now. Whole is the string kept, untouched, so the
        // default history is byte for byte what it always was.
        private string Sent(Exchange exchange)
        {
            switch (remembers)
            {
                case RememberedDecision.Move:
                    return Rewrite(exchange.Decision, null);
                case RememberedDecision.MoveAndOutcome:
                    return Rewrite(exchange.Decision, exchange.Outcome);
                default:
                    return exchange.Decision;
            }
        }

        // The decision's JSON with its statement left out and, given one, the outcome after the rest,
        // the other fields in the order they were written. A decision that is not a JSON object, which
        // only an import can bring, is sent as it is.
        internal static string Rewrite(string decision, string outcome)
        {
            JObject json;
            try
            {
                using (var reader = new JsonTextReader(new StringReader(decision)) { DateParseHandling = DateParseHandling.None })
                {
                    json = JObject.Load(reader);
                }
            }
            catch (JsonException)
            {
                return decision;
            }

            json.Remove(DecisionSchema.StatementFieldName);
            json.Remove(OutcomeFieldName);
            if (!string.IsNullOrWhiteSpace(outcome))
            {
                json.Add(OutcomeFieldName, outcome);
            }

            return json.ToString(Formatting.None);
        }

        // One exchange: what was sent and decided, the decision object it came from (null once
        // imported), and the outcome the game attached, which may change while it is held.
        private sealed class Exchange
        {
            public Exchange(string stimulus, string decision, AgentDecision source, string outcome)
            {
                Stimulus = stimulus;
                Decision = decision;
                Source = source;
                Outcome = outcome;
            }

            public string Stimulus { get; }
            public string Decision { get; }
            public AgentDecision Source { get; }
            public string Outcome { get; set; }
        }
    }
}

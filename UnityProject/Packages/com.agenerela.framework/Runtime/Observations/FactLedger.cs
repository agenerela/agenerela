using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Agenerela
{
    /// <summary>
    /// A keyed list of facts, each a line an agent reads among its observations, that the game sets,
    /// words per reader, and lets expire in its own units (#216 item 4, #266 item 5). One ledger may
    /// be read by many agents, each through its own <see cref="For"/> view, so one fact is "ours" to
    /// one country and news to four others. An observation source, so nothing new reaches the model:
    /// its lines go where every source's go.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Facts, not exchanges.</b> Memory holds what an agent said and was asked; a ledger holds what
    /// is true for a while: "orsk broke a truce with us on turn 6", "the well is dry", "plague in
    /// kesh". Use it for what the game knows and wants told, and memory for what the agent did. A
    /// ledger is never shared through memory's rules: several agents may read one, because each reads
    /// the game's facts, not another agent's words.
    /// </para>
    /// <para>
    /// <b>Keys.</b> <see cref="Set"/> again with a key replaces that fact, its line, its wordings and
    /// its expiry, and makes it the newest. <see cref="Remove"/> forgets it.
    /// </para>
    /// <para>
    /// <b>Order.</b> Lines come in the order facts were last set, oldest first, the same on every
    /// call, so a request changes only where a fact did.
    /// </para>
    /// <para>
    /// <b>Expiry</b> is counted by the game's <see cref="Tick"/>, in whatever its units are (turns,
    /// days, decisions), because the framework has no clock it may assume. A fact set to expire after
    /// 3 is read until the third tick after it was set, then removed. <see cref="Forever"/> never
    /// expires.
    /// </para>
    /// <para>
    /// <b>The budget.</b> With <see cref="MaxLines"/> above 0, a view returns at most that many lines,
    /// the newest, and reports the oldest it left out through <see cref="IReportsDroppedObservations"/>,
    /// which <see cref="DecisionTelemetry.ObservationsDropped"/> records. The agent's own
    /// <see cref="Agent.MaxObservations"/> still applies after it.
    /// </para>
    /// <para>
    /// <b>Saving.</b> <see cref="Export"/> and <see cref="Import"/> carry every fact, wording and
    /// expiry, as <see cref="IExportsMemory"/> does for a memory, so a ledger is saved beside the
    /// game's state. Called on the main thread, as every source is.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// var news = new FactLedger();
    /// foreach (var ruler in rulers) ruler.Court.ObservationSources.Add(news.For(ruler.Id));
    ///
    /// news.Set("plague_kesh", "A plague spreads in kesh.", expiresAfter: 3);
    /// news.Word("plague_kesh", "kesh", "Plague is killing our people.");
    /// // every turn:
    /// news.Tick();
    /// </code>
    /// </example>
    public sealed class FactLedger : IObservationSource, IReportsDroppedObservations, IExportsMemory
    {
        /// <summary>The expiry of a fact that never expires: the default.</summary>
        public const int Forever = 0;

        /// <summary>What <see cref="Export"/> writes in <see cref="MemorySnapshot.Format"/>.</summary>
        public const string SnapshotFormat = "Agenerela.FactLedger";

        /// <summary>The version of <see cref="MemorySnapshot.Data"/> this ledger writes and reads.</summary>
        public const int SnapshotVersion = 1;

        private readonly List<Fact> facts = new List<Fact>();
        private readonly FactLedgerView shared;
        private int maxLines;

        public FactLedger()
        {
            shared = new FactLedgerView(this, null);
        }

        /// <summary>
        /// The most lines one view returns, the newest; 0, the default, for no budget. A negative
        /// value is a mistake that throws.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
        public int MaxLines
        {
            get => maxLines;
            set
            {
                if (value < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "FactLedger.MaxLines cannot be negative. 0 means no budget.");
                }

                maxLines = value;
            }
        }

        /// <summary>How many facts the ledger holds now.</summary>
        public int Count => facts.Count;

        /// <summary>The keys held, in line order.</summary>
        public IReadOnlyList<string> Keys
        {
            get
            {
                var keys = new List<string>(facts.Count);
                foreach (var fact in facts)
                {
                    keys.Add(fact.Key);
                }

                return keys;
            }
        }

        /// <summary>
        /// Sets a fact every reader sees as <paramref name="line"/>, replacing one with the same key
        /// and making it the newest.
        /// </summary>
        /// <param name="key">The game's name for the fact, such as <c>plague_kesh</c>.</param>
        /// <param name="line">The line read, verbatim. Null or blank shows it to nobody but the readers it is worded for.</param>
        /// <param name="expiresAfter">Ticks it is read for; <see cref="Forever"/>, the default, for no expiry.</param>
        /// <exception cref="ArgumentException"><paramref name="key"/> is null, empty or whitespace.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="expiresAfter"/> is negative.</exception>
        public void Set(string key, string line, int expiresAfter = Forever)
        {
            CheckKey(key);
            if (expiresAfter < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(expiresAfter), expiresAfter,
                    "A fact's expiry cannot be negative. FactLedger.Forever (0) never expires.");
            }

            int at = IndexOf(key);
            if (at >= 0)
            {
                facts.RemoveAt(at);
            }

            facts.Add(new Fact { Key = key, Line = line, Remaining = expiresAfter });
        }

        /// <summary>
        /// Gives <paramref name="reader"/> its own wording of the fact under <paramref name="key"/>:
        /// "ours" where everyone else reads news. Null or blank hides the fact from that reader. Kept
        /// until the fact is set again or removed.
        /// </summary>
        /// <returns>Whether the ledger holds the fact.</returns>
        /// <exception cref="ArgumentException"><paramref name="key"/> or <paramref name="reader"/> is null, empty or whitespace.</exception>
        public bool Word(string key, string reader, string line)
        {
            CheckKey(key);
            CheckReader(reader);

            int at = IndexOf(key);
            if (at < 0)
            {
                return false;
            }

            var fact = facts[at];
            if (fact.Wordings == null)
            {
                fact.Wordings = new Dictionary<string, string>(StringComparer.Ordinal);
            }

            fact.Wordings[reader] = line ?? "";
            return true;
        }

        /// <summary>Forgets the fact under <paramref name="key"/>; returns whether there was one.</summary>
        public bool Remove(string key)
        {
            int at = key == null ? -1 : IndexOf(key);
            if (at < 0)
            {
                return false;
            }

            facts.RemoveAt(at);
            return true;
        }

        /// <summary>Whether the ledger holds a fact under <paramref name="key"/>.</summary>
        public bool Contains(string key) => key != null && IndexOf(key) >= 0;

        /// <summary>
        /// Ticks left before the fact under <paramref name="key"/> expires, <see cref="Forever"/> for
        /// one that never does, and -1 when there is none.
        /// </summary>
        public int Remaining(string key)
        {
            int at = key == null ? -1 : IndexOf(key);
            return at < 0 ? -1 : facts[at].Remaining;
        }

        /// <summary>
        /// Counts one unit of the game's time: every fact that expires has one tick less, and those at
        /// none are removed. Returns the keys removed, in line order.
        /// </summary>
        public IReadOnlyList<string> Tick()
        {
            var removed = new List<string>();
            for (int i = 0; i < facts.Count; i++)
            {
                var fact = facts[i];
                if (fact.Remaining == Forever)
                {
                    continue;
                }

                fact.Remaining--;
                if (fact.Remaining <= 0)
                {
                    removed.Add(fact.Key);
                    facts.RemoveAt(i);
                    i--;
                }
            }

            return removed;
        }

        /// <summary>Forgets every fact.</summary>
        public void Clear()
        {
            facts.Clear();
        }

        /// <summary>
        /// The view <paramref name="reader"/> reads the ledger through: each fact in that reader's
        /// wording when it has one, the shared line otherwise. Add it to that agent's
        /// <see cref="Agent.ObservationSources"/>. Each call makes a new view; any number may read one
        /// ledger.
        /// </summary>
        /// <exception cref="ArgumentException"><paramref name="reader"/> is null, empty or whitespace.</exception>
        public FactLedgerView For(string reader)
        {
            CheckReader(reader);
            return new FactLedgerView(this, reader);
        }

        /// <summary>
        /// The shared lines only, for an agent that has no wording of its own: the ledger itself as a
        /// source reads as <c>For</c> a reader no fact is worded for.
        /// </summary>
        public IEnumerable<string> Observe(AgentContext ctx) => shared.Observe(ctx);

        /// <summary>What the budget left out of the last <see cref="Observe"/> on the ledger itself.</summary>
        public IReadOnlyList<string> Dropped => shared.Dropped;

        /// <summary>
        /// Every fact, its wordings and its expiry, as plain data: <see cref="MemorySnapshot.Format"/>
        /// is <see cref="SnapshotFormat"/> and <see cref="MemorySnapshot.Data"/> is JSON. No exchanges.
        /// </summary>
        public MemorySnapshot Export()
        {
            var saved = new List<Fact>(facts.Count);
            foreach (var fact in facts)
            {
                saved.Add(fact.Copy());
            }

            return new MemorySnapshot
            {
                Format = SnapshotFormat,
                Version = SnapshotVersion,
                Data = JsonConvert.SerializeObject(saved),
            };
        }

        /// <summary>
        /// Whether <paramref name="snapshot"/> is one <see cref="Import"/> would take, changing nothing.
        /// </summary>
        public bool CanImport(MemorySnapshot snapshot) => TryRead(snapshot, out _, out _);

        /// <summary>
        /// Replaces every fact with <paramref name="snapshot"/>'s. One this ledger did not write, or a
        /// version it does not know, throws and leaves the ledger as it was.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="snapshot"/> is null.</exception>
        /// <exception cref="ArgumentException">The snapshot is not one this ledger can read.</exception>
        public void Import(MemorySnapshot snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            if (!TryRead(snapshot, out var read, out string problem))
            {
                throw new ArgumentException(problem, nameof(snapshot));
            }

            facts.Clear();
            facts.AddRange(read);
        }

        // The lines one reader reads now, oldest first, and those the budget cut.
        internal void Lines(string reader, List<string> lines, List<string> dropped)
        {
            foreach (var fact in facts)
            {
                string line = fact.Line;
                if (reader != null && fact.Wordings != null && fact.Wordings.TryGetValue(reader, out string worded))
                {
                    line = worded;
                }

                if (!string.IsNullOrWhiteSpace(line))
                {
                    lines.Add(line);
                }
            }

            if (maxLines > 0 && lines.Count > maxLines)
            {
                int cut = lines.Count - maxLines;
                dropped.AddRange(lines.GetRange(0, cut));
                lines.RemoveRange(0, cut);
            }
        }

        private static bool TryRead(MemorySnapshot snapshot, out List<Fact> read, out string problem)
        {
            read = null;
            if (snapshot == null)
            {
                problem = "The snapshot is null.";
                return false;
            }

            if (snapshot.Format != SnapshotFormat)
            {
                problem = $"The snapshot was written by '{snapshot.Format}', not by a FactLedger ('{SnapshotFormat}').";
                return false;
            }

            if (snapshot.Version != SnapshotVersion)
            {
                problem = $"The snapshot is version {snapshot.Version}; this FactLedger reads version {SnapshotVersion}.";
                return false;
            }

            try
            {
                read = string.IsNullOrEmpty(snapshot.Data) ? new List<Fact>() : JsonConvert.DeserializeObject<List<Fact>>(snapshot.Data) ?? new List<Fact>();
            }
            catch (JsonException e)
            {
                problem = "The snapshot's facts are not readable JSON. " + e.Message;
                return false;
            }

            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var fact in read)
            {
                if (fact == null || string.IsNullOrWhiteSpace(fact.Key) || !keys.Add(fact.Key) || fact.Remaining < 0)
                {
                    problem = "The snapshot holds a fact with no key, a key twice or a negative expiry.";
                    read = null;
                    return false;
                }
            }

            problem = null;
            return true;
        }

        private int IndexOf(string key)
        {
            for (int i = 0; i < facts.Count; i++)
            {
                if (string.Equals(facts[i].Key, key, StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return -1;
        }

        private static void CheckKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("A fact needs a key.", nameof(key));
            }
        }

        private static void CheckReader(string reader)
        {
            if (string.IsNullOrWhiteSpace(reader))
            {
                throw new ArgumentException("A reader needs a name, such as the agent's id.", nameof(reader));
            }
        }

        // One fact as it is held and saved.
        internal sealed class Fact
        {
            [JsonProperty("key")] public string Key;
            [JsonProperty("line")] public string Line;
            [JsonProperty("remaining")] public int Remaining;
            [JsonProperty("wordings", NullValueHandling = NullValueHandling.Ignore)] public Dictionary<string, string> Wordings;

            public Fact Copy()
            {
                return new Fact
                {
                    Key = Key,
                    Line = Line,
                    Remaining = Remaining,
                    Wordings = Wordings == null ? null : new Dictionary<string, string>(Wordings, StringComparer.Ordinal),
                };
            }
        }
    }

    /// <summary>
    /// One reader's view of a <see cref="FactLedger"/>, from <see cref="FactLedger.For"/>: an
    /// observation source that reads each fact in that reader's wording, and reports what the
    /// ledger's budget left out.
    /// </summary>
    public sealed class FactLedgerView : IObservationSource, IReportsDroppedObservations
    {
        private readonly FactLedger ledger;
        private List<string> dropped = new List<string>();

        internal FactLedgerView(FactLedger ledger, string reader)
        {
            this.ledger = ledger;
            Reader = reader;
        }

        /// <summary>The reader this view reads for; null for the ledger's shared lines.</summary>
        public string Reader { get; }

        /// <summary>The ledger it reads.</summary>
        public FactLedger Ledger => ledger;

        /// <summary>The ledger's lines for this reader, oldest first, within its budget.</summary>
        public IEnumerable<string> Observe(AgentContext ctx)
        {
            var lines = new List<string>();
            var cut = new List<string>();
            ledger.Lines(Reader, lines, cut);
            dropped = cut;
            return lines;
        }

        /// <summary>What the budget left out of the last <see cref="Observe"/>, oldest first.</summary>
        public IReadOnlyList<string> Dropped => dropped;
    }
}

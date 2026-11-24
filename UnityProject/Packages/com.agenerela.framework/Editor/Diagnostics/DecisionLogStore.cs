using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace Agenerela.Editor.Diagnostics
{
    /// <summary>
    /// The Decision Log's rows (#172): every decision the <see cref="DecisionFeed"/> raised while a
    /// Decision Log window was open and recording, oldest first, at most <see cref="Cap"/> of them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Only while someone is looking.</b> It subscribes to the feed only while at least one window
    /// is attached (<see cref="Attach"/>) and <see cref="Recording"/> is on, so with every window
    /// closed the feed has no subscriber and costs the game a null check.
    /// </para>
    /// <para>
    /// <b>It survives leaving Play mode.</b> Each row is kept as its JSON line beside the row
    /// itself, so the copy Unity serializes across a domain reload is written once per entry, never
    /// per reload. <see cref="DecisionLogSession"/> holds the store across reloads; quitting the
    /// Editor drops it, as the issue scopes it.
    /// </para>
    /// <para>
    /// <b>Threads.</b> An entry is copied into its line on the thread that raised it, as
    /// <see cref="DecisionLog"/> does, since a result's telemetry changes when it is executed. One
    /// raised on the main thread is added at once; one from another thread waits for the next
    /// Editor update, since naming its GameObject is main-thread work.
    /// </para>
    /// </remarks>
    [Serializable]
    internal sealed class DecisionLogStore
    {
        /// <summary>The rows kept unless <see cref="Cap"/> is set: enough for a long Play session, small enough to scroll.</summary>
        internal const int DefaultCap = 1000;

        [Serializable]
        private struct Stored
        {
            public string line;
            public string agent;
            public int gameObject;
            public string providerSource;
        }

        [SerializeField] private List<Stored> stored = new List<Stored>();
        [SerializeField] private int cap = DefaultCap;
        [SerializeField] private bool recording = true;
        [SerializeField] private bool clearOnPlay;

        // Rebuilt from stored after a domain reload, on first use.
        [NonSerialized] private List<DecisionLogRow> rows;

        // The row each live result is on, so its Execute entry lands on the same row. Weak, so a row
        // keeps no result alive; empty after a reload, when no result from before is live anyway.
        [NonSerialized] private ConditionalWeakTable<DecisionResult, DecisionLogRow> live = new ConditionalWeakTable<DecisionResult, DecisionLogRow>();
        [NonSerialized] private HashSet<object> owners = new HashSet<object>();
        [NonSerialized] private ConcurrentQueue<Arrived> arrived = new ConcurrentQueue<Arrived>();
        [NonSerialized] private bool subscribed;
        [NonSerialized] private int mainThread;
        [NonSerialized] private bool warned;

        private readonly struct Arrived
        {
            internal Arrived(Agent agent, DecisionResult result, DecisionLogLine line)
            {
                Agent = agent;
                Result = result;
                Line = line;
            }

            internal Agent Agent { get; }
            internal DecisionResult Result { get; }
            internal DecisionLogLine Line { get; }
        }

        /// <summary>Raised on the main thread after the rows change: one added, one updated, the cap, or <see cref="Clear"/>.</summary>
        internal event Action Changed;

        /// <summary>The rows, oldest first.</summary>
        internal IReadOnlyList<DecisionLogRow> Rows => EnsureRows();

        /// <summary>Whether the store is subscribed to the feed right now.</summary>
        internal bool Subscribed => subscribed;

        /// <summary>The most rows kept; adding past it drops the oldest. At least 1.</summary>
        internal int Cap
        {
            get => cap;
            set
            {
                cap = Math.Max(1, value);
                if (Trim())
                {
                    Changed?.Invoke();
                }
            }
        }

        /// <summary>Whether decisions are recorded. Off unsubscribes from the feed; the rows stay.</summary>
        internal bool Recording
        {
            get => recording;
            set
            {
                recording = value;
                Sync();
            }
        }

        /// <summary>Whether entering Play mode clears the log, as the Console's own toggle does.</summary>
        internal bool ClearOnPlay
        {
            get => clearOnPlay;
            set => clearOnPlay = value;
        }

        /// <summary>
        /// Starts listening on behalf of <paramref name="owner"/>, a window, if <see cref="Recording"/>
        /// is on. Call it on the main thread.
        /// </summary>
        internal void Attach(object owner)
        {
            mainThread = Thread.CurrentThread.ManagedThreadId;
            Owners.Add(owner);
            Sync();
        }

        /// <summary>Stops listening for <paramref name="owner"/>; with no owner left the feed has no subscriber from this store.</summary>
        internal void Detach(object owner)
        {
            Owners.Remove(owner);
            Sync();
        }

        /// <summary>
        /// Subscribes again, after the feed dropped every subscriber as Play mode started (#190, G8).
        /// The window calls it from <c>EditorApplication.playModeStateChanged</c>.
        /// </summary>
        internal void Resubscribe()
        {
            if (!subscribed)
            {
                return;
            }

            DecisionFeed.Recorded -= OnEntry;
            DecisionFeed.Recorded += OnEntry;
        }

        /// <summary>Removes every row.</summary>
        internal void Clear()
        {
            foreach (var row in EnsureRows())
            {
                row.Removed = true;
            }

            rows.Clear();
            stored.Clear();
            live = new ConditionalWeakTable<DecisionResult, DecisionLogRow>();
            Changed?.Invoke();
        }

        /// <summary>Adds any entry that arrived on another thread. The Editor's update calls it while subscribed.</summary>
        internal void Drain()
        {
            var changed = false;
            while (Queue.TryDequeue(out var entry))
            {
                Add(entry);
                changed = true;
            }

            if (changed)
            {
                Changed?.Invoke();
            }
        }

        /// <summary>
        /// The feed's subscriber, and the tests' way in: copies the entry now, then adds it, at once on
        /// the main thread and at the next update otherwise. Never throws into the game.
        /// </summary>
        internal void OnEntry(DecisionFeedEntry entry)
        {
            DecisionLogLine line;
            try
            {
                // With the prompt: section ④ shows what the agent knew and what it was offered.
                line = DecisionLogLine.From(entry, withPrompt: true);
            }
            catch (Exception e)
            {
                if (!warned)
                {
                    warned = true;
                    Debug.LogWarning($"The Decision Log could not read feed entry {entry?.Sequence}, so it is missing from the window. {e.GetType().Name}: {e.Message}");
                }

                return;
            }

            var arrival = new Arrived(entry.Agent, entry.Result, line);
            if (mainThread == 0 || Thread.CurrentThread.ManagedThreadId == mainThread)
            {
                Add(arrival);
                Changed?.Invoke();
            }
            else
            {
                Queue.Enqueue(arrival);
            }
        }

        /// <summary>The line written for a row, as one line of a <see cref="DecisionLog"/> file.</summary>
        internal static string ToJson(DecisionLogLine line)
        {
            return JsonConvert.SerializeObject(line, DecisionLogFormat.Settings);
        }

        /// <summary>The same line, indented, for <b>Copy as JSON</b>.</summary>
        internal static string ToIndentedJson(DecisionLogLine line)
        {
            var settings = new JsonSerializerSettings
            {
                Formatting = Formatting.Indented,
                NullValueHandling = DecisionLogFormat.Settings.NullValueHandling,
                DateTimeZoneHandling = DecisionLogFormat.Settings.DateTimeZoneHandling,
                Converters = DecisionLogFormat.Settings.Converters,
            };
            return JsonConvert.SerializeObject(line, settings);
        }

        /// <summary>
        /// <paramref name="rows"/> as a JSON-lines text, one line each in the order given and a newline
        /// after each: the format <see cref="DecisionLogReader"/> reads.
        /// </summary>
        internal static string ToJsonLines(IEnumerable<DecisionLogRow> rows)
        {
            var text = new System.Text.StringBuilder();
            foreach (var row in rows)
            {
                text.Append(ToJson(row.Line)).Append('\n');
            }

            return text.ToString();
        }

        private HashSet<object> Owners => owners ?? (owners = new HashSet<object>());

        private ConcurrentQueue<Arrived> Queue => arrived ?? (arrived = new ConcurrentQueue<Arrived>());

        private ConditionalWeakTable<DecisionResult, DecisionLogRow> Live => live ?? (live = new ConditionalWeakTable<DecisionResult, DecisionLogRow>());

        private void Sync()
        {
            var want = recording && Owners.Count > 0;
            if (want && !subscribed)
            {
                DecisionFeed.Recorded += OnEntry;
                EditorApplication.update += Drain;
                subscribed = true;
            }
            else if (!want && subscribed)
            {
                DecisionFeed.Recorded -= OnEntry;
                EditorApplication.update -= Drain;
                subscribed = false;

                // Anything from another thread already copied still belongs to the session.
                Drain();
            }
        }

        private void Add(Arrived arrival)
        {
            var list = EnsureRows();
            var line = arrival.Line;

            // The execution of a decision already here: the same row, now with what Execute did.
            if (line.Stage == DecisionStage.Executed && arrival.Result != null &&
                Live.TryGetValue(arrival.Result, out var existing) && !existing.Removed)
            {
                existing.Line = line;
                var index = list.IndexOf(existing);
                if (index >= 0)
                {
                    var keep = stored[index];
                    keep.line = ToJson(line);
                    stored[index] = keep;
                }

                return;
            }

            string agent = null;
            int gameObject = 0;
            var owner = AgentBehaviour.Find(arrival.Agent);
            if (owner != null)
            {
                agent = owner.gameObject.name;
                gameObject = owner.gameObject.GetInstanceID();
            }

            var row = new DecisionLogRow(line, agent, gameObject, ProviderSourceOf(line, owner));
            list.Add(row);
            stored.Add(new Stored { line = ToJson(line), agent = row.Agent, gameObject = gameObject, providerSource = row.ProviderSource });
            if (arrival.Result != null)
            {
                Live.Remove(arrival.Result);
                Live.Add(arrival.Result, row);
            }

            Trim();
        }

        // Drops the oldest rows past the cap. True when any went.
        private bool Trim()
        {
            var list = EnsureRows();
            var over = list.Count - cap;
            if (over <= 0)
            {
                return false;
            }

            for (int i = 0; i < over; i++)
            {
                list[i].Removed = true;
            }

            list.RemoveRange(0, over);
            stored.RemoveRange(0, over);
            return true;
        }

        private List<DecisionLogRow> EnsureRows()
        {
            if (rows != null)
            {
                return rows;
            }

            // After a domain reload: the rows back from their lines. A line that no longer reads
            // is dropped with its stored copy, so the two lists stay one to one.
            rows = new List<DecisionLogRow>(stored.Count);
            var kept = new List<Stored>(stored.Count);
            foreach (var item in stored)
            {
                if (DecisionLogReader.TryParse(item.line, out var line))
                {
                    rows.Add(new DecisionLogRow(line, item.agent, item.gameObject, string.IsNullOrEmpty(item.providerSource) ? null : item.providerSource));
                    kept.Add(item);
                }
            }

            stored = kept;
            return rows;
        }

        /// <summary>
        /// Which of DR-013's three places the line's provider config came from, as far as the window
        /// can tell when the entry arrives: the agent's own field, the project default, or neither,
        /// which is code or a scene override.
        /// </summary>
        internal static string ProviderSourceOf(DecisionLogLine line, AgentBehaviour owner)
        {
            if (string.IsNullOrEmpty(line.ProviderConfig))
            {
                return "no config asset: the provider was built in code";
            }

            if (owner != null && owner.Provider != null && owner.Provider.name == line.ProviderConfig)
            {
                return "the agent's own Provider field";
            }

            var settings = AgenerelaSettings.Load();
            if (settings != null && settings.DefaultProvider != null && settings.DefaultProvider.name == line.ProviderConfig)
            {
                return "the project default (Project Settings → Agenerela)";
            }

            return "set in code or by a scene override";
        }
    }

    /// <summary>
    /// Holds the one <see cref="DecisionLogStore"/> of this Editor session, so its rows survive the
    /// domain reload that leaving Play mode brings. Not written to disk: a restart starts empty.
    /// </summary>
    internal sealed class DecisionLogSession : ScriptableSingleton<DecisionLogSession>
    {
        [SerializeField] private DecisionLogStore store = new DecisionLogStore();

        internal static DecisionLogStore Store => instance.store ?? (instance.store = new DecisionLogStore());
    }
}

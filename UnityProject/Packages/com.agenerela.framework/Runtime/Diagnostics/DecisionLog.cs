using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Newtonsoft.Json;
using UnityEngine;

namespace Agenerela
{
    /// <summary>
    /// Writes every decision every agent makes to a JSON-lines file, one <see cref="DecisionLogLine"/>
    /// per <see cref="DecisionFeed"/> entry (#190, G7). One line switches it on:
    /// <c>var log = DecisionLog.Start(path);</c>, and <see cref="DecisionLogReader"/> reads the file back.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>What a line holds.</b> When, which agent, the stimulus and its label, what the game got,
    /// the model's own answer when a guard changed it, the verdict, what <c>Execute</c> did, the
    /// provider, model, latency, waits and tokens, and a pipeline error's kind. The prompt itself is
    /// left out unless <see cref="IncludePrompt"/> is on: it is large, and it may hold the player's
    /// own words. No key is ever written: telemetry holds none, and credentials in a URL anywhere in
    /// the line's text, a reply or an error message alike, are masked.
    /// </para>
    /// <para>
    /// <b>It never blocks or throws into the game.</b> The entry is copied into a line on the thread
    /// that raised it, since a result's telemetry changes when it is executed, and the line goes to
    /// the file from a worker thread. A write that fails is reported once, as a warning, and the line
    /// is counted in <see cref="LinesDropped"/>; later lines are still tried, and a later failure is
    /// not reported again.
    /// </para>
    /// <para>
    /// <b>Several agents</b> share one log and one file: their lines interleave in the order the feed
    /// raised them, each whole, and <see cref="DecisionLogLine.AgentId"/> and
    /// <see cref="DecisionLogLine.AgentName"/> tell them apart. Use one log per file: two logs
    /// writing one path would interleave their lines too, but each would roll the file over alone.
    /// </para>
    /// <para>
    /// <b>Play sessions.</b> The feed drops every subscriber as Play mode starts, so a log lives one
    /// Play session: start it again in the next, as the game starts. Leaving Play mode or quitting
    /// stops it (<c>Application.quitting</c>), which writes what is still queued first. If the
    /// process dies mid-line, the file ends in a partial line: the reader skips it, and the next log
    /// on that file starts on a new line, so nothing after it is lost.
    /// </para>
    /// <para>
    /// <b>File growth.</b> Before a line would take the file past <see cref="MaxFileBytes"/>, the
    /// file is moved to <see cref="RolloverPath"/>, replacing the one there, and a new file starts.
    /// Each file stays under the cap, but for a single line larger than it, so at most twice the cap
    /// is kept, the older half in the rollover file.
    /// </para>
    /// <para>
    /// Extension points, not now: the agent's GameObject name (a game can name its agents through
    /// <see cref="Agent.Id"/>), the call's <see cref="DecisionResult.Correlation"/>, which is the
    /// game's own object, and the whole schema rather than the actions and targets it offered.
    /// </para>
    /// </remarks>
    public sealed class DecisionLog : IDisposable
    {
        /// <summary>The default <see cref="MaxFileBytes"/>: 16 MiB.</summary>
        public const long DefaultMaxFileBytes = 16L * 1024 * 1024;

        // Masks the user and password of a URL, such as https://user:key@host/, in every text field,
        // also where the text is JSON, as a raw reply is: there the slashes may be escaped
        // (https:\/\/user:key@host), and the match stops at a quote or an escaped one, so it never
        // runs past the end of a JSON string. Any other character, an apostrophe or an escaped
        // backslash included, can be part of the user and password.
        private static readonly Regex UrlCredentials = new Regex(
            @"(?<scheme>[a-zA-Z][a-zA-Z0-9+.\-]*:(?:\\?/){2})(?:[^/\\\s@""]|\\[^/""\s])+@", RegexOptions.Compiled);

        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false);

        // Lines waiting for the worker, and whether a worker is draining them. Guarded by gate,
        // which Flush also waits on.
        private readonly Queue<string> pending = new Queue<string>();
        private readonly object gate = new object();
        private bool draining;
        private bool tailChecked;
        private int reported;

        private long linesWritten;
        private long linesDropped;
        private volatile bool includePrompt;
        private long maxFileBytes = DefaultMaxFileBytes;
        private volatile bool running;
        private volatile Exception lastError;

        private DecisionLog(string path, bool includePrompt)
        {
            Path = System.IO.Path.GetFullPath(path);
            this.includePrompt = includePrompt;
        }

        /// <summary>
        /// Starts a log that appends every feed entry to <paramref name="path"/>, creating the file and
        /// its folder when they are missing. Call it on the main thread, once per Play session.
        /// </summary>
        /// <param name="path">The file to append to, such as <see cref="DefaultPath"/>.</param>
        /// <param name="includePrompt">Whether each line carries what was sent: see <see cref="IncludePrompt"/>.</param>
        /// <exception cref="ArgumentException"><paramref name="path"/> is null, empty or not a valid path.</exception>
        public static DecisionLog Start(string path, bool includePrompt = false)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("A decision log needs a file to write to.", nameof(path));
            }

            var log = new DecisionLog(path, includePrompt);
            log.running = true;
            DecisionFeed.Recorded += log.Write;
            Application.quitting += log.Stop;
            return log;
        }

        /// <summary>
        /// A new file for this Play session: <c>Logs/agenerela-decisions-&lt;time&gt;.jsonl</c> beside
        /// <c>Assets</c> in the Editor, and under <c>Application.persistentDataPath</c> in a player.
        /// Call it on the main thread.
        /// </summary>
        public static string DefaultPath()
        {
            var folder = Application.isEditor
                ? System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Application.dataPath) ?? ".", "Logs")
                : Application.persistentDataPath;
            return System.IO.Path.Combine(folder, "agenerela-decisions-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".jsonl");
        }

        /// <summary>
        /// Where a full <paramref name="path"/> is moved when it reaches the cap: the same name with
        /// <c>.1</c> before the extension, such as <c>decisions.1.jsonl</c>.
        /// </summary>
        public static string RolloverPath(string path)
        {
            var folder = System.IO.Path.GetDirectoryName(path) ?? string.Empty;
            var name = System.IO.Path.GetFileNameWithoutExtension(path) + ".1" + System.IO.Path.GetExtension(path);
            return System.IO.Path.Combine(folder, name);
        }

        /// <summary>The file this log appends to, as a full path.</summary>
        public string Path { get; }

        /// <summary>
        /// Whether each line carries <see cref="DecisionLogLine.Request"/>: the system prompt, the
        /// few-shot block, the observations, the history, the stimulus turn and the actions and
        /// targets offered. Off by default, since it is large and may hold the player's own words.
        /// Read for each entry, so a change applies from the next one.
        /// </summary>
        public bool IncludePrompt
        {
            get => includePrompt;
            set => includePrompt = value;
        }

        /// <summary>
        /// The size, in bytes, past which the file is rolled over to <see cref="RolloverPath"/>.
        /// <see cref="DefaultMaxFileBytes"/> unless set. A line larger than the cap is still written,
        /// alone in a new file.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Set to 0 or less.</exception>
        public long MaxFileBytes
        {
            get => Interlocked.Read(ref maxFileBytes);
            set
            {
                if (value <= 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "The cap must be more than 0 bytes.");
                }

                Interlocked.Exchange(ref maxFileBytes, value);
            }
        }

        /// <summary>Whether the log is still subscribed to the feed: true from <see cref="Start"/> until <see cref="Stop"/>.</summary>
        public bool IsRunning => running;

        /// <summary>Lines written to the file so far.</summary>
        public long LinesWritten => Interlocked.Read(ref linesWritten);

        /// <summary>Entries that never reached the file, because a write or copying the entry failed.</summary>
        public long LinesDropped => Interlocked.Read(ref linesDropped);

        /// <summary>The last failure, or null when nothing has failed. Only the first is logged.</summary>
        public Exception LastError => lastError;

        /// <summary>
        /// Waits until every line queued so far is written, or <paramref name="timeout"/> passes.
        /// True when nothing is left to write. A game seldom needs it; a test does.
        /// </summary>
        public bool Flush(TimeSpan timeout)
        {
            var deadline = DateTime.UtcNow + timeout;
            lock (gate)
            {
                while (draining || pending.Count > 0)
                {
                    var left = deadline - DateTime.UtcNow;
                    if (left <= TimeSpan.Zero)
                    {
                        return false;
                    }

                    Monitor.Wait(gate, left);
                }

                return true;
            }
        }

        /// <summary>
        /// Unsubscribes from the feed and writes what is still queued, waiting up to two seconds for
        /// it. Safe to call more than once; <see cref="Dispose"/> does the same.
        /// </summary>
        public void Stop()
        {
            if (!running)
            {
                return;
            }

            running = false;
            DecisionFeed.Recorded -= Write;
            Application.quitting -= Stop;
            Flush(TimeSpan.FromSeconds(2));
        }

        /// <summary>The same as <see cref="Stop"/>.</summary>
        public void Dispose()
        {
            Stop();
        }

        // The feed's subscriber: copy the entry into a line now, while the telemetry still says what
        // this entry saw, and queue it for the worker. Never throws.
        internal void Write(DecisionFeedEntry entry)
        {
            Write(new[] { entry });
        }

        // Several entries queued at once, so the worker takes them as one batch: the feed's path for
        // one entry, and the tests' for a batch.
        internal void Write(IReadOnlyList<DecisionFeedEntry> entries)
        {
            var lines = new List<string>(entries.Count);
            foreach (var entry in entries)
            {
                try
                {
                    lines.Add(JsonConvert.SerializeObject(DecisionLogLine.From(entry, includePrompt), DecisionLogFormat.Settings));
                }
                catch (Exception e)
                {
                    Drop(1, e);
                }
            }

            if (lines.Count == 0)
            {
                return;
            }

            lock (gate)
            {
                foreach (var line in lines)
                {
                    pending.Enqueue(line);
                }

                if (draining)
                {
                    return;
                }

                draining = true;
            }

            if (!ThreadPool.QueueUserWorkItem(_ => Drain()))
            {
                Drain();
            }
        }

        // Writes queued lines until none are left. Only one runs at a time, so lines are never split
        // or reordered between writers.
        private void Drain()
        {
            while (true)
            {
                string[] batch;
                lock (gate)
                {
                    if (pending.Count == 0)
                    {
                        draining = false;
                        Monitor.PulseAll(gate);
                        return;
                    }

                    batch = pending.ToArray();
                    pending.Clear();
                }

                var written = 0;
                try
                {
                    Append(batch, ref written);
                }
                catch (Exception e)
                {
                    // A failed write may have left part of a line: check the tail again next time.
                    tailChecked = false;
                    Drop(batch.Length - written, e);
                }
                finally
                {
                    Interlocked.Add(ref linesWritten, written);
                }
            }
        }

        // Appends the lines in order, rolling the file over before any line that would take it past
        // the cap, so only a single line larger than the cap ever passes it. Returns through written
        // how many lines reached the file, so a failure partway counts only the rest as dropped.
        private void Append(string[] batch, ref int written)
        {
            var folder = System.IO.Path.GetDirectoryName(Path);
            if (!string.IsNullOrEmpty(folder))
            {
                Directory.CreateDirectory(folder);
            }

            var cap = MaxFileBytes;
            var next = 0;
            while (next < batch.Length)
            {
                var file = new FileInfo(Path);
                var length = file.Exists ? file.Length : 0;
                var first = Utf8.GetBytes(batch[next] + "\n");
                if (length > 0 && length + first.Length > cap)
                {
                    var rolled = RolloverPath(Path);
                    File.Delete(rolled);
                    File.Move(Path, rolled);
                    length = 0;
                    tailChecked = true;
                }

                // Counted as written only once the stream is closed, so lines a failed flush lost are dropped.
                var inStream = 0;
                using (var stream = new FileStream(Path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
                {
                    // The first write checks how the file ends: a line cut short by a process that
                    // died mid-write, or by a write that failed, is closed off, so the next line does
                    // not run into it. Marked checked only once the check and its repair succeed.
                    if (!tailChecked)
                    {
                        if (EndsMidLine())
                        {
                            stream.WriteByte((byte)'\n');
                            length++;
                        }

                        tailChecked = true;
                    }

                    // This line, and every one after it that still fits under the cap.
                    var bytes = first;
                    do
                    {
                        stream.Write(bytes, 0, bytes.Length);
                        length += bytes.Length;
                        next++;
                        inStream++;
                        bytes = next < batch.Length ? Utf8.GetBytes(batch[next] + "\n") : null;
                    }
                    while (bytes != null && length + bytes.Length <= cap);
                }

                written += inStream;
            }
        }

        private bool EndsMidLine()
        {
            using (var read = new FileStream(Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                if (read.Length == 0)
                {
                    return false;
                }

                read.Seek(-1, SeekOrigin.End);
                return read.ReadByte() != '\n';
            }
        }

        private void Drop(int lines, Exception error)
        {
            Interlocked.Add(ref linesDropped, lines);
            lastError = error;
            if (Interlocked.Exchange(ref reported, 1) == 0)
            {
                Debug.LogWarning(
                    $"The decision log could not write to {Path}, so {lines} line(s) are missing from it. " +
                    $"It keeps trying, and reports no further failures. {error.GetType().Name}: {error.Message}");
            }
        }

        internal static string MaskCredentials(string text)
        {
            return text == null ? null : UrlCredentials.Replace(text, "${scheme}***@");
        }
    }
}

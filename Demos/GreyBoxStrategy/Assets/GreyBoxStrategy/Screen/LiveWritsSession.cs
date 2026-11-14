using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Agenerela;
using Agenerela.Providers;
using GreyBoxStrategy.Replay;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace GreyBoxStrategy.Realm
{
    /// <summary>What the screen's live game reads, whichever turn it plays.</summary>
    public interface ILiveSession
    {
        bool Started { get; }
        bool Finished { get; }
        bool Stopped { get; }
        string Error { get; }
        GameResult Result { get; }
        string Status { get; }
        string LogPath { get; }
        Dictionary<string, RecordedPlayer> Players { get; }
        Awaitable RunAsync();
        void Stop();
        IEnumerable<string> Lines();
        Recording Recording();
    }

    /// <summary>
    /// A live writs game (rules v2, R1b): a <see cref="WritRuler"/> for every country, a court and a marshal
    /// each, playing the turn round by round as <see cref="WritModelRunner"/> does. Every decision is
    /// recorded as the game asked it, with the army a marshal's order is for, so the replay rebuilds the
    /// game and the inspector opens each one; the framework's <c>DecisionLog</c> gives each its prompt,
    /// facts and telemetry. Tests drive it with a <c>ScriptedProvider</c>.
    /// </summary>
    /// <remarks>
    /// The rulers' agents decide outside any <c>DecisionRound</c>, so their log lines carry no chain id to
    /// read a turn from. This session pairs each agent's decisions with that agent's log records in order,
    /// and checks the pairing by the stimulus the ruler sent; a pair that does not match is left without
    /// its log record rather than given another's.
    /// </remarks>
    public sealed class LiveWritsSession : ILiveSession
    {
        private readonly MapDefinition map;
        private readonly GameRules rules;
        private readonly string model;
        private readonly Func<string, World, ILLMProvider> providerFor;
        private readonly CancellationTokenSource cancel = new CancellationTokenSource();
        private readonly List<Asked> asked = new List<Asked>();
        private readonly object gate = new object();
        private readonly Dictionary<string, Persona> personas = new Dictionary<string, Persona>(StringComparer.Ordinal);
        private DecisionLog log;
        private long sequence;

        /// <summary>One decision the game asked for, as it was asked and answered.</summary>
        private sealed class Asked
        {
            public CountryRequest Request;
            public string AgentId;
            public string Stimulus;
            public CountryAnswer Answer;
            public ModelDecision Record;
            public long Sequence;
            public List<string> Offered;
        }

        public LiveWritsSession(string model, int seed, MapDefinition map, GameRules rules, int stopAfterTurns, string logPath, Func<string, World, ILLMProvider> providerFor)
        {
            if (rules == null || rules.Turn != TurnStyle.Writs) throw new ArgumentException("A live writs session plays the writs turn.");
            this.model = model; this.map = map; this.rules = rules; this.providerFor = providerFor;
            Seed = seed;
            StopAfterTurns = stopAfterTurns;
            LogPath = logPath;
            // The personas table's seats: every country a model ruler with its persona, as a live one-move game has.
            var table = Tables.Named("personas", model, null, "default", true);
            for (int i = 0; i < map.Countries.Count; i++)
            {
                var persona = table.PersonaFor(i % 5, 0);
                personas[map.Countries[i].Id] = persona;
                Players[map.Countries[i].Id] = new RecordedPlayer { Model = model, Persona = persona?.Id };
            }
        }

        public int Seed { get; }
        public int StopAfterTurns { get; }
        public string LogPath { get; }
        public bool Started { get; private set; }
        public bool Finished { get; private set; }
        public bool Stopped { get; private set; }
        public string Error { get; private set; }
        public GameResult Result { get; private set; }
        public Dictionary<string, RecordedPlayer> Players { get; } = new Dictionary<string, RecordedPlayer>(StringComparer.Ordinal);
        public string Status => !Started ? "not started" : Error != null ? "error: " + Error : !Finished ? "playing" : Stopped ? "stopped" : "finished";
        /// <summary>The game being played, for the tests and the readouts.</summary>
        public Game Game { get; private set; }
        public IReadOnlyDictionary<string, WritRuler> Seats => seats;
        private readonly Dictionary<string, WritRuler> seats = new Dictionary<string, WritRuler>(StringComparer.Ordinal);

        public async Awaitable RunAsync()
        {
            Started = true;
            if (LogPath != null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(LogPath));
                log = DecisionLog.Start(LogPath, includePrompt: true);
            }
            try
            {
                var game = new Game(Seed, map, rules);
                Game = game;
                var provider = providerFor(model, game.World);
                foreach (var c in game.World.CountryList)
                {
                    var seat = new WritRuler(game.World, c.Id, provider, null, personas.TryGetValue(c.Id, out var p) ? p : null) { Seed = Seed };
                    seats[c.Id] = seat;
                    game.Gates[c.Id] = seat;
                }
                int guard = rules.MaxTurns + 1;
                while (!game.IsOver)
                {
                    cancel.Token.ThrowIfCancellationRequested();
                    if (guard-- <= 0) throw new InvalidOperationException("The game did not end within its turn limit.");
                    // The turn limit is enforced before the rulers are asked, so no model sees the turn after it.
                    if (StopAfterTurns > 0 && game.World.Turn >= StopAfterTurns) { Stopped = true; break; }
                    var requests = game.BeginTurn();
                    while (requests.Count > 0)
                    {
                        var answers = new List<CountryAnswer>();
                        // One ruler's decisions back to back, as the rules page asks (v2 1.3).
                        foreach (var r in requests.OrderBy(r => r.CountryId, StringComparer.Ordinal))
                        {
                            var seat = seats[r.CountryId];
                            string stimulus = seat.StimulusFor(r);
                            int before = seat.Decisions.Count;
                            var answer = await seat.DecideAsync(r, cancel.Token);
                            answers.Add(answer);
                            lock (gate)
                                asked.Add(new Asked
                                {
                                    Request = r, Answer = answer, Stimulus = stimulus, Sequence = sequence++,
                                    AgentId = r.Moment == Moment.Army ? r.CountryId + RecordedDecision.MarshalSuffix : r.CountryId,
                                    Record = seat.Decisions.Count > before ? seat.Decisions[seat.Decisions.Count - 1] : null,
                                    Offered = r.Legal.Select(m => m.ToString()).ToList(),
                                });
                        }
                        requests = game.Submit(answers);
                    }
                    var report = game.Resolve();
                    foreach (var seat in seats.Values) seat.Learn(report);
                    foreach (var seat in seats.Values) seat.EndTurn();
                }
                Result = game.Result;
            }
            catch (OperationCanceledException) { Stopped = true; }
            catch (Exception e) { Error = e.Message; }
            finally
            {
                if (log != null)
                {
                    log.Flush(TimeSpan.FromSeconds(5));
                    log.Dispose();
                }
                Finished = true;
            }
        }

        public void Stop() => cancel.Cancel();

        public IEnumerable<string> Lines()
        {
            if (LogPath == null || !File.Exists(LogPath)) return Array.Empty<string>();
            if (!Finished) log?.Flush(TimeSpan.FromMilliseconds(200));
            using (var fs = new FileStream(LogPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (var r = new StreamReader(fs))
                return r.ReadToEnd().Split('\n');
        }

        /// <summary>
        /// The game so far as a recording: every decision as the game asked it, its army, what the agent
        /// did with it when it landed, and its log record (prompt, facts, guards, telemetry) where it pairs.
        /// </summary>
        public Recording Recording()
        {
            List<Asked> snapshot;
            lock (gate) snapshot = asked.ToList();
            var records = LogRecords();
            var rec = new Recording
            {
                Seed = Seed,
                Title = $"Live writs game, seed {Seed}: {model} on {(map.Source == null ? "the Classic board" : "a generated world")}",
                Source = "live",
                Players = Players,
            };
            var used = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var a in snapshot)
            {
                // The k-th decision of an agent is the k-th record of that agent in the log, if its stimulus says so.
                used.TryGetValue(a.AgentId, out int k);
                JObject raw = null;
                if (records.TryGetValue(a.AgentId, out var list) && k < list.Count && (string)list[k]["stimulus"] == a.Stimulus) { raw = list[k]; used[a.AgentId] = k + 1; }
                else if (records.TryGetValue(a.AgentId, out list) && k < list.Count) used[a.AgentId] = k + 1;
                rec.Decisions.Add(Decision(a, raw));
            }
            return rec;
        }

        private RecordedDecision Decision(Asked a, JObject raw)
        {
            var r = a.Request;
            string moment = r.Moment == Moment.Envoy ? "envoys" : r.Moment == Moment.Army ? RecordedDecision.ArmyMoment : "council";
            RecordedDecision d;
            if (raw != null)
            {
                var o = (JObject)raw.DeepClone();
                o["turn"] = r.Turn;
                o["moment"] = moment;
                d = GreyBoxStrategy.Replay.Recording.FromLogRecord(o);
            }
            else d = new RecordedDecision { Turn = r.Turn, Moment = moment, Stimulus = a.Stimulus };
            // What the game asked and took is the truth; the log record adds how the model got there.
            d.AgentId = a.AgentId;
            d.Turn = r.Turn;
            d.Moment = moment;
            d.ArmyId = r.ArmyId;
            d.Sequence = a.Sequence;
            d.ChainId = d.ChainId ?? $"seed-{Seed}-turn-{r.Turn}-{moment}{(r.ArmyId != null ? "-" + r.ArmyId : "")}";
            d.Action = a.Answer.Choice?.Action ?? Actions.None;
            d.Target = a.Answer.Choice?.Target ?? RecordedDecision.NoTarget;
            d.ModelName = model;
            var m = a.Record;
            if (m != null)
            {
                d.Statement = m.Statement ?? d.Statement ?? "";
                d.ModelAction = m.ModelAction ?? d.ModelAction;
                d.ModelTarget = m.ModelTarget ?? d.ModelTarget;
                d.LatencySeconds = d.LatencySeconds > 0 ? d.LatencySeconds : m.LatencySeconds;
                // How it ended when it landed: refused by the agent at Execute, as the replay's gate must refuse it too.
                if (m.Execution.HasValue) { d.ExecutionStatus = m.Execution.Value.ToString(); d.ExecutionReason = m.ExecutionReason; }
            }
            d.OfferedMoves = a.Offered;
            d.OfferedActions = r.Legal.Select(x => x.Action).Distinct().ToList();
            return d;
        }

        // The log's "Decided" records by agent, in the order written, with the execution of their "Executed" ones.
        private Dictionary<string, List<JObject>> LogRecords()
        {
            var byAgent = new Dictionary<string, List<JObject>>(StringComparer.Ordinal);
            foreach (string line in Lines())
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                JObject o;
                try { o = JObject.Parse(line); } catch (Newtonsoft.Json.JsonReaderException) { continue; } // a line still being written
                if ((string)o["stage"] != "Decided") continue;
                string agent = (string)o["agentId"];
                if (agent == null) continue;
                if (!byAgent.TryGetValue(agent, out var list)) byAgent[agent] = list = new List<JObject>();
                list.Add(o);
            }
            return byAgent;
        }
    }
}

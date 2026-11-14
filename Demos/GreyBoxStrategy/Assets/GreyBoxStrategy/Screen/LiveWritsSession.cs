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
        private readonly Dictionary<string, Persona> personas = new Dictionary<string, Persona>(StringComparer.Ordinal);
        private readonly WritsRecorder recorder;
        private readonly TableDefinition table;
        private readonly StoryRules story;
        private readonly string arm;
        private DecisionLog log;

        /// <param name="arm">The table arm: "default", or one with a storyteller ("default+storyteller+s2..."), which then tells before every upkeep.</param>
        public LiveWritsSession(string model, int seed, MapDefinition map, GameRules rules, int stopAfterTurns, string logPath, Func<string, World, ILLMProvider> providerFor, string arm = "default")
        {
            if (rules == null || rules.Turn != TurnStyle.Writs) throw new ArgumentException("A live writs session plays the writs turn.");
            this.model = model; this.map = map; this.rules = rules; this.providerFor = providerFor;
            recorder = new WritsRecorder(seed);
            Seed = seed;
            StopAfterTurns = stopAfterTurns;
            LogPath = logPath;
            // The personas table's seats: every country a model ruler with its persona, as a live one-move game has.
            this.arm = arm ?? "default";
            table = Tables.Named("personas", model, null, this.arm, true);
            story = table.Options.Storyteller ? table.Options.StoryRules() : null;
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
                Storyteller teller = null;
                if (story != null)
                {
                    // A storyteller game: its rules from the arm, no random table beside it, the teller on the same model.
                    game.EventTable = false;
                    game.Story = story;
                    teller = new Storyteller(game, provider, table.Options);
                }
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
                    if (teller != null)
                    {
                        await teller.TellAsync(cancel.Token);
                        // A dilemma: each ruler it reaches chooses through its court before the upkeep.
                        if (game.PendingChoices.Count > 0)
                        {
                            var chosen = new List<CountryAnswer>();
                            foreach (var r in game.ChoiceRequests())
                            {
                                var seat = seats[r.CountryId];
                                string stimulus = seat.StimulusFor(r);
                                int before = seat.Decisions.Count;
                                var answer = await seat.DecideAsync(r, cancel.Token);
                                chosen.Add(answer);
                                recorder.Model(r, answer, stimulus, seat.Decisions.Count > before ? seat.Decisions[seat.Decisions.Count - 1] : null, model);
                            }
                            game.SubmitChoices(chosen);
                        }
                    }
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
                            recorder.Model(r, answer, stimulus, seat.Decisions.Count > before ? seat.Decisions[seat.Decisions.Count - 1] : null, model);
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
            var rec = new Recording
            {
                Seed = Seed,
                Title = $"Live writs game, seed {Seed}: {model} on {(map.Source == null ? "the Classic board" : "a generated world")}",
                Source = "live",
                Players = Players,
            };
            var lines = Lines().ToList();
            if (story != null)
            {
                // The storyteller's and its scribe's decisions, read from the log by their stimulus as in a one-move game.
                rec.Decisions.AddRange(Replay.Recording.FromDecisionLog(lines, Seed, Players).Decisions.Where(d => d.IsStory || d.IsTitle));
                rec.StoryArm = arm;
            }
            rec.Decisions.AddRange(recorder.Decisions(lines));
            return rec;
        }
    }
}

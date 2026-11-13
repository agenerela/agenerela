using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Agenerela;
using Agenerela.Providers;
using GreyBoxStrategy.Replay;
using UnityEngine;

namespace GreyBoxStrategy.Realm
{
    /// <summary>
    /// One live game, without a scene: five model seats with personas played by the game's own
    /// <c>TableRunner</c>, every decision written by the framework's <c>DecisionLog</c>. The screen
    /// (<see cref="LiveGame"/>) reads the log as it grows; tests drive it with a
    /// <c>ScriptedProvider</c>.
    /// </summary>
    public sealed class LiveSession
    {
        private readonly TableDefinition table;
        private readonly Func<string, World, ILLMProvider> providerFor;
        private readonly CancellationTokenSource cancel = new CancellationTokenSource();
        private DecisionLog log;

        public LiveSession(string model, string arm, int seed, int stopAfterTurns, string logPath, Func<string, World, ILLMProvider> providerFor)
        {
            Seed = seed;
            StopAfterTurns = stopAfterTurns;
            LogPath = logPath;
            this.providerFor = providerFor;
            table = Tables.Named("personas", model, null, arm, true);
            StoryArm = table.Options.Storyteller ? arm : null;
            var map = MapDefinition.Standard();
            for (int i = 0; i < map.Countries.Count; i++)
                Players[map.Countries[i].Id] = new RecordedPlayer { Model = table.SeatFor(i, 0).Model, Persona = table.PersonaFor(i, 0)?.Id };
        }

        public int Seed { get; }
        /// <summary>The arm when the game has a storyteller, for the replay; null otherwise.</summary>
        public string StoryArm { get; }
        /// <summary>0 for a whole game; otherwise the game stops once this many turns have been played.</summary>
        public int StopAfterTurns { get; }
        public string LogPath { get; }
        public bool Started { get; private set; }
        public bool Finished { get; private set; }
        public bool Stopped { get; private set; }
        public string Error { get; private set; }
        public GameResult Result { get; private set; }
        public Dictionary<string, RecordedPlayer> Players { get; } = new Dictionary<string, RecordedPlayer>(StringComparer.Ordinal);

        public string Status => !Started ? "not started" : Error != null ? "error: " + Error : !Finished ? "playing" : Stopped ? "stopped" : "finished";

        /// <summary>Plays the game to its end, or to <see cref="StopAfterTurns"/>, or until <see cref="Stop"/>.</summary>
        public async Awaitable RunAsync()
        {
            Started = true;
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath));
            log = DecisionLog.Start(LogPath, includePrompt: true);
            try
            {
                // The turn limit is enforced where the game asks its rulers: the council after the
                // last allowed turn is cancelled before any model sees it, whatever the screen is
                // doing. (The first live run counted turns played back, and the model ran on.)
                var wrapped = new Dictionary<string, ILLMProvider>(StringComparer.Ordinal);
                ILLMProvider For(string model, World world)
                {
                    if (!wrapped.TryGetValue(model, out var p))
                        wrapped[model] = p = new TurnLimitedProvider(providerFor(model, world), world, StopAfterTurns, cancel);
                    return p;
                }
                var game = await TableRunner.PlayAsync(table, Seed, 0, For, cancel.Token);
                Result = game.Record.Result;
            }
            catch (OperationCanceledException) { Stopped = true; }
            catch (Exception e) { Error = e.Message; }
            finally
            {
                log.Flush(TimeSpan.FromSeconds(5));
                log.Dispose();
                Finished = true;
            }
        }

        public void Stop() => cancel.Cancel();

        /// <summary>Every line the log has written so far; read while it is still being written.</summary>
        public IEnumerable<string> Lines()
        {
            if (LogPath == null || !File.Exists(LogPath)) return Array.Empty<string>();
            if (!Finished) log?.Flush(TimeSpan.FromMilliseconds(200));
            using (var fs = new FileStream(LogPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (var r = new StreamReader(fs))
                return r.ReadToEnd().Split('\n');
        }

        /// <summary>The game so far, as the screen plays it.</summary>
        public ReplayGame Replay() => ReplayBuilder.Build(Recording(), partial: true);

        /// <summary>The game so far as a recording: the rulers', the storyteller's and the scribe's decisions from the log.</summary>
        public Recording Recording() => GreyBoxStrategy.Replay.Recording.FromDecisionLog(Lines(), Seed, Players, null, StoryArm);

        /// <summary>
        /// Passes requests through until the game asks for a turn past the limit, then cancels the
        /// game. A request carries no turn, but the world it is played on does.
        /// </summary>
        private sealed class TurnLimitedProvider : ILLMProvider
        {
            private readonly ILLMProvider inner;
            private readonly World world;
            private readonly int limit;
            private readonly CancellationTokenSource cancel;

            public TurnLimitedProvider(ILLMProvider inner, World world, int limit, CancellationTokenSource cancel)
            {
                this.inner = inner; this.world = world; this.limit = limit; this.cancel = cancel;
            }

            public string Name => inner.Name;
            public ProviderCapabilities Capabilities => inner.Capabilities;

            public Awaitable<ProviderResult> RequestAsync(DecisionRequest req, CancellationToken ct)
            {
                if (limit > 0 && world.Turn > limit) cancel.Cancel();
                return inner.RequestAsync(req, cancel.IsCancellationRequested ? cancel.Token : ct);
            }
        }
    }
}

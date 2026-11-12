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
    /// A live game for the screen: five model seats with personas, played by the game's own
    /// <c>TableRunner</c>, every decision written by the framework's <c>DecisionLog</c>. The screen
    /// reads that log as it grows and plays each turn once it has resolved. Off unless enabled on
    /// the scene's Watch object or started by <see cref="Begin"/>.
    /// </summary>
    /// <remarks>
    /// The server comes from <c>AGENERELA_OLLAMA_URL</c> and <c>AGENERELA_OLLAMA_API_KEY</c> in the
    /// environment Unity was started with, as the table runs read them. The RTX 5080 is shared: take
    /// <c>.scratch/ollama-5080-lock</c> before starting a live game.
    /// </remarks>
    public sealed class LiveGame : MonoBehaviour
    {
        [SerializeField] private bool playLive;
        [SerializeField] private string model = "qwen3.5:4b";
        [SerializeField] private string arm = "default";
        [SerializeField] private int seed = 101;
        [SerializeField] private int stopAfterTurns;

        public bool PlayLive => playLive;
        public int Seed => seed;
        public string LogPath { get; private set; }
        public bool Started { get; private set; }
        public bool Finished { get; private set; }
        public string Error { get; private set; }
        public Dictionary<string, RecordedPlayer> Players { get; } = new Dictionary<string, RecordedPlayer>(StringComparer.Ordinal);

        private DecisionLog log;
        private CancellationTokenSource cancel;
        private ILLMProvider provider;

        /// <summary>For the Unity CLI: start a live game before Play, or check what it is doing.</summary>
        public void Configure(bool live, int gameSeed, int turns = 0) { playLive = live; seed = gameSeed; stopAfterTurns = turns; }
        public string Status => !Started ? "not started" : Error != null ? "error: " + Error : Finished ? "finished" : "playing";

        public void Begin()
        {
            if (Started) return;
            Started = true;
            var table = Tables.Named("personas", model, null, arm, true);
            var map = MapDefinition.Standard();
            for (int i = 0; i < map.Countries.Count; i++)
                Players[map.Countries[i].Id] = new RecordedPlayer { Model = table.SeatFor(i, 0).Model, Persona = table.PersonaFor(i, 0)?.Id };

            string folder = Path.Combine(Application.dataPath, "..", "Logs", "Live");
            Directory.CreateDirectory(folder);
            LogPath = Path.GetFullPath(Path.Combine(folder, $"seed-{seed:D4}-{DateTime.Now:yyyyMMdd-HHmmss}.jsonl"));
            log = DecisionLog.Start(LogPath, includePrompt: true);
            cancel = new CancellationTokenSource();
            Run(table);
        }

        private async void Run(TableDefinition table)
        {
            try
            {
                ILLMProvider ProviderFor(string m, World world)
                {
                    return provider ?? (provider = new OllamaProvider(new OllamaSettings
                    {
                        Endpoint = Environment.GetEnvironmentVariable("AGENERELA_OLLAMA_URL") is string url && url.Length > 0 ? url : "http://127.0.0.1:11434",
                        ApiKey = Environment.GetEnvironmentVariable("AGENERELA_OLLAMA_API_KEY") ?? "",
                        Model = m,
                        TimeoutSeconds = 90f,
                    }));
                }
                Debug.Log($"Painted Realm: live game, seed {seed}, {model} with personas, arm {arm}; decisions into {LogPath}");
                var game = await TableRunner.PlayAsync(table, seed, 0, ProviderFor, cancel.Token);
                Debug.Log($"Painted Realm: live game over: {game.Record.Result.Winner} won on turn {game.Record.Result.Turns}");
            }
            catch (OperationCanceledException) { Debug.Log("Painted Realm: live game stopped"); }
            catch (Exception e) { Error = e.Message; Debug.LogException(e); }
            finally
            {
                log?.Flush(TimeSpan.FromSeconds(5));
                Finished = true;
            }
        }

        /// <summary>Every line the log has written so far; read while it is still being written.</summary>
        public IEnumerable<string> Lines()
        {
            if (LogPath == null || !File.Exists(LogPath)) return Array.Empty<string>();
            log?.Flush(TimeSpan.FromMilliseconds(200));
            using (var fs = new FileStream(LogPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (var r = new StreamReader(fs))
                return r.ReadToEnd().Split('\n');
        }

        /// <summary>Stops the game after <paramref name="turns"/> turns have been played back, for a short live check.</summary>
        public int StopAfterTurns => stopAfterTurns;
        public void Stop() { cancel?.Cancel(); }

        private void OnDestroy()
        {
            cancel?.Cancel();
            log?.Dispose();
        }
    }
}

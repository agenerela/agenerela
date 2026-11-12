using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Agenerela;
using Agenerela.Providers;
using GreyBoxStrategy.Replay;
using UnityEngine;

namespace GreyBoxStrategy.Realm
{
    /// <summary>
    /// The screen's live game: a <see cref="LiveSession"/> on the server in the environment, behind
    /// the RTX 5080's lock. It checks before it starts and says why it cannot, rather than hang:
    /// the two environment variables, the lock, and that the server answers.
    /// </summary>
    /// <remarks>
    /// The server is <c>AGENERELA_OLLAMA_URL</c>, the key <c>AGENERELA_OLLAMA_API_KEY</c>, read from
    /// the environment Unity was started in. The lock is the repository's
    /// <c>.scratch/ollama-5080-lock/held</c>: a live game takes it while the model plays and
    /// releases it as soon as the model is done (playback carries on from the log).
    /// </remarks>
    public sealed class LiveGame : MonoBehaviour
    {
        public const string UrlVariable = Gateway.UrlVariable;
        public const string KeyVariable = Gateway.KeyVariable;

        [SerializeField] private bool playLive;
        [SerializeField] private string model = "qwen3.5:4b";
        [SerializeField] private string arm = "default";
        [SerializeField] private int seed = 101;
        [SerializeField] private int stopAfterTurns;

        private LiveSession session;
        private bool tookLock;

        public bool PlayLive => playLive;
        public string Model => model;
        public int Seed => seed;
        public int StopAfterTurns => stopAfterTurns;
        public bool Started => session != null && session.Started;
        public bool Finished => session != null && session.Finished;
        public string Error { get; private set; }
        public string LogPath => session?.LogPath;
        public Dictionary<string, RecordedPlayer> Players => session?.Players ?? new Dictionary<string, RecordedPlayer>();
        public string Status => Error != null ? "error: " + Error : session?.Status ?? "not started";

        /// <summary>For the start menu and the Unity CLI.</summary>
        public void Configure(bool live, int gameSeed, int turns = 0, string modelName = null)
        {
            playLive = live; seed = gameSeed; stopAfterTurns = turns;
            if (!string.IsNullOrWhiteSpace(modelName)) model = modelName.Trim();
        }

        /// <summary>Why a live game cannot start now, or null when it can. Quick checks only; the server is checked by <see cref="Gateway.CheckServer"/>.</summary>
        public static string WhyNotNow()
        {
            string why = Gateway.WhyNotSet();
            if (why != null) return why;
            string holder = Gateway.LockHolder();
            return holder == null ? null : "The RTX 5080 is in use (its lock is held: " + holder + "). Try again when it is free.";
        }

        /// <summary>Starts the live game: takes the lock, then plays. Call only after <see cref="WhyNotNow"/> and <see cref="Gateway.CheckServer"/> said yes.</summary>
        public void Begin()
        {
            if (session != null) return;
            if (!Gateway.TakeLock("Painted Realm live game (GreyBoxStrategy Watch scene)", $"one live game, {model} x5 with personas, seed {seed}; a few minutes", model)) { Error = WhyNotNow() ?? "could not take the RTX 5080 lock"; return; }
            tookLock = true;

            string folder = Path.Combine(Application.dataPath, "..", "Logs", "Live");
            string path = Path.GetFullPath(Path.Combine(folder, $"seed-{seed:D4}-{DateTime.Now:yyyyMMdd-HHmmss}.jsonl"));
            ILLMProvider provider = null;
            session = new LiveSession(model, arm, seed, stopAfterTurns, path, (m, world) => provider ?? (provider = Gateway.Provider(m)));
            Debug.Log($"Painted Realm: live game, seed {seed}, {model} with personas, arm {arm}; decisions into {path}");
            Run();
        }

        /// <summary>True for a game played with stand-in rulers from the dev menu: never a measured game.</summary>
        public bool Dev { get; private set; }

        /// <summary>
        /// The dev menu's watch game: five stand-in rulers (a <c>ScriptedProvider</c> picking moves
        /// the rules offer, through the real agents), no server and no card.
        /// </summary>
        public void BeginScripted(int gameSeed)
        {
            if (session != null) return;
            playLive = true; seed = gameSeed; model = "scripted"; Dev = true;
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "Live", $"dev-seed-{seed:D4}-{DateTime.Now:yyyyMMdd-HHmmss}.jsonl"));
            var rng = new System.Random(gameSeed);
            World world = null;
            var scripted = new ScriptedProvider { Callback = r => ReplayDirector.ScriptedRuler(r, rng, world) };
            scripted.Otherwise(ScriptedReply.Decide("none", statement: "We wait."));
            var provider = DevProvider.Wrap(scripted);
            session = new LiveSession(model, arm, seed, stopAfterTurns, path, (m, w) => { world = w; return provider; });
            Run();
        }

        private async void Run()
        {
            await session.RunAsync();
            if (session.Error != null) { Error = session.Error; Debug.LogWarning("Painted Realm: live game stopped: " + session.Error); }
            else if (session.Result != null) Debug.Log($"Painted Realm: live game over: {session.Result.Winner} won on turn {session.Result.Turns}");
            if (tookLock) await Gateway.Unload(model);
            ReleaseLock();
        }

        private void ReleaseLock()
        {
            if (!tookLock) return;
            tookLock = false;
            Gateway.ReleaseLock();
        }

        public IEnumerable<string> Lines() => session?.Lines() ?? Array.Empty<string>();
        public void Stop() => session?.Stop();

        /// <summary>Whether this game holds the RTX 5080 now.</summary>
        public bool HoldsCard => tookLock;

        private void OnDestroy()
        {
            session?.Stop();
            if (tookLock) { tookLock = false; Gateway.ReleaseHeldNow(); } // unloads the model too
        }
    }
}

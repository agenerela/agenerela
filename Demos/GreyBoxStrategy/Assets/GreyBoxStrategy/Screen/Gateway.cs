using System;
using System.Collections;
using System.IO;
using System.Threading;
using Agenerela;
using Agenerela.Providers;
using UnityEngine;

namespace GreyBoxStrategy.Realm
{
    /// <summary>
    /// The server a live game or a rule game talks to, and the RTX 5080's lock: the two
    /// environment variables, the quick checks, the server check, the provider and the unload.
    /// </summary>
    /// <remarks>
    /// The server is <c>AGENERELA_OLLAMA_URL</c>, the key <c>AGENERELA_OLLAMA_API_KEY</c>, read from
    /// the environment Unity was started in. The lock is the repository's
    /// <c>.scratch/ollama-5080-lock/held</c>, a directory taken atomically and holding
    /// <c>owner.txt</c>, shared with every session that uses the card.
    /// </remarks>
    public static class Gateway
    {
        public const string UrlVariable = "AGENERELA_OLLAMA_URL";
        public const string KeyVariable = "AGENERELA_OLLAMA_API_KEY";

        public static string Url => Environment.GetEnvironmentVariable(UrlVariable)?.TrimEnd('/');
        private static string Key => Environment.GetEnvironmentVariable(KeyVariable) ?? "";

        /// <summary>
        /// The lock the RTX 5080's users share: <c>.scratch/ollama-5080-lock/held</c> in the main
        /// checkout. From a git worktree, its <c>.git</c> file leads back to the main checkout, so
        /// every worktree and the main checkout share one lock.
        /// </summary>
        public static string LockPath => Path.Combine(MainCheckout(Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", ".."))), ".scratch", "ollama-5080-lock", "held");

        /// <summary>The main checkout of the repository at <paramref name="root"/>: itself, or, for a worktree, the one its <c>.git</c> file names.</summary>
        public static string MainCheckout(string root)
        {
            string git = Path.Combine(root, ".git");
            if (!File.Exists(git)) return root;
            try
            {
                // "gitdir: <main>/.git/worktrees/<name>"
                string line = File.ReadAllText(git).Trim();
                if (!line.StartsWith("gitdir:", StringComparison.Ordinal)) return root;
                var dir = new DirectoryInfo(Path.GetFullPath(Path.Combine(root, line.Substring(7).Trim())));
                while (dir != null && dir.Name != ".git") dir = dir.Parent;
                return dir?.Parent?.FullName ?? root;
            }
            catch (IOException) { return root; }
        }

        /// <summary>Why the variables are not usable, or null.</summary>
        public static string WhyNotSet()
        {
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(UrlVariable)))
                return $"{UrlVariable} is not set in the environment Unity was started from. Set it and {KeyVariable}, then restart Unity and Unity Hub.";
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(KeyVariable)))
                return $"{KeyVariable} is not set in the environment Unity was started from. Set it, then restart Unity and Unity Hub.";
            return null;
        }

        /// <summary>Who holds the lock, as its owner line, or null when it is free.</summary>
        public static string LockHolder() => ReadLock()?.Owner;

        /// <summary>The lock's owner.txt: who, since when, and what they expect, or null when it is free.</summary>
        public sealed class LockNote
        {
            public string Owner = "someone", Expect;
            public DateTime? Since;
            public int? Pid;

            /// <summary>"owner (since 23:41, expects 30 min)"</summary>
            public override string ToString()
            {
                var parts = new System.Collections.Generic.List<string>();
                if (Since != null) parts.Add($"since {Since.Value:HH:mm}");
                if (!string.IsNullOrEmpty(Expect)) parts.Add("expects " + Expect);
                return parts.Count == 0 ? Owner : $"{Owner} ({string.Join(", ", parts)})";
            }
        }

        public static LockNote ReadLock() => ReadLock(LockPath);

        public static LockNote ReadLock(string lockPath)
        {
            // The dev menu's pretended holder: shown everywhere, and no real lock is read or taken.
            if (DevFaults.FakeLockHolder != null) return new LockNote { Owner = DevFaults.FakeLockHolder, Since = DateTime.Now.AddMinutes(-3), Expect = "pretended by the dev menu" };
            if (!Directory.Exists(lockPath)) return null;
            var note = new LockNote();
            try
            {
                foreach (string line in File.ReadAllLines(Path.Combine(lockPath, "owner.txt")))
                {
                    int colon = line.IndexOf(':');
                    if (colon < 0) continue;
                    string key = line.Substring(0, colon).Trim(), value = line.Substring(colon + 1).Trim();
                    if (key == "owner") note.Owner = value;
                    else if (key == "expect") note.Expect = value;
                    else if (key == "since" && DateTime.TryParse(value, out var since)) note.Since = since;
                    else if (key == "pid" && int.TryParse(value.Split(' ')[0], out int pid)) note.Pid = pid;
                }
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            return note;
        }

        private static int Pid => System.Diagnostics.Process.GetCurrentProcess().Id;

        // The lock this Unity took, if any, so every way out of the game can let it go.
        private static string heldPath, heldModel;

        /// <summary>Takes the lock if it is free. False when someone else holds it.</summary>
        public static bool TakeLock(string owner, string what, string model = null) => TakeLock(LockPath, owner, what, model);

        public static bool TakeLock(string lockPath, string owner, string what, string model)
        {
            if (DevFaults.FakeLockHolder != null) return false; // pretend someone holds it; touch nothing
            if (Directory.Exists(lockPath)) return false;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(lockPath));
                Directory.CreateDirectory(lockPath);
                File.WriteAllText(Path.Combine(lockPath, "owner.txt"),
                    $"owner: {owner}\npid: {Pid} (Unity)\nwhat: {what}\nsince: {DateTime.Now:s}\nexpect: until the game ends or is left\n");
                heldPath = lockPath;
                heldModel = model;
                WatchExits();
                return true;
            }
            catch (IOException e) { Debug.LogWarning("Painted Realm: could not take the RTX 5080 lock: " + e.Message); return false; }
        }

        /// <summary>Releases the lock, but only one this Unity took: another session's lock is never touched.</summary>
        public static void ReleaseLock() => ReleaseLock(LockPath);

        public static void ReleaseLock(string lockPath)
        {
            try
            {
                var note = ReadLock(lockPath);
                if (note != null && (note.Pid == null || note.Pid == Pid)) Directory.Delete(lockPath, true);
            }
            catch (IOException e) { Debug.LogWarning("Painted Realm: could not release the RTX 5080 lock: " + e.Message); }
            if (heldPath == lockPath) { heldPath = null; heldModel = null; }
        }

        /// <summary>Whether this Unity holds the RTX 5080's lock now.</summary>
        public static bool HoldsLock => heldPath != null;

        private static bool watching;

        // Quitting, leaving Play mode and a script reload all end the game without its own code
        // running to the end: each lets the card go here, at once.
        private static void WatchExits()
        {
            if (watching) return;
            watching = true;
            Application.quitting += ReleaseHeldNow;
#if UNITY_EDITOR
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += ReleaseHeldNow;
            UnityEditor.EditorApplication.playModeStateChanged += s => { if (s == UnityEditor.PlayModeStateChange.ExitingPlayMode) ReleaseHeldNow(); };
#endif
        }

        /// <summary>Unloads the model (a few seconds at most) and releases the lock this Unity holds, if any.</summary>
        public static void ReleaseHeldNow()
        {
            if (heldPath == null) return;
            string path = heldPath, model = heldModel;
            if (model != null && !string.IsNullOrEmpty(Url))
            {
                try { http.SendAsync(Request(System.Net.Http.HttpMethod.Post, "/api/generate", "{\"model\":\"" + model + "\",\"keep_alive\":0}")).Wait(TimeSpan.FromSeconds(3)); }
                catch (Exception) { }
            }
            ReleaseLock(path);
        }

        // HttpClient, as the framework's OllamaProvider uses: UnityWebRequest refuses a plain-HTTP
        // gateway unless the project allows insecure connections, and this one does not.
        private static readonly System.Net.Http.HttpClient http = new System.Net.Http.HttpClient();

        private static System.Net.Http.HttpRequestMessage Request(System.Net.Http.HttpMethod method, string path, string json = null)
        {
            var req = new System.Net.Http.HttpRequestMessage(method, Url + path);
            req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", Key);
            if (json != null) req.Content = new System.Net.Http.StringContent(json, System.Text.Encoding.UTF8, "application/json");
            return req;
        }

        /// <summary>Asks the gateway for its version, with a short timeout. Calls back with null when it answers.</summary>
        public static IEnumerator CheckServer(Action<string> done)
        {
            if (DevFaults.ServerDown)
            {
                yield return new WaitForSeconds(1f);
                done("Cannot reach the server at " + UrlVariable + " (pretended by the dev menu). Check the network or the VPN.");
                yield break;
            }
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(6));
            var task = http.SendAsync(Request(System.Net.Http.HttpMethod.Get, "/api/version"), cts.Token);
            while (!task.IsCompleted) yield return null;
            if (task.IsFaulted || task.IsCanceled)
            {
                string why = task.IsCanceled ? "no answer in 6 s" : task.Exception?.GetBaseException().Message;
                done("Cannot reach the server at " + UrlVariable + " (" + why + "). Check the network or the VPN.");
                yield break;
            }
            using (var res = task.Result)
            {
                if (res.IsSuccessStatusCode) done(null);
                else if ((int)res.StatusCode == 401) done("The server refused the key in " + KeyVariable + ".");
                else done("The server at " + UrlVariable + " answered " + (int)res.StatusCode + ".");
            }
        }

        public static ILLMProvider Provider(string model) => DevProvider.Wrap(new OllamaProvider(new OllamaSettings
        {
            Endpoint = Environment.GetEnvironmentVariable(UrlVariable),
            ApiKey = Key,
            Model = model,
            TimeoutSeconds = 90f,
        }));

        /// <summary>Leaves the card as it was found: asks the server to unload the model (keep_alive 0).</summary>
        public static async Awaitable Unload(string model)
        {
            if (string.IsNullOrEmpty(Url)) return;
            try
            {
                var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
                var task = http.SendAsync(Request(System.Net.Http.HttpMethod.Post, "/api/generate", "{\"model\":\"" + model + "\",\"keep_alive\":0}"), cts.Token);
                while (!task.IsCompleted) await Awaitable.NextFrameAsync();
                using (var res = await task)
                    if (!res.IsSuccessStatusCode) Debug.LogWarning("Painted Realm: could not unload " + model + ": the server answered " + (int)res.StatusCode);
            }
            catch (Exception e)
            {
                Debug.LogWarning("Painted Realm: could not unload " + model + ": " + e.GetBaseException().Message);
            }
        }
    }

    /// <summary>
    /// The RTX 5080's lock for a rule game: taken before the first council and held until the game
    /// ends or is left, with the model unloaded when it is let go (the lead's decision, 12 November).
    /// While another session holds it, the game waits and says who.
    /// </summary>
    public sealed class CardLock : RuleSession.ICardLock
    {
        private readonly string model, what;
        private bool held;

        public CardLock(string model, string what) { this.model = model; this.what = what; }

        public string WaitingFor { get; private set; }
        public bool Held => held;

        public async Awaitable Acquire(CancellationToken ct)
        {
            while (!Gateway.TakeLock("Painted Realm rule game (GreyBoxStrategy Watch scene)", what, model))
            {
                WaitingFor = Gateway.ReadLock()?.ToString() ?? "someone";
                await Awaitable.WaitForSecondsAsync(2f, ct);
            }
            WaitingFor = null;
            held = true;
        }

        public async Awaitable Release()
        {
            if (!held) return;
            held = false;
            try { await Gateway.Unload(model); }
            finally { Gateway.ReleaseLock(); }
        }

        /// <summary>For leaving the game: unload and let go at once.</summary>
        public void ReleaseNow()
        {
            if (!held) return;
            held = false;
            Gateway.ReleaseHeldNow();
        }
    }
}

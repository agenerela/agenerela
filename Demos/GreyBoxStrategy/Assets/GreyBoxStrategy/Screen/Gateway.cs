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
        public static string LockHolder()
        {
            if (!Directory.Exists(LockPath)) return null;
            try { var lines = File.ReadAllLines(Path.Combine(LockPath, "owner.txt")); return lines.Length > 0 ? System.Text.RegularExpressions.Regex.Replace(lines[0], @"^owner:\s*", "") : "someone"; }
            catch (IOException) { return "someone"; }
            catch (UnauthorizedAccessException) { return "someone"; }
        }

        /// <summary>Takes the lock if it is free. False when someone else holds it.</summary>
        public static bool TakeLock(string owner, string what)
        {
            if (Directory.Exists(LockPath)) return false;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(LockPath));
                Directory.CreateDirectory(LockPath);
                File.WriteAllText(Path.Combine(LockPath, "owner.txt"), $"owner: {owner}\nwhat: {what}\nsince: {DateTime.Now:s}\n");
                return true;
            }
            catch (IOException e) { Debug.LogWarning("Painted Realm: could not take the RTX 5080 lock: " + e.Message); return false; }
        }

        public static void ReleaseLock()
        {
            try { if (Directory.Exists(LockPath)) Directory.Delete(LockPath, true); }
            catch (IOException e) { Debug.LogWarning("Painted Realm: could not release the RTX 5080 lock: " + e.Message); }
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

        public static ILLMProvider Provider(string model) => new OllamaProvider(new OllamaSettings
        {
            Endpoint = Environment.GetEnvironmentVariable(UrlVariable),
            ApiKey = Key,
            Model = model,
            TimeoutSeconds = 90f,
        });

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
    /// The RTX 5080's lock for a rule game: taken for each round of model calls and released, with
    /// the model unloaded, as soon as the round is decided. The card is never held while the game
    /// waits for the person's order, and a round waits its turn when another session holds it.
    /// </summary>
    public sealed class CardLock : RuleSession.ICardLock
    {
        private readonly string model, what;
        private bool held;

        public CardLock(string model, string what) { this.model = model; this.what = what; }

        public string WaitingFor { get; private set; }

        public async Awaitable Acquire(CancellationToken ct)
        {
            while (!Gateway.TakeLock("Painted Realm rule game (GreyBoxStrategy Watch scene)", what + "; one council round at a time, released between rounds"))
            {
                WaitingFor = Gateway.LockHolder() ?? "someone";
                await Awaitable.WaitForSecondsAsync(3f, ct);
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

        /// <summary>For OnDestroy: let go at once, without waiting on the server.</summary>
        public void ReleaseNow()
        {
            if (!held) return;
            held = false;
            Gateway.ReleaseLock();
        }
    }
}

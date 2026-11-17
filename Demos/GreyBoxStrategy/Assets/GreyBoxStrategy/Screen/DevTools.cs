using System;
using System.Linq;
using System.Threading;
using Agenerela.Providers;
using UnityEngine;

namespace GreyBoxStrategy.Realm
{
    /// <summary>
    /// Pretended faults, for seeing the messages #268 added without breaking anything real. None of
    /// them touches the RTX 5080's lock directory or the server: the lock and the server check read
    /// these first, and <see cref="DevProvider"/> answers in place of the model.
    /// </summary>
    public static class DevFaults
    {
        /// <summary>Who pretends to hold the card; null for nobody. While set, the game never takes the real lock.</summary>
        public static string FakeLockHolder;
        /// <summary>The server check says the server cannot be reached.</summary>
        public static bool ServerDown;
        /// <summary>Every model request hangs until it is cancelled, so a round times out.</summary>
        public static bool Hang;
        /// <summary>Every model request comes back as text no parser can read.</summary>
        public static bool Unreadable;

        public static bool Any => FakeLockHolder != null || ServerDown || Hang || Unreadable;

        public static void Clear() { FakeLockHolder = null; ServerDown = false; Hang = false; Unreadable = false; }

        public static string Describe() => !Any ? "none" :
            string.Join(", ", new[]
            {
                FakeLockHolder != null ? $"lock held by \"{FakeLockHolder}\"" : null,
                ServerDown ? "server unreachable" : null,
                Hang ? "rounds hang" : null,
                Unreadable ? "replies unreadable" : null,
            }.Where(x => x != null));
    }

    /// <summary>
    /// Every provider the screen makes goes through this: it counts requests in flight, keeps the
    /// last reply's latency and tokens for the dev menu's readouts, and applies <see cref="DevFaults"/>.
    /// With no fault set it passes every request through unchanged.
    /// </summary>
    public sealed class DevProvider : ILLMProvider
    {
        private static int inFlight;
        private readonly ILLMProvider inner;

        public DevProvider(ILLMProvider inner) { this.inner = inner ?? throw new ArgumentNullException(nameof(inner)); }

        public static ILLMProvider Wrap(ILLMProvider p) => p is DevProvider ? p : new DevProvider(p);

        public static int InFlight => inFlight;
        public static int Requests { get; private set; }
        public static float LastLatency { get; private set; }
        public static int LastPromptTokens { get; private set; }
        public static int LastCompletionTokens { get; private set; }

        public string Name => inner.Name;
        public ProviderCapabilities Capabilities => inner.Capabilities;

        public async Awaitable<ProviderResult> RequestAsync(DecisionRequest req, CancellationToken ct)
        {
            Interlocked.Increment(ref inFlight);
            Requests++;
            try
            {
                if (DevFaults.Hang)
                {
                    while (true) { ct.ThrowIfCancellationRequested(); await System.Threading.Tasks.Task.Delay(50); }
                }
                if (DevFaults.Unreadable)
                    return new ProviderResult { Text = "{\"action\": \"attack\", \"target\": <<unreadable: pretended by the dev menu", LatencySeconds = 0.05f, Model = "dev-fault" };
                var r = await inner.RequestAsync(req, ct);
                if (r != null) { LastLatency = r.LatencySeconds; LastPromptTokens = r.PromptTokens; LastCompletionTokens = r.CompletionTokens; }
                return r;
            }
            finally { Interlocked.Decrement(ref inFlight); }
        }
    }
}

namespace GreyBoxStrategy.Realm
{
    /// <summary>
    /// The dev menu's one entry point from code: <c>DevTools.Run("turn 12")</c>,
    /// <c>DevTools.Run("scripted-rule kesh")</c>, <c>DevTools.Run("gold kesh +100")</c>. Each returns
    /// what happened as a line of text. <c>DevTools.Run("help")</c> lists every command.
    /// </summary>
    /// <remarks>Only in the editor and in development builds (<c>Debug.isDebugBuild</c>).</remarks>
    public static class DevTools
    {
        public static bool Available => Debug.isDebugBuild;

        internal static ReplayDirector Director;

        public static string Run(string command)
        {
            if (!Available) return "The dev menu is only in the editor and development builds.";
            if (Director == null) return "No Painted Realm screen is running: open Watch.unity and press Play.";
            return Director.Dev.Run(command);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Agenerela.Providers
{
    /// <summary>
    /// A provider with no model behind it: it answers each request with a reply the developer
    /// scripted, keyed by the asking agent and its stimulus or chosen by a callback, after a
    /// latency of the developer's choosing. For tests, headless captures, and presenting a game
    /// with the model off (#190, G6). <see cref="ScriptedProviderConfig"/> makes one an asset, so a
    /// scene can select it like any other provider.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>It never passes for a model.</b> <see cref="Name"/> is always <see cref="ProviderName"/>,
    /// and every reply's <see cref="ProviderResult.Model"/> is too, so telemetry, a report or a
    /// HUD says "scripted" wherever it would name the model, whatever the reply replays.
    /// </para>
    /// <para>
    /// <b>Which reply answers.</b> A decision request is answered by the first of:
    /// </para>
    /// <list type="number">
    ///   <item><description>the replies scripted for this agent and this stimulus, <see cref="When"/>;</description></item>
    ///   <item><description>those for this stimulus from any agent, <c>When(null, stimulus, …)</c>;</description></item>
    ///   <item><description>those for this agent with any stimulus, <c>When(agent, null, …)</c>;</description></item>
    ///   <item><description><see cref="Callback"/>, when it is set and returns a reply;</description></item>
    ///   <item><description><see cref="Otherwise"/>.</description></item>
    /// </list>
    /// <para>
    /// The agent is matched by its <see cref="Agenerela.Agent.Id"/> when it has one, then by its
    /// <see cref="AgentIdentity.Name"/>, within each stage above. Ids and names share one key, so a
    /// script written for <c>"npc_mara"</c> answers the agent with that id, and one written for
    /// <c>"Mara"</c> answers every agent of that name that no id script answered. The stimulus is
    /// matched by the exact text passed to <see cref="Agenerela.Agent.DecideAsync"/>, without its
    /// label. Both are case-sensitive.
    /// Several replies for one key answer in order, and the last keeps answering once they run out.
    /// With none of these, the request fails with a <see cref="ProviderException"/> of
    /// <see cref="ProviderErrorKind.Rejected"/> naming the agent and stimulus, so a test that asks
    /// something it did not script finds out. A refusal-line request
    /// (<see cref="ScriptedRequest.IsRefusalLine"/>) takes no keyed reply: <see cref="Callback"/>
    /// answers it, then <see cref="RefusalLine"/>, else it fails and the decision stands without a line.
    /// </para>
    /// <para>
    /// <b>Latency</b> is wall-clock time, waited before the reply arrives
    /// (<see cref="ScriptedReply.After"/>), so <see cref="DecisionTelemetry.LatencySeconds"/> records
    /// it as it would a model's. A reply with none answers within <see cref="RequestAsync"/>, so
    /// <c>await agent.DecideAsync(…)</c> completes in the call. Cancelling the request ends the wait.
    /// <see cref="Hold"/> keeps replies back until <see cref="Release"/>, for a test that needs a
    /// request to stay with the provider while something else happens.
    /// </para>
    /// <para>
    /// <b>Replaying a decision log</b> (#190, G7, not built yet) goes through this class as it is:
    /// one <see cref="When"/> per agent and stimulus, with the log's replies in order, each
    /// <see cref="ScriptedReply.Raw"/> text <see cref="ScriptedReply.After"/> its recorded latency.
    /// </para>
    /// <para>
    /// Scripting a key, a callback or the fallback, <see cref="Hold"/> and <see cref="Release"/>
    /// belong on the main thread, where agents call <see cref="RequestAsync"/>. A delayed reply
    /// arrives there too when Unity's main-thread context is present, as in a game or a test.
    /// Nothing here is static, so nothing outlives a Play session without a domain reload.
    /// </para>
    /// </remarks>
    public sealed class ScriptedProvider : ILLMProvider
    {
        /// <summary>What <see cref="Name"/> and every reply's <see cref="ProviderResult.Model"/> say.</summary>
        public const string ProviderName = "scripted";

        private readonly Dictionary<Key, Script> scripts = new Dictionary<Key, Script>();

        // The requests Hold kept back, oldest first. Guarded by gate: a cancellation can remove one
        // from any thread.
        private readonly List<HeldRequest> held = new List<HeldRequest>();
        private readonly object gate = new object();

        /// <summary>
        /// A provider whose capabilities are what a scripted reply can honestly claim: no constrained
        /// decoding and no token probabilities, with the schema in <see cref="SchemaDialect.JsonSchema"/>.
        /// So with <see cref="DecideOptions.CheckTarget"/> on, the prompt carries the one-line target
        /// rule, as for any provider without token probabilities (#107).
        /// </summary>
        public ScriptedProvider()
            : this(null)
        {
        }

        /// <param name="capabilities">
        /// What it claims to be able to do, so the requests it receives are the ones a real backend
        /// would get: pass Ollama's (<c>ReportsTokenProbabilities = true</c>) to receive Ollama's
        /// prompt. Null for the defaults above. Fixed for the provider's lifetime, as the contract asks.
        /// </param>
        public ScriptedProvider(ProviderCapabilities capabilities)
        {
            Capabilities = capabilities ?? new ProviderCapabilities
            {
                SupportsConstrainedDecoding = false,
                Dialect = SchemaDialect.JsonSchema,
                ReportsTokenProbabilities = false,
            };
        }

        /// <summary>Always <see cref="ProviderName"/>, which telemetry records.</summary>
        public string Name => ProviderName;

        /// <inheritdoc />
        public ProviderCapabilities Capabilities { get; }

        /// <summary>
        /// Chooses a reply for a request no key answers, or for a refusal line: return one, or null
        /// to leave it to <see cref="Otherwise"/> (or <see cref="RefusalLine"/>). It may read the
        /// agent, the stimulus and what the schema offers (<see cref="ScriptedRequest"/>), as a game's
        /// keyword rules would. If it throws, the request fails with that exception.
        /// </summary>
        public Func<ScriptedRequest, ScriptedReply> Callback { get; set; }

        /// <summary>
        /// The answer to a refusal-line request that <see cref="Callback"/> leaves, usually a
        /// <see cref="ScriptedReply.Statement"/>. Null fails the request, and the decision stands
        /// with no line, as when a model's line cannot be read.
        /// </summary>
        public ScriptedReply RefusalLine { get; set; }

        /// <summary>
        /// While true, each new request waits, its reply already chosen, until <see cref="Release"/>
        /// or cancellation. Turning it off releases nothing: requests already held stay held.
        /// </summary>
        public bool Hold { get; set; }

        /// <summary>How many requests <see cref="Hold"/> is keeping back now.</summary>
        public int HeldCount
        {
            get
            {
                lock (gate)
                {
                    return held.Count;
                }
            }
        }

        /// <summary>How many requests it has received, refusal lines included.</summary>
        public int RequestCount { get; private set; }

        /// <summary>The last request received, or null before the first.</summary>
        public ScriptedRequest LastRequest { get; private set; }

        /// <summary>
        /// Scripts how <paramref name="agent"/> answers <paramref name="stimulus"/>: with
        /// <paramref name="replies"/> in order, the last repeating once they run out. Replaces
        /// anything scripted for the same pair, and starts it from its first reply.
        /// </summary>
        /// <param name="agent">
        /// The agent's <see cref="Agenerela.Agent.Id"/> or its <see cref="AgentIdentity.Name"/>; null or
        /// empty for any agent. An agent with an id is matched by its id before its name.
        /// </param>
        /// <param name="stimulus">The stimulus as passed to <c>DecideAsync</c>, without its label; null or empty for any stimulus.</param>
        /// <param name="replies">One reply or more.</param>
        /// <returns>This provider, so scripts read as one statement.</returns>
        /// <exception cref="ArgumentException"><paramref name="replies"/> is empty.</exception>
        /// <exception cref="ArgumentNullException"><paramref name="replies"/> or one of them is null.</exception>
        public ScriptedProvider When(string agent, string stimulus, params ScriptedReply[] replies)
        {
            if (replies == null)
            {
                throw new ArgumentNullException(nameof(replies));
            }

            if (replies.Length == 0)
            {
                throw new ArgumentException("Script at least one reply.", nameof(replies));
            }

            foreach (var reply in replies)
            {
                if (reply == null)
                {
                    throw new ArgumentNullException(nameof(replies), "A scripted reply is null.");
                }
            }

            scripts[new Key(agent, stimulus)] = new Script((ScriptedReply[])replies.Clone());
            return this;
        }

        /// <summary>
        /// Scripts the answer to every decision request nothing else answers, any agent, any
        /// stimulus: <c>When(null, null, replies)</c>.
        /// </summary>
        public ScriptedProvider Otherwise(params ScriptedReply[] replies)
        {
            return When(null, null, replies);
        }

        /// <summary>
        /// Forgets every scripted reply, <see cref="Otherwise"/> included. <see cref="Callback"/>,
        /// <see cref="RefusalLine"/> and the requests held stay as they are.
        /// </summary>
        public void Clear()
        {
            scripts.Clear();
        }

        /// <summary>
        /// Lets the oldest request <see cref="Hold"/> kept back go on with its reply: at once, or once
        /// its latency has passed since it was asked, whichever is later.
        /// </summary>
        /// <exception cref="InvalidOperationException">No request is held.</exception>
        public void Release()
        {
            HeldRequest entry;
            lock (gate)
            {
                if (held.Count == 0)
                {
                    throw new InvalidOperationException("No request is held.");
                }

                entry = held[0];
                held.RemoveAt(0);
            }

            entry.Cancellation.Dispose();
            Start(entry.Source, entry.Reply, entry.Clock, entry.Token);
        }

        /// <summary>
        /// Answers with the scripted reply for this request, after its latency.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="req"/> is null.</exception>
        /// <exception cref="OperationCanceledException"><paramref name="ct"/> is already cancelled.</exception>
        public Awaitable<ProviderResult> RequestAsync(DecisionRequest req, CancellationToken ct)
        {
            if (req == null)
            {
                throw new ArgumentNullException(nameof(req));
            }

            ct.ThrowIfCancellationRequested();

            var clock = Stopwatch.StartNew();
            var request = new ScriptedRequest(req, RequestCount);
            RequestCount++;
            LastRequest = request;

            var reply = Choose(request) ?? Unscripted(request);
            var source = new AwaitableCompletionSource<ProviderResult>();

            if (Hold)
            {
                var entry = new HeldRequest { Source = source, Reply = reply, Clock = clock, Token = ct };
                lock (gate)
                {
                    held.Add(entry);
                }

                entry.Cancellation = ct.Register(() =>
                {
                    lock (gate)
                    {
                        if (!held.Remove(entry))
                        {
                            return;
                        }
                    }

                    source.TrySetCanceled();
                });
                return source.Awaitable;
            }

            Start(source, reply, clock, ct);
            return source.Awaitable;
        }

        private ScriptedReply Choose(ScriptedRequest request)
        {
            if (request.IsRefusalLine)
            {
                return Callback?.Invoke(request) ?? RefusalLine;
            }

            // A stage that needs the agent or the stimulus is skipped when the request has none: an
            // agent with no name or id, an empty stimulus, or a request built by hand. Its key would
            // otherwise be the wildcard, and Otherwise would answer ahead of the callback.
            bool stimulus = !string.IsNullOrEmpty(request.Stimulus);
            return (stimulus ? TakeForAgent(request, request.Stimulus) : null)
                   ?? (stimulus ? Take(new Key(null, request.Stimulus)) : null)
                   ?? TakeForAgent(request, null)
                   ?? Callback?.Invoke(request)
                   ?? Take(new Key(null, null));
        }

        // The asking agent's next reply for this stimulus, or for any when it is null: scripted for
        // its id first, then for its name (#190, Living Vale #21). Null when it has neither, or
        // nothing is scripted for either. An id equal to the name is one key, taken once.
        private ScriptedReply TakeForAgent(ScriptedRequest request, string stimulus)
        {
            string id = request.AgentId;
            string name = string.IsNullOrEmpty(request.Agent) ? null : request.Agent;
            return (id != null ? Take(new Key(id, stimulus)) : null)
                   ?? (name != null && !string.Equals(name, id, StringComparison.Ordinal) ? Take(new Key(name, stimulus)) : null);
        }

        // The key's next reply, or null when nothing is scripted for it.
        private ScriptedReply Take(Key key)
        {
            return scripts.TryGetValue(key, out var script) ? script.Next() : null;
        }

        private ScriptedReply Unscripted(ScriptedRequest request)
        {
            string what = request.IsRefusalLine ? "refusal line" : "reply";
            string id = request.AgentId != null ? $" (id '{request.AgentId}')" : "";
            string who = request.Agent == null && request.AgentId == null && request.Stimulus == null
                ? "a request PromptBuilder did not build"
                : $"agent '{request.Agent}'{id} and stimulus '{request.Stimulus}'";
            string how = request.IsRefusalLine
                ? "Set RefusalLine, or answer it from a Callback."
                : "Script one with When(agent, stimulus, …), set a Callback, or set Otherwise.";
            return ScriptedReply.Fail(ProviderErrorKind.Rejected, $"The scripted provider has no {what} for {who}. {how}");
        }

        private void Start(AwaitableCompletionSource<ProviderResult> source, ScriptedReply reply, Stopwatch clock, CancellationToken ct)
        {
            if (reply.IsNever)
            {
                ct.Register(() => source.TrySetCanceled());
                return;
            }

            if (reply.LatencySeconds > 0f)
            {
                // Not awaited: it completes the source itself, and catches everything it can throw.
                _ = AnswerLater(source, reply, clock, ct);
                return;
            }

            Answer(source, reply, clock);
        }

        // Awaiting the delay comes back through the caller's context, which for an agent is Unity's
        // main thread, so the reply arrives where the decision was asked.
        private async Task AnswerLater(AwaitableCompletionSource<ProviderResult> source, ScriptedReply reply, Stopwatch clock, CancellationToken ct)
        {
            try
            {
                var wait = TimeSpan.FromSeconds(reply.LatencySeconds) - clock.Elapsed;
                if (wait > TimeSpan.Zero)
                {
                    await Task.Delay(wait, ct);
                }
            }
            catch (OperationCanceledException)
            {
                source.TrySetCanceled();
                return;
            }
            catch (Exception e)
            {
                // Nothing awaits this task, so a failure here must reach the request, not vanish.
                source.TrySetException(e);
                return;
            }

            if (ct.IsCancellationRequested)
            {
                source.TrySetCanceled();
                return;
            }

            Answer(source, reply, clock);
        }

        private void Answer(AwaitableCompletionSource<ProviderResult> source, ScriptedReply reply, Stopwatch clock)
        {
            if (reply.Failure != null)
            {
                var kind = reply.Failure.Value;
                source.TrySetException(new ProviderException(kind, Name,
                    reply.FailureMessage ?? $"The scripted reply fails as {kind}."));
                return;
            }

            source.TrySetResult(new ProviderResult
            {
                Text = reply.Text,
                LatencySeconds = (float)clock.Elapsed.TotalSeconds,
                TokenProbabilities = reply.TokenProbabilities,
                Model = ProviderName,
            });
        }

        // An agent's name and a stimulus, either null for any. Empty counts as null, as a blank
        // field does in ScriptedProviderConfig.
        private readonly struct Key : IEquatable<Key>
        {
            private readonly string agent;
            private readonly string stimulus;

            public Key(string agent, string stimulus)
            {
                this.agent = string.IsNullOrEmpty(agent) ? null : agent;
                this.stimulus = string.IsNullOrEmpty(stimulus) ? null : stimulus;
            }

            public bool Equals(Key other)
            {
                return string.Equals(agent, other.agent, StringComparison.Ordinal)
                       && string.Equals(stimulus, other.stimulus, StringComparison.Ordinal);
            }

            public override bool Equals(object obj)
            {
                return obj is Key other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return ((agent != null ? StringComparer.Ordinal.GetHashCode(agent) : 0) * 397)
                           ^ (stimulus != null ? StringComparer.Ordinal.GetHashCode(stimulus) : 0);
                }
            }
        }

        private sealed class Script
        {
            private readonly ScriptedReply[] replies;
            private int next;

            public Script(ScriptedReply[] replies)
            {
                this.replies = replies;
            }

            public ScriptedReply Next()
            {
                var reply = replies[Math.Min(next, replies.Length - 1)];
                if (next < replies.Length)
                {
                    next++;
                }

                return reply;
            }
        }

        private sealed class HeldRequest
        {
            public AwaitableCompletionSource<ProviderResult> Source;
            public ScriptedReply Reply;
            public Stopwatch Clock;
            public CancellationToken Token;
            public CancellationTokenRegistration Cancellation;
        }
    }
}

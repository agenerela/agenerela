using System;
using System.Collections.Generic;
using System.Threading;
using Agenerela.Providers;
using UnityEngine;

namespace Agenerela.Tests
{
    /// <summary>
    /// A provider with no model behind it: it answers with a scripted reply, and records every
    /// <see cref="DecisionRequest"/> it is sent, so a test can assert on what actually reached the
    /// model. What lets Phase 1 test the whole pipeline with nothing installed (#17).
    /// </summary>
    /// <remarks>
    /// By default it answers at once, so <c>await agent.DecideAsync(...)</c> completes within the
    /// call. <see cref="Hold"/> leaves the reply pending, the way a real backend's does, until
    /// <see cref="Release"/> or cancellation. Several can be held at once, released oldest first,
    /// which is how the queue tests (#89) let one request through at a time.
    /// </remarks>
    public sealed class FakeProvider : ILLMProvider
    {
        private readonly List<DecisionRequest> requests = new List<DecisionRequest>();
        private readonly List<int> requestThreads = new List<int>();

        // The requests Hold kept pending, oldest first. One leaves the list when it is released or
        // cancelled, so neither can complete it twice once its pooled Awaitable has been handed back.
        private readonly List<HeldRequest> held = new List<HeldRequest>();

        /// <param name="reply">The text every request is answered with, until <see cref="Reply"/> changes it.</param>
        /// <param name="name">What <see cref="Name"/> reports, which telemetry records.</param>
        public FakeProvider(string reply = "", string name = "fake")
        {
            Reply = reply;
            Name = name;
        }

        public string Name { get; }

        public ProviderCapabilities Capabilities { get; set; } = new ProviderCapabilities
        {
            SupportsConstrainedDecoding = true,
            Dialect = SchemaDialect.JsonSchema,
        };

        /// <summary>The text of the next reply, sent as received: it may be anything, or null.</summary>
        public string Reply;

        /// <summary>Token counts the next reply reports. 0, as a backend that reports none.</summary>
        public int PromptTokens;
        public int CompletionTokens;

        /// <summary>
        /// The token probabilities the next reply carries, as <see cref="ProviderResult.TokenProbabilities"/>.
        /// Null, as a provider that reports none. <see cref="Tokens"/> builds them.
        /// </summary>
        public IReadOnlyList<TokenProbability> TokenProbabilities;

        /// <summary>
        /// Scripts the next reply as these tokens: <see cref="Reply"/> becomes their texts joined, and
        /// <see cref="TokenProbabilities"/> the tokens themselves.
        /// </summary>
        public void ReplyWith(IReadOnlyList<TokenProbability> tokens)
        {
            Reply = Tokens.Join(tokens);
            TokenProbabilities = tokens;
        }

        /// <summary>
        /// When set, the request fails with this instead of replying: through the returned
        /// <c>Awaitable</c>, as a transport error arrives, or thrown by <c>RequestAsync</c> itself
        /// when <see cref="FailsImmediately"/> is set.
        /// </summary>
        public Exception Failure;
        public bool FailsImmediately;

        /// <summary>
        /// When true, the request completes with no <c>ProviderResult</c> at all, which the
        /// provider contract never allows: what a broken provider would do.
        /// </summary>
        public bool ReturnsNoResult;

        /// <summary>
        /// What a held request fails with when it is cancelled. Null fails it with an
        /// <c>OperationCanceledException</c>, as the provider contract asks; set it to imitate a
        /// backend that reports an abort as some other error.
        /// </summary>
        public Exception CancelledWith;

        /// <summary>How long each request blocks before replying, so a test can check the latency.</summary>
        public int DelayMilliseconds;

        /// <summary>When true, the reply stays pending until <see cref="Release"/> or cancellation.</summary>
        public bool Hold;

        /// <summary>How many requests <see cref="Hold"/> is keeping pending now.</summary>
        public int HeldCount => held.Count;

        /// <summary>
        /// Makes every request fail as a real provider says why: with a <see cref="ProviderException"/>
        /// of <paramref name="kind"/>, carrying this provider's <see cref="Name"/>. Sets
        /// <see cref="Failure"/>, so <see cref="FailsImmediately"/> still applies. Returns the exception.
        /// </summary>
        public ProviderException FailWith(ProviderErrorKind kind, string message = null)
        {
            var failure = new ProviderException(kind, Name, message ?? $"The fake provider failed: {kind}.");
            Failure = failure;
            return failure;
        }

        /// <summary>Every request received, oldest first.</summary>
        public IReadOnlyList<DecisionRequest> Requests => requests;

        /// <summary>The last request received, or null if none was sent.</summary>
        public DecisionRequest LastRequest => requests.Count > 0 ? requests[requests.Count - 1] : null;

        /// <summary>The managed thread each request was sent from, in the order of <see cref="Requests"/>.</summary>
        public IReadOnlyList<int> RequestThreads => requestThreads;

        /// <summary>The cancellation token the last request was sent with.</summary>
        public CancellationToken LastToken { get; private set; }

        public Awaitable<ProviderResult> RequestAsync(DecisionRequest req, CancellationToken ct)
        {
            requests.Add(req);
            requestThreads.Add(Thread.CurrentThread.ManagedThreadId);
            LastToken = ct;

            if (Failure != null && FailsImmediately)
            {
                throw Failure;
            }

            if (DelayMilliseconds > 0)
            {
                Thread.Sleep(DelayMilliseconds);
            }

            var source = new AwaitableCompletionSource<ProviderResult>();

            if (Hold)
            {
                var entry = new HeldRequest { Source = source };
                held.Add(entry);
                entry.Cancellation = ct.Register(() =>
                {
                    if (!held.Remove(entry))
                    {
                        return;
                    }

                    if (CancelledWith != null)
                    {
                        source.TrySetException(CancelledWith);
                    }
                    else
                    {
                        source.TrySetCanceled();
                    }
                });
                return source.Awaitable;
            }

            Answer(source);
            return source.Awaitable;
        }

        /// <summary>
        /// Answers the oldest request <see cref="Hold"/> kept pending, with the reply as scripted now.
        /// </summary>
        public void Release()
        {
            if (held.Count == 0)
            {
                throw new InvalidOperationException("No request is pending.");
            }

            var entry = held[0];
            held.RemoveAt(0);
            entry.Cancellation.Dispose();
            Answer(entry.Source);
        }

        private void Answer(AwaitableCompletionSource<ProviderResult> source)
        {
            if (Failure != null)
            {
                source.TrySetException(Failure);
                return;
            }

            if (ReturnsNoResult)
            {
                source.TrySetResult(null);
                return;
            }

            var result = new ProviderResult
            {
                Text = Reply,
                PromptTokens = PromptTokens,
                CompletionTokens = CompletionTokens,
                TokenProbabilities = TokenProbabilities,
            };
            source.TrySetResult(result);
        }

        private sealed class HeldRequest
        {
            public AwaitableCompletionSource<ProviderResult> Source;
            public CancellationTokenRegistration Cancellation;
        }
    }
}

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
    /// <see cref="Release"/> or cancellation.
    /// </remarks>
    public sealed class FakeProvider : ILLMProvider
    {
        private readonly List<DecisionRequest> requests = new List<DecisionRequest>();
        private AwaitableCompletionSource<ProviderResult> pending;

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
        /// When set, the request fails with this instead of replying: through the returned
        /// <c>Awaitable</c>, as a transport error arrives, or thrown by <c>RequestAsync</c> itself
        /// when <see cref="FailsImmediately"/> is set.
        /// </summary>
        public Exception Failure;
        public bool FailsImmediately;

        /// <summary>How long each request blocks before replying, so a test can check the latency.</summary>
        public int DelayMilliseconds;

        /// <summary>When true, the reply stays pending until <see cref="Release"/> or cancellation.</summary>
        public bool Hold;

        /// <summary>Every request received, oldest first.</summary>
        public IReadOnlyList<DecisionRequest> Requests => requests;

        /// <summary>The last request received, or null if none was sent.</summary>
        public DecisionRequest LastRequest => requests.Count > 0 ? requests[requests.Count - 1] : null;

        /// <summary>The cancellation token the last request was sent with.</summary>
        public CancellationToken LastToken { get; private set; }

        public Awaitable<ProviderResult> RequestAsync(DecisionRequest req, CancellationToken ct)
        {
            requests.Add(req);
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
                pending = source;
                ct.Register(() => source.TrySetCanceled());
                return source.Awaitable;
            }

            Answer(source);
            return source.Awaitable;
        }

        /// <summary>Answers the request <see cref="Hold"/> kept pending, with the reply as scripted now.</summary>
        public void Release()
        {
            if (pending == null)
            {
                throw new InvalidOperationException("No request is pending.");
            }

            var source = pending;
            pending = null;
            Answer(source);
        }

        private void Answer(AwaitableCompletionSource<ProviderResult> source)
        {
            if (Failure != null)
            {
                source.TrySetException(Failure);
                return;
            }

            var result = new ProviderResult
            {
                Text = Reply,
                PromptTokens = PromptTokens,
                CompletionTokens = CompletionTokens,
            };
            source.TrySetResult(result);
        }
    }
}

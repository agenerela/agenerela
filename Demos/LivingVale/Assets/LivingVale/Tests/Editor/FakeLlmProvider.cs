using System;
using System.Collections.Generic;
using System.Threading;
using Agenerela.Providers;
using UnityEngine;

namespace LivingVale.Tests
{
    /// <summary>
    /// A model the test scripts: it answers every request with <see cref="Reply"/>, fails with
    /// <see cref="Failure"/>, or, with <see cref="Hold"/>, waits until the request is cancelled.
    /// It records each request, so a test can read what reached the model. As a model server
    /// (<see cref="IModelLifecycle"/>) it counts warm-ups and unloads, and can hold or fail them.
    /// </summary>
    internal sealed class FakeLlmProvider : ILLMProvider, IModelLifecycle
    {
        public readonly List<DecisionRequest> Requests = new List<DecisionRequest>();

        public string Reply = Json("none", "no_target", "Hm.");
        public Exception Failure;
        public bool Hold;
        public CancellationToken LastToken;

        public string Name => "fake";

        public ProviderCapabilities Capabilities { get; } = new ProviderCapabilities
        {
            SupportsConstrainedDecoding = true,
            Dialect = SchemaDialect.JsonSchema,
            ReportsTokenProbabilities = true,
        };

        public DecisionRequest LastRequest => Requests.Count > 0 ? Requests[Requests.Count - 1] : null;

        public static string Json(string action, string target, string statement) =>
            $"{{\"action\":\"{action}\",\"target\":\"{target}\",\"statement\":\"{statement}\"}}";

        public Awaitable<ProviderResult> RequestAsync(DecisionRequest req, CancellationToken ct)
        {
            Requests.Add(req);
            LastToken = ct;
            var source = new AwaitableCompletionSource<ProviderResult>();

            if (Hold)
            {
                ct.Register(() => source.TrySetCanceled());
                return source.Awaitable;
            }

            if (Failure != null)
            {
                source.TrySetException(Failure);
            }
            else
            {
                source.TrySetResult(new ProviderResult { Text = Reply });
            }

            return source.Awaitable;
        }

        public IReadOnlyList<ModelInfo> Installed = Array.Empty<ModelInfo>();
        public int WarmUps;
        public bool HoldWarmUp;
        public Exception WarmUpFailure;
        public CancellationToken LastWarmUpToken;
        public int Unloads;

        /// <summary>What an unload answers; null answers true at once.</summary>
        public Func<Awaitable<bool>> OnUnload;

        public Awaitable<IReadOnlyList<ModelInfo>> ListModelsAsync(CancellationToken ct)
        {
            var source = new AwaitableCompletionSource<IReadOnlyList<ModelInfo>>();
            source.TrySetResult(Installed);
            return source.Awaitable;
        }

        public Awaitable WarmUpAsync(CancellationToken ct)
        {
            WarmUps++;
            LastWarmUpToken = ct;
            var source = new AwaitableCompletionSource();
            if (HoldWarmUp)
            {
                ct.Register(() => source.TrySetCanceled());
            }
            else if (WarmUpFailure != null)
            {
                source.TrySetException(WarmUpFailure);
            }
            else
            {
                source.TrySetResult();
            }

            return source.Awaitable;
        }

        public Awaitable<bool> UnloadAsync(CancellationToken ct)
        {
            Unloads++;
            if (OnUnload != null)
            {
                return OnUnload();
            }

            var source = new AwaitableCompletionSource<bool>();
            source.TrySetResult(true);
            return source.Awaitable;
        }
    }
}

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
    /// It records each request, so a test can read what reached the model.
    /// </summary>
    internal sealed class FakeLlmProvider : ILLMProvider
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
    }
}

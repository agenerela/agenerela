using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Agenerela.Providers;
using NUnit.Framework;
using UnityEngine;

namespace LivingVale.Tests
{
    /// <summary>
    /// A model the test answers by hand, request by request. With <see cref="IgnoresCancellation"/>
    /// it behaves like a backend that finishes anyway after being cancelled, the case that makes
    /// cancellation alone insufficient (design §12).
    /// </summary>
    internal sealed class HandProvider : ILLMProvider
    {
        public readonly List<DecisionRequest> Requests = new List<DecisionRequest>();
        private readonly List<AwaitableCompletionSource<ProviderResult>> pending = new List<AwaitableCompletionSource<ProviderResult>>();

        public bool IgnoresCancellation;

        public string Name => "hand";

        public ProviderCapabilities Capabilities { get; } = new ProviderCapabilities
        {
            SupportsConstrainedDecoding = true,
            Dialect = SchemaDialect.JsonSchema,
        };

        public Awaitable<ProviderResult> RequestAsync(DecisionRequest req, CancellationToken ct)
        {
            var source = new AwaitableCompletionSource<ProviderResult>();
            Requests.Add(req);
            pending.Add(source);
            if (!IgnoresCancellation)
            {
                ct.Register(() => source.TrySetCanceled());
            }

            return source.Awaitable;
        }

        /// <summary>Answers request <paramref name="index"/> with <c>none</c> and this statement.</summary>
        public void Say(int index, string statement) => Act(index, "none", statement);

        public void Act(int index, string action, string statement) =>
            Answer(index, FakeLlmProvider.Json(action, "no_target", statement));

        /// <summary>Answers with this raw text, which may not be a decision at all.</summary>
        public void Answer(int index, string text) => pending[index].TrySetResult(new ProviderResult { Text = text });

        public void Fail(int index, Exception e) => pending[index].TrySetException(e);
    }

    internal static class Run
    {
        /// <summary>
        /// Awaits a turn and fails the test if it ended cancelled: the Test Framework counts a
        /// cancelled <see cref="Task"/> as passed (AGENTS.md, Lessons).
        /// </summary>
        public static async Task NotCancelled(Task turn)
        {
            try
            {
                await turn;
            }
            catch (OperationCanceledException e)
            {
                Assert.Fail($"The turn ended cancelled: {e.Message}");
            }
        }
    }
}

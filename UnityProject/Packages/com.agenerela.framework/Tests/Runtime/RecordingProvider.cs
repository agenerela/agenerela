using System.Threading;
using Agenerela.Providers;
using UnityEngine;

namespace Agenerela.Tests
{
    /// <summary>
    /// Passes every request to a real provider unchanged and keeps the last one, so a live test
    /// can check the decision against the schema it was offered and the history it was sent.
    /// </summary>
    internal sealed class RecordingProvider : ILLMProvider
    {
        private readonly ILLMProvider inner;

        public RecordingProvider(ILLMProvider inner)
        {
            this.inner = inner;
        }

        public DecisionRequest LastRequest { get; private set; }

        public string Name => inner.Name;

        public ProviderCapabilities Capabilities => inner.Capabilities;

        public Awaitable<ProviderResult> RequestAsync(DecisionRequest req, CancellationToken ct)
        {
            LastRequest = req;
            return inner.RequestAsync(req, ct);
        }
    }
}

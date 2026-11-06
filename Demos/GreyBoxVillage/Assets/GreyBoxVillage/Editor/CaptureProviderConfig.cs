using System;
using System.Threading;
using Agenerela;
using Agenerela.Providers;
using UnityEngine;

namespace Demos.GreyBoxVillage.Editor
{
    /// <summary>
    /// A provider config for the capture tool: every agent on it shares one <see cref="CaptureProvider"/>,
    /// which answers with the reply the tool scripted, or fails as a stopped Ollama does. The captures
    /// never call a real model.
    /// </summary>
    internal sealed class CaptureProviderConfig : ProviderConfig
    {
        public readonly CaptureProvider Scripted = new CaptureProvider();

        protected override ILLMProvider CreateProvider() => Scripted;
    }

    /// <summary>
    /// Answers every request with <see cref="Reply"/>, or with what <see cref="Answer"/> gives for it
    /// when that is set (the presenter script); while <see cref="Fail"/> is set, fails with it instead.
    /// </summary>
    internal sealed class CaptureProvider : ILLMProvider
    {
        public string Reply = Decision("none", "no_target", "Good day.");
        public Func<DecisionRequest, string> Answer;
        public ProviderErrorKind? Fail;

        public string Name => "scripted";
        public ProviderCapabilities Capabilities { get; } = new ProviderCapabilities { SupportsConstrainedDecoding = true };

        public Awaitable<ProviderResult> RequestAsync(DecisionRequest request, CancellationToken ct)
        {
            var source = new AwaitableCompletionSource<ProviderResult>();
            if (Fail != null) source.SetException(new ProviderException(Fail.Value, Name, "The capture tool's provider is set to fail."));
            else source.SetResult(new ProviderResult { Text = Answer != null ? Answer(request) : Reply, Model = "capture" });
            return source.Awaitable;
        }

        public static string Decision(string action, string target, string statement) =>
            "{\"action\":\"" + action + "\",\"target\":\"" + target + "\",\"statement\":\"" + statement + "\"}";
    }
}

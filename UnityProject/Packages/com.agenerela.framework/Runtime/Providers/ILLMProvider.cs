using System.Threading;
using UnityEngine;

namespace Agenerela.Providers
{
    /// <summary>
    /// The one thing every backend must be able to do: take an assembled
    /// <see cref="DecisionRequest"/>, send it to a model, and return the raw reply.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Three very different backends have to fit behind this: a local Ollama server
    /// (development), a cloud API such as Gemini or OpenAI (Phase 6), and a model running
    /// inside the built game (Phase 6b). The interface is written now, knowing about all
    /// three, so that adding the second cloud vendor is an implementation task rather than
    /// a framework edit. <c>Documentation~/providers.md</c> works through how each answers
    /// it, and what a second vendor would need.
    /// </para>
    /// <para>
    /// <b>A provider is untrusted by design.</b> It reports what it can do through
    /// <see cref="ProviderCapabilities"/>, but the validation pipeline (Phase 3) runs over
    /// every decision regardless of what was claimed. A provider with no constrained
    /// decoding at all is still contained; it is just less likely to produce parseable
    /// output on the first try. Never let a capability flag switch a guard off.
    /// </para>
    /// <para>
    /// The provider's job stops at text. It does not parse the reply, does not know what an
    /// action is, and does not decide whether the answer was legal — <c>Agent</c> (#17)
    /// parses <see cref="ProviderResult.Text"/> and builds <c>DecisionTelemetry</c> (#3)
    /// from the rest.
    /// </para>
    /// </remarks>
    public interface ILLMProvider
    {
        /// <summary>
        /// Short stable identifier for this backend, recorded in decision telemetry so a
        /// captured benchmark run says which provider produced it — <c>"ollama"</c>,
        /// <c>"gemini"</c>, <c>"llmunity"</c>. It names the provider, not the model: the
        /// model is provider configuration, and two providers may run the same weights.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// What this backend can and cannot do. Read by the schema serializers, which
        /// differ per provider, and by the scheduler, which has to respect
        /// <see cref="ProviderCapabilities.RateLimit"/>. Expected to be constant for the
        /// lifetime of the instance, so callers may cache it.
        /// </summary>
        ProviderCapabilities Capabilities { get; }

        /// <summary>
        /// Sends one assembled request and returns the model's raw reply.
        /// </summary>
        /// <param name="req">
        /// The envelope <c>PromptBuilder</c> (#16) fills. <see cref="DecisionRequest"/>
        /// documents which field holds what, and the order they are composed in.
        /// </param>
        /// <param name="ct">
        /// Cancellation. A decision must be abandonable — the agent it was asked for can
        /// die, the scene can unload, or the player can walk away mid-generation, and none
        /// of those should leave a request running. Implementations pass this down to the
        /// transport (an HTTP request is aborted, an in-process generation is stopped) and
        /// let <see cref="System.OperationCanceledException"/> propagate rather than
        /// returning a partial or empty <see cref="ProviderResult"/>: a cancelled decision
        /// has no result, and silently returning one would be scored as a wrong answer.
        /// </param>
        /// <returns>The raw reply plus whatever telemetry the backend reported.</returns>
        /// <remarks>
        /// Transport and protocol failures — server down, model not pulled, HTTP 429,
        /// malformed envelope — are thrown, not returned. <see cref="ProviderResult"/>
        /// describes a reply that arrived. Phase 2 introduces the typed provider exception
        /// so that a missing model surfaces as a diagnosable error rather than a leaked
        /// <c>HttpRequestException</c>; until then, throwing is still the contract.
        /// API keys must never appear in an exception message (hard rule 1).
        /// </remarks>
        Awaitable<ProviderResult> RequestAsync(DecisionRequest req, CancellationToken ct);
    }
}

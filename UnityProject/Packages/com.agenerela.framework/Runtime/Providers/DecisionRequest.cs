using System.Collections.Generic;

namespace Agenerela.Providers
{
    /// <summary>
    /// Everything one decision needs, assembled and ready to send. Built by
    /// <c>PromptBuilder</c> (#16); consumed by every <see cref="ILLMProvider"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>How a provider composes these fields.</b> Both #16 and every provider depend on
    /// this being unambiguous, so it is stated once, here, and nowhere else. The system
    /// side of the request is the non-empty ones of, in this order:
    /// </para>
    /// <list type="number">
    ///   <item><description><see cref="SystemPrompt"/> — who the agent is, and the fixed instruction lines.</description></item>
    ///   <item><description><see cref="FewShotBlock"/> — the worked examples.</description></item>
    ///   <item><description><see cref="Observations"/> — what the agent knows right now.</description></item>
    /// </list>
    /// <para>
    /// Then <see cref="History"/>, then <see cref="Stimulus"/> as the final user turn —
    /// never appended to the system text, because the model has to see it as the thing it
    /// is answering. A provider whose API has no system role concatenates the system side
    /// into the first user turn, in that same order.
    /// </para>
    /// <para>
    /// The blocks arrive pre-rendered as text, including their own headings. A provider
    /// joins them with a blank line and sends them; it never rewrites, reorders, truncates
    /// or re-emphasises them. Prompt wording is measured, and two of the three regressions
    /// caught by the prototype's A/B harness were prompt wording changes — a provider
    /// quietly "improving" a block would be invisible and would invalidate the numbers.
    /// </para>
    /// <para>
    /// <see cref="Schema"/> is the exception: it arrives <b>unserialised</b>, because each
    /// provider serialises it in its own dialect.
    /// </para>
    /// </remarks>
    public sealed class DecisionRequest
    {
        /// <summary>
        /// The system prompt: agent name, role, personality and goals, plus the two fixed
        /// instruction lines. Derived from <c>AgentIdentity</c> — nothing else goes in it.
        /// </summary>
        public string SystemPrompt;

        /// <summary>
        /// The few-shot examples as a rendered block, heading included. Assembled per
        /// request by <c>FewShotBuilder</c> (#10) from the actions actually available, so
        /// it changes as agent state changes. Null or empty when there are no examples.
        /// </summary>
        /// <remarks>
        /// Kept a separate field rather than folded into <see cref="SystemPrompt"/> because
        /// few-shot was the largest single accuracy lever measured (+35 points), and an
        /// A/B run needs to be able to swap or drop this block alone, with the rest of the
        /// prompt byte-identical.
        /// </remarks>
        public string FewShotBlock;

        /// <summary>
        /// What the agent knows right now, as a rendered block with its own heading — the
        /// developer's observations, verbatim, one per line. Null or empty when the agent
        /// has none, in which case the block is omitted entirely rather than sent as an
        /// empty heading.
        /// </summary>
        /// <remarks>
        /// Its own field, and not part of <see cref="SystemPrompt"/>, because it belongs
        /// *after* <see cref="FewShotBlock"/>: the examples teach the answer format, and
        /// what the agent currently knows should be the last thing it reads before the
        /// question. It is also the only system-side block that changes every call, which
        /// matters once prompt caching is worth having.
        /// </remarks>
        public string Observations;

        /// <summary>
        /// Previous turns, oldest first. Empty in Phase 1; Phase 2's
        /// <c>IMemoryStrategy</c> fills it, defaulting to a rolling window of the last few
        /// turns. Never null — an empty list, so providers need no null check.
        /// </summary>
        /// <remarks>
        /// Deliberately a flat list of strings so that memory can be added in Phase 2
        /// without changing this type or #16's signature. How a turn encodes its speaker is
        /// the one thing here left open: it is decided in Phase 2 alongside the memory
        /// strategy, and until then the list is always empty, so no provider can be wrong
        /// about it yet. Do not invent an encoding and read it back.
        /// </remarks>
        public IReadOnlyList<string> History = new List<string>();

        /// <summary>
        /// The thing being responded to, already labelled — <c>Player: "Go to the tower."</c>,
        /// <c>Report: "Grain stores fell 12% this winter."</c> Sent as the final user turn.
        /// </summary>
        /// <remarks>
        /// The label is a free string the developer sets per call, and the framework never
        /// classifies what kind of stimulus this is (DR-008). A provider must not read the
        /// label, branch on it, or assume a speaking character: an agent may be a country
        /// reading a report, and nothing here is a line of dialogue.
        /// </remarks>
        public string Stimulus;

        // TODO(#8): the provider-neutral DecisionSchema — ordered fields, each with a name,
        // description and optional enum of allowed values, built per request from the
        // agent's currently available actions and registered targets.
        //
        //     public DecisionSchema Schema;
        //
        // It is the last field to land because #8 owns the type and it does not exist yet.
        // It stays UNSERIALISED here on purpose: a provider reads Capabilities.Dialect and
        // runs its own serializer over it — JSON Schema for Ollama and cloud APIs (#9),
        // GBNF for the in-process provider (Phase 6b) — which is what keeps a second
        // dialect a second serializer rather than a second schema system. Providers written
        // before #8 lands should treat this field as required, not optional: a request
        // without a schema is not a decision request.
    }
}

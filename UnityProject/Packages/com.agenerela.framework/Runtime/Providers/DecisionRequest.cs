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
    ///   <item><description><see cref="SystemPrompt"/> — who the agent is, and the fixed instruction line.</description></item>
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
        /// The system prompt: agent name, role, personality and goals, plus one fixed
        /// instruction line. Derived from <c>AgentIdentity</c> — nothing else goes in it.
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
        /// What happened before this request, oldest first: the exchanges the agent's memory
        /// recalls, two entries each (#90). Never null — an empty list, so providers need no null
        /// check — and empty when the agent has no memory or nothing to recall.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>The encoding</b>, decided once (build plan §2.9, rule 4). Each exchange is two
        /// entries, in this order:
        /// </para>
        /// <list type="number">
        ///   <item><description>The stimulus exactly as it was sent then, label included:
        ///   <c>Player: "Follow me."</c></description></item>
        ///   <item><description>The decision the agent made, as the JSON object the schema asks
        ///   for, in schema field order, on one line:
        ///   <c>{"action":"follow_player","target":"no_target","statement":"Lead the way."}</c></description></item>
        /// </list>
        /// <para>
        /// So the count is always even, and entry <c>i</c> is a stimulus when <c>i</c> is even. A
        /// provider with chat roles sends each stimulus as a <c>user</c> message and each decision
        /// as an <c>assistant</c> message; one without roles writes them into the user turn in
        /// the same order, before <see cref="Stimulus"/>. Either way they go in verbatim, like
        /// every other field.
        /// </para>
        /// <para>
        /// The decision is the one <c>DecideAsync</c> returned, written by the framework, never
        /// the provider's raw reply (§2.9, rule 1): the model sees its earlier answers in exactly
        /// the form it must give now, and what it reads is what the agent did. A provider must
        /// not parse the entries back or branch on their contents; the label in a stimulus is the
        /// developer's free string (DR-008).
        /// </para>
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

        /// <summary>
        /// The shape the answer must take: ordered fields, each with a name, a description
        /// and, unless it is free text, the only values it may take. Built per request by
        /// <see cref="DecisionSchema.Build"/> (#8) from the agent's currently available
        /// actions and registered targets.
        /// </summary>
        /// <remarks>
        /// Unserialised on purpose: a provider reads <c>Capabilities.Dialect</c> and runs its
        /// own serializer over it — JSON Schema for Ollama and cloud APIs (#9), GBNF for the
        /// in-process provider (Phase 6b) — which is what keeps a second dialect a second
        /// serializer rather than a second schema system. Required, not optional: a request
        /// without a schema is not a decision request.
        /// </remarks>
        public DecisionSchema Schema;
    }
}

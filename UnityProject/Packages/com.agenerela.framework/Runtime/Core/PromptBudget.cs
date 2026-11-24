using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Agenerela.Providers;

namespace Agenerela
{
    /// <summary>
    /// How long a decision's prompt may grow, in tokens, and what the framework may leave out, in the
    /// order the developer gives, to keep it under that (#151). Set one on <see cref="Agent.PromptBudget"/>
    /// for every call, or on <see cref="DecideOptions.PromptBudget"/> for one. None by default: with no
    /// budget every request is what it was before budgets existed, byte for byte.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A game's prompt grows with every feature it adds, and nothing says so: Painted Realm's court
    /// prompt grew by 136 tokens a request when its economy went on (findings, 23 November 2026). The
    /// costs are latency and, on a small model, perhaps worse choices; past the provider's context
    /// window (<see cref="Providers.OllamaSettings.ContextSize"/>) the provider cuts it. A budget makes the
    /// size something a developer sets and the telemetry sees.
    /// </para>
    /// <para>
    /// Each decision records its estimate (<see cref="DecisionTelemetry.PromptTokensEstimated"/>), the
    /// budget, and whether it went over (<see cref="DecisionTelemetry.OverBudget"/>), judged on the
    /// provider's own count once it reports one. The first decision of an agent that goes over logs a
    /// warning; the rest are in the telemetry, the decision feed and the Decision Log.
    /// </para>
    /// <para>
    /// The estimate is made before sending, with no model call: the prompt's characters divided by
    /// <see cref="CharsPerToken"/>. Measured on 6,304 of Painted Realm's requests to <c>qwen3.5:4b</c>,
    /// Ollama's <c>prompt_eval_count</c>, chat template included, came to 3.10 characters a token,
    /// within 3.30 and 2.94 for every one (findings, 24 November 2026). The default, 3, errs long by
    /// 3.6% on average: 53 of those 6,304 came out short, by 2% at most, and none passed 1,600 tokens
    /// while its estimate did not. So a trim fires a little early rather than late. Another model's tokenizer or another
    /// language differs: divide <see cref="DecisionTelemetry.PromptCharacters"/> by
    /// <see cref="DecisionTelemetry.PromptTokens"/> over a few decisions and set your own.
    /// </para>
    /// <para>
    /// With no <see cref="Trims"/> a budget only measures and warns. Each trim the developer lists is
    /// tried in that order, one step at a time, only while the estimate is over the budget, and each
    /// step is recorded: nothing is left out silently, and nothing is left out that the list does not
    /// name. What the model reads changes when a trim fires, so a budget with trims is a choice the
    /// game makes, measured before it becomes a default (hard rule 2). The field order and the schema
    /// never change, and no trim removes an action or makes a target unofferable: the guards and the
    /// schema see the same agent with or without a budget.
    /// </para>
    /// </remarks>
    public sealed class PromptBudget
    {
        /// <summary>The characters a token unless one is given: a little under the 3.10 measured on <c>qwen3.5:4b</c>, so the estimate errs long.</summary>
        public const float DefaultCharsPerToken = 3f;

        /// <summary>
        /// A budget that turns the agent's off for one call: set it on <see cref="DecideOptions.PromptBudget"/>.
        /// Nothing is measured against it, so the call records no budget.
        /// </summary>
        public static readonly PromptBudget None = new PromptBudget();

        private PromptBudget()
        {
            Trims = Array.Empty<PromptTrim>();
            CharsPerToken = DefaultCharsPerToken;
        }

        /// <summary>A budget of <paramref name="tokens"/> tokens.</summary>
        /// <param name="tokens">The most tokens the prompt should run to. Above 0.</param>
        /// <param name="trims">What may be left out to keep under it, in the order to try. None only measures and warns.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="tokens"/> is 0 or less.</exception>
        /// <exception cref="ArgumentException">A trim is listed twice or is not a <see cref="PromptTrim"/>.</exception>
        public PromptBudget(int tokens, params PromptTrim[] trims) : this(tokens, DefaultCharsPerToken, trims)
        {
        }

        /// <summary>A budget of <paramref name="tokens"/> tokens, estimated at <paramref name="charsPerToken"/> characters a token.</summary>
        /// <param name="tokens">The most tokens the prompt should run to. Above 0.</param>
        /// <param name="charsPerToken">Characters a token for the estimate, above 0 (<see cref="CharsPerToken"/>).</param>
        /// <param name="trims">What may be left out to keep under it, in the order to try. None only measures and warns.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="tokens"/> is 0 or less, or <paramref name="charsPerToken"/> is not above 0.</exception>
        /// <exception cref="ArgumentException">A trim is listed twice or is not a <see cref="PromptTrim"/>.</exception>
        public PromptBudget(int tokens, float charsPerToken, params PromptTrim[] trims)
        {
            if (tokens <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(tokens), tokens,
                    "A prompt budget must be above 0 tokens. PromptBudget.None turns an agent's budget off for one call.");
            }

            if (!(charsPerToken > 0f) || float.IsInfinity(charsPerToken))
            {
                throw new ArgumentOutOfRangeException(nameof(charsPerToken), charsPerToken,
                    $"Characters a token must be above 0. {DefaultCharsPerToken} is the default.");
            }

            var list = new List<PromptTrim>();
            foreach (var trim in trims ?? Array.Empty<PromptTrim>())
            {
                if (!Enum.IsDefined(typeof(PromptTrim), trim))
                {
                    throw new ArgumentException($"{(int)trim} is not a PromptTrim.", nameof(trims));
                }

                if (list.Contains(trim))
                {
                    throw new ArgumentException($"PromptTrim.{trim} is listed twice. Each is tried once, in the order given.", nameof(trims));
                }

                list.Add(trim);
            }

            Tokens = tokens;
            CharsPerToken = charsPerToken;
            Trims = new ReadOnlyCollection<PromptTrim>(list);
        }

        /// <summary>The most tokens the prompt should run to; 0 only for <see cref="None"/>.</summary>
        public int Tokens { get; }

        /// <summary>
        /// The characters a token the estimate divides by, <see cref="DefaultCharsPerToken"/> unless
        /// given. Lower it to estimate longer, and so trim sooner.
        /// </summary>
        public float CharsPerToken { get; }

        /// <summary>What may be left out to keep under the budget, in the order tried. Empty: measure and warn only.</summary>
        public IReadOnlyList<PromptTrim> Trims { get; }

        /// <summary>Whether this is a budget at all: false for <see cref="None"/>.</summary>
        public bool IsSet => Tokens > 0;

        /// <summary>
        /// The tokens <paramref name="request"/> is estimated to run to at <paramref name="charsPerToken"/>
        /// characters a token, rounded up, with no model call.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
        public static int Estimate(DecisionRequest request, float charsPerToken = DefaultCharsPerToken)
        {
            if (!(charsPerToken > 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(charsPerToken), charsPerToken, "Characters a token must be above 0.");
            }

            return (int)Math.Ceiling(Characters(request) / charsPerToken);
        }

        /// <summary>
        /// The characters of what <paramref name="request"/> sends as text: the system prompt, the
        /// examples, the observations, each entry of history and the stimulus turn. The schema is not
        /// counted: a model served through Ollama never reads it (findings, 10 October 2026).
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
        public static int Characters(DecisionRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            int count = Length(request.SystemPrompt) + Length(request.FewShotBlock) + Length(request.Observations) + Length(request.Stimulus);
            if (request.History != null)
            {
                foreach (string entry in request.History)
                {
                    count += Length(entry);
                }
            }

            return count;
        }

        private static int Length(string text) => text?.Length ?? 0;
    }

    /// <summary>
    /// What a <see cref="PromptBudget"/> may leave out of a prompt to keep it under the budget, each
    /// only when the developer lists it, in the order listed (#151). Each step is recorded in the
    /// decision's telemetry. Serialized as its name.
    /// </summary>
    public enum PromptTrim
    {
        /// <summary>
        /// The call's <see cref="DecideOptions.OptionalObservations"/>, from the last given, one line a
        /// step. Recorded in <see cref="DecisionTelemetry.ObservationsDropped"/>.
        /// </summary>
        OptionalObservations,

        /// <summary>
        /// The target lines of the prompt's action and target list (<see cref="DecideOptions.ListActionsAndTargets"/>)
        /// that no offered action may take now, by its categories and its per-target checks, in one
        /// step. The schema still offers them; only their lines go, recorded in
        /// <see cref="DecisionTelemetry.TargetsUnlisted"/>. Nothing to do with
        /// <see cref="DecideOptions.LegalTargets"/> on, which lists only those already.
        /// </summary>
        UntakeableTargets,

        /// <summary>
        /// Remembered exchanges, oldest first, one a step. Recorded in
        /// <see cref="DecisionTelemetry.HistoryTurnsDropped"/>; memory itself keeps them.
        /// </summary>
        History,
    }
}

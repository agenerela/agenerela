using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Agenerela.Providers
{
    /// <summary>
    /// One answer a <see cref="ScriptedProvider"/> gives: the reply's text, how long it takes to
    /// arrive, or how it fails. Made with <see cref="Decide"/>, <see cref="Raw"/>,
    /// <see cref="Statement"/>, <see cref="Fail"/> or <see cref="Never"/>, and adjusted with
    /// <see cref="After"/> and <see cref="WithTokens"/>, each of which returns a copy.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Immutable, so one reply can be scripted for several agents, or kept and reused.
    /// </para>
    /// <para>
    /// It carries what a decision log line would: the text exactly as a model sent it, and the
    /// latency it took. Replaying a log (#190, G7) is one <see cref="ScriptedProvider.When"/> per
    /// agent and stimulus, with <see cref="Raw"/> and <see cref="After"/> for each line in order.
    /// It has no model name and no token counts on purpose: whatever it replays, a scripted reply
    /// never says a model produced it.
    /// </para>
    /// </remarks>
    public sealed class ScriptedReply
    {
        private ScriptedReply(string text, float latencySeconds, ProviderErrorKind? failure, string failureMessage,
                              bool never, IReadOnlyList<TokenProbability> tokens)
        {
            Text = text;
            LatencySeconds = latencySeconds;
            Failure = failure;
            FailureMessage = failureMessage;
            IsNever = never;
            TokenProbabilities = tokens;
        }

        /// <summary>
        /// The reply's text, sent as received, as a model's would be: anything, even null or text
        /// that is not JSON. Null when the reply <see cref="Failure">fails</see> or <see cref="IsNever">never arrives</see>.
        /// </summary>
        public string Text { get; }

        /// <summary>
        /// Wall-clock seconds the provider waits before it answers or fails, as a model would take
        /// to reply. 0 answers at once, within <see cref="ILLMProvider.RequestAsync"/>.
        /// </summary>
        public float LatencySeconds { get; }

        /// <summary>
        /// When set, the request fails with a <see cref="ProviderException"/> of this kind instead
        /// of answering, after <see cref="LatencySeconds"/>, as a stopped server or a timeout would.
        /// </summary>
        public ProviderErrorKind? Failure { get; }

        /// <summary>The failed request's message, or null for the provider's own.</summary>
        public string FailureMessage { get; }

        /// <summary>
        /// When true the request never answers: it stays pending until it is cancelled, as a hung
        /// backend's does, so a game's own timeout can be tested.
        /// </summary>
        public bool IsNever { get; }

        /// <summary>
        /// The token probabilities the reply carries, as <see cref="ProviderResult.TokenProbabilities"/>,
        /// so the grounding guard has something to read. Null, as most scripted replies.
        /// </summary>
        public IReadOnlyList<TokenProbability> TokenProbabilities { get; }

        /// <summary>
        /// A decision, as the JSON object the schema asks for, fields in its order:
        /// <c>{"action":…,"target":…,"statement":…}</c>. Nothing checks that the action or target
        /// is one the agent offers: a scripted reply may be illegal on purpose, to show a guard
        /// stopping it.
        /// </summary>
        /// <param name="action">The action's id, such as <c>"move_to"</c>, or <see cref="ActionRegistry.None"/>.</param>
        /// <param name="target">The target's id, or <see cref="TargetRegistry.NoTarget"/>.</param>
        /// <param name="statement">What the agent says. Null is sent as an empty string.</param>
        /// <exception cref="ArgumentNullException"><paramref name="action"/> or <paramref name="target"/> is null.</exception>
        public static ScriptedReply Decide(string action, string target = TargetRegistry.NoTarget, string statement = "")
        {
            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            var reply = new JObject
            {
                [DecisionSchema.ActionFieldName] = action,
                [DecisionSchema.TargetFieldName] = target,
                [DecisionSchema.StatementFieldName] = statement ?? "",
            };
            return Raw(reply.ToString(Formatting.None));
        }

        /// <summary>
        /// The answer to a refusal-line request (<see cref="DecideOptions.AskForRefusalLine"/>), as the
        /// JSON object its schema asks for: <c>{"statement":…}</c>.
        /// </summary>
        /// <param name="statement">What the agent says. Null is sent as an empty string.</param>
        public static ScriptedReply Statement(string statement)
        {
            var reply = new JObject { [DecisionSchema.StatementFieldName] = statement ?? "" };
            return Raw(reply.ToString(Formatting.None));
        }

        /// <summary>
        /// A reply of exactly this text, unchecked: a decision written by hand, a line read back
        /// from a log, or something unreadable, to show how a game copes with a reply that does not
        /// parse.
        /// </summary>
        public static ScriptedReply Raw(string text)
        {
            return new ScriptedReply(text, 0f, null, null, false, null);
        }

        /// <summary>
        /// A request that fails as a provider says why, with a <see cref="ProviderException"/> of
        /// <paramref name="kind"/>: <see cref="ProviderErrorKind.Unreachable"/> for a stopped server,
        /// <see cref="ProviderErrorKind.Timeout"/> for one too slow.
        /// </summary>
        /// <param name="kind">Why it fails.</param>
        /// <param name="message">The exception's message, or null for one saying the failure was scripted.</param>
        public static ScriptedReply Fail(ProviderErrorKind kind, string message = null)
        {
            return new ScriptedReply(null, 0f, kind, message, false, null);
        }

        /// <summary>A request that never answers until it is cancelled. <see cref="After"/> changes nothing for it.</summary>
        public static ScriptedReply Never()
        {
            return new ScriptedReply(null, 0f, null, null, true, null);
        }

        /// <summary>The longest latency <see cref="After"/> takes: a day.</summary>
        public const float MaxLatencySeconds = 86400f;

        /// <summary>This reply, arriving (or failing) <paramref name="seconds"/> after it is asked for.</summary>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="seconds"/> is negative, not a number, or more than <see cref="MaxLatencySeconds"/>.
        /// </exception>
        public ScriptedReply After(float seconds)
        {
            if (!(seconds >= 0f && seconds <= MaxLatencySeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(seconds), seconds,
                    $"A latency is 0 to {MaxLatencySeconds} seconds. For a reply that never arrives, use ScriptedReply.Never().");
            }

            return new ScriptedReply(Text, seconds, Failure, FailureMessage, IsNever, TokenProbabilities);
        }

        /// <summary>
        /// This reply, carrying these token probabilities. Joined in order, their texts should
        /// equal <see cref="Text"/>, as a real provider's do (<see cref="ProviderResult.TokenProbabilities"/>);
        /// nothing checks it, so a test can also show the guard meeting tokens that do not fit.
        /// </summary>
        public ScriptedReply WithTokens(IReadOnlyList<TokenProbability> tokens)
        {
            return new ScriptedReply(Text, LatencySeconds, Failure, FailureMessage, IsNever, tokens);
        }

        /// <summary>What the reply does, for test messages: its text, its failure, or that it never arrives.</summary>
        public override string ToString()
        {
            string what = IsNever ? "never answers" : Failure != null ? $"fails: {Failure}" : Text ?? "null";
            return LatencySeconds > 0f ? $"{what} after {LatencySeconds} s" : what;
        }
    }
}

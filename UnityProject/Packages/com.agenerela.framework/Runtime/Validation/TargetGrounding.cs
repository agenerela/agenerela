using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using Agenerela.Providers;

namespace Agenerela
{
    /// <summary>
    /// How much of the model's probability, at the target of its reply, could still lead to a
    /// target on the list (DR-016). What <see cref="TargetGroundingGuard"/> decides by, as a pure
    /// function that needs no agent and no pipeline.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The schema forces the model to name a listed target, so a model asked to attack a
    /// scarecrow that is not there attacks the training dummy. Its token probabilities, taken
    /// before the schema's mask, still show what it wanted: at "Attack the scarecrow." only 0.05
    /// of its probability at the target stayed on the list, because it wanted to write "sc…".
    /// </para>
    /// <para>
    /// The algorithm is the probe's, number for number: <c>target_info</c> in
    /// <c>tools/benchmarks/target_swap_probe.py</c>, and <c>on_list_share</c> and the
    /// <c>path</c> signal in <c>target_swap_score.py</c>. At each token of the target, it adds up
    /// the probability of the alternatives whose text continues what was written so far into a
    /// listed id, and keeps the lowest of those shares. It never multiplies them, which punished
    /// a right answer whenever the mask forced a split, and it reads past the first token, which
    /// alone passed "spear" as <c>sword</c>. Any change to it needs an A/B run with a control arm
    /// (build plan §2.5, Limits).
    /// </para>
    /// </remarks>
    public static class TargetGrounding
    {
        // The probe's own pattern for the target's value.
        private static readonly Regex TargetValue =
            new Regex("\"" + DecisionSchema.TargetFieldName + "\"\\s*:\\s*\"([^\"]*)\"", RegexOptions.CultureInvariant);

        /// <summary>
        /// Reads the lowest on-list share across the tokens of the reply's target.
        /// </summary>
        /// <param name="replyText"><see cref="ProviderResult.Text"/>, against which the tokens' join is checked.</param>
        /// <param name="tokens"><see cref="ProviderResult.TokenProbabilities"/>, one per generated token, in order.</param>
        /// <param name="targetIds">
        /// The target enum the schema offered, <see cref="TargetRegistry.NoTarget"/> included. Null or
        /// empty when the schema had no target field.
        /// </param>
        /// <returns>
        /// A reading whose <see cref="GroundingReading.Status"/> is <see cref="GroundingStatus.Scored"/>,
        /// or says why there is no share: no target field; no probabilities, or a token of the target
        /// without alternatives; or a target that could not be found in the tokens. Never null, and
        /// never <see cref="GroundingStatus.Off"/>.
        /// </returns>
        public static GroundingReading Read(string replyText, IReadOnlyList<TokenProbability> tokens,
                                            IReadOnlyCollection<string> targetIds)
        {
            if (targetIds == null || targetIds.Count == 0)
            {
                return GroundingReading.Unscored(GroundingStatus.NoTargetField);
            }

            if (tokens == null || tokens.Count == 0)
            {
                return GroundingReading.Unscored(GroundingStatus.NoProbabilities);
            }

            // The tokens joined, and where each one starts in the join.
            var joined = new StringBuilder();
            var starts = new int[tokens.Count];
            for (int i = 0; i < tokens.Count; i++)
            {
                if (tokens[i] == null)
                {
                    return GroundingReading.Unscored(GroundingStatus.Unreadable);
                }

                starts[i] = joined.Length;
                joined.Append(tokens[i].Text ?? "");
            }

            string text = joined.ToString();
            var match = TargetValue.Match(text);
            if (!match.Success)
            {
                return GroundingReading.Unscored(GroundingStatus.Unreadable);
            }

            // A character split across two tokens is missing from both (TokenProbability.Text), and
            // shifts every offset after it. Only the text up to the end of the target's value has to
            // agree: the statement comes after it.
            int end = match.Index + match.Length;
            if (replyText == null || replyText.Length < end || string.CompareOrdinal(replyText, 0, text, 0, end) != 0)
            {
                return GroundingReading.Unscored(GroundingStatus.Unreadable);
            }

            var values = new List<string>(targetIds.Count);
            foreach (string id in targetIds)
            {
                if (id != null)
                {
                    values.Add(id.ToLowerInvariant());
                }
            }

            int a = match.Groups[1].Index;
            int b = a + match.Groups[1].Length;
            string prefix = "";
            int position = 0;
            double lowestShare = double.PositiveInfinity;
            var lowest = GroundingReading.Unscored(GroundingStatus.Unreadable);

            for (int i = 0; i < tokens.Count; i++)
            {
                int s = starts[i];
                int e = s + (tokens[i].Text ?? "").Length;
                if (s >= b || e <= a)
                {
                    continue;
                }

                // The part of this token inside the value. A token of the target is one with
                // something there besides quotes.
                int from = Math.Max(s, a);
                string inside = text.Substring(from, Math.Min(e, b) - from);
                if (inside.Trim('"').Length == 0)
                {
                    continue;
                }

                // Without the alternatives there is nothing to read. Counted as off the list, as the
                // probe would, a provider that reports none would refuse every target.
                if (tokens[i].Alternatives == null || tokens[i].Alternatives.Count == 0)
                {
                    return GroundingReading.Unscored(GroundingStatus.NoProbabilities);
                }

                double share = OnListShare(tokens[i].Alternatives, prefix, values, position == 0, out string wanted);
                if (share < lowestShare)
                {
                    lowestShare = share;
                    lowest = new GroundingReading
                    {
                        Status = GroundingStatus.Scored,
                        Share = (float)share,
                        LowestAt = position,
                        Wanted = wanted,
                    };
                }

                prefix += inside;
                position++;
            }

            return lowest;
        }

        // How much of one position's alternatives continues prefix into a listed id, as the probe's
        // on_list_share reads it: what the five alternatives do not cover counts as off the list,
        // because the 0.8 threshold was calibrated that way. wanted is the likeliest alternative
        // that does not, written onto prefix, or null when there is none with any text.
        private static double OnListShare(IReadOnlyList<TokenAlternative> alternatives, string prefix,
                                          List<string> values, bool first, out string wanted)
        {
            wanted = null;
            double share = 0;
            float likeliestOff = float.NegativeInfinity;

            if (alternatives == null)
            {
                return share;
            }

            foreach (var alternative in alternatives)
            {
                if (alternative == null)
                {
                    continue;
                }

                // At the first token, the alternative may start before the value, with the space and
                // the quote that open it.
                string t = alternative.Text ?? "";
                if (first)
                {
                    t = t.TrimStart().TrimStart('"');
                }

                int quote = t.IndexOf('"');
                bool closes = quote >= 0;
                string candidate = prefix + (closes ? t.Substring(0, quote) : t);
                string lower = candidate.ToLowerInvariant();

                // A token that closes the value must complete a whole listed id; any other must
                // still be on the way to one.
                bool onList = closes ? values.Contains(lower) : lower.Length > 0 && StartsAny(values, lower);
                if (onList)
                {
                    share += Math.Exp(alternative.LogProbability);
                }
                else if (candidate.Length > 0 && alternative.LogProbability > likeliestOff)
                {
                    likeliestOff = alternative.LogProbability;
                    wanted = candidate;
                }
            }

            return share;
        }

        private static bool StartsAny(List<string> values, string start)
        {
            foreach (string value in values)
            {
                if (value.StartsWith(start, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>
    /// What <see cref="TargetGrounding.Read"/> found: the share, or why there is none.
    /// </summary>
    public sealed class GroundingReading
    {
        /// <summary><see cref="GroundingStatus.Scored"/>, or why the reply could not be scored.</summary>
        public GroundingStatus Status;

        /// <summary>
        /// The lowest on-list share across the target's tokens, from 0 to 1, when
        /// <see cref="Status"/> is <see cref="GroundingStatus.Scored"/>; 0 otherwise.
        /// </summary>
        public float Share;

        /// <summary>
        /// Which token of the target the lowest share was at, 0-based, the first when two tie; -1
        /// unless scored.
        /// </summary>
        public int LowestAt = -1;

        /// <summary>
        /// The likeliest off-list text at that token, written onto the part of the target before it:
        /// "sc" for "Attack the scarecrow.", "spear" for "Pick up the spear." beside <c>sword</c>.
        /// Null unless scored, and when no alternative there was off the list, so that the share
        /// fell short only by probability the provider did not report.
        /// </summary>
        public string Wanted;

        internal static GroundingReading Unscored(GroundingStatus status)
        {
            return new GroundingReading { Status = status };
        }
    }

    // Whether the grounding guard read a share, and if not, why. Recorded in
    // DecisionTelemetry.TargetGroundingStatus and serialized as its position, like
    // DecisionOutcome, so a new value goes at the end.
    public enum GroundingStatus
    {
        // A share was read
        Scored,

        // The reply carried no token probabilities: the provider reports none, or sent none this
        // time, or a token of the target came without the alternatives the reading adds up
        NoProbabilities,

        // The schema had no target field, because no target was registered
        NoTargetField,

        // The target could not be found in the tokens, or their join did not match the reply
        Unreadable,

        // DecideOptions.CheckTarget was false for this call, so the guard did not run. Never
        // returned by TargetGrounding.Read
        Off
    }
}

using System.Collections.Generic;

namespace Agenerela.Evaluation
{
    /// <summary>
    /// Agreement between two named annotators on their shared prompts. A category for Cohen's
    /// kappa is the complete action/target answer, not the prompt's category or the settled answer.
    /// </summary>
    public sealed class AnnotatorAgreement
    {
        internal AnnotatorAgreement(string first, string second, IEnumerable<PromptAgreement> prompts)
        {
            First = first;
            Second = second;
            var firstCounts = new Dictionary<LabelAnswer, int>();
            var secondCounts = new Dictionary<LabelAnswer, int>();
            foreach (var prompt in prompts)
            {
                if (!prompt.Answers.TryGetValue(first, out var a) || !prompt.Answers.TryGetValue(second, out var b))
                {
                    continue;
                }

                AnsweredByBoth++;
                if (a.Equals(b))
                {
                    Agreed++;
                }

                firstCounts.TryGetValue(a, out int countA);
                secondCounts.TryGetValue(b, out int countB);
                firstCounts[a] = countA + 1;
                secondCounts[b] = countB + 1;
            }

            if (AnsweredByBoth == 0)
            {
                return;
            }

            // Keep integer counts until the final division. This also detects p_e == 1 exactly,
            // rather than rounding near-constant marginals to an undefined kappa.
            long expected = 0;
            foreach (var answer in firstCounts)
            {
                if (secondCounts.TryGetValue(answer.Key, out int count))
                {
                    expected += (long)answer.Value * count;
                }
            }

            long squared = (long)AnsweredByBoth * AnsweredByBoth;
            ChanceAgreement = (double)expected / squared;
            if (expected != squared)
            {
                CohensKappa = ((double)Agreed * AnsweredByBoth - expected) / (squared - expected);
            }
        }

        public string First { get; }
        public string Second { get; }

        /// <summary>Prompts answered by both people; other people's answers do not affect this pair.</summary>
        public int AnsweredByBoth { get; }

        /// <summary>Shared prompts with exactly the same action and target.</summary>
        public int Agreed { get; }

        /// <summary>Raw percent agreement; null without shared prompts.</summary>
        public double? PercentAgreement => AnsweredByBoth == 0 ? (double?)null : 100.0 * Agreed / AnsweredByBoth;

        /// <summary>Expected agreement p_e from the two people's answer marginals, as a fraction.</summary>
        public double? ChanceAgreement { get; }

        /// <summary>
        /// (p_o - p_e) / (1 - p_e), using complete action/target answers. Null with no overlap or
        /// when p_e is 1 (both people always gave the same single answer), even at 100% raw agreement.
        /// </summary>
        public double? CohensKappa { get; }
    }
}

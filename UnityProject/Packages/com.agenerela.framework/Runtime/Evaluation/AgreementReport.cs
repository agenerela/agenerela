using System;
using System.Collections.Generic;
using System.Linq;

namespace Agenerela.Evaluation
{
    /// <summary>
    /// How often a set's annotators agree: per prompt, overall, and the prompts where they split.
    /// Settling a split stays a person's job (findings.md), done with <see cref="PromptSet.Settle"/>.
    /// </summary>
    /// <remarks>
    /// <para>A prompt's answers come from three places, earliest first:</para>
    /// <list type="number">
    ///   <item><description>
    ///   Its <c>labels</c>, written when it was settled. They are never replaced by a later answer.
    ///   </description></item>
    ///   <item><description>
    ///   Until it is settled, the expected answer counts as its writer's, when <c>labelledBy</c>
    ///   names exactly one person and the target is not an open choice. A writer who accepts either
    ///   of two targets has not given one answer.
    ///   </description></item>
    ///   <item><description>Each annotator's label file.</description></item>
    /// </list>
    /// </remarks>
    public sealed class AgreementReport
    {
        private AgreementReport(List<PromptAgreement> prompts)
        {
            Prompts = prompts;
            Splits = prompts.Where(p => p.IsSplit).ToList();
        }

        /// <summary>Every prompt with at least one answer, in the set's order.</summary>
        public IReadOnlyList<PromptAgreement> Prompts { get; }

        /// <summary>The prompts whose annotators gave different answers.</summary>
        public IReadOnlyList<PromptAgreement> Splits { get; }

        /// <summary>How many prompts two or more people answered.</summary>
        public int AnsweredByTwoOrMore => Prompts.Count(p => p.Answered >= 2);

        /// <summary>How many of those got the same answer from everyone.</summary>
        public int Unanimous => Prompts.Count(p => p.Answered >= 2 && !p.IsSplit);

        /// <summary><see cref="Unanimous"/> over <see cref="AnsweredByTwoOrMore"/>; null when no prompt has two answers.</summary>
        public double? PercentAgreement => AnsweredByTwoOrMore == 0 ? (double?)null : 100.0 * Unanimous / AnsweredByTwoOrMore;

        /// <summary>The agreement on one prompt, or null when nobody has answered it.</summary>
        public PromptAgreement For(string promptId) => Prompts.FirstOrDefault(p => p.PromptId == promptId);

        /// <summary>Compares every answer to every prompt in <paramref name="set"/>.</summary>
        /// <param name="labels">The label files, usually <see cref="PromptSet.LoadLabels"/>.</param>
        public static AgreementReport Compute(PromptSet set, IEnumerable<AnnotatorLabels> labels)
        {
            var files = labels.ToList();
            var prompts = new List<PromptAgreement>();

            foreach (var category in set.Categories)
            {
                foreach (var prompt in category.Prompts)
                {
                    var answers = AnswersTo(prompt, files);
                    if (answers.Count > 0)
                    {
                        prompts.Add(new PromptAgreement(prompt.Id, category.Id, answers));
                    }
                }
            }

            return new AgreementReport(prompts);
        }

        /// <summary>The annotators who have answered <paramref name="prompt"/>, by the rules in the remarks.</summary>
        public static IReadOnlyCollection<string> AnnotatorsOf(EvalPrompt prompt, IEnumerable<AnnotatorLabels> labels)
        {
            return AnswersTo(prompt, labels.ToList()).Keys;
        }

        private static SortedDictionary<string, LabelAnswer> AnswersTo(EvalPrompt prompt, List<AnnotatorLabels> files)
        {
            var answers = new SortedDictionary<string, LabelAnswer>(StringComparer.Ordinal);

            if (prompt.Labels != null)
            {
                foreach (var label in prompt.Labels)
                {
                    if (LabelAnswer.TryParse(label.Value, out var answer))
                    {
                        answers[label.Key] = answer;
                    }
                }
            }
            else if (prompt.LabelledBy != null && prompt.LabelledBy.Count == 1
                     && prompt.ExpectedTarget != null && !prompt.ExpectedTarget.IsOpenChoice
                     && !string.IsNullOrEmpty(prompt.ExpectedAction))
            {
                answers[prompt.LabelledBy[0]] = prompt.ExpectedAnswer;
            }

            foreach (var file in files)
            {
                if (!answers.ContainsKey(file.Annotator)
                    && file.Labels.TryGetValue(prompt.Id, out string text)
                    && LabelAnswer.TryParse(text, out var answer))
                {
                    answers[file.Annotator] = answer;
                }
            }

            return answers;
        }
    }
}

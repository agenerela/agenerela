using System.Collections.Generic;
using System.Linq;

namespace Agenerela.Evaluation
{
    /// <summary>What the annotators of one prompt answered, and whether they agree.</summary>
    public sealed class PromptAgreement
    {
        internal PromptAgreement(string promptId, string categoryId, SortedDictionary<string, LabelAnswer> answers)
        {
            PromptId = promptId;
            CategoryId = categoryId;
            Answers = answers;
            DistinctAnswers = answers.Values.Distinct().ToList();
            Agreeing = answers.Count == 0 ? 0 : answers.Values.GroupBy(a => a).Max(g => g.Count());
        }

        public string PromptId { get; }

        public string CategoryId { get; }

        /// <summary>Each annotator's answer, by annotator name in ordinal order.</summary>
        public IReadOnlyDictionary<string, LabelAnswer> Answers { get; }

        /// <summary>Each different answer once, in the order of the first annotator to give it.</summary>
        public IReadOnlyList<LabelAnswer> DistinctAnswers { get; }

        /// <summary>How many annotators answered.</summary>
        public int Answered => Answers.Count;

        /// <summary>How many gave the most common answer.</summary>
        public int Agreeing { get; }

        /// <summary>True when two or more annotators gave different answers.</summary>
        public bool IsSplit => DistinctAnswers.Count > 1;
    }
}

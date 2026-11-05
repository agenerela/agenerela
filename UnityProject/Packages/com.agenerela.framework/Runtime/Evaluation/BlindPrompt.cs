using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Agenerela.Evaluation
{
    /// <summary>
    /// A prompt as an annotator labelling blind sees it: the label, stimulus, observations and
    /// state, and nothing that gives the answer away. No expected action or target, note,
    /// <c>labelledBy</c> or anyone's label is copied in, so a view given only these cannot show them.
    /// </summary>
    /// <remarks>
    /// findings.md asks each annotator to answer alone. A window can hide the answers where a JSON
    /// file cannot, and this type is how it hides them. The id and category are here only so an
    /// answer can be filed.
    /// </remarks>
    public sealed class BlindPrompt
    {
        private BlindPrompt(EvalPrompt prompt, string categoryId)
        {
            Id = prompt.Id;
            CategoryId = categoryId;
            Label = prompt.Label;
            Stimulus = prompt.Stimulus;
            Observations = (prompt.Observations ?? new List<string>()).ToList();
            PreconditionState = (JObject)(prompt.PreconditionState ?? new JObject()).DeepClone();
        }

        public string Id { get; }

        public string CategoryId { get; }

        public string Label { get; }

        public string Stimulus { get; }

        public IReadOnlyList<string> Observations { get; }

        /// <summary>A copy: changing it changes nothing in the set.</summary>
        public JObject PreconditionState { get; }

        /// <summary>
        /// The prompts <paramref name="annotator"/> still has to label, in the set's order: every
        /// prompt they have neither written nor answered.
        /// </summary>
        /// <param name="labels">The label files, usually <see cref="PromptSet.LoadLabels"/>.</param>
        /// <param name="categoryId">Only this category; null for all of them.</param>
        public static IReadOnlyList<BlindPrompt> Queue(PromptSet set, IEnumerable<AnnotatorLabels> labels, string annotator, string categoryId = null)
        {
            var files = labels.ToList();
            var queue = new List<BlindPrompt>();
            foreach (var category in set.Categories)
            {
                if (categoryId != null && category.Id != categoryId)
                {
                    continue;
                }

                foreach (var prompt in category.Prompts)
                {
                    bool wrote = prompt.LabelledBy != null && prompt.LabelledBy.Contains(annotator);
                    if (!wrote && !AgreementReport.AnnotatorsOf(prompt, files).Contains(annotator))
                    {
                        queue.Add(new BlindPrompt(prompt, category.Id));
                    }
                }
            }

            return queue;
        }
    }
}

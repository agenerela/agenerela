using System.Linq;
using Agenerela.Evaluation;

namespace Agenerela.Editor.Evaluation
{
    /// <summary>
    /// How far one annotator is through Label mode (#158): "12 of 40 left to label". Counts only,
    /// so Label mode can show it and still hold nothing that gives an answer away.
    /// </summary>
    internal readonly struct LabelProgress
    {
        internal LabelProgress(int total, int left, int skipped)
        {
            Total = total;
            Left = left;
            Skipped = skipped;
        }

        /// <summary>The prompts this annotator is asked to label: every prompt they did not write.</summary>
        internal int Total { get; }

        /// <summary>Those they have not answered yet, skipped ones included.</summary>
        internal int Left { get; }

        /// <summary>Those skipped since Label mode opened, which come back when it opens again.</summary>
        internal int Skipped { get; }

        /// <summary>
        /// How many prompts <paramref name="annotator"/> is asked to label in <paramref name="set"/>:
        /// those they did not write. The ones still to answer are <see cref="BlindPrompt.Queue"/>.
        /// </summary>
        internal static int TotalFor(PromptSet set, string annotator)
        {
            return set.Prompts.Count(p => p.LabelledBy == null || !p.LabelledBy.Contains(annotator));
        }

        /// <summary>The line above each prompt.</summary>
        internal string Heading => $"{Left} of {Total} left to label";

        /// <summary>What Label mode says once it has no prompt to show <paramref name="annotator"/>.</summary>
        internal string NothingLeft(string annotator)
        {
            if (Total == 0)
            {
                return $"Nothing for {annotator} to label: every prompt in the set is one you wrote. " +
                       "Label mode asks for other people's prompts, so someone else labels yours.";
            }

            if (Left == 0)
            {
                return $"All done: {annotator} has labelled all {Total} prompts they did not write, and nothing is left. " +
                       "Agreement compares your answers with everyone else's.";
            }

            return $"{Left} of {Total} left to label, all of them ones you skipped. " +
                   "They come back when you open Label mode again or click Reload.";
        }
    }
}

using System;
using System.Threading;

namespace Agenerela.Evaluation
{
    /// <summary>
    /// What <see cref="EvalRunner.RunAsync"/> throws when its token is cancelled: an
    /// <see cref="OperationCanceledException"/>, so a <c>catch (OperationCanceledException)</c>
    /// still catches it, carrying the report as far as the run got.
    /// </summary>
    public sealed class EvalCancelledException : OperationCanceledException
    {
        internal EvalCancelledException(EvalReport partial, OperationCanceledException cause, CancellationToken token)
            : base($"The evaluation run was cancelled after {partial.Answers.Count} of {partial.Conditions.Prompts * 2} answers.", cause, token)
        {
            Partial = partial;
        }

        /// <summary>
        /// The report so far, <see cref="EvalReport.Cancelled"/> true: every answer given before the
        /// cancel, the conditions recorded until then, and a noise check only where both arms were
        /// asked the same prompts. Never an accuracy result: the set was not asked whole.
        /// </summary>
        public EvalReport Partial { get; }
    }
}

using System;
using Newtonsoft.Json;

namespace Agenerela.Evaluation
{
    /// <summary>How many answers fell into each of the five outcomes (build plan §4, item 5).</summary>
    /// <remarks>
    /// <see cref="Accuracy"/> is the share that is <see cref="DecisionOutcome.Correct"/> out of every
    /// prompt asked, pipeline errors included: a prompt the pipeline lost was not answered right.
    /// It is never reported alone, because each wrong outcome needs a different fix.
    /// </remarks>
    [JsonObject(MemberSerialization.OptIn)]
    public sealed class OutcomeCounts
    {
        [JsonProperty("total")] public int Total;
        [JsonProperty("correct")] public int Correct;
        [JsonProperty("wrongLegalAction")] public int WrongLegalAction;
        [JsonProperty("containedByGuard")] public int ContainedByGuard;
        [JsonProperty("rejectedWhenActionExpected")] public int RejectedWhenActionExpected;
        [JsonProperty("pipelineError")] public int PipelineError;

        /// <summary><see cref="Correct"/> out of <see cref="Total"/>, from 0 to 1; 0 when nothing was asked.</summary>
        [JsonProperty("accuracy")] public double Accuracy => Total == 0 ? 0 : (double)Correct / Total;

        /// <summary>The count for <paramref name="outcome"/>.</summary>
        public int this[DecisionOutcome outcome]
        {
            get
            {
                switch (outcome)
                {
                    case DecisionOutcome.Correct: return Correct;
                    case DecisionOutcome.WrongLegalAction: return WrongLegalAction;
                    case DecisionOutcome.ContainedByGuard: return ContainedByGuard;
                    case DecisionOutcome.RejectedWhenActionExpected: return RejectedWhenActionExpected;
                    case DecisionOutcome.PipelineError: return PipelineError;
                    default: throw new ArgumentOutOfRangeException(nameof(outcome), outcome, null);
                }
            }
        }

        internal void Add(DecisionOutcome outcome)
        {
            Total++;
            switch (outcome)
            {
                case DecisionOutcome.Correct: Correct++; break;
                case DecisionOutcome.WrongLegalAction: WrongLegalAction++; break;
                case DecisionOutcome.ContainedByGuard: ContainedByGuard++; break;
                case DecisionOutcome.RejectedWhenActionExpected: RejectedWhenActionExpected++; break;
                case DecisionOutcome.PipelineError: PipelineError++; break;
                default: throw new ArgumentOutOfRangeException(nameof(outcome), outcome, null);
            }
        }
    }
}

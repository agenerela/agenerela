namespace Agenerela.Evaluation
{
    /// <summary>
    /// How far an <see cref="EvalRunner"/> run has come, as <see cref="EvalRunner.Progress"/>
    /// reports it: the phase, the arm, and the answers so far out of every answer the run will give.
    /// </summary>
    public sealed class EvalProgress
    {
        internal EvalProgress(EvalPhase phase, string arm, int answered, int total)
        {
            Phase = phase;
            Arm = arm;
            Answered = answered;
            Total = total;
        }

        public EvalPhase Phase { get; }

        /// <summary>The arm's name; null while both servers are checked before anything is sent.</summary>
        public string Arm { get; }

        /// <summary>The answers given so far, both arms', warm-up excluded.</summary>
        public int Answered { get; }

        /// <summary>Every answer the run will give: the set's prompts times two arms.</summary>
        public int Total { get; }

        public override string ToString() => $"{Phase} {Arm ?? "both arms"} {Answered}/{Total}";
    }
}

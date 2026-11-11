using Agenerela;

namespace GreyBoxStrategy
{
    /// <summary>What the council call's stimulus says.</summary>
    public enum CouncilStimulus
    {
        /// <summary>"Your move." The situation is all in the facts.</summary>
        Plain,
        /// <summary>The draft's headline: "Turn 7 of 30. orsk took fenmark from us; we lost 12 soldiers."</summary>
        Headline,
    }

    /// <summary>
    /// The prompt-side choices a model seat makes, each one an arm of stage 2's A/B runs. The
    /// defaults are the control arm.
    /// </summary>
    public sealed class ModelSeatOptions
    {
        /// <summary>
        /// Where this turn's facts go: <see cref="ObservationPlacement.WithStimulus"/> (the control,
        /// after the stimulus) or <see cref="ObservationPlacement.AfterSources"/> (the framework's
        /// default, after the standing facts).
        /// </summary>
        public ObservationPlacement Placement = ObservationPlacement.WithStimulus;

        public CouncilStimulus Stimulus = CouncilStimulus.Plain;

        /// <summary>
        /// Whether the standing facts end with the line naming each move's legal targets
        /// (<see cref="Facts.MovesLine"/>): the game-side workaround for #216's N1, legal
        /// action–target pairs reaching a local model nowhere else.
        /// </summary>
        public bool MovesLine = true;

        /// <summary>
        /// <see cref="DecideOptions.CheckTarget"/>: whether the target grounding guard runs. Stage 2's
        /// first runs found it containing legal attacks when the model's probability is split
        /// across several provinces it could legally attack.
        /// </summary>
        public bool CheckTarget = true;

        /// <summary>A short name for logs and tables: "control", "after-sources", "headline", "no-moves-line".</summary>
        public string Arm = "control";

        public static ModelSeatOptions Control => new ModelSeatOptions();
        public static ModelSeatOptions AfterSources => new ModelSeatOptions { Placement = ObservationPlacement.AfterSources, Arm = "after-sources" };
        public static ModelSeatOptions Headline => new ModelSeatOptions { Stimulus = CouncilStimulus.Headline, Arm = "headline" };
        public static ModelSeatOptions NoMovesLine => new ModelSeatOptions { MovesLine = false, Arm = "no-moves-line" };
        public static ModelSeatOptions NoGrounding => new ModelSeatOptions { CheckTarget = false, Arm = "no-grounding" };

        /// <summary>The arm by its name, or null.</summary>
        public static ModelSeatOptions Named(string arm)
        {
            switch (arm)
            {
                case null:
                case "":
                case "control": return Control;
                case "after-sources": return AfterSources;
                case "headline": return Headline;
                case "no-moves-line": return NoMovesLine;
                case "no-grounding": return NoGrounding;
                default: return null;
            }
        }
    }
}

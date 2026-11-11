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
    /// field defaults are stage 2's control arm; the seat's default, <see cref="Default"/>, adds
    /// the paired schema to it (measured 10 November 2026, the README's "Can the framework replace
    /// the moves line?").
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

        /// <summary>
        /// <see cref="DecideOptions.LegalTargets"/>: whether the framework's own prompt says which
        /// targets each move may take now, from the per-target checks (#151). The framework's
        /// replacement for <see cref="MovesLine"/>.
        /// </summary>
        public LegalTargetListing LegalTargets = LegalTargetListing.Off;

        /// <summary>
        /// <see cref="DecideOptions.PairTargetsWithActions"/>: whether the schema pairs each move with
        /// the targets it may take, so the grammar cannot sample an illegal pair.
        /// </summary>
        public bool PairTargets;

        /// <summary>A short name for logs and tables: "control", "per-target", "no-moves-line", and so on.</summary>
        public string Arm = "control";

        /// <summary>
        /// What a seat plays with unless told otherwise: stage 2's control (the moves line, grounding
        /// on) plus the paired schema, which cut the model's illegal answers to none at no measured cost.
        /// </summary>
        public static ModelSeatOptions Default => new ModelSeatOptions { PairTargets = true, Arm = "default" };

        /// <summary>Stage 2's control arm: the moves line, a flat schema, grounding on.</summary>
        public static ModelSeatOptions Control => new ModelSeatOptions();
        public static ModelSeatOptions AfterSources => new ModelSeatOptions { Placement = ObservationPlacement.AfterSources, Arm = "after-sources" };
        public static ModelSeatOptions Headline => new ModelSeatOptions { Stimulus = CouncilStimulus.Headline, Arm = "headline" };
        public static ModelSeatOptions NoMovesLine => new ModelSeatOptions { MovesLine = false, Arm = "no-moves-line" };
        public static ModelSeatOptions NoGrounding => new ModelSeatOptions { CheckTarget = false, Arm = "no-grounding" };

        /// <summary>No moves line; the framework lists each move's legal targets (<see cref="LegalTargetListing.PerTarget"/>).</summary>
        public static ModelSeatOptions PerTarget => new ModelSeatOptions
            { MovesLine = false, LegalTargets = LegalTargetListing.PerTarget, Arm = "per-target" };

        /// <summary><see cref="PerTarget"/> with the paired schema.</summary>
        public static ModelSeatOptions PerTargetPaired => new ModelSeatOptions
            { MovesLine = false, LegalTargets = LegalTargetListing.PerTarget, PairTargets = true, Arm = "per-target-paired" };

        /// <summary>
        /// The arm by its name, or null. A name may add modifiers after a <c>+</c>, each applied on
        /// top of the arm: <c>+no-grounding</c> switches the grounding guard off, <c>+paired</c>
        /// switches the paired schema on, <c>+listed</c> the framework's listing
        /// (<see cref="LegalTargetListing.PerTarget"/>) ("control+paired", "control+listed",
        /// "per-target-paired+no-grounding").
        /// </summary>
        public static ModelSeatOptions Named(string arm)
        {
            int plus = arm?.IndexOf('+') ?? -1;
            if (plus >= 0)
            {
                var options = Named(arm.Substring(0, plus));
                if (options == null) return null;
                foreach (string modifier in arm.Substring(plus + 1).Split('+'))
                {
                    if (modifier == "no-grounding") options.CheckTarget = false;
                    else if (modifier == "paired") options.PairTargets = true;
                    else if (modifier == "listed") options.LegalTargets = LegalTargetListing.PerTarget;
                    else return null;
                }
                options.Arm = arm;
                return options;
            }
            switch (arm)
            {
                case null:
                case "":
                case "default": return Default;
                case "control": return Control;
                case "after-sources": return AfterSources;
                case "headline": return Headline;
                case "no-moves-line": return NoMovesLine;
                case "no-grounding": return NoGrounding;
                case "per-target": return PerTarget;
                case "per-target-paired": return PerTargetPaired;
                default: return null;
            }
        }
    }
}

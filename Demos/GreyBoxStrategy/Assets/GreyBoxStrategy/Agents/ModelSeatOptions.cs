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
    /// the moves line?") and the grounding threshold 0.6 (measured 11 November, gap N4).
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
        /// <see cref="DecideOptions.TargetGroundingThreshold"/>: the share below which the grounding
        /// guard contains a decision (#244). The framework's 0.8 here; <see cref="Default"/> sets 0.6.
        /// </summary>
        public float GroundingThreshold = TargetGroundingGuard.DefaultThreshold;

        /// <summary>
        /// <see cref="Agenerela.Providers.OllamaSettings.TokenAlternatives"/> for the model seat's
        /// provider: how many alternatives the grounding guard reads at each token (#244). The
        /// framework's default, 20 since 11 November 2026; <c>+alternatives-5</c> for the runs before.
        /// </summary>
        public int TokenAlternatives = Agenerela.Providers.OllamaSettings.DefaultTokenAlternatives;

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
        /// <summary>
        /// <see cref="RollingHistory.Remembers"/>: what a remembered move holds (#244 item 4, gap N7).
        /// <see cref="RememberedDecision.Whole"/>, the framework's default, keeps the statement;
        /// <c>+memory-move</c> leaves it out; <c>+memory-outcome</c> sends the turn report's line for
        /// the move in its place (<see cref="CountryAgent.Learn"/>).
        /// </summary>
        public RememberedDecision Remembers = RememberedDecision.Whole;

        /// <summary>
        /// <see cref="DecideOptions.StatementRulesWording"/>: how the persona's statement rules are
        /// introduced (#244 item 8, gap N9). The framework's default unless an arm sets another.
        /// </summary>
        public StatementRulesWording RulesWording = StatementRulesWording.YourStatement;

        /// <summary>
        /// Whether a <see cref="Storyteller"/> invents the game's events (the README's "Storyteller",
        /// stage S1 of the research page): one event a turn at upkeep in place of the random event
        /// table, which is then off (<see cref="Game.EventTable"/>). Off in every named arm; <c>+storyteller</c>
        /// switches it on. It adds what the rulers read, an event's title among their facts, so it is an
        /// arm until measured (hard rule 2).
        /// </summary>
        public bool Storyteller;

        /// <summary>Whether the storyteller reads an inspiration line (<see cref="GreyBoxStrategy.Storyteller.InspirationLine"/>); <c>+no-inspiration</c> drops it.</summary>
        public bool Inspiration = true;

        /// <summary>Which inspiration draw the storyteller gets: <c>+inspiration-1</c> draws other words on the same seeds, for the divergence runs.</summary>
        public int InspirationSalt;

        // --- The storyteller's stage S2 (the README's "Storyteller"), each a modifier on +storyteller ---

        /// <summary><c>+choices</c>: dilemmas; the ruler struck chooses (and the seats register the options as actions).</summary>
        public bool Choices;
        /// <summary><c>+multi</c>: group events reach the members the fairness rules allow.</summary>
        public bool Multi;
        /// <summary><c>+history</c>: the recent-happenings line, and the story check against invented history.</summary>
        public bool History;
        /// <summary><c>+traits</c>: province traits in the targets, and the three trait events.</summary>
        public bool Traits;
        /// <summary><c>+title-call</c>: the story in the statement, the title from a second call, "told so far" without titles.</summary>
        public bool TitleCall;
        /// <summary><c>+chains-free</c> or <c>+chains-skeleton</c>.</summary>
        public ChainMode Chains = ChainMode.None;
        /// <summary><c>+preset-&lt;name&gt;</c> (<see cref="StorytellerSettings.Presets"/>); null keeps S1's settings.</summary>
        public string Preset;

        /// <summary>The game's <see cref="StoryRules"/> for these options.</summary>
        public StoryRules StoryRules() => new StoryRules
        {
            Choices = Choices, Multi = Multi, History = History, Traits = Traits, TitleCall = TitleCall, Chains = Chains,
            Settings = Preset == null ? new StorytellerSettings() : StorytellerSettings.Named(Preset),
        };

        // --- The writs turn's voice (rules v2, after R1c: statements repeated 35% word for word) ---

        /// <summary>
        /// <c>+one-voice</c>: in the writs turn only a ruler's first court decision of a turn speaks. The second
        /// court order, army orders, treaty answers and dilemmas are asked to leave the statement empty, and a
        /// line written anyway is dropped (<see cref="ModelDecision.Unspoken"/>). The framework always asks for
        /// the statement field, so this is the demo's way. The framework's own way, <c>DecideOptions.AskForStatement</c> (#266),
        /// merged after the A/B of 13–14 November and is not used here yet.
        /// </summary>
        public bool OneVoice;
        /// <summary>
        /// <c>+own-lines</c>: in the writs turn a decision that speaks reads the ruler's last few lines, and is asked to
        /// say something new. On in <see cref="Default"/> since 14 November 2026 (statements repeated word for word fell
        /// from 36% to 22% in 15 games an arm, legality and persona unchanged); <c>+no-own-lines</c> switches it off.
        /// </summary>
        public bool OwnLines;
        /// <summary>How many of the ruler's last lines <see cref="OwnLines"/> shows.</summary>
        public const int OwnLinesShown = 4;

        /// <summary>A short name for logs and tables: "control", "per-target", "no-moves-line", and so on.</summary>
        public string Arm = "control";

        /// <summary>
        /// What a seat plays with unless told otherwise: stage 2's control (the moves line, grounding
        /// on) plus the paired schema, which cut the model's illegal answers to none at no measured
        /// cost, and a grounding threshold of 0.6, which with the framework's 20 alternatives cut the
        /// guard's containments of legal moves from 72 to 3 (qwen3.5:4b) and 94 to 3 (qwen3.5:2b) in
        /// 30 games (README, gap N4), and in the writs turn the ruler's own last lines (<see cref="OwnLines"/>,
        /// measured 13–14 November). The arm measured as <c>default</c> on 10 November is
        /// <c>control+paired+alternatives-5</c> now; the writs turn's <c>default</c> before 14 November is
        /// <c>default+no-own-lines</c>.
        /// </summary>
        public static ModelSeatOptions Default => new ModelSeatOptions { PairTargets = true, GroundingThreshold = 0.6f, OwnLines = true, Arm = "default" };

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
        /// top of the arm: <c>+no-grounding</c> switches the grounding guard off,
        /// <c>+threshold-0.5</c> sets its threshold, <c>+alternatives-20</c> how many alternatives
        /// the provider reports at each token (#244), <c>+paired</c>
        /// switches the paired schema on, <c>+listed</c> the framework's listing
        /// (<see cref="LegalTargetListing.PerTarget"/>) ("control+paired", "control+listed",
        /// "per-target-paired+no-grounding"). For #244: <c>+no-moves-line</c> drops the game's own
        /// moves line, <c>+moves-sentence</c> has the framework say the legal moves as an observation
        /// line (<see cref="LegalTargetListing.ObservationLine"/>), and <c>+how-it-reads</c> and
        /// <c>+own-words</c> word the statement rules (<see cref="StatementRulesWording"/>)
        /// ("default+no-moves-line+moves-sentence", "default+how-it-reads"), <c>+one-voice</c>, <c>+own-lines</c> and
        /// <c>+no-own-lines</c> for the writs turn's statements, and <c>+memory-move</c>
        /// and <c>+memory-outcome</c> what a remembered move holds (<see cref="Remembers"/>, #244 item 4).
        /// For the storyteller: <c>+storyteller</c> (<see cref="Storyteller"/>), <c>+no-inspiration</c> and
        /// <c>+inspiration-&lt;n&gt;</c> ("default+storyteller+inspiration-1"); stage S2's <c>+choices</c>,
        /// <c>+multi</c>, <c>+history</c>, <c>+traits</c>, <c>+title-call</c>, <c>+chains-free</c>,
        /// <c>+chains-skeleton</c>, <c>+preset-&lt;name&gt;</c>, and <c>+s2</c> for all but the chains on
        /// <c>even_hand</c> ("default+storyteller+s2+chains-free").
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
                    else if (modifier.StartsWith("threshold-")
                             && float.TryParse(modifier.Substring("threshold-".Length), System.Globalization.NumberStyles.Float,
                                 System.Globalization.CultureInfo.InvariantCulture, out float threshold)
                             && threshold >= 0f && threshold <= 1f)
                        options.GroundingThreshold = threshold;
                    else if (modifier.StartsWith("alternatives-")
                             && int.TryParse(modifier.Substring("alternatives-".Length), out int alternatives)
                             && alternatives >= 1 && alternatives <= Agenerela.Providers.OllamaSettings.MaxTokenAlternatives)
                        options.TokenAlternatives = alternatives;
                    else if (modifier == "paired") options.PairTargets = true;
                    else if (modifier == "listed") options.LegalTargets = LegalTargetListing.PerTarget;
                    else if (modifier == "memory-move") options.Remembers = RememberedDecision.Move;
                    else if (modifier == "memory-outcome") options.Remembers = RememberedDecision.MoveAndOutcome;
                    else if (modifier == "no-moves-line") options.MovesLine = false;
                    else if (modifier == "moves-sentence") options.LegalTargets = LegalTargetListing.ObservationLine;
                    else if (modifier == "how-it-reads") options.RulesWording = StatementRulesWording.HowItShouldRead;
                    else if (modifier == "own-words") options.RulesWording = StatementRulesWording.InYourOwnWords;
                    else if (modifier == "one-voice") options.OneVoice = true;
                    else if (modifier == "own-lines") options.OwnLines = true;
                    else if (modifier == "no-own-lines") options.OwnLines = false;
                    else if (modifier == "storyteller") options.Storyteller = true;
                    else if (modifier == "choices") options.Choices = true;
                    else if (modifier == "multi") options.Multi = true;
                    else if (modifier == "history") options.History = true;
                    else if (modifier == "traits") options.Traits = true;
                    else if (modifier == "title-call") options.TitleCall = true;
                    else if (modifier == "chains-free") options.Chains = ChainMode.Free;
                    else if (modifier == "chains-skeleton") options.Chains = ChainMode.Skeleton;
                    else if (modifier.StartsWith("preset-") && StorytellerSettings.Named(modifier.Substring("preset-".Length)) != null)
                        options.Preset = modifier.Substring("preset-".Length);
                    else if (modifier == "s2")
                    {
                        // Every S2 piece but the chains, on the measured preset.
                        options.Choices = options.Multi = options.History = options.Traits = options.TitleCall = true;
                        options.Preset = options.Preset ?? StorytellerSettings.EvenHand.Name;
                    }
                    else if (modifier == "no-inspiration") options.Inspiration = false;
                    else if (modifier.StartsWith("inspiration-") && int.TryParse(modifier.Substring("inspiration-".Length), out int salt) && salt >= 0)
                        options.InspirationSalt = salt;
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

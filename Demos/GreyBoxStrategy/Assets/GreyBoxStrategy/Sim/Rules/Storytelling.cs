using System;
using System.Collections.Generic;
using System.Linq;

namespace GreyBoxStrategy
{
    /// <summary>How chains are written: none (S1), freely by the model, or on skeletons the game registers.</summary>
    public enum ChainMode
    {
        None,
        /// <summary>Any recent event can be continued with any effect its subject may take; <c>end_tale</c> closes one.</summary>
        Free,
        /// <summary>The game's <see cref="ChainSkeletons"/>: <c>begin_&lt;skeleton&gt;</c> starts one, and each later stage is prescribed.</summary>
        Skeleton,
    }

    /// <summary>How hard the storyteller leans toward the weak and against the leader.</summary>
    public enum StoryLean
    {
        /// <summary>No lean: a gift may go to the leader; the budget still holds.</summary>
        None,
        /// <summary>S1's rules: no gift at <see cref="GameRules.EventNoBonusFromProvinces"/> provinces, no blow under <see cref="GameRules.EventMinProvincesForPenalty"/>.</summary>
        Mild,
        /// <summary>As mild, and gifts only to countries at or below the mean in provinces, blows only on those at or above it.</summary>
        Strong,
    }

    /// <summary>
    /// Storyteller settings a player could tweak before a game, as plain data a menu can list
    /// (<see cref="Presets"/>). The chances are drawn from the game's seed, so a game is repeatable.
    /// </summary>
    public sealed class StorytellerSettings
    {
        public string Name = "s1";
        public string Label = "Stage S1";
        public string Description = "The first storyteller's rules.";
        public StoryLean Lean = StoryLean.Mild;
        /// <summary>The chance, each turn, that the storyteller is asked at all.</summary>
        public double ActChance = 1.0;
        /// <summary>The chance that a turn is a dilemma's (with <c>+choices</c>): only dilemmas, and open tales, are offered on it.</summary>
        public double ChoiceChance = 0.25;
        /// <summary>The chance that a turn is a tale's (with a chain mode): only beginnings and continuations of tales are offered on it.</summary>
        public double ChainChance = 0.15;
        /// <summary>Whether story-only events (an omen, no effect) are open; off in S1's settings, on in every preset but cruel_fate.</summary>
        public bool StoryOnly;

        public StorytellerSettings Copy() => (StorytellerSettings)MemberwiseClone();

        public static StorytellerSettings EvenHand => new StorytellerSettings
        {
            Name = "even_hand", Label = "Even hand", StoryOnly = true,
            Description = "Helps those falling behind as often as it troubles the strong, as the first storyteller did; dilemmas and tales now and then.",
        };

        public static StorytellerSettings Underdog => new StorytellerSettings
        {
            Name = "underdog", Label = "Underdog's friend", Lean = StoryLean.Strong, StoryOnly = true,
            Description = "Gifts only for those at or below the middle, blows only for those at or above it.",
        };

        public static StorytellerSettings CruelFate => new StorytellerSettings
        {
            Name = "cruel_fate", Label = "Cruel fate", Lean = StoryLean.None, StoryOnly = false,
            Description = "No lean: fortune may crown the leader. The budget still stops a run of luck on one country.",
        };

        public static StorytellerSettings QuietYears => new StorytellerSettings
        {
            Name = "quiet_years", Label = "Quiet years", ActChance = 0.5, ChoiceChance = 0.15, ChainChance = 0.08, StoryOnly = true,
            Description = "Acts on about half the turns, rarely asks for a decision, rarely starts a tale.",
        };

        public static StorytellerSettings Saga => new StorytellerSettings
        {
            Name = "saga", Label = "Saga", ChoiceChance = 0.35, ChainChance = 0.3, StoryOnly = true,
            Description = "Many dilemmas and long tales.",
        };

        /// <summary>Every named preset, the measured one (<see cref="EvenHand"/>) first.</summary>
        public static IReadOnlyList<StorytellerSettings> Presets => new[] { EvenHand, Underdog, CruelFate, QuietYears, Saga };

        public static StorytellerSettings Named(string name) => Presets.FirstOrDefault(p => p.Name == name);
    }

    /// <summary>
    /// Which of stage S2's pieces a game's storyteller uses, and its settings. <see cref="S1"/>, every
    /// piece off, is the first storyteller exactly.
    /// </summary>
    public sealed class StoryRules
    {
        public bool Choices;
        public bool Multi;
        public bool History;
        public bool Traits;
        public bool TitleCall;
        public ChainMode Chains = ChainMode.None;
        public StorytellerSettings Settings = new StorytellerSettings();

        public static StoryRules S1 => new StoryRules();

        /// <summary>Whether any S2 piece is on.</summary>
        public bool IsS2 => Choices || Multi || History || Traits || TitleCall || Chains != ChainMode.None || Settings.Name != "s1";

        /// <summary>Whether this template is offered under these rules.</summary>
        public bool Offers(EventTemplate t)
        {
            switch (t.Feature)
            {
                case EventFeature.S1: return true;
                case EventFeature.Choice: return Choices;
                case EventFeature.Trait: return Traits;
                case EventFeature.StoryOnly: return IsS2 && Settings.StoryOnly;
                case EventFeature.FreeChain: return Chains == ChainMode.Free;
                case EventFeature.SkeletonChain: return Chains == ChainMode.Skeleton;
                case EventFeature.ChainDilemma: return Chains == ChainMode.Skeleton;
                default: return false;
            }
        }
    }

    /// <summary>
    /// What a turn is for (S2), dealt by the game from the preset's chances: a tale's beginning, a
    /// dilemma, or an ordinary event. A tale already open may go on in any turn.
    /// </summary>
    public enum TurnKind
    {
        Ordinary,
        Dilemma,
        Tale,
    }

    /// <summary>Which S2 piece registers a template.</summary>
    public enum EventFeature
    {
        S1,
        Choice,
        Trait,
        StoryOnly,
        FreeChain,
        SkeletonChain,
        /// <summary>A dilemma told only as a stage of a skeleton chain.</summary>
        ChainDilemma,
    }

    // --- Effects as data ----------------------------------------------------------------------

    public enum EffectKind
    {
        Gold,
        /// <summary>The country's income is halved at this upkeep.</summary>
        IncomeCut,
        ArmyLoss,
        Soldiers,
        Stability,
        /// <summary>A fort in the event's province.</summary>
        Fort,
    }

    /// <summary>One part of an effect: a kind, a sign and a size; the game sets the amount (<see cref="EventEffects.Plan"/>).</summary>
    public sealed class EffectPart
    {
        public EffectPart(EffectKind kind, int sign, bool small = false)
        {
            Kind = kind;
            Sign = sign;
            Small = small;
        }

        public EffectKind Kind { get; }
        public int Sign { get; }
        public bool Small { get; }

        public static EffectPart GainGold(bool small = false) => new EffectPart(EffectKind.Gold, +1, small);
        public static EffectPart PayGold(bool small = false) => new EffectPart(EffectKind.Gold, -1, small);
        public static EffectPart CutIncome => new EffectPart(EffectKind.IncomeCut, -1);
        public static EffectPart LoseSoldiers(bool small = false) => new EffectPart(EffectKind.ArmyLoss, -1, small);
        public static EffectPart GainSoldiers(bool small = false) => new EffectPart(EffectKind.Soldiers, +1, small);
        public static EffectPart Steadier => new EffectPart(EffectKind.Stability, +1);
        public static EffectPart Shakier => new EffectPart(EffectKind.Stability, -1);
        public static EffectPart RaiseFort => new EffectPart(EffectKind.Fort, +1);
    }

    /// <summary>One option of a dilemma: what the ruler reads, what the game applies, and a lean for the analysis.</summary>
    public sealed class DilemmaOption
    {
        public DilemmaOption(string id, string label, string lean, params EffectPart[] parts)
        {
            Id = id;
            Label = label;
            Lean = lean;
            Parts = parts;
        }

        /// <summary>The ruler's action id: unique across every dilemma.</summary>
        public string Id { get; }
        /// <summary>"hire them": what the option is, in the game's words.</summary>
        public string Label { get; }
        /// <summary>war, thrift, gold, order or faith: what kind of ruler would choose it.</summary>
        public string Lean { get; }
        public IReadOnlyList<EffectPart> Parts { get; }
        public override string ToString() => Id;
    }

    /// <summary>What the game applies for one effect part, for one country.</summary>
    public static class EventEffects
    {
        public static (int amount, int value, string text) Plan(World w, EffectPart p, string countryId)
        {
            var r = w.Rules;
            var c = w.Country(countryId);
            switch (p.Kind)
            {
                case EffectKind.Gold:
                {
                    int full = Math.Max(r.EventWindfallMin, Math.Min(r.EventWindfallMax, w.Income(c)));
                    int gold = p.Small ? Math.Max(10, full / 2) : full;
                    return (gold, p.Sign * gold, $"{gold} gold");
                }
                case EffectKind.IncomeCut:
                {
                    int cut = w.Income(c) / 2;
                    return (cut, -cut, $"{cut} gold of income");
                }
                case EffectKind.ArmyLoss:
                {
                    double share = p.Small ? r.EventPlagueShare / 2 : r.EventPlagueShare;
                    int lost = Math.Min(c.Army, Math.Max(p.Small ? 2 : 3, (int)Math.Round(c.Army * share)));
                    return (lost, -lost * r.GoldPerSoldier, $"{lost} soldiers");
                }
                case EffectKind.Soldiers:
                {
                    int full = Math.Min(r.EventVolunteersMax, r.LevyBase + 3 * w.ProvinceCount(countryId));
                    int gain = p.Small ? Math.Max(3, full / 2) : full;
                    return (gain, gain * r.GoldPerSoldier, $"{gain} soldiers");
                }
                case EffectKind.Stability: return (1, p.Sign * EventRules.StabilityValue, "1 stability");
                case EffectKind.Fort: return (1, p.Sign * r.FortCost, "1 fort");
                default: return (0, 0, "nothing");
            }
        }

        /// <summary>Why this part cannot apply to the country now, or null: the gold to pay, an army to lose, stability to lose or gain, room for a fort.</summary>
        public static string WhyNot(World w, EffectPart p, string countryId, string province)
        {
            var r = w.Rules;
            var c = w.Country(countryId);
            switch (p.Kind)
            {
                case EffectKind.Gold:
                    if (p.Sign < 0 && c.Gold < Plan(w, p, countryId).amount) return $"{countryId} cannot pay {Plan(w, p, countryId).amount} gold";
                    return null;
                case EffectKind.ArmyLoss: return c.Army < r.EventMinArmyForPlague ? $"{countryId}'s army is too small to lose men" : null;
                case EffectKind.Stability:
                    if (p.Sign > 0 && c.Stability >= r.MaxStability) return $"{countryId}'s stability is already {c.Stability}";
                    if (p.Sign < 0 && c.Stability <= 2) return $"{countryId}'s stability is already low";
                    return null;
                case EffectKind.Fort:
                {
                    var pr = w.Province(province);
                    if (pr == null) return "a fort needs a province";
                    if (p.Sign > 0 && pr.Forts >= r.MaxFortsPerProvince) return $"{province} already has {pr.Forts} forts";
                    if (p.Sign < 0 && (pr.Forts == 0 || pr.IsCapital)) return $"{province} has no fort to lose";
                    return null;
                }
                default: return null;
            }
        }

        /// <summary>Applies one planned part; a cut in income is left in <paramref name="incomeCut"/> for this upkeep.</summary>
        public static void Apply(World w, EffectPart p, string countryId, string province, int amount, Dictionary<string, int> incomeCut)
        {
            var c = w.Country(countryId);
            switch (p.Kind)
            {
                case EffectKind.Gold: c.Gold = Math.Max(0, c.Gold + p.Sign * amount); break;
                case EffectKind.IncomeCut: incomeCut[countryId] = (incomeCut.TryGetValue(countryId, out int had) ? had : 0) + amount; break;
                case EffectKind.ArmyLoss: c.Army = Math.Max(0, c.Army - amount); break;
                case EffectKind.Soldiers: c.Army += amount; break;
                case EffectKind.Stability:
                    if (p.Sign > 0) c.Stability = Math.Min(w.Rules.MaxStability, c.Stability + 1);
                    else
                    {
                        // No recovery at this upkeep either, as for unrest.
                        c.Stability = Math.Max(1, c.Stability - 1);
                        c.RaisedTaxes = true;
                    }
                    break;
                case EffectKind.Fort:
                    var pr = w.Province(province);
                    pr.Forts = Math.Max(0, pr.Forts + p.Sign);
                    break;
            }
        }

        /// <summary>A part in the ruler's words: "pay 30 gold", "15 soldiers join our army", "income halved this turn (15 gold)".</summary>
        public static string Phrase(EffectPart p, int amount, string province)
        {
            switch (p.Kind)
            {
                case EffectKind.Gold: return p.Sign < 0 ? $"pay {amount} gold" : $"gain {amount} gold";
                case EffectKind.IncomeCut: return $"income halved this turn ({amount} gold)";
                case EffectKind.ArmyLoss: return $"lose {amount} soldiers";
                case EffectKind.Soldiers: return $"{amount} soldiers join our army";
                case EffectKind.Stability: return p.Sign > 0 ? "stability +1" : "stability -1";
                case EffectKind.Fort: return p.Sign > 0 ? $"+1 fort in {province}" : $"-1 fort in {province}";
                default: return "nothing";
            }
        }
    }

    // --- Choices ------------------------------------------------------------------------------

    /// <summary>One option as offered to one country, with the game's amounts.</summary>
    public sealed class ChoiceOption
    {
        public string Id;
        public string Label;
        public string Lean;
        /// <summary>"pay 30 gold, 15 soldiers join our army", or "nothing changes".</summary>
        public string Effect;
        public List<(EffectPart part, int amount, int value, string text)> Planned = new List<(EffectPart, int, int, string)>();
        public int Value => Planned.Sum(x => x.value);
    }

    /// <summary>
    /// A decision an event asks of one ruler, for the screen and the model seats alike: the story,
    /// the options with what each does, and what was chosen. Answered between turns
    /// (<see cref="Game.Choose"/>); an unanswered choice takes <see cref="DefaultOption"/> at upkeep.
    /// </summary>
    public sealed class EventChoice
    {
        public string Id;
        /// <summary>The upkeep the event and its choice land at.</summary>
        public int Turn;
        public string Country;
        public string Template;
        public string Title;
        public string Story;
        public string Province;
        public string ChainId;
        public List<ChoiceOption> Options = new List<ChoiceOption>();
        public string DefaultOption;
        /// <summary>The option chosen, or null while it waits.</summary>
        public string Chosen;
        /// <summary>model, script, random, person, or default when nobody answered.</summary>
        public string ChosenBy;

        public ChoiceOption Option(string id) => Options.FirstOrDefault(o => o.Id == id);

        /// <summary>"hire_sellswords, hire them: pay 30 gold, 15 soldiers join our army; send_sellswords_away, ..."</summary>
        public string OptionsLine() => "Our choice: " + string.Join("; ", Options.Select(o => $"{o.Id}, {o.Label}: {o.Effect}")) + ".";
    }

    // --- Province traits ----------------------------------------------------------------------

    /// <summary>A province's terrain, landmark and resource: data an event can depend on and name.</summary>
    public sealed class ProvinceTraits
    {
        public ProvinceTraits(string terrain, string landmark, string resource)
        {
            Terrain = terrain;
            Landmark = landmark;
            Resource = resource;
        }

        public string Terrain { get; }
        public string Landmark { get; }
        public string Resource { get; }
        public bool Holy => TraitCatalog.HolyLandmarks.Contains(Landmark);
        public bool Mined => TraitCatalog.MinedResources.Contains(Resource);
        public bool Wet => TraitCatalog.WetTerrains.Contains(Terrain);
        public string Line => $"{Terrain}, {Landmark}, {Resource}";
        public override string ToString() => Line;
    }

    /// <summary>The trait lists, and their seeded assignment: any map, any number of provinces.</summary>
    public static class TraitCatalog
    {
        public static readonly IReadOnlyList<string> Terrains = new[]
            { "hills", "marsh", "forest", "plains", "coast", "river valley", "moor", "heath" };
        public static readonly HashSet<string> WetTerrains = new HashSet<string> { "marsh", "coast", "river valley" };

        public static readonly IReadOnlyList<string> Landmarks = new[]
        {
            "an old abbey", "a stone bridge", "a ruined watchtower", "a standing stone", "a pilgrims' shrine", "a lighthouse",
            "a great oak", "a salt market", "a sunken chapel", "a hill fort", "a holy well", "a windmill", "a toll gate",
            "a crossroads inn", "a cathedral", "a hermit's cave", "a burial mound", "a horse fair", "a monastery", "an old mine",
        };
        public static readonly HashSet<string> HolyLandmarks = new HashSet<string>
            { "an old abbey", "a pilgrims' shrine", "a sunken chapel", "a holy well", "a cathedral", "a hermit's cave", "a monastery" };

        public static readonly IReadOnlyList<string> Resources = new[]
            { "grain", "iron", "salt", "timber", "wool", "horses", "wine", "copper", "fish", "silver", "honey", "stone" };
        public static readonly HashSet<string> MinedResources = new HashSet<string> { "iron", "salt", "copper", "silver", "stone" };

        /// <summary>Three traits for every province, drawn from the seed and the province id.</summary>
        public static Dictionary<string, ProvinceTraits> Assign(IEnumerable<string> provinceIds, int seed)
        {
            var map = new Dictionary<string, ProvinceTraits>(StringComparer.Ordinal);
            foreach (string id in provinceIds)
                map[id] = new ProvinceTraits(
                    Terrains[Dice.Int(seed, 0, id, "trait-terrain", 0, Terrains.Count)],
                    Landmarks[Dice.Int(seed, 0, id, "trait-landmark", 0, Landmarks.Count)],
                    Resources[Dice.Int(seed, 0, id, "trait-resource", 0, Resources.Count)]);
            return map;
        }
    }

    // --- Chains -------------------------------------------------------------------------------

    public enum ChainStatus
    {
        /// <summary>One event so far, which may still be continued (free chains only).</summary>
        Single,
        Open,
        Completed,
        Abandoned,
    }

    /// <summary>
    /// A story over several turns, held by the game: its subject, the events told in it with what
    /// each ruler chose, and its status. An open one is a target of category <c>tale</c>.
    /// </summary>
    public sealed class Chain
    {
        public string Id;
        public ChainMode Mode;
        /// <summary>The skeleton's id (<c>pretender</c>), or null for a free chain.</summary>
        public string Skeleton;
        /// <summary>The target the first event struck: a country, a group or a province.</summary>
        public string Subject;
        public EventTargetKind SubjectKind;
        /// <summary>The countries the first event reached.</summary>
        public List<string> Countries = new List<string>();
        public string Province;
        public string Title;
        public List<EventRecord> Events = new List<EventRecord>();
        public ChainStatus Status = ChainStatus.Single;
        /// <summary>The skeleton stage told last.</summary>
        public int StageIndex;
        /// <summary>Why it ended: "told to its end", "end_tale", "not continued", "its subject is out".</summary>
        public string EndReason;

        public int LastTurn => Events.Count == 0 ? 0 : Events[Events.Count - 1].Turn;
        public bool Started => Events.Count >= 2;
        public bool IsOpenTarget => Status == ChainStatus.Single || Status == ChainStatus.Open;
        public override string ToString() => $"{Id} \"{Title}\" ({Events.Count} events, {Status})";
    }

    /// <summary>One stage of a skeleton: the templates that may tell it, and which stage follows (by the option chosen).</summary>
    public sealed class ChainStage
    {
        public ChainStage(string[] templates, int next = -2, Dictionary<string, int> branch = null)
        {
            Templates = templates;
            Next = next;
            Branch = branch;
        }

        public IReadOnlyList<string> Templates { get; }
        /// <summary>The next stage's index; -1 ends the chain; -2 means the one after this.</summary>
        public int Next { get; }
        /// <summary>For a dilemma stage: the next stage by the option chosen.</summary>
        public Dictionary<string, int> Branch { get; }
    }

    /// <summary>A chain the game registers, which the model starts with <c>begin_&lt;id&gt;</c> and fills with its stories.</summary>
    public sealed class ChainSkeleton
    {
        public string Id;
        public string Description;
        public EventTargetKind Takes;
        public ChainStage[] Stages;
        /// <summary>Not before this turn.</summary>
        public int FromTurn;
        public bool OncePerGame;
        /// <summary>A province subject must have this trait (only checked with traits on).</summary>
        public Func<ProvinceTraits, bool> Needs;
        public string BeginAction => "begin_" + Id;
    }

    /// <summary>The game's chain skeletons.</summary>
    public static class ChainSkeletons
    {
        public const int MaxEvents = 6;
        /// <summary>A chain this long or longer is long: one a game.</summary>
        public const int Long = 4;
        public const int MaxOpen = 2;
        /// <summary>A chain whose last event is this many turns old is abandoned.</summary>
        public const int IdleTurns = 3;

        public static readonly IReadOnlyList<ChainSkeleton> All = new[]
        {
            new ChainSkeleton
            {
                Id = "pretender", Takes = EventTargetKind.Country,
                Description = "Begin a tale of a pretender to the crown, two or three events over the next turns: first the ruler pays him or defies him.",
                Stages = new[]
                {
                    new ChainStage(new[] { "pretender" }, branch: new Dictionary<string, int> { ["pay_pretender"] = 1, ["defy_pretender"] = 2 }),
                    new ChainStage(new[] { EventRules.Zeal }, -1),
                    new ChainStage(new[] { EventRules.Unrest }),
                    new ChainStage(new[] { "pretender_rising" }, -1),
                },
            },
            new ChainSkeleton
            {
                Id = "deep_vein", Takes = EventTargetKind.Province, Needs = t => t.Mined,
                Description = "Begin a tale of a mine in a province, two or three events: a strike of ore, then the ruler digs deeper or seals it.",
                Stages = new[]
                {
                    new ChainStage(new[] { "rich_vein", EventRules.Windfall }),
                    new ChainStage(new[] { "deep_vein" }, branch: new Dictionary<string, int> { ["dig_deeper"] = 2, ["seal_the_mine"] = -1 }),
                    new ChainStage(new[] { EventRules.Plague, EventRules.Unrest }, -1),
                },
            },
            new ChainSkeleton
            {
                Id = "fever", Takes = EventTargetKind.Country,
                Description = "Begin a tale of a fever, three events: a sickness, then the ruler quarantines the camps or marches on, then what came of it.",
                Stages = new[]
                {
                    new ChainStage(new[] { EventRules.Plague }),
                    new ChainStage(new[] { "fever_camps" }, branch: new Dictionary<string, int> { ["quarantine_camps"] = 2, ["march_on"] = 3 }),
                    new ChainStage(new[] { EventRules.Zeal }, -1),
                    new ChainStage(new[] { EventRules.Unrest }, -1),
                },
            },
            new ChainSkeleton
            {
                Id = "long_winter", Takes = EventTargetKind.Group, FromTurn = 8, OncePerGame = true,
                Description = "Begin a long tale of a hard winter over several countries, six events: famine, rationing, sickness, a thaw, a spring fair, an omen. Rare.",
                Stages = new[]
                {
                    new ChainStage(new[] { EventRules.BadHarvest }),
                    new ChainStage(new[] { "ration" }),
                    new ChainStage(new[] { EventRules.Plague, EventRules.BadHarvest }),
                    new ChainStage(new[] { EventRules.Zeal }),
                    new ChainStage(new[] { EventRules.Windfall }),
                    new ChainStage(new[] { "omen", EventRules.Zeal }, -1),
                },
            },
        };

        public static ChainSkeleton Named(string id) => All.FirstOrDefault(s => s.Id == id);
        public static ChainSkeleton ByAction(string action) => All.FirstOrDefault(s => s.BeginAction == action);

        /// <summary>The stage after the one told last, or -1 when the chain is over.</summary>
        public static int NextStage(ChainSkeleton s, Chain c)
        {
            var stage = s.Stages[c.StageIndex];
            if (stage.Branch != null)
            {
                var last = c.Events[c.Events.Count - 1];
                // A group's choice: the option most of its rulers took.
                string chosen = last.Choices.Values.GroupBy(v => v).OrderByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.Ordinal)
                    .Select(g => g.Key).FirstOrDefault();
                if (chosen != null && stage.Branch.TryGetValue(chosen, out int to)) return to;
                return stage.Next == -2 ? c.StageIndex + 1 : stage.Next;
            }
            int next = stage.Next == -2 ? c.StageIndex + 1 : stage.Next;
            return next >= s.Stages.Length ? -1 : next;
        }
    }

    // --- What happened in the game ------------------------------------------------------------

    /// <summary>One thing that happened, read from the game's reports, for the storyteller's facts and the history check.</summary>
    public sealed class Happening
    {
        public int Turn;
        /// <summary>capture, failed_attack, war, truce, out, revolt, choice, lead.</summary>
        public string Kind;
        public List<string> Countries = new List<string>();
        public string Province;
        public string Text;
        public override string ToString() => $"turn {Turn}: {Text}";
    }

    /// <summary>The game's history as happenings, and the mechanical check of a story's claims against it.</summary>
    public static class History
    {
        private static readonly System.Text.RegularExpressions.Regex TookFrom =
            new System.Text.RegularExpressions.Regex(@"^took (\S+) from (the neutrals|\S+) \(");
        private static readonly System.Text.RegularExpressions.Regex Failed =
            new System.Text.RegularExpressions.Regex(@"^attack on (\S+) failed");

        /// <summary>Every happening of the game so far, in order.</summary>
        public static List<Happening> Of(Game game)
        {
            var list = new List<Happening>();
            string leader = null;
            foreach (var r in game.Reports)
            {
                foreach (var o in r.Outcomes)
                {
                    if (o.Request == null || o.Refused || o.Text == null) continue;
                    string me = o.Request.CountryId;
                    var m = TookFrom.Match(o.Text);
                    if (m.Success)
                    {
                        string from = m.Groups[2].Value;
                        var h = new Happening { Turn = r.Turn, Kind = "capture", Province = m.Groups[1].Value };
                        h.Countries.Add(me);
                        if (from != "the neutrals") h.Countries.Add(from);
                        h.Text = from == "the neutrals" ? $"{me} took {m.Groups[1].Value} from the neutrals" : $"{me} took {m.Groups[1].Value} from {from}";
                        list.Add(h);
                        continue;
                    }
                    var f = Failed.Match(o.Text);
                    if (f.Success)
                    {
                        string owner = game.Snapshots.FirstOrDefault(s => s.Turn == r.Turn)?.OwnerOf(f.Groups[1].Value);
                        var h = new Happening { Turn = r.Turn, Kind = "failed_attack", Province = f.Groups[1].Value };
                        h.Countries.Add(me);
                        if (owner != null) h.Countries.Add(owner);
                        h.Text = owner != null ? $"{me} failed to take {f.Groups[1].Value} from {owner}" : $"{me} failed to take {f.Groups[1].Value}";
                        list.Add(h);
                        continue;
                    }
                    if (o.Text.StartsWith("declared war on ", StringComparison.Ordinal))
                    {
                        string on = o.Text.Substring("declared war on ".Length);
                        list.Add(new Happening { Turn = r.Turn, Kind = "war", Countries = { me, on }, Text = $"{me} declared war on {on}" });
                    }
                }
                foreach (string line in r.Resolution)
                {
                    if (line.Contains(": a truce for "))
                    {
                        var who = game.World.CountryList.Where(c => System.Text.RegularExpressions.Regex.IsMatch(line, @"\b" + c.Id + @"\b")).Select(c => c.Id).ToList();
                        if (who.Count == 2)
                            list.Add(new Happening { Turn = r.Turn, Kind = "truce", Countries = who, Text = $"{who[0]} and {who[1]} made a truce" });
                    }
                    if (line.EndsWith("is out of the game.", StringComparison.Ordinal))
                    {
                        string id = line.Split(' ')[0];
                        list.Add(new Happening { Turn = r.Turn, Kind = "out", Countries = { id }, Text = $"{id} lost its last province and fell" });
                    }
                }
                foreach (string line in r.Upkeep)
                {
                    int at = line.IndexOf(" revolted and is neutral again", StringComparison.Ordinal);
                    if (at < 0) continue;
                    // "kesh: fenmark revolted and is neutral again."
                    int colon = line.IndexOf(": ", StringComparison.Ordinal);
                    if (colon < 0) continue;
                    string c = line.Substring(0, colon), p = line.Substring(colon + 2, at - colon - 2);
                    list.Add(new Happening { Turn = r.Turn, Kind = "revolt", Countries = { c }, Province = p, Text = $"{p} revolted against {c}" });
                }
                foreach (var e in r.Events)
                    foreach (var kv in e.Choices)
                    {
                        var t = EventRules.Template(e.Template);
                        var o = t?.Options?.FirstOrDefault(x => x.Id == kv.Value);
                        if (o == null) continue;
                        list.Add(new Happening { Turn = r.Turn, Kind = "choice", Countries = { kv.Key }, Province = e.Province,
                            Text = $"{kv.Key} chose to {o.Label} (\"{e.Title}\")" });
                    }
                var counts = r.ProvinceCounts.Where(kv => kv.Value > 0).ToList();
                if (counts.Count > 0)
                {
                    int most = counts.Max(kv => kv.Value);
                    var top = counts.Where(kv => kv.Value == most).Select(kv => kv.Key).ToList();
                    if (top.Count == 1 && top[0] != leader)
                    {
                        if (leader != null) list.Add(new Happening { Turn = r.Turn, Kind = "lead", Countries = { top[0], leader }, Text = $"{top[0]} overtook {leader} as the largest" });
                        leader = top[0];
                    }
                }
            }
            return list;
        }

        private static readonly Dictionary<string, int> Weight = new Dictionary<string, int>
            { ["out"] = 0, ["capture"] = 1, ["war"] = 2, ["truce"] = 3, ["choice"] = 4, ["lead"] = 5, ["revolt"] = 6, ["failed_attack"] = 7 };

        /// <summary>The happenings of the last <paramref name="turns"/> turns worth telling, at most <paramref name="max"/>, latest last; captures from the neutrals left out.</summary>
        public static List<Happening> Recent(IReadOnlyList<Happening> all, int nextTurn, int turns = 3, int max = 6) =>
            all.Where(h => h.Turn >= nextTurn - turns && !(h.Kind == "capture" && h.Countries.Count == 1))
                .OrderBy(h => Weight[h.Kind]).ThenByDescending(h => h.Turn).Take(max)
                .OrderBy(h => h.Turn).ThenBy(h => Weight[h.Kind]).ToList();

        private static readonly System.Text.RegularExpressions.Regex Claim = new System.Text.RegularExpressions.Regex(
            @"\b(took|taken|captur\w*|seiz\w*|conquer\w*|fell to|fallen to|sack\w*|storm(ed|ing)|defeat\w*|routed|overr?an|invad\w*|razed|besieg\w*|battle\w*|slaughter\w*)\b",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        private static bool Names(string text, string id, string name) =>
            System.Text.RegularExpressions.Regex.IsMatch(text, @"\b" + System.Text.RegularExpressions.Regex.Escape(id) + @"\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase)
            || (name != null && System.Text.RegularExpressions.Regex.IsMatch(text, @"\b" + System.Text.RegularExpressions.Regex.Escape(name) + @"\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase));

        /// <summary>
        /// The story's claims of capture or battle, checked against the game: a sentence with such a
        /// verb that names a province must match a capture or an attack there; one that names two
        /// countries and no province must match a fight between them. Returns the first false claim
        /// ("invented: ..."), else the first true one ("real: ..."), else a story naming a country or
        /// province from <paramref name="recent"/> that the event did not reach ("real: ..."), else null.
        /// A claim with no name in it, a motive or a count cannot be checked.
        /// </summary>
        public static string Check(World w, IReadOnlyList<Happening> all, IReadOnlyList<Happening> recent, IReadOnlyList<string> hit, string story)
        {
            if (string.IsNullOrWhiteSpace(story)) return null;
            string real = null;
            foreach (string sentence in System.Text.RegularExpressions.Regex.Split(story, @"(?<=[.!?;])\s+"))
            {
                if (!Claim.IsMatch(sentence)) continue;
                var provinces = w.ProvinceList.Where(p => Names(sentence, p.Id, p.Name)).Select(p => p.Id).ToList();
                var countries = w.CountryList.Where(c => Names(sentence, c.Id, c.Name)).Select(c => c.Id).ToList();
                if (provinces.Count > 0)
                {
                    foreach (string p in provinces)
                    {
                        var at = all.Where(h => h.Province == p && (h.Kind == "capture" || h.Kind == "failed_attack" || h.Kind == "revolt")).ToList();
                        if (at.Count == 0) return $"invented: no fight or capture at {p} yet";
                        string stranger = countries.FirstOrDefault(c => !at.Any(h => h.Countries.Contains(c)) && !hit.Contains(c));
                        if (stranger != null) return $"invented: {stranger} never fought at {p}";
                        real = real ?? $"real: {at[at.Count - 1].Text}, turn {at[at.Count - 1].Turn}";
                    }
                }
                else if (countries.Count >= 2)
                {
                    var fought = all.FirstOrDefault(h => (h.Kind == "capture" || h.Kind == "failed_attack" || h.Kind == "war")
                                                         && countries.Count(c => h.Countries.Contains(c)) >= 2);
                    if (fought == null) return $"invented: {countries[0]} and {countries[1]} never fought";
                    real = real ?? $"real: {fought.Text}, turn {fought.Turn}";
                }
            }
            if (real != null) return real;
            foreach (var h in recent)
            {
                string named = h.Countries.FirstOrDefault(c => !hit.Contains(c) && Names(story, c, w.Country(c)?.Name))
                               ?? (h.Province != null && Names(story, h.Province, w.Province(h.Province)?.Name) && !hit.Contains(w.Province(h.Province)?.Owner ?? "") ? h.Province : null);
                if (named != null) return $"real: names {named} ({h.Text}, turn {h.Turn})";
            }
            return null;
        }

        /// <summary>Every country and province any happening of the game names: names a game-aware story may use.</summary>
        public static HashSet<string> Named(IReadOnlyList<Happening> all)
        {
            var set = new HashSet<string>(StringComparer.Ordinal);
            foreach (var h in all)
            {
                foreach (string c in h.Countries) set.Add(c);
                if (h.Province != null) set.Add(h.Province);
            }
            return set;
        }
    }
}

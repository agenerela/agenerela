using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace GreyBoxStrategy
{
    /// <summary>What kind of thing an event template takes as its target.</summary>
    [Flags]
    public enum EventTargetKind
    {
        None = 0,
        Country = 1,
        Group = 2,
        Province = 4,
        /// <summary>An open chain (S2): an effect on it strikes the chain's subject.</summary>
        Tale = 8,
    }

    /// <summary>
    /// One effect the game can apply, which the storyteller picks by id (an action of its agent).
    /// The game, never the model, decides how strong it is (<see cref="EventRules.Plan"/>) and
    /// writes the lines the rulers read.
    /// </summary>
    public sealed class EventTemplate
    {
        public EventTemplate(string id, bool bonus, EventTargetKind takes, string description, string fallbackTitle,
            string fallbackStory, string newsVerb, params string[] keywords)
        {
            Id = id;
            Bonus = bonus;
            Takes = takes;
            Description = description;
            FallbackTitle = fallbackTitle;
            FallbackStory = fallbackStory;
            NewsVerb = newsVerb;
            Keywords = keywords;
        }

        public string Id { get; }
        /// <summary>True for a gift, false for a blow: which side of the fairness budget it draws on.</summary>
        public bool Bonus { get; }
        public EventTargetKind Takes { get; }
        /// <summary>What the model reads about it: the action's description.</summary>
        public string Description { get; }
        /// <summary>The title when the model gave none, or its story was replaced.</summary>
        public string FallbackTitle { get; }
        /// <summary>The story when the model's was replaced; <c>{who}</c> is the countries hit, <c>{where}</c> the province.</summary>
        public string FallbackStory { get; }
        /// <summary>The news other rulers hear, after the names: "received a windfall of gold".</summary>
        public string NewsVerb { get; }
        /// <summary>Words that name this kind of event in a ruler's statement, for the scorecard.</summary>
        public IReadOnlyList<string> Keywords { get; }

        // --- Stage S2 (all null or false on S1's eight) ---

        /// <summary>Which S2 piece registers it; S1 for the first eight.</summary>
        public EventFeature Feature { get; set; } = EventFeature.S1;
        /// <summary>The effect as data, for every template after S1's eight (which keep their own code).</summary>
        public IReadOnlyList<EffectPart> Parts { get; set; }
        /// <summary>A dilemma's options; null for an event that asks nothing.</summary>
        public IReadOnlyList<DilemmaOption> Options { get; set; }
        /// <summary>The option an unanswered choice, or a scripted seat, takes.</summary>
        public string DefaultOption { get; set; }
        /// <summary>Neither a gift nor a blow by itself: a dilemma, a story-only event, a chain's start or end.</summary>
        public bool Neutral { get; set; }
        /// <summary>A province target must have this trait.</summary>
        public Func<ProvinceTraits, bool> Needs { get; set; }
        /// <summary>Only on a tale (end_tale).</summary>
        public bool TaleOnly { get; set; }
        /// <summary>For begin_&lt;skeleton&gt;: the skeleton it starts.</summary>
        public string Skeleton { get; set; }
        /// <summary>Not again within this many of the storyteller's events; 0 is <see cref="GameRules.EventTemplateCooldown"/>.</summary>
        public int Cooldown { get; set; }

        public bool IsChoice => Options != null;

        public override string ToString() => Id;
    }

    /// <summary>A decision the storyteller made, handed to <see cref="Game.QueueEvent"/>: the template, the target, and its title and story as one statement.</summary>
    public sealed class StoryEvent
    {
        public StoryEvent(string template, string target, string statement, string title = null)
        {
            Template = template;
            Target = target;
            Statement = statement;
            Title = title;
        }

        /// <summary>The title from a second call (<c>+title-call</c>), or null when the statement carries it.</summary>
        public string Title { get; }

        public string Template { get; }
        public string Target { get; }
        /// <summary>The model's statement: "A title: one or two sentences."</summary>
        public string Statement { get; }

        /// <summary>
        /// Why the storyteller's history guard replaced the story before it was queued ("invented: ..."), or
        /// null when it did not (#292). <see cref="Statement"/> is then the guard's line, and the event's verdict
        /// is "replaced: " and this.
        /// </summary>
        public string HistoryReplaced { get; set; }
        /// <summary>The model's own statement when a guard replaced it, for the record; null when none did.</summary>
        public string ModelStatement { get; set; }
    }

    /// <summary>One event that happened: what the game applied, to whom, and the story told about it.</summary>
    public sealed class EventRecord
    {
        public const string FromStoryteller = "storyteller";
        public const string FromTable = "table";

        public int Turn;
        /// <summary><see cref="FromStoryteller"/> or <see cref="FromTable"/> (the random event table).</summary>
        public string Source;
        public string Template;
        /// <summary>The target as chosen: a country, a group or a province id; for a table event, the country.</summary>
        public string Target;
        /// <summary>For a province target, the province; otherwise null.</summary>
        public string Province;
        /// <summary>The countries the effect reached, in seat order.</summary>
        public List<string> Hit = new List<string>();
        /// <summary>Per country hit, what the effect was worth in gold (soldiers at their price, stability at 20): the fairness budget's unit.</summary>
        public Dictionary<string, int> Value = new Dictionary<string, int>(StringComparer.Ordinal);
        /// <summary>Per country hit, the game's own words for what happened to it ("40 gold", "12 soldiers").</summary>
        public Dictionary<string, string> Effect = new Dictionary<string, string>(StringComparer.Ordinal);
        /// <summary>Provinces of every living country when the event happened: its standing.</summary>
        public Dictionary<string, int> ProvincesAtTime = new Dictionary<string, int>(StringComparer.Ordinal);
        /// <summary>The title and story as told to the rulers and the log, after the story check; null for a table event.</summary>
        public string Title;
        public string Story;
        /// <summary>The model's statement as it came, before the story check.</summary>
        public string Statement;
        /// <summary>What the story check did: <c>kept</c>, <c>no title</c>, or <c>replaced: ...</c> with the reason.</summary>
        public string StoryVerdict;

        // --- Stage S2 ---

        /// <summary>The storyteller's action when it was not the effect itself (begin_&lt;skeleton&gt;); else the template.</summary>
        public string Action;
        /// <summary>The title the model wrote (before a colon, or from the title call); null when it wrote none.</summary>
        public string ModelTitle;
        /// <summary>For a dilemma: the option each country hit took.</summary>
        public Dictionary<string, string> Choices = new Dictionary<string, string>(StringComparer.Ordinal);
        /// <summary>For a dilemma: who chose for each country (model, seat, person, default).</summary>
        public Dictionary<string, string> ChosenBy = new Dictionary<string, string>(StringComparer.Ordinal);
        /// <summary>For a dilemma: the options offered, by id.</summary>
        public List<string> Options = new List<string>();
        /// <summary>Members of a group the fairness rules left out (<c>+multi</c>).</summary>
        public List<string> Spared = new List<string>();
        public string ChainId;
        /// <summary>Its place in the chain, from 1.</summary>
        public int ChainStage;
        /// <summary>The province's traits when the event names one and traits are on.</summary>
        public string Traits;
        /// <summary>What the history check found: "real: ...", "invented: ...", or null.</summary>
        public string HistoryRef;

        public bool StoryReplaced => StoryVerdict != null && StoryVerdict.StartsWith("replaced", StringComparison.Ordinal);
        public int TotalValue => Value.Values.Sum();

        public override string ToString() => $"turn {Turn}: {Template} on {Target}" + (Title != null ? $" \"{Title}\"" : "");
    }

    /// <summary>Every event of a game, for the fairness budget, the cooldowns and the "told so far" line.</summary>
    public sealed class EventLedger
    {
        public List<EventRecord> Events { get; } = new List<EventRecord>();

        public IEnumerable<EventRecord> Told => Events.Where(e => e.Source == EventRecord.FromStoryteller);

        /// <summary>Every chain of the game (S2), in the order they began.</summary>
        public List<Chain> Chains { get; } = new List<Chain>();

        public Chain Chain(string id) => id == null ? null : Chains.FirstOrDefault(c => c.Id == id);

        /// <summary>What each turn was dealt (S2), fixed the first time it is asked, so the storyteller's offer and the upkeep's re-check agree.</summary>
        public Dictionary<int, TurnKind> TurnKinds { get; } = new Dictionary<int, TurnKind>();

        /// <summary>What events were worth to a country over the <paramref name="window"/> turns before <paramref name="turn"/> (turns turn − window to turn − 1).</summary>
        public int NetValue(string country, int turn, int window) =>
            Events.Where(e => e.Turn >= turn - window && e.Turn < turn && e.Value.ContainsKey(country)).Sum(e => e.Value[country]);

        /// <summary>The storyteller's last <paramref name="n"/> events, latest last.</summary>
        public List<EventRecord> LastTold(int n)
        {
            var told = Told.ToList();
            return told.Skip(Math.Max(0, told.Count - n)).ToList();
        }
    }

    /// <summary>
    /// The groups of countries a storyteller may target as one: each a target of its own, since a
    /// decision has one target (the research page's gap 1). Members are the living countries the
    /// group means now.
    /// </summary>
    public static class EventGroups
    {
        public const string Everyone = "everyone";
        public const string North = "the_north";
        public const string South = "the_south";
        public const string Leader = "the_leader";
        public const string Weakest = "the_weakest";
        public const string AtWar = "all_at_war";

        public static readonly IReadOnlyList<string> All = new[] { Everyone, North, South, Leader, Weakest, AtWar };

        public static bool IsGroup(string id) => All.Contains(id);

        public static List<string> Members(World w, string group)
        {
            var alive = w.Alive.ToList();
            switch (group)
            {
                case Everyone: return alive.Select(c => c.Id).ToList();
                case North: return alive.Where(c => c.Id == "valen" || c.Id == "orsk").Select(c => c.Id).ToList();
                case South: return alive.Where(c => c.Id == "kesh" || c.Id == "ilvar").Select(c => c.Id).ToList();
                case Leader:
                case Weakest:
                {
                    if (alive.Count < 2) return new List<string>();
                    var counts = alive.Select(c => (c.Id, n: w.ProvinceCount(c.Id))).ToList();
                    int edge = group == Leader ? counts.Max(x => x.n) : counts.Min(x => x.n);
                    var at = counts.Where(x => x.n == edge).ToList();
                    return at.Count == 1 ? new List<string> { at[0].Id } : new List<string>();
                }
                case AtWar: return alive.Where(c => w.EnemiesOf(c.Id).Any()).Select(c => c.Id).ToList();
                default: return new List<string>();
            }
        }

        /// <summary>The group's line in the storyteller's target list.</summary>
        public static string Describe(World w, string group)
        {
            var m = Members(w, group);
            string who = m.Count == 0 ? "nobody now" : string.Join(", ", m);
            switch (group)
            {
                case Everyone: return $"every living country: {who}";
                case North: return $"the north: {who}";
                case South: return $"the south: {who}";
                case Leader: return m.Count == 0 ? "the country with the most provinces: nobody leads alone now" : $"the country with the most provinces: {who}";
                case Weakest: return m.Count == 0 ? "the country with the fewest provinces: no single one now" : $"the country with the fewest provinces: {who}";
                case AtWar: return $"every country at war: {who}";
                default: return group;
            }
        }
    }

    /// <summary>
    /// The storyteller's templates, and the game's rules for them: who may be struck or helped now
    /// (<see cref="WhyNot"/>, the fairness budget and the cooldowns as availability), how strong an
    /// event is (<see cref="Plan"/>), what the rulers read, and the story check.
    /// </summary>
    public static class EventRules
    {
        public const string Windfall = "windfall";
        public const string BadHarvest = "bad_harvest";
        public const string Plague = "plague";
        public const string Volunteers = "volunteers";
        public const string Zeal = "zeal";
        public const string Unrest = "unrest";
        public const string Stronghold = "stronghold";
        public const string WallsCrumble = "walls_crumble";

        // The random event table's kinds, recorded in the same ledger so the control is scored alike.
        public const string GoodHarvest = "good_harvest";
        public const string PoorHarvest = "poor_harvest";
        public const string Bandits = "bandits";

        /// <summary>What a point of stability is worth in the budget's gold.</summary>
        public const int StabilityValue = 20;
        /// <summary>Stories longer than this are replaced.</summary>
        public const int MaxStoryWords = 70;

        public static readonly IReadOnlyList<EventTemplate> Templates = new[]
        {
            new EventTemplate(Windfall, true, EventTargetKind.Country | EventTargetKind.Group,
                "Good fortune: the target gains gold, about a turn's income.", "A Windfall", "Fortune smiled on {who}.",
                "received a windfall of gold", "windfall", "fortune", "treasure", "riches"),
            new EventTemplate(BadHarvest, false, EventTargetKind.Country | EventTargetKind.Group,
                "A bad harvest: the target's income is halved this turn.", "A Bad Harvest", "The fields of {who} failed.",
                "had a bad harvest", "harvest", "famine", "crops", "blight", "drought", "fields"),
            new EventTemplate(Plague, false, EventTargetKind.Country | EventTargetKind.Group,
                "A sickness: the target loses about a sixth of its army.", "The Sickness", "A sickness ran through the camps of {who}.",
                "lost soldiers to sickness", "plague", "sickness", "disease", "fever", "pestilence", "cough", "sick"),
            new EventTemplate(Volunteers, true, EventTargetKind.Country,
                "Volunteers: soldiers join the target's army for free, about one levy.", "Volunteers", "Volunteers flocked to the banners of {who}.",
                "gained volunteers", "volunteers", "volunteer", "recruits"),
            new EventTemplate(Zeal, true, EventTargetKind.Country | EventTargetKind.Group,
                "Zeal: the target's people rally; +1 stability.", "A Wave of Zeal", "A fervour swept the people of {who}.",
                "grew steadier with zeal", "zeal", "fervour", "fervor", "faith", "rally"),
            new EventTemplate(Unrest, false, EventTargetKind.Country,
                "Unrest: the target's people grumble; -1 stability.", "Unrest", "Grumbling spread through the towns of {who}.",
                "lost stability to unrest", "unrest", "riot", "riots", "discontent", "grumbling", "uprising"),
            new EventTemplate(Stronghold, true, EventTargetKind.Province,
                "A stronghold: a province's walls are raised; +1 fort there.", "Walls Raised", "New walls rose at {where}.",
                "raised a fort", "walls", "wall", "fort", "stronghold", "fortress"),
            new EventTemplate(WallsCrumble, false, EventTargetKind.Province,
                "Crumbling walls: a province's fort falls; -1 fort there.", "Walls Crumble", "The walls of {where} crumbled.",
                "lost a fort", "walls", "wall", "fort", "crumble", "crumbled", "collapse"),
        };

        // --- Stage S2's templates -----------------------------------------------------------------

        public const string Omen = "omen";
        public const string EndTale = "end_tale";

        private static EventTemplate Dilemma(string id, EventTargetKind takes, string description, string title, string story, string news,
            string[] keywords, string defaultOption, EventFeature feature, params DilemmaOption[] options) =>
            new EventTemplate(id, false, takes, description, title, story, news, keywords)
            { Feature = feature, Options = options, DefaultOption = defaultOption, Neutral = true };

        private static EventTemplate Effect(string id, bool bonus, EventTargetKind takes, string description, string title, string story,
            string news, string[] keywords, EventFeature feature, Func<ProvinceTraits, bool> needs, params EffectPart[] parts) =>
            new EventTemplate(id, bonus, takes, description, title, story, news, keywords) { Feature = feature, Parts = parts, Needs = needs };

        private const EventTargetKind CountryOrGroup = EventTargetKind.Country | EventTargetKind.Group;

        /// <summary>Every template after S1's eight: dilemmas, trait events, the omen, and the chain actions. <see cref="StoryRules.Offers"/> says which a game uses.</summary>
        public static readonly IReadOnlyList<EventTemplate> S2Templates = new List<EventTemplate>
        {
            Dilemma("sellswords", CountryOrGroup, "A dilemma: sellswords offer their swords; the ruler hires them for gold or sends them away.",
                "The Sellswords", "Sellswords came to {who} offering their swords.", "were offered sellswords",
                new[] { "sellswords", "mercenaries", "sellsword", "hire" }, "send_sellswords_away", EventFeature.Choice,
                new DilemmaOption("hire_sellswords", "hire them", "war", EffectPart.PayGold(), EffectPart.GainSoldiers()),
                new DilemmaOption("send_sellswords_away", "send them away", "thrift")),
            Dilemma("petitioners", EventTargetKind.Province, "A dilemma: petitioners from a province beg for relief; the ruler grants it for gold or refuses and risks their anger.",
                "The Petitioners", "Petitioners from {where} came begging for relief.", "heard petitioners",
                new[] { "petition", "petitioners", "relief", "beg" }, "refuse_relief", EventFeature.Choice,
                new DilemmaOption("grant_relief", "grant the relief", "order", EffectPart.PayGold(true), EffectPart.Steadier),
                new DilemmaOption("refuse_relief", "refuse them", "thrift", EffectPart.Shakier)),
            Dilemma("relic", EventTargetKind.Province, "A dilemma: a relic is unearthed in a province; the ruler builds it a shrine, sells it, or buries it again.",
                "The Relic", "A relic was unearthed at {where}.", "unearthed a relic",
                new[] { "relic", "shrine", "saint", "bones" }, "rebury_relic", EventFeature.Choice,
                new DilemmaOption("build_shrine", "build it a shrine", "faith", EffectPart.PayGold(true), EffectPart.Steadier),
                new DilemmaOption("sell_relic", "sell it", "gold", EffectPart.GainGold(), EffectPart.Shakier),
                new DilemmaOption("rebury_relic", "bury it again", "thrift")),
            Dilemma("muster", CountryOrGroup, "A dilemma: the harvest is ripe as the muster is called; the ruler calls the men to arms or brings in the harvest.",
                "The Muster", "The harvest in {who} ripened on the day of the muster.", "faced the harvest or the muster",
                new[] { "muster", "harvest", "arms", "levy" }, "bring_in_harvest", EventFeature.Choice,
                new DilemmaOption("call_the_muster", "call the men to arms", "war", EffectPart.GainSoldiers(), EffectPart.CutIncome),
                new DilemmaOption("bring_in_harvest", "bring in the harvest", "thrift", EffectPart.GainGold(true))),
            Dilemma("engineers", EventTargetKind.Province, "A dilemma: engineers offer to raise walls in a province for gold.",
                "The Engineers", "Engineers came to {where} offering to raise its walls.", "were offered new walls",
                new[] { "engineers", "walls", "masons", "fort" }, "keep_the_coin", EventFeature.Choice,
                new DilemmaOption("raise_walls", "raise the walls", "order", EffectPart.PayGold(), EffectPart.RaiseFort),
                new DilemmaOption("keep_the_coin", "keep the coin", "thrift")),
            Dilemma("fever_camps", CountryOrGroup, "A dilemma: a fever breaks out in the camps; the ruler pays for a quarantine or marches on and loses men.",
                "Fever in the Camps", "A fever broke out in the camps of {who}.", "had a fever in the camps",
                new[] { "fever", "quarantine", "camps", "sickness" }, "march_on", EventFeature.Choice,
                new DilemmaOption("quarantine_camps", "pay for a quarantine", "order", EffectPart.PayGold(true)),
                new DilemmaOption("march_on", "march on", "war", EffectPart.LoseSoldiers())),

            Dilemma("pretender", EventTargetKind.Country, "A dilemma within a tale: a pretender claims the crown; the ruler pays him to go or defies him.",
                "The Pretender", "A pretender to the crown of {who} came forward.", "faced a pretender",
                new[] { "pretender", "claimant", "crown", "usurper" }, "defy_pretender", EventFeature.ChainDilemma,
                new DilemmaOption("pay_pretender", "pay him to go", "gold", EffectPart.PayGold()),
                new DilemmaOption("defy_pretender", "defy him", "war")),
            Dilemma("pretender_rising", EventTargetKind.Country, "A dilemma within a tale: the pretender's men rise; the ruler crushes them or bargains.",
                "The Rising", "The pretender's men rose in {who}.", "faced a rising",
                new[] { "rising", "rebels", "pretender", "revolt" }, "crush_rising", EventFeature.ChainDilemma,
                new DilemmaOption("crush_rising", "crush the rising", "war", EffectPart.LoseSoldiers(true)),
                new DilemmaOption("bargain_rising", "bargain with the rebels", "gold", EffectPart.PayGold(true))),
            Dilemma("deep_vein", EventTargetKind.Province, "A dilemma within a tale: the miners find the vein runs deep; the ruler digs deeper or seals the mine.",
                "The Deep Vein", "The miners of {where} found the vein running deep.", "found a deep vein",
                new[] { "vein", "mine", "miners", "ore" }, "seal_the_mine", EventFeature.ChainDilemma,
                new DilemmaOption("dig_deeper", "dig deeper", "gold", EffectPart.GainGold()),
                new DilemmaOption("seal_the_mine", "seal the mine", "thrift")),
            Dilemma("ration", CountryOrGroup, "A dilemma within a tale: the winter stores run low; the ruler opens the granaries or hoards the grain.",
                "The Granaries", "The winter stores of {who} ran low.", "rationed their grain",
                new[] { "granaries", "grain", "winter", "stores", "ration" }, "hoard_grain", EventFeature.ChainDilemma,
                new DilemmaOption("open_granaries", "open the granaries", "order", EffectPart.PayGold(true)),
                new DilemmaOption("hoard_grain", "hoard the grain", "thrift", EffectPart.Shakier)),

            Effect("rich_vein", true, EventTargetKind.Province, "A rich vein: ore is struck in a province that has a mine; its owner gains gold, about a turn's income.",
                "A Rich Vein", "Miners struck ore at {where}.", "struck ore", new[] { "vein", "ore", "mine", "miners" },
                EventFeature.Trait, t => t.Mined, EffectPart.GainGold()),
            Effect("flood", false, EventTargetKind.Province, "A flood in a province by water (a river valley, a marsh or a coast): its owner's income is halved this turn.",
                "The Flood", "The waters rose at {where}.", "suffered a flood", new[] { "flood", "waters", "river", "drowned" },
                EventFeature.Trait, t => t.Wet, EffectPart.CutIncome),
            Effect("pilgrims", true, EventTargetKind.Province, "Pilgrims come to a province with a holy place; its owner gains +1 stability.",
                "The Pilgrims", "Pilgrims came to {where}.", "welcomed pilgrims", new[] { "pilgrims", "pilgrim", "shrine", "holy" },
                EventFeature.Trait, t => t.Holy, EffectPart.Steadier),

            new EventTemplate(Omen, false, CountryOrGroup | EventTargetKind.Province, "An omen: a story only, with no effect: a sign, a rumour, a marvel.",
                "An Omen", "Strange signs were seen over {who}.", "saw an omen", "omen", "sign", "portent")
                { Feature = EventFeature.StoryOnly, Parts = new EffectPart[0], Neutral = true, Cooldown = 8 },
            new EventTemplate(EndTale, false, EventTargetKind.Tale, "End a tale: its last event, a story only, with no effect.",
                "The End of the Tale", "So ended the tale of {who}.", "saw a tale end", "end", "ended")
                { Feature = EventFeature.FreeChain, Parts = new EffectPart[0], Neutral = true, TaleOnly = true },
        }.Concat(ChainSkeletons.All.Select(k => new EventTemplate(k.BeginAction, false, k.Takes, k.Description,
            "A Tale Begins", "A tale began in {who}.", "saw a tale begin") { Feature = EventFeature.SkeletonChain, Neutral = true, Skeleton = k.Id }))
          .ToList();

        public static IEnumerable<EventTemplate> AllTemplates => Templates.Concat(S2Templates);

        /// <summary>The templates a game with these rules offers: S1's eight, and S2's that are switched on.</summary>
        public static IEnumerable<EventTemplate> TemplatesFor(StoryRules rules) =>
            rules == null ? Templates : AllTemplates.Where(rules.Offers);

        /// <summary>The random table's kinds, for <see cref="KeywordsFor"/>.</summary>
        private static readonly Dictionary<string, string[]> TableKeywords = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            [GoodHarvest] = new[] { "harvest", "crops", "fields" },
            [PoorHarvest] = new[] { "harvest", "famine", "crops", "fields" },
            [Bandits] = new[] { "bandits", "bandit", "brigands" },
            [Unrest] = new[] { "unrest", "riot", "discontent" },
        };

        public static EventTemplate Template(string id) => AllTemplates.FirstOrDefault(t => t.Id == id);

        public static IReadOnlyList<string> KeywordsFor(string template) =>
            Template(template)?.Keywords ?? (TableKeywords.TryGetValue(template ?? "", out var k) ? k : Array.Empty<string>());

        /// <summary>Every id a storyteller can name: the living countries, the groups, the provinces.</summary>
        public static IEnumerable<string> TargetIds(World w) =>
            w.Alive.Select(c => c.Id).Concat(EventGroups.All).Concat(w.ProvinceList.Select(p => p.Id));

        /// <summary>With chains, the open tales too.</summary>
        public static IEnumerable<string> TargetIds(World w, EventLedger ledger) =>
            TargetIds(w).Concat(ledger.Chains.Where(c => c.IsOpenTarget).Select(c => c.Id));

        /// <summary>A target's kind, tales included.</summary>
        public static EventTargetKind KindOf(World w, EventLedger ledger, string id) =>
            ledger != null && ledger.Chain(id) != null ? EventTargetKind.Tale : KindOf(w, id);

        public static EventTargetKind KindOf(World w, string id) =>
            w.IsCountry(id) ? EventTargetKind.Country
            : EventGroups.IsGroup(id) ? EventTargetKind.Group
            : w.IsProvince(id) ? EventTargetKind.Province
            : EventTargetKind.None;

        /// <summary>The countries an event on this target would reach now.</summary>
        public static List<string> Reach(World w, string target)
        {
            switch (KindOf(w, target))
            {
                case EventTargetKind.Country: return w.Country(target).Alive ? new List<string> { target } : new List<string>();
                case EventTargetKind.Group: return EventGroups.Members(w, target);
                case EventTargetKind.Province:
                    string owner = w.Province(target).Owner;
                    return owner != null && w.Country(owner).Alive ? new List<string> { owner } : new List<string>();
                default: return new List<string>();
            }
        }

        /// <summary>The target's line in the storyteller's list: a country's standing, a group's members, a province's owner and forts.</summary>
        public static string Describe(World w, string id)
        {
            switch (KindOf(w, id))
            {
                case EventTargetKind.Country:
                {
                    var c = w.Country(id);
                    return $"a country: {Facts.Provinces(w.ProvinceCount(id))}, army {c.Army}, {c.Gold} gold, stability {c.Stability} of {w.Rules.MaxStability}";
                }
                case EventTargetKind.Group: return EventGroups.Describe(w, id);
                case EventTargetKind.Province:
                {
                    var p = w.Province(id);
                    string forts = p.Forts == 0 ? "no fort" : p.Forts == 1 ? "1 fort" : $"{p.Forts} forts";
                    return p.Owner == null ? $"a neutral province, {forts}" : $"{p.Owner}'s province, {forts}{(p.IsCapital ? ", its capital" : "")}";
                }
                default: return id;
            }
        }

        // --- Who may be struck or helped -------------------------------------------------------

        /// <summary>
        /// Why <paramref name="template"/> on <paramref name="target"/> may not happen on
        /// <paramref name="turn"/>, or null when it may. Every fairness rule is here, so the list the
        /// storyteller is offered and the re-check when the event lands are the same rule.
        /// </summary>
        public static string WhyNot(World w, EventLedger ledger, int turn, string template, string target) =>
            WhyNot(w, ledger, turn, template, target, null);

        /// <summary>
        /// <see cref="WhyNot(World, EventLedger, int, string, string)"/> under a game's
        /// <see cref="StoryRules"/>; null rules are S1's exactly. S2 adds the dilemmas (open on a turn
        /// by the preset's chance, every option payable), the trait events, the omen, tales as targets
        /// (free or on skeletons), group events that spare the members the rules protect (<c>+multi</c>),
        /// and the preset's lean.
        /// </summary>
        public static string WhyNot(World w, EventLedger ledger, int turn, string template, string target, StoryRules rules) =>
            WhyNotAs(w, ledger, turn, template, target, rules, rules == null || !rules.IsS2 ? TurnKind.Ordinary : KindOfTurn(w, ledger, turn, rules));

        /// <summary>
        /// What <paramref name="turn"/> is for: a tale's when the preset's chain chance says so and a
        /// tale can begin, else a dilemma's when its chance says so and one is open, else ordinary.
        /// Fixed the first time it is asked (<see cref="EventLedger.TurnKinds"/>).
        /// </summary>
        public static TurnKind KindOfTurn(World w, EventLedger ledger, int turn, StoryRules rules)
        {
            if (rules == null || !rules.IsS2) return TurnKind.Ordinary;
            if (ledger.TurnKinds.TryGetValue(turn, out var kind)) return kind;
            kind = TurnKind.Ordinary;
            var ids = TargetIds(w, ledger).ToList();
            bool AnyOpen(TurnKind k, Func<EventTemplate, bool> which) =>
                TemplatesFor(rules).Where(which).Any(t => ids.Any(id => WhyNotAs(w, ledger, turn, t.Id, id, rules, k) == null && StartsTale(ledger, t, id) == (k == TurnKind.Tale)));
            if (rules.Chains != ChainMode.None && Dice.Unit(w.Seed, turn, "storyteller", "chain-open") < rules.Settings.ChainChance
                && AnyOpen(TurnKind.Tale, t => true))
                kind = TurnKind.Tale;
            else if (rules.Choices && Dice.Unit(w.Seed, turn, "storyteller", "choice-open") < rules.Settings.ChoiceChance
                     && AnyOpen(TurnKind.Dilemma, t => t.IsChoice))
                kind = TurnKind.Dilemma;
            ledger.TurnKinds[turn] = kind;
            return kind;
        }

        // Whether this begins a tale: a skeleton's beginning, or a free tale's second event.
        private static bool StartsTale(EventLedger ledger, EventTemplate t, string target) =>
            t.Skeleton != null || ledger.Chain(target)?.Status == ChainStatus.Single;

        private static string WhyNotAs(World w, EventLedger ledger, int turn, string template, string target, StoryRules rules, TurnKind turnKind)
        {
            var t = Template(template);
            if (t == null || (rules == null ? t.Feature != EventFeature.S1 : !rules.Offers(t))) return $"{template} is not an event";
            var r = w.Rules;
            if (turn < r.EventFirstTurn) return $"no event before turn {r.EventFirstTurn}";
            var kind = KindOf(w, ledger, target);
            if (kind == EventTargetKind.None) return $"{target} is not a country, a group or a province";
            if (kind == EventTargetKind.Tale) return WhyNotOnTale(w, ledger, turn, t, ledger.Chain(target), rules, turnKind);
            if (t.TaleOnly) return $"{template} takes a tale";
            if (t.Feature == EventFeature.ChainDilemma) return $"{template} happens only within a tale";
            if ((t.Takes & kind) == 0) return $"{template} does not take a {kind.ToString().ToLowerInvariant()}";
            if (t.Skeleton != null) return turnKind != TurnKind.Tale ? "no new tale this turn" : WhyNotBegin(w, ledger, turn, t, target, kind, rules);
            if (t.IsChoice && turnKind != TurnKind.Dilemma) return "no dilemma this turn";
            if (!t.IsChoice && turnKind != TurnKind.Ordinary)
                return turnKind == TurnKind.Tale ? "this turn is for a tale" : "this turn is for a dilemma";
            return WhyNotOn(w, ledger, turn, t, target, kind, rules, onTale: false, skeleton: false);
        }

        // The S1 rules and S2's additions for one template on a country, a group or a province.
        private static string WhyNotOn(World w, EventLedger ledger, int turn, EventTemplate t, string target, EventTargetKind kind,
            StoryRules rules, bool onTale, bool skeleton, IReadOnlyList<string> members = null)
        {
            var r = w.Rules;
            string template = t.Id;
            // Cooldowns: a template not again within the last few events, a target not twice in a row.
            var recent = ledger.LastTold(r.EventTemplateCooldown);
            int cooldown = t.Cooldown > 0 ? t.Cooldown : r.EventTemplateCooldown;
            if (!skeleton && ledger.LastTold(cooldown).Any(e => e.Template == template)) return $"{template} happened within the last {cooldown} events";
            var last = recent.LastOrDefault();
            int same = r.EventMaxSameKindInARow;
            // Neutral events (dilemmas, omens) neither make nor break a run of gifts or blows; S1 has none.
            var streak = ledger.Told.Where(e => Template(e.Template)?.Neutral != true).ToList();
            streak = streak.Skip(Math.Max(0, streak.Count - same)).ToList();
            if (!t.Neutral && same > 0 && streak.Count == same && streak.All(e => Template(e.Template)?.Bonus == t.Bonus))
                return t.Bonus ? $"{same} gifts in a row: a blow is due" : $"{same} blows in a row: a gift is due";
            if (!onTale && last != null && last.Target == target) return $"the last event struck {target}";

            string province = null;
            if (kind == EventTargetKind.Province)
            {
                var p = w.Province(target);
                province = p.Id;
                if (p.Owner == null) return $"{target} is neutral";
                if (template == Stronghold && p.Forts >= r.MaxFortsPerProvince) return $"{target} already has {p.Forts} forts";
                if (template == WallsCrumble && p.Forts == 0) return $"{target} has no fort";
                if (template == WallsCrumble && p.IsCapital) return $"{target} is a capital";
                if (rules != null && rules.Traits && t.Needs != null && !t.Needs(w.Traits[p.Id]))
                    return $"{target} has no place for {template} ({w.Traits[p.Id].Line})";
            }

            var reach = members?.ToList() ?? Reach(w, target);
            if (reach.Count == 0) return $"{target} reaches no living country now";
            bool single = target == EventGroups.Leader || target == EventGroups.Weakest;
            if (kind == EventTargetKind.Group && reach.Count < 2 && !single)
                return $"{target} is only {reach[0]} now";
            if (!onTale && kind != EventTargetKind.Group && last != null && last.Hit.Contains(reach[0]))
                return $"{reach[0]} was struck by the last event";
            if (kind == EventTargetKind.Group && rules != null && rules.Multi)
            {
                // +multi: the event reaches the members the rules allow, and is open when two are (one for the leader or the weakest).
                var allowed = reach.Where(c => WhyNotFor(w, ledger, turn, t, c, rules, province) == null).ToList();
                if (allowed.Count >= (single ? 1 : 2)) return null;
                return WhyNotFor(w, ledger, turn, t, reach.First(c => !allowed.Contains(c)), rules, province);
            }
            foreach (string c in reach)
            {
                string why = WhyNotFor(w, ledger, turn, t, c, rules, province);
                if (why != null) return why;
            }
            return null;
        }

        // begin_<skeleton>: the skeleton's own rules, then its first stage on the target.
        private static string WhyNotBegin(World w, EventLedger ledger, int turn, EventTemplate t, string target, EventTargetKind kind, StoryRules rules)
        {
            var k = ChainSkeletons.Named(t.Skeleton);
            if (turn < k.FromTurn) return $"{t.Id} not before turn {k.FromTurn}";
            if (k.OncePerGame && ledger.Chains.Any(c => c.Skeleton == k.Id)) return $"{t.Id} happens once a game";
            if (ledger.Chains.Count(c => c.Status == ChainStatus.Open) >= ChainSkeletons.MaxOpen) return $"{ChainSkeletons.MaxOpen} tales are open already";
            if (ledger.Chains.Any(c => c.Skeleton == k.Id && c.Status == ChainStatus.Open)) return $"a tale of {k.Id} is open already";
            if (kind == EventTargetKind.Province && rules.Traits && k.Needs != null && !k.Needs(w.Traits[target]))
                return $"{target} has no place for {t.Id} ({w.Traits[target].Line})";
            string why = null;
            foreach (var stage in FirstStage(k, rules))
            {
                why = WhyNotOn(w, ledger, turn, stage, target, kind, rules, onTale: false, skeleton: false);
                if (why == null) return null;
            }
            return why ?? $"{t.Id} has no first event it can tell";
        }

        /// <summary>A skeleton's first-stage templates that these rules offer (its own dilemmas always).</summary>
        public static IEnumerable<EventTemplate> FirstStage(ChainSkeleton k, StoryRules rules) =>
            k.Stages[0].Templates.Select(Template).Where(x => x != null && (rules == null || rules.Offers(x)));

        /// <summary>The template a begin_&lt;skeleton&gt; tells now: its first stage's first template that is open; null when none is.</summary>
        public static EventTemplate BeginStage(World w, EventLedger ledger, int turn, EventTemplate begin, string target, StoryRules rules)
        {
            var k = ChainSkeletons.Named(begin.Skeleton);
            var kind = KindOf(w, target);
            return FirstStage(k, rules).FirstOrDefault(s => WhyNotOn(w, ledger, turn, s, target, kind, rules, false, false) == null);
        }

        // An effect on a tale: the chain's rules, then the effect on the chain's subject.
        private static string WhyNotOnTale(World w, EventLedger ledger, int turn, EventTemplate t, Chain c, StoryRules rules, TurnKind turnKind)
        {
            if (rules == null || rules.Chains == ChainMode.None) return "tales are off";
            if (!c.IsOpenTarget) return $"{c.Id} is over";
            if (t.Skeleton != null) return $"{t.Id} begins a tale; it does not continue one";
            if (c.Events.Count >= ChainSkeletons.MaxEvents) return $"{c.Id} has run its course";
            if (c.Events.Count + 1 >= ChainSkeletons.Long && ledger.Chains.Any(o => o != c && o.Events.Count >= ChainSkeletons.Long))
                return "only one long tale a game";
            if (c.LastTurn < turn - ChainSkeletons.IdleTurns) return $"{c.Id} is too old to continue";
            if (c.Status == ChainStatus.Single)
            {
                if (ledger.Chains.Count(o => o.Status == ChainStatus.Open) >= ChainSkeletons.MaxOpen) return $"{ChainSkeletons.MaxOpen} tales are open already";
                if (turnKind != TurnKind.Tale) return "no new tale this turn";
            }
            bool skeleton = c.Mode == ChainMode.Skeleton;
            if (skeleton)
            {
                var k = ChainSkeletons.Named(c.Skeleton);
                int next = ChainSkeletons.NextStage(k, c);
                if (next < 0) return $"{c.Id} is told";
                if (!k.Stages[next].Templates.Contains(t.Id)) return $"{c.Id}'s next event is {string.Join(" or ", k.Stages[next].Templates)}";
            }
            else if (t.TaleOnly) return c.Status == ChainStatus.Open ? null : $"{c.Id} is not a tale yet";
            var (subject, kind, members) = SubjectOf(w, c);
            if ((t.Takes & kind) == 0) return $"{t.Id} does not take {c.Id}'s {kind.ToString().ToLowerInvariant()}";
            if (members.Count == 0) return $"{c.Id}'s subject is out";
            return WhyNotOn(w, ledger, turn, t, subject, kind, rules, onTale: true, skeleton: skeleton, members: kind == EventTargetKind.Group ? members : null);
        }

        /// <summary>A tale's subject now: the target its effects strike, its kind, and the countries it reaches (for a group, its first members still alive).</summary>
        public static (string target, EventTargetKind kind, List<string> members) SubjectOf(World w, Chain c)
        {
            if (c.SubjectKind == EventTargetKind.Group)
                return (c.Subject, EventTargetKind.Group, c.Countries.Where(id => w.Country(id)?.Alive == true).ToList());
            return (c.Subject, c.SubjectKind, Reach(w, c.Subject));
        }

        /// <summary>
        /// The countries an event reaches under a game's rules, and the members of a group it spares:
        /// with <c>+multi</c>, those the fairness rules protect now; otherwise none (S1, where such a
        /// group is not open at all).
        /// </summary>
        public static (List<string> hit, List<string> spared) ReachFor(World w, EventLedger ledger, int turn, EventTemplate t, string target, StoryRules rules)
        {
            var c = ledger.Chain(target);
            List<string> reach;
            EventTargetKind kind;
            string province = null;
            if (c != null)
            {
                var s = SubjectOf(w, c);
                reach = s.members;
                kind = s.kind;
                if (kind == EventTargetKind.Province) province = s.target;
            }
            else
            {
                reach = Reach(w, target);
                kind = KindOf(w, target);
                if (kind == EventTargetKind.Province) province = target;
            }
            if (kind != EventTargetKind.Group || rules == null || !rules.Multi) return (reach, new List<string>());
            var hit = reach.Where(id => WhyNotFor(w, ledger, turn, t, id, rules, province) == null).ToList();
            return (hit, reach.Where(id => !hit.Contains(id)).ToList());
        }

        /// <summary>The per-country part of <see cref="WhyNot"/>: the budget, the weak and the leader, and what the effect needs.</summary>
        public static string WhyNotFor(World w, EventLedger ledger, int turn, EventTemplate t, string countryId) =>
            WhyNotFor(w, ledger, turn, t, countryId, null, null);

        /// <summary>
        /// The per-country rule under a game's rules: S1's budget and lean for a gift or a blow, the
        /// preset's lean (none, mild, strong), and for S2's data effects whether each part can apply. A
        /// dilemma is a gift when every option gains, a blow when every option costs, else neither; a
        /// story-only event touches nobody.
        /// </summary>
        public static string WhyNotFor(World w, EventLedger ledger, int turn, EventTemplate t, string countryId, StoryRules rules, string province)
        {
            var r = w.Rules;
            var c = w.Country(countryId);
            if (c == null || !c.Alive) return $"{countryId} is out of the game";
            int held = w.ProvinceCount(countryId);
            int net = ledger.NetValue(countryId, turn, r.EventBudgetWindow);
            bool? gift = t.Bonus;
            if (t.IsChoice)
            {
                foreach (var o in t.Options)
                    foreach (var part in o.Parts)
                    {
                        string why = EventEffects.WhyNot(w, part, countryId, province);
                        if (why != null) return why;
                    }
                var values = t.Options.Select(o => o.Parts.Sum(p => EventEffects.Plan(w, p, countryId).value)).ToList();
                gift = values.Min() > 0 ? true : values.Max() < 0 ? false : (bool?)null;
            }
            else if (t.Parts != null)
            {
                if (t.Parts.Count == 0) return null; // a story only
                foreach (var part in t.Parts)
                {
                    string why = EventEffects.WhyNot(w, part, countryId, province);
                    if (why != null) return why;
                }
            }
            var lean = rules?.Settings.Lean ?? StoryLean.Mild;
            double mean = w.Alive.Average(x => (double)w.ProvinceCount(x.Id));
            if (gift == true)
            {
                if (lean != StoryLean.None && held >= r.EventNoBonusFromProvinces) return $"{countryId} holds {held} provinces: no gift may crown the leader";
                if (lean == StoryLean.Strong && held > mean) return $"{countryId} holds more than most: the underdog's friend gives it nothing";
                if (net >= r.EventBudget) return $"{countryId} has had enough good fortune lately";
            }
            else if (gift == false)
            {
                if (held < r.EventMinProvincesForPenalty) return $"{countryId} holds only {Facts.Provinces(held)}: too weak to strike";
                if (lean == StoryLean.Strong && held < mean) return $"{countryId} holds less than most: the underdog's friend spares it";
                if (net <= -r.EventBudget) return $"{countryId} has suffered enough lately";
            }
            switch (t.Id)
            {
                case Plague: return c.Army < r.EventMinArmyForPlague ? $"{countryId}'s army is too small for a sickness" : null;
                case Zeal: return c.Stability >= r.MaxStability ? $"{countryId}'s stability is already {c.Stability}" : null;
                case Unrest: return c.Stability <= 2 ? $"{countryId}'s stability is already low" : null;
                default: return null;
            }
        }

        /// <summary>Every event open now, by template, for the storyteller's "Events open" line and the tests.</summary>
        public static List<(string template, string target)> Open(World w, EventLedger ledger, int turn) =>
            Templates.SelectMany(t => TargetIds(w).Where(id => WhyNot(w, ledger, turn, t.Id, id) == null).Select(id => (t.Id, id))).ToList();

        /// <summary>Every event open now under a game's rules, tales included; null rules, or S1's, are the S1 list.</summary>
        public static List<(string template, string target)> Open(World w, EventLedger ledger, int turn, StoryRules rules)
        {
            if (rules == null || !rules.IsS2) return Open(w, ledger, turn);
            var ids = TargetIds(w, ledger).ToList();
            return TemplatesFor(rules).SelectMany(t => ids.Where(id => WhyNot(w, ledger, turn, t.Id, id, rules) == null).Select(id => (t.Id, id))).ToList();
        }

        // --- How strong, and what it does --------------------------------------------------------

        /// <summary>What the effect is for one country now: the amount in its own unit (gold, soldiers, stability, forts) and its value in gold.</summary>
        public static (int amount, int value, string effect) Plan(World w, string template, string countryId)
        {
            var r = w.Rules;
            var c = w.Country(countryId);
            switch (template)
            {
                case Windfall:
                {
                    int gold = Math.Max(r.EventWindfallMin, Math.Min(r.EventWindfallMax, w.Income(c)));
                    return (gold, gold, $"{gold} gold");
                }
                case BadHarvest:
                {
                    int cut = w.Income(c) / 2;
                    return (cut, -cut, $"{cut} gold of income");
                }
                case Plague:
                {
                    int lost = Math.Min(c.Army, Math.Max(3, (int)Math.Round(c.Army * r.EventPlagueShare)));
                    return (lost, -lost * r.GoldPerSoldier, $"{lost} soldiers");
                }
                case Volunteers:
                {
                    int gain = Math.Min(r.EventVolunteersMax, r.LevyBase + 3 * w.ProvinceCount(countryId));
                    return (gain, gain * r.GoldPerSoldier, $"{gain} soldiers");
                }
                case Zeal: return (1, StabilityValue, "1 stability");
                case Unrest: return (1, -StabilityValue, "1 stability");
                case Stronghold: return (1, r.FortCost, "1 fort");
                case WallsCrumble: return (1, -r.FortCost, "1 fort");
                default: return (0, 0, "nothing");
            }
        }

        // --- The story -----------------------------------------------------------------------------

        private static readonly Regex Digits = new Regex(@"\d");

        /// <summary>
        /// The story check, in the game because a framework guard can only keep or contain the whole
        /// decision (the research page's gap 4): the title and story split at the first colon; a story
        /// that names a country the event did not reach, or a province of one, that holds a number, or
        /// that runs too long, is replaced by the template's own line, and the effect stands.
        /// </summary>
        public static (string title, string story, string verdict) CheckStory(World w, EventTemplate t, IReadOnlyList<string> hit,
            string province, string statement) => CheckStory(w, t, hit, province, statement, null, null, false);

        /// <summary>
        /// The S2 story check: with <paramref name="titleCalled"/>, the title comes from the title call
        /// (<paramref name="calledTitle"/>, null when it gave none) and the statement is the story;
        /// <paramref name="allowed"/> are names a game-aware story may use beside the countries hit.
        /// </summary>
        public static (string title, string story, string verdict) CheckStory(World w, EventTemplate t, IReadOnlyList<string> hit,
            string province, string statement, string calledTitle, ICollection<string> allowed, bool titleCalled)
        {
            string who = Join(hit);
            string fallbackStory = t.FallbackStory.Replace("{who}", who).Replace("{where}", province ?? who);
            string text = (statement ?? "").Trim();
            if (text.Length == 0) return (t.FallbackTitle, fallbackStory, "replaced: no story");

            var (title, story) = SplitTitle(text);
            if (titleCalled)
            {
                title = CleanTitle(calledTitle);
                if (story.Length == 0) story = text;
            }

            string wrong = WrongName(w, hit, (title ?? "") + " " + story, allowed);
            if (wrong != null) return (t.FallbackTitle, fallbackStory, $"replaced: names {wrong}");
            if (Digits.IsMatch(text)) return (t.FallbackTitle, fallbackStory, "replaced: has a number");
            if (Scorecard.Words(story) > MaxStoryWords) return (title ?? t.FallbackTitle, fallbackStory, "replaced: too long");
            if (story.Length == 0) return (title ?? t.FallbackTitle, fallbackStory, "replaced: no story");
            return title == null ? (t.FallbackTitle, story, "no title") : (title, story, "kept");
        }

        /// <summary>The template's own story, for the countries hit or the province.</summary>
        public static string FallbackStory(EventTemplate t, IReadOnlyList<string> hit, string province)
        {
            string who = Join(hit);
            return t.FallbackStory.Replace("{who}", who).Replace("{where}", province ?? who);
        }

        /// <summary>A statement's title, before its first colon within 80 characters, and its story; the title is null when there is none.</summary>
        public static (string title, string story) SplitTitle(string statement)
        {
            string text = (statement ?? "").Trim();
            int colon = text.IndexOf(':');
            if (colon <= 0 || colon > 80) return (null, text);
            string title = Clean(text.Substring(0, colon));
            return (title.Length == 0 ? null : title, text.Substring(colon + 1).Trim());
        }

        /// <summary>A country the text names that the event did not reach, or one whose province it names; null when there is none.</summary>
        public static string WrongName(World w, IReadOnlyList<string> hit, string text) => WrongName(w, hit, text, null);

        /// <summary>As <see cref="WrongName(World, IReadOnlyList{string}, string)"/>, allowing the names in <paramref name="allowed"/>.</summary>
        public static string WrongName(World w, IReadOnlyList<string> hit, string text, ICollection<string> allowed)
        {
            foreach (var c in w.CountryList)
                if (!hit.Contains(c.Id) && (allowed == null || !allowed.Contains(c.Id)) && Names(text, c.Id, c.Name)) return c.Id;
            foreach (var p in w.ProvinceList)
                if (p.Owner != null && !hit.Contains(p.Owner) && (allowed == null || !allowed.Contains(p.Id)) && Names(text, p.Id, p.Name)) return $"{p.Id}, {p.Owner}'s";
            return null;
        }

        /// <summary>
        /// A title from the title call, cleaned: quotes, a "Title:" label and end punctuation off; null
        /// when there is none, or it is not a title (more than seven words, a number, two sentences).
        /// </summary>
        public static string CleanTitle(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;
            string s = raw.Trim();
            s = Regex.Replace(s, @"^(the title|title)\s*[:\-]\s*", "", RegexOptions.IgnoreCase);
            int colon = s.IndexOf(':');
            if (colon > 0) s = s.Substring(0, colon);
            s = Clean(s).TrimEnd('.', '!', '?', ',', ';').Trim();
            s = Clean(s);
            if (s.Length == 0 || Digits.IsMatch(s) || Regex.IsMatch(s, @"[.!?]\s")) return null;
            int words = Scorecard.Words(s);
            return words < 1 || words > 7 ? null : s;
        }

        private static bool Names(string text, string id, string name) =>
            Regex.IsMatch(text, @"\b" + Regex.Escape(id) + @"\b", RegexOptions.IgnoreCase)
            || (name != null && Regex.IsMatch(text, @"\b" + Regex.Escape(name) + @"\b", RegexOptions.IgnoreCase));

        private static string Clean(string title) => title.Trim().Trim('"', '*', '#', '\'', '“', '”').Trim();

        /// <summary>"valen", "valen and orsk", "valen, orsk and kesh".</summary>
        public static string Join(IReadOnlyList<string> ids) =>
            ids.Count == 0 ? "nobody" : ids.Count == 1 ? ids[0] : string.Join(", ", ids.Take(ids.Count - 1)) + " and " + ids[ids.Count - 1];

        /// <summary>The line a country hit by the event reads among this turn's facts.</summary>
        public static string HitLine(EventRecord e, string countryId, int amount)
        {
            string head = $"Event \"{e.Title}\":";
            switch (e.Template)
            {
                case Windfall: return $"{head} a windfall of {amount} gold reached our treasury.";
                case BadHarvest: return $"{head} a bad harvest halved our income this turn, {amount} gold lost.";
                case Plague: return $"{head} a sickness killed {amount} of our soldiers.";
                case Volunteers: return $"{head} {amount} volunteers joined our army.";
                case Zeal: return $"{head} zeal swept our provinces: stability +1.";
                case Unrest: return $"{head} unrest in our towns: stability -1.";
                case Stronghold: return $"{head} new walls in {e.Province}: +1 fort there.";
                case WallsCrumble: return $"{head} the fort in {e.Province} crumbled: -1 fort there.";
                default: return $"{head} it touched us.";
            }
        }

        /// <summary>The news every other living country reads.</summary>
        public static string NewsLine(EventRecord e, EventTemplate t) =>
            $"News: {Join(e.Hit)} {t.NewsVerb}{(e.Province != null ? " in " + e.Province : "")} (\"{e.Title}\").";
    }
}

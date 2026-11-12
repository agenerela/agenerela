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

        public override string ToString() => Id;
    }

    /// <summary>A decision the storyteller made, handed to <see cref="Game.QueueEvent"/>: the template, the target, and its title and story as one statement.</summary>
    public sealed class StoryEvent
    {
        public StoryEvent(string template, string target, string statement)
        {
            Template = template;
            Target = target;
            Statement = statement;
        }

        public string Template { get; }
        public string Target { get; }
        /// <summary>The model's statement: "A title: one or two sentences."</summary>
        public string Statement { get; }
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

        public bool StoryReplaced => StoryVerdict != null && StoryVerdict.StartsWith("replaced", StringComparison.Ordinal);
        public int TotalValue => Value.Values.Sum();

        public override string ToString() => $"turn {Turn}: {Template} on {Target}" + (Title != null ? $" \"{Title}\"" : "");
    }

    /// <summary>Every event of a game, for the fairness budget, the cooldowns and the "told so far" line.</summary>
    public sealed class EventLedger
    {
        public List<EventRecord> Events { get; } = new List<EventRecord>();

        public IEnumerable<EventRecord> Told => Events.Where(e => e.Source == EventRecord.FromStoryteller);

        /// <summary>What events were worth to a country over the <paramref name="window"/> turns before <paramref name="turn"/>.</summary>
        public int NetValue(string country, int turn, int window) =>
            Events.Where(e => e.Turn > turn - window && e.Turn < turn && e.Value.ContainsKey(country)).Sum(e => e.Value[country]);

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

        /// <summary>The random table's kinds, for <see cref="KeywordsFor"/>.</summary>
        private static readonly Dictionary<string, string[]> TableKeywords = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            [GoodHarvest] = new[] { "harvest", "crops", "fields" },
            [PoorHarvest] = new[] { "harvest", "famine", "crops", "fields" },
            [Bandits] = new[] { "bandits", "bandit", "brigands" },
            [Unrest] = new[] { "unrest", "riot", "discontent" },
        };

        public static EventTemplate Template(string id) => Templates.FirstOrDefault(t => t.Id == id);

        public static IReadOnlyList<string> KeywordsFor(string template) =>
            Template(template)?.Keywords ?? (TableKeywords.TryGetValue(template ?? "", out var k) ? k : Array.Empty<string>());

        /// <summary>Every id a storyteller can name: the living countries, the groups, the provinces.</summary>
        public static IEnumerable<string> TargetIds(World w) =>
            w.Alive.Select(c => c.Id).Concat(EventGroups.All).Concat(w.ProvinceList.Select(p => p.Id));

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
        public static string WhyNot(World w, EventLedger ledger, int turn, string template, string target)
        {
            var t = Template(template);
            if (t == null) return $"{template} is not an event";
            var r = w.Rules;
            if (turn < r.EventFirstTurn) return $"no event before turn {r.EventFirstTurn}";
            var kind = KindOf(w, target);
            if (kind == EventTargetKind.None) return $"{target} is not a country, a group or a province";
            if ((t.Takes & kind) == 0) return $"{template} does not take a {kind.ToString().ToLowerInvariant()}";

            // Cooldowns: a template not again within the last few events, a target not twice in a row.
            var recent = ledger.LastTold(r.EventTemplateCooldown);
            if (recent.Any(e => e.Template == template)) return $"{template} happened within the last {r.EventTemplateCooldown} events";
            var last = recent.LastOrDefault();
            int same = r.EventMaxSameKindInARow;
            var streak = ledger.LastTold(same);
            if (same > 0 && streak.Count == same && streak.All(e => Template(e.Template)?.Bonus == t.Bonus))
                return t.Bonus ? $"{same} gifts in a row: a blow is due" : $"{same} blows in a row: a gift is due";
            if (last != null && last.Target == target) return $"the last event struck {target}";

            string province = null;
            if (kind == EventTargetKind.Province)
            {
                var p = w.Province(target);
                province = p.Id;
                if (p.Owner == null) return $"{target} is neutral";
                if (template == Stronghold && p.Forts >= r.MaxFortsPerProvince) return $"{target} already has {p.Forts} forts";
                if (template == WallsCrumble && p.Forts == 0) return $"{target} has no fort";
                if (template == WallsCrumble && p.IsCapital) return $"{target} is a capital";
            }

            var reach = Reach(w, target);
            if (reach.Count == 0) return $"{target} reaches no living country now";
            if (kind == EventTargetKind.Group && reach.Count < 2 && target != EventGroups.Leader && target != EventGroups.Weakest)
                return $"{target} is only {reach[0]} now";
            if (kind != EventTargetKind.Group && last != null && last.Hit.Contains(reach[0]))
                return $"{reach[0]} was struck by the last event";
            foreach (string c in reach)
            {
                string why = WhyNotFor(w, ledger, turn, t, c);
                if (why != null) return why;
            }
            return null;
        }

        /// <summary>The per-country part of <see cref="WhyNot"/>: the budget, the weak and the leader, and what the effect needs.</summary>
        public static string WhyNotFor(World w, EventLedger ledger, int turn, EventTemplate t, string countryId)
        {
            var r = w.Rules;
            var c = w.Country(countryId);
            if (c == null || !c.Alive) return $"{countryId} is out of the game";
            int held = w.ProvinceCount(countryId);
            int net = ledger.NetValue(countryId, turn, r.EventBudgetWindow);
            if (t.Bonus)
            {
                if (held >= r.EventNoBonusFromProvinces) return $"{countryId} holds {held} provinces: no gift may crown the leader";
                if (net >= r.EventBudget) return $"{countryId} has had enough good fortune lately";
            }
            else
            {
                if (held < r.EventMinProvincesForPenalty) return $"{countryId} holds only {Facts.Provinces(held)}: too weak to strike";
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
            string province, string statement)
        {
            string who = Join(hit);
            string fallbackStory = t.FallbackStory.Replace("{who}", who).Replace("{where}", province ?? who);
            string text = (statement ?? "").Trim();
            if (text.Length == 0) return (t.FallbackTitle, fallbackStory, "replaced: no story");

            string title = null, story = text;
            int colon = text.IndexOf(':');
            if (colon > 0 && colon <= 80)
            {
                title = Clean(text.Substring(0, colon));
                story = text.Substring(colon + 1).Trim();
                if (title.Length == 0) title = null;
            }

            string wrong = WrongName(w, hit, (title ?? "") + " " + story);
            if (wrong != null) return (t.FallbackTitle, fallbackStory, $"replaced: names {wrong}");
            if (Digits.IsMatch(text)) return (t.FallbackTitle, fallbackStory, "replaced: has a number");
            if (Scorecard.Words(story) > MaxStoryWords) return (title ?? t.FallbackTitle, fallbackStory, "replaced: too long");
            if (story.Length == 0) return (title ?? t.FallbackTitle, fallbackStory, "replaced: no story");
            return title == null ? (t.FallbackTitle, story, "no title") : (title, story, "kept");
        }

        /// <summary>A country the text names that the event did not reach, or one whose province it names; null when there is none.</summary>
        public static string WrongName(World w, IReadOnlyList<string> hit, string text)
        {
            foreach (var c in w.CountryList)
                if (!hit.Contains(c.Id) && Names(text, c.Id, c.Name)) return c.Id;
            foreach (var p in w.ProvinceList)
                if (p.Owner != null && !hit.Contains(p.Owner) && Names(text, p.Id, p.Name)) return $"{p.Id}, {p.Owner}'s";
            return null;
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

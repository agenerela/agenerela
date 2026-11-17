using System;
using System.Collections.Generic;
using System.Linq;

namespace GreyBoxStrategy
{
    /// <summary>
    /// One game, played a turn at a time in three calls, so the caller decides how each country's
    /// decision is made: synchronously by a script (<see cref="GameRunner"/>), or, in stage 2,
    /// asynchronously by framework agents all asked on one snapshot.
    /// </summary>
    /// <remarks>
    /// <code>
    /// var council = game.BeginTurn();               // upkeep, then one request per living country
    /// var envoys  = game.SubmitCouncil(answers);    // one request per offer of peace
    /// var report  = game.Resolve(envoyAnswers);     // peace, home moves, wars, attacks, victory
    /// </code>
    /// Nothing changes the world between <see cref="BeginTurn"/> and <see cref="Resolve"/>, so every
    /// country decides on the same snapshot. Resolution then re-checks each move with
    /// <see cref="Legality.WhyNot"/> before it lands, and refuses one an earlier move made stale.
    /// </remarks>
    public sealed partial class Game
    {
        private enum Phase { AwaitingTurn, AwaitingCouncil, AwaitingEnvoys, Over }

        private sealed class Offer
        {
            public string Id;
            public string From;
            public string To;
            public bool Mutual;
            public CountryRequest EnvoyRequest;
        }

        private readonly Dictionary<string, List<TurnFact>> pending = new Dictionary<string, List<TurnFact>>(StringComparer.Ordinal);
        private Phase phase = Phase.AwaitingTurn;
        private TurnReport report;
        private List<CountryRequest> councilRequests;
        private List<CountryAnswer> councilAnswers;
        private List<Offer> offers;
        // Who held each province when the council decided, so a refusal can say that someone took
        // it first rather than only what the rules now say.
        private Dictionary<string, string> ownersAtCouncil = new Dictionary<string, string>(StringComparer.Ordinal);

        public Game(int seed, MapDefinition map = null, GameRules rules = null)
        {
            Seed = seed;
            World = new World(map ?? MapDefinition.Standard(), rules ?? GameRules.Standard, seed);
            foreach (var c in World.CountryList) pending[c.Id] = new List<TurnFact>();
        }

        public int Seed { get; }
        public World World { get; }
        /// <summary>
        /// Gates by country id: asked before each move of that country lands (<see cref="IMoveGate"/>).
        /// Countries without one are admitted as before.
        /// </summary>
        public Dictionary<string, IMoveGate> Gates { get; } = new Dictionary<string, IMoveGate>(StringComparer.Ordinal);
        public GameRules Rules => World.Rules;
        public GameResult Result { get; private set; }
        public bool IsOver => Result != null;
        /// <summary>Every resolved turn, in order.</summary>
        public List<TurnReport> Reports { get; } = new List<TurnReport>();
        /// <summary>The map at each council, after upkeep: what every country decided on, turn by turn.</summary>
        public List<TurnSnapshot> Snapshots { get; } = new List<TurnSnapshot>();

        /// <summary>
        /// Whether upkeep rolls the random event table (harvests, bandits, unrest) for each country.
        /// On by default. A game with a storyteller turns it off, so the storyteller's one event a
        /// turn replaces the table rather than adding to it, and the two never both fire.
        /// </summary>
        public bool EventTable
        {
            get => eventTable;
            set
            {
                if (value && queued != null) throw new InvalidOperationException("An event is queued: the random table cannot be switched on as well.");
                eventTable = value;
            }
        }

        /// <summary>Every event of the game, the table's and the storyteller's, for the fairness rules and the logs.</summary>
        public EventLedger Events { get; } = new EventLedger();

        /// <summary>The turn the next <see cref="BeginTurn"/> starts: the one an event queued now happens on.</summary>
        public int NextTurn => World.Turn + 1;

        /// <summary>
        /// Which of the storyteller's stage S2 pieces this game uses (<see cref="StoryRules"/>); S1's
        /// rules, every piece off, unless set. Set it before the first event.
        /// </summary>
        public StoryRules Story
        {
            get => story;
            set => story = value ?? StoryRules.S1;
        }

        private StoryRules story = StoryRules.S1;

        /// <summary>Why this event may not happen at the next upkeep, or null (<see cref="EventRules.WhyNot"/>).</summary>
        public string WhyNotEvent(string template, string target) => EventRules.WhyNot(World, Events, NextTurn, template, target, story);

        /// <summary>Every chain of the game (S2): the screen follows a tale through these.</summary>
        public IReadOnlyList<Chain> Chains => Events.Chains;

        /// <summary>
        /// The choices the queued event asks, one per ruler it reaches, until the next upkeep lands
        /// them. A person's seat answers through <see cref="Choose"/>; model and scripted seats through
        /// <see cref="ChoiceRequests"/> and <see cref="SubmitChoices"/>. An unanswered one takes its default.
        /// </summary>
        public IReadOnlyList<EventChoice> PendingChoices => pendingChoices;

        private readonly List<EventChoice> pendingChoices = new List<EventChoice>();
        // The queued event as the rulers will read it (S2), built when it is queued so a dilemma can be asked before upkeep.
        private EventRecord draft;
        private EventTemplate draftTemplate;

        /// <summary>One request per waiting choice, for the seats: the story and the options among its facts, the options as its moves.</summary>
        public IReadOnlyList<CountryRequest> ChoiceRequests()
        {
            Expect(Phase.AwaitingTurn);
            return pendingChoices.Where(c => c.Chosen == null && World.Country(c.Country).Alive).Select(c => new CountryRequest(
                Moment.Choice, c.Turn, c.Country, World, Facts.Standing(World, c.Country),
                new List<string> { $"Event \"{c.Title}\": {c.Story}", c.OptionsLine() },
                c.Options.Select(o => new Move(o.Id)).ToList(), offerId: c.Id, defaultMove: new Move(c.DefaultOption))).ToList();
        }

        /// <summary>A ruler's choice, by a person on the screen or a seat; null when taken, else why not.</summary>
        public string Choose(string countryId, string optionId, string by = "person")
        {
            Expect(Phase.AwaitingTurn);
            var c = pendingChoices.FirstOrDefault(x => x.Country == countryId && x.Chosen == null);
            if (c == null) return $"{countryId} has no choice waiting";
            if (c.Option(optionId) == null) return $"{optionId} is not one of {countryId}'s options";
            c.Chosen = optionId;
            c.ChosenBy = by;
            return null;
        }

        /// <summary>
        /// The seats' answers to <see cref="ChoiceRequests"/>. A model seat's gate executes the
        /// decision first (<see cref="IMoveGate"/>), so its memory keeps what it chose; an answer that is
        /// not one of the options, or that the gate refuses, leaves the default for upkeep.
        /// </summary>
        public void SubmitChoices(IEnumerable<CountryAnswer> answers)
        {
            Expect(Phase.AwaitingTurn);
            foreach (var a in answers ?? Enumerable.Empty<CountryAnswer>())
            {
                if (a?.Request == null || a.Request.Moment != Moment.Choice) continue;
                var c = pendingChoices.FirstOrDefault(x => x.Id == a.Request.OfferId && x.Chosen == null);
                if (c == null || a.Choice == null || c.Option(a.Choice.Action) == null) continue;
                bool seat = Gates.TryGetValue(c.Country, out var gate);
                if (seat && gate.Admit(a.Request, a.Choice) != null) continue;
                Choose(c.Country, a.Choice.Action, seat ? "model" : "seat");
            }
        }

        /// <summary>
        /// Queues the storyteller's event for the next upkeep, where it is re-checked, applied and told
        /// to the rulers. Only between turns, only with <see cref="EventTable"/> off, and one a turn.
        /// </summary>
        public void QueueEvent(StoryEvent e)
        {
            if (e == null) throw new ArgumentNullException(nameof(e));
            Expect(Phase.AwaitingTurn);
            if (eventTable) throw new InvalidOperationException("The random event table is on: a storyteller event would fire beside it.");
            if (queued != null) throw new InvalidOperationException("An event is already queued for this turn.");
            queued = e;
            if (story.IsS2) Draft(e);
        }

        /// <summary>
        /// The history check (S2's <c>+history</c>) on a story before it is queued, for the storyteller's guard
        /// (#292): null when the check keeps it, or leaves it to the name, number and length check that runs
        /// first; else why it is invented, and the statement to queue in its place, the template's own story
        /// under the model's title. The same checks, in the same order, as the event's at <see cref="QueueEvent"/>.
        /// </summary>
        public (string reason, string statement)? HistoryVerdict(string template, string target, string statement)
        {
            if (!story.IsS2 || !story.History || string.IsNullOrWhiteSpace(statement)) return null;
            int turn = NextTurn;
            var asked = EventRules.Template(template);
            var t = asked?.Skeleton != null ? EventRules.BeginStage(World, Events, turn, asked, target, story) : asked;
            if (t == null || EventRules.WhyNot(World, Events, turn, template, target, story) != null) return null;
            var chain = Events.Chain(target);
            var (hit, _) = EventRules.ReachFor(World, Events, turn, t, target, story);
            string subject = chain?.Subject ?? target;
            string province = World.IsProvince(subject) ? subject : null;
            var happenings = History.Of(this);
            var allowed = new HashSet<string>(History.Named(happenings), StringComparer.Ordinal);
            if (chain != null)
            {
                allowed.UnionWith(chain.Countries);
                if (chain.Province != null) allowed.Add(chain.Province);
            }
            // The title call's title is not known yet; the event's own check judges it at the queue.
            var (_, told, verdict) = EventRules.CheckStory(World, t, hit, province, statement, null, allowed.Count > 0 ? allowed : null, story.TitleCall);
            if (verdict != null && verdict.StartsWith("replaced", StringComparison.Ordinal)) return null;
            string why = History.Check(World, happenings, History.Recent(happenings, turn), hit, told);
            if (why == null || !why.StartsWith("invented", StringComparison.Ordinal)) return null;
            string fallback = EventRules.FallbackStory(t, hit, province);
            string modelTitle = story.TitleCall ? null : EventRules.SplitTitle(statement).title;
            return (why, modelTitle != null ? $"{modelTitle}: {fallback}" : fallback);
        }

        // S2: the event as the rulers will read it, built when it is queued, and the choices it asks.
        private void Draft(StoryEvent q)
        {
            int turn = NextTurn;
            var asked = EventRules.Template(q.Template);
            var t = asked?.Skeleton != null ? EventRules.BeginStage(World, Events, turn, asked, q.Target, story) : asked;
            draft = null;
            draftTemplate = null;
            pendingChoices.Clear();
            if (t == null || EventRules.WhyNot(World, Events, turn, q.Template, q.Target, story) != null) return; // refused at upkeep

            var chain = Events.Chain(q.Target);
            var (hit, spared) = EventRules.ReachFor(World, Events, turn, t, q.Target, story);
            string subject = chain?.Subject ?? q.Target;
            string province = World.IsProvince(subject) ? subject : null;
            var e = new EventRecord
            {
                Turn = turn,
                Source = EventRecord.FromStoryteller,
                Template = t.Id,
                Action = q.Template,
                Target = q.Target,
                Province = province,
                Statement = q.ModelStatement ?? q.Statement,
            };
            e.Hit.AddRange(hit);
            e.Spared.AddRange(spared);
            foreach (var other in World.Alive) e.ProvincesAtTime[other.Id] = World.ProvinceCount(other.Id);
            if (story.Traits && province != null) e.Traits = World.Traits[province].Line;

            var happenings = story.History ? History.Of(this) : null;
            var allowed = new HashSet<string>(StringComparer.Ordinal);
            if (happenings != null) allowed.UnionWith(History.Named(happenings));
            if (chain != null)
            {
                allowed.UnionWith(chain.Countries);
                if (chain.Province != null) allowed.Add(chain.Province);
            }
            (e.Title, e.Story, e.StoryVerdict) = EventRules.CheckStory(World, t, e.Hit, province, q.Statement, q.Title,
                allowed.Count > 0 ? allowed : null, story.TitleCall);
            e.ModelTitle = story.TitleCall ? EventRules.CleanTitle(q.Title) : EventRules.SplitTitle(q.Statement).title;
            if (q.HistoryReplaced != null && !e.StoryReplaced)
            {
                // The storyteller's guard ran this check before the event was queued and told the template's line.
                e.HistoryRef = q.HistoryReplaced;
                e.StoryVerdict = "replaced: " + q.HistoryReplaced;
            }
            else if (happenings != null && !e.StoryReplaced)
            {
                e.HistoryRef = History.Check(World, happenings, History.Recent(happenings, turn), e.Hit, e.Story);
                if (e.HistoryRef != null && e.HistoryRef.StartsWith("invented", StringComparison.Ordinal))
                {
                    e.Story = EventRules.FallbackStory(t, e.Hit, province);
                    e.StoryVerdict = "replaced: " + e.HistoryRef;
                }
            }

            if (t.IsChoice)
            {
                foreach (string id in e.Hit)
                {
                    var ch = new EventChoice
                    {
                        Id = $"t{turn}-{id}-{t.Id}", Turn = turn, Country = id, Template = t.Id, Title = e.Title, Story = e.Story,
                        Province = province, ChainId = chain?.Id, DefaultOption = t.DefaultOption,
                    };
                    foreach (var o in t.Options)
                    {
                        var co = new ChoiceOption { Id = o.Id, Label = o.Label, Lean = o.Lean };
                        foreach (var part in o.Parts)
                        {
                            var (amount, value, text) = EventEffects.Plan(World, part, id);
                            co.Planned.Add((part, amount, value, text));
                        }
                        co.Effect = co.Planned.Count == 0 ? "nothing changes"
                            : string.Join(", ", co.Planned.Select(x => EventEffects.Phrase(x.part, x.amount, province)));
                        ch.Options.Add(co);
                    }
                    pendingChoices.Add(ch);
                }
                e.Options.AddRange(t.Options.Select(o => o.Id));
            }
            draft = e;
            draftTemplate = t;
        }

        private bool eventTable = true;
        private StoryEvent queued;
        // Income a bad harvest takes from a country at this upkeep.
        private readonly Dictionary<string, int> incomeCut = new Dictionary<string, int>(StringComparer.Ordinal);

        // ------------------------------------------------------------------------------------
        // 1. Upkeep and council requests
        // ------------------------------------------------------------------------------------

        /// <summary>
        /// Starts the next turn: truces count down, every living country collects income, pays
        /// upkeep and rolls for an event and a revolt. Returns one council request per living
        /// country, all built on the state as it stands now.
        /// </summary>
        public IReadOnlyList<CountryRequest> BeginTurn()
        {
            Expect(Phase.AwaitingTurn);
            World.Turn++;
            report = new TurnReport { Turn = World.Turn };

            foreach (var (a, b) in World.TickTruces())
            {
                if (!World.Country(a).Alive || !World.Country(b).Alive) continue;
                Tell(a, 3, $"Our truce with {b} has ended.");
                Tell(b, 3, $"Our truce with {a} has ended.");
                report.Upkeep.Add($"The truce between {a} and {b} ended.");
            }

            if (queued != null) ApplyQueued();
            if (story.Chains != ChainMode.None) TendChains();
            foreach (var c in World.Alive.ToList()) Upkeep(c);
            incomeCut.Clear();

            ownersAtCouncil = World.ProvinceList.ToDictionary(p => p.Id, p => p.Owner, StringComparer.Ordinal);
            if (Rules.Turn == TurnStyle.Writs) SyncArmies();
            Snapshots.Add(new TurnSnapshot(World));
            if (Rules.Turn == TurnStyle.Writs) return BeginWrits();
            councilRequests = World.Alive.Select(c => new CountryRequest(
                Moment.Council, World.Turn, c.Id, World,
                Facts.Standing(World, c.Id),
                Facts.Select(pending[c.Id]),
                Legality.CouncilMoves(World, c.Id))).ToList();
            phase = Phase.AwaitingCouncil;
            return councilRequests;
        }

        private void Upkeep(Country c)
        {
            var r = Rules;
            bool poorHarvest = false;
            if (eventTable)
            {
                double roll = Dice.Unit(Seed, World.Turn, c.Id, "event");
                if (roll < r.GoodHarvestChance)
                {
                    c.Gold += r.GoodHarvestGold;
                    Tell(c.Id, 2, $"A good harvest brought {r.GoodHarvestGold} gold.");
                    report.Upkeep.Add($"{c.Id}: good harvest, +{r.GoodHarvestGold} gold.");
                    TableEvent(c, EventRules.GoodHarvest, r.GoodHarvestGold, $"{r.GoodHarvestGold} gold");
                }
                else if (roll < r.GoodHarvestChance + r.PoorHarvestChance)
                {
                    poorHarvest = true;
                    Tell(c.Id, 2, "A poor harvest halved our income this turn.");
                    report.Upkeep.Add($"{c.Id}: poor harvest, income halved.");
                    TableEvent(c, EventRules.PoorHarvest, -(World.Income(c) - World.Income(c) / 2), "half the income");
                }
                else if (roll < r.GoodHarvestChance + r.PoorHarvestChance + r.BanditsChance)
                {
                    int lost = Math.Min(c.Army, r.BanditsSoldiers);
                    c.Army -= lost;
                    Tell(c.Id, 2, $"Bandits killed {lost} of our soldiers.");
                    report.Upkeep.Add($"{c.Id}: bandits, -{lost} soldiers.");
                    TableEvent(c, EventRules.Bandits, -lost * r.GoldPerSoldier, $"{lost} soldiers");
                }
                else if (roll < r.GoodHarvestChance + r.PoorHarvestChance + r.BanditsChance + r.UnrestChance)
                {
                    if (c.Stability > 1)
                    {
                        c.Stability--;
                        Tell(c.Id, 2, "Unrest in the provinces cost us 1 stability.");
                        report.Upkeep.Add($"{c.Id}: unrest, -1 stability.");
                        TableEvent(c, EventRules.Unrest, -EventRules.StabilityValue, "1 stability");
                    }
                }
            }

            int income = World.Income(c);
            if (poorHarvest) income /= 2;
            if (incomeCut.TryGetValue(c.Id, out int cut)) income -= Math.Min(income, cut);
            int upkeep = World.Upkeep(c);
            c.Gold += income - upkeep;
            if (c.Gold < 0)
            {
                int deserted = Math.Max(1, (int)Math.Round(c.Army * r.DesertionShare));
                deserted = Math.Min(deserted, c.Army);
                c.Army -= deserted;
                c.Gold = 0;
                Tell(c.Id, 1, $"The treasury ran dry: {deserted} soldiers deserted.");
                report.Upkeep.Add($"{c.Id}: bankrupt, {deserted} soldiers deserted.");
            }

            if (!c.RaisedTaxes && c.Stability < r.MaxStability) c.Stability++;
            c.RaisedTaxes = false;

            if (c.Stability <= 1 && Dice.Unit(Seed, World.Turn, c.Id, "revolt") < r.RevoltChance)
            {
                var candidates = World.ProvincesOf(c.Id).Where(p => !p.IsCapital)
                    .OrderBy(p => p.Id, StringComparer.Ordinal).ToList();
                if (candidates.Count > 0 && World.ProvinceCount(c.Id) > 1)
                {
                    var lost = candidates[Dice.Int(Seed, World.Turn, c.Id, "revolt-province", 0, candidates.Count)];
                    lost.Owner = null;
                    lost.Forts = 0;
                    lost.Garrison = r.RevoltGarrison;
                    c.Stability = r.StabilityAfterRevolt;
                    report.TerritoryChanges++;
                    Tell(c.Id, 0, $"{lost.Id} revolted and broke away from us.");
                    report.Upkeep.Add($"{c.Id}: {lost.Id} revolted and is neutral again.");
                }
            }
        }

        // A random table event, recorded beside the storyteller's so both arms are scored alike.
        private void TableEvent(Country c, string kind, int value, string effect)
        {
            var e = new EventRecord { Turn = World.Turn, Source = EventRecord.FromTable, Template = kind, Target = c.Id };
            e.Hit.Add(c.Id);
            e.Value[c.Id] = value;
            e.Effect[c.Id] = effect;
            foreach (var other in World.Alive) e.ProvincesAtTime[other.Id] = World.ProvinceCount(other.Id);
            report.Events.Add(e);
            Events.Events.Add(e);
        }

        // The storyteller's event, at the start of upkeep: re-checked with the same rule that offered
        // it, then applied with the game's strength, its story checked, and told to every ruler.
        private void ApplyQueued()
        {
            var q = queued;
            queued = null;
            if (story.IsS2)
            {
                ApplyDraft(q);
                return;
            }
            string why = EventRules.WhyNot(World, Events, World.Turn, q.Template, q.Target);
            if (why != null)
            {
                report.EventRefused = $"{q.Template} on {q.Target}: {why}";
                report.Upkeep.Add($"The storyteller's {q.Template} on {q.Target} was refused: {why}.");
                return;
            }

            var t = EventRules.Template(q.Template);
            var e = new EventRecord
            {
                Turn = World.Turn,
                Source = EventRecord.FromStoryteller,
                Template = t.Id,
                Target = q.Target,
                Province = World.IsProvince(q.Target) ? q.Target : null,
                Statement = q.Statement,
            };
            e.Hit.AddRange(EventRules.Reach(World, q.Target));
            foreach (var other in World.Alive) e.ProvincesAtTime[other.Id] = World.ProvinceCount(other.Id);
            (e.Title, e.Story, e.StoryVerdict) = EventRules.CheckStory(World, t, e.Hit, e.Province, q.Statement);
            e.ModelTitle = EventRules.SplitTitle(q.Statement).title;
            e.Action = q.Template;

            foreach (string id in e.Hit)
            {
                var (amount, value, effect) = ApplyS1(t, id, e.Province);
                e.Value[id] = value;
                e.Effect[id] = effect;
                Tell(id, 1, EventRules.HitLine(e, id, amount));
                string sign = value >= 0 ? "+" : "-";
                report.Upkeep.Add($"{id}: event \"{e.Title}\" ({t.Id}{(e.Province != null ? " in " + e.Province : "")}), {sign}{effect}.");
            }
            string news = EventRules.NewsLine(e, t);
            foreach (var other in World.Alive.Where(c => !e.Hit.Contains(c.Id))) Tell(other.Id, 3, news);
            report.Events.Add(e);
            Events.Events.Add(e);
        }

        // One of S1's eight templates on one country, as S1 applied it.
        private (int amount, int value, string effect) ApplyS1(EventTemplate t, string id, string province)
        {
            var c = World.Country(id);
            var (amount, value, effect) = EventRules.Plan(World, t.Id, id);
            switch (t.Id)
            {
                case EventRules.Windfall: c.Gold += amount; break;
                case EventRules.BadHarvest: incomeCut[id] = amount; break;
                case EventRules.Plague: c.Army -= amount; break;
                case EventRules.Volunteers: c.Army += amount; break;
                case EventRules.Zeal: c.Stability = Math.Min(Rules.MaxStability, c.Stability + 1); break;
                case EventRules.Unrest:
                    // And no recovery at this upkeep, as after a tax rise; otherwise the point
                    // lost is back before the council and the event does nothing.
                    c.Stability = Math.Max(1, c.Stability - 1);
                    c.RaisedTaxes = true;
                    break;
                case EventRules.Stronghold: World.Province(province).Forts++; break;
                case EventRules.WallsCrumble: World.Province(province).Forts--; break;
            }
            return (amount, value, effect);
        }

        // S2's event at upkeep: re-checked with the rule that offered it, then each country's effect
        // (for a dilemma, the option it chose, or the default), told to every ruler, and its chain kept.
        private void ApplyDraft(StoryEvent q)
        {
            var e = draft;
            var t = draftTemplate;
            var choices = pendingChoices.ToList();
            draft = null;
            draftTemplate = null;
            pendingChoices.Clear();
            string why = e == null ? "it could not be told" : EventRules.WhyNot(World, Events, World.Turn, q.Template, q.Target, story);
            if (why != null)
            {
                report.EventRefused = $"{q.Template} on {q.Target}: {why}";
                report.Upkeep.Add($"The storyteller's {q.Template} on {q.Target} was refused: {why}.");
                return;
            }

            foreach (string id in e.Hit.ToList())
            {
                int value;
                string effect, line;
                if (t.IsChoice)
                {
                    var ch = choices.First(x => x.Country == id);
                    string chosen = ch.Chosen ?? ch.DefaultOption;
                    var o = ch.Option(chosen);
                    foreach (var x in o.Planned) EventEffects.Apply(World, x.part, id, e.Province, x.amount, incomeCut);
                    value = o.Value;
                    e.Choices[id] = chosen;
                    e.ChosenBy[id] = ch.Chosen != null ? ch.ChosenBy : "default";
                    effect = o.Planned.Count == 0 ? $"chose to {o.Label}" : $"chose to {o.Label}: " + string.Join(", ", o.Planned.Select(x => x.text));
                    line = $"Event \"{e.Title}\": we chose to {o.Label}" + (o.Planned.Count == 0 ? "." : ": " + o.Effect + ".");
                }
                else if (t.Parts != null)
                {
                    var planned = t.Parts.Select(p => (p, plan: EventEffects.Plan(World, p, id))).ToList();
                    foreach (var x in planned) EventEffects.Apply(World, x.p, id, e.Province, x.plan.amount, incomeCut);
                    value = planned.Sum(x => x.plan.value);
                    effect = planned.Count == 0 ? "a story only" : string.Join(", ", planned.Select(x => x.plan.text));
                    line = planned.Count == 0 ? $"Event \"{e.Title}\": {e.Story}"
                        : $"Event \"{e.Title}\": " + string.Join(", ", planned.Select(x => EventEffects.Phrase(x.p, x.plan.amount, e.Province))) + ".";
                }
                else
                {
                    var (amount, v, text) = ApplyS1(t, id, e.Province);
                    value = v;
                    effect = text;
                    line = EventRules.HitLine(e, id, amount);
                }
                e.Value[id] = value;
                e.Effect[id] = effect;
                Tell(id, 1, line);
                report.Upkeep.Add($"{id}: event \"{e.Title}\" ({t.Id}{(e.Province != null ? " in " + e.Province : "")}), {effect}.");
            }
            string news = EventRules.NewsLine(e, t);
            foreach (var other in World.Alive.Where(c => !e.Hit.Contains(c.Id))) Tell(other.Id, 3, news);
            report.Events.Add(e);
            Events.Events.Add(e);
            if (story.Chains != ChainMode.None) KeepChain(e, q);
        }

        // A chain begins, continues or ends with this event (S2).
        private void KeepChain(EventRecord e, StoryEvent q)
        {
            var asked = EventRules.Template(q.Template);
            var chain = Events.Chain(q.Target);
            if (asked.Skeleton != null)
            {
                chain = NewChain(e, q.Target, ChainMode.Skeleton, asked.Skeleton);
                chain.Status = ChainStatus.Open;
                chain.Events.Add(e);
                chain.StageIndex = 0;
                if (ChainSkeletons.NextStage(ChainSkeletons.Named(chain.Skeleton), chain) < 0) End(chain, ChainStatus.Completed, "told to its end");
            }
            else if (chain != null)
            {
                if (chain.Mode == ChainMode.Skeleton)
                {
                    var k = ChainSkeletons.Named(chain.Skeleton);
                    int stage = ChainSkeletons.NextStage(k, chain);
                    chain.Events.Add(e);
                    chain.StageIndex = stage;
                    if (ChainSkeletons.NextStage(k, chain) < 0) End(chain, ChainStatus.Completed, "told to its end");
                }
                else
                {
                    chain.Events.Add(e);
                    chain.Status = ChainStatus.Open;
                    if (q.Template == EventRules.EndTale) End(chain, ChainStatus.Completed, "end_tale");
                    else if (chain.Events.Count >= ChainSkeletons.MaxEvents) End(chain, ChainStatus.Completed, "its full length");
                }
            }
            else if (story.Chains == ChainMode.Free)
            {
                chain = NewChain(e, q.Target, ChainMode.Free, null);
                chain.Events.Add(e);
            }
            if (chain == null) return;
            e.ChainId = chain.Id;
            e.ChainStage = chain.Events.Count;
        }

        private Chain NewChain(EventRecord e, string target, ChainMode mode, string skeleton)
        {
            var c = new Chain
            {
                Id = $"tale_{Events.Chains.Count + 1}",
                Mode = mode,
                Skeleton = skeleton,
                Subject = target,
                SubjectKind = EventRules.KindOf(World, target),
                Province = e.Province,
                Title = e.Title,
            };
            c.Countries.AddRange(e.Hit.Concat(e.Spared));
            Events.Chains.Add(c);
            return c;
        }

        private static void End(Chain c, ChainStatus status, string reason)
        {
            c.Status = status;
            c.EndReason = reason;
        }

        // At every upkeep: an open chain whose subject is out, or that nobody continued for a while, is abandoned.
        private void TendChains()
        {
            foreach (var c in Events.Chains.Where(c => c.Status == ChainStatus.Open))
            {
                if (c.Countries.All(id => World.Country(id)?.Alive != true)) End(c, ChainStatus.Abandoned, "its subject is out");
                else if (c.LastTurn < World.Turn - ChainSkeletons.IdleTurns) End(c, ChainStatus.Abandoned, "not continued");
            }
        }

        // ------------------------------------------------------------------------------------
        // 2. Council answers and envoy requests
        // ------------------------------------------------------------------------------------

        /// <summary>
        /// Takes every country's council move. A country with no answer holds. Returns one envoy
        /// request per offer of peace: the country offered peace answers it before anything lands.
        /// Two countries that offer each other peace in one turn make it without being asked.
        /// </summary>
        public IReadOnlyList<CountryRequest> SubmitCouncil(IEnumerable<CountryAnswer> answers)
        {
            Expect(Phase.AwaitingCouncil);
            var byCountry = (answers ?? Enumerable.Empty<CountryAnswer>())
                .Where(a => a?.Request != null && a.Request.Moment == Moment.Council)
                .GroupBy(a => a.Request.CountryId)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
            councilAnswers = councilRequests
                .Select(req => byCountry.TryGetValue(req.CountryId, out var a) ? new CountryAnswer(req, a.Choice ?? Move.Hold, a.Note) : new CountryAnswer(req, Move.Hold))
                .ToList();

            offers = new List<Offer>();
            foreach (var a in councilAnswers.Where(a => a.Choice.Action == Actions.OfferPeace && IsOffered(a)))
            {
                var mutual = offers.FirstOrDefault(o => o.From == a.Choice.Target && o.To == a.Request.CountryId);
                if (mutual != null)
                {
                    mutual.Mutual = true;
                    continue;
                }
                offers.Add(new Offer
                {
                    Id = $"t{World.Turn}-{a.Request.CountryId}-{a.Choice.Target}",
                    From = a.Request.CountryId,
                    To = a.Choice.Target,
                });
            }

            foreach (var o in offers.Where(o => !o.Mutual))
            {
                var turnFacts = new List<string> { $"{o.From} offers us peace: a truce for {Rules.TruceTurns} turns." };
                turnFacts.AddRange(Facts.Select(pending[o.To], Facts.MaxTurnFacts - 1));
                o.EnvoyRequest = new CountryRequest(Moment.Envoy, World.Turn, o.To, World,
                    Facts.Standing(World, o.To), turnFacts, Legality.EnvoyMoves(), o.Id, o.From);
            }

            report.PeaceOffers = offers.Count;
            phase = Phase.AwaitingEnvoys;
            return offers.Where(o => o.EnvoyRequest != null).Select(o => o.EnvoyRequest).ToList();
        }

        // ------------------------------------------------------------------------------------
        // 3. Resolution
        // ------------------------------------------------------------------------------------

        /// <summary>
        /// Lands the turn: peace first, then home moves (recruit, taxes, forts), then declarations
        /// of war, then attacks. Within each step countries go in an order that rotates every turn.
        /// Each move is re-checked first, so a move an earlier one made stale is refused. Then
        /// countries with no province left are out, and the game checks for a winner.
        /// </summary>
        public TurnReport Resolve(IEnumerable<CountryAnswer> envoyAnswers = null)
        {
            if (Rules.Turn == TurnStyle.Writs) return ResolveWrits(envoyAnswers);
            Expect(Phase.AwaitingEnvoys);
            foreach (var c in World.CountryList) pending[c.Id].Clear();

            var answers = (envoyAnswers ?? Enumerable.Empty<CountryAnswer>())
                .Where(a => a?.Request?.OfferId != null)
                .GroupBy(a => a.Request.OfferId)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

            var order = RotatedOrder();
            var councilByCountry = councilAnswers.ToDictionary(a => a.Request.CountryId, StringComparer.Ordinal);
            var done = new HashSet<string>(StringComparer.Ordinal);

            // Illegal on the snapshot itself: a player offered something it was not offered.
            foreach (var a in councilAnswers.Where(a => !IsOffered(a)))
            {
                report.Outcomes.Add(new MoveOutcome(a.Request, a.Choice, Move.Hold, true, $"{a.Choice} was not offered; held instead"));
                report.Resolution.Add($"{a.Request.CountryId} chose {a.Choice}, which it was not offered, and held instead.");
                done.Add(a.Request.CountryId);
            }

            ResolvePeace(answers, councilByCountry, done);

            foreach (string action in new[] { Actions.Recruit, Actions.RaiseTaxes, Actions.Fortify, Actions.DeclareWar, Actions.Attack })
                foreach (string id in order)
                {
                    if (done.Contains(id) || !councilByCountry.TryGetValue(id, out var a) || a.Choice.Action != action) continue;
                    done.Add(id);
                    Land(a);
                }

            foreach (var a in councilAnswers.Where(a => !done.Contains(a.Request.CountryId)))
            {
                Gate(a.Request, a.Choice);
                report.Outcomes.Add(new MoveOutcome(a.Request, a.Choice, Move.Hold, false, "held"));
            }

            var notes = councilAnswers.Concat(answers.Values).Where(a => a.Note != null)
                .GroupBy(a => a.Request).ToDictionary(g => g.Key, g => g.First().Note);
            foreach (var o in report.Outcomes)
                if (o.Request != null && notes.TryGetValue(o.Request, out string note)) o.Note = note;

            foreach (var c in World.Alive.ToList())
            {
                if (World.ProvinceCount(c.Id) > 0) continue;
                c.Alive = false;
                c.EliminatedOnTurn = World.Turn;
                report.Resolution.Add($"{c.Id} has lost its last province and is out of the game.");
            }

            report.OwnerGrid = World.OwnerGrid();
            foreach (var c in World.CountryList) report.ProvinceCounts[c.Id] = World.ProvinceCount(c.Id);
            Reports.Add(report);
            CheckForWinner();
            phase = IsOver ? Phase.Over : Phase.AwaitingTurn;
            return report;
        }

        private void ResolvePeace(Dictionary<string, CountryAnswer> answers, Dictionary<string, CountryAnswer> council, HashSet<string> done)
        {
            foreach (var o in offers)
            {
                var offerer = council[o.From];
                done.Add(o.From);
                string stale = Legality.WhyNot(World, o.From, offerer.Choice) ?? Gate(offerer.Request, offerer.Choice);
                if (stale != null)
                {
                    Refuse(offerer, stale);
                    if (o.EnvoyRequest != null)
                    {
                        answers.TryGetValue(o.Id, out var unanswered);
                        report.Outcomes.Add(new MoveOutcome(o.EnvoyRequest, unanswered?.Choice ?? new Move(Actions.RefusePeace),
                            Move.Hold, true, "the offer no longer stood"));
                    }
                    continue;
                }

                if (o.Mutual)
                {
                    done.Add(o.To);
                    council.TryGetValue(o.To, out var partner);
                    string partnerWhy = partner == null ? null : Gate(partner.Request, partner.Choice);
                    if (partnerWhy != null)
                    {
                        // The partner's own side was refused, so there is no truce to make.
                        Refuse(partner, partnerWhy);
                        report.Outcomes.Add(new MoveOutcome(offerer.Request, offerer.Choice, offerer.Choice, false, $"{o.To}'s offer was refused: no truce"));
                        report.Resolution.Add($"{o.From} offered peace, but {o.To}'s offer was refused: no truce.");
                        continue;
                    }
                    MakeTruce(o.From, o.To);
                    report.Outcomes.Add(new MoveOutcome(offerer.Request, offerer.Choice, offerer.Choice, false, $"{o.To} offered peace too: truce"));
                    if (council.TryGetValue(o.To, out var other))
                        report.Outcomes.Add(new MoveOutcome(other.Request, other.Choice, other.Choice, false, $"{o.From} offered peace too: truce"));
                    report.Resolution.Add($"{o.From} and {o.To} both offered peace: a truce for {Rules.TruceTurns} turns.");
                    continue;
                }

                answers.TryGetValue(o.Id, out var answer);
                var reply = answer?.Choice ?? new Move(Actions.RefusePeace);
                if (answer != null && Gate(o.EnvoyRequest, reply) != null) reply = new Move(Actions.RefusePeace);
                bool accepted = reply.Action == Actions.AcceptPeace;
                report.Outcomes.Add(new MoveOutcome(o.EnvoyRequest, reply, accepted ? reply : new Move(Actions.RefusePeace), false,
                    accepted ? $"accepted {o.From}'s peace" : $"refused {o.From}'s peace"));
                if (accepted)
                {
                    MakeTruce(o.From, o.To);
                    report.Outcomes.Add(new MoveOutcome(offerer.Request, offerer.Choice, offerer.Choice, false, $"{o.To} accepted: truce"));
                    report.Resolution.Add($"{o.To} accepted {o.From}'s offer of peace: a truce for {Rules.TruceTurns} turns.");
                    Tell(o.From, 2, $"{o.To} accepted our peace: a truce for {Rules.TruceTurns} turns.");
                    Tell(o.To, 2, $"We accepted peace with {o.From}: a truce for {Rules.TruceTurns} turns.");
                }
                else
                {
                    report.Outcomes.Add(new MoveOutcome(offerer.Request, offerer.Choice, offerer.Choice, false, $"{o.To} refused"));
                    report.Resolution.Add($"{o.To} refused {o.From}'s offer of peace.");
                    Tell(o.From, 1, $"{o.To} refused our offer of peace.");
                }
            }
        }

        private void MakeTruce(string a, string b)
        {
            World.SetRelation(a, b, Relation.Truce, Rules.TruceTurns);
            report.PeaceAccepted++;
        }

        private void Land(CountryAnswer a)
        {
            string id = a.Request.CountryId;
            var move = a.Choice;
            string why = Legality.WhyNot(World, id, move);
            if (why != null && move.Action == Actions.Attack && World.Province(move.Target) is Province taken
                && ownersAtCouncil.TryGetValue(taken.Id, out string before) && before != taken.Owner && taken.Owner != null)
                why = $"{taken.Owner} took {taken.Id} first";
            why = why ?? Gate(a.Request, move);
            if (why != null)
            {
                Refuse(a, why);
                return;
            }

            var me = World.Country(id);
            var r = Rules;
            switch (move.Action)
            {
                case Actions.Recruit:
                {
                    int levy = World.Levy(me);
                    me.Gold -= levy * r.GoldPerSoldier;
                    me.Army += levy;
                    Done(a, $"recruited {levy} soldiers for {levy * r.GoldPerSoldier} gold");
                    break;
                }

                case Actions.RaiseTaxes:
                    me.Gold += r.TaxGold;
                    me.Stability--;
                    me.RaisedTaxes = true;
                    Done(a, $"raised {r.TaxGold} gold in taxes");
                    break;

                case Actions.Fortify:
                    me.Gold -= r.FortCost;
                    World.Province(move.Target).Forts++;
                    Done(a, $"built a fort in {move.Target}");
                    break;

                case Actions.DeclareWar:
                    World.SetRelation(id, move.Target, Relation.War);
                    Done(a, $"declared war on {move.Target}");
                    Tell(move.Target, 0, $"{id} declared war on us.");
                    break;

                case Actions.Attack:
                    Battle(a);
                    break;
            }
        }

        private void Battle(CountryAnswer a)
        {
            string id = a.Request.CountryId;
            var me = World.Country(id);
            var p = World.Province(a.Choice.Target);
            var r = Rules;
            string defenderId = p.Owner;
            var defender = World.Country(defenderId);
            report.Battles++;

            int attack = (int)Math.Round(me.Army * Dice.Range(Seed, World.Turn, id, "attack:" + p.Id, r.RollMin, r.RollMax));
            double defRoll = Dice.Range(Seed, World.Turn, p.Id, "defend:" + id, r.RollMin, r.RollMax);
            int defence = defender == null
                ? (int)Math.Round(p.Garrison * defRoll) + p.Forts * r.FortBonus
                : (int)Math.Round(defender.Army * r.DefenceShare * defRoll) + p.Forts * r.FortBonus + (p.IsCapital ? r.CapitalBonus : 0);

            bool won = attack > defence;
            int myLoss = Math.Min(me.Army, Math.Max(1, (int)Math.Round(defence * r.CasualtyRate)));
            me.Army -= myLoss;
            int theirLoss;
            if (defender == null)
            {
                theirLoss = won ? p.Garrison : Math.Min(p.Garrison - 1, Math.Max(1, (int)Math.Round(attack * r.CasualtyRate * r.HeldCasualtyFactor)));
                p.Garrison -= Math.Max(0, theirLoss);
            }
            else
            {
                double share = won ? 1.0 : r.HeldCasualtyFactor;
                theirLoss = Math.Min(defender.Army, Math.Max(1, (int)Math.Round(attack * r.CasualtyRate * share)));
                defender.Army -= theirLoss;
            }

            string from = defender == null ? "the neutrals" : defenderId;
            string score = $"{attack} against {defence}";
            if (won)
            {
                p.Owner = id;
                p.Garrison = 0;
                p.Forts = Math.Max(0, p.Forts - 1);
                report.TerritoryChanges++;
                Done(a, $"took {p.Id} from {from} ({score}); lost {myLoss}");
                Tell(id, defender == null ? 2 : 1, $"We took {p.Id} from {from}; we lost {myLoss} soldiers.");
                if (defender != null) Tell(defenderId, 0, $"{id} took {p.Id} from us; we lost {theirLoss} soldiers.");
            }
            else
            {
                Done(a, $"attack on {p.Id} failed ({score}); lost {myLoss}");
                Tell(id, 1, $"Our attack on {p.Id} failed; we lost {myLoss} soldiers.");
                if (defender != null) Tell(defenderId, 1, $"We held {p.Id} against {id}; we lost {theirLoss} soldiers.");
            }
        }

        private void Done(CountryAnswer a, string text)
        {
            report.Outcomes.Add(new MoveOutcome(a.Request, a.Choice, a.Choice, false, text));
            report.Resolution.Add($"{a.Request.CountryId} {text}.");
        }

        private void Refuse(CountryAnswer a, string why)
        {
            report.StaleRefusals++;
            report.Outcomes.Add(new MoveOutcome(a.Request, a.Choice, Move.Hold, true, "refused: " + why));
            report.Resolution.Add($"{a.Request.CountryId}'s {a.Choice} was refused: {why}.");
            Tell(a.Request.CountryId, 2, $"Our {Phrase(a.Choice)} could not happen: {why}.");
        }

        /// <summary>A move as a noun, for a sentence: "attack on fenmark", "levy".</summary>
        private static string Phrase(Move m)
        {
            switch (m.Action)
            {
                case Actions.Attack: return $"attack on {m.Target}";
                case Actions.Fortify: return $"fort in {m.Target}";
                case Actions.DeclareWar: return $"declaration of war on {m.Target}";
                case Actions.OfferPeace: return $"offer of peace to {m.Target}";
                case Actions.Recruit: return "levy";
                case Actions.RaiseTaxes: return "tax rise";
                default: return m.ToString();
            }
        }

        private void CheckForWinner()
        {
            var alive = World.Alive.ToList();
            var counts = World.CountryList.ToDictionary(c => c.Id, c => World.ProvinceCount(c.Id), StringComparer.Ordinal);
            if (alive.Count == 1)
            {
                Result = new GameResult(alive[0].Id, Ending.LastStanding, World.Turn, counts);
                return;
            }
            var dominant = Rules.Victory == VictoryMode.Domination
                ? alive.FirstOrDefault(c => counts[c.Id] >= Rules.ProvincesToWin)
                : null;
            if (dominant != null)
            {
                Result = new GameResult(dominant.Id, Ending.Domination, World.Turn, counts);
                return;
            }
            if (World.Turn >= Rules.MaxTurns)
            {
                var best = alive
                    .OrderByDescending(c => counts[c.Id])
                    .ThenByDescending(c => c.Army)
                    .ThenByDescending(c => c.Gold)
                    .First();
                Result = new GameResult(best.Id, Rules.Victory == VictoryMode.Conquest ? Ending.Unfinished : Ending.TurnLimit, World.Turn, counts);
            }
        }

        /// <summary>
        /// Living countries in seat order, starting one seat later each turn, so nobody always
        /// moves first within a step.
        /// </summary>
        private List<string> RotatedOrder()
        {
            var seats = World.CountryList;
            int start = (World.Turn - 1) % seats.Count;
            var order = new List<string>();
            for (int i = 0; i < seats.Count; i++)
            {
                var c = seats[(start + i) % seats.Count];
                if (c.Alive) order.Add(c.Id);
            }
            return order;
        }

        private string Gate(CountryRequest request, Move move) =>
            request != null && Gates.TryGetValue(request.CountryId, out var gate) && gate != null ? gate.Admit(request, move) : null;

        private static bool IsOffered(CountryAnswer a) => a.Request.Legal.Contains(a.Choice);

        private void Tell(string countryId, int priority, string line)
        {
            if (countryId != null && pending.TryGetValue(countryId, out var list)) list.Add(new TurnFact(priority, line));
        }

        private void Expect(Phase expected)
        {
            if (phase == Phase.Over) throw new InvalidOperationException("The game is over.");
            if (phase != expected)
                throw new InvalidOperationException($"Expected {expected}, but the game is at {phase}.");
        }
    }
}

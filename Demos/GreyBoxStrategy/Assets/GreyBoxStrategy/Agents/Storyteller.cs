using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Agenerela;
using Agenerela.Providers;
using UnityEngine;

namespace GreyBoxStrategy
{
    /// <summary>What one storyteller call came to, for the run's tables.</summary>
    public sealed class StorytellerDecision
    {
        public int Seed;
        /// <summary>Why the history guard replaced the story (#292), or null; <see cref="Statement"/> stays the model's.</summary>
        public string HistoryReplaced;
        /// <summary>The turn the event was for (the upkeep it lands at).</summary>
        public int Turn;
        /// <summary>What the model answered, before any guard.</summary>
        public string ModelAction;
        public string ModelTarget;
        /// <summary>What the agent decided, after the guards.</summary>
        public string Action;
        public string Target;
        public string Statement;
        public float LatencySeconds;
        public int PromptTokens;
        public string Guard;
        public GuardVerdictKind? Verdict;
        public string GuardReason;
        public PipelineErrorKind? Error;
        /// <summary>Whether the model's own answer was an event the game offered (or none).</summary>
        public bool ModelAnswerLegal;
        /// <summary>What <c>Agent.Execute</c> did, or null when there was nothing to execute.</summary>
        public ExecutionStatus? Execution;
        public string ExecutionReason;
        /// <summary>Whether an event was queued for the upkeep.</summary>
        public bool Queued;
        /// <summary>How many events were open to it (template and target pairs).</summary>
        public int Open;
        /// <summary>The title call's answer and seconds (<c>+title-call</c>); null and 0 without one.</summary>
        public string TitleStatement;
        public float TitleLatencySeconds;
        /// <summary>The strength the model asked (from the action id or the parameter), and the one decided; null without one (DR-023's A/B).</summary>
        public string ModelStrength;
        public string Strength;
        /// <summary>How many parameters took their default, and how many values a per-value check left out, from the telemetry.</summary>
        public int ArgumentsDefaulted;
        public int ValuesUnavailable;
        public int CompletionTokens;
        /// <summary>True when the preset's chance to act said not this turn: no call was made.</summary>
        public bool Skipped;

        public bool Contained => Verdict == GuardVerdictKind.RewriteToNone;
    }

    /// <summary>A target the storyteller can name: a country, a group or a province, described as it stands.</summary>
    internal sealed class EventTarget : IDescribedTarget
    {
        private readonly Game game;

        public EventTarget(Game game, string id)
        {
            this.game = game;
            Id = id;
        }

        public string Id { get; }
        public string Description => Storyteller.Describe(game, Id);
    }

    /// <summary>The storyteller's targets: the living countries, the groups and the provinces, in categories <c>country</c>, <c>group</c> and <c>province</c>.</summary>
    internal sealed class EventTargetSource : ITargetSource
    {
        private readonly Game game;

        public EventTargetSource(Game game)
        {
            this.game = game;
        }

        public void Collect(AgentContext ctx, TargetRegistry into)
        {
            var world = game.World;
            foreach (var c in world.Alive) into.Register(c.Id, new EventTarget(game, c.Id), Storyteller.CountryCategory);
            foreach (string g in EventGroups.All) into.Register(g, new EventTarget(game, g), Storyteller.GroupCategory);
            foreach (var p in world.ProvinceList.Where(p => p.Owner != null))
                into.Register(p.Id, new EventTarget(game, p.Id), Storyteller.ProvinceCategory);
            // S2: the tales that can still be continued, in a category of their own.
            if (game.Story.Chains != ChainMode.None)
                foreach (var c in game.Chains.Where(c => c.IsOpenTarget && c.LastTurn >= game.NextTurn - ChainSkeletons.IdleTurns))
                    into.Register(c.Id, new EventTarget(game, c.Id), Storyteller.TaleCategory);
        }
    }

    /// <summary>
    /// What the storyteller reads each turn, from the game as it stands: the turn, every country's
    /// standing, the events open now, what it already told (not its replies: the research page's
    /// gap 6), and an inspiration line drawn from the seed.
    /// </summary>
    internal sealed class StorytellerFacts : IObservationSource
    {
        private readonly Storyteller teller;

        public StorytellerFacts(Storyteller teller)
        {
            this.teller = teller;
        }

        public IEnumerable<string> Observe(AgentContext ctx) => teller.StandingFacts();
    }

    /// <summary>
    /// The storyteller: a framework <see cref="Agent"/> with no body (DR-011), asked once a turn at
    /// upkeep, from <see cref="GameRules.EventFirstTurn"/>, to make one thing happen or nothing. Its
    /// actions are the game's effect templates (<see cref="EventRules.Templates"/>), its targets the
    /// countries, the groups (<see cref="EventGroups"/>) and the provinces, and the paired schema
    /// keeps every illegal pair out. Every fairness rule is availability (<see cref="EventRules.WhyNot"/>).
    /// The model writes only the title and the story; the game decides the strength, checks the
    /// story and tells the rulers (<see cref="Game.QueueEvent"/>).
    /// </summary>
    public sealed class Storyteller
    {
        public const string AgentId = "storyteller";
        public const string CountryCategory = "country";
        public const string GroupCategory = "group";
        public const string ProvinceCategory = "province";
        public const string TaleCategory = "tale";
        public const string ScribeId = "scribe";
        /// <summary>How many past events the "Told so far" line names.</summary>
        public const int ToldLines = 5;

        /// <summary>
        /// Words for the inspiration line, two a call, drawn from the seed, the turn and the salt, so a
        /// run is reproducible and two runs with different salts draw differently. Images, not plots:
        /// a seed for the story, never an instruction.
        /// </summary>
        public static readonly IReadOnlyList<string> Inspirations = new[]
        {
            "a drowned bell", "lanterns", "a white stag", "salt", "a broken crown", "ravens", "a red comet", "old bones",
            "a silver mine", "fog", "a pilgrim", "honey", "a burning ship", "wolves", "a sealed letter", "ash",
            "a blind prophet", "frost", "a golden meteor", "eels", "a cracked bridge", "a feast", "a stolen relic", "moths",
            "a flood", "a wandering knight", "copper bells", "a black tide", "a hermit", "lightning", "a buried hoard", "crows",
            "a mad bishop", "amber", "a child king", "smoke", "a sunken road", "a lost army", "rain of frogs", "a bright star",
            "a traitor", "wine", "a giant's bones", "locusts", "a lighthouse", "a duel", "a holy spring", "hailstones",
            "a runaway bride", "iron", "a talking well", "embers", "a rope bridge", "a fortune teller", "snow in summer", "a toll gate",
            "a hidden chapel", "thunder", "a stranger's ship", "a great fish", "a cursed sword", "pearls", "a weeping statue", "bees",
        };

        private readonly Game game;
        private readonly ModelSeatOptions options;

        public Storyteller(Game game, ILLMProvider provider, ModelSeatOptions options = null)
        {
            this.game = game ?? throw new ArgumentNullException(nameof(game));
            this.options = options ?? ModelSeatOptions.Default;
            var w = game.World;
            rules = game.Story;
            string countries = EventRules.Join(w.CountryList.Select(c => c.Id).ToList());
            var identity = new AgentIdentity
            {
                Name = "The Chronicler",
                Role = $"the storyteller of a game in which {CountryAgent.CountWord(w.CountryList.Count)} countries ({countries}) fight over {w.ProvinceList.Count} provinces. "
                       + "Once a turn you may make one thing happen: to a country, a group of countries or a province",
                Personality = "A wry chronicler of fortune and calamity, fond of omens, weather, saints and strange luck.",
                Goals = "Make every game memorable and different from the last, with events nobody saw coming. Keep it fair: "
                        + "help those falling behind as often as you trouble the strong. Choose none when nothing should happen.",
                Knowledge = new List<string>
                {
                    EventGroups.KnowledgeLine(w), // on Classic: "the_north is valen and orsk; the_south is kesh and ilvar", as measured
                    "The game decides how strong an event is and tells the rulers the numbers; you tell the story.",
                },
                StatementRules = "A title of two to five words, a colon, then one or two sentences telling what happened, "
                                 + "as in \"Title Of The Event: what happened to whom.\" "
                                 + "Name only the country or province it strikes. No numbers.",
            };
            // Stage S2: what the new pieces need it to know, only when they are on (S1's prompt is unchanged otherwise).
            if (rules.Choices) identity.Knowledge.Add("A dilemma asks the ruler it strikes to choose between options the game offers; tell the situation, not the choice.");
            if (rules.Chains == ChainMode.Free)
                identity.Knowledge.Add("A tale is a story over several turns: an event on a tale_ target continues that tale and strikes its subject again; end_tale closes it. Keep tales rare and worth following.");
            if (rules.Chains == ChainMode.Skeleton)
                identity.Knowledge.Add("A begin_ action starts a tale the game has planned; while a tale_ target is listed, its next event continues that tale. Tell how each follows from the last.");
            if (rules.History)
                identity.Knowledge.Add("The recent happenings line is what really happened: a story may follow from it and name those in it, but never invent a battle or a capture.");
            if (rules.Traits) identity.Knowledge.Add("Every province has a terrain, a landmark and a resource; a story may use them.");
            if (rules.Settings.StoryOnly && rules.IsS2) identity.Knowledge.Add("omen is a story only, with no effect.");
            if (rules.TitleCall)
                identity.StatementRules = "One or two sentences telling what happened, and where. Name only the countries and places it strikes"
                                          + (rules.History ? ", or those in the recent happenings" : "") + ". No numbers.";
            Agent = new Agent(identity, Array.Empty<ActionDefinition>(), provider)
            {
                Id = AgentId,
                MaxObservations = 12,
                // No memory of its replies: the "Told so far" line says what happened instead (gap 6).
                Memory = null,
            };
            // +history: the history check is a guard that replaces the story and keeps the event (#292).
            if (rules.History && this.options.HistoryGuard) Agent.Guards.Add(new HistoryGuard(game));
            Agent.TargetSources.Add(new EventTargetSource(game));
            Agent.ObservationSources.Add(new StorytellerFacts(this));
            foreach (var t in EventRules.TemplatesFor(rules)) Register(t);
            if (rules.TitleCall) Scribe = NewScribe(provider);
        }

        private readonly StoryRules rules;

        /// <summary>The second agent that titles each story (<c>+title-call</c>), or null.</summary>
        public Agent Scribe { get; }

        private static Agent NewScribe(ILLMProvider provider)
        {
            var identity = new AgentIdentity
            {
                Name = "The Chronicler's scribe",
                Role = "the scribe who gives each event in a chronicle its title",
                Personality = "Brief and vivid.",
                Goals = "A title a reader remembers.",
                StatementRules = "Only the title: two to five words, as in \"The Weeping Statue\" or \"Salt in the Wells\". No numbers.",
            };
            var scribe = new Agent(identity, Array.Empty<ActionDefinition>(), provider) { Id = ScribeId, Memory = null, MaxObservations = 4 };
            scribe.Actions.Register(new ActionDefinition { Id = "title", Description = "Give the event its title.", TakesNoTarget = true },
                DelegateActionHandler.WithResult(ctx => true, (ctx, d) => ActionResult.Done()));
            return scribe;
        }

        // The title the scribe gave the event being queued, read by the handler.
        private string calledTitle;
        // What the history guard did to the event being queued (its reason, and the model's own words), read by the handler.
        private string historyReplaced, modelStatement;

        /// <summary>
        /// The history check as a guard (#292): a story that tells of a fight that never happened is replaced
        /// by the template's own line, under the model's title, and the event stands. The game's
        /// <see cref="Game.HistoryVerdict"/> decides, so the verdict is the one the event's check gave before.
        /// </summary>
        public sealed class HistoryGuard : IDecisionGuard
        {
            public const string GuardName = "history";
            private readonly Game game;
            public HistoryGuard(Game game) { this.game = game; }
            public string Name => GuardName;

            public GuardVerdict Inspect(GuardInput input)
            {
                var d = input.Decision;
                if (d == null || d.ActionId == ActionRegistry.None) return GuardVerdict.Pass;
                var v = game.HistoryVerdict(d.ActionId, d.TargetId, d.Statement);
                return v == null ? GuardVerdict.Pass : GuardVerdict.ReplaceStatement(v.Value.reason, v.Value.statement);
            }
        }

        /// <summary>A target's line in the storyteller's list: S1's, with a province's traits and a tale's story so far in S2.</summary>
        public static string Describe(Game game, string id)
        {
            var w = game.World;
            var chain = game.Events.Chain(id);
            if (chain != null) return DescribeTale(game, chain);
            string line = EventRules.Describe(w, id);
            if (game.Story.Traits && w.IsProvince(id)) line += "; " + w.Traits[id].Line;
            return line;
        }

        private static string DescribeTale(Game game, Chain c)
        {
            string events = string.Join("; ", c.Events.Select(e =>
                $"turn {e.Turn} {e.Template}" + (e.Choices.Count > 0 ? ", " + string.Join(", ", e.Choices.Select(kv => $"{kv.Key} chose {kv.Value}")) : "")));
            string line = $"the tale \"{c.Title}\" of {c.Subject}: {events}";
            if (c.Mode == ChainMode.Skeleton)
            {
                var k = ChainSkeletons.Named(c.Skeleton);
                int next = ChainSkeletons.NextStage(k, c);
                if (next >= 0) line += $"; next: {string.Join(" or ", k.Stages[next].Templates)}";
            }
            return line;
        }

        public Agent Agent { get; }
        public int Seed => game.Seed;
        /// <summary>Every call, in order.</summary>
        public List<StorytellerDecision> Decisions { get; } = new List<StorytellerDecision>();

        /// <summary>The strength parameter of an event in the parameter form (DR-023): its line in the prompt says what each value does.</summary>
        public static ActionParameter StrengthParameter() => new ActionParameter
        {
            Name = "strength",
            Description = "how hard it strikes: minor about half of that, major about one and a half times",
            Values = EventRules.Strengths.ToArray(),
        };

        /// <summary>The bundled form's description of one strength of an event: the event's own line and what the strength does to it.</summary>
        public static string BundledDescription(EventTemplate t, string strength) =>
            t.Description + (strength == EventRules.Minor ? " Minor: about half of that." : " Major: about one and a half times that.");

        // An event with an amount in a game that asks a strength (GameRules.EventStrength): two actions in the
        // bundled form, one with a parameter in the other. Every other event is registered as before.
        private void Register(EventTemplate t)
        {
            var form = game.Rules.EventStrength;
            if (form == EventStrengthForm.Bundled && EventRules.TakesStrength(t.Id))
            {
                foreach (string s in EventRules.Strengths) Register(t, EventRules.BundledId(t.Id, s), BundledDescription(t, s), s);
                return;
            }
            Register(t, t.Id, t.Description, null);
        }

        private void Register(EventTemplate t, string actionId, string description, string strength)
        {
            bool parameter = game.Rules.EventStrength == EventStrengthForm.Parameter && EventRules.TakesStrength(t.Id);
            var categories = new List<string>();
            bool taleOnly = t.TaleOnly || t.Feature == EventFeature.ChainDilemma;
            if (!taleOnly && (t.Takes & EventTargetKind.Country) != 0) categories.Add(CountryCategory);
            if (!taleOnly && (t.Takes & EventTargetKind.Group) != 0) categories.Add(GroupCategory);
            if (!taleOnly && (t.Takes & EventTargetKind.Province) != 0) categories.Add(ProvinceCategory);
            if (rules.Chains != ChainMode.None && t.Skeleton == null) categories.Add(TaleCategory);
            var definition = new ActionDefinition
            {
                Id = actionId,
                Description = description,
                RequiresTarget = true,
                TargetCategories = categories.ToArray(),
                Parameters = parameter ? new[] { StrengthParameter() } : null,
            };
            var handler = DelegateActionHandler.WithResult(
                // In the parameter form the action is open when some target is; the strength is the value check's.
                ctx => EventRules.TargetIds(game.World, game.Events).Any(id => game.WhyNotEvent(t.Id, id, strength) == null
                                                                              || (parameter && game.WhyNotEvent(t.Id, id) == null)),
                (ctx, d) =>
                {
                    string s = strength;
                    if (parameter) d.TryGetArgument("strength", out s);
                    game.QueueEvent(new StoryEvent(t.Id, d.TargetId, d.Statement, calledTitle)
                    {
                        HistoryReplaced = historyReplaced,
                        ModelStatement = historyReplaced != null ? modelStatement : null,
                        Strength = s,
                    });
                    return ActionResult.Done();
                });
            handler.IsAvailableFor = (ctx, target) =>
            {
                string why = parameter ? game.WhyNotEvent(t.Id, target) : game.WhyNotEvent(t.Id, target, strength);
                return why == null ? Availability.Yes : Availability.No(why);
            };
            if (parameter)
                handler.IsAvailableValue = (ctx, name, value) =>
                {
                    string why = EventRules.WhyNotStrength(game.World, game.Events, t.Id, value);
                    return why == null ? Availability.Yes : Availability.No(why);
                };
            Agent.Actions.Register(definition, handler);
        }

        /// <summary>
        /// Every event open now as the model sees it, as (action id, target) pairs: S1's list in a game
        /// that asks no strength; the bundled ids (<c>windfall_major</c>) with their strength's rule in the
        /// bundled form; the event's own id, open when some strength is, in the parameter form.
        /// </summary>
        public List<(string action, string target)> OpenActions()
        {
            var open = EventRules.Open(game.World, game.Events, game.NextTurn, rules);
            var form = game.Rules.EventStrength;
            if (form == EventStrengthForm.None) return open;
            var list = new List<(string, string)>();
            foreach (var (template, target) in open)
            {
                if (!EventRules.TakesStrength(template)) list.Add((template, target));
                else if (form == EventStrengthForm.Bundled)
                {
                    foreach (string s in EventRules.Strengths)
                        if (EventRules.WhyNotStrength(game.World, game.Events, template, s) == null) list.Add((EventRules.BundledId(template, s), target));
                }
                else if (EventRules.Strengths.Any(s => EventRules.WhyNotStrength(game.World, game.Events, template, s) == null)) list.Add((template, target));
            }
            return list;
        }

        // --- What it reads --------------------------------------------------------------------

        /// <summary>The storyteller's standing facts for the next upkeep, in order.</summary>
        public List<string> StandingFacts()
        {
            var w = game.World;
            int turn = game.NextTurn;
            var lines = new List<string>();
            var alive = w.Alive.ToList();
            int most = alive.Count == 0 ? 0 : alive.Max(c => w.ProvinceCount(c.Id));
            lines.Add($"Turn {turn} of {w.Rules.MaxTurns}. The largest country holds {most} of {w.ProvinceList.Count} provinces; {w.Rules.ProvincesToWin} wins.");
            foreach (var c in alive)
            {
                var wars = w.EnemiesOf(c.Id).Select(e => e.Id).ToList();
                string war = wars.Count == 0 ? "at peace" : "at war with " + EventRules.Join(wars);
                lines.Add($"{c.Id}: {Facts.Provinces(w.ProvinceCount(c.Id))}, army {c.Army}, {c.Gold} gold, stability {c.Stability}; {war}.");
            }
            string open = OpenLine();
            if (open != null) lines.Add(open);
            if (rules.History) lines.Add(HappeningsLine());
            lines.Add(ToldLine());
            if (options.Inspiration) lines.Add(InspirationLine(turn));
            return lines;
        }

        /// <summary>The events open now, by template, in the game's words: the N1 workaround, as the rulers' moves line is.</summary>
        public string OpenLine()
        {
            var open = OpenActions();
            if (open.Count == 0) return null;
            var parts = new List<string>();
            foreach (var t in EventRules.TemplatesFor(rules))
            {
                var ids = game.Rules.EventStrength == EventStrengthForm.Bundled && EventRules.TakesStrength(t.Id)
                    ? EventRules.Strengths.Select(s => EventRules.BundledId(t.Id, s)).ToList()
                    : new List<string> { t.Id };
                foreach (string id in ids)
                {
                    var targets = open.Where(o => o.action == id).Select(o => o.target).ToList();
                    if (targets.Count == 0) continue;
                    string list = targets.Count == 1 ? targets[0] : string.Join(", ", targets.Take(targets.Count - 1)) + " or " + targets[targets.Count - 1];
                    parts.Add($"{id} {((t.Takes & EventTargetKind.Province) != 0 ? "in" : "on")} {list}");
                }
            }
            return "Events open now: " + string.Join("; ", parts) + ".";
        }

        /// <summary>"Told so far: ..." with the last few events, or that nothing has been told yet.</summary>
        public string ToldLine()
        {
            var told = game.Events.LastTold(ToldLines);
            if (told.Count == 0) return "Told so far: nothing yet.";
            // With the title call, by template and target only: no title to copy (S1's finding).
            if (rules.TitleCall)
                return "Told so far: " + string.Join("; ", told.Select(e =>
                    $"{e.Template} {(e.Province != null ? "in" : "on")} {e.Target} (turn {e.Turn})")) + ".";
            return "Told so far: " + string.Join("; ", told.Select(e =>
                $"\"{e.Title}\" ({e.Template} {(e.Province != null ? "in" : "on")} {e.Target}, turn {e.Turn})")) + ".";
        }

        /// <summary>Two words drawn from <see cref="Inspirations"/> by the seed, the turn and the arm's salt.</summary>
        public string InspirationLine(int turn)
        {
            string purpose = "inspiration-" + options.InspirationSalt;
            int n = Inspirations.Count;
            int a = Dice.Int(game.Seed, turn, AgentId, purpose + "-a", 0, n);
            int b = Dice.Int(game.Seed, turn, AgentId, purpose + "-b", 0, n - 1);
            if (b >= a) b++;
            return $"Inspiration, if you want it: {Inspirations[a]}, {Inspirations[b]}.";
        }

        /// <summary>"Recent happenings: turn 7: kesh took fenmark from orsk; ...", from the game's reports (<c>+history</c>).</summary>
        public string HappeningsLine()
        {
            var recent = History.Recent(History.Of(game), game.NextTurn);
            if (recent.Count == 0) return "Recent happenings: none worth telling yet.";
            return "Recent happenings: " + string.Join("; ", recent.Select(h => $"turn {h.Turn}: {h.Text}")) + ".";
        }

        public string Stimulus() => $"Turn {game.NextTurn}. What happens?";

        /// <summary>The title call's options (<c>+title-call</c>).</summary>
        public DecideOptions TitleOptions() => new DecideOptions
        {
            StimulusLabel = "Title",
            ListActionsAndTargets = true,
            Priority = DecisionPriority.Background,
            CheckTarget = false,
            StatementRulesWording = options.RulesWording,
            MaxReplyTokens = options.StorytellerMaxReplyTokens,
        };

        public DecideOptions OptionsForCall() => new DecideOptions
        {
            StimulusLabel = "Upkeep",
            ListActionsAndTargets = true,
            MaxListedTargets = 40,
            Priority = DecisionPriority.Background,
            CheckTarget = options.CheckTarget,
            TargetGroundingThreshold = options.GroundingThreshold,
            StatementRulesWording = options.RulesWording,
            PairTargetsWithActions = true,
            MaxReplyTokens = options.StorytellerMaxReplyTokens,
        };

        // --- Asking it ------------------------------------------------------------------------

        /// <summary>
        /// Asks for the next upkeep's event, and queues it on the game when there is one. Not asked
        /// before <see cref="GameRules.EventFirstTurn"/>, nor when no event is open; then returns null.
        /// </summary>
        /// <summary>What the scribe is asked: the event in the game's words, and the story.</summary>
        public string TitleStimulus(AgentDecision d)
        {
            var t = EventRules.Template(EventRules.TryParseBundled(d.ActionId, out string bundled, out _) ? bundled : d.ActionId);
            string story = EventRules.SplitTitle(d.Statement ?? "").story;
            return $"Title this event. {t?.Description} The story: \"{(story.Length > 0 ? story : d.Statement)}\"";
        }

        // The strength a decision carries: in the id (bundled) or as the parameter; null when none.
        private string StrengthOf(AgentDecision d)
        {
            if (d == null) return null;
            if (EventRules.TryParseBundled(d.ActionId, out _, out string s)) return s;
            return d.TryGetArgument("strength", out s) ? s : null;
        }

        public async Awaitable<StorytellerDecision> TellAsync(CancellationToken ct = default)
        {
            if (game.IsOver || game.NextTurn < game.Rules.EventFirstTurn) return null;
            // The preset's chance to act at all this turn (1 in S1: always asked).
            if (Dice.Unit(game.Seed, game.NextTurn, AgentId, "act") >= rules.Settings.ActChance)
            {
                var skipped = new StorytellerDecision { Seed = game.Seed, Turn = game.NextTurn, Skipped = true };
                Decisions.Add(skipped);
                return skipped;
            }
            var open = OpenActions();
            if (open.Count == 0) return null;

            var result = await Agent.DecideAsync(Stimulus(), OptionsForCall(), ct);
            var t = result?.Telemetry;
            var record = new StorytellerDecision
            {
                Seed = game.Seed,
                Turn = game.NextTurn,
                Open = open.Count,
                ModelAction = result?.ModelDecision?.ActionId,
                ModelTarget = result?.ModelDecision?.TargetId,
                Action = result?.Decision?.ActionId,
                Target = result?.Decision?.TargetId,
                // The model's own words, as before the history check became a guard (#292).
                Statement = result?.ModelDecision?.Statement ?? result?.Decision?.Statement,
                LatencySeconds = t?.LatencySeconds ?? 0,
                PromptTokens = t?.PromptTokens ?? 0,
                Verdict = t?.Verdict,
                GuardReason = t?.GuardReason,
                Guard = t != null && t.GuardsFired.Count > 0 ? t.GuardsFired[0] : null,
                Error = result == null ? PipelineErrorKind.ProviderFailed : t?.ErrorKind,
            };
            record.ModelStrength = StrengthOf(result?.ModelDecision);
            record.Strength = StrengthOf(result?.Decision);
            record.ArgumentsDefaulted = t?.ArgumentsDefaulted?.Count ?? 0;
            record.ValuesUnavailable = t?.ValuesUnavailable?.Count ?? 0;
            record.CompletionTokens = t?.CompletionTokens ?? 0;
            string asked = record.ModelAction;
            // Legal: an event offered on that target, and in the parameter form a strength offered for it.
            record.ModelAnswerLegal = asked != null && (asked == ActionRegistry.None
                || (open.Any(o => o.action == asked && o.target == record.ModelTarget)
                    && (game.Rules.EventStrength != EventStrengthForm.Parameter || !EventRules.TakesStrength(asked)
                        || (record.ModelStrength != null && EventRules.WhyNotStrength(game.World, game.Events, asked, record.ModelStrength) == null))));
            Decisions.Add(record);

            var d = result?.Decision;
            if (d == null || d.ActionId == ActionRegistry.None) return record;
            calledTitle = null;
            if (Scribe != null)
            {
                var named = await Scribe.DecideAsync(TitleStimulus(d), TitleOptions(), ct);
                record.TitleStatement = named?.Decision?.Statement ?? named?.ModelDecision?.Statement;
                record.TitleLatencySeconds = named?.Telemetry?.LatencySeconds ?? 0;
                calledTitle = record.TitleStatement;
            }
            historyReplaced = t?.StatementsReplaced.FirstOrDefault(x => x.Guard == HistoryGuard.GuardName)?.Reason;
            modelStatement = result.ModelDecision?.Statement;
            record.HistoryReplaced = historyReplaced;
            Agent.Execute(result, out var outcome);
            calledTitle = historyReplaced = modelStatement = null;
            record.Execution = outcome.Status;
            record.ExecutionReason = outcome.Reason;
            record.Queued = outcome.Succeeded;
            return record;
        }
    }
}

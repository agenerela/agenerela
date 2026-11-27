using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Agenerela.Providers;
using GreyBoxStrategy.Realm;
using GreyBoxStrategy.Replay;
using NUnit.Framework;
using UnityEngine;

namespace GreyBoxStrategy.Screen.Tests
{
    /// <summary>
    /// The storyteller on screen, its non-visual parts: the arm the menu builds, a person's dilemma
    /// stopping the turn until it is answered, and a storyteller game's events, choices and tales
    /// rebuilt exactly from the recording the screen plays. A stand-in storyteller (ScriptedStory)
    /// plays through the real Storyteller agent; the four rulers hold.
    /// </summary>
    public class StoryTests
    {
        private const string You = "kesh";
        private string folder;

        [SetUp] public void SetUp() { folder = Path.Combine(Application.temporaryCachePath, "story-tests-" + Guid.NewGuid().ToString("N")); DevFaults.Clear(); }
        [TearDown] public void TearDown() { try { Directory.Delete(folder, true); } catch (IOException) { } }

        private static ScriptedProvider Stand_ins(int seed)
        {
            var rng = new System.Random(seed);
            return new ScriptedProvider
            {
                Callback = r => r.AgentId == Recording.StorytellerId ? ScriptedStory.Tell(r, rng)
                    : r.AgentId == Recording.ScribeId ? ScriptedStory.Title(rng)
                    : ScriptedStory.IsChoiceCall(r) ? ScriptedStory.Choose(r, rng) : null,
            }.Otherwise(ScriptedReply.Decide("none", statement: "We wait."));
        }

        private RuleSession Session(string preset = "saga") =>
            new RuleSession(You, "qwen3.5:4b", StoryArms.For(preset), 11, Path.Combine(folder, "decisions.jsonl"), (m, w) => Stand_ins(5));

        private static async Task Until(Func<bool> condition, string what)
        {
            for (int i = 0; i < 4000 && !condition(); i++) await Task.Delay(5);
            Assert.That(condition(), Is.True, "timed out waiting for " + what);
        }

        // Holds every turn and takes the default of any dilemma, until the game has resolved this many turns.
        private static async Task PlayTo(RuleSession s, int resolved)
        {
            for (int i = 0; i < 8000 && s.Resolved < resolved && s.Error == null; i++)
            {
                if (s.WaitingFor == RuleSession.Waiting.Choice) s.Choose(s.Choice.DefaultOption);
                else if (s.WaitingFor == RuleSession.Waiting.Orders) s.Order(Move.Hold);
                await Task.Delay(5);
            }
            Assert.That(s.Error, Is.Null);
            Assert.That(s.Resolved, Is.GreaterThanOrEqualTo(resolved), $"the game reached turn {resolved}");
        }

        [TestCase("default")]
        [TestCase("default+storyteller")]
        [TestCase("default+storyteller+s2")]
        [TestCase("default+storyteller+s2+chains-free")]
        [TestCase("default+storyteller+preset-saga+s2+chains-skeleton")]
        [TestCase("default+storyteller+s2+chains-skeleton+preset-underdog")]
        [TestCase("default+storyteller+title-call+multi")]
        public void The_replay_reads_an_arm_as_the_table_does(string arm)
        {
            var options = ModelSeatOptions.Named(arm);
            var mine = StoryArms.Rules(arm);
            Assert.That(mine != null, Is.EqualTo(options.Storyteller));
            if (mine == null) return;
            var theirs = options.StoryRules();
            Assert.That((mine.Choices, mine.Multi, mine.History, mine.Traits, mine.TitleCall, mine.Chains, mine.Settings.Name),
                Is.EqualTo((theirs.Choices, theirs.Multi, theirs.History, theirs.Traits, theirs.TitleCall, theirs.Chains, theirs.Settings.Name)));
        }

        [Test]
        public void The_menu_s_arm_has_the_title_call_and_planned_tales()
        {
            Assert.That(StoryArms.For("off"), Is.EqualTo("default+no-storyteller"));
            Assert.That(StoryArms.Rules(StoryArms.For("off")), Is.Null, "off is off");
            var byDefault = StoryArms.Rules("default");
            var agents = ModelSeatOptions.Named("default").StoryRules();
            Assert.That((byDefault.Choices, byDefault.Multi, byDefault.History, byDefault.Traits, byDefault.TitleCall, byDefault.Chains, byDefault.Settings.Name),
                Is.EqualTo((agents.Choices, agents.Multi, agents.History, agents.Traits, agents.TitleCall, agents.Chains, agents.Settings.Name)), "a recording's \"default\" replays the agents' default");
            Assert.That(new NewGameChoice().Storyteller, Is.EqualTo("saga"), "New game opens on Saga");
            foreach (var p in StorytellerSettings.Presets)
            {
                var r = StoryArms.Rules(StoryArms.For(p.Name));
                Assert.That(r.TitleCall && r.Choices && r.Chains == ChainMode.Skeleton && r.Settings.Name == p.Name, Is.True, p.Name);
            }
        }

        [Test]
        public async Task Your_dilemma_stops_the_turn_until_you_choose()
        {
            var s = Session();
            var run = s.RunAsync();
            await PlayTo(s, 1);
            await Until(() => s.WaitingFor == RuleSession.Waiting.Orders, "turn 2's council");
            // Turn 3 is the first an event may land on: set a dilemma on you for it.
            Assert.That(s.QueueDev($"event sellswords {You}"), Is.Null);
            s.Order(Move.Hold);
            await Until(() => s.WaitingFor == RuleSession.Waiting.Choice, "the choice card");

            Assert.That(s.Resolved, Is.EqualTo(2));
            Assert.That(s.Choice.Country, Is.EqualTo(You));
            Assert.That(s.Choice.Options.Select(o => o.Id), Is.EquivalentTo(new[] { "hire_sellswords", "send_sellswords_away" }));
            Assert.That(s.Choice.Options.All(o => !string.IsNullOrEmpty(o.Effect)), Is.True, "every option says what it costs or gives");
            await Task.Delay(200);
            Assert.That(s.WaitingFor, Is.EqualTo(RuleSession.Waiting.Choice), "the turn does not go on by itself");
            Assert.That(s.Game.World.Turn, Is.EqualTo(2), "turn 3 has not begun");
            Assert.Throws<ArgumentException>(() => s.Choose("burn_the_inn"), "only an option is taken");

            s.Choose("hire_sellswords");
            await Until(() => s.WaitingFor == RuleSession.Waiting.Orders && s.Game.World.Turn == 3, "turn 3's council");
            await PlayTo(s, 3);
            var g = s.BuildReplay();
            var told = g.Turns[2].Events.Single(e => e.Kind == ReplayEventKind.Story);
            Assert.That(told.Story.Template, Is.EqualTo("sellswords"));
            Assert.That(told.Story.Choices[You], Is.EqualTo("hire_sellswords"), "your choice landed, not the default");
            Assert.That(told.Story.ChosenBy[You], Is.EqualTo("person"));
            Assert.That(told.Decision, Is.Null, "the dev menu set it: no storyteller decision behind it");
            s.Stop();
            await run;
        }

        [Test]
        public async Task A_storyteller_game_replays_its_events_choices_and_tales_exactly()
        {
            var s = Session();
            var run = s.RunAsync();
            await PlayTo(s, 1);
            await Until(() => s.WaitingFor == RuleSession.Waiting.Orders, "turn 2's council");
            // Turn 3, the first an event may land on, opens a planned tale on you; the storyteller tells the rest.
            Assert.That(s.QueueDev($"event begin_pretender {You}"), Is.Null, "a planned tale begun on you");
            await PlayTo(s, 10);
            s.Stop();
            await run;

            var game = s.Game;
            var g = s.BuildReplay();
            var replayed = g.Turns.SelectMany(t => t.Events).Where(e => e.Kind == ReplayEventKind.Story).Select(e => e.Story).ToList();
            var landed = game.Events.Told.Where(e => e.Turn <= g.Turns.Count).ToList();
            Assert.That(landed.Count, Is.GreaterThanOrEqualTo(4), "the stand-in storyteller told events");
            Assert.That(replayed.Select(e => (e.Turn, e.Template, e.Target, e.Title, e.Story)),
                Is.EqualTo(landed.Select(e => (e.Turn, e.Template, e.Target, e.Title, e.Story))), "every event, as the game told it");
            Assert.That(replayed.Select(e => string.Join(",", e.Choices.OrderBy(kv => kv.Key).Select(kv => kv.Key + "=" + kv.Value))),
                Is.EqualTo(landed.Select(e => string.Join(",", e.Choices.OrderBy(kv => kv.Key).Select(kv => kv.Key + "=" + kv.Value)))), "every choice");
            Assert.That(g.Chains.Select(c => (c.Id, c.Title, c.Events.Count(x => x.Turn <= g.Turns.Count))),
                Is.EqualTo(game.Chains.Select(c => (c.Id, c.Title, c.Events.Count(x => x.Turn <= g.Turns.Count)))), "every tale");
            Assert.That(g.Chains.Any(c => c.Skeleton != null && c.Countries.Contains(You)), Is.True, "the pretender's tale is followed");

            var byModel = g.Turns.SelectMany(t => t.Events).Where(e => e.Kind == ReplayEventKind.Story && e.Decision != null).ToList();
            Assert.That(byModel.Count, Is.GreaterThan(0), "some events were the storyteller's own");
            Assert.That(byModel.All(e => e.Decision.IsStory && e.Decision.Action == e.Story.Action && e.TitleDecision != null), Is.True,
                "each carries the storyteller's decision and the title call behind it, for the inspector");
            Assert.That(byModel.All(e => e.Turn == e.Decision.Turn), Is.True, "the log's storyteller lines are read at their turn");

            // The chronicle tells it in the chronicler's voice.
            var line = Chronicle.Line(byModel[0], id => id);
            Assert.That(line.Category, Is.EqualTo(ChronicleCategory.Story));
            Assert.That(line.Text, Does.Contain(byModel[0].Story.Title));
            Assert.That(line.Quote, Is.EqualTo(byModel[0].Story.Story));

            // Saved and loaded, it replays the same.
            var back = ReplayBuilder.Build(Recording.Parse(s.BuildRecording().ToJson()), partial: true);
            Assert.That(back.Turns.SelectMany(t => t.Events).Count(e => e.Kind == ReplayEventKind.Story), Is.EqualTo(replayed.Count));
        }

        [Test]
        public void A_tale_the_dev_menu_continues_tells_of_its_subject()
        {
            var game = new Game(4, MapDefinition.Standard()) { EventTable = false, Story = StoryArms.Rules(StoryArms.For("saga")) };
            void Turn() { game.BeginTurn(); game.SubmitCouncil(null); game.Resolve(); }
            Turn(); Turn();
            Assert.That(DevEdits.Apply(game, "event begin_pretender kesh"), Is.Null);
            Turn();
            var tale = game.Chains.Single();
            // The stage the rules offer the tale next, on a turn dealt as a tale's, as the dev menu fires it.
            game.Events.TurnKinds[game.NextTurn] = TurnKind.Tale;
            var next = EventRules.Open(game.World, game.Events, game.NextTurn, game.Story).First(p => p.target == tale.Id);
            Assert.That(DevEdits.Apply(game, $"event {next.template} {tale.Id}"), Is.Null);
            Turn();
            var told = game.Events.Told.Last();
            Assert.That(told.ChainId, Is.EqualTo(tale.Id));
            Assert.That(told.Story, Does.Contain("kesh"), "the tale's subject, not nobody");
            Assert.That(told.Story, Does.Not.Contain("nobody"));
        }

        [Test]
        public void A_banner_stands_where_the_event_strikes()
        {
            var map = MapDefinition.Standard();
            var rec = new Recording { Seed = 4, StoryArm = StoryArms.For("even_hand") };
            rec.DevEdits.Add(new DevEdit { Turn = 3, Command = "event stronghold highmoor" });
            rec.DevEdits.Add(new DevEdit { Turn = 4, Command = "event windfall kesh" });
            foreach (var c in map.Countries)
                for (int t = 1; t <= 4; t++)
                    rec.Decisions.Add(new RecordedDecision { AgentId = c.Id, Turn = t, Moment = "council", Action = "none", Target = RecordedDecision.NoTarget });
            var g = ReplayBuilder.Build(rec, partial: true);
            var fort = g.Turns[2].Events.Single(e => e.Kind == ReplayEventKind.Story);
            var gold = g.Turns[3].Events.Single(e => e.Kind == ReplayEventKind.Story);
            Assert.That(fort.Province, Is.EqualTo("highmoor"), "a province's event stands at the province");
            Assert.That(gold.Province, Is.EqualTo("keshtar"), "a realm's event stands at its capital");
            Assert.That(fort.ProvinceIndex, Is.EqualTo(g.IndexOf("highmoor")));
        }
            // ---- today's default game: a generated world, the writs turn, R3's army rules ----

        // The menu's default New game with a storyteller: a medium generated world, the writs turn and every R3 rule as
        // GameSetup and GameRules give them, the arm StoryArms builds for the preset. Stand-ins play every model.
        private RuleSession DefaultWorldSession(string preset, out string you)
        {
            var setup = GameSetup.Writs(11, GameSetup.GeneratedMapId);
            setup.Countries = MapSettings.DefaultCountries(MapSize.Medium);
            setup.Generator = new MapSettings { Size = MapSize.Medium, Shape = LandShape.Continent };
            var map = setup.BuildMap();
            var rules = setup.BuildRules(map);
            Assert.That((rules.Turn, rules.UnitTypes, rules.Generals, rules.Sieges, rules.Supply), Is.EqualTo((TurnStyle.Writs, true, true, true, true)), "today's defaults");
            you = map.Countries[0].Id;
            return new RuleSession(you, "qwen3.5:4b", StoryArms.For(preset), 11, Path.Combine(folder, "decisions.jsonl"), (m, w) => Stand_ins(5), null, map, rules);
        }

        // Holds every army and the court each writs turn, and takes the default of any dilemma, until this many turns resolved.
        private static async Task PlayWritsTo(RuleSession s, int resolved)
        {
            for (int i = 0; i < 20000 && s.Resolved < resolved && s.Error == null && !s.Finished; i++)
            {
                if (s.WaitingFor == RuleSession.Waiting.Choice) s.Choose(s.Choice.DefaultOption);
                else if (s.WaitingFor == RuleSession.Waiting.Envoy) s.Answer(false);
                else if (s.WaitingFor == RuleSession.Waiting.Orders && s.Orders != null) s.Plan(new RuleSession.WritsPlan());
                await Task.Delay(5);
            }
            Assert.That(s.Error, Is.Null);
            Assert.That(s.Resolved, Is.GreaterThanOrEqualTo(resolved), $"the game reached turn {resolved}");
        }

        [Test]
        public async Task In_the_default_game_your_dilemma_shows_its_card_and_its_effect_lands_on_your_armies()
        {
            var s = DefaultWorldSession("saga", out string you);
            var run = s.RunAsync();
            await PlayWritsTo(s, 1);
            await Until(() => s.WaitingFor == RuleSession.Waiting.Orders && s.Orders != null, "turn 2's orders");
            Assert.That(s.QueueDev($"event sellswords {you}"), Is.Null);
            s.Plan(new RuleSession.WritsPlan());
            await Until(() => s.WaitingFor == RuleSession.Waiting.Choice || s.Error != null, "the choice card");
            Assert.That(s.Error, Is.Null);

            var choice = s.Choice;
            Assert.That(choice.Country, Is.EqualTo(you));
            Assert.That(choice.Options.Select(o => o.Id), Is.EquivalentTo(new[] { "hire_sellswords", "send_sellswords_away" }));
            Assert.That(choice.Options.All(o => !string.IsNullOrEmpty(o.Effect)), Is.True, "every option says what it costs or gives");
            var hire = choice.Options.Single(o => o.Id == "hire_sellswords");
            int soldiers = hire.Planned.Where(x => x.part.Kind == EffectKind.Soldiers).Sum(x => x.amount);
            int gold = hire.Planned.Where(x => x.part.Kind == EffectKind.Gold).Sum(x => x.amount);
            Assert.That(soldiers, Is.GreaterThan(0), "the sellswords join");
            Assert.That(gold, Is.GreaterThan(0), "for gold");
            int before = s.Game.World.ArmiesOf(you).Sum(a => a.Soldiers);

            s.Choose("hire_sellswords");
            await PlayWritsTo(s, 3);
            var e = s.Game.Events.Told.Single(x => x.Template == "sellswords");
            Assert.That(e.Choices[you], Is.EqualTo("hire_sellswords"));
            Assert.That(e.ChosenBy[you], Is.EqualTo("person"));
            Assert.That(e.Effect[you], Does.Contain("soldiers"));
            Assert.That(s.Game.World.ArmiesOf(you).Sum(a => a.Soldiers), Is.GreaterThanOrEqualTo(before + soldiers), "the soldiers are on your armies");
            Assert.That(s.Game.World.Country(you).Army, Is.EqualTo(s.Game.World.ArmiesOf(you).Sum(a => a.Soldiers)), "the count and the armies agree");
            s.Stop();
            await run;
        }

        [TestCase("saga")]
        [TestCase("even_hand")]
        public async Task In_the_default_game_a_storyteller_plays_ten_turns_with_its_dilemmas_and_tales(string preset)
        {
            var s = DefaultWorldSession(preset, out _);
            var run = s.RunAsync();
            await PlayWritsTo(s, 10);
            s.Stop();
            await run;
            var told = s.Game.Events.Told.ToList();
            Assert.That(told.Count, Is.GreaterThanOrEqualTo(4), "events were told");
            Assert.That(told.Any(e => e.Choices.Count > 0), Is.True, "and at least one dilemma answered");
            foreach (var c in s.Game.World.CountryList.Where(c => c.Alive))
                Assert.That(c.Army, Is.EqualTo(s.Game.World.ArmiesOf(c.Id).Sum(a => a.Soldiers)), $"{c.Id}: the count and the armies agree");
            Assert.That(s.Game.World.CountryList.All(c => c.Gold >= 0), Is.True, "no treasury below zero");
        }
    }
}

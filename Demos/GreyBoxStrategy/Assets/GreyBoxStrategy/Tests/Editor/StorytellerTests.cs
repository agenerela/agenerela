using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Agenerela;
using Agenerela.Providers;
using NUnit.Framework;
using static GreyBoxStrategy.Tests.TestGames;

namespace GreyBoxStrategy.Tests
{
    /// <summary>The storyteller (stage S1): effects, fairness, the table off, the story check, and what the rulers read.</summary>
    public sealed class StorytellerTests
    {
        private const string Golden = "The Golden Star: A meteor of gold fell in the hills of valen.";

        // A calm game with the random table off, played to the end of turn 2, so the next upkeep (turn 3) may hold an event.
        private static Game AtTurnTwo(int seed = 1)
        {
            var g = NewGame(seed);
            g.EventTable = false;
            PlayTurn(g);
            PlayTurn(g);
            return g;
        }

        // Two identical games; one gets the event at turn 3's upkeep. Returns both after that upkeep.
        private static (Game with, Game without, EventRecord e) Twin(string template, string target, Action<World> setup = null,
            string statement = Golden)
        {
            var a = AtTurnTwo();
            var b = AtTurnTwo();
            setup?.Invoke(a.World);
            setup?.Invoke(b.World);
            Assert.That(a.WhyNotEvent(template, target), Is.Null, $"{template} on {target} should be open");
            a.QueueEvent(new StoryEvent(template, target, statement));
            Finish(a, a.BeginTurn(), null);
            Finish(b, b.BeginTurn(), null);
            return (a, b, a.Events.Events.Single());
        }

        private static (int gold, int army, int stability) Of(Game g, string id)
        {
            var c = g.World.Country(id);
            return (c.Gold, c.Army, c.Stability);
        }

        // Everything but the named country, and every province, is the same in both games.
        private static void OnlyThisChanged(Game a, Game b, string country, string province = null)
        {
            foreach (var c in a.World.CountryList.Where(c => c.Id != country))
                Assert.That(Of(a, c.Id), Is.EqualTo(Of(b, c.Id)), $"{c.Id} untouched");
            foreach (var p in a.World.ProvinceList.Where(p => p.Id != province))
                Assert.That((p.Owner, p.Forts, p.Garrison), Is.EqualTo((b.World.Province(p.Id).Owner, b.World.Province(p.Id).Forts, b.World.Province(p.Id).Garrison)), p.Id);
        }

        // --- An event applies exactly its effect ------------------------------------------------

        [Test]
        public void EachTemplate_AppliesExactlyItsEffect_AndNothingElse()
        {
            var (a, b, e) = Twin(EventRules.Windfall, "valen");
            Assert.That(Of(a, "valen").gold - Of(b, "valen").gold, Is.EqualTo(30), "a windfall of one turn's income, 3 provinces");
            Assert.That((Of(a, "valen").army, Of(a, "valen").stability), Is.EqualTo((Of(b, "valen").army, Of(b, "valen").stability)));
            Assert.That(e.Value["valen"], Is.EqualTo(30));
            OnlyThisChanged(a, b, "valen");

            (a, b, e) = Twin(EventRules.BadHarvest, "valen");
            Assert.That(Of(a, "valen").gold - Of(b, "valen").gold, Is.EqualTo(-15), "half of 30 income");
            OnlyThisChanged(a, b, "valen");

            (a, b, e) = Twin(EventRules.Plague, "valen");
            int lost = Of(b, "valen").army - Of(a, "valen").army;
            Assert.That(lost, Is.EqualTo(e.Value["valen"] / -2).And.GreaterThanOrEqualTo(3));
            OnlyThisChanged(a, b, "valen");

            (a, b, e) = Twin(EventRules.Volunteers, "valen");
            Assert.That(Of(a, "valen").army - Of(b, "valen").army, Is.EqualTo(14), "5 + 3 a province");
            OnlyThisChanged(a, b, "valen");

            (a, b, e) = Twin(EventRules.Zeal, "valen", w => w.Country("valen").Stability = 2);
            Assert.That(Of(a, "valen").stability - Of(b, "valen").stability, Is.EqualTo(1));
            OnlyThisChanged(a, b, "valen");

            (a, b, e) = Twin(EventRules.Unrest, "valen", w => w.Country("valen").Stability = 4);
            Assert.That(Of(a, "valen").stability - Of(b, "valen").stability, Is.EqualTo(-1));
            OnlyThisChanged(a, b, "valen");

            (a, b, e) = Twin(EventRules.Stronghold, "ashby");
            Assert.That(a.World.Province("ashby").Forts - b.World.Province("ashby").Forts, Is.EqualTo(1));
            Assert.That(Of(a, "valen"), Is.EqualTo(Of(b, "valen")));
            OnlyThisChanged(a, b, "valen", "ashby");

            (a, b, e) = Twin(EventRules.WallsCrumble, "ashby", w => w.Province("ashby").Forts = 2);
            Assert.That(a.World.Province("ashby").Forts, Is.EqualTo(1));
            OnlyThisChanged(a, b, "valen", "ashby");
        }

        [Test]
        public void AGroupEvent_ReachesEveryMember_AndOnlyThem()
        {
            var (a, b, e) = Twin(EventRules.Windfall, EventGroups.North, statement: "Northern Lights: Gold rained on valen and orsk.");
            Assert.That(e.Hit, Is.EqualTo(new[] { "valen", "orsk" }));
            Assert.That(Of(a, "valen").gold - Of(b, "valen").gold, Is.EqualTo(30));
            Assert.That(Of(a, "orsk").gold - Of(b, "orsk").gold, Is.EqualTo(30));
            foreach (string id in new[] { "merrow", "kesh", "ilvar" }) Assert.That(Of(a, id), Is.EqualTo(Of(b, id)));
            Assert.That(e.StoryVerdict, Is.EqualTo("kept"));
        }

        // --- Fairness and cooldowns ------------------------------------------------------------

        private static EventRecord Told(int turn, string template, string target, params (string c, int v)[] values)
        {
            var e = new EventRecord { Turn = turn, Source = EventRecord.FromStoryteller, Template = template, Target = target, Title = "x" };
            foreach (var (c, v) in values)
            {
                e.Hit.Add(c);
                e.Value[c] = v;
            }
            return e;
        }

        [Test]
        public void Fairness_IsAvailability()
        {
            var g = NewGame();
            g.EventTable = false;
            PlayTurn(g);
            Assert.That(g.WhyNotEvent(EventRules.Windfall, "valen"), Does.Contain("before turn 3"), "none before turn 3");
            PlayTurn(g);
            Assert.That(g.WhyNotEvent(EventRules.Windfall, "valen"), Is.Null);

            // The leader is not crowned; the weak are not kicked.
            foreach (string p in new[] { "stonereach", "fenmark", "templegate", "westmarch", "harrow", "corwick", "saltmere", "redcliff", "orskhaven" })
                Give(g.World, p, "valen");
            Assert.That(g.World.ProvinceCount("valen"), Is.EqualTo(12));
            Assert.That(g.WhyNotEvent(EventRules.Windfall, "valen"), Does.Contain("crown the leader"));
            Assert.That(g.WhyNotEvent(EventRules.Volunteers, "valen"), Does.Contain("crown the leader"));
            Assert.That(g.WhyNotEvent(EventRules.Plague, "valen"), Is.Null, "a blow on the leader is fair");
            Assert.That(g.World.ProvinceCount("orsk"), Is.EqualTo(0));
            Give(g.World, "orskhaven", "orsk");
            Give(g.World, "corwick", "orsk");
            Assert.That(g.WhyNotEvent(EventRules.Plague, "orsk"), Does.Contain("too weak"));
            Assert.That(g.WhyNotEvent(EventRules.BadHarvest, EventGroups.North), Does.Contain("too weak"), "a group is open only if every member is");
            Assert.That(g.WhyNotEvent(EventRules.Windfall, "orsk"), Is.Null, "a gift to the weak is fair");
            Assert.That(g.WhyNotEvent(EventRules.WallsCrumble, "highmoor"), Does.Contain("capital"));
        }

        [Test]
        public void TheBudget_AndTheCooldowns_HoldOptionsBack()
        {
            var g = AtTurnTwo();
            // A windfall worth 60 to kesh on turn 2: no more gifts to kesh for six turns.
            g.Events.Events.Add(Told(2, EventRules.Windfall, "kesh", ("kesh", 60)));
            Assert.That(g.WhyNotEvent(EventRules.Plague, "ilvar"), Is.Null);
            Assert.That(g.WhyNotEvent(EventRules.Windfall, "ilvar"), Does.Contain("within the last 3 events"), "a template not again within three events");
            Assert.That(g.WhyNotEvent(EventRules.Plague, "kesh"), Does.Contain("the last event struck kesh"), "a target not twice in a row");
            Assert.That(g.WhyNotEvent(EventRules.Stronghold, "keshtar"), Does.Contain("kesh was struck by the last event"), "nor a country through its province");

            // Three more events later, the windfall is open again, but kesh's budget is still spent until turn 8.
            g.Events.Events.Add(Told(2, EventRules.Plague, "orsk", ("orsk", -8)));
            g.Events.Events.Add(Told(2, EventRules.Zeal, "merrow", ("merrow", 20)));
            g.Events.Events.Add(Told(2, EventRules.Unrest, "valen", ("valen", -20)));
            Assert.That(g.WhyNotEvent(EventRules.Windfall, "ilvar"), Is.Null);
            Assert.That(g.WhyNotEvent(EventRules.Windfall, "kesh"), Does.Contain("enough good fortune"));
            Assert.That(g.WhyNotEvent(EventRules.Volunteers, "kesh"), Does.Contain("enough good fortune"), "the budget holds every gift back");
            Assert.That(g.WhyNotEvent(EventRules.Unrest, "valen"), Is.Not.Null, "valen was struck by the last event");

            var open = EventRules.Open(g.World, g.Events, g.NextTurn);
            Assert.That(open, Has.No.Member((EventRules.Volunteers, "kesh")));
            Assert.That(open, Has.Member((EventRules.Windfall, "ilvar")));
        }

        [Test]
        public async Task OverWholeGames_NoEventBreaksTheFairnessRules()
        {
            // A storyteller that picks at random among what it is offered, with the rulers as scripts:
            // whatever it picks, every event that landed obeyed the rules on the state it landed on.
            for (int seed = 1; seed <= 4; seed++)
            {
                var game = new Game(seed);
                game.EventTable = false;
                var rng = new Random(seed);
                var provider = new ScriptedProvider
                {
                    Callback = req =>
                    {
                        var open = EventRules.Open(game.World, game.Events, game.NextTurn);
                        var (t, target) = open[rng.Next(open.Count)];
                        return ScriptedReply.Decide(t, target, "A Strange Turn: Something happened to someone.");
                    },
                };
                var teller = new Storyteller(game, provider);
                var players = game.World.CountryList.ToDictionary(c => c.Id, c => (IPlayer)new ScriptedPlayer(seed % 2 == 0 ? Temperament.Expansionist : Temperament.Cautious));
                var before = new Dictionary<int, (Dictionary<string, int> provinces, List<EventRecord> history)>();
                while (!game.IsOver)
                {
                    before[game.NextTurn] = (game.World.Alive.ToDictionary(c => c.Id, c => game.World.ProvinceCount(c.Id)), game.Events.Events.ToList());
                    await Await.NotCancelled(ModelGameRunner.PlayTurnAsync(game, players, new Dictionary<string, CountryAgent>(), default, teller));
                }

                var told = game.Events.Told.ToList();
                Assert.That(told.Count, Is.GreaterThan(5), $"seed {seed}: events happened");
                Assert.That(game.Events.Events.All(e => e.Source == EventRecord.FromStoryteller), Is.True, "no table events");
                var r = game.Rules;
                for (int i = 0; i < told.Count; i++)
                {
                    var e = told[i];
                    var t = EventRules.Template(e.Template);
                    var (provinces, history) = before[e.Turn];
                    Assert.That(e.Turn, Is.GreaterThanOrEqualTo(r.EventFirstTurn));
                    foreach (string c in e.Hit)
                    {
                        int net = history.Where(h => h.Turn > e.Turn - r.EventBudgetWindow && h.Value.ContainsKey(c)).Sum(h => h.Value[c]);
                        if (t.Bonus)
                        {
                            Assert.That(provinces[c], Is.LessThan(r.EventNoBonusFromProvinces), $"seed {seed} {e}: no gift to the leader");
                            Assert.That(net, Is.LessThan(r.EventBudget), $"seed {seed} {e}: budget");
                        }
                        else
                        {
                            Assert.That(provinces[c], Is.GreaterThanOrEqualTo(r.EventMinProvincesForPenalty), $"seed {seed} {e}: no blow on the weak");
                            Assert.That(net, Is.GreaterThan(-r.EventBudget), $"seed {seed} {e}: budget");
                        }
                    }
                    var recent = told.Skip(Math.Max(0, i - r.EventTemplateCooldown)).Take(Math.Min(i, r.EventTemplateCooldown)).ToList();
                    Assert.That(recent.Select(x => x.Template), Does.Not.Contain(e.Template), $"seed {seed} {e}: template cooldown");
                    if (i > 0) Assert.That(told[i - 1].Target, Is.Not.EqualTo(e.Target), $"seed {seed} {e}: target twice in a row");
                }
                Assert.That(teller.Decisions.All(d => d.ModelAnswerLegal && d.Queued), Is.True);
            }
        }

        [Test]
        public async Task AnIllegalPair_NeverLands()
        {
            var game = AtTurnTwo();
            game.Events.Events.Add(Told(2, EventRules.Windfall, "kesh", ("kesh", 60)));
            // A windfall again, within the cooldown: the paired schema has no such pair, and the guards hold it.
            var provider = new ScriptedProvider().Otherwise(ScriptedReply.Decide(EventRules.Windfall, "ilvar", "Luck: Gold for ilvar."));
            var teller = new Storyteller(game, provider);
            var d = await Await.NotCancelled(teller.TellAsync());
            Assert.That(d.ModelAnswerLegal, Is.False);
            Assert.That(d.Queued, Is.False);
            game.BeginTurn();
            Assert.That(game.Events.Events.Count, Is.EqualTo(1), "nothing new happened");
        }

        // --- The table and the storyteller never both fire ---------------------------------------

        [Test]
        public void TheTableAndTheStoryteller_NeverBothFire()
        {
            var on = NewGame(1, GameRules.Standard);
            PlayTurn(on);
            PlayTurn(on);
            Assert.That(() => on.QueueEvent(new StoryEvent(EventRules.Windfall, "valen", Golden)), Throws.InvalidOperationException,
                "the table is on");
            var off = NewGame(1, GameRules.Standard);
            off.EventTable = false;
            PlayTurn(off);
            PlayTurn(off);
            off.QueueEvent(new StoryEvent(EventRules.Windfall, "valen", Golden));
            Assert.That(() => off.EventTable = true, Throws.InvalidOperationException, "an event is queued");
            Assert.That(() => off.QueueEvent(new StoryEvent(EventRules.Plague, "orsk", "x")), Throws.InvalidOperationException, "one a turn");
        }

        [Test]
        public async Task AStorytellerTable_HasNoTableEvents_AndTheControlNoStories()
        {
            // The same five fake rulers with and without the storyteller arm, on rules whose table fires often.
            ILLMProvider Fake(World w) => new ScriptedProvider
            {
                Callback = req => req.AgentId == Storyteller.AgentId
                    ? ScriptedReply.Decide(ActionRegistry.None, TargetRegistry.NoTarget, "Calm.")
                    : ScriptedReply.Decide(ActionRegistry.None, TargetRegistry.NoTarget, "We wait."),
            };
            var control = Tables.Named("fake,fake,fake,fake,fake", null, arm: "default");
            var story = Tables.Named("fake,fake,fake,fake,fake", null, arm: "default+storyteller");
            Assert.That(story.Options.Storyteller, Is.True);
            Assert.That(control.Options.Storyteller, Is.False);
            var a = await Await.NotCancelled(TableRunner.PlayAsync(control, 2, 0, (m, w) => Fake(w)));
            var b = await Await.NotCancelled(TableRunner.PlayAsync(story, 2, 0, (m, w) => Fake(w)));
            var tableEvents = a.Record.Reports.SelectMany(r => r.Events).ToList();
            Assert.That(tableEvents, Is.Not.Empty, "the control's table fires");
            Assert.That(tableEvents.All(e => e.Source == EventRecord.FromTable), Is.True);
            Assert.That(a.Storyteller, Is.Empty);
            Assert.That(b.Record.Reports.SelectMany(r => r.Events).Any(e => e.Source == EventRecord.FromTable), Is.False, "no table event beside the storyteller");
            Assert.That(b.Storyteller.Count, Is.EqualTo(b.Record.Reports.Count - 2), "asked every turn from turn 3");
            Assert.That(b.Storyteller.All(s => s.Action == ActionRegistry.None), Is.True);
        }

        // --- The story check ------------------------------------------------------------------------

        [Test]
        public void AStoryNamingTheWrongCountry_IsReplaced_AndTheEffectStands()
        {
            var (a, b, e) = Twin(EventRules.Windfall, "valen", statement: "The Golden Star: Gold fell on orsk, to everyone's surprise.");
            Assert.That(e.StoryVerdict, Is.EqualTo("replaced: names orsk"));
            Assert.That(e.Title, Is.EqualTo("A Windfall"));
            Assert.That(e.Story, Is.EqualTo("Fortune smiled on valen."));
            Assert.That(Of(a, "valen").gold - Of(b, "valen").gold, Is.EqualTo(30), "the effect stands");

            var w = a.World;
            var t = EventRules.Template(EventRules.Plague);
            Assert.That(EventRules.CheckStory(w, t, new[] { "valen" }, null, "Grey Cough: It spread from Corwick.").verdict, Is.EqualTo("replaced: names corwick, orsk's"));
            Assert.That(EventRules.CheckStory(w, t, new[] { "valen" }, null, "Grey Cough: Twelve fell in Highmoor.").verdict, Is.EqualTo("kept"), "valen's own province is fine");
            Assert.That(EventRules.CheckStory(w, t, new[] { "valen" }, null, "Grey Cough: 12 soldiers died.").verdict, Is.EqualTo("replaced: has a number"));
            Assert.That(EventRules.CheckStory(w, t, new[] { "valen" }, null, "A cough spread through valen's camps.").verdict, Is.EqualTo("no title"));
        }

        [Test]
        public async Task TheStorytellerPath_ReplacesAWrongStory_InTheLog()
        {
            var game = AtTurnTwo();
            var provider = new ScriptedProvider().Otherwise(ScriptedReply.Decide(EventRules.Windfall, "valen", "Merrow's Folly: Merrow's gold rolled downhill."));
            var teller = new Storyteller(game, provider);
            var d = await Await.NotCancelled(teller.TellAsync());
            Assert.That(d.Queued, Is.True);
            var report = game.BeginTurn();
            var e = game.Events.Events.Single();
            Assert.That(e.StoryReplaced, Is.True);
            Assert.That(e.Statement, Is.EqualTo("Merrow's Folly: Merrow's gold rolled downhill."));
            Assert.That(EventLog.Line(e), Does.Contain("replaced: names merrow").And.Contain("the model wrote"));
        }

        // --- What the rulers read ---------------------------------------------------------------------

        [Test]
        public async Task TheRulers_ReadTheEvent_OnTheirNextCouncil()
        {
            var game = AtTurnTwo();
            var tellerModel = new ScriptedProvider().Otherwise(ScriptedReply.Decide(EventRules.Windfall, "valen", Golden));
            var valenModel = new ScriptedProvider().Otherwise(ScriptedReply.Decide(ActionRegistry.None, TargetRegistry.NoTarget, "We count our gold."));
            var orskModel = new ScriptedProvider().Otherwise(ScriptedReply.Decide(ActionRegistry.None, TargetRegistry.NoTarget, "We watch."));
            var seats = new Dictionary<string, CountryAgent>
            {
                ["valen"] = new CountryAgent(game.World, "valen", valenModel),
                ["orsk"] = new CountryAgent(game.World, "orsk", orskModel),
            };
            foreach (var kv in seats) game.Gates[kv.Key] = kv.Value;
            var players = game.World.CountryList.Where(c => !seats.ContainsKey(c.Id)).ToDictionary(c => c.Id, c => (IPlayer)new HoldingPlayer());
            var teller = new Storyteller(game, tellerModel);
            await Await.NotCancelled(ModelGameRunner.PlayTurnAsync(game, players, seats, default, teller));

            string valenSaw = valenModel.LastRequest.Request.Stimulus + "\n" + valenModel.LastRequest.Request.Observations;
            string orskSaw = orskModel.LastRequest.Request.Stimulus + "\n" + orskModel.LastRequest.Request.Observations;
            Assert.That(valenSaw, Does.Contain("Event \"The Golden Star\": a windfall of 30 gold reached our treasury."));
            Assert.That(orskSaw, Does.Contain("News: valen received a windfall of gold (\"The Golden Star\")."));
            Assert.That(valenSaw, Does.Not.Contain("meteor"), "the rulers read the title, not the story");
            var report = game.Reports.Last();
            Assert.That(report.Upkeep, Does.Contain("valen: event \"The Golden Star\" (windfall), +30 gold."));
        }

        // --- The storyteller's prompt, the arm, the scorecard ------------------------------------------

        [Test]
        public async Task TheStorytellerReads_OpenEvents_WhatItTold_AndAnInspiration()
        {
            var game = AtTurnTwo();
            var provider = new ScriptedProvider().Otherwise(ScriptedReply.Decide(EventRules.Plague, "kesh", "The Grey Cough: A cough ran through kesh's camps."));
            var teller = new Storyteller(game, provider);
            await Await.NotCancelled(teller.TellAsync());
            string first = provider.LastRequest.Request.Observations;
            Assert.That(first, Does.Contain("Events open now: windfall on "));
            Assert.That(first, Does.Contain("Told so far: nothing yet."));
            Assert.That(first, Does.Contain("Inspiration, if you want it: "));
            Assert.That(provider.LastRequest.Request.Stimulus, Does.Contain("Turn 3. What happens?"));
            Finish(game, game.BeginTurn(), null);
            await Await.NotCancelled(teller.TellAsync());
            string second = provider.LastRequest.Request.Observations;
            Assert.That(second, Does.Contain("Told so far: \"The Grey Cough\" (plague on kesh, turn 3)."));
            string openLine = second.Split('\n').First(l => l.Contains("Events open now:"));
            Assert.That(openLine, Does.Not.Contain("plague on "), "the cooldown keeps plague out of the open line");

            var other = new Storyteller(AtTurnTwo(), provider, ModelSeatOptions.Named("default+storyteller+inspiration-1"));
            Assert.That(other.InspirationLine(3), Is.Not.EqualTo(teller.InspirationLine(3)), "another salt, other words");
            Assert.That(new Storyteller(AtTurnTwo(), provider).InspirationLine(3), Is.EqualTo(teller.InspirationLine(3)), "the same seed and salt, the same words");
        }

        [Test]
        public async Task ATableRun_WritesEveryEvent_AndTheStorytellersCalls()
        {
            // Two arms interleaved, as the A/B runs them: the files each arm's folder gets.
            ILLMProvider Fake(World w) => new ScriptedProvider
            {
                Callback = req =>
                {
                    if (req.AgentId != Storyteller.AgentId) return ScriptedReply.Decide(ActionRegistry.None, TargetRegistry.NoTarget, "We wait.");
                    return w.Turn % 2 == 0
                        ? ScriptedReply.Decide(ActionRegistry.None, TargetRegistry.NoTarget, "Quiet.")
                        : ScriptedReply.Decide(EventRules.Windfall, EventGroups.Everyone, "A Year of Plenty: Every barn was full.");
                },
            };
            string folder = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "gbs-storyteller-" + Guid.NewGuid().ToString("N"));
            try
            {
                var arms = new List<(string, TableDefinition)>
                {
                    ("control", Tables.Named("fake,fake,fake,fake,fake", null, arm: "default")),
                    ("story", Tables.Named("fake,fake,fake,fake,fake", null, arm: "default+storyteller")),
                };
                var cards = await Await.NotCancelled(StrategyTableRuns.RunInterleavedAsync(folder, arms, (m, w) => Fake(w), 2, 1, 1));
                string story = System.IO.Path.Combine(folder, "story");
                foreach (string f in new[] { "events.csv", "events.md", "storyteller.csv", "scorecard.md", "scorecard-games.csv", "game-0001.md", "game-0002.md" })
                    Assert.That(System.IO.File.Exists(System.IO.Path.Combine(story, f)), Is.True, f);
                Assert.That(System.IO.File.Exists(System.IO.Path.Combine(folder, "control", "storyteller.csv")), Is.False);
                string md = System.IO.File.ReadAllText(System.IO.Path.Combine(story, "events.md"));
                Assert.That(md, Does.Contain("**\"A Year of Plenty\"**, windfall on everyone"));
                string log = System.IO.File.ReadAllText(System.IO.Path.Combine(story, "game-0001.md"));
                Assert.That(log, Does.Contain("The storyteller:"));
                Assert.That(System.IO.File.ReadAllText(System.IO.Path.Combine(story, "scorecard.md")), Does.Contain("## Events"));
                Assert.That(cards[1].Get("events", "table", "storyteller calls"), Is.GreaterThan(10));
                Assert.That(cards[1].Get("events", "table", "titles shared across seeds"), Is.EqualTo(1), "one title, in both games");
                Assert.That(cards[0].Get("events", "table", "storyteller calls"), Is.NaN);
                var lines = System.IO.File.ReadAllLines(System.IO.Path.Combine(story, "events.csv"));
                Assert.That(lines.Skip(1).All(l => l.Contains(",storyteller,windfall,everyone,")), Is.True);
            }
            finally
            {
                if (System.IO.Directory.Exists(folder)) System.IO.Directory.Delete(folder, true);
            }
        }

        [Test]
        public void TheArm_ParsesInTheExistingStyle()
        {
            var o = ModelSeatOptions.Named("default+storyteller+inspiration-2");
            Assert.That((o.Storyteller, o.Inspiration, o.InspirationSalt, o.PairTargets, o.Arm), Is.EqualTo((true, true, 2, true, "default+storyteller+inspiration-2")));
            Assert.That(ModelSeatOptions.Named("default+storyteller+no-inspiration").Inspiration, Is.False);
            Assert.That(ModelSeatOptions.Default.Storyteller, Is.False, "off in the default");
        }

        [Test]
        public void TheScorecard_ScoresEvents()
        {
            Assert.That(Scorecard.SignedGini(new double[] { 10, 10, 10, 10, 10 }), Is.EqualTo(0).Within(1e-9));
            Assert.That(Scorecard.SignedGini(new double[] { 50, 0, 0, 0, 0 }), Is.EqualTo(0.8).Within(1e-9));
            Assert.That(Scorecard.SignedGini(new double[] { 0, 0, 0, 0, 0 }), Is.NaN);

            var (a, _, e) = Twin(EventRules.Windfall, "valen");
            Assert.That(Scorecard.NamesEvent("The golden star blesses us; we recruit.", e, a.World), Is.True, "a title word");
            Assert.That(Scorecard.NamesEvent("Fortune favours valen!", e, a.World), Is.True, "the kind's word");
            Assert.That(Scorecard.NamesEvent("We recruit in valen.", e, a.World), Is.False);

            var record = new GameRecord(a, a.World.CountryList.ToDictionary(c => c.Id, c => "fake"));
            var card = new Scorecard("t", new[] { new ScoredGame(record, null, null) }, characterTest: false);
            Assert.That(card.Get("events", "table", "events a game"), Is.EqualTo(1));
            Assert.That(card.Get("events", "table", "distinct titles"), Is.EqualTo(1));
            Assert.That(card.Get("events", "table", "stories replaced"), Is.EqualTo(0));
            Assert.That(card.Get("events", "table", "event value a game"), Is.EqualTo(30));
        }
    }
}

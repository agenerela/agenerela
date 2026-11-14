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
    /// <summary>
    /// The storyteller's stage S2: dilemmas and the rulers' choices, events on many, chains free and
    /// on skeletons, game-aware stories and the history check, province traits, the title call and
    /// the presets. S1's own behaviour with every piece off is <see cref="StorytellerTests"/>.
    /// </summary>
    public sealed class StorytellerS2Tests
    {
        private const string Story = "Sellswords came over the hills to valen's gate.";

        // Every dilemma and tale open whenever the rules allow, so a test is not at the dice's mercy.
        private static StoryRules Rules(Action<StoryRules> set = null)
        {
            var settings = StorytellerSettings.EvenHand;
            settings.ChoiceChance = 1;
            settings.ChainChance = 1;
            var r = new StoryRules { Settings = settings };
            set?.Invoke(r);
            return r;
        }

        private static Game AtTurnTwo(StoryRules rules, int seed = 1)
        {
            var g = NewGame(seed);
            g.EventTable = false;
            g.Story = rules;
            PlayTurn(g);
            PlayTurn(g);
            return g;
        }

        private static void Tell(Game g, string template, string target, string statement = "Something stirred.", string title = null)
        {
            Assert.That(g.WhyNotEvent(template, target), Is.Null, $"{template} on {target} should be open");
            g.QueueEvent(new StoryEvent(template, target, statement, title));
        }

        // --- S1 untouched -----------------------------------------------------------------------

        [Test]
        public void WithEveryPieceOff_TheStorytellerIsS1s()
        {
            var options = ModelSeatOptions.Named("default+storyteller");
            Assert.That(options.StoryRules().IsS2, Is.False, "the control arm is S1");
            var game = AtTurnTwo(options.StoryRules());
            var teller = new Storyteller(game, new ScriptedProvider(), options);
            Assert.That(teller.Agent.Actions.Definitions.Select(d => d.Id), Is.EquivalentTo(EventRules.Templates.Select(t => t.Id)), "S1's eight, no more");
            Assert.That(teller.Scribe, Is.Null);
            Assert.That(teller.Agent.Identity.Knowledge.Count, Is.EqualTo(2), "S1's two knowledge lines");
            Assert.That(EventRules.Open(game.World, game.Events, game.NextTurn, game.Story),
                Is.EqualTo(EventRules.Open(game.World, game.Events, game.NextTurn)), "the same events open");
            var seat = new CountryAgent(game.World, "valen", new ScriptedProvider(), options);
            Assert.That(seat.Agent.Actions.Definitions.Any(d => d.Id == "hire_sellswords"), Is.False, "no option actions on a seat");
        }

        [Test]
        public void TheArms_Parse()
        {
            var s2 = ModelSeatOptions.Named("default+storyteller+s2+chains-free");
            Assert.That((s2.Storyteller, s2.Choices, s2.Multi, s2.History, s2.Traits, s2.TitleCall, s2.Chains, s2.Preset),
                Is.EqualTo((true, true, true, true, true, true, ChainMode.Free, "even_hand")));
            Assert.That(ModelSeatOptions.Named("default+storyteller+s2+chains-skeleton").Chains, Is.EqualTo(ChainMode.Skeleton));
            Assert.That(ModelSeatOptions.Named("default+storyteller+title-call").StoryRules().Choices, Is.False, "one piece alone");
            Assert.That(ModelSeatOptions.Named("default+storyteller+preset-saga").StoryRules().Settings.Name, Is.EqualTo("saga"));
            Assert.That(ModelSeatOptions.Named("default+storyteller+preset-nonsense"), Is.Null);
            Assert.That(StorytellerSettings.Presets.Select(p => p.Name), Is.EqualTo(new[] { "even_hand", "underdog", "cruel_fate", "quiet_years", "saga" }));
            Assert.That(StorytellerSettings.Presets.All(p => !string.IsNullOrEmpty(p.Label) && !string.IsNullOrEmpty(p.Description)), Is.True, "a menu can show them");
        }

        // --- Dilemmas -------------------------------------------------------------------------

        [Test]
        public void ADilemma_AsksTheRuler_AndLandsWhatItChose()
        {
            var g = AtTurnTwo(Rules(r => r.Choices = true));
            g.World.Country("valen").Gold = 100;
            Tell(g, "sellswords", "valen", Story);
            var choice = g.PendingChoices.Single();
            Assert.That((choice.Country, choice.DefaultOption), Is.EqualTo(("valen", "send_sellswords_away")));
            Assert.That(choice.Options.Select(o => o.Id), Is.EqualTo(new[] { "hire_sellswords", "send_sellswords_away" }));
            var hire = choice.Option("hire_sellswords");
            Assert.That(hire.Effect, Is.EqualTo("pay 30 gold, 14 soldiers join our army"), "the game's amounts, stated");
            var request = g.ChoiceRequests().Single();
            Assert.That(request.Moment, Is.EqualTo(Moment.Choice));
            Assert.That(request.Legal.Select(m => m.Action), Is.EqualTo(new[] { "hire_sellswords", "send_sellswords_away" }));
            Assert.That(request.TurnFacts[1], Does.StartWith("Our choice: hire_sellswords, hire them: pay 30 gold"));
            Assert.That(request.DefaultMove.Action, Is.EqualTo("send_sellswords_away"));

            Assert.That(g.Choose("valen", "burn_the_city"), Does.Contain("not one of"));
            Assert.That(g.Choose("valen", "hire_sellswords"), Is.Null);
            Assert.That(g.Choose("valen", "send_sellswords_away"), Does.Contain("no choice waiting"), "one answer");
            var (gold, army) = (g.World.Country("valen").Gold, g.World.Country("valen").Army);
            var report = PlayTurn(g);
            var e = report.Events.Single();
            Assert.That(e.Choices["valen"], Is.EqualTo("hire_sellswords"));
            Assert.That(e.ChosenBy["valen"], Is.EqualTo("person"));
            Assert.That(e.Value["valen"], Is.EqualTo(-30 + 14 * g.Rules.GoldPerSoldier));
            Assert.That(g.World.Country("valen").Army - army, Is.EqualTo(14));
            Assert.That(g.World.Country("valen").Gold, Is.EqualTo(gold - 30 + 30 - g.World.Upkeep(g.World.Country("valen"))), "paid 30, then income 30 and upkeep");
            Assert.That(g.PendingChoices, Is.Empty);
        }

        [Test]
        public void AnUnansweredChoice_TakesItsDefault()
        {
            var g = AtTurnTwo(Rules(r => r.Choices = true));
            g.World.Country("valen").Gold = 100;
            Tell(g, "sellswords", "valen", Story);
            int army = g.World.Country("valen").Army;
            var e = PlayTurn(g).Events.Single();
            Assert.That((e.Choices["valen"], e.ChosenBy["valen"]), Is.EqualTo(("send_sellswords_away", "default")));
            Assert.That(e.Value["valen"], Is.EqualTo(0));
            Assert.That(g.World.Country("valen").Army, Is.EqualTo(army));
        }

        [Test]
        public void ADilemma_IsOpenOnlyWhenEveryOptionCanApply_AndOnItsTurns()
        {
            var g = AtTurnTwo(Rules(r => r.Choices = true));
            g.World.Country("valen").Gold = 5;
            Assert.That(g.WhyNotEvent("sellswords", "valen"), Does.Contain("cannot pay"));
            Assert.That(g.WhyNotEvent("pretender", "valen"), Does.Contain("is not an event"), "a tale's own dilemma, without skeletons");
            var tales = AtTurnTwo(Rules(r => r.Chains = ChainMode.Skeleton));
            tales.World.Country("valen").Gold = 100;
            Assert.That(tales.WhyNotEvent("pretender", "valen"), Does.Contain("only within a tale"));
            var never = AtTurnTwo(Rules(r =>
            {
                r.Choices = true;
                r.Settings.ChoiceChance = 0;
            }));
            never.World.Country("valen").Gold = 100;
            Assert.That(never.WhyNotEvent("sellswords", "valen"), Is.EqualTo("no dilemma this turn"));
            Assert.That(AtTurnTwo(StoryRules.S1).WhyNotEvent("sellswords", "valen"), Does.Contain("is not an event"), "S1 has none");
        }

        [Test]
        public async Task AModelRuler_AnswersAChoice_WithItsOptionsAsItsOnlyMoves()
        {
            var g = AtTurnTwo(Rules(r => r.Choices = true));
            g.World.Country("valen").Gold = 100;
            Tell(g, "sellswords", "valen", Story);
            var options = ModelSeatOptions.Named("default+storyteller+choices");
            var seat = new CountryAgent(g.World, "valen", new ScriptedProvider().Otherwise(
                ScriptedReply.Decide("hire_sellswords", TargetRegistry.NoTarget, "Gold for swords; we march.")), options);
            g.Gates["valen"] = seat;
            var request = g.ChoiceRequests().Single();
            var preview = seat.Agent.Preview(seat.StimulusFor(request), seat.OptionsFor(request));
            Assert.That(preview.Available.Select(a => a.Id), Is.EquivalentTo(new[] { "hire_sellswords", "send_sellswords_away" }), "no council move on a choice");
            Assert.That(seat.StimulusFor(request), Is.EqualTo("An event asks for our decision."));

            var answer = await Await.NotCancelled(seat.DecideAsync(request));
            Assert.That(answer.Choice.Action, Is.EqualTo("hire_sellswords"));
            g.SubmitChoices(new[] { answer });
            Assert.That(g.PendingChoices.Single().ChosenBy, Is.EqualTo("model"));
            Assert.That(seat.Decisions.Single().Moment, Is.EqualTo(Moment.Choice));
            Assert.That(seat.Decisions.Single().ModelAnswerLegal, Is.True);

            // The council afterwards offers no option.
            PlayTurn(g);
            var council = g.BeginTurn();
            var cp = seat.Agent.Preview(seat.StimulusFor(For(council, "valen")), seat.OptionsFor(For(council, "valen")));
            Assert.That(cp.Available.Any(a => a.Id == "hire_sellswords"), Is.False);
        }

        [Test]
        public void TheGameDealsEachTurn_ATale_ADilemma_OrAnOrdinaryEvent()
        {
            var d = AtTurnTwo(Rules(r => r.Choices = true));
            foreach (var c in d.World.CountryList) c.Gold = 100;
            Assert.That(EventRules.KindOfTurn(d.World, d.Events, d.NextTurn, d.Story), Is.EqualTo(TurnKind.Dilemma));
            Assert.That(d.WhyNotEvent(EventRules.Windfall, "valen"), Is.EqualTo("this turn is for a dilemma"));
            Assert.That(d.WhyNotEvent("sellswords", "valen"), Is.Null);

            var t = AtTurnTwo(Rules(r =>
            {
                r.Choices = true;
                r.Chains = ChainMode.Skeleton;
            }));
            foreach (var c in t.World.CountryList) c.Gold = 100;
            Assert.That(EventRules.KindOfTurn(t.World, t.Events, t.NextTurn, t.Story), Is.EqualTo(TurnKind.Tale), "a tale before a dilemma");
            Assert.That(t.WhyNotEvent(EventRules.Windfall, "valen"), Is.EqualTo("this turn is for a tale"));
            Assert.That(t.WhyNotEvent("sellswords", "valen"), Is.EqualTo("no dilemma this turn"));
            Assert.That(t.WhyNotEvent("begin_pretender", "kesh"), Is.Null);

            var o = AtTurnTwo(Rules(r =>
            {
                r.Choices = true;
                r.Settings.ChoiceChance = 0;
            }));
            Assert.That(EventRules.KindOfTurn(o.World, o.Events, o.NextTurn, o.Story), Is.EqualTo(TurnKind.Ordinary));
            Assert.That(o.WhyNotEvent(EventRules.Windfall, "valen"), Is.Null);

            // The share of dilemma turns follows the preset's chance, by the game's own dice.
            var even = StorytellerSettings.EvenHand;
            int dilemmas = Enumerable.Range(3, 400).Count(turn => Dice.Unit(1, turn, "storyteller", "choice-open") < even.ChoiceChance);
            Assert.That(dilemmas / 400.0, Is.EqualTo(even.ChoiceChance).Within(0.06));
        }

        // --- Events on many -------------------------------------------------------------------

        [Test]
        public void Multi_AGroupEventSparesWhomTheRulesProtect_AndReachesTheRest()
        {
            EventRecord Lucky(int turn) => new EventRecord
            {
                Turn = turn, Source = EventRecord.FromTable, Template = EventRules.GoodHarvest, Target = "valen",
                Hit = { "valen" }, Value = { ["valen"] = 60 },
            };
            var s1 = AtTurnTwo(StoryRules.S1);
            s1.Events.Events.Add(Lucky(2));
            Assert.That(s1.WhyNotEvent(EventRules.Windfall, EventGroups.Everyone), Does.Contain("enough good fortune"), "S1: all or nothing");

            var g = AtTurnTwo(Rules(r => r.Multi = true));
            g.Events.Events.Add(Lucky(2));
            Tell(g, EventRules.Windfall, EventGroups.Everyone, "Gold rained on every land.");
            var e = PlayTurn(g).Events.Single();
            Assert.That(e.Hit, Is.EquivalentTo(new[] { "orsk", "merrow", "kesh", "ilvar" }));
            Assert.That(e.Spared, Is.EqualTo(new[] { "valen" }));
            Assert.That(e.Value.ContainsKey("valen"), Is.False);
        }

        [Test]
        public void Multi_AGroupDilemma_GivesEachMemberItsOwnChoice()
        {
            var g = AtTurnTwo(Rules(r =>
            {
                r.Choices = true;
                r.Multi = true;
            }));
            foreach (var c in g.World.CountryList) c.Gold = 100;
            Tell(g, "sellswords", EventGroups.North, "Sellswords walked the northern roads.");
            Assert.That(g.PendingChoices.Select(c => c.Country), Is.EqualTo(new[] { "valen", "orsk" }));
            g.Choose("orsk", "hire_sellswords");
            var e = PlayTurn(g).Events.Single();
            Assert.That(e.Choices, Is.EquivalentTo(new Dictionary<string, string> { ["valen"] = "send_sellswords_away", ["orsk"] = "hire_sellswords" }));
        }

        // --- Chains ---------------------------------------------------------------------------

        [Test]
        public void ASkeletonChain_FollowsItsStages_ByWhatWasChosen()
        {
            var g = AtTurnTwo(Rules(r => r.Chains = ChainMode.Skeleton));
            g.World.Country("kesh").Gold = 100;
            Assert.That(g.WhyNotEvent("begin_long_winter", EventGroups.Everyone), Does.Contain("not before turn 8"));
            Tell(g, "begin_pretender", "kesh", "A man in kesh's colours claims the hill throne.");
            Assert.That(g.PendingChoices.Single().Template, Is.EqualTo("pretender"));
            g.Choose("kesh", "defy_pretender");
            PlayTurn(g);
            var tale = g.Chains.Single();
            Assert.That((tale.Id, tale.Status, tale.Events.Count, tale.Skeleton), Is.EqualTo(("tale_1", ChainStatus.Open, 1, "pretender")));

            Assert.That(g.WhyNotEvent(EventRules.Zeal, "tale_1"), Does.Contain("next event is unrest"), "defied: the unrest stage, not the payment's ending");
            Assert.That(g.WhyNotEvent("begin_pretender", "orsk"), Does.Contain("open already"));
            Tell(g, EventRules.Unrest, "tale_1", "The pretender's banners stir the kesh villages.");
            PlayTurn(g);
            Assert.That(g.WhyNotEvent(EventRules.Zeal, "tale_1"), Does.Contain("next event is pretender_rising"));
            Tell(g, "pretender_rising", "tale_1", "The pretender's men rise at last.");
            g.Choose("kesh", "crush_rising");
            var last = PlayTurn(g).Events.Single();
            Assert.That((last.ChainId, last.ChainStage), Is.EqualTo(("tale_1", 3)));
            Assert.That((tale.Status, tale.EndReason, tale.Events.Count), Is.EqualTo((ChainStatus.Completed, "told to its end", 3)));
            Assert.That(g.WhyNotEvent(EventRules.Plague, "tale_1"), Does.Contain("is over"));
        }

        [Test]
        public void ASkeletonChain_EndsEarly_ByAnotherChoice_OrIsAbandoned()
        {
            var g = AtTurnTwo(Rules(r => r.Chains = ChainMode.Skeleton));
            g.World.Country("kesh").Gold = 100;
            Tell(g, "begin_pretender", "kesh");
            g.Choose("kesh", "pay_pretender");
            PlayTurn(g);
            Assert.That(g.WhyNotEvent(EventRules.Unrest, "tale_1"), Does.Contain("next event is zeal"), "paid: the ending");
            for (int i = 0; i < 4; i++) PlayTurn(g);
            Assert.That((g.Chains[0].Status, g.Chains[0].EndReason), Is.EqualTo((ChainStatus.Abandoned, "not continued")));
        }

        [Test]
        public void AFreeChain_StartsWhenContinued_AndEndsWithEndTale()
        {
            var g = AtTurnTwo(Rules(r => r.Chains = ChainMode.Free));
            Tell(g, EventRules.Plague, "kesh", "A cough ran through the kesh camps.");
            PlayTurn(g);
            var tale = g.Chains.Single();
            Assert.That((tale.Status, tale.Started), Is.EqualTo((ChainStatus.Single, false)), "one event is not a chain yet");
            Assert.That(g.WhyNotEvent(EventRules.EndTale, "tale_1"), Does.Contain("not a tale yet"));
            Assert.That(g.WhyNotEvent(EventRules.Plague, "tale_1"), Does.Contain("within the last"), "free chains keep the template cooldown");
            g.World.Country("kesh").Stability = 3;
            Tell(g, EventRules.Zeal, "tale_1", "The sick of kesh rose from their beds and gave thanks.");
            var e = PlayTurn(g).Events.Single();
            Assert.That(e.Hit, Is.EqualTo(new[] { "kesh" }), "a tale strikes its subject, even twice in a row");
            Assert.That((tale.Status, tale.Started, e.ChainStage), Is.EqualTo((ChainStatus.Open, true, 2)));
            Tell(g, EventRules.EndTale, "tale_1", "And so the fever of kesh passed into song.");
            var end = PlayTurn(g).Events.Single();
            Assert.That(end.Value["kesh"], Is.EqualTo(0), "a story only");
            Assert.That((tale.Status, tale.EndReason, tale.Events.Count), Is.EqualTo((ChainStatus.Completed, "end_tale", 3)));
        }

        [Test]
        public void ChainRules_OneLongTaleAGame_TwoOpenAtMost_AndTheyGetOld()
        {
            var g = AtTurnTwo(Rules(r => r.Chains = ChainMode.Free));
            var w = g.World;
            Chain Tale(string id, int events, ChainStatus status, string subject)
            {
                var c = new Chain { Id = id, Mode = ChainMode.Free, Subject = subject, SubjectKind = EventTargetKind.Country, Status = status, Title = id };
                c.Countries.Add(subject);
                for (int i = 0; i < events; i++) c.Events.Add(new EventRecord { Turn = g.NextTurn - 1, Template = "omen", Target = id });
                g.Events.Chains.Add(c);
                return c;
            }
            Tale("tale_1", 4, ChainStatus.Open, "orsk");
            Tale("tale_2", 3, ChainStatus.Open, "kesh");
            Assert.That(g.WhyNotEvent(EventRules.Windfall, "tale_2"), Is.EqualTo("only one long tale a game"));
            Assert.That(g.WhyNotEvent(EventRules.Windfall, "tale_1"), Is.Null, "the long one may go on");
            var third = Tale("tale_3", 1, ChainStatus.Single, "merrow");
            Assert.That(g.WhyNotEvent(EventRules.Windfall, "tale_3"), Does.Contain("tales are open already"));
            third.Events[0].Turn = g.NextTurn - 4;
            Assert.That(g.WhyNotEvent(EventRules.Windfall, "tale_3"), Does.Contain("too old"));
        }

        // --- Game-aware stories ---------------------------------------------------------------

        [Test]
        public void TheHistoryCheck_KnowsARealCapture_FromAnInventedOne()
        {
            var w = NewGame().World;
            var all = new List<Happening>
            {
                new Happening { Turn = 4, Kind = "capture", Province = "fenmark", Countries = { "valen", "orsk" }, Text = "valen took fenmark from orsk" },
                new Happening { Turn = 5, Kind = "war", Countries = { "kesh", "merrow" }, Text = "kesh declared war on merrow" },
            };
            var hit = new[] { "valen" };
            Assert.That(History.Check(w, all, all, hit, "Since valen took fenmark from orsk, its bells have rung."), Does.StartWith("real: valen took fenmark"));
            Assert.That(History.Check(w, all, all, hit, "After valen seized harrow, the wells ran dry."), Is.EqualTo("invented: no fight or capture at harrow yet"));
            Assert.That(History.Check(w, all, all, hit, "The battle between valen and ilvar left widows."), Is.EqualTo("invented: valen and ilvar never fought"));
            Assert.That(History.Check(w, all, all, hit, "Kesh and merrow fought a bitter battle in the rain."), Does.StartWith("real: kesh declared war"));
            Assert.That(History.Check(w, all, all, hit, "Snow fell on valen."), Is.Null, "no claim, no name from the happenings");
            Assert.That(History.Check(w, all, all, hit, "Merchants from orsk came to valen with salt."), Does.StartWith("real: names orsk"));
        }

        [Test]
        public void AGameAwareStory_MayNameWhatHappened_ButAnInventedBattleIsReplaced()
        {
            var g = AtTurnTwo(Rules(r => r.History = true));
            Give(g.World, "harrow", "valen");
            g.Reports.Last().Outcomes.Add(new MoveOutcome(
                new CountryRequest(Moment.Council, 2, "valen", g.World, new string[0], new string[0], new Move[0]),
                new Move(Actions.Attack, "harrow"), new Move(Actions.Attack, "harrow"), false, "took harrow from orsk (40 against 10); lost 3"));
            Tell(g, EventRules.Windfall, "valen", "Orsk Silver: Since valen took harrow from orsk, its markets have filled with orsk silver.");
            var real = PlayTurn(g).Events.Single();
            Assert.That(real.StoryVerdict, Is.EqualTo("kept"), "orsk may be named: it lost harrow");
            Assert.That(real.HistoryRef, Does.StartWith("real: valen took harrow"));

            var h = AtTurnTwo(Rules(r => r.History = true));
            Tell(h, EventRules.Windfall, "valen", "After valen stormed ashby's walls, gold poured in.");
            var invented = PlayTurn(h).Events.Single();
            Assert.That(invented.StoryVerdict, Is.EqualTo("replaced: invented: no fight or capture at ashby yet"));
            Assert.That(invented.Story, Is.EqualTo("Fortune smiled on valen."));
        }

        [Test]
        public async Task AnInventedStory_IsReplacedByTheHistoryGuard_WithTheVerdictTheGameGaveBefore()
        {
            const string Invented = "Gold Rush: After valen stormed ashby's walls, gold poured in.";
            const string Plain = "Gold Rush: Gold was found in the hills of valen.";
            async Task<(StorytellerDecision d, EventRecord e)> Told(string arm, string story)
            {
                var g = AtTurnTwo(Rules(r => r.History = true));
                var provider = new ScriptedProvider().Otherwise(ScriptedReply.Decide(EventRules.Windfall, "valen", story));
                var teller = new Storyteller(g, provider, ModelSeatOptions.Named(arm));
                var d = await Await.NotCancelled(teller.TellAsync());
                return (d, PlayTurn(g).Events.Single());
            }

            var (guarded, ge) = await Told("default+storyteller+history", Invented);
            var (old, oe) = await Told("default+storyteller+history+history-in-game", Invented);
            Assert.That(guarded.HistoryReplaced, Is.EqualTo("invented: no fight or capture at ashby yet"), "the guard replaced it (#292)");
            Assert.That(old.HistoryReplaced, Is.Null, "+history-in-game leaves it to the event's check");
            Assert.That(ge.StoryVerdict, Is.EqualTo("replaced: invented: no fight or capture at ashby yet"));
            Assert.That((ge.StoryVerdict, ge.HistoryRef, ge.Title, ge.Story, ge.Statement), Is.EqualTo((oe.StoryVerdict, oe.HistoryRef, oe.Title, oe.Story, oe.Statement)),
                "the event reads as it did with the check in the game");
            Assert.That((ge.Title, ge.Story, ge.Statement), Is.EqualTo(("Gold Rush", "Fortune smiled on valen.", Invented)), "the model's title, the template's story, the model's words kept");
            Assert.That(guarded.Statement, Is.EqualTo(Invented), "the record keeps the model's words");

            var (kept, ke) = await Told("default+storyteller+history", Plain);
            Assert.That((kept.HistoryReplaced, ke.StoryVerdict, ke.Story), Is.EqualTo(((string)null, "kept", "Gold was found in the hills of valen.")));
        }

        [Test]
        public void Happenings_AreReadFromTheReports()
        {
            var g = NewGame();
            g.World.Country("valen").Army = 300;
            PlayTurn(g, ("valen", Actions.Attack, "fenmark"));
            PlayTurn(g, ("valen", Actions.DeclareWar, "merrow"));
            var list = History.Of(g);
            Assert.That(list.Select(x => x.Text), Does.Contain("valen took fenmark from the neutrals"));
            Assert.That(list.Select(x => x.Text), Does.Contain("valen declared war on merrow"));
            var recent = History.Recent(list, g.NextTurn);
            Assert.That(recent.Select(x => x.Kind), Does.Not.Contain("capture"), "a neutral province taken is not news");
        }

        // --- Traits ---------------------------------------------------------------------------

        [Test]
        public void Traits_AreSeeded_ForEveryProvince_AndTraitEventsNeedThem()
        {
            var a = NewGame(3).World.Traits;
            var b = NewGame(3).World.Traits;
            Assert.That(a.Count, Is.EqualTo(25));
            Assert.That(a.Keys.All(k => a[k].Line == b[k].Line), Is.True, "the same seed, the same land");
            Assert.That(NewGame(4).World.Traits.Values.Select(t => t.Line), Is.Not.EqualTo(a.Values.Select(t => t.Line)), "another seed, other land");
            // The land itself is the map's (rules v2's R0 data): the Classic board's highmoor is coastal mountains in every game.
            Assert.That(a["highmoor"].Terrain, Is.EqualTo("coastal mountains"));
            Assert.That(NewGame(4).World.Traits["highmoor"].Terrain, Is.EqualTo("coastal mountains"));
            Assert.That(a.Values.Count(t => t.Wet), Is.GreaterThan(0).And.LessThan(25));

            var g = AtTurnTwo(Rules(r => r.Traits = true), 3);
            var w = g.World;
            string dry = w.ProvinceList.First(p => p.Owner != null && !w.Traits[p.Id].Wet).Id;
            Assert.That(g.WhyNotEvent("flood", dry), Does.Contain("has no place for flood"));
            var wet = w.ProvinceList.FirstOrDefault(p => p.Owner != null && w.Traits[p.Id].Wet && p.Owner != null);
            if (wet != null) Assert.That(g.WhyNotEvent("flood", wet.Id), Is.Null.Or.Not.Contain("no place"));
            var teller = new Storyteller(g, new ScriptedProvider(), ModelSeatOptions.Named("default+storyteller+traits"));
            Assert.That(Storyteller.Describe(g, dry), Does.EndWith(w.Traits[dry].Line), "the storyteller reads the place");
            Assert.That(teller.Agent.Actions.Definitions.Any(d => d.Id == "flood"), Is.True);
        }

        // --- Titles ---------------------------------------------------------------------------

        [Test]
        public void TheTitleCall_IsCleaned_AndUsed()
        {
            Assert.That(EventRules.CleanTitle("\"The Weeping Statue\"."), Is.EqualTo("The Weeping Statue"));
            Assert.That(EventRules.CleanTitle("Title: Salt in the Wells"), Is.EqualTo("Salt in the Wells"));
            Assert.That(EventRules.CleanTitle("The Iron Hermit: a story"), Is.EqualTo("The Iron Hermit"));
            Assert.That(EventRules.CleanTitle("It rained. Then it snowed on everyone"), Is.Null, "two sentences");
            Assert.That(EventRules.CleanTitle("The 3 Bells"), Is.Null);
            Assert.That(EventRules.CleanTitle("one two three four five six seven eight"), Is.Null);
            Assert.That(EventRules.CleanTitle("  "), Is.Null);

            var g = AtTurnTwo(Rules(r => r.TitleCall = true));
            Tell(g, EventRules.Windfall, "valen", "A meteor of gold fell in the hills of valen.", "The Golden Star");
            var e = PlayTurn(g).Events.Single();
            Assert.That((e.Title, e.ModelTitle, e.Story, e.StoryVerdict), Is.EqualTo(("The Golden Star", "The Golden Star", "A meteor of gold fell in the hills of valen.", "kept")));
            var none = AtTurnTwo(Rules(r => r.TitleCall = true));
            Tell(none, EventRules.Windfall, "valen", "A meteor of gold fell in the hills of valen.", "It fell. It glowed");
            var f = PlayTurn(none).Events.Single();
            Assert.That((f.Title, f.ModelTitle, f.StoryVerdict), Is.EqualTo(("A Windfall", (string)null, "no title")));
        }

        [Test]
        public async Task TheStoryteller_AsksTheScribe_AndSkipsATurnByItsPreset()
        {
            var g = AtTurnTwo(Rules(r => r.TitleCall = true));
            var asked = new List<string>();
            var provider = new ScriptedProvider
            {
                Callback = req =>
                {
                    asked.Add(req.AgentId);
                    return req.AgentId == Storyteller.ScribeId
                        ? ScriptedReply.Decide("title", TargetRegistry.NoTarget, "The Golden Star")
                        : ScriptedReply.Decide(EventRules.Windfall, "valen", "A meteor of gold fell in the hills of valen.");
                },
            };
            var teller = new Storyteller(g, provider, ModelSeatOptions.Named("default+storyteller+title-call"));
            Assert.That(teller.ToldLine(), Is.EqualTo("Told so far: nothing yet."));
            var d = await Await.NotCancelled(teller.TellAsync());
            Assert.That(asked, Is.EqualTo(new[] { Storyteller.AgentId, Storyteller.ScribeId }));
            Assert.That((d.Queued, d.TitleStatement), Is.EqualTo((true, "The Golden Star")));
            PlayTurn(g);
            Assert.That(g.Events.Told.Single().Title, Is.EqualTo("The Golden Star"));
            Assert.That(teller.ToldLine(), Is.EqualTo("Told so far: windfall on valen (turn 3)."), "no title to copy");

            var quiet = Rules(r => r.TitleCall = true);
            quiet.Settings.ActChance = 0;
            var q = AtTurnTwo(quiet);
            var silent = new Storyteller(q, new ScriptedProvider { Callback = req => throw new InvalidOperationException("never asked") }, null);
            var skipped = await Await.NotCancelled(silent.TellAsync());
            Assert.That(skipped.Skipped, Is.True);
        }

        // --- The storyteller's facts ----------------------------------------------------------

        [Test]
        public void TheStorytellerReads_TalesAndHappenings_OnlyWhenTheyAreOn()
        {
            var g = AtTurnTwo(Rules(r =>
            {
                r.History = true;
                r.Chains = ChainMode.Skeleton;
                r.Choices = true;
            }));
            g.World.Country("kesh").Gold = 100;
            var teller = new Storyteller(g, new ScriptedProvider(), ModelSeatOptions.Named("default+storyteller+s2+chains-skeleton"));
            var facts = teller.StandingFacts();
            Assert.That(facts.Any(f => f.StartsWith("Recent happenings:")), Is.True);
            Assert.That(teller.Agent.Identity.Knowledge.Any(k => k.Contains("begin_")), Is.True);
            Tell(g, "begin_pretender", "kesh");
            PlayTurn(g);
            Assert.That(Storyteller.Describe(g, "tale_1"), Does.Contain("kesh chose defy_pretender").And.Contain("next: unrest"));
            Assert.That(teller.OpenLine(), Does.Contain("unrest on").And.Contain("tale_1"));
        }

        // --- Whole games ----------------------------------------------------------------------

        [Test]
        public async Task OverWholeGames_S2_ObeysItsRules_AndEveryChainIsAccounted()
        {
            foreach (var mode in new[] { ChainMode.Free, ChainMode.Skeleton })
                for (int seed = 1; seed <= 3; seed++)
                {
                    var options = ModelSeatOptions.Named("default+storyteller+s2+" + (mode == ChainMode.Free ? "chains-free" : "chains-skeleton"));
                    var game = new Game(seed) { EventTable = false };
                    game.Story = options.StoryRules();
                    game.Story.Settings.ChainChance = 0.8;
                    game.Story.Settings.ChoiceChance = 0.8;
                    var rng = new Random(seed);
                    var provider = new ScriptedProvider
                    {
                        Callback = req =>
                        {
                            if (req.AgentId == Storyteller.ScribeId) return ScriptedReply.Decide("title", TargetRegistry.NoTarget, "A Strange Turn");
                            var open = EventRules.Open(game.World, game.Events, game.NextTurn, game.Story);
                            // Lean on tales so chains form.
                            var tales = open.Where(o => o.target.StartsWith("tale_") || o.template.StartsWith("begin_")).ToList();
                            var (t, target) = tales.Count > 0 && rng.NextDouble() < 0.7 ? tales[rng.Next(tales.Count)] : open[rng.Next(open.Count)];
                            return ScriptedReply.Decide(t, target, "Something happened to someone.");
                        },
                    };
                    var teller = new Storyteller(game, provider, options);
                    var players = game.World.CountryList.ToDictionary(c => c.Id, c => (IPlayer)new RandomPlayer());
                    while (!game.IsOver)
                        await Await.NotCancelled(ModelGameRunner.PlayTurnAsync(game, players, new Dictionary<string, CountryAgent>(), default, teller));

                    var told = game.Events.Told.ToList();
                    string tag = $"{mode} seed {seed}";
                    Assert.That(told.Count, Is.GreaterThan(5), tag);
                    Assert.That(game.Reports.Count(r => r.EventRefused != null), Is.EqualTo(0), $"{tag}: what was offered landed");
                    Assert.That(told.Where(e => e.Options.Count > 0).All(e => e.Hit.All(e.Choices.ContainsKey)), Is.True, $"{tag}: every dilemma answered");
                    foreach (var c in game.Chains)
                    {
                        Assert.That(c.Events.Count, Is.LessThanOrEqualTo(ChainSkeletons.MaxEvents), tag);
                        Assert.That(c.Events.Select(e => e.ChainId).Distinct(), Is.EqualTo(new[] { c.Id }), tag);
                        Assert.That(c.Events.Select(e => e.ChainStage), Is.EqualTo(Enumerable.Range(1, c.Events.Count)), tag);
                        if (c.Status == ChainStatus.Completed) Assert.That(c.EndReason, Is.Not.Null, tag);
                    }
                    Assert.That(game.Chains.Count(c => c.Events.Count >= ChainSkeletons.Long), Is.LessThanOrEqualTo(1), $"{tag}: one long tale");
                    if (mode == ChainMode.Skeleton)
                        Assert.That(game.Chains.All(c => c.Skeleton != null), Is.True, tag);
                    Assert.That(game.Chains.Any(c => c.Started), Is.True, $"{tag}: a chain formed");
                }
        }

        [Test]
        public async Task ABlockOfALongerSeries_IsSeatedAsTheSeriesWouldSeatIt()
        {
            ILLMProvider Fake(World w) => new ScriptedProvider().Otherwise(ScriptedReply.Decide(ActionRegistry.None, TargetRegistry.NoTarget, "We wait."));
            var table = Tables.Named("fake,fake,fake,fake,fake", null, arm: "default", personas: true);
            string folder = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "gbs-s2-index-" + Guid.NewGuid().ToString("N"));
            try
            {
                await Await.NotCancelled(StrategyTableRuns.RunInterleavedAsync(folder, new List<(string, TableDefinition)> { ("p", table) },
                    (m, w) => Fake(w), 1, 8, 1, null, default, firstIndex: 7));
                string md = System.IO.File.ReadAllText(System.IO.Path.Combine(folder, "p", "game-0008.md"));
                var map = MapDefinition.Standard();
                for (int i = 0; i < map.Countries.Count; i++)
                    Assert.That(md, Does.Contain($"| {map.Countries[i].Id} | fake {table.PersonaFor(i, 7).Id} |"), "seat " + i);
            }
            finally
            {
                if (System.IO.Directory.Exists(folder)) System.IO.Directory.Delete(folder, true);
            }
        }

        [Test]
        public async Task AnS2Table_PlaysToTheEnd_AndKeepsChoicesOutOfTheVoice()
        {
            // The storyteller offers valen sellswords whenever it may; every ruler answers "hire" to anything,
            // which is legal only on a choice (the guards hold it on the council).
            ILLMProvider Fake(World w) => new ScriptedProvider
            {
                Callback = req =>
                {
                    if (req.AgentId == Storyteller.AgentId) return ScriptedReply.Decide("sellswords", "valen", "Sellswords came to valen.");
                    if (req.AgentId == Storyteller.ScribeId) return ScriptedReply.Decide("title", TargetRegistry.NoTarget, "The Sellswords");
                    return ScriptedReply.Decide("hire_sellswords", TargetRegistry.NoTarget, "Swords for gold.");
                },
            };
            var table = Tables.Named("fake,fake,fake,fake,fake", null, arm: "default+storyteller+s2+chains-free");
            var g = await Await.NotCancelled(TableRunner.PlayAsync(table, 2, 0, (m, w) => Fake(w)));
            Assert.That(g.Record.Game.Story.Choices && g.Record.Game.Story.History, Is.True);
            var choices = g.Game.Decisions.Where(d => d.Moment == Moment.Choice).ToList();
            Assert.That(choices, Is.Not.Empty, "valen was asked");
            Assert.That(g.Record.Reports.SelectMany(r => r.Events).Any(e => e.ChosenBy.Values.Contains("model")), Is.True);
            var scored = g.ToScored();
            Assert.That(scored.Decisions.Any(d => d.Moment == Moment.Choice), Is.False, "the voice and sense sections never see a choice");
            Assert.That(scored.Choices.Count, Is.EqualTo(choices.Count));
        }
    }
}

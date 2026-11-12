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
    /// <summary>A player that always holds, for a turn where only the model seats matter.</summary>
    internal sealed class HoldingPlayer : IPlayer
    {
        public string Kind => "holds";
        public Move Decide(CountryRequest request) =>
            request.Moment == Moment.Envoy ? new Move(Actions.RefusePeace) : Move.Hold;
    }

    public sealed class TableTests
    {
        // A fake model that plays like a scripted player of the given temperament, by asking one:
        // one script per seat, since a script remembers its own offers of peace.
        private static ILLMProvider LikeScript(World world, Temperament temperament)
        {
            var script = new ScriptedPlayer(temperament);
            return new ScriptedProvider { Callback = req => Answer(world, req, script.Decide) };
        }

        private static ScriptedReply Answer(World w, ScriptedRequest req, Func<CountryRequest, Move> decide)
        {
            string country = req.AgentId;
            string stimulus = req.Request.Stimulus ?? "";
            if (stimulus.EndsWith(" offers us peace."))
            {
                string from = stimulus.Substring(0, stimulus.IndexOf(' '));
                var envoy = new CountryRequest(Moment.Envoy, w.Turn, country, w, Facts.Standing(w, country), new List<string>(),
                    Legality.EnvoyMoves(), "offer", from);
                return ScriptedReply.Decide(decide(envoy).Action);
            }
            var council = new CountryRequest(Moment.Council, w.Turn, country, w, Facts.Standing(w, country), new List<string>(),
                Legality.CouncilMoves(w, country));
            var m = decide(council);
            return ScriptedReply.Decide(m.Action, m.Target ?? TargetRegistry.NoTarget, $"{country} chooses {m}.");
        }

        private static TableDefinition FiveFakes(bool personas) =>
            Tables.Named("fake,fake,fake,fake,fake", null, personas: personas);

        // --- The table and its rounds ------------------------------------------------------------

        [Test]
        public async Task FiveModelSeats_PlayExactlyTheGameFiveScriptsPlay()
        {
            // Every seat a framework agent, decided in rounds and executed through them in the game's
            // order, plays move for move the game five scripted expansionists play on their own.
            var table = FiveFakes(false);
            var viaRounds = await Await.NotCancelled(TableRunner.PlayAsync(table, 3, 0, (model, w) => LikeScript(w, Temperament.Expansionist)));
            var map = MapDefinition.Standard();
            var direct = GameRunner.Play(3, map.Countries.ToDictionary(c => c.Id, c => (IPlayer)new ScriptedPlayer(Temperament.Expansionist)), map);
            Assert.That(viaRounds.Record.Game.IsOver, Is.True);
            Assert.That(viaRounds.Record.Reports.Select(r => r.OwnerGrid), Is.EqualTo(direct.Reports.Select(r => r.OwnerGrid)));
            Assert.That(viaRounds.Record.Reports.Count, Is.GreaterThan(5));
            Assert.That(viaRounds.Game.Decisions.All(d => d.Offered && d.Error == null), Is.True);
        }

        [Test]
        public async Task EachTurnsCouncil_IsOneRound_AndEnvoysAreTheirOwn()
        {
            var table = FiveFakes(false);
            var game = await Await.NotCancelled(TableRunner.PlayAsync(table, 3, 0, (model, w) => LikeScript(w, Temperament.Cautious)));
            var decisions = game.Game.Decisions;
            foreach (var turn in decisions.Where(d => d.Moment == Moment.Council).GroupBy(d => d.Turn))
            {
                Assert.That(turn.Select(d => d.Round).Distinct(), Is.EqualTo(new[] { $"seed-3-turn-{turn.Key}-council" }),
                    $"turn {turn.Key}: every council decision in one round");
                Assert.That(turn.Count(), Is.EqualTo(game.Record.Game.Snapshots[turn.Key - 1].Army.Count), "one per living country");
            }
            var envoys = decisions.Where(d => d.Moment == Moment.Envoy).ToList();
            Assert.That(envoys, Is.Not.Empty, "cautious players offer and answer peace");
            Assert.That(envoys.All(d => d.Round != null && d.Round.StartsWith($"seed-3-turn-{d.Turn}-envoys")), Is.True);
            int landed = game.Record.Reports.SelectMany(r => r.Outcomes).Count(o => !o.Refused && o.Request.Moment == Moment.Council);
            Assert.That(decisions.Count(d => d.Moment == Moment.Council && d.Execution != null), Is.EqualTo(landed),
                "every council move that landed went through its round's Execute");
        }

        [Test]
        public async Task AMoveAnEarlierOneBroke_NeverLands_AndIsNotRemembered()
        {
            // valen and kesh both attack westmarch; valen moves first on turn 1 and takes it, so kesh's
            // attack, legal when the round decided it, is refused when it comes to land.
            var game = NewGame(1);
            game.World.Country("valen").Army = 500;
            var reply = ScriptedReply.Decide(Actions.Attack, "westmarch", "Westmarch is ours.");
            var seats = new Dictionary<string, CountryAgent>
            {
                ["valen"] = new CountryAgent(game.World, "valen", new ScriptedProvider().Otherwise(reply)),
                ["kesh"] = new CountryAgent(game.World, "kesh", new ScriptedProvider().Otherwise(reply)),
            };
            foreach (var kv in seats) game.Gates[kv.Key] = kv.Value;
            var players = new[] { "orsk", "merrow", "ilvar" }.ToDictionary(id => id, id => (IPlayer)new HoldingPlayer());
            var report = await Await.NotCancelled(ModelGameRunner.PlayTurnAsync(game, players, seats));

            Assert.That(game.World.Province("westmarch").Owner, Is.EqualTo("valen"));
            var valen = seats["valen"].Decisions.Single();
            var kesh = seats["kesh"].Decisions.Single();
            Assert.That(valen.Round, Is.EqualTo(kesh.Round), "decided in one round");
            Assert.That(valen.Execution, Is.EqualTo(ExecutionStatus.Done));
            Assert.That(Outcome(report, "kesh").Refused, Is.True);
            Assert.That(kesh.Execution, Is.Null, "the game's own re-check refused it before the round could");
            Assert.That(kesh.Stale, Is.False);
            var remembered = string.Join("\n", ((RollingHistory)seats["kesh"].Agent.Memory).Export().Exchanges.Select(e => e.Decision));
            Assert.That(remembered, Does.Not.Contain("westmarch"), "the round ended without it: memory never kept it");
            var valenMemory = string.Join("\n", ((RollingHistory)seats["valen"].Agent.Memory).Export().Exchanges.Select(e => e.Decision));
            Assert.That(valenMemory, Does.Contain("westmarch"));
        }

        [Test]
        public async Task MixedTable_ModelsScriptsAndRandom_PlayTogether()
        {
            var table = Tables.Named("fake,other,expansionist,cautious,random", null, personas: true);
            var models = new List<string>();
            var game = await Await.NotCancelled(TableRunner.PlayAsync(table, 4, 0, (model, w) =>
            {
                models.Add(model);
                return LikeScript(w, Temperament.Expansionist);
            }));
            Assert.That(models, Is.EquivalentTo(new[] { "fake", "other" }), "one provider asked for per model seat");
            Assert.That(game.Record.Game.IsOver, Is.True);
            Assert.That(game.Game.ModelCountries.Count, Is.EqualTo(2));
            Assert.That(game.Labels.Values, Is.EquivalentTo(new[] { "fake", "other", "expansionist", "cautious", "random" }));
            Assert.That(game.Game.Decisions.All(d => d.Persona != null), Is.True, "model seats play personas");
            Assert.That(game.Characters.Values, Does.Contain("random"), "a script's character is its kind");
        }

        // --- Seating -----------------------------------------------------------------------------

        [Test]
        public void Rotation_EveryCountryPlaysEverySeatAndPersona_AndEachPersonaMeetsEachModel()
        {
            var table = Tables.Named("two-models", "a", "b");
            Assert.That(table.UsesPersonas, Is.True);
            var pairs = new Dictionary<(string persona, string model), int>();
            for (int country = 0; country < 5; country++)
            {
                var seats = Enumerable.Range(0, 5).Select(g => table.Seats.ToList().IndexOf(table.SeatFor(country, g))).ToList();
                Assert.That(seats, Is.EquivalentTo(new[] { 0, 1, 2, 3, 4 }), $"country {country}: every seat once in five games");
                var personas = Enumerable.Range(0, 5).Select(g => table.PersonaFor(country, g).Id).ToList();
                Assert.That(personas, Is.EquivalentTo(Personas.All.Select(p => p.Id)), $"country {country}: every persona once in five games");
            }
            for (int g = 0; g < 5; g++)
            for (int country = 0; country < 5; country++)
            {
                var key = (table.PersonaFor(country, g).Id, table.SeatFor(country, g).Model);
                pairs[key] = pairs.TryGetValue(key, out int n) ? n + 1 : 1;
            }
            foreach (var p in Personas.All)
            {
                Assert.That(pairs[(p.Id, "a")], Is.EqualTo(3), $"{p.Id} played by a in 3 of 5 games");
                Assert.That(pairs[(p.Id, "b")], Is.EqualTo(2), $"{p.Id} played by b in 2 of 5 games");
            }
        }

        [Test]
        public void Tables_ByName()
        {
            Assert.That(Tables.Named("models", "m").UsesPersonas, Is.False);
            Assert.That(Tables.Named("personas", "m").UsesPersonas, Is.True);
            Assert.That(Tables.Named("personas", "m", personas: false).UsesPersonas, Is.False);
            Assert.That(Tables.Named("scripted", null).Models, Is.Empty);
            Assert.That(Tables.Named("random", null).Seats.All(s => s.Kind == Lineups.Random), Is.True);
            Assert.That(Tables.Named("personas", "m", arm: "default+no-grounding").Options.CheckTarget, Is.False);
            Assert.That(Tables.Named("nonsense", "m"), Is.Null);
            Assert.That(Tables.Named("model-vs-scripted", "m").Seats.Count(s => s.IsModel), Is.EqualTo(1));
            Assert.Throws<ArgumentException>(() => Tables.Named("two-models", "m"), "two-models needs a second model");
            Assert.Throws<ArgumentException>(() => Tables.Named("personas", "m", arm: "nonsense"));
            Assert.That(Tables.Named("models", "m").CharacterFor(0, 0), Is.EqualTo("slot 1"), "a placebo slot where there is no persona");
        }

        // --- Personas ----------------------------------------------------------------------------

        [Test]
        public void Persona_ReachesThePromptThroughTheIdentity_AndNoneKeepsStageTwo()
        {
            var game = NewGame();
            var council = game.BeginTurn();
            var request = For(council, "valen");
            var plain = new CountryAgent(game.World, "valen", new ScriptedProvider());
            var proud = new CountryAgent(game.World, "valen", new ScriptedProvider(), persona: Personas.Proud);
            Assert.That(plain.Agent.Identity.Personality, Is.EqualTo(game.World.Country("valen").Info.Personality));
            Assert.That(plain.Agent.Identity.Knowledge, Is.Empty);
            Assert.That(plain.Agent.Identity.StatementRules, Is.Empty);

            string prompt = proud.Agent.Preview(proud.StimulusFor(request), proud.OptionsFor(request)).Request.SystemPrompt;
            Assert.That(prompt, Does.Contain(Personas.Proud.Personality));
            foreach (string line in Personas.Proud.Knowledge) Assert.That(prompt, Does.Contain(line));
            Assert.That(prompt, Does.Contain("About honour or revenge."));
            Assert.That(prompt, Does.Not.Contain(game.World.Country("valen").Info.Personality), "the persona replaces the country's line");
            Assert.That(prompt, Does.Contain("Valen"), "the country is still who it rules");
            Finish(game, council, null);
        }

        [Test]
        public async Task Personas_ThatPlayDifferently_AreTold_AndATableWithoutThemIsNot()
        {
            // A fake model that attacks whenever it can if its prompt carries the warmonger, and
            // otherwise holds. With personas the character test must see it; without, all hold.
            ILLMProvider Fake(World w) => new ScriptedProvider
            {
                Callback = req => Answer(w, req, r =>
                {
                    bool war = req.Request.SystemPrompt.Contains(Personas.Warmonger.Personality);
                    var attack = r.Legal.FirstOrDefault(m => m.Action == Actions.Attack) ?? r.Legal.FirstOrDefault(m => m.Action == Actions.DeclareWar);
                    return r.Moment == Moment.Envoy ? new Move(Actions.RefusePeace) : war && attack != null ? attack : Move.Hold;
                }),
            };
            var seeds = GameRunner.Seeds(10);
            var with = await Await.NotCancelled(TableRunner.PlaySeriesAsync(FiveFakes(true), seeds, (m, w) => Fake(w)));
            var without = await Await.NotCancelled(TableRunner.PlaySeriesAsync(FiveFakes(false), seeds, (m, w) => Fake(w)));
            var a = new Scorecard("with", with.Select(g => g.ToScored()).ToList());
            var b = new Scorecard("without", without.Select(g => g.ToScored()).ToList());

            Assert.That(a.Get("character", "warmonger", "aggression"), Is.GreaterThan(0.1));
            Assert.That(a.Get("character", "merchant", "aggression"), Is.Zero);
            Assert.That(a.Get("character", "table", "eta2 aggression"), Is.GreaterThan(0.5));
            Assert.That(a.Get("character", "table", "p aggression"), Is.LessThan(0.01));
            Assert.That(b.Get("character", "table", "eta2 aggression"), Is.Zero);
            Assert.That(b.Get("character", "table", "p aggression"), Is.EqualTo(1).Within(1e-9));
            Assert.That(b.Get("character", "slot 1", "none"), Is.EqualTo(1), "without a persona every seat holds");
        }

        // --- The scorecard -----------------------------------------------------------------------

        [Test]
        public void Scorecard_Arithmetic()
        {
            Assert.That(Scorecard.Gini(new double[] { 3, 3, 3, 3, 3 }), Is.EqualTo(0));
            Assert.That(Scorecard.Gini(new double[] { 15, 0, 0, 0, 0 }), Is.EqualTo(0.8).Within(1e-9));
            Assert.That(Scorecard.Gini(new double[] { 0, 0, 0, 0, 0 }), Is.EqualTo(0));
            Assert.That(Scorecard.Eta2(new double[] { 1, 1, 0, 0 }, new[] { "a", "a", "b", "b" }), Is.EqualTo(1));
            Assert.That(Scorecard.Eta2(new double[] { 1, 0, 1, 0 }, new[] { "a", "a", "b", "b" }), Is.EqualTo(0));
            Assert.That(Scorecard.Words("  We march   on orsk. "), Is.EqualTo(4));
            Assert.That(Scorecard.Normal(" We  March\non orsk. "), Is.EqualTo(Scorecard.Normal("we march on orsk.")));
            var world = NewGame().World;
            Assert.That(Scorecard.Named("Orsk will fall; Fenmark is ours. Valenwood is not a place.", world),
                Is.EquivalentTo(new[] { "orsk", "fenmark" }), "whole words, any case");
        }

        [Test]
        public void Scorecard_Liveliness_CountsLeadChangesAndWarsFromTheReports()
        {
            var game = NewGame();
            game.World.Country("valen").Army = 300;
            game.World.Country("kesh").Army = 300;
            PlayTurn(game, ("kesh", Actions.Attack, "ravenholt"));           // kesh 4: leads alone
            PlayTurn(game, ("valen", Actions.Attack, "fenmark"));            // 4 and 4: a tie keeps kesh
            PlayTurn(game, ("kesh", Actions.Attack, "westmarch"));           // kesh 5
            PlayTurn(game, ("valen", Actions.Attack, "stonereach"));         // 5 and 5: still kesh
            PlayTurn(game, ("valen", Actions.Attack, "harrow"));             // valen 6: the one change
            PlayTurn(game, ("valen", Actions.DeclareWar, "merrow"));
            Assert.That(game.World.ProvinceCount("valen"), Is.EqualTo(6), "every attack was won");
            var record = new GameRecord(game, game.World.CountryList.ToDictionary(c => c.Id, c => "holds"));
            Assert.That(Scorecard.LeadChanges(record), Is.EqualTo(1));
            var card = new Scorecard("t", new[] { new ScoredGame(record, null, null) });
            Assert.That(card.Get("liveliness", "table", "lead changes a game"), Is.EqualTo(1));
            Assert.That(card.Get("liveliness", "table", "wars declared a game"), Is.EqualTo(1));
            Assert.That(card.Get("liveliness", "table", "turns"), Is.EqualTo(6));
            Assert.That(card.Get("liveliness", "table", "provinces taken from a country a game"), Is.EqualTo(0), "all from the neutrals");
            Assert.That(card.Get("liveliness", "table", "battles a turn"), Is.EqualTo(5 / 6.0).Within(1e-9));
        }

        [Test]
        public void Scorecard_AnswersAnAttack_StrikingBackFortifyingOrSuingForPeace_ButNotHolding([Values] bool answer)
        {
            var game = NewGame();
            Give(game.World, "stonereach", "orsk"); // orsk now borders valen's ashby
            game.World.SetRelation("valen", "orsk", Relation.War);
            game.World.Country("orsk").Army = 500;
            PlayTurn(game, ("orsk", Actions.Attack, "ashby"));
            Assert.That(game.World.Province("ashby").Owner, Is.EqualTo("orsk"));
            if (answer) PlayTurn(game, ("valen", Actions.OfferPeace, "orsk"));
            else PlayTurn(game);
            PlayTurn(game);
            var kinds = game.World.CountryList.ToDictionary(c => c.Id, c => c.Id == "valen" ? "victim" : "other");
            var card = new Scorecard("t", new[] { new ScoredGame(new GameRecord(game, kinds), null, null) });
            Assert.That(card.Get("sense", "victim", "attacks suffered"), Is.EqualTo(1));
            Assert.That(card.Get("sense", "victim", "answers an attack"), Is.EqualTo(answer ? 1 : 0));
            Assert.That(card.Get("sense", "other", "aims at the weaker"), Is.EqualTo(1), "orsk's 500 against valen's 30");
            Assert.That(card.Get("sense", "other", "attack win rate"), Is.EqualTo(1));
        }

        [Test]
        public void Scorecard_PeaceWhenLosing_IsReadFromTheArmiesAtTheCouncil([Values] bool accept)
        {
            var game = NewGame();
            game.World.SetRelation("valen", "orsk", Relation.War);
            game.World.Country("orsk").Army = 500;
            PlayTurn(game, r => accept, ("orsk", Actions.OfferPeace, "valen"));
            var kinds = game.World.CountryList.ToDictionary(c => c.Id, c => c.Id);
            var card = new Scorecard("t", new[] { new ScoredGame(new GameRecord(game, kinds), null, null) });
            Assert.That(card.Get("sense", "valen", "peace offers received"), Is.EqualTo(1));
            Assert.That(card.Get("sense", "valen", "accepts peace when losing"), Is.EqualTo(accept ? 1 : 0));
            Assert.That(card.Get("sense", "valen", "accepts peace when winning"), Is.NaN, "no offer from a weaker country");
        }

        [Test]
        public void Scorecard_Voice_RepeatsTargetsAndContradictions()
        {
            var game = NewGame();
            PlayTurn(game);
            var record = new GameRecord(game, game.World.CountryList.ToDictionary(c => c.Id, c => "m"));
            SeatDecisionInfo Said(int turn, string action, string target, string statement) => new SeatDecisionInfo
            {
                Seed = 1, Turn = turn, Country = "valen", Moment = Moment.Council, Answered = true, AskedLegal = true,
                AskedAction = action, AskedTarget = target, Statement = statement,
            };
            var said = new[]
            {
                Said(1, Actions.Attack, "fenmark", "Fenmark will be ours."),
                Said(2, Actions.Attack, "fenmark", "We march on orsk."),       // names someone else: contradicts
                Said(3, Actions.Recruit, null, "fenmark will  be ours."),       // the first, again
                Said(4, Actions.Attack, "fenmark", "Forward!"),                 // names nobody: neither
            };
            var characters = game.World.CountryList.ToDictionary(c => c.Id, c => "warmonger");
            var card = new Scorecard("t", new[] { new ScoredGame(record, null, characters, said) });
            Assert.That(card.Get("voice", "warmonger", "statements given"), Is.EqualTo(1));
            Assert.That(card.Get("voice", "warmonger", "repeated word for word"), Is.EqualTo(0.25));
            Assert.That(card.Get("voice", "warmonger", "targeted statements"), Is.EqualTo(3));
            Assert.That(card.Get("voice", "warmonger", "names its target"), Is.EqualTo(1 / 3.0).Within(1e-9));
            Assert.That(card.Get("voice", "warmonger", "contradicts its move"), Is.EqualTo(0.5), "1 of the 2 that name anyone");
            Assert.That(card.Get("voice", "all: m", "answers"), Is.EqualTo(4));
        }

        [Test]
        public void Scorecard_Voice_ClaimsOfLandTaken()
        {
            Assert.That(Scorecard.ClaimsLandTaken("Valen has taken our land; now they shall pay."), Is.True);
            Assert.That(Scorecard.ClaimsLandTaken("Peace from those who stole our land is an insult!"), Is.True);
            Assert.That(Scorecard.ClaimsLandTaken("They will bleed for taking our land back."), Is.True);
            Assert.That(Scorecard.ClaimsLandTaken("Merrow is seizing my provinces!"), Is.True);
            Assert.That(Scorecard.ClaimsLandTaken("We seek the land they took from us."), Is.True);
            Assert.That(Scorecard.ClaimsLandTaken("We will take their land."), Is.False, "a plan, not a grievance");
            Assert.That(Scorecard.ClaimsLandTaken("We are taking back our land."), Is.False);
            Assert.That(Scorecard.ClaimsLandTaken("We took fenmark. Our land grows."), Is.False, "two sentences");
            Assert.That(Scorecard.ClaimsLandTaken(null), Is.False);
        }

        [Test]
        public void Scorecard_Voice_AGrievanceIsInventedWhenTheAccusedTookNothingFromUs()
        {
            var game = NewGame();
            Give(game.World, "stonereach", "valen");
            game.World.SetRelation("valen", "orsk", Relation.War);
            game.World.Country("orsk").Army = 500;
            var first = PlayTurn(game, ("orsk", Actions.Attack, "stonereach"));
            Assert.That(Outcome(first, "orsk").Text, Does.StartWith("took stonereach from valen ("));
            PlayTurn(game);
            var record = new GameRecord(game, game.World.CountryList.ToDictionary(c => c.Id, c => "m"));
            SeatDecisionInfo Said(int turn, string statement, string action = Actions.Recruit, string target = null, string offeredBy = null) =>
                new SeatDecisionInfo
                {
                    Seed = 1, Turn = turn, Country = "valen", Answered = true, AskedLegal = true, Statement = statement,
                    Moment = offeredBy != null ? Moment.Envoy : Moment.Council, AskedAction = action, AskedTarget = target, OfferedBy = offeredBy,
                };
            var said = new[]
            {
                Said(1, "Orsk took our land!"),                                      // before it happened: invented
                Said(2, "Orsk took our land, again!"),                               // true
                Said(2, "Kesh stole our provinces; they will pay."),                 // kesh took nothing: invented
                Said(2, "Peace from those who took our land is an insult.", Actions.RefusePeace, null, "orsk"), // the offerer: true
                Said(2, "Peace from those who stole our land is an insult.", Actions.RefusePeace, null, "kesh"), // invented
                Said(2, "They will bleed for taking our land back.", Actions.Attack, "corwick"), // corwick is orsk's: true
                Said(2, "We will take their land."),                                 // no claim
                Said(2, "Merrow is taking our fields!"),                             // invented
            };
            var card = new Scorecard("t", new[] { new ScoredGame(record, null, null, said) });
            Assert.That(card.Get("voice", "m", "claims land taken"), Is.EqualTo(7 / 8.0));
            Assert.That(card.Get("voice", "m", "invented grievance"), Is.EqualTo(4 / 8.0));
        }

        [Test]
        public void Scorecard_ReferenceTables_ScriptsRarelyAcceptPeaceWhenWinning_AndRandomAcceptsHalf()
        {
            var seeds = GameRunner.Seeds(30);
            var scripted = GameRunner.PlaySeries("scripted", seeds);
            var card = new Scorecard("scripted", scripted.Select(r => new ScoredGame(r, null, null)).ToList());
            // A script offers peace only when outmatched, so offers nearly always come from the weaker
            // side, and a script mostly refuses them: well below random's half.
            foreach (string kind in new[] { Lineups.Expansionist, Lineups.Cautious })
            {
                Assert.That(card.Get("sense", kind, "peace offers received"), Is.GreaterThan(10), kind);
                Assert.That(card.Get("sense", kind, "accepts peace when winning"), Is.LessThan(0.3), kind);
            }
            Assert.That(card.Get("voice", "expansionist", "answers"), Is.NaN, "scripts say nothing");
            Assert.That(card.ToMarkdown(), Does.Contain("Nothing to score: no seat here makes statements."));

            var random = new Scorecard("random", GameRunner.PlaySeries("random", seeds).Select(r => new ScoredGame(r, null, null)).ToList());
            Assert.That(random.Get("sense", "random", "accepts peace when losing"), Is.EqualTo(0.5).Within(0.15));
            Assert.That(random.Get("sense", "random", "accepts peace when winning"), Is.EqualTo(0.5).Within(0.15));
            Assert.That(random.Get("liveliness", "table", "turns"), Is.GreaterThan(card.Get("liveliness", "table", "turns")), "random games drag on");
            Assert.That(random.ToCsv().Split('\n')[0], Is.EqualTo("table,section,group,metric,value"));
            var perGame = random.PerGameCsv().Split('\n');
            Assert.That(perGame[0], Is.EqualTo("table,seed,section,group,metric,value"));
            Assert.That(perGame.Count(l => l.Contains(",liveliness,table,turns,")), Is.EqualTo(30), "one row a game");
            Assert.That(perGame, Has.Some.StartsWith("random,7,liveliness,table,turns,30"));
        }
    }
}

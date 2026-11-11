using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Agenerela;
using Agenerela.Providers;
using NUnit.Framework;
using UnityEngine;
using static GreyBoxStrategy.Tests.TestGames;

namespace GreyBoxStrategy.Tests
{
    /// <summary>
    /// The framework's own <c>Fixtures.NotCancelled</c> lives in its test assembly, which a demo
    /// cannot reference, so this is the same thing: the Test Framework counts a test whose task
    /// ends cancelled as passed, and this turns that into a failure.
    /// </summary>
    internal static class Await
    {
        public static async Task<T> NotCancelled<T>(Awaitable<T> awaitable)
        {
            try
            {
                return await awaitable;
            }
            catch (OperationCanceledException e)
            {
                Assert.Fail($"It ended cancelled, though nothing cancelled it: {e.Message}");
                return default;
            }
        }
    }

    public sealed class ModelSeatTests
    {
        [Test]
        public void Availability_TheAgentIsOfferedExactlyTheLegalMovesAndPairs()
        {
            var game = NewGame(5);
            game.World.Country("valen").Army = 200;
            Give(game.World, "stonereach", "valen");
            game.World.SetRelation("valen", "orsk", Relation.War);
            var council = game.BeginTurn();
            var request = For(council, "valen");
            var seat = new CountryAgent(game.World, "valen", new ScriptedProvider());
            var preview = seat.Agent.Preview(seat.StimulusFor(request), seat.OptionsFor(request));

            var offered = new HashSet<string>(preview.Available.Select(a => a.Id));
            var legalActions = new HashSet<string>(request.Legal.Select(m => m.Action).Where(a => a != Actions.None));
            Assert.That(offered, Is.EquivalentTo(legalActions), "action-wide availability");

            var rejected = new HashSet<string>(preview.TargetsUnavailable.Select(u => u.Action + " " + u.Target));
            var collected = preview.TargetSources.SelectMany(src => src.Kept).ToList();
            Assert.That(collected.Count, Is.EqualTo(25 + 4), "every province and every other country");
            int pairs = 0;
            foreach (string action in new[] { Actions.Attack, Actions.Fortify, Actions.DeclareWar, Actions.OfferPeace })
            {
                if (!offered.Contains(action)) continue;
                var def = seat.Agent.Actions.Definitions.First(d => d.Id == action);
                foreach (string id in collected.Where(t => def.AcceptsCategory(preview.Context.Targets.CategoryOf(t))))
                {
                    bool legal = request.Legal.Contains(new Move(action, id));
                    Assert.That(!rejected.Contains(action + " " + id), Is.EqualTo(legal), $"{action} {id}");
                    pairs++;
                }
            }
            Assert.That(pairs, Is.GreaterThan(20), "the check looked at real pairs");
            Assert.That(rejected.Count, Is.GreaterThan(0), "some pairs are illegal in this state");
            Assert.That(offered, Does.Not.Contain(Actions.AcceptPeace), "envoy answers are not council moves");
            Finish(game, council, null);
        }

        [Test]
        public void Envoy_OffersOnlyTheTwoAnswers()
        {
            var game = NewGame();
            game.World.SetRelation("valen", "orsk", Relation.War);
            var council = game.BeginTurn();
            var envoy = game.SubmitCouncil(new[] { new CountryAnswer(For(council, "orsk"), new Move(Actions.OfferPeace, "valen")) })[0];
            var seat = new CountryAgent(game.World, "valen", new ScriptedProvider());
            var preview = seat.Agent.Preview(seat.StimulusFor(envoy), seat.OptionsFor(envoy));
            Assert.That(preview.Available.Select(a => a.Id), Is.EquivalentTo(new[] { Actions.AcceptPeace, Actions.RefusePeace }));
            Assert.That(seat.StimulusFor(envoy), Is.EqualTo("orsk offers us peace."));
            game.Resolve();
        }

        [Test]
        public async Task Envoy_AcceptedByTheModelMakesTheTruce()
        {
            var game = NewGame();
            game.World.SetRelation("valen", "orsk", Relation.War);
            var provider = new ScriptedProvider().Otherwise(ScriptedReply.Decide(Actions.AcceptPeace));
            var seat = new CountryAgent(game.World, "valen", provider);
            game.Gates["valen"] = seat;
            var council = game.BeginTurn();
            var envoys = game.SubmitCouncil(new[] { new CountryAnswer(For(council, "orsk"), new Move(Actions.OfferPeace, "valen")) });
            var answer = await Await.NotCancelled(seat.DecideAsync(envoys[0]));
            game.Resolve(new[] { answer });
            Assert.That(game.World.RelationBetween("valen", "orsk"), Is.EqualTo(Relation.Truce));
            Assert.That(seat.Decisions.Single().Execution, Is.EqualTo(ExecutionStatus.Done));
        }

        [Test]
        public async Task TwoEnvoysInOneTurn_EachDecisionKeepsItsOwnExecution()
        {
            var game = NewGame();
            game.World.SetRelation("valen", "orsk", Relation.War);
            game.World.SetRelation("valen", "merrow", Relation.War);
            var provider = new ScriptedProvider()
                .When("valen", "orsk offers us peace.", ScriptedReply.Decide(Actions.AcceptPeace))
                .When("valen", "merrow offers us peace.", ScriptedReply.Decide(Actions.RefusePeace));
            var seat = new CountryAgent(game.World, "valen", provider);
            game.Gates["valen"] = seat;
            var council = game.BeginTurn();
            var envoys = game.SubmitCouncil(new[]
            {
                new CountryAnswer(For(council, "orsk"), new Move(Actions.OfferPeace, "valen")),
                new CountryAnswer(For(council, "merrow"), new Move(Actions.OfferPeace, "valen")),
            });
            Assert.That(envoys.Count, Is.EqualTo(2));
            var replies = new List<CountryAnswer>();
            foreach (var e in envoys) replies.Add(await Await.NotCancelled(seat.DecideAsync(e)));
            game.Resolve(replies);
            var records = seat.Decisions.Where(d => d.Moment == Moment.Envoy).ToList();
            Assert.That(records.Select(r => r.Move.Action), Is.EquivalentTo(new[] { Actions.AcceptPeace, Actions.RefusePeace }));
            Assert.That(records.All(r => r.Execution == ExecutionStatus.Done), Is.True, "each record got its own Execute");
            Assert.That(game.World.RelationBetween("valen", "orsk"), Is.EqualTo(Relation.Truce));
            Assert.That(game.World.AtWar("valen", "merrow"), Is.True);
        }

        [Test]
        public async Task ALegalMove_IsCarriedOutThroughAgentExecute()
        {
            var game = NewGame();
            var provider = new ScriptedProvider().Otherwise(ScriptedReply.Decide(Actions.Attack, "westmarch", "Westmarch is ours."));
            game.World.Country("valen").Army = 200;
            var seat = new CountryAgent(game.World, "valen", provider);
            game.Gates["valen"] = seat;
            var council = game.BeginTurn();
            var answer = await Await.NotCancelled(seat.DecideAsync(For(council, "valen")));
            Assert.That(answer.Choice, Is.EqualTo(new Move(Actions.Attack, "westmarch")));
            game.SubmitCouncil(new[] { answer });
            var report = game.Resolve();
            seat.EndTurn();
            Assert.That(game.World.Province("westmarch").Owner, Is.EqualTo("valen"));
            var d = seat.Decisions.Single();
            Assert.That(d.Execution, Is.EqualTo(ExecutionStatus.Done));
            Assert.That(d.ModelAnswerLegal, Is.True);
            Assert.That(Outcome(report, "valen").Note, Does.Contain("Westmarch is ours."));
            Assert.That(provider.LastRequest.Request.Observations, Does.Contain("Moves open to us: attack fenmark, stonereach or westmarch"));
        }

        [Test]
        public async Task AnIllegalPair_IsContainedHeldAndLogged()
        {
            var game = NewGame();
            // ilvarion is a registered target, but not one attack can take now.
            var provider = new ScriptedProvider().Otherwise(ScriptedReply.Decide(Actions.Attack, "ilvarion"));
            var seat = new CountryAgent(game.World, "valen", provider);
            game.Gates["valen"] = seat;
            var council = game.BeginTurn();
            var answer = await Await.NotCancelled(seat.DecideAsync(For(council, "valen")));
            Assert.That(answer.Choice, Is.EqualTo(Move.Hold));
            var d = seat.Decisions.Single();
            Assert.That(d.ModelAction, Is.EqualTo(Actions.Attack));
            Assert.That(d.Verdict, Is.Not.Null, "a guard stopped it");
            Assert.That(d.ModelAnswerLegal, Is.False, "the model's own answer was not offered");
            Assert.That(answer.Note, Does.Contain("ilvarion"));
            game.SubmitCouncil(new[] { answer });
            var report = game.Resolve();
            Assert.That(Outcome(report, "valen").Note, Does.Contain("model: `attack ilvarion`"));
            Assert.That(game.World.Province("ilvarion").Owner, Is.EqualTo("ilvar"));
        }

        [Test]
        public async Task ATargetOnAMoveThatTakesNone_IsDropped()
        {
            var game = NewGame();
            var provider = new ScriptedProvider().Otherwise(ScriptedReply.Decide(Actions.Recruit, "highmoor"));
            // Only a flat schema lets the model name a target with recruit; the default pairs them.
            var seat = new CountryAgent(game.World, "valen", provider, ModelSeatOptions.Control);
            game.Gates["valen"] = seat;
            var council = game.BeginTurn();
            var answer = await Await.NotCancelled(seat.DecideAsync(For(council, "valen")));
            Assert.That(answer.Choice, Is.EqualTo(new Move(Actions.Recruit)));
            var d = seat.Decisions.Single();
            Assert.That(d.TargetIgnored, Is.True);
            Assert.That(d.ModelTarget, Is.EqualTo("highmoor"));
            Assert.That(d.Target, Is.EqualTo(TargetRegistry.NoTarget), "The framework dropped it before the handler (#244 item 2).");
            game.SubmitCouncil(new[] { answer });
            var report = game.Resolve();
            Assert.That(Outcome(report, "valen").Text, Does.StartWith("recruited"));
        }

        [Test]
        public async Task ATargetOnAMoveThatTakesNone_UnderThePairedDefault_IsRejectedAndHeld()
        {
            // The paired schema's grammar forbids recruit with a target, so a provider that sends one
            // anyway broke the contract: SchemaLegalityGuard rejects it rather than dropping it.
            var game = NewGame();
            var provider = new ScriptedProvider().Otherwise(ScriptedReply.Decide(Actions.Recruit, "highmoor"));
            var seat = new CountryAgent(game.World, "valen", provider);
            var council = game.BeginTurn();
            var answer = await Await.NotCancelled(seat.DecideAsync(For(council, "valen")));
            Assert.That(answer.Choice, Is.EqualTo(Move.Hold));
            var d = seat.Decisions.Single();
            Assert.That((d.Guard, d.Verdict), Is.EqualTo(("SchemaLegalityGuard", (GuardVerdictKind?)GuardVerdictKind.Reject)));
            Assert.That(d.ModelAnswerLegal, Is.True, "the move itself, recruit, was offered");
            Finish(game, council, null);
        }

        [Test]
        public async Task APipelineError_HoldsAndIsRecorded()
        {
            var game = NewGame();
            var provider = new ScriptedProvider().Otherwise(ScriptedReply.Fail(ProviderErrorKind.Timeout, "too slow"));
            var seat = new CountryAgent(game.World, "valen", provider);
            game.Gates["valen"] = seat;
            var council = game.BeginTurn();
            var answer = await Await.NotCancelled(seat.DecideAsync(For(council, "valen")));
            Assert.That(answer.Choice, Is.EqualTo(Move.Hold));
            Assert.That(seat.Decisions.Single().Error, Is.Not.Null);
            game.SubmitCouncil(new[] { answer });
            game.Resolve();
        }

        [Test]
        public async Task AStaleMove_IsRefusedByTheGameAndNeverRemembered()
        {
            var game = NewGame();
            game.World.Country("valen").Army = 200;
            Give(game.World, "stonereach", "valen");
            game.World.SetRelation("valen", "orsk", Relation.War);
            var provider = new ScriptedProvider()
                .When("valen", "Your move.", ScriptedReply.Decide(Actions.Attack, "corwick"))
                .When("valen", "orsk offers us peace.", ScriptedReply.Decide(Actions.AcceptPeace));
            var seat = new CountryAgent(game.World, "valen", provider);
            game.Gates["valen"] = seat;
            var council = game.BeginTurn();
            var attack = await Await.NotCancelled(seat.DecideAsync(For(council, "valen")));
            var envoys = game.SubmitCouncil(new[] { attack, new CountryAnswer(For(council, "orsk"), new Move(Actions.OfferPeace, "valen")) });
            var accept = await Await.NotCancelled(seat.DecideAsync(envoys[0]));
            var report = game.Resolve(new[] { accept });
            seat.EndTurn();
            Assert.That(Outcome(report, "valen").Refused, Is.True, "peace landed first");
            var council1 = seat.Decisions.First(d => d.Moment == Moment.Council);
            Assert.That(council1.Execution, Is.Null, "the game refused it before asking the agent");
            var history = string.Join("\n", ((RollingHistory)seat.Agent.Memory).Export().Exchanges.Select(e => e.Decision));
            Assert.That(history, Does.Not.Contain("corwick"), "a move that never landed is not remembered");
            Assert.That(history, Does.Contain(Actions.AcceptPeace));
        }

        // A fake model that plays like the scripted expansionist would, by asking it, so a whole
        // game through the framework is repeatable and its moves are always legal.
        private static async Task<ModelGameRecord> PlayWithFakeModel(int seed)
        {
            return await Await.NotCancelled(ModelGameRunner.PlayAsync(seed,
                ModelGameRunner.SeatAgainstScripted(ModelGameRunner.Model),
                (world, id) =>
                {
                    var provider = new ScriptedProvider();
                    var script = new ScriptedPlayer(Temperament.Expansionist); // one per seat: it remembers its peace offers
                    provider.Callback = req => Like(world, script, req);
                    return new CountryAgent(world, id, provider);
                }, "fake"));
        }

        [Test]
        public async Task WholeGame_EverythingButTheModelIsDeterministic()
        {
            var a = await PlayWithFakeModel(21);
            var b = await PlayWithFakeModel(21);
            Assert.That(a.Record.Game.IsOver, Is.True);
            Assert.That(StripLatency(GameLog.Write(b.Record)), Is.EqualTo(StripLatency(GameLog.Write(a.Record))));
            Assert.That(a.Decisions.Count, Is.GreaterThan(5));
            Assert.That(a.Decisions.All(d => d.Offered), Is.True, "the fake model only makes offered moves");
            Assert.That(a.Decisions.All(d => d.Verdict == null && d.Error == null), Is.True);
        }

        [Test]
        public async Task WholeGame_ThroughTheFrameworkTheScriptPlaysTheSameGameAsDirectly([Values(1, 2, 3)] int seed)
        {
            // The seat adds nothing to the play: a fake model that answers as the script would gives,
            // move for move, the game the script plays on its own in that seat.
            var viaAgent = await PlayWithFakeModel(seed);
            var map = MapDefinition.Standard();
            var kinds = ModelGameRunner.SeatAgainstScripted(Lineups.Expansionist);
            var direct = GameRunner.Play(seed, Enumerable.Range(0, 5).ToDictionary(i => map.Countries[i].Id, i => Lineups.Make(kinds[i])), map);
            Assert.That(viaAgent.Record.Reports.Select(r => r.OwnerGrid), Is.EqualTo(direct.Reports.Select(r => r.OwnerGrid)));
            string seat = viaAgent.ModelCountries.Single();
            int landed = viaAgent.Record.Reports.SelectMany(t => t.Outcomes)
                .Count(o => o.Request.CountryId == seat && !o.Refused);
            Assert.That(viaAgent.Decisions.Count(d => d.Execution != null), Is.EqualTo(landed), "every landed move went through Agent.Execute");
        }

        private static ScriptedReply Like(World w, ScriptedPlayer script, ScriptedRequest req)
        {
            string country = req.AgentId;
            string stimulus = req.Request.Stimulus ?? "";
            if (stimulus.EndsWith(" offers us peace."))
            {
                string from = stimulus.Substring(0, stimulus.IndexOf(' '));
                var envoy = new CountryRequest(Moment.Envoy, w.Turn, country, w, Facts.Standing(w, country), new List<string>(),
                    Legality.EnvoyMoves(), "offer", from);
                return ScriptedReply.Decide(script.Decide(envoy).Action);
            }
            var legal = Legality.CouncilMoves(w, country);
            var r = new CountryRequest(Moment.Council, w.Turn, country, w, Facts.Standing(w, country), new List<string>(), legal);
            var m = script.Decide(r);
            return ScriptedReply.Decide(m.Action, m.Target ?? TargetRegistry.NoTarget);
        }

        private static string StripLatency(string log) =>
            System.Text.RegularExpressions.Regex.Replace(log, @", \d+\.\d\d s", "");

        [Test]
        public void Stimulus_PlainOrHeadline()
        {
            var game = NewGame();
            game.World.Country("valen").Army = 200;
            game.World.Country("valen").Gold = 1000;
            PlayTurn(game, ("valen", Actions.Attack, "westmarch"));
            var council = game.BeginTurn();
            var request = For(council, "valen");
            var plain = new CountryAgent(game.World, "valen", new ScriptedProvider());
            var headline = new CountryAgent(game.World, "valen", new ScriptedProvider(), ModelSeatOptions.Headline);
            Assert.That(plain.StimulusFor(request), Is.EqualTo("Your move."));
            Assert.That(plain.OptionsFor(request).Observations, Is.EqualTo(request.TurnFacts));
            Assert.That(headline.StimulusFor(request), Does.StartWith("Turn 2 of 30. We took westmarch from the neutrals"));
            Assert.That(headline.OptionsFor(request).Observations, Is.EqualTo(request.TurnFacts.Skip(1)), "the headline's fact is not repeated");
            Assert.That(plain.OptionsFor(request).ObservationPlacement, Is.EqualTo(ObservationPlacement.WithStimulus));
            Assert.That(new CountryAgent(game.World, "valen", new ScriptedProvider(), ModelSeatOptions.AfterSources)
                .OptionsFor(request).ObservationPlacement, Is.EqualTo(ObservationPlacement.AfterSources));
            Finish(game, council, null);
        }

        [Test]
        public void MovesLine_NamesEveryLegalTargetAndIsLeftOutWhenOff()
        {
            var game = NewGame();
            Give(game.World, "stonereach", "valen");
            var council = game.BeginTurn();
            Assert.That(Facts.MovesLine(game.World, "valen"),
                Is.EqualTo("Moves open to us: attack fenmark or westmarch; fortify ashby, brindle or stonereach; declare war on merrow or orsk."));
            var on = new CountryAgent(game.World, "valen", new ScriptedProvider());
            var off = new CountryAgent(game.World, "valen", new ScriptedProvider(), ModelSeatOptions.NoMovesLine);
            var request = For(council, "valen");
            Assert.That(on.Agent.Preview(on.StimulusFor(request), on.OptionsFor(request)).Request.Observations, Does.Contain("Moves open to us"));
            Assert.That(off.Agent.Preview(off.StimulusFor(request), off.OptionsFor(request)).Request.Observations, Does.Not.Contain("Moves open to us"));
            Finish(game, council, null);
        }

        [Test]
        public void PerTargetArm_TheFrameworkListsEachMovesLegalTargetsInPlaceOfTheMovesLine()
        {
            // The same state as the moves line's test: attack fenmark or westmarch; fortify ashby,
            // brindle or stonereach; declare war on merrow or orsk.
            var game = NewGame();
            Give(game.World, "stonereach", "valen");
            var council = game.BeginTurn();
            var request = For(council, "valen");
            var seat = new CountryAgent(game.World, "valen", new ScriptedProvider(), ModelSeatOptions.PerTarget);
            var options = seat.OptionsFor(request);
            Assert.That(options.LegalTargets, Is.EqualTo(LegalTargetListing.PerTarget));
            Assert.That(options.PairTargetsWithActions, Is.False);

            var sent = seat.Agent.Preview(seat.StimulusFor(request), options).Request;
            Assert.That(sent.Observations, Does.Not.Contain("Moves open to us"), "no game-side line");
            foreach (string pair in new[] { "fenmark (for attack)", "westmarch (for attack)", "ashby (for fortify)",
                         "brindle (for fortify)", "stonereach (for fortify)", "merrow (for declare_war)", "orsk (for declare_war)" })
                Assert.That(sent.SystemPrompt, Does.Contain(pair));
            // A province no move can take now is not listed as a target (the grounding line still
            // names every registered id), and a legal one only for its own moves.
            Assert.That(sent.SystemPrompt, Does.Not.Contain("- ilvarion"));
            Assert.That(sent.SystemPrompt, Does.Not.Contain("- highmoor"));
            Assert.That(sent.SystemPrompt, Does.Not.Contain("fenmark (for attack, fortify"));

            var control = new CountryAgent(game.World, "valen", new ScriptedProvider());
            Assert.That(control.Agent.Preview(control.StimulusFor(request), control.OptionsFor(request)).Request.SystemPrompt,
                Does.Not.Contain("(for attack)"), "the control arm leaves the framework's listing off");
            Finish(game, council, null);
        }

        [Test]
        public void Arms_ByName()
        {
            var paired = ModelSeatOptions.Named("per-target-paired");
            Assert.That((paired.MovesLine, paired.LegalTargets, paired.PairTargets, paired.CheckTarget),
                Is.EqualTo((false, LegalTargetListing.PerTarget, true, true)));
            var perTarget = ModelSeatOptions.Named("per-target");
            Assert.That((perTarget.MovesLine, perTarget.LegalTargets, perTarget.PairTargets),
                Is.EqualTo((false, LegalTargetListing.PerTarget, false)));
            var checksOnly = ModelSeatOptions.Named("no-moves-line");
            Assert.That((checksOnly.MovesLine, checksOnly.LegalTargets, checksOnly.PairTargets),
                Is.EqualTo((false, LegalTargetListing.Off, false)));
            var off = ModelSeatOptions.Named("per-target-paired+no-grounding");
            Assert.That((off.PairTargets, off.CheckTarget, off.Arm), Is.EqualTo((true, false, "per-target-paired+no-grounding")));
            Assert.That(ModelSeatOptions.Named("per-target").CheckTarget, Is.True, "a modifier changes only its own copy");
            Assert.That(ModelSeatOptions.Named("nonsense+no-grounding"), Is.Null);
            Assert.That(ModelSeatOptions.Named("control+nonsense"), Is.Null);
            var linePaired = ModelSeatOptions.Named("control+paired");
            Assert.That((linePaired.MovesLine, linePaired.LegalTargets, linePaired.PairTargets, linePaired.CheckTarget),
                Is.EqualTo((true, LegalTargetListing.Off, true, true)));
            var lineListed = ModelSeatOptions.Named("control+listed");
            Assert.That((lineListed.MovesLine, lineListed.LegalTargets, lineListed.PairTargets),
                Is.EqualTo((true, LegalTargetListing.PerTarget, false)));

            var byDefault = ModelSeatOptions.Named(null);
            Assert.That((byDefault.Arm, byDefault.MovesLine, byDefault.LegalTargets, byDefault.PairTargets, byDefault.CheckTarget),
                Is.EqualTo(("default", true, LegalTargetListing.Off, true, true)), "the moves line, paired");
            Assert.That(ModelSeatOptions.Named("control").PairTargets, Is.False, "stage 2's control stays as it was measured");

            var game = NewGame();
            var council = game.BeginTurn();
            var request = For(council, "valen");
            Assert.That(new CountryAgent(game.World, "valen", new ScriptedProvider(), paired).OptionsFor(request).PairTargetsWithActions, Is.True);
            var unset = new CountryAgent(game.World, "valen", new ScriptedProvider());
            Assert.That(unset.Options.Arm, Is.EqualTo("default"));
            Assert.That(unset.OptionsFor(request).PairTargetsWithActions, Is.True);
            Finish(game, council, null);
        }

        [Test]
        public async Task Report_CountsAttacksLandedAndLegalAnswers()
        {
            var game = await PlayWithFakeModel(2);
            var report = new ModelReport("fake", new[] { game });
            int doneAttacks = game.Decisions.Count(d => d.Move.Action == Actions.Attack && d.Execution == ExecutionStatus.Done);
            Assert.That(doneAttacks, Is.GreaterThan(0));
            Assert.That(report.AttacksLanded, Is.EqualTo(doneAttacks), "every attack Agent.Execute admitted reached a battle");
            Assert.That(report.AttacksWon, Is.InRange(1, report.AttacksLanded));
            Assert.That(report.IllegalAnswers, Is.Zero, "the fake model only makes offered moves");
            Assert.That(report.ContainedLegal, Is.Zero);
            Assert.That(report.ToMarkdown(), Does.Contain($"| Attacks landed / won | {report.AttacksLanded} / {report.AttacksWon} |"));
        }
    }
}

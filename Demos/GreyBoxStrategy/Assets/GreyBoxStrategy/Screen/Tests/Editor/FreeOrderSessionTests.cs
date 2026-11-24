using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Agenerela.Providers;
using GreyBoxStrategy.Realm;
using GreyBoxStrategy.Replay;
using Newtonsoft.Json;
using NUnit.Framework;
using UnityEngine;

namespace GreyBoxStrategy.Screen.Tests
{
    /// <summary>
    /// The turn rework, slice 1: rule mode with free orders. The model rulers decide from the moment the council opens,
    /// while you plan; End turn waits only for what is left; your court gives as many orders as gold allows; the
    /// recording replays as a free-orders game.
    /// </summary>
    public class FreeOrderSessionTests
    {
        private const string You = "kesh";
        private string folder;

        [SetUp] public void SetUp() => folder = Path.Combine(Application.temporaryCachePath, "free-order-tests-" + Guid.NewGuid().ToString("N"));
        [TearDown] public void TearDown() { try { Directory.Delete(folder, true); } catch (IOException) { } }

        private static ScriptedProvider Rulers(Func<World> world)
        {
            var rng = new System.Random(4);
            return new ScriptedProvider
            {
                Callback = r =>
                {
                    if (r.IsRefusalLine) return ScriptedReply.Statement("We will not.");
                    if (r.OfferedActions.Contains(Actions.AcceptPeace)) return ScriptedReply.Decide(Actions.RefusePeace, statement: "Never.");
                    var w = world();
                    bool marshal = r.AgentId.EndsWith(RecordedDecision.MarshalSuffix, StringComparison.Ordinal);
                    string country = marshal ? r.AgentId.Substring(0, r.AgentId.Length - RecordedDecision.MarshalSuffix.Length) : r.AgentId;
                    var army = Regex.Match(r.Stimulus ?? "", @"^Orders for (\w+)\.");
                    // A court picks from what the rules offer it now; half the time it answers none.
                    if (!marshal && rng.Next(2) == 0) return ScriptedReply.Decide("none", statement: "Enough for now.");
                    var moves = (marshal ? WritLegality.ArmyMoves(w, country, army.Groups[1].Value) : WritLegality.CourtMoves(w, country))
                        .Where(m => m.Action != Actions.None).ToList();
                    if (moves.Count == 0) return ScriptedReply.Decide("none", statement: "We wait.");
                    var pick = moves[rng.Next(moves.Count)];
                    return ScriptedReply.Decide(pick.Action, pick.Target ?? "no_target", "As we must.");
                },
            };
        }

        private static async Task Until(Func<bool> condition, string what)
        {
            for (int i = 0; i < 4000 && !condition(); i++) await Task.Delay(5);
            Assert.That(condition(), Is.True, "timed out waiting for " + what);
        }

        [Test]
        public async Task The_rulers_decide_while_you_plan_your_court_gives_several_orders_and_the_game_replays()
        {
            var setup = GameSetup.Writs(5);
            var map = setup.BuildMap();
            var rules = setup.BuildRules(map);
            rules.FreeOrders = true;
            World w = null;
            var provider = Rulers(() => w);
            var s = new RuleSession(You, "qwen3.5:4b", "default", 5, Path.Combine(folder, "decisions.jsonl"), (m, world) => { w = world; return provider; }, null, map, rules);
            Assert.That(rules.CourtOrdersOverride.ContainsKey(You), Is.False, "the rules given are not changed");
            Assert.That(s.Game.Rules.CourtOrdersFor(You), Is.EqualTo(RuleSession.YourCourtOrders), "your court follows gold alone");
            Assert.That(s.Game.Rules.CourtOrdersFor("valen"), Is.EqualTo(rules.MaxCourtOrders), "a model court keeps its pacing");

            var run = s.RunAsync();
            int severalCourts = 0;
            for (int t = 1; t <= 5; t++)
            {
                await Until(() => (s.WaitingFor == RuleSession.Waiting.Orders && s.Orders != null) || s.WaitingFor == RuleSession.Waiting.Envoy || s.Finished, $"turn {t}'s orders");
                if (s.WaitingFor == RuleSession.Waiting.Envoy) { s.Answer(false); t--; continue; }
                // While you plan, the rulers finish theirs.
                await Until(() => s.RulersDeciding == 0, $"turn {t}: the rulers to finish while you plan");
                Assert.That(s.WaitingFor, Is.EqualTo(RuleSession.Waiting.Orders), "still your council");

                var court = s.Orders;
                var plan = new RuleSession.WritsPlan();
                var recruit = court.Legal.FirstOrDefault(m => m.Action == Actions.Recruit || Actions.RecruitTypes.Contains(m.Action));
                if (recruit != null) { plan.Courts.Add(recruit); plan.Courts.Add(recruit); }
                var taxes = court.Legal.FirstOrDefault(m => m.Action == Actions.RaiseTaxes);
                if (taxes != null) plan.Courts.Add(taxes);
                foreach (var a in court.World.ArmiesOf(You).Where(a => !a.Marching))
                    if (WritLegality.ArmyMoves(court.World, You, a.Id).FirstOrDefault(m => m.Action == Actions.March) is Move march) plan.Armies[a.Id] = march;
                s.Plan(plan);
                int turn = t;
                bool card = false;
                while (true)
                {
                    await Until(() => s.Resolved == turn || s.Finished || s.WaitingFor == RuleSession.Waiting.Envoy, $"turn {turn} to resolve");
                    if (s.WaitingFor != RuleSession.Waiting.Envoy) break;
                    card = true;
                    s.Answer(false);
                }
                if (!card) Assert.That(s.EndTurnWaitSeconds, Is.LessThan(2), "the rulers had finished: End turn did not wait for them");
                var given = s.Game.Reports.Last().Outcomes.Count(o => o.Request != null && o.Request.CountryId == You && o.Request.Moment == Moment.Council && o.Choice.Action != Actions.None);
                if (given > 1) severalCourts++;
            }
            Assert.That(severalCourts, Is.GreaterThan(0), "your court gave more than one order in a turn");

            var g1 = s.BuildReplay();
            Assert.That(g1.Turns.Count, Is.EqualTo(s.Resolved));
            Assert.That(g1.Turns.Sum(x => x.ArmyDrift), Is.EqualTo(0));
            var rec = Recording.Parse(s.BuildRecording().ToJson());
            Assert.That(rec.FreeOrders, Is.True, "the recording says the game had free orders");
            var g2 = ReplayBuilder.Build(rec, map, setup.BuildRules(map), partial: true);
            Assert.That(g2.StoppedBecause, Is.Null.Or.Contain("has no"), "it replays as free orders from rules that do not say so");
            Assert.That(g2.Turns.Count, Is.EqualTo(g1.Turns.Count));

            s.Stop();
            await run;
            Assert.That(s.Error, Is.Null);
        }

        // One played game of three turns: your plan given the moment the council opens (the rulers held back until it
        // is in), or once every ruler has finished. Returns the rulers' decisions and End turn's waits.
        private async Task<(string decisions, double[] waits, int[] decidingAtPlan)> PlayThree(bool pressAtOnce)
        {
            var setup = GameSetup.Writs(5);
            var map = setup.BuildMap();
            var rules = setup.BuildRules(map);
            rules.FreeOrders = true;
            World w = null;
            var provider = Rulers(() => w);
            var s = new RuleSession(You, "qwen3.5:4b", "default", 5, Path.Combine(folder, (pressAtOnce ? "now" : "late") + ".jsonl"), (m, world) => { w = world; return provider; }, null, map, rules);
            var waits = new double[3];
            var deciding = new int[3];
            // Pressed at once: every ruler's call waits until your orders are in.
            provider.Hold = pressAtOnce;
            var run = s.RunAsync();
            for (int t = 1; t <= 3; t++)
            {
                await Until(() => s.WaitingFor == RuleSession.Waiting.Orders && s.Orders != null, $"turn {t}'s orders");
                if (!pressAtOnce) await Until(() => s.RulersDeciding == 0, "the rulers to finish while you plan");
                deciding[t - 1] = s.RulersDeciding;
                var plan = new RuleSession.WritsPlan();
                var recruit = s.Orders.Legal.FirstOrDefault(m => m.Action == Actions.Recruit || Actions.RecruitTypes.Contains(m.Action));
                if (recruit != null) plan.Courts.Add(recruit);
                s.Plan(plan);
                // Your orders are in: the held calls go on, one by one, while End turn waits for them.
                for (int i = 0; i < 8000 && s.Resolved < t; i++)
                {
                    if (s.WaitingFor == RuleSession.Waiting.Models) provider.Release();
                    if (s.WaitingFor == RuleSession.Waiting.Envoy) s.Answer(false);
                    await Task.Delay(5);
                }
                Assert.That(s.Resolved, Is.EqualTo(t), $"turn {t} resolved");
                waits[t - 1] = s.EndTurnWaitSeconds;
            }
            var theirs = s.BuildRecording().Decisions.Where(d => d.Turn <= 3 && d.AgentId != null && !d.AgentId.StartsWith(You, StringComparison.Ordinal))
                .Select(d => $"{d.Turn} {d.AgentId} {d.Moment} {d.ArmyId} {d.Action} {d.Target}");
            s.Stop();
            await run;
            Assert.That(s.Error, Is.Null);
            return (string.Join(" | ", theirs), waits, deciding);
        }

        [Test]
        public async Task End_turn_at_once_waits_for_every_ruler_late_for_none_and_the_rulers_decide_the_same()
        {
            var now = await PlayThree(pressAtOnce: true);
            var late = await PlayThree(pressAtOnce: false);
            TestContext.Progress.WriteLine($"End turn waits, pressed at once: {string.Join(", ", now.waits.Select(x => x.ToString("0.000")))} s; late: {string.Join(", ", late.waits.Select(x => x.ToString("0.000")))} s");
            Assert.That(now.decidingAtPlan, Is.All.EqualTo(4), "pressed at once, all four rulers are still deciding");
            Assert.That(late.decidingAtPlan, Is.All.EqualTo(0), "pressed late, none is");
            Assert.That(late.waits, Is.All.LessThan(0.5), "pressed late, End turn waits for no ruler");
            Assert.That(now.decisions, Is.Not.Empty);
            Assert.That(now.decisions, Is.EqualTo(late.decisions), "the rulers' orders are the same whenever you press End turn");
        }

        // Turn 1 of one game. Begun early (a generated world's session starts behind the loading card): every ruler has
        // finished its turn-1 orders before the screen is built and your council read. Otherwise the screen is built as
        // the session starts, and the rulers' calls are held until your plan is in, so each ruler's later requests are
        // asked after your orders landed. Returns what the screen opened on, your council, and every ruler's input.
        private async Task<(string opening, string council, string rulersSaw, string rulersDid)> PlayFirstTurn(bool begunEarly)
        {
            var setup = GameSetup.Writs(5);
            var map = setup.BuildMap();
            var rules = setup.BuildRules(map);
            rules.FreeOrders = true;
            World w = null;
            var provider = Rulers(() => w);
            var scripted = provider.Callback;
            var saw = new System.Collections.Generic.List<string>();
            int? firstTurn = null;
            provider.Callback = r =>
            {
                // Turn 1's calls only: turn 2's begin the moment turn 1 resolves.
                firstTurn = firstTurn ?? w?.Turn;
                if (r.AgentId != null && !r.IsRefusalLine && w != null && w.Turn == firstTurn)
                    saw.Add($"{r.AgentId} | {r.Request.Observations} | {r.Request.Stimulus} | {string.Join(",", r.Request.History)} | {string.Join(",", r.OfferedActions)} | {string.Join(",", r.OfferedTargets)}");
                return scripted(r);
            };
            var s = new RuleSession(You, "qwen3.5:4b", "default", 5, Path.Combine(folder, (begunEarly ? "early" : "now") + ".jsonl"), (m, world) => { w = world; return provider; }, null, map, rules);
            provider.Hold = !begunEarly;
            var run = s.RunAsync();
            if (begunEarly) await Until(() => s.RulersDeciding == 0 && s.WaitingFor == RuleSession.Waiting.Orders && s.Orders != null, "the rulers to finish turn 1 behind the loading card");
            // The screen is built on the game as the director builds it, and your council drawn from the request.
            var g = s.BuildReplay();
            string opening = $"{g.Turns.Count} turns | " + JsonConvert.SerializeObject(g.Initial);
            await Until(() => s.WaitingFor == RuleSession.Waiting.Orders && s.Orders != null, "your turn-1 council");
            var court = s.Orders;
            string council = $"{court.Turn} | {string.Join(",", court.Legal)} | " + JsonConvert.SerializeObject(ReplayBuilder.StateOf(court.World, g.ProvinceIds, g.Initial));
            if (!begunEarly) Assert.That(s.RulersDeciding, Is.EqualTo(4), "held: no ruler has finished when your council opens");

            var plan = new RuleSession.WritsPlan();
            var recruit = court.Legal.FirstOrDefault(m => m.Action == Actions.Recruit || Actions.RecruitTypes.Contains(m.Action));
            if (recruit != null) plan.Courts.Add(recruit);
            var taxes = court.Legal.FirstOrDefault(m => m.Action == Actions.RaiseTaxes);
            if (taxes != null) plan.Courts.Add(taxes);
            // An order that holds gold back at once (Country.Reserved), the one change your court makes before resolution.
            var paid = court.Legal.FirstOrDefault(m => m.Action != Actions.None && !plan.Courts.Contains(m) && WritLegality.CourtOrderCost(court.World, You, m) > 0);
            if (paid != null) plan.Courts.Insert(0, paid);
            foreach (var a in court.World.ArmiesOf(You).Where(a => !a.Marching).OrderBy(a => a.Id))
                if (WritLegality.ArmyMoves(court.World, You, a.Id).FirstOrDefault(m => m.Action == Actions.March) is Move march) plan.Armies[a.Id] = march;
            Assert.That(paid, Is.Not.Null, "your plan holds gold back, which the rulers could see if your orders reached them");
            Assert.That(plan.Armies.Count, Is.GreaterThan(0), "and marches");
            s.Plan(plan);
            for (int i = 0; i < 8000 && s.Resolved < 1 && s.Error == null; i++)
            {
                if (s.WaitingFor == RuleSession.Waiting.Models) provider.Release();
                if (s.WaitingFor == RuleSession.Waiting.Envoy) s.Answer(false);
                await Task.Delay(5);
            }
            Assert.That(s.Resolved, Is.EqualTo(1), "turn 1 resolved");
            var did = s.BuildRecording().Decisions.Where(d => d.Turn == 1 && d.AgentId != null && !d.AgentId.StartsWith(You, StringComparison.Ordinal))
                .Select(d => $"{d.AgentId} {d.Moment} {d.ArmyId} {d.Action} {d.Target}");
            s.Stop();
            await run;
            Assert.That(s.Error, Is.Null);
            return (opening, council, string.Join("\n", saw), string.Join(" | ", did));
        }

        [Test]
        public async Task Rulers_begun_behind_the_loading_card_decide_on_the_same_snapshot_and_show_you_nothing_early()
        {
            var now = await PlayFirstTurn(begunEarly: false);
            var early = await PlayFirstTurn(begunEarly: true);
            Assert.That(now.rulersSaw, Is.Not.Empty);
            Assert.That(early.rulersSaw, Is.EqualTo(now.rulersSaw), "every ruler's turn-1 input is the same: your orders never reach it, early start or not");
            Assert.That(early.rulersDid, Is.EqualTo(now.rulersDid), "so the rulers' turn-1 orders are the same");
            Assert.That(early.opening, Is.EqualTo(now.opening), "the screen opens on the same game, with none of the rulers' turn-1 orders in it");
            Assert.That(early.council, Is.EqualTo(now.council), "your council offers the same moves on the same map");
        }

        [Test]
        public void A_writs_recording_saves_without_the_free_orders_field()
        {
            var rec = new Recording { Seed = 3, Title = "old", Source = "rule" };
            Assert.That(rec.ToJson(), Does.Not.Contain("freeOrders"));
            Assert.That(Recording.Parse(rec.ToJson()).FreeOrders, Is.False);
        }
    }
}

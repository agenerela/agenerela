using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Agenerela.Providers;
using GreyBoxStrategy.Realm;
using GreyBoxStrategy.Replay;
using NUnit.Framework;
using UnityEngine;

namespace GreyBoxStrategy.Screen.Tests
{
    /// <summary>
    /// The advisor in rule mode (the lead's answer 29), with a scripted model: its advice maps only to moves the
    /// rules offer, taking it gives exactly that plan, and off means no call is made.
    /// </summary>
    public class AdvisorTests
    {
        private const string You = "kesh";
        private string folder;

        [SetUp] public void SetUp() => folder = Path.Combine(Application.temporaryCachePath, "advisor-tests-" + Guid.NewGuid().ToString("N"));
        [TearDown] public void TearDown() { try { Directory.Delete(folder, true); } catch (IOException) { } }

        private static bool IsAdvisor(string agentId) => agentId.StartsWith(You + Advisor.AgentSuffix, StringComparison.Ordinal);

        // The rulers pick at random among their moves. The advisor, every other call, names a move the rules never
        // offer (a war on itself, a march to the far side of the map); otherwise the first move it is offered.
        private static ScriptedProvider Models(Func<World> world, List<(string agent, string action, string target)> advisorAnswers)
        {
            var rng = new System.Random(4);
            int courtCalls = 0, marshalCalls = 0;
            return new ScriptedProvider
            {
                Callback = r =>
                {
                    if (r.IsRefusalLine) return ScriptedReply.Statement("We will not.");
                    if (r.OfferedActions.Contains(Actions.AcceptPeace)) return ScriptedReply.Decide(Actions.RefusePeace, statement: "Never.");
                    var w = world();
                    bool marshal = r.AgentId.EndsWith(RecordedDecision.MarshalSuffix, StringComparison.Ordinal);
                    var army = Regex.Match(r.Stimulus ?? "", @"^Orders for (\w+)\.");
                    if (IsAdvisor(r.AgentId))
                    {
                        var legal = marshal ? WritLegality.ArmyMoves(w, You, army.Groups[1].Value) : WritLegality.CourtMoves(w, You);
                        legal = legal.Where(m => m.Action != Actions.None).ToList();
                        int k = marshal ? marshalCalls++ : courtCalls++;
                        // The court is wrong on its second, fourth... call, the marshal on its first, third...
                        (string a, string t) said = k % 2 == (marshal ? 0 : 1)
                            ? (marshal ? (Actions.March, w.ProvinceList.Last().Id) : (Actions.DeclareWar, You))
                            : legal.Count > 0 ? (legal[0].Action, legal[0].Target ?? "no_target") : ("none", "no_target");
                        lock (advisorAnswers) advisorAnswers.Add((r.AgentId, said.a, said.t));
                        return ScriptedReply.Decide(said.a, said.t, "Strike while their armies are away, my lord.");
                    }
                    string country = marshal ? r.AgentId.Substring(0, r.AgentId.Length - RecordedDecision.MarshalSuffix.Length) : r.AgentId;
                    var moves = (marshal ? WritLegality.ArmyMoves(w, country, army.Groups[1].Value) : WritLegality.CourtMoves(w, country)).Where(m => m.Action != Actions.None).ToList();
                    if (moves.Count == 0) return ScriptedReply.Decide("none", statement: "We wait.");
                    var pick = moves[rng.Next(moves.Count)];
                    return ScriptedReply.Decide(pick.Action, pick.Target ?? "no_target", "As we must.");
                },
            };
        }

        private RuleSession Session(bool advisorOn, List<(string, string, string)> answers, out ScriptedProvider provider)
        {
            var setup = GameSetup.Writs(5);
            var map = setup.BuildMap();
            World w = null;
            var p = provider = Models(() => w, answers);
            return new RuleSession(You, "qwen3.5:4b", "default", 5, Path.Combine(folder, "decisions.jsonl"), (m, world) => { w = world; return p; }, null, map, setup.BuildRules(map))
            {
                AdvisorOn = advisorOn,
            };
        }

        private static async Task Until(Func<bool> condition, string what)
        {
            for (int i = 0; i < 4000 && !condition(); i++) await Task.Delay(5);
            Assert.That(condition(), Is.True, "timed out waiting for " + what);
        }

        private static async Task<CountryRequest> NextOrders(RuleSession s, int turn)
        {
            while (true)
            {
                await Until(() => (s.WaitingFor == RuleSession.Waiting.Orders && s.Orders != null) || s.WaitingFor == RuleSession.Waiting.Envoy || s.Finished, $"turn {turn}'s orders");
                if (s.WaitingFor == RuleSession.Waiting.Envoy) { s.Answer(false); continue; }
                return s.Orders;
            }
        }

        [Test]
        public async Task The_advice_maps_only_to_moves_the_rules_offer()
        {
            var answers = new List<(string, string, string)>();
            var s = Session(true, answers, out _);
            var run = s.RunAsync();
            int illegalAsked = 0, legalAsked = 0, spoke = 0;
            for (int t = 1; t <= 4; t++)
            {
                var court = await NextOrders(s, t);
                await Until(() => s.Advice != null || s.AdvisorError != null, $"turn {t}'s advice");
                Assert.That(s.AdvisorError, Is.Null);
                var advice = s.Advice;
                Assert.That(advice.Turn, Is.EqualTo(court.Turn));
                Assert.That(advice.Items[0].Request.Moment, Is.EqualTo(Moment.Council), "the court first");
                // Checked against the rules themselves, not only the requests the advisor built.
                var world = court.World;
                Assert.That(advice.Court.Action == Actions.None || WritLegality.CourtMoves(world, You).Contains(advice.Court), Is.True, $"turn {t}: court advice {advice.Court} is a court move");
                foreach (var kv in advice.Armies)
                    Assert.That(kv.Value.Action == Actions.None || WritLegality.ArmyMoves(world, You, kv.Key).Contains(kv.Value), Is.True, $"turn {t}: {kv.Key}'s advice {kv.Value} is one of its moves");
                foreach (var item in advice.Items)
                {
                    var r = item.Request;
                    Assert.That(item.Move.Action == Actions.None || r.Legal.Contains(item.Move), Is.True, $"turn {t}: {item.Move} is offered on {r.Moment} {r.ArmyId}");
                    if (item.Record?.ModelAction != null && item.Record.ModelAction != Actions.None
                        && r.Legal.Any(m => m.Action == item.Record.ModelAction && (m.Target ?? "no_target") == (item.Record.ModelTarget ?? "no_target")))
                    {
                        // A legal answer is the advice, exactly: the move the model named.
                        Assert.That(item.Move.Action, Is.EqualTo(item.Record.ModelAction), $"turn {t}: the advice is the model's own legal answer");
                        Assert.That(item.Move.Target ?? "no_target", Is.EqualTo(item.Record.ModelTarget ?? "no_target"));
                        legalAsked++;
                    }
                }
                if (advice.Items.Any(i => !string.IsNullOrWhiteSpace(i.Record?.Statement)))
                {
                    Assert.That(advice.Counsel, Is.EqualTo("Strike while their armies are away, my lord."), "its spoken line is the counsel");
                    spoke++;
                }
                // The armies a writ reaches, and no more.
                int slots = Math.Max(0, court.Writs - 1);
                Assert.That(advice.Armies.Keys, Is.EquivalentTo(WritLegality.Awaiting(court.World, You).Take(slots).Select(a => a.Id)));
                s.Plan(new RuleSession.WritsPlan());
                int turn = t;
                await Until(() => s.Resolved == turn || s.Finished, $"turn {turn} to resolve");
            }
            illegalAsked = answers.Count(a => a.Item2 == Actions.DeclareWar && a.Item3 == You || a.Item2 == Actions.March);
            Assert.That(illegalAsked, Is.GreaterThan(0), "the advisor did name moves the rules do not offer");
            Assert.That(legalAsked, Is.GreaterThan(0));
            Assert.That(spoke, Is.GreaterThan(0), "the advisor spoke on some turn");
            s.Stop();
        }

        [Test]
        public async Task Taking_the_advice_gives_exactly_that_plan_and_only_then_is_it_yours()
        {
            var answers = new List<(string, string, string)>();
            var s = Session(true, answers, out _);
            var run = s.RunAsync();
            var taken = new List<(int turn, Advice advice)>();
            for (int t = 1; t <= 4; t++)
            {
                await NextOrders(s, t);
                await Until(() => s.Advice != null || s.AdvisorError != null, $"turn {t}'s advice");
                var advice = s.Advice;
                var plan = RuleSession.WritsPlan.From(advice);
                Assert.That(plan.Court, Is.EqualTo(advice.Court));
                Assert.That(plan.Armies, Is.EquivalentTo(advice.Armies));
                // Taken on odd turns, ignored on even ones.
                if (t % 2 == 1) { s.Plan(plan); taken.Add((t, advice)); }
                else s.Plan(new RuleSession.WritsPlan());
                int turn = t;
                await Until(() => s.Resolved == turn || s.Finished, $"turn {turn} to resolve");
            }
            var rec = s.BuildRecording();
            var yours = rec.Decisions.Where(d => d.ModelName == RuleSession.YourModel).ToList();
            foreach (var (turn, advice) in taken)
            {
                var court = yours.First(d => d.Turn == turn && d.ArmyId == null);
                Assert.That(court.Action, Is.EqualTo(advice.Court.Action), $"turn {turn}: your court gave the advised order");
                Assert.That(court.Target ?? RecordedDecision.NoTarget, Is.EqualTo(advice.Court.Target ?? RecordedDecision.NoTarget));
                foreach (var kv in advice.Armies)
                {
                    var army = yours.First(d => d.Turn == turn && d.ArmyId == kv.Key);
                    Assert.That(army.Action, Is.EqualTo(kv.Value.Action), $"turn {turn}: {kv.Key} as advised");
                    Assert.That(army.Target ?? RecordedDecision.NoTarget, Is.EqualTo(kv.Value.Target ?? RecordedDecision.NoTarget));
                }
            }
            foreach (var d in yours.Where(d => d.Turn % 2 == 0 && d.ArmyId == null))
                Assert.That(d.Action, Is.EqualTo(Actions.None), $"turn {d.Turn}: advice ignored, your court held");
            Assert.That(rec.Decisions.Any(d => d.AgentId != null && IsAdvisor(d.AgentId)), Is.False, "the advisor's decisions are never recorded as moves");
            Assert.That(s.AdviceDecision(taken[0].advice.Items[0]).ModelName, Does.EndWith("advice"), "the inspector marks it as advice");
            s.Stop();
        }

        [Test]
        public async Task With_the_advisor_off_no_call_is_made()
        {
            var answers = new List<(string, string, string)>();
            var s = Session(false, answers, out var provider);
            var run = s.RunAsync();
            for (int t = 1; t <= 3; t++)
            {
                await NextOrders(s, t);
                await Task.Delay(50);
                Assert.That(s.Advice, Is.Null);
                Assert.That(s.AdvisorThinking, Is.False);
                if (t == 3)
                {
                    Assert.That(s.AdvisorCalls, Is.EqualTo(0));
                    Assert.That(answers, Is.Empty, "no request reached the model from the advisor");
                    // Turned on in game: the next turn asks it.
                    s.AdvisorOn = true;
                }
                s.Plan(new RuleSession.WritsPlan());
                int turn = t;
                await Until(() => s.Resolved == turn || s.Finished, $"turn {turn} to resolve");
            }
            Assert.That(provider.RequestCount, Is.GreaterThan(0), "the rulers were asked");
            await NextOrders(s, 4);
            await Until(() => s.Advice != null || s.AdvisorError != null, "turn 4's advice");
            Assert.That(s.AdvisorCalls, Is.GreaterThan(0));
            s.Stop();
        }
    }
}

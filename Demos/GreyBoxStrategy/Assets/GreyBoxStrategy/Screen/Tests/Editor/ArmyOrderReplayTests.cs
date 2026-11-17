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
    /// Merging and splitting armies (#20) as the human player gives them on the screen: through <see cref="RuleSession"/>,
    /// recorded, and given again by the replay, so the replay shows what the game did. The rules' own tests are
    /// <c>ArmyOrderTests</c>.
    /// </summary>
    public class ArmyOrderReplayTests
    {
        private const string You = "kesh";
        private string folder;

        [SetUp] public void SetUp() => folder = Path.Combine(Application.temporaryCachePath, "army-order-tests-" + Guid.NewGuid().ToString("N"));
        [TearDown] public void TearDown() { try { Directory.Delete(folder, true); } catch (IOException) { } }

        // The model rulers through the real WritRuler, each picking at random from what it is offered.
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
        public async Task Your_merges_and_splits_land_at_once_are_recorded_and_the_replay_gives_them_again()
        {
            var setup = GameSetup.Writs(5);
            var map = setup.BuildMap();
            var rules = setup.BuildRules(map);
            World w = null;
            var provider = Rulers(() => w);
            var s = new RuleSession(You, "qwen3.5:4b", "default", 5, Path.Combine(folder, "decisions.jsonl"), (m, world) => { w = world; return provider; }, null, map, rules);
            Assert.That(s.CanMerge(new[] { "a_host", "b_host" }), Is.EqualTo("the game is not waiting for your orders"), "not before the game asks");

            var run = s.RunAsync();
            int splits = 0, merges = 0;
            for (int t = 1; t <= 6; t++)
            {
                await Until(() => (s.WaitingFor == RuleSession.Waiting.Orders && s.Orders != null) || s.WaitingFor == RuleSession.Waiting.Envoy || s.Finished, $"turn {t}'s orders");
                if (s.WaitingFor == RuleSession.Waiting.Envoy) { s.Answer(false); t--; continue; }
                var court = s.Orders;
                var world = court.World;
                int writs = s.Game.ArmyWrits(You);

                // Split your largest army in two, then merge any two of yours standing together.
                var largest = world.ArmiesOf(You).Where(a => !a.Marching && a.Soldiers >= 2).OrderByDescending(a => a.Soldiers).ThenBy(a => a.Id).FirstOrDefault();
                if (largest != null)
                {
                    Assert.That(s.CanSplit(largest.Id, largest.Soldiers), Does.Contain("1 to"), "never the whole army");
                    int before = world.ArmiesOf(You).Count();
                    Assert.That(s.Split(largest.Id, largest.Soldiers / 2), Is.Null);
                    Assert.That(world.ArmiesOf(You).Count(), Is.EqualTo(before + 1), "the new army stands at once");
                    splits++;
                }
                var pair = world.ArmiesOf(You).Where(a => !a.Marching).GroupBy(a => a.Location)
                    .Select(g => g.OrderBy(a => a.Id).Take(2).ToList()).FirstOrDefault(g => g.Count == 2 && g.Sum(a => a.Soldiers) <= world.Rules.ArmyMax);
                if (pair != null)
                {
                    Assert.That(s.Merge(pair.Select(a => a.Id).ToList()), Is.Null);
                    Assert.That(world.ArmiesOf(You).Count(a => pair.Any(p => p.Id == a.Id)), Is.EqualTo(1), "one army stands where two did");
                    merges++;
                }
                Assert.That(s.Game.ArmyWrits(You), Is.EqualTo(writs), "neither costs a writ");

                var plan = new RuleSession.WritsPlan { Court = court.Legal.FirstOrDefault(m => m.Action == Actions.Recruit || Actions.RecruitTypes.Contains(m.Action)) ?? court.Legal.First() };
                // The armies as they stand now are the ones your plan orders.
                foreach (var a in world.ArmiesOf(You).Where(a => !a.Marching).OrderBy(a => a.Id))
                {
                    if (plan.Armies.Count >= writs) break;
                    var march = WritLegality.ArmyMoves(world, You, a.Id).FirstOrDefault(m => m.Action == Actions.March);
                    if (march != null) plan.Armies[a.Id] = march;
                }
                s.Plan(plan);
                int turn = t;
                await Until(() => s.Resolved == turn || s.Finished, $"turn {turn} to resolve");
            }
            Assert.That(splits, Is.GreaterThan(0), "you split an army");
            Assert.That(merges, Is.GreaterThan(0), "you merged armies");

            var g1 = s.BuildReplay();
            Assert.That(g1.Turns.Count, Is.EqualTo(s.Resolved));
            Assert.That(g1.Turns.Sum(x => x.ArmyDrift), Is.EqualTo(0), "the replay's armies stand where the game's do");
            var orders = g1.AllEvents.Where(e => e.Kind == ReplayEventKind.ArmiesMerged || e.Kind == ReplayEventKind.ArmySplit).ToList();
            Assert.That(orders.Count(e => e.Kind == ReplayEventKind.ArmySplit), Is.EqualTo(splits));
            Assert.That(orders.Count(e => e.Kind == ReplayEventKind.ArmiesMerged), Is.EqualTo(merges));
            Assert.That(orders.All(e => e.Country == You), Is.True, "yours alone");
            var lines = orders.Select(e => Chronicle.Line(e, id => char.ToUpperInvariant(id[0]) + id.Substring(1))).ToList();
            Assert.That(lines.All(l => l != null && l.Text.Length > 0), Is.True, "the chronicle tells each");
            Assert.That(lines.Any(l => l.Text.Contains(" merges ")), Is.True);
            Assert.That(lines.Any(l => l.Text.Contains(" splits ") && l.Text.Contains(", led by ")), Is.True, "the new army's general is named");
            foreach (var e in orders)
            {
                var after = e.After.Armies;
                if (e.Kind == ReplayEventKind.ArmiesMerged) Assert.That(e.ArmyOrder.Others.Any(id => after.Any(a => a.Id == id)), Is.False, $"turn {e.Turn}: the armies merged away are gone from the map");
                Assert.That(e.ArmyOrder.After.All(x => after.Any(a => a.Id == x.Id && a.Soldiers == x.Soldiers)), Is.True, $"turn {e.Turn}: the armies stand as the order left them");
            }

            // Saved and loaded, the recording replays the same.
            var rec = s.BuildRecording();
            Assert.That(rec.ArmyOrders.Count, Is.EqualTo(splits + merges));
            var back = Recording.Parse(rec.ToJson());
            Assert.That(back.ArmyOrders.Select(o => o.ToString()), Is.EqualTo(rec.ArmyOrders.Select(o => o.ToString())));
            var g2 = ReplayBuilder.Build(back, map, rules, partial: true);
            Assert.That(g2.Turns.Count, Is.EqualTo(g1.Turns.Count));
            Assert.That(g2.AllEvents.Select(e => e.Kind + " " + e.Text), Is.EqualTo(g1.AllEvents.Select(e => e.Kind + " " + e.Text)), "the same events, in the same order");

            // Without its army orders the recording is not the game's: the replay says so rather than drift.
            back.ArmyOrders.Clear();
            var g3 = ReplayBuilder.Build(back, map, rules, partial: true);
            Assert.That(g3.StoppedBecause, Is.Not.Null, "a replay that skips them cannot rebuild the game");

            s.Stop();
            await run;
            Assert.That(s.Error, Is.Null);
        }

        [Test]
        public void A_recording_without_army_orders_saves_as_before_and_loads_with_none()
        {
            var rec = new Recording { Seed = 3, Title = "old", Source = "rule" };
            string json = rec.ToJson();
            Assert.That(json, Does.Not.Contain("armyOrders"), "a game without them writes nothing new");
            Assert.That(Recording.Parse(json).ArmyOrders, Is.Empty);
        }
    }
}

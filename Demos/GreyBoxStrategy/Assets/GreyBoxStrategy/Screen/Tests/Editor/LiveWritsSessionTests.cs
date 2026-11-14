using System;
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
    /// A live writs game (rules v2, R1b, on the screen) with the model replaced by a ScriptedProvider that
    /// plays like a ruler: the court picks a court order, the marshal a march for the army it is asked
    /// about. The game must replay exactly from what the session recorded, and every decision must open
    /// in the inspector with its prompt, a marshal's with its army.
    /// </summary>
    public class LiveWritsSessionTests
    {
        private string folder;

        [SetUp] public void SetUp() => folder = Path.Combine(Application.temporaryCachePath, "live-writs-tests-" + Guid.NewGuid().ToString("N"));
        [TearDown] public void TearDown() { try { Directory.Delete(folder, true); } catch (IOException) { } }

        private static ScriptedProvider Rulers(Func<World> world)
        {
            var rng = new System.Random(5);
            var p = new ScriptedProvider
            {
                Callback = r =>
                {
                    if (r.IsRefusalLine) return ScriptedReply.Statement("We will not.");
                    if (r.OfferedActions.Contains(Actions.AcceptPeace)) return ScriptedReply.Decide(Actions.AcceptPeace, statement: "So be it.");
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
            return p;
        }

        [Test]
        public async Task A_live_writs_game_replays_from_its_record_and_every_order_opens_with_its_prompt()
        {
            var setup = GameSetup.Writs(9);
            var map = setup.BuildMap();
            var rules = setup.BuildRules(map);
            World w = null;
            var provider = Rulers(() => w);
            var s = new LiveWritsSession("qwen3.5:4b", 9, map, rules, 8, Path.Combine(folder, "decisions.jsonl"), (m, world) => { w = world; return provider; });
            await s.RunAsync();
            Assert.That(s.Error, Is.Null);
            Assert.That(s.Stopped, Is.True, "stopped at the turn limit, before a model saw turn 9");

            var g = ReplayBuilder.Build(s.Recording(), map, rules, partial: true);
            // A game in progress replays to the first turn not yet decided: here turn 9, which no model saw.
            Assert.That(g.StoppedBecause, Does.Contain("turn 9"), "the replay rebuilds every turn the live game played, and stops where it stopped");
            Assert.That(g.Turns.Count, Is.EqualTo(8));
            Assert.That(g.Turns.Sum(t => t.ArmyDrift), Is.EqualTo(0));
            Assert.That(g.Writs, Is.True);

            var orders = g.AllEvents.Where(e => e.Kind == ReplayEventKind.ArmyOrder).ToList();
            Assert.That(orders.Count, Is.GreaterThan(5), "the marshals ordered armies");
            foreach (var e in orders)
            {
                Assert.That(e.Decision.ArmyId, Is.EqualTo(e.Army), "the army in the decision is the army ordered");
                Assert.That(e.Decision.AgentId, Is.EqualTo(e.Country + "_marshal"));
                Assert.That(e.Decision.Observations, Is.Not.Null.And.Not.Empty, $"turn {e.Turn}: {e.Army}'s order keeps its prompt from the log");
            }
            var card = DecisionCard.From(orders[0]);
            Assert.That(card.Moment, Does.Contain("marshal").And.Contain(Chronicle.Host(orders[0].Army)));
            Assert.That(card.Country, Is.EqualTo(orders[0].Country), "the inspector names the country, not its marshal agent");

            var court = g.AllEvents.Where(e => e.Decision != null && e.Kind != ReplayEventKind.ArmyOrder && e.Decision.ArmyId == null).ToList();
            Assert.That(court.Count, Is.GreaterThan(5));
            Assert.That(court.All(e => !string.IsNullOrEmpty(e.Decision.Observations)), Is.True, "every court decision keeps its prompt");
            Assert.That(s.Players.Values.All(p => p.Persona != null), Is.True, "each ruler has its persona");
        }

        [Test]
        public void The_writs_session_plays_the_writs_turn_only()
        {
            var map = MapDefinition.Standard();
            Assert.Throws<ArgumentException>(() => new LiveWritsSession("m", 1, map, GameSetup.Classic(1).BuildRules(map), 0, null, (m, w) => null));
        }
    }
}

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
    /// Rule mode on the writs turn without a screen: you rule Kesh with a plan a turn (a court order, an order for
    /// each army with a writ, a second court order), and a ScriptedProvider plays the four model rulers through
    /// the real WritRuler, a court and a marshal each.
    /// </summary>
    public class WritsRuleTests
    {
        private const string You = "kesh";
        private string folder;

        [SetUp] public void SetUp() => folder = Path.Combine(Application.temporaryCachePath, "writs-rule-tests-" + Guid.NewGuid().ToString("N"));
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
                    var moves = (marshal ? WritLegality.ArmyMoves(w, country, army.Groups[1].Value) : WritLegality.CourtMoves(w, country))
                        .Where(m => m.Action != Actions.None).ToList();
                    if (moves.Count == 0) return ScriptedReply.Decide("none", statement: "We wait.");
                    var pick = moves[rng.Next(moves.Count)];
                    return ScriptedReply.Decide(pick.Action, pick.Target ?? "no_target", "As we must.");
                },
            };
        }

        private RuleSession Session()
        {
            var setup = GameSetup.Writs(5);
            var map = setup.BuildMap();
            World w = null;
            var provider = Rulers(() => w);
            return new RuleSession(You, "qwen3.5:4b", "default", 5, Path.Combine(folder, "decisions.jsonl"), (m, world) => { w = world; return provider; }, null, map, setup.BuildRules(map));
        }

        private static async Task Until(Func<bool> condition, string what)
        {
            for (int i = 0; i < 4000 && !condition(); i++) await Task.Delay(5);
            Assert.That(condition(), Is.True, "timed out waiting for " + what);
        }

        [Test]
        public async Task Your_plan_lands_as_the_rules_ask_it_and_the_replay_rebuilds_your_turns()
        {
            var s = Session();
            Assert.That(s.Writs, Is.True);
            var run = s.RunAsync();
            var ui = new RuleSession.WritsPlan();
            int marches = 0;
            for (int t = 1; t <= 6; t++)
            {
                await Until(() => (s.WaitingFor == RuleSession.Waiting.Orders && s.Orders != null) || s.WaitingFor == RuleSession.Waiting.Envoy || s.Finished, $"turn {t}'s orders");
                if (s.WaitingFor == RuleSession.Waiting.Envoy) { s.Answer(false); t--; continue; }
                var court = s.Orders;
                var w = court.World;
                Assert.That(court.Writs, Is.EqualTo(w.Writs(You)), "your writs are the rules' count, a setting, not a constant");
                Assert.That(court.Legal, Is.EqualTo(WritLegality.CourtMoves(w, You)), "your court is offered exactly what a model's court is offered");

                var plan = new RuleSession.WritsPlan { Court = court.Legal.FirstOrDefault(m => m.Action == Actions.Recruit) ?? court.Legal.First() };
                foreach (var a in WritLegality.Awaiting(w, You))
                {
                    var march = WritLegality.ArmyMoves(w, You, a.Id).FirstOrDefault(m => m.Action == Actions.March);
                    if (march != null) { plan.Armies[a.Id] = march; marches++; }
                }
                // An order the rules do not offer is refused before it is sent.
                var bad = new RuleSession.WritsPlan { Court = new Move(Actions.DeclareWar, You) };
                Assert.Throws<ArgumentException>(() => s.Plan(bad));
                s.Plan(plan);
                int turn = t;
                await Until(() => s.Resolved == turn || s.Finished, $"turn {turn} to resolve");
            }
            Assert.That(marches, Is.GreaterThan(0), "you ordered armies");

            var g = s.BuildReplay();
            Assert.That(g.Turns.Count, Is.EqualTo(s.Resolved), "the screen plays every turn that resolved");
            Assert.That(g.Turns.Sum(x => x.ArmyDrift), Is.EqualTo(0));
            var yours = g.AllEvents.Where(e => e.Decision != null && e.Decision.ModelName == RuleSession.YourModel).ToList();
            Assert.That(yours.Any(e => e.Kind == ReplayEventKind.Recruit), Is.True, "your court's orders are in the replay");
            Assert.That(yours.Any(e => e.Kind == ReplayEventKind.ArmyOrder && e.Decision.ArmyId == e.Army), Is.True, "your armies' orders are in the replay with their army");
            Assert.That(g.AllEvents.Any(e => e.Decision != null && e.Decision.AgentId.EndsWith("_marshal")), Is.True, "the model rulers' marshals ordered armies");

            s.Stop();
            await run;
            Assert.That(s.Error, Is.Null);
        }

        [Test]
        public void A_plan_answers_the_court_twice_and_each_army_with_its_own_order()
        {
            var setup = GameSetup.Writs(5);
            var map = setup.BuildMap();
            var game = new Game(5, map, setup.BuildRules(map));
            var requests = game.BeginTurn();
            var court = requests.Single(r => r.CountryId == You);
            var army = game.World.ArmiesOf(You).First();
            var march = WritLegality.ArmyMoves(game.World, You, army.Id).First(m => m.Action == Actions.March);
            var recruit = court.Legal.First(m => m.Action == Actions.Recruit);
            var taxes = court.Legal.First(m => m.Action == Actions.RaiseTaxes);
            var plan = new RuleSession.WritsPlan { Court = recruit, Court2 = taxes };
            plan.Armies[army.Id] = march;

            Assert.That(plan.For(court), Is.EqualTo(recruit), "the first court request takes the court's order");
            var armyRequest = new CountryRequest(Moment.Army, court.Turn, You, game.World, court.StandingFacts, court.TurnFacts, WritLegality.ArmyMoves(game.World, You, army.Id)) { ArmyId = army.Id };
            Assert.That(plan.For(armyRequest), Is.EqualTo(march));
            var second = new CountryRequest(Moment.Council, court.Turn, You, game.World, court.StandingFacts, court.TurnFacts, court.Legal);
            Assert.That(plan.For(second), Is.EqualTo(taxes), "the second court request takes the second order");
            var other = new CountryRequest(Moment.Army, court.Turn, You, game.World, court.StandingFacts, court.TurnFacts, WritLegality.ArmyMoves(game.World, You, army.Id)) { ArmyId = "nobody_host" };
            Assert.That(plan.For(other).Action, Is.EqualTo(Actions.None), "an army with no order holds");
        }

        [Test]
        public void A_ruler_saying_the_same_line_again_is_shown_once()
        {
            var f = new RepeatFilter();
            Assert.That(f.Repeats("kesh", "We march at dawn."), Is.False);
            Assert.That(f.Repeats("kesh", "We march at dawn. "), Is.True, "the same words from the same ruler, at once again");
            Assert.That(f.Repeats("valen", "We march at dawn."), Is.False, "another ruler's line is its own");
            Assert.That(f.Repeats("kesh", "Hold the line."), Is.False);
            Assert.That(f.Repeats("kesh", "We march at dawn."), Is.False, "said again after something else: shown");
        }

        [Test]
        public void Fog_shows_rival_armies_only_where_your_court_sees()
        {
            var setup = GameSetup.Writs(3);
            var map = setup.BuildMap();
            var rules = setup.BuildRules(map);
            var g = ReplayBuilder.Build(ScriptedWorldGame.Play(setup, map, rules), map, rules);
            var s = g.Turns[10].End;
            var sight = WritsSight.Of(s, g.ProvinceIds, You, id => map.Neighbours(id));
            foreach (var a in s.Armies.Where(a => a.Owner != You))
            {
                bool near = s.Owners.Select((o, i) => (o, i)).Any(x => (x.o == You || s.Controller(x.i) == You) && (g.ProvinceIds[x.i] == a.Location || map.AreNeighbours(g.ProvinceIds[x.i], a.Location)))
                            || s.Armies.Any(m => m.Owner == You && (m.Location == a.Location || map.AreNeighbours(m.Location, a.Location)));
                Assert.That(sight.Contains(a.Location), Is.EqualTo(near), $"{a.Id} at {a.Location}");
            }
            Assert.That(s.Armies.Any(a => a.Owner != You && !sight.Contains(a.Location)), Is.True, "some rival armies are beyond your sight");
        }
    }
}

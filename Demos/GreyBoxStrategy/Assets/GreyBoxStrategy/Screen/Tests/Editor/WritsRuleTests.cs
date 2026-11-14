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
        public async Task Unlimited_writs_from_new_game_reach_the_rules_and_every_army_is_asked()
        {
            Assert.That(new NewGameChoice { Rule = true, Writs = true, UnlimitedWrits = true }.YourWritsUnlimited, Is.True);
            Assert.That(new NewGameChoice { Rule = false, Writs = true, UnlimitedWrits = true }.YourWritsUnlimited, Is.False, "only when you rule");
            Assert.That(new NewGameChoice { Rule = true, Writs = false, UnlimitedWrits = true }.YourWritsUnlimited, Is.False, "only on the writs turn");
            Assert.That(new NewGameChoice().UnlimitedWrits, Is.False, "off by default");

            var setup = GameSetup.Writs(5);
            var map = setup.BuildMap();
            var given = setup.BuildRules(map);
            Assert.That(ReplayDirector.WithYourWrits(given, You, false), Is.SameAs(given), "off: the rules as given");
            var rules = ReplayDirector.WithYourWrits(given, You, true);
            Assert.That(given.WritsOverride.ContainsKey(You), Is.False, "the preset's rules are not changed");
            Assert.That(rules.WritsOverride[You], Is.EqualTo(GameRules.UnlimitedWrits));
            Assert.That(rules.WritsOverride.Count, Is.EqualTo(1), "you alone");

            World w = null;
            var provider = Rulers(() => w);
            var s = new RuleSession(You, "qwen3.5:4b", "default", 5, Path.Combine(folder, "decisions.jsonl"), (m, world) => { w = world; return provider; }, null, map, rules);
            var run = s.RunAsync();
            for (int t = 1; t <= 3; t++)
            {
                await Until(() => (s.WaitingFor == RuleSession.Waiting.Orders && s.Orders != null) || s.WaitingFor == RuleSession.Waiting.Envoy || s.Finished, $"turn {t}'s orders");
                if (s.WaitingFor == RuleSession.Waiting.Envoy) { s.Answer(false); t--; continue; }
                var court = s.Orders;
                Assert.That(court.Writs, Is.EqualTo(GameRules.UnlimitedWrits), "your court is told it has no limit");
                foreach (var other in court.World.CountryList.Where(c => c.Info.Id != You))
                    Assert.That(court.World.Writs(other.Info.Id), Is.LessThanOrEqualTo(court.World.Rules.MaxWrits), $"{other.Info.Id} keeps the rules' count");
                // Every army awaiting orders gets one, however many: the plan the rules accept says so.
                var plan = new RuleSession.WritsPlan { Court = court.Legal.FirstOrDefault(m => m.Action == Actions.Recruit) ?? court.Legal.First() };
                foreach (var a in WritLegality.Awaiting(court.World, You))
                    plan.Armies[a.Id] = WritLegality.ArmyMoves(court.World, You, a.Id).FirstOrDefault(m => m.Action == Actions.March) ?? WritLegality.ArmyMoves(court.World, You, a.Id).First();
                s.Plan(plan);
                int turn = t;
                await Until(() => s.Resolved == turn || s.Finished, $"turn {turn} to resolve");
            }
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
        public async Task You_rule_a_realm_of_a_generated_world_and_the_replay_rebuilds_it()
        {
            // A medium world, six realms: you take the third seat, five stand-ins the rest, through the real agents.
            var setup = GameSetup.Writs(3, GameSetup.GeneratedMapId);
            setup.Countries = 6;
            setup.Generator = new MapSettings { Size = MapSize.Medium, Shape = LandShape.Continent };
            var map = setup.BuildMap();
            Assert.That(map.Provinces.Count, Is.GreaterThan(40), "a medium world, not the Classic board");
            string you = map.Countries[2].Id;
            World w = null;
            var provider = Rulers(() => w);
            var s = new RuleSession(you, "qwen3.5:4b", "default", 3, Path.Combine(folder, "decisions.jsonl"), (m, world) => { w = world; return provider; }, null, map, setup.BuildRules(map));
            Assert.That(s.Players.Count, Is.EqualTo(6), "every realm has a ruler, the personas taken in turn past the fifth");
            var run = s.RunAsync();
            int marches = 0;
            for (int t = 1; t <= 4; t++)
            {
                await Until(() => (s.WaitingFor == RuleSession.Waiting.Orders && s.Orders != null) || s.WaitingFor == RuleSession.Waiting.Envoy || s.Finished, $"turn {t}'s orders");
                if (s.WaitingFor == RuleSession.Waiting.Envoy) { s.Answer(false); t--; continue; }
                var court = s.Orders;
                Assert.That(court.Legal, Is.EqualTo(WritLegality.CourtMoves(court.World, you)));
                var plan = new RuleSession.WritsPlan { Court = court.Legal.FirstOrDefault(m => m.Action == Actions.Recruit) ?? court.Legal.First() };
                foreach (var a in WritLegality.Awaiting(court.World, you))
                {
                    var march = WritLegality.ArmyMoves(court.World, you, a.Id).FirstOrDefault(m => m.Action == Actions.March);
                    if (march != null) { plan.Armies[a.Id] = march; marches++; }
                }
                s.Plan(plan);
                int turn = t;
                await Until(() => s.Resolved == turn || s.Finished, $"turn {turn} to resolve");
            }
            Assert.That(marches, Is.GreaterThan(0));
            var g = s.BuildReplay();
            Assert.That(g.ProvinceIds.Count, Is.EqualTo(map.Provinces.Count));
            Assert.That(g.Turns.Count, Is.EqualTo(s.Resolved));
            Assert.That(g.Turns.Sum(x => x.ArmyDrift), Is.EqualTo(0), "the screen's replay matches the rules on a generated world");
            Assert.That(g.AllEvents.Any(e => e.Decision != null && e.Decision.ModelName == RuleSession.YourModel && e.Kind == ReplayEventKind.ArmyOrder), Is.True);
            // Fog on a world this size: your court sees a part of it, not all of it.
            var sight = WritsSight.Of(g.Turns[g.Turns.Count - 1].End, g.ProvinceIds, you, id => map.Neighbours(id));
            Assert.That(sight.Count, Is.GreaterThan(0).And.LessThan(map.Provinces.Count / 2));
            s.Stop();
            await run;
            Assert.That(s.Error, Is.Null);
        }

        [Test]
        public void New_game_starts_every_way_of_playing_on_either_world()
        {
            NewGameRoute R(bool generated, bool writs, bool rule, bool scripted) =>
                new NewGameChoice { Generated = generated, Writs = writs, Rule = rule, Scripted = scripted }.Route(out _);
            Assert.That(R(true, true, true, false), Is.EqualTo(NewGameRoute.WorldRule));
            Assert.That(R(true, true, true, true), Is.EqualTo(NewGameRoute.WorldRule), "stand-ins through the agents");
            Assert.That(R(true, true, false, true), Is.EqualTo(NewGameRoute.WorldReplay));
            Assert.That(R(true, true, false, false), Is.EqualTo(NewGameRoute.WorldLive));
            Assert.That(R(true, false, false, true), Is.EqualTo(NewGameRoute.WorldReplay), "the one-move game as a replay");
            Assert.That(R(true, false, true, false), Is.EqualTo(NewGameRoute.None), "the one-move game is not ruled on a generated world");
            Assert.That(R(false, true, true, false), Is.EqualTo(NewGameRoute.ClassicWritsRule));
            Assert.That(R(false, true, false, true), Is.EqualTo(NewGameRoute.ClassicWritsReplay));
            Assert.That(R(false, false, false, false), Is.EqualTo(NewGameRoute.ClassicLive));
            var tooMany = new NewGameChoice { Generated = true, Writs = true, Size = MapSize.Small, Countries = 9 };
            Assert.That(tooMany.Route(out string why), Is.EqualTo(NewGameRoute.None));
            Assert.That(why, Does.Contain("at most"), "a small world seats at most five");
            Assert.That(new NewGameChoice { Generated = true, Size = MapSize.Large }.CountriesFor(), Is.EqualTo(MapSettings.DefaultCountries(MapSize.Large)));
        }

        [Test]
        public void Pieces_shrink_as_provinces_come_close_on_screen()
        {
            Assert.That(ReplayDirector.HostScaleFor(200), Is.EqualTo(1f), "far apart: full size");
            Assert.That(ReplayDirector.HostScaleFor(70), Is.LessThan(0.7f).And.GreaterThan(0.6f), "a large world at its whole view: about two thirds");
            Assert.That(ReplayDirector.HostScaleFor(10), Is.EqualTo(0.6f), "never smaller than 60%, so the number still reads");
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
        public void The_chronicle_says_a_recruit_with_names_not_ids()
        {
            string P(string id) => char.ToUpperInvariant(id[0]) + id.Substring(1);
            Assert.That(Chronicle.RecruitText("raised 15 soldiers at orskhaven (fenna_host +15)", P), Is.EqualTo("raises 15 soldiers at Orskhaven for Fenna's host"));
            Assert.That(Chronicle.RecruitText("raised 15 soldiers at highmoor (maren_host +5, new tolly_host2 10)", P),
                Is.EqualTo("raises 15 soldiers at Highmoor: 5 for Maren's host and a new army, Tolly's second host, of 10"));
            Assert.That(Chronicle.Host("hester2_host"), Is.EqualTo("Hester's second host"), "a general's name taken again is numbered by the rules");
            Assert.That(Chronicle.RecruitText("recruited 10 soldiers for 50 gold", P), Is.EqualTo("recruits 10 soldiers for 50 gold"), "the one-move game's recruit");
            var e = new ReplayEvent { Turn = 3, Kind = ReplayEventKind.Recruit, Country = "orsk", Text = "raised 15 soldiers at orskhaven (fenna_host +15)" };
            var line = Chronicle.Line(e, id => P(id));
            Assert.That(line.Text, Does.Not.Contain("_host").And.Not.Contain("orskhaven").And.Not.Contain("raised"));
            Assert.That(line.Short, Is.EqualTo("raises 15 soldiers at Orskhaven for Fenna's host"));
        }

        [Test]
        public void The_rules_reasons_are_said_with_names()
        {
            var words = new Wording(new[] { "valen", "kesh" }, new[] { "highmoor", "corwick" }, id => id == "valen" ? "Valen" : "Kesh");
            Assert.That(words.Destination("highmoor belongs to valen, at peace with us"), Is.EqualTo("Valen's, and we are at peace"));
            Assert.That(words.Destination("corwick is more than 3 steps away"), Is.EqualTo("more than 3 steps away"));
            Assert.That(words.Plain("highmoor belongs to valen, at peace with us"), Is.EqualTo("Highmoor is Valen's, and we are at peace"));
            Assert.That(words.Plain("we are already at war with valen"), Is.EqualTo("We are already at war with Valen"));
            Assert.That(words.Plain("maren_host cannot raise_taxes at corwick"), Is.EqualTo("Maren's host cannot raise taxes at Corwick"));
        }

        [Test]
        public void A_rulers_orders_of_a_turn_are_one_entry_with_its_line_once()
        {
            ChronicleLine L(int turn, string c, ReplayEventKind k, string text, string quote) =>
                Chronicle.Line(new ReplayEvent { Turn = turn, Kind = k, Country = c, Province = "glimmerford", Text = text, Statement = quote }, id => char.ToUpperInvariant(id[0]) + id.Substring(1));
            var oldestFirst = new List<ChronicleLine>
            {
                L(3, "merrow", ReplayEventKind.Fort, "", "Glimmerford is mine."),
                L(3, "orsk", ReplayEventKind.Taxes, "", "Gold."),
                L(3, "merrow", ReplayEventKind.Fort, "", "Glimmerford is mine!"),
                L(3, "valen", ReplayEventKind.Capture, "", ""),
                L(4, "merrow", ReplayEventKind.Fort, "", "Again."),
            };
            var groups = Chronicle.Group(Enumerable.Reverse(oldestFirst).ToList(), ScreenUI.SaidAgain);
            Assert.That(groups.Count, Is.EqualTo(4), "turn 4's fort, turn 3's capture, Orsk's taxes, Merrow's two forts as one");
            var merrow = groups.Single(g => g.Turn == 3 && g.Country == "merrow");
            Assert.That(merrow.Lines.Count, Is.EqualTo(2));
            Assert.That(merrow.Items.Select(i => i.text), Is.EqualTo(new[] { "fortifies Glimmerford, twice" }));
            Assert.That(merrow.Quotes, Is.EqualTo(new[] { "Glimmerford is mine." }), "the same words once");
            Assert.That(groups.Single(g => g.Turn == 3 && g.Country == "valen").Single, Is.True, "a capture is not an order: its own line");
            Assert.That(groups.IndexOf(merrow), Is.LessThan(groups.FindIndex(g => g.Country == "orsk")), "the group stands where its newest order stood");
        }

        [Test]
        public void Pieces_that_would_overlap_are_set_apart()
        {
            var at = new List<Vector2> { new Vector2(100, 100), new Vector2(110, 104), new Vector2(300, 100), new Vector2(104, 98) };
            var size = Enumerable.Repeat(new Vector2(52, 20), 4).ToList();
            ScreenUI.Unclutter(at, size, new List<bool> { false, false, false, true });
            Assert.That(at[3], Is.EqualTo(new Vector2(104, 98)), "a moving piece stays on its path");
            Assert.That(at[2], Is.EqualTo(new Vector2(300, 100)), "a piece clear of the others stays");
            for (int i = 0; i < at.Count; i++)
                for (int j = i + 1; j < at.Count; j++)
                    Assert.That(Mathf.Abs(at[i].x - at[j].x) >= 54 || Mathf.Abs(at[i].y - at[j].y) >= 21.9f, Is.True, $"{i} and {j} overlap: {at[i]} {at[j]}");
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

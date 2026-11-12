using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Agenerela;
using Agenerela.Providers;
using GreyBoxStrategy.Realm;
using GreyBoxStrategy.Replay;
using NUnit.Framework;
using UnityEngine;

namespace GreyBoxStrategy.Screen.Tests
{
    /// <summary>
    /// Rule mode without a screen: you rule Kesh, and a ScriptedProvider plays the four model seats
    /// through the real CountryAgent and DecisionRound, as the RTX 5080 would.
    /// </summary>
    public class RuleSessionTests
    {
        private const string You = "kesh";
        private string folder;

        [SetUp] public void SetUp() => folder = Path.Combine(Application.temporaryCachePath, "rule-session-tests-" + Guid.NewGuid().ToString("N"));
        [TearDown] public void TearDown() { try { Directory.Delete(folder, true); } catch (IOException) { } }

        private RuleSession Session(ScriptedProvider provider, RuleSession.ICardLock cardLock = null, string you = You) =>
            new RuleSession(you, "qwen3.5:4b", "default", 11, Path.Combine(folder, "decisions.jsonl"), (m, w) => provider, cardLock);

        private static async Task Until(Func<bool> condition, string what)
        {
            for (int i = 0; i < 2000 && !condition(); i++) await Task.Delay(5);
            Assert.That(condition(), Is.True, "timed out waiting for " + what);
        }

        [Test]
        public async Task The_rulers_decide_at_once_and_the_turn_waits_for_your_order()
        {
            var provider = new ScriptedProvider().Otherwise(ScriptedReply.Decide("none", statement: "We wait."));
            var s = Session(provider);
            var run = s.RunAsync();
            await Until(() => s.WaitingFor == RuleSession.Waiting.Orders && !s.ModelsThinking, "your first order");

            Assert.That(provider.RequestCount, Is.EqualTo(4), "the four model seats decided while you chose");
            Assert.That(s.Resolved, Is.EqualTo(0), "nothing lands before your order");
            Assert.That(s.Orders.CountryId, Is.EqualTo(You));
            Assert.That(s.Orders.Legal, Is.EqualTo(Legality.CouncilMoves(s.Game.World, You)), "you are offered exactly what a model seat is offered");
            Assert.Throws<ArgumentException>(() => s.Order(new Move(Actions.Attack, "highmoor")), "a move not offered is refused");

            var recruit = s.Orders.Legal.First(m => m.Action == Actions.Recruit);
            int armyBefore = s.Game.World.Country(You).Army;
            s.Order(recruit);
            await Until(() => s.Resolved == 1 && s.WaitingFor == RuleSession.Waiting.Orders, "turn 2's council");

            Assert.That(s.Game.World.Country(You).Army, Is.GreaterThan(armyBefore), "your recruit landed");
            var g = s.BuildReplay();
            Assert.That(g.Turns.Count, Is.EqualTo(1), "the screen plays only what has resolved");
            var mine = g.Turns[0].Events.Single(e => e.Decision != null && e.Decision.AgentId == You);
            Assert.That(mine.Decision.Action, Is.EqualTo(Actions.Recruit));
            Assert.That(mine.Decision.ModelName, Is.EqualTo(RuleSession.YourModel));
            Assert.That(g.Turns[0].Events.Count(e => e.Decision != null && e.Decision.AgentId != You), Is.EqualTo(4), "the four rulers' moves are in the replay beside yours");

            s.Stop();
            await run;
            Assert.That(s.Stopped, Is.True);
            Assert.That(s.Error, Is.Null);
        }

        [Test]
        public async Task A_peace_offer_to_you_waits_for_your_answer()
        {
            // Every ruler at war with you offers peace; the others hold.
            var provider = new ScriptedProvider
            {
                Callback = r => r.OfferedActions.Contains(Actions.OfferPeace) && r.OfferedTargets.Contains(You)
                    ? ScriptedReply.Decide(Actions.OfferPeace, You, "Peace, neighbour.") : null,
            }.Otherwise(ScriptedReply.Decide("none", statement: "We wait."));
            var s = Session(provider);
            var run = s.RunAsync();
            // Kesh borders no country at the start: take neutral land until a
            // rival borders you, then declare war on it.
            Move war = null;
            for (int t = 0; t < 12 && war == null; t++)
            {
                await Until(() => s.WaitingFor == RuleSession.Waiting.Orders, $"order {t + 1}");
                war = s.Orders.Legal.FirstOrDefault(m => m.Action == Actions.DeclareWar);
                s.Order(war ?? s.Orders.Legal.FirstOrDefault(m => m.Action == Actions.Attack) ?? Move.Hold);
                int turns = t + 1;
                await Until(() => s.Resolved == turns && (s.WaitingFor == RuleSession.Waiting.Orders || s.Finished), $"turn {turns} to resolve");
            }
            Assert.That(war, Is.Not.Null, "a rival came to border you");
            Assert.That(s.Game.World.AtWar(You, war.Target), Is.True);
            int resolved = s.Resolved;

            s.Order(Move.Hold);
            await Until(() => s.WaitingFor == RuleSession.Waiting.Envoy, "the envoy");
            Assert.That(s.Offer.OfferedBy, Is.EqualTo(war.Target));
            Assert.That(s.OfferStatement, Is.EqualTo("Peace, neighbour."));
            Assert.That(s.Resolved, Is.EqualTo(resolved), "the turn waits for your answer");
            s.Answer(true);
            await Until(() => s.Resolved == resolved + 1, "the turn to resolve");

            Assert.That(s.Game.World.RelationBetween(You, war.Target), Is.EqualTo(Relation.Truce));
            var g = s.BuildReplay();
            Assert.That(g.Turns[resolved].Events.Any(e => e.Kind == ReplayEventKind.PeaceAccepted), Is.True, "the replay shows the truce you accepted");
            s.Stop();
            await run;
        }

        [Test]
        public async Task The_card_is_held_for_the_whole_game_and_let_go_when_it_ends()
        {
            var cardLock = new CountingLock();
            var provider = new ScriptedProvider().Otherwise(ScriptedReply.Decide("none"));
            var s = Session(provider, cardLock);
            var run = s.RunAsync();
            await Until(() => s.WaitingFor == RuleSession.Waiting.Orders && !s.ModelsThinking, "your first order");
            Assert.That(cardLock.Held && s.HoldsCard, Is.True, "held while the game waits for you: a person is playing");
            s.Order(Move.Hold);
            await Until(() => s.Resolved == 1 && s.WaitingFor == RuleSession.Waiting.Orders && !s.ModelsThinking, "turn 2's council");
            Assert.That(cardLock.Taken, Is.EqualTo(1), "taken once for the game, not once a round");
            Assert.That(cardLock.Held, Is.True);
            s.Stop();
            await run;
            Assert.That(cardLock.Held || s.HoldsCard, Is.False, "let go when the game is left");
            Assert.That(cardLock.Released, Is.EqualTo(1));
        }

        [Test]
        public async Task A_round_that_never_answers_stops_the_game_and_says_why()
        {
            var cardLock = new CountingLock();
            var provider = new ScriptedProvider().Otherwise(ScriptedReply.Never());
            var s = Session(provider, cardLock);
            s.RoundTimeout = TimeSpan.FromSeconds(0.3);
            var run = s.RunAsync();
            // The game is waiting for your order when the rulers' round times out: it must not wait on.
            await Until(() => s.Finished, "the game to stop");
            await run;
            Assert.That(s.Error, Does.Contain("did not answer within"), "a message the screen can show");
            Assert.That(s.Stopped, Is.False);
            Assert.That(cardLock.Held || s.HoldsCard, Is.False, "the card is let go on a timeout too");
            Assert.That(cardLock.Released, Is.EqualTo(1));
        }

        [Test]
        public async Task Leaving_while_the_card_is_busy_never_takes_it()
        {
            var cardLock = new BusyLock();
            var s = Session(new ScriptedProvider().Otherwise(ScriptedReply.Decide("none")), cardLock);
            var run = s.RunAsync();
            await Until(() => s.WaitingFor == RuleSession.Waiting.Card, "the wait for the card");
            Assert.That(s.WaitingForCard, Is.EqualTo("another session (since 23:41)"), "the screen can say who holds it");
            Assert.That(s.Orders, Is.Null, "no council meets before the game has the card");
            s.Stop();
            await run;
            Assert.That(s.Stopped, Is.True);
            Assert.That(cardLock.Released, Is.EqualTo(0), "it never had the card, so it lets nothing go");
        }

        private sealed class CountingLock : RuleSession.ICardLock
        {
            public bool Held; public int Taken, Released;
            public string WaitingFor => null;
            public async Awaitable Acquire(System.Threading.CancellationToken ct) { Assert.That(Held, Is.False, "taken twice"); Held = true; Taken++; await Task.CompletedTask; }
            public async Awaitable Release() { Held = false; Released++; await Task.CompletedTask; }
        }

        // Another session holds the card for as long as the test runs.
        private sealed class BusyLock : RuleSession.ICardLock
        {
            public int Released;
            public string WaitingFor => "another session (since 23:41)";
            public async Awaitable Acquire(System.Threading.CancellationToken ct)
            {
                while (true) { ct.ThrowIfCancellationRequested(); await Task.Delay(5); }
            }
            public async Awaitable Release() { Released++; await Task.CompletedTask; }
        }
    }

    public class FogTests
    {
        private static ReplayGame Start()
        {
            var empty = new Recording { Seed = 11, Title = "empty" };
            return ReplayBuilder.Build(empty, partial: true);
        }

        [Test]
        public void Your_court_knows_your_land_and_its_borders_not_beyond()
        {
            var g = Start(); var q = new RealmQueries(g); var s = g.Initial;
            var fog = new Fog("kesh", q);
            int keshtar = g.IndexOf("keshtar"), highmoor = g.IndexOf("highmoor");
            Assert.That(fog.KnowsProvince(s, keshtar), Is.True, "your capital");
            foreach (int n in q.Neighbours(keshtar)) Assert.That(fog.KnowsProvince(s, n), Is.True, $"{g.ProvinceIds[n]} touches your land");
            Assert.That(fog.KnowsProvince(s, highmoor), Is.False, "Valen's capital is across the map");
            Assert.That(fog.Known(s).Count, Is.LessThan(25));
            Assert.That(fog.KnowsTreasury("kesh"), Is.True);
            Assert.That(fog.KnowsTreasury("valen"), Is.False);
            Assert.That(fog.KnowsDecision("valen", gameOver: false), Is.False, "a ruler's council is sealed while the game runs");
            Assert.That(fog.KnowsDecision("valen", gameOver: true), Is.True);
            Assert.That(fog.KnowsUpkeep(new ReplayEvent { Kind = ReplayEventKind.Upkeep, Country = "valen" }), Is.False, "another country's harvest");
            Assert.That(fog.KnowsUpkeep(new ReplayEvent { Kind = ReplayEventKind.Upkeep, Country = "kesh" }), Is.True);
            Assert.That(fog.KnowsUpkeep(new ReplayEvent { Kind = ReplayEventKind.Revolt, Country = "valen" }), Is.True, "land changing hands is public");
        }

        [Test]
        public void Watch_mode_hides_nothing()
        {
            var g = Start(); var fog = new Fog(null, new RealmQueries(g));
            Assert.That(fog.Known(g.Initial).Count, Is.EqualTo(25));
            Assert.That(fog.KnowsTreasury("valen") && fog.KnowsDecision("valen", false), Is.True);
        }
    }

    public class GatewayTests
    {
        [Test]
        public void A_worktree_shares_the_main_checkouts_lock()
        {
            string tmp = Path.Combine(Application.temporaryCachePath, "gateway-" + Guid.NewGuid().ToString("N"));
            string main = Path.Combine(tmp, "repo"), tree = Path.Combine(tmp, "repo", ".claude", "worktrees", "w");
            try
            {
                Directory.CreateDirectory(Path.Combine(main, ".git", "worktrees", "w"));
                Directory.CreateDirectory(tree);
                File.WriteAllText(Path.Combine(tree, ".git"), "gitdir: " + Path.Combine(main, ".git", "worktrees", "w").Replace('\\', '/') + "\n");
                Assert.That(Path.GetFullPath(Gateway.MainCheckout(tree)), Is.EqualTo(Path.GetFullPath(main)), "a worktree leads back to the main checkout");
                Assert.That(Gateway.MainCheckout(main), Is.EqualTo(main), "the main checkout is its own");
            }
            finally { try { Directory.Delete(tmp, true); } catch (IOException) { } }
        }

        [Test]
        public void The_lock_is_released_only_by_the_Unity_that_took_it()
        {
            string tmp = Path.Combine(Application.temporaryCachePath, "lock-" + Guid.NewGuid().ToString("N"));
            string held = Path.Combine(tmp, "held");
            try
            {
                Directory.CreateDirectory(held);
                File.WriteAllText(Path.Combine(held, "owner.txt"), "owner: another session\npid: 1 (Git Bash)\nsince: 2026-11-11T23:41:37\nexpect: 30 min\n");
                var note = Gateway.ReadLock(held);
                Assert.That(note.ToString(), Is.EqualTo("another session (since 23:41, expects 30 min)"));
                Assert.That(Gateway.TakeLock(held, "test", "test", null), Is.False, "someone else holds it");
                Gateway.ReleaseLock(held);
                Assert.That(Directory.Exists(held), Is.True, "another session's lock is never deleted");

                Directory.Delete(held, true);
                Assert.That(Gateway.TakeLock(held, "test", "test", null), Is.True);
                Assert.That(Gateway.HoldsLock, Is.True);
                Gateway.ReleaseHeldNow(); // as quitting, leaving Play mode or a script reload does
                Assert.That(Directory.Exists(held), Is.False, "the lock this Unity took is let go on every way out");
                Assert.That(Gateway.HoldsLock, Is.False);
            }
            finally { try { Directory.Delete(tmp, true); } catch (IOException) { } }
        }
    }

    public class UiScaleTests
    {
        [TestCase(1920, 1080, 1f, false)]
        [TestCase(2560, 1440, 1.3333f, false)]
        [TestCase(3840, 2160, 2f, false)]
        [TestCase(3440, 1440, 1.3333f, false)] // ultrawide: the height decides
        [TestCase(1366, 768, 1f, true)]
        [TestCase(2202, 1168, 1.0815f, false)] // the lead's maximized Game view on 12 November
        public void The_UI_grows_with_the_screen_and_never_shrinks_below_its_design(int w, int h, float scale, bool compact)
        {
            Assert.That(UiScale.For(w, h), Is.EqualTo(scale).Within(0.001f));
            Assert.That(UiScale.Compact(w, h), Is.EqualTo(compact));
        }
    }
}

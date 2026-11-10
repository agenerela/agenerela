using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Agenerela;
using Agenerela.Providers;
using LivingVale.Agenerela;
using LivingVale.Interaction;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace LivingVale.Tests
{
    /// <summary>The coordinator over the NPCs' real agents and conversations (#209), with a model answered by hand.</summary>
    public sealed class ConversationCoordinatorTests
    {
        private static readonly NpcDescriptor Mara = new NpcDescriptor("npc_mara", "Mara", "General trader");
        private static readonly NpcDescriptor Iven = new NpcDescriptor("npc_iven", "Iven", "Healer");

        private HandProvider provider;
        private AgenerelaNpcDecisionDriver driver;
        private List<NpcTurn> turns;
        private ConversationCoordinator coordinator;
        private List<ConversationEvent> events;

        private AgenerelaNpcDecisionDriver Driver(System.Action<NpcDescriptor, Agent> configure = null)
        {
            var built = new AgenerelaNpcDecisionDriver(provider, "hand", configureAgent: configure);
            built.TurnObservations = turn =>
            {
                turns.Add(turn);
                return null;
            };
            return built;
        }

        [SetUp]
        public void SetUp()
        {
            provider = new HandProvider();
            turns = new List<NpcTurn>();
            driver = Driver();
            coordinator = new ConversationCoordinator(driver);
            events = new List<ConversationEvent>();
            coordinator.Changed += events.Add;
        }

        private IEnumerable<ConversationEventKind> Kinds => events.Select(e => e.Kind);

        // The deadline ends the turn through Unity's main loop, which batch mode can run slowly:
        // wait for the timeout's event itself, boundedly, rather than for a fixed time.
        private async Task UntilTimedOut()
        {
            for (int i = 0; i < 100 && !events.Any(e => e.Kind == ConversationEventKind.TurnFailed && e.Failure == TurnFailure.Timeout); i++)
            {
                await Task.Delay(50);
            }
        }

        private static string[] Lines(IReadOnlyList<ChatLine> history) => history.Select(l => l.ToString()).ToArray();

        [Test]
        public async Task ReplyIsExecutedAndShownInTheOpenConversation()
        {
            coordinator.Open(Mara);
            Assert.That(coordinator.Submit("  hello  "), Is.True);
            Assert.That(coordinator.IsAwaitingReply, Is.True);

            var turn = coordinator.LastTurn;
            provider.Say(0, "Morning.");
            await Run.NotCancelled(turn);

            Assert.That(Lines(coordinator.HistoryFor(Mara.Id)), Is.EqualTo(new[] { "Player: hello", "Npc: Morning." }));
            Assert.That(driver.ExecutedCount, Is.EqualTo(1));
            Assert.That(coordinator.IsAwaitingReply, Is.False);
            Assert.That(Kinds, Is.EqualTo(new[]
            {
                ConversationEventKind.Opened, ConversationEventKind.PlayerSaid,
                ConversationEventKind.Waiting, ConversationEventKind.NpcReplied
            }));
        }

        [Test]
        public void OneTurnAtATime_NothingQueued()
        {
            coordinator.Open(Mara);
            Assert.That(coordinator.Submit("first"), Is.True);
            Assert.That(coordinator.Submit("second"), Is.False);

            Assert.That(turns, Has.Count.EqualTo(1));
            Assert.That(Lines(coordinator.HistoryFor(Mara.Id)), Is.EqualTo(new[] { "Player: first" }));
        }

        [Test]
        public void SubmitRefusesBlankTooLongOrClosed()
        {
            Assert.That(coordinator.Submit("hello"), Is.False, "no conversation open");

            coordinator.Open(Mara);
            Assert.That(coordinator.Submit(null), Is.False);
            Assert.That(coordinator.Submit("   "), Is.False);
            Assert.That(coordinator.Submit(new string('a', coordinator.Settings.MaxLineLength + 1)), Is.False);
            Assert.That(coordinator.Submit(new string('a', coordinator.Settings.MaxLineLength)), Is.True);
            Assert.That(turns, Has.Count.EqualTo(1));
        }

        // The G1 exit check's "without cross-talk": a reply to Mara that arrives after the player
        // turned to Iven is applied to neither.
        [Test]
        public async Task SwitchingSpeaker_LateReplyReachesNoOne()
        {
            provider.IgnoresCancellation = true;
            coordinator.Open(Mara);
            coordinator.Submit("where is the wagon?");
            var maraTurn = coordinator.LastTurn;

            coordinator.Open(Iven);
            Assert.That(coordinator.ActiveNpc, Is.SameAs(Iven));
            Assert.That(coordinator.IsAwaitingReply, Is.False);

            provider.Say(0, "Ask Iven.");
            await Run.NotCancelled(maraTurn);

            Assert.That(Lines(coordinator.HistoryFor(Mara.Id)), Is.EqualTo(new[] { "Player: where is the wagon?" }));
            Assert.That(coordinator.HistoryFor(Iven.Id), Is.Empty);
            Assert.That(driver.ExecutedCount, Is.Zero);
            Assert.That(Kinds, Does.Contain(ConversationEventKind.StaleReplyDropped));
            Assert.That(events.Single(e => e.Kind == ConversationEventKind.Closed).CloseReason,
                Is.EqualTo(ConversationCloseReason.SwitchedSpeaker));
        }

        [Test]
        public async Task SwitchingSpeaker_CancelsTheBackendRequest()
        {
            coordinator.Open(Mara);
            coordinator.Submit("hello");
            var maraTurn = coordinator.LastTurn;

            coordinator.Open(Iven);
            await Run.NotCancelled(maraTurn);

            Assert.That(driver.ExecutedCount, Is.Zero);
            Assert.That(Kinds, Does.Contain(ConversationEventKind.StaleReplyDropped));
        }

        [Test]
        public async Task ClosingThenReopening_OldReplyStaysDropped_NewOneApplies()
        {
            provider.IgnoresCancellation = true;
            coordinator.Open(Mara);
            coordinator.Submit("first");
            var oldTurn = coordinator.LastTurn;

            coordinator.Close(ConversationCloseReason.LeftRange);
            coordinator.Open(Mara);
            coordinator.Submit("second");
            var newTurn = coordinator.LastTurn;

            // The old answer lands while the new turn, to the same NPC, is pending.
            provider.Say(0, "Answer to first.");
            await Run.NotCancelled(oldTurn);
            Assert.That(coordinator.IsAwaitingReply, Is.True, "the old reply must not end the new turn");

            provider.Say(1, "Answer to second.");
            await Run.NotCancelled(newTurn);

            Assert.That(Lines(coordinator.HistoryFor(Mara.Id)),
                Is.EqualTo(new[] { "Player: first", "Player: second", "Npc: Answer to second." }));
            Assert.That(driver.ExecutedCount, Is.EqualTo(1));
        }

        [Test]
        public async Task CancelPending_DropsTheLateReply_AndAllowsAnotherTry()
        {
            provider.IgnoresCancellation = true;
            coordinator.Open(Mara);
            coordinator.Submit("hello");
            var turn = coordinator.LastTurn;

            coordinator.CancelPending();
            Assert.That(coordinator.IsAwaitingReply, Is.False);
            Assert.That(coordinator.IsOpen, Is.True);

            provider.Say(0, "Too late.");
            await Run.NotCancelled(turn);

            Assert.That(driver.ExecutedCount, Is.Zero);
            Assert.That(Lines(coordinator.HistoryFor(Mara.Id)),
                Is.EqualTo(new[] { "Player: hello", "System: You stop waiting for an answer." }));
            Assert.That(coordinator.Submit("hello again"), Is.True);
        }

        [Test]
        public async Task Timeout_EndsTheTurnWithAGameLine()
        {
            coordinator = new ConversationCoordinator(driver, new ConversationSettings { TurnTimeout = TimeSpan.FromMilliseconds(50) });
            coordinator.Changed += events.Add;
            coordinator.Open(Mara);
            coordinator.Submit("hello");

            await Run.NotCancelled(coordinator.LastTurn);

            var failed = events.Single(e => e.Kind == ConversationEventKind.TurnFailed);
            Assert.That(failed.Failure, Is.EqualTo(TurnFailure.Timeout));
            Assert.That(coordinator.HistoryFor(Mara.Id).Last().Speaker, Is.EqualTo(ChatSpeaker.System));
            Assert.That(coordinator.IsAwaitingReply, Is.False);
            Assert.That(coordinator.Submit("still there?"), Is.True);
        }

        // A backend that ignores cancellation and answers after the timeout: the turn already
        // failed, so the answer is late and must not run.
        [Test]
        public async Task Timeout_ReplyArrivingAfterwardsIsNotApplied()
        {
            provider.IgnoresCancellation = true;
            coordinator = new ConversationCoordinator(driver, new ConversationSettings { TurnTimeout = TimeSpan.FromMilliseconds(50) });
            coordinator.Changed += events.Add;
            coordinator.Open(Mara);
            coordinator.Submit("hello");
            var turn = coordinator.LastTurn;

            // The turn must end at the deadline, without waiting for the backend.
            await UntilTimedOut();
            Assert.That(events.Single(e => e.Kind == ConversationEventKind.TurnFailed).Failure, Is.EqualTo(TurnFailure.Timeout));
            Assert.That(coordinator.IsAwaitingReply, Is.False);
            Assert.That(turn.IsCompleted, Is.True, "the turn ended at its deadline, with the backend still out");
            Assert.That(coordinator.Submit("are you there?"), Is.True);

            provider.Say(0, "Sorry, what?");
            await Run.NotCancelled(turn);

            Assert.That(driver.ExecutedCount, Is.Zero);
            Assert.That(coordinator.HistoryFor(Mara.Id).Any(l => l.Text == "Sorry, what?"), Is.False);
            Assert.That(coordinator.IsAwaitingReply, Is.True, "the late answer must not end the retry");
            Assert.That(provider.Requests[1].History.Count, Is.Zero, "and the NPC never remembers it");
        }

        [TestCase(NpcReplyFailure.Malformed, TurnFailure.Malformed)]
        [TestCase(NpcReplyFailure.ProviderError, TurnFailure.ProviderError)]
        public async Task DriverFailure_IsReportedAndNothingExecutes(NpcReplyFailure reported, TurnFailure expected)
        {
            coordinator.Open(Mara);
            coordinator.Submit("hello");
            var turn = coordinator.LastTurn;
            if (reported == NpcReplyFailure.Malformed)
            {
                provider.Answer(0, "Not a decision.");
            }
            else
            {
                provider.Fail(0, new ProviderException(ProviderErrorKind.ServerError, provider.Name, "Backend error."));
            }

            await Run.NotCancelled(turn);

            Assert.That(events.Single(e => e.Kind == ConversationEventKind.TurnFailed).Failure, Is.EqualTo(expected));
            Assert.That(driver.ExecutedCount, Is.Zero);
        }

        [Test]
        public async Task NullReply_IsMalformed()
        {
            coordinator.Open(Mara);
            coordinator.Submit("hello");
            var turn = coordinator.LastTurn;
            provider.Answer(0, null);
            await Run.NotCancelled(turn);

            Assert.That(events.Single(e => e.Kind == ConversationEventKind.TurnFailed).Failure, Is.EqualTo(TurnFailure.Malformed));
        }

        [Test]
        public async Task DriverThrowing_IsAProviderError()
        {
            coordinator.Open(Mara);
            coordinator.Submit("hello");
            var turn = coordinator.LastTurn;
            provider.Fail(0, new ProviderException(ProviderErrorKind.Unreachable, provider.Name, "backend down"));
            await Run.NotCancelled(turn);

            Assert.That(events.Single(e => e.Kind == ConversationEventKind.TurnFailed).Failure, Is.EqualTo(TurnFailure.ProviderError));
            Assert.That(coordinator.Submit("retry"), Is.True);
        }

        // The statement may describe the very action the re-check refused, so it is never shown.
        [Test]
        public async Task RefusedExecution_ShowsTheGameLineNotTheStatement()
        {
            // An action the game's own handler refuses, as the executor refuses a stale quote.
            driver = Driver((npc, agent) => agent.Actions.Register(
                new ActionDefinition { Id = "give_stock", Description = "Give the traveler your stock." },
                DelegateActionHandler.WithResult(ctx => true, (ctx, decision) => ActionResult.Failed("The stock is not yours to give."))));
            coordinator = new ConversationCoordinator(driver);
            coordinator.Changed += events.Add;
            coordinator.Open(Mara);
            coordinator.Submit("give me your stock");
            var t = coordinator.LastTurn;
            provider.Act(0, "give_stock", "Here, take everything.");
            await Run.NotCancelled(t);

            Assert.That(events.Single(e => e.Kind == ConversationEventKind.TurnFailed).Failure, Is.EqualTo(TurnFailure.Refused));
            Assert.That(coordinator.HistoryFor(Mara.Id).Any(l => l.Text.Contains("take everything")), Is.False);
        }

        [Test]
        public async Task DriverHistory_ExcludesTheCurrentLine_AndIsCapped()
        {
            coordinator = new ConversationCoordinator(driver, new ConversationSettings { MaxHistoryForDriver = 3 });
            coordinator.Open(Mara);
            for (int i = 0; i < 3; i++)
            {
                coordinator.Submit($"line {i}");
                var t = coordinator.LastTurn;
                provider.Say(i, $"reply {i}");
                await Run.NotCancelled(t);
            }

            var last = turns[2];
            Assert.That(last.PlayerLine, Is.EqualTo("line 2"));
            Assert.That(Lines(last.History), Is.EqualTo(new[] { "Npc: reply 0", "Player: line 1", "Npc: reply 1" }));
        }

        [Test]
        public void OpeningTheSameNpc_DoesNotEndItsTurn()
        {
            coordinator.Open(Mara);
            coordinator.Submit("hello");
            long generation = coordinator.Generation;

            coordinator.Open(Mara);

            Assert.That(coordinator.IsAwaitingReply, Is.True);
            Assert.That(coordinator.Generation, Is.EqualTo(generation));
        }

        // A view that cancels and retries from inside the Waiting event must not hand the first
        // turn the retry's cancellation source (found in review): the retry keeps its own timeout.
        [Test]
        public async Task SubscriberCancellingAndRetrying_RetryKeepsItsOwnTimeout()
        {
            provider.IgnoresCancellation = true;
            coordinator = new ConversationCoordinator(driver, new ConversationSettings { TurnTimeout = TimeSpan.FromMilliseconds(50) });
            coordinator.Changed += events.Add;
            bool retried = false;
            coordinator.Changed += e =>
            {
                if (e.Kind == ConversationEventKind.Waiting && !retried)
                {
                    retried = true;
                    coordinator.CancelPending();
                    coordinator.Submit("retry");
                }
            };
            coordinator.Open(Mara);

            Assert.That(coordinator.Submit("first"), Is.True);
            Assert.That(turns.Select(t => t.PlayerLine), Is.EqualTo(new[] { "retry" }),
                "the abandoned first turn never reaches the backend");

            await UntilTimedOut();
            Assert.That(events.Count(e => e.Kind == ConversationEventKind.TurnFailed && e.Failure == TurnFailure.Timeout), Is.EqualTo(1));
            Assert.That(coordinator.IsAwaitingReply, Is.False, "the retry timed out on its own deadline");

            var retryTurn = coordinator.LastTurn;
            provider.Say(0, "late");
            await Run.NotCancelled(retryTurn);
            Assert.That(driver.ExecutedCount, Is.Zero);
        }

        // After a load the NPCs remember only up to the save, so the shown history must go too.
        [Test]
        public async Task ClearHistories_ForgetsEveryNpcsLines_OnlyWhenClosed()
        {
            coordinator.Open(Mara);
            coordinator.Submit("hello");
            var t = coordinator.LastTurn;
            provider.Say(0, "Morning.");
            await Run.NotCancelled(t);

            Assert.Throws<InvalidOperationException>(() => coordinator.ClearHistories());
            Assert.That(coordinator.HistoryFor(Mara.Id), Is.Not.Empty);

            coordinator.Close(ConversationCloseReason.Interrupted);
            coordinator.ClearHistories();

            Assert.That(coordinator.HistoryFor(Mara.Id), Is.Empty);
            coordinator.Open(Mara);
            Assert.That(coordinator.Submit("hello again"), Is.True);
        }

        [Test]
        public async Task ThrowingSubscriber_DoesNotWedgeTheConversation()
        {
            coordinator.Changed += e =>
            {
                if (e.Kind == ConversationEventKind.NpcReplied)
                {
                    throw new InvalidOperationException("broken view");
                }
            };
            coordinator.Open(Mara);
            coordinator.Submit("hello");
            var t = coordinator.LastTurn;

            LogAssert.Expect(LogType.Exception, new System.Text.RegularExpressions.Regex("broken view"));
            provider.Say(0, "Morning.");
            await Run.NotCancelled(t);

            Assert.That(coordinator.IsAwaitingReply, Is.False);
            Assert.That(coordinator.Submit("again"), Is.True);
        }
    }
}

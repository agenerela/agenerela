using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Agenerela;
using LivingVale.Agenerela;
using LivingVale.Game;
using LivingVale.Interaction;
using NUnit.Framework;

namespace LivingVale.Tests
{
    /// <summary>
    /// G4's <c>offer_quest</c>: Iven offers the main quest through the shared executor, from either
    /// driver, and only the card's Accept moves it to Active.
    /// </summary>
    public sealed class QuestOfferTests
    {
        private static readonly NpcDescriptor Iven = new NpcDescriptor(QuestLog.IvenId, "Iven", "Healer");
        private static readonly NpcDescriptor Mara = new NpcDescriptor(VendorStock.MaraId, "Mara", "General trader");

        private PlayerInventory inventory;
        private TradeService trade;
        private QuestLog quests;
        private NpcActionExecutor executor;
        private ConversationTradeLink link;
        private ConversationCoordinator coordinator;
        private ScriptedNpcDecisionDriver scripted;

        [SetUp]
        public void SetUp()
        {
            var catalog = ItemCatalog.CreateDefault();
            inventory = PlayerInventory.CreateStarting(catalog);
            link = new ConversationTradeLink();
            trade = new TradeService(inventory, new[] { VendorStock.CreateMara(catalog) }, link);
            quests = new QuestLog(inventory, new Progression(), link);
            executor = new NpcActionExecutor(trade, quests);
            scripted = new ScriptedNpcDecisionDriver(seed: 1, executor: executor) { NormalDelay = TimeSpan.FromMilliseconds(5) };
            coordinator = new ConversationCoordinator(scripted, new ConversationSettings { TurnTimeout = TimeSpan.FromSeconds(2) });
            link.Attach(coordinator, trade);
            link.AttachQuests(quests);
        }

        [TearDown]
        public void TearDown() => link.Dispose();

        private async Task Say(string line)
        {
            Assert.That(coordinator.Submit(line), Is.True);
            await Run.NotCancelled(coordinator.LastTurn);
        }

        private static bool Lists(NpcActionExecutor source, string npcId, string actionId, string targetId) =>
            source.AvailableFor(npcId).Any(o => o.ActionId == actionId && o.Targets.Any(t => t.Id == targetId));

        [Test]
        public void OfferQuestIsListedForIvenOnlyWhileUndiscoveredOrOffered()
        {
            Assert.IsTrue(Lists(executor, QuestLog.IvenId, GameActions.OfferQuest, QuestLog.IvenOfferId));
            Assert.IsFalse(executor.AvailableFor(VendorStock.MaraId).Any(o => o.ActionId == GameActions.OfferQuest));

            coordinator.Open(Iven);
            Assert.IsTrue(executor.Execute(QuestLog.IvenId, GameActions.OfferQuest, QuestLog.IvenOfferId, coordinator.Generation).Executed);
            Assert.AreEqual(MainQuestState.Offered, quests.MainState);
            Assert.IsTrue(Lists(executor, QuestLog.IvenId, GameActions.OfferQuest, QuestLog.IvenOfferId), "Still offerable while Offered.");

            Assert.IsTrue(quests.AcceptOffer(QuestLog.IvenOfferId).Applied);
            Assert.IsFalse(executor.AvailableFor(QuestLog.IvenId).Any(o => o.ActionId == GameActions.OfferQuest));
            var refused = executor.Execute(QuestLog.IvenId, GameActions.OfferQuest, QuestLog.IvenOfferId, coordinator.Generation);
            Assert.IsFalse(refused.Executed);
            Assert.AreEqual(QuestRejection.WrongState, refused.QuestReason);
        }

        [Test]
        public void AnExecutorWithoutAQuestLogKeepsG3Behaviour()
        {
            var tradeOnly = new NpcActionExecutor(trade);
            Assert.IsEmpty(tradeOnly.AvailableFor(QuestLog.IvenId));
            Assert.IsFalse(tradeOnly.Execute(QuestLog.IvenId, GameActions.OfferQuest, QuestLog.IvenOfferId, 0).Executed);
        }

        [Test]
        public void OfferingFromAnotherConversationIsRefused()
        {
            coordinator.Open(Iven);
            var outcome = executor.Execute(QuestLog.IvenId, GameActions.OfferQuest, QuestLog.IvenOfferId, coordinator.Generation - 1);
            Assert.IsFalse(outcome.Executed);
            Assert.AreEqual(QuestRejection.StaleConversation, outcome.QuestReason);
            Assert.AreEqual(MainQuestState.Undiscovered, quests.MainState);
        }

        [Test]
        public async Task ScriptedIvenOffersOnAWagonLineAndTheCardAccepts()
        {
            coordinator.Open(Iven);
            await Say("What happened to the wagon?");
            Assert.AreEqual(GameActions.OfferQuest, scripted.LastOutcome?.ActionId, string.Join("\n", coordinator.HistoryFor(Iven.Id)));
            Assert.IsTrue(scripted.LastOutcome.Executed);
            Assert.AreEqual(MainQuestState.Offered, quests.MainState, "Offered, not accepted: the model never accepts.");
            Assert.IsNotNull(quests.ActiveOffer);
            Assert.AreEqual(coordinator.Generation, quests.ActiveOffer.ConversationGeneration);

            var receipt = quests.AcceptOffer(QuestLog.IvenOfferId);
            Assert.IsTrue(receipt.Applied);
            Assert.AreEqual(MainQuestState.Active, quests.MainState);
            Assert.AreEqual(ChatSpeaker.System, coordinator.HistoryFor(Iven.Id).Last().Speaker, "The receipt is in the chat.");

            await Say("Any news of the medicine?");
            Assert.AreEqual(GameActions.None, scripted.LastOutcome.ActionId, "Accepted: nothing left to offer.");
            Assert.AreEqual(MainQuestState.Active, quests.MainState);
        }

        [Test]
        public async Task ClosingTheConversationTakesTheCardAway()
        {
            coordinator.Open(Iven);
            await Say("Can I help with the medicine?");
            Assert.IsNotNull(quests.ActiveOffer);
            coordinator.Close(ConversationCloseReason.PlayerClosed);
            Assert.IsNull(quests.ActiveOffer);
            Assert.IsFalse(quests.AcceptOffer(QuestLog.IvenOfferId).Applied);
            Assert.AreEqual(MainQuestState.Offered, quests.MainState);
        }

        [Test]
        public async Task MaraDoesNotOfferTheQuest()
        {
            coordinator.Open(Mara);
            await Say("Any news about the wagon?");
            Assert.AreNotEqual(GameActions.OfferQuest, scripted.LastOutcome?.ActionId);
            Assert.AreEqual(MainQuestState.Undiscovered, quests.MainState);
        }

        // ---- The Agenerela driver, with a scripted model ----

        private FakeLlmProvider provider;
        private AgenerelaNpcDecisionDriver model;
        private long nextId;

        private void UseModel()
        {
            provider = new FakeLlmProvider();
            model = new AgenerelaNpcDecisionDriver(provider, "fake", executor: executor);
            coordinator.Open(Iven);
        }

        private NpcTurn Turn(string line) => new NpcTurn(++nextId, Iven, line, Array.Empty<ChatLine>(), coordinator.Generation);

        private async Task<NpcReply> Decide(NpcTurn turn)
        {
            var decide = model.DecideAsync(turn, CancellationToken.None);
            await Run.NotCancelled(decide);
            return decide.Result;
        }

        private static string[] Allowed(NpcReply reply, string field) =>
            ((DecisionResult)reply.DriverState).Request.Schema.Fields.First(f => f.Name == field).AllowedValues.ToArray();

        [Test]
        public async Task TheModelDriverRegistersOfferQuestAndDescribesItInObservations()
        {
            UseModel();
            provider.Reply = FakeLlmProvider.Json(GameActions.OfferQuest, QuestLog.IvenOfferId, "Will you find the wagon?");
            var turn = Turn("Is there something I can do?");
            var reply = await Decide(turn);

            CollectionAssert.Contains(Allowed(reply, DecisionSchema.ActionFieldName), GameActions.OfferQuest);
            CollectionAssert.Contains(Allowed(reply, DecisionSchema.TargetFieldName), QuestLog.IvenOfferId);
            CollectionAssert.DoesNotContain(Allowed(reply, DecisionSchema.ActionFieldName), GameActions.QuotePurchase, "Iven trades nothing.");
            var observations = provider.LastRequest.Observations;
            StringAssert.Contains("Target for offer_quest: offer_iven_missing_wagon", observations,
                "A local model never reads the schema, so the target is in the prompt.");

            Assert.IsTrue(model.Execute(turn, reply));
            Assert.AreEqual(GameActions.OfferQuest, model.LastOutcome.ActionId);
            Assert.AreEqual(MainQuestState.Offered, quests.MainState);
            Assert.IsNotNull(quests.ActiveOffer);
            Assert.IsTrue(quests.AcceptOffer(QuestLog.IvenOfferId).Applied);
            Assert.AreEqual(MainQuestState.Active, quests.MainState);
        }

        [Test]
        public async Task AfterAcceptingTheModelCannotOfferAgainAndIsTold()
        {
            UseModel();
            Assert.AreEqual(QuestRejection.None, quests.TryOffer(QuestLog.IvenId, QuestLog.IvenOfferId, coordinator.Generation, out _));
            quests.AcceptOffer(QuestLog.IvenOfferId);

            provider.Reply = FakeLlmProvider.Json(GameActions.OfferQuest, QuestLog.IvenOfferId, "Will you find the wagon?");
            var turn = Turn("Anything else?");
            var reply = await Decide(turn);
            CollectionAssert.DoesNotContain(Allowed(reply, DecisionSchema.ActionFieldName), GameActions.OfferQuest);
            StringAssert.Contains("agreed to look for the medicine wagon", provider.LastRequest.Observations);
            Assert.IsFalse(model.Execute(turn, reply) && model.LastOutcome?.ActionId == GameActions.OfferQuest);
            Assert.AreEqual(MainQuestState.Active, quests.MainState);
        }

        [Test]
        public async Task AnOfferNamingATradeTargetBecomesNone()
        {
            UseModel();
            provider.Reply = FakeLlmProvider.Json(GameActions.OfferQuest, "stock_mara_potion_one", "Here.");
            var reply = await Decide(Turn("Help?"));
            Assert.AreEqual(ActionRegistry.None, reply.Decision.ActionId);
            Assert.AreEqual(MainQuestState.Undiscovered, quests.MainState);
        }
    }
}

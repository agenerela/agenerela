using System;
using System.IO;
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
    /// G4c: the main quest's three recovery routes, its three dispositions, both side quests and the
    /// world pickups, through the shared executor from both drivers. Only the card's button commits,
    /// a claim in chat never records a clue, and nothing pays twice, across a save and load included.
    /// </summary>
    public sealed class QuestRouteTests
    {
        private static readonly NpcDescriptor Iven = new NpcDescriptor(QuestLog.IvenId, "Iven", "Healer");
        private static readonly NpcDescriptor Fen = new NpcDescriptor(QuestRoutes.FenId, "Fen", "Farm worker");
        private static readonly NpcDescriptor Kest = new NpcDescriptor(QuestRoutes.KestId, "Kest", "Camp leader");
        private static readonly NpcDescriptor Brann = new NpcDescriptor(QuestRoutes.BrannId, "Brann", "Gate guard");
        private static readonly NpcDescriptor Elra = new NpcDescriptor(QuestRoutes.ElraId, "Elra", "Castle steward");

        private PlayerInventory inventory;
        private Progression progression;
        private TradeService trade;
        private QuestLog quests;
        private QuestRoutes routes;
        private NpcActionExecutor executor;
        private ConversationTradeLink link;
        private ConversationCoordinator coordinator;
        private ScriptedNpcDecisionDriver scripted;
        private AgenerelaNpcDecisionDriver npcs;

        // The action the agent last carried out: a none runs no handler, so it leaves no outcome.
        private string LastAction => npcs.LastOutcome?.ActionId ?? GameActions.None;
        private string directory;

        [SetUp]
        public void SetUp()
        {
            var catalog = ItemCatalog.CreateDefault();
            inventory = PlayerInventory.CreateStarting(catalog);
            progression = new Progression();
            link = new ConversationTradeLink();
            trade = new TradeService(inventory, new[] { VendorStock.CreateMara(catalog) }, link);
            quests = new QuestLog(inventory, progression, link);
            routes = new QuestRoutes(quests, link);
            executor = new NpcActionExecutor(trade, quests, routes);
            scripted = new ScriptedNpcDecisionDriver(seed: 1, executor: executor) { NormalDelay = TimeSpan.FromMilliseconds(5) };
            npcs = new ScriptedReplies(scripted).Driver(executor);
            coordinator = new ConversationCoordinator(npcs, new ConversationSettings { TurnTimeout = TimeSpan.FromSeconds(2) });
            link.Attach(coordinator, trade);
            link.AttachQuests(quests);
            link.AttachRoutes(routes);
            directory = Path.Combine(Path.GetTempPath(), "LivingValeRouteTests_" + Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            link.Dispose();
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }

        // ---- Helpers ----

        private async Task Say(NpcDescriptor npc, string line)
        {
            if (coordinator.ActiveNpc?.Id != npc.Id)
            {
                coordinator.Open(npc);
            }

            Assert.That(coordinator.Submit(line), Is.True);
            await Run.NotCancelled(coordinator.LastTurn);
        }

        private string History(NpcDescriptor npc) => string.Join("\n", coordinator.HistoryFor(npc.Id).Select(l => $"{l.Speaker}: {l.Text}"));

        private bool Lists(string npcId, string actionId, string targetId) =>
            executor.AvailableFor(npcId).Any(o => o.ActionId == actionId && o.Targets.Any(t => t.Id == targetId));

        /// <summary>Asserts the NPC put <paramref name="contractId"/> on the card, then presses its button.</summary>
        private QuestReceipt ConfirmCard(string contractId)
        {
            var proposal = routes.ActiveProposal;
            Assert.IsNotNull(proposal, $"No card for {contractId}.");
            Assert.AreEqual(contractId, proposal.Contract.Id);
            return routes.Confirm(proposal.Id);
        }

        private void AcceptMainQuest()
        {
            coordinator.Open(Iven);
            Assert.AreEqual(QuestRejection.None, quests.TryOffer(QuestLog.IvenId, QuestLog.IvenOfferId, coordinator.Generation, out _));
            Assert.IsTrue(quests.AcceptOffer(QuestLog.IvenOfferId).Applied);
            coordinator.Close(ConversationCloseReason.PlayerClosed);
        }

        private void ClearCamp()
        {
            foreach (var id in QuestRoutes.DefenderIds)
            {
                routes.ReportDefeated(id);
            }
        }

        private async Task RecoverByCombat()
        {
            ClearCamp();
            Assert.IsTrue(routes.TryPickUp(QuestRoutes.PickupParcel).Applied);
            await Task.CompletedTask;
        }

        private async Task GetPermit()
        {
            await Say(Brann, "I need a permit to deliver to the steward.");
            Assert.IsTrue(routes.HasPermit, History(Brann));
        }

        // ---- Investigation, and "saying it does not make it so" ----

        [Test]
        public async Task ClaimingProofUnlocksNothingUntilFenTellsIt()
        {
            AcceptMainQuest();
            await Say(Kest, "I have proof, Fen saw you take the wagon.");
            Assert.AreEqual(GameActions.None, LastAction, History(Kest));
            Assert.IsNull(routes.ActiveProposal);
            Assert.IsFalse(quests.HasClue(QuestLog.ClueFenTestimony), "A claim in chat records nothing.");
            Assert.IsFalse(Lists(QuestRoutes.KestId, GameActions.OfferExchange, QuestRoutes.ExchangeKestTestimony));
            StringAssert.Contains("witness", History(Kest));

            await Say(Fen, "What did you see on the road?");
            Assert.AreEqual(GameActions.ShareInformation, LastAction, History(Fen));
            Assert.IsTrue(quests.HasClue(QuestLog.ClueFenTestimony));
            Assert.IsTrue(quests.HasClue(QuestLog.ClueCampLocated));
            StringAssert.Contains("Clue recorded", History(Fen), "The receipt is in the chat.");
            Assert.IsEmpty(routes.KnowledgeFor(QuestRoutes.FenId), "Told once; nothing left to tell.");

            await Say(Kest, "Fen saw you. She'll tell the castle.");
            Assert.AreEqual(GameActions.OfferExchange, LastAction, History(Kest));
            Assert.AreEqual(MainQuestState.Active, quests.MainState, "On the card, not yet carried out.");
            Assert.IsTrue(ConfirmCard(QuestRoutes.ExchangeKestTestimony).Applied);
            Assert.AreEqual(MainQuestState.ParcelRecovered, quests.MainState);
            Assert.AreEqual(RecoveryRoute.Investigation, quests.Route);
            Assert.AreEqual(PlayerInventory.StartingGold, inventory.Gold, "Fen's account costs nothing.");
        }

        [Test]
        public void ShareInformationFromAnotherConversationIsRefused()
        {
            coordinator.Open(Fen);
            var outcome = executor.Execute(QuestRoutes.FenId, GameActions.ShareInformation, QuestRoutes.KnowledgeFenRaid, coordinator.Generation - 1);
            Assert.IsFalse(outcome.Executed);
            Assert.AreEqual(QuestRejection.StaleConversation, outcome.QuestReason);
            var other = executor.Execute(QuestRoutes.KestId, GameActions.ShareInformation, QuestRoutes.KnowledgeFenRaid, coordinator.Generation);
            Assert.IsFalse(other.Executed, "Only Fen holds Fen's account.");
            Assert.IsFalse(quests.HasClue(QuestLog.ClueFenTestimony));
        }

        // ---- Negotiation: payment and herb favour ----

        [Test]
        public async Task KestReleasesTheParcelForTheFixedPayment()
        {
            AcceptMainQuest();
            await Say(Kest, "How much do you want for the parcel?");
            Assert.AreEqual(GameActions.OfferExchange, LastAction, History(Kest));
            Assert.AreEqual(PlayerInventory.StartingGold, inventory.Gold, "The card alone moves no gold.");
            Assert.IsTrue(ConfirmCard(QuestRoutes.ExchangeKestPayment).Applied);
            Assert.AreEqual(PlayerInventory.StartingGold - QuestRoutes.KestPayment, inventory.Gold);
            Assert.AreEqual(RecoveryRoute.Negotiation, quests.Route);
            Assert.AreEqual(1, inventory.QuestItems[ItemCatalog.MedicalParcel]);
        }

        [Test]
        public async Task KestsPaymentWithoutTheGoldChangesNothing()
        {
            AcceptMainQuest();
            Assert.IsTrue(inventory.TryApply(-(PlayerInventory.StartingGold - 5), Array.Empty<ItemChange>(), out _));
            await Say(Kest, "I'll pay for it.");
            var receipt = ConfirmCard(QuestRoutes.ExchangeKestPayment);
            Assert.IsFalse(receipt.Applied);
            Assert.AreEqual(QuestRejection.NotEnoughGold, receipt.Reason);
            Assert.AreEqual(5, inventory.Gold);
            Assert.AreEqual(MainQuestState.Active, quests.MainState);
            Assert.IsFalse(inventory.HasApplied(QuestLog.ParcelEvent), "A refused step does not use up its event ID.");
        }

        [Test]
        public async Task HerbsQuestEndToEndThenKestHonoursTheFavour()
        {
            AcceptMainQuest();
            await Say(Kest, "Iven's herbs, does that count for something?");
            Assert.AreEqual(GameActions.None, LastAction, "No favour done yet.");

            await Say(Iven, "Is there anything else I can do? Herbs, maybe?");
            Assert.AreEqual(GameActions.OfferQuest, LastAction, History(Iven));
            Assert.AreEqual(SideQuestState.Undiscovered, quests.HerbsState, "Offered, not accepted.");
            Assert.IsTrue(ConfirmCard(QuestRoutes.OfferHerbsIven).Applied);
            Assert.AreEqual(SideQuestState.Active, quests.HerbsState);

            await Say(Iven, "Here are the herbs.");
            Assert.AreNotEqual(GameActions.ResolveDelivery, LastAction, "Not three bundles yet.");

            foreach (var id in new[] { QuestRoutes.PickupHerb1, QuestRoutes.PickupHerb2, QuestRoutes.PickupHerb3 })
            {
                Assert.IsTrue(routes.TryPickUp(id).Applied);
                Assert.IsFalse(routes.TryPickUp(id).Applied, "A herb spot gives once.");
            }

            Assert.AreEqual(3, inventory.CountOf(ItemCatalog.HerbBundle));
            StringAssert.Contains("herbs 3/3", string.Join("\n", routes.JournalLines()));
            await Say(Iven, "Here are the herbs you asked for.");
            Assert.AreEqual(GameActions.ResolveDelivery, LastAction, History(Iven));
            Assert.IsTrue(ConfirmCard(QuestRoutes.DeliveryHerbsIven).Applied);
            Assert.AreEqual(SideQuestState.Completed, quests.HerbsState);
            Assert.AreEqual(0, inventory.CountOf(ItemCatalog.HerbBundle));
            Assert.AreEqual(QuestLog.SideQuestXp, progression.Xp);

            await Say(Kest, "I gathered herbs for Iven's sick. Will you release it?");
            Assert.IsTrue(ConfirmCard(QuestRoutes.ExchangeKestFavour).Applied, History(Kest));
            Assert.AreEqual(RecoveryRoute.Negotiation, quests.Route);
            Assert.AreEqual(PlayerInventory.StartingGold, inventory.Gold);
        }

        // ---- Combat ----

        [Test]
        public void TheParcelIsLockedUntilEveryDefenderIsDown()
        {
            AcceptMainQuest();
            var locked = routes.TryPickUp(QuestRoutes.PickupParcel);
            Assert.IsFalse(locked.Applied);
            Assert.AreEqual(QuestRejection.WrongState, locked.Reason);

            routes.ReportDefeated("enemy_raider_1");
            routes.ReportDefeated(QuestRoutes.DefenderIds[0]);
            routes.ReportDefeated(QuestRoutes.DefenderIds[0]);
            routes.ReportDefeated(QuestRoutes.DefenderIds[1]);
            Assert.IsFalse(routes.CampCleared, "The meadow raider and two of three defenders are not the camp.");
            Assert.IsFalse(routes.CanUse(QuestRoutes.PickupParcel));

            routes.ReportDefeated(QuestRoutes.DefenderIds[2]);
            Assert.IsTrue(routes.CampCleared);
            Assert.IsTrue(routes.TryPickUp(QuestRoutes.PickupParcel).Applied);
            Assert.AreEqual(RecoveryRoute.Combat, quests.Route);
            Assert.IsTrue(routes.IsDone(QuestRoutes.PickupParcel));
            Assert.IsFalse(routes.TryPickUp(QuestRoutes.PickupParcel).Applied);
            Assert.IsEmpty(routes.ContractsFor(QuestRoutes.KestId, GameActions.OfferExchange), "Kest has nothing left to release.");
        }

        // ---- The three dispositions ----

        [Test]
        public async Task HealerEndingThroughIvensCard()
        {
            AcceptMainQuest();
            await RecoverByCombat();
            await Say(Iven, "Here is the medicine.");
            Assert.AreEqual(GameActions.ResolveDelivery, LastAction, History(Iven));
            Assert.AreEqual(MainQuestState.ParcelRecovered, quests.MainState, "On the card, not yet delivered.");
            Assert.IsTrue(ConfirmCard(QuestRoutes.DeliveryParcelIven).Applied);
            Assert.AreEqual(ParcelDisposition.Healer, quests.Disposition);
            Assert.AreEqual(QuestLog.MainQuestXp, progression.Xp);
            Assert.AreEqual(PlayerInventory.StartingGold + QuestLog.HealerGold, inventory.Gold);
        }

        [Test]
        public async Task GarrisonEndingNeedsBrannsPermit()
        {
            AcceptMainQuest();
            await Say(Brann, "Give me a permit to see the steward.");
            Assert.IsFalse(routes.HasPermit, "No parcel, no permit.");

            await RecoverByCombat();
            await Say(Elra, "Here is the parcel for the garrison.");
            Assert.AreEqual(GameActions.None, LastAction, "No permit yet.");
            StringAssert.Contains("permit", History(Elra));
            Assert.IsFalse(Lists(QuestRoutes.ElraId, GameActions.ResolveDelivery, QuestRoutes.DeliveryParcelElra));

            await GetPermit();
            Assert.IsEmpty(routes.GrantsFor(QuestRoutes.BrannId), "Granted once.");
            await Say(Elra, "Here is the parcel for the garrison.");
            Assert.IsTrue(ConfirmCard(QuestRoutes.DeliveryParcelElra).Applied, History(Elra));
            Assert.AreEqual(ParcelDisposition.Garrison, quests.Disposition);
            Assert.AreEqual(PlayerInventory.StartingGold + QuestLog.GarrisonGold, inventory.Gold);
            Assert.AreEqual(QuestLog.MainQuestXp, progression.Xp);
        }

        [Test]
        public async Task DivisionNeedsBothRecordedAgreements()
        {
            AcceptMainQuest();
            await RecoverByCombat();
            Assert.IsFalse(Lists(QuestLog.IvenId, GameActions.ResolveDelivery, QuestRoutes.DeliveryDivisionIven));

            await Say(Iven, "Could we split the medicine with the garrison?");
            Assert.AreEqual(GameActions.OfferExchange, LastAction, History(Iven));
            Assert.IsFalse(quests.HasClue(QuestLog.AgreementHealer), "Proposed, not agreed.");
            Assert.IsTrue(ConfirmCard(QuestRoutes.ExchangeDivisionIven).Applied);
            Assert.IsTrue(quests.HasClue(QuestLog.AgreementHealer));
            Assert.IsFalse(Lists(QuestLog.IvenId, GameActions.ResolveDelivery, QuestRoutes.DeliveryDivisionIven), "One agreement is not enough.");
            Assert.AreEqual(QuestRejection.NoAgreement, quests.TryResolve(ParcelDisposition.Division).Reason);

            await GetPermit();
            await Say(Elra, "Would the garrison agree to divide it with Iven?");
            Assert.IsTrue(ConfirmCard(QuestRoutes.ExchangeDivisionElra).Applied, History(Elra));
            await Say(Iven, "Here is your share, as agreed.");
            Assert.AreEqual(GameActions.ResolveDelivery, LastAction, History(Iven));
            Assert.IsTrue(ConfirmCard(QuestRoutes.DeliveryDivisionIven).Applied);
            Assert.AreEqual(ParcelDisposition.Division, quests.Disposition);
            Assert.AreEqual(PlayerInventory.StartingGold + QuestLog.DivisionGold, inventory.Gold);
        }

        // ---- Cards ----

        [Test]
        public async Task OnlyTheCardCommitsAndAClosedConversationTakesItAway()
        {
            AcceptMainQuest();
            await Say(Kest, "What's your price for the parcel?");
            var proposal = routes.ActiveProposal;
            Assert.IsNotNull(proposal);
            Assert.IsTrue(routes.Decline(proposal.Id));
            Assert.IsFalse(routes.Confirm(proposal.Id).Applied, "Declined.");

            await Say(Kest, "What's your price for the parcel?");
            proposal = routes.ActiveProposal;
            coordinator.Close(ConversationCloseReason.PlayerClosed);
            Assert.IsNull(routes.ActiveProposal);
            coordinator.Open(Kest);
            var stale = routes.Confirm(proposal.Id);
            Assert.IsFalse(stale.Applied);
            Assert.AreEqual(MainQuestState.Active, quests.MainState);
            Assert.AreEqual(PlayerInventory.StartingGold, inventory.Gold);
        }

        [Test]
        public async Task ACardWhoseConditionsLapsedIsRefused()
        {
            AcceptMainQuest();
            await Say(Kest, "How much for the parcel?");
            var proposal = routes.ActiveProposal;
            Assert.IsNotNull(proposal);
            ClearCamp();
            Assert.IsTrue(routes.TryPickUp(QuestRoutes.PickupParcel).Applied, "Taken by force while the card was open.");
            var receipt = routes.Confirm(proposal.Id);
            Assert.IsFalse(receipt.Applied);
            Assert.AreEqual(PlayerInventory.StartingGold, inventory.Gold, "Never charged for a parcel already taken.");
        }

        // ---- Badge ----

        [Test]
        public async Task BadgeOfferedFoundAndReturned()
        {
            await Say(Brann, "You look troubled. Lost something?");
            Assert.IsTrue(ConfirmCard(QuestRoutes.OfferBadgeBrann).Applied, History(Brann));
            Assert.AreEqual(SideQuestState.Active, quests.BadgeState);
            StringAssert.Contains("search the Old Road wreck", string.Join("\n", routes.JournalLines()));

            Assert.IsTrue(routes.TryPickUp(QuestRoutes.PickupBadge).Applied);
            Assert.IsTrue(routes.IsDone(QuestRoutes.PickupBadge));
            await Say(Brann, "I found your badge.");
            Assert.IsTrue(ConfirmCard(QuestRoutes.DeliveryBadgeBrann).Applied, History(Brann));
            Assert.AreEqual(SideQuestState.Completed, quests.BadgeState);
            Assert.AreEqual(PlayerInventory.StartingGold + QuestLog.BadgeGold, inventory.Gold);
            Assert.AreEqual(QuestLog.SideQuestXp, progression.Xp);
        }

        [Test]
        public async Task ABadgeFoundBeforeBrannAsksCanStillBeReturned()
        {
            Assert.IsTrue(routes.TryPickUp(QuestRoutes.PickupBadge).Applied);
            Assert.IsFalse(Lists(QuestRoutes.BrannId, GameActions.OfferQuest, QuestRoutes.OfferBadgeBrann), "Nothing to ask for: it is found.");
            await Say(Brann, "Here's a badge, I think it is yours.");
            Assert.IsTrue(ConfirmCard(QuestRoutes.DeliveryBadgeBrann).Applied, History(Brann));
            Assert.AreEqual(SideQuestState.Completed, quests.BadgeState);
        }

        // ---- Wreck, journal and level-up ----

        [Test]
        public void TheWreckRecordsAClueAndItsLandmarkXpOnce()
        {
            Assert.IsTrue(routes.TryPickUp(QuestRoutes.PickupWreck).Applied);
            Assert.IsTrue(quests.HasClue(QuestLog.ClueWreckSeen));
            Assert.AreEqual(QuestRoutes.LandmarkXp, progression.Xp);
            Assert.IsFalse(routes.TryPickUp(QuestRoutes.PickupWreck).Applied);
            Assert.AreEqual(QuestRoutes.LandmarkXp, progression.Xp);
            StringAssert.Contains("wreck searched", string.Join("\n", routes.JournalLines()));
        }

        [Test]
        public async Task TheJournalFollowsTheRouteAndTheMainQuestLevelsThePlayerUp()
        {
            int levelUps = 0;
            progression.LeveledUp += _ => levelUps++;
            AcceptMainQuest();
            routes.TryPickUp(QuestRoutes.PickupWreck);
            await Say(Fen, "Did you see what happened to the wagon?");
            StringAssert.Contains("Fen's account", string.Join("\n", routes.JournalLines()));
            await Say(Kest, "Fen is a witness to your raid.");
            ConfirmCard(QuestRoutes.ExchangeKestTestimony);
            string journal = string.Join("\n", routes.JournalLines());
            StringAssert.Contains("Fen's evidence", journal);
            StringAssert.Contains("permit from Brann", journal);

            await Say(Iven, "I brought the medicine.");
            ConfirmCard(QuestRoutes.DeliveryParcelIven);
            Assert.AreEqual(QuestLog.MainQuestXp + QuestRoutes.LandmarkXp, progression.Xp);
            Assert.AreEqual(2, progression.Level);
            Assert.AreEqual(1, levelUps);
            StringAssert.Contains("resolved (Healer)", string.Join("\n", routes.JournalLines()));
        }

        // ---- Save and load ----

        [Test]
        public async Task NothingPaysTwiceAcrossASaveAndLoad()
        {
            var save = new SaveGame(inventory, trade, quests, progression, directory);
            AcceptMainQuest();
            routes.TryPickUp(QuestRoutes.PickupWreck);
            routes.TryPickUp(QuestRoutes.PickupHerb1);
            ClearCamp();
            routes.TryPickUp(QuestRoutes.PickupParcel);
            await GetPermit();
            Assert.IsTrue(save.TrySave(out var error), error);

            int gold = inventory.Gold, xp = progression.Xp, herbs = inventory.CountOf(ItemCatalog.HerbBundle);
            Assert.IsTrue(save.TryLoad(out error), error);
            Assert.IsTrue(routes.HasPermit, "The permit is saved with the clues.");
            Assert.IsTrue(routes.CampCleared);
            Assert.IsTrue(routes.IsDone(QuestRoutes.PickupHerb1));
            Assert.IsFalse(routes.IsDone(QuestRoutes.PickupHerb2));
            foreach (var id in new[] { QuestRoutes.PickupWreck, QuestRoutes.PickupHerb1, QuestRoutes.PickupParcel })
            {
                Assert.IsFalse(routes.TryPickUp(id).Applied, $"{id} after a load.");
            }

            Assert.AreEqual(gold, inventory.Gold);
            Assert.AreEqual(xp, progression.Xp);
            Assert.AreEqual(herbs, inventory.CountOf(ItemCatalog.HerbBundle));

            await Say(Elra, "Here is the parcel.");
            Assert.IsTrue(ConfirmCard(QuestRoutes.DeliveryParcelElra).Applied, History(Elra));
            Assert.IsTrue(save.TrySave(out error), error);
            Assert.IsTrue(save.TryLoad(out error), error);
            Assert.AreEqual(ParcelDisposition.Garrison, quests.Disposition);
            Assert.IsEmpty(routes.ContractsFor(QuestRoutes.ElraId, GameActions.ResolveDelivery), "Resolved: nothing to deliver after the load.");
            Assert.AreEqual(QuestRejection.WrongState, quests.TryResolve(ParcelDisposition.Garrison).Reason);
            Assert.AreEqual(xp + QuestLog.MainQuestXp, progression.Xp);
        }

        [Test]
        public void LoadingASaveFromBeforeTheFightKeepsTheCombatRouteOpen()
        {
            var save = new SaveGame(inventory, trade, quests, progression, directory);
            AcceptMainQuest();
            Assert.IsTrue(save.TrySave(out var error), error);
            ClearCamp();
            Assert.IsTrue(routes.CampCleared);

            // The defenders stay down in this session, so the older save cannot lock the parcel away.
            Assert.IsTrue(save.TryLoad(out error), error);
            Assert.IsTrue(routes.CampCleared);
            Assert.IsTrue(routes.TryPickUp(QuestRoutes.PickupParcel).Applied);
            Assert.AreEqual(RecoveryRoute.Combat, quests.Route);
        }

        [Test]
        public async Task APriceQuestionNamingIvenStillReachesThePayment()
        {
            AcceptMainQuest();
            await Say(Kest, "How much gold for Iven's parcel?");
            Assert.AreEqual(GameActions.OfferExchange, LastAction, History(Kest));
            Assert.AreEqual(QuestRoutes.ExchangeKestPayment, routes.ActiveProposal?.Contract.Id);
        }

        [Test]
        public async Task LoadingTakesThePendingCardAway()
        {
            var save = new SaveGame(inventory, trade, quests, progression, directory) { Routes = routes };
            AcceptMainQuest();
            Assert.IsTrue(save.TrySave(out var error), error);
            await Say(Kest, "Name your price for the parcel.");
            var proposal = routes.ActiveProposal;
            Assert.IsNotNull(proposal);

            // The conversation stays open on purpose: only the load itself may take the card away
            // (#206 review: closing first hid a load that left the card live).
            Assert.IsTrue(save.TryLoad(out error), error);
            Assert.IsNull(routes.ActiveProposal);
            Assert.IsFalse(routes.Confirm(proposal.Id).Applied);
            Assert.AreEqual(MainQuestState.Active, quests.MainState);
        }

        // ---- The Agenerela driver, with a scripted model ----

        private FakeLlmProvider provider;
        private AgenerelaNpcDecisionDriver model;
        private long nextId;

        private void UseModel(NpcDescriptor npc)
        {
            provider = new FakeLlmProvider();
            model = new AgenerelaNpcDecisionDriver(provider, "fake", executor: executor);
            coordinator.Open(npc);
        }

        private NpcTurn Turn(NpcDescriptor npc, string line) => new NpcTurn(++nextId, npc, line, Array.Empty<ChatLine>(), coordinator.Generation);

        private async Task<(NpcTurn turn, NpcReply reply)> Decide(NpcDescriptor npc, string line, string action, string target)
        {
            provider.Reply = FakeLlmProvider.Json(action, target, "Mm.");
            var turn = Turn(npc, line);
            var decide = model.DecideAsync(turn, CancellationToken.None);
            await Run.NotCancelled(decide);
            return (turn, decide.Result);
        }

        private static string[] Allowed(NpcReply reply, string field) =>
            ((DecisionResult)reply.DriverState).Request.Schema.Fields.First(f => f.Name == field).AllowedValues.ToArray();

        [Test]
        public async Task TheModelCannotUnlockKestsTestimonyByClaimingIt()
        {
            AcceptMainQuest();
            UseModel(Kest);
            var (turn, reply) = await Decide(Kest, "I have proof. Fen saw you.", GameActions.OfferExchange, QuestRoutes.ExchangeKestTestimony);
            CollectionAssert.DoesNotContain(Allowed(reply, DecisionSchema.TargetFieldName), QuestRoutes.ExchangeKestTestimony);
            StringAssert.Contains("No witness account", provider.LastRequest.Observations);
            Assert.AreEqual(ActionRegistry.None, reply.Decision?.ActionId ?? ActionRegistry.None, "An unlisted target never survives.");
            model.Execute(turn, reply);
            Assert.IsNull(routes.ActiveProposal);
            Assert.AreEqual(MainQuestState.Active, quests.MainState);
            Assert.IsFalse(quests.HasClue(QuestLog.ClueFenTestimony));
        }

        [Test]
        public async Task TheModelDriverRunsTheInvestigationRoute()
        {
            AcceptMainQuest();
            UseModel(Fen);
            var (turn, reply) = await Decide(Fen, "What did you see?", GameActions.ShareInformation, QuestRoutes.KnowledgeFenRaid);
            CollectionAssert.Contains(Allowed(reply, DecisionSchema.ActionFieldName), GameActions.ShareInformation);
            StringAssert.Contains($"- {QuestRoutes.KnowledgeFenRaid}", provider.LastRequest.SystemPrompt,
                "A local model never reads the schema, so the framework lists the target in the prompt.");
            Assert.IsTrue(model.Execute(turn, reply));
            Assert.AreEqual(GameActions.ShareInformation, model.LastOutcome.ActionId);
            Assert.IsTrue(quests.HasClue(QuestLog.ClueFenTestimony));

            coordinator.Open(Kest);
            (turn, reply) = await Decide(Kest, "Fen saw you.", GameActions.OfferExchange, QuestRoutes.ExchangeKestTestimony);
            StringAssert.Contains("Fen, really did see your raid", provider.LastRequest.Observations);
            Assert.IsTrue(model.Execute(turn, reply));
            Assert.IsNotNull(model.LastOutcome.Proposal);
            Assert.AreEqual(MainQuestState.Active, quests.MainState, "The card, not the model, hands it over.");
            Assert.IsTrue(ConfirmCard(QuestRoutes.ExchangeKestTestimony).Applied);
            Assert.AreEqual(RecoveryRoute.Investigation, quests.Route);
        }

        [Test]
        public async Task TheModelDriverGrantsThePermitAndDeliversOnTheCard()
        {
            AcceptMainQuest();
            await RecoverByCombat();
            UseModel(Brann);
            var (turn, reply) = await Decide(Brann, "Let me through to the steward.", GameActions.GrantAccess, QuestLog.AccessDeliveryPermit);
            Assert.IsTrue(model.Execute(turn, reply));
            Assert.IsTrue(routes.HasPermit);

            coordinator.Open(Elra);
            (turn, reply) = await Decide(Elra, "This is for the garrison.", GameActions.ResolveDelivery, QuestRoutes.DeliveryParcelElra);
            Assert.IsTrue(model.Execute(turn, reply));
            Assert.AreEqual(MainQuestState.ParcelRecovered, quests.MainState);
            Assert.IsTrue(ConfirmCard(QuestRoutes.DeliveryParcelElra).Applied);
            Assert.AreEqual(ParcelDisposition.Garrison, quests.Disposition);
        }

        [Test]
        public async Task TheModelDriverOffersASideQuestThroughOfferQuest()
        {
            UseModel(Brann);
            var (turn, reply) = await Decide(Brann, "Anything I can do?", GameActions.OfferQuest, QuestRoutes.OfferBadgeBrann);
            CollectionAssert.Contains(Allowed(reply, DecisionSchema.TargetFieldName), QuestRoutes.OfferBadgeBrann);
            Assert.IsTrue(model.Execute(turn, reply));
            Assert.IsTrue(ConfirmCard(QuestRoutes.OfferBadgeBrann).Applied);
            Assert.AreEqual(SideQuestState.Active, quests.BadgeState);
        }

        [Test]
        public async Task ARouteActionNamingAnotherActionsTargetBecomesNone()
        {
            AcceptMainQuest();
            await RecoverByCombat();
            Assert.IsTrue(routes.TryPickUp(QuestRoutes.PickupBadge).Applied);
            UseModel(Brann);
            Assert.IsTrue(Lists(QuestRoutes.BrannId, GameActions.ResolveDelivery, QuestRoutes.DeliveryBadgeBrann), "Both actions are registered,");
            Assert.IsTrue(Lists(QuestRoutes.BrannId, GameActions.GrantAccess, QuestLog.AccessDeliveryPermit), "so only the pair guard stops the mix.");
            var (_, reply) = await Decide(Brann, "Permit?", GameActions.ResolveDelivery, QuestLog.AccessDeliveryPermit);
            Assert.AreEqual(ActionRegistry.None, reply.Decision.ActionId);
            Assert.IsFalse(routes.HasPermit);
        }
    }
}

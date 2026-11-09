using System.Linq;
using LivingVale.Game;
using NUnit.Framework;

namespace LivingVale.Tests
{
    /// <summary>G4's quest log: every legal and illegal transition, and every reward applied once.</summary>
    public sealed class QuestLogTests
    {
        private PlayerInventory inventory;
        private Progression progression;
        private FakeTradeSession session;
        private QuestLog quests;

        [SetUp]
        public void SetUp()
        {
            inventory = PlayerInventory.CreateStarting(ItemCatalog.CreateDefault());
            progression = new Progression();
            session = new FakeTradeSession { ActiveNpcId = QuestLog.IvenId, Generation = 3 };
            quests = new QuestLog(inventory, progression, session);
        }

        private void Offer()
        {
            Assert.AreEqual(QuestRejection.None, quests.TryOffer(QuestLog.IvenId, QuestLog.IvenOfferId, session.Generation, out _));
        }

        private void Accept()
        {
            Offer();
            Assert.IsTrue(quests.AcceptOffer(QuestLog.IvenOfferId).Applied);
        }

        private void Recover(RecoveryRoute route = RecoveryRoute.Combat)
        {
            Assert.IsTrue(quests.TryRecoverParcel(route).Applied);
        }

        private void GiveHerbs(int count) =>
            Assert.IsTrue(inventory.TryApply(0, new[] { new ItemChange(ItemCatalog.HerbBundle, count) }, out _));

        [Test]
        public void TheMainQuestWalksEveryStateInOrder()
        {
            Assert.AreEqual(MainQuestState.Undiscovered, quests.MainState);
            Offer();
            Assert.AreEqual(MainQuestState.Offered, quests.MainState);
            Assert.IsTrue(quests.AcceptOffer(QuestLog.IvenOfferId).Applied);
            Assert.AreEqual(MainQuestState.Active, quests.MainState);
            Recover(RecoveryRoute.Negotiation);
            Assert.AreEqual(MainQuestState.ParcelRecovered, quests.MainState);
            Assert.AreEqual(RecoveryRoute.Negotiation, quests.Route);
            Assert.AreEqual(1, inventory.CountOf(ItemCatalog.MedicalParcel));
            Assert.IsTrue(quests.TryResolve(ParcelDisposition.Healer).Applied);
            Assert.AreEqual(MainQuestState.Resolved, quests.MainState);
            Assert.AreEqual(ParcelDisposition.Healer, quests.Disposition);
            Assert.AreEqual(0, inventory.CountOf(ItemCatalog.MedicalParcel), "The parcel left the player.");
        }

        [Test]
        public void AcceptingNeedsAPendingOfferInTheSameConversation()
        {
            Assert.AreEqual(QuestRejection.UnknownOffer, quests.AcceptOffer("offer_nope").Reason);
            Assert.AreEqual(QuestRejection.OfferNotPending, quests.AcceptOffer(QuestLog.IvenOfferId).Reason, "Nothing was offered yet.");
            Assert.AreEqual(MainQuestState.Undiscovered, quests.MainState);

            Offer();
            session.Generation++;
            Assert.AreEqual(QuestRejection.StaleConversation, quests.AcceptOffer(QuestLog.IvenOfferId).Reason);
            Assert.AreEqual(MainQuestState.Offered, quests.MainState);

            quests.InvalidatePending();
            Assert.IsNull(quests.ActiveOffer);
            Assert.AreEqual(QuestRejection.OfferNotPending, quests.AcceptOffer(QuestLog.IvenOfferId).Reason);
            Assert.AreEqual(MainQuestState.Offered, quests.MainState, "Closing the card keeps the quest offered.");
        }

        [Test]
        public void ADeclinedOfferCanBeOfferedAgainAndThenAccepted()
        {
            Offer();
            Assert.IsTrue(quests.DeclineOffer(QuestLog.IvenOfferId));
            Assert.IsFalse(quests.AcceptOffer(QuestLog.IvenOfferId).Applied);
            Assert.AreEqual(MainQuestState.Offered, quests.MainState);
            Accept();
            Assert.AreEqual(MainQuestState.Active, quests.MainState);
        }

        [Test]
        public void OfferingIsRefusedOutsideItsStatesForOtherNpcsAndOtherConversations()
        {
            Assert.AreEqual(QuestRejection.UnknownOffer, quests.TryOffer(VendorStock.MaraId, QuestLog.IvenOfferId, session.Generation, out _),
                "Only Iven offers it.");
            Assert.AreEqual(QuestRejection.StaleConversation, quests.TryOffer(QuestLog.IvenId, QuestLog.IvenOfferId, session.Generation + 1, out _));
            Assert.AreEqual(MainQuestState.Undiscovered, quests.MainState, "A refused offer changes nothing.");

            Accept();
            Assert.AreEqual(QuestRejection.WrongState, quests.TryOffer(QuestLog.IvenId, QuestLog.IvenOfferId, session.Generation, out _));
            Assert.IsEmpty(quests.OffersFor(QuestLog.IvenId));
            Assert.AreEqual(MainQuestState.Active, quests.MainState);
        }

        [Test]
        public void DiscoverySkipsAheadToRecoveredFromUndiscoveredAndOffered()
        {
            Recover(RecoveryRoute.Combat);
            Assert.AreEqual(MainQuestState.ParcelRecovered, quests.MainState);
            Assert.AreEqual(RecoveryRoute.FoundEarly, quests.Route);

            SetUp();
            Offer();
            Recover(RecoveryRoute.Investigation);
            Assert.AreEqual(MainQuestState.ParcelRecovered, quests.MainState);
            Assert.IsNull(quests.ActiveOffer, "The card for a quest already past its offer goes away.");
            Assert.AreEqual(QuestRejection.OfferNotPending, quests.AcceptOffer(QuestLog.IvenOfferId).Reason);
        }

        [Test]
        public void IllegalMainQuestStepsAreRefusedWithNothingChanged()
        {
            int gold = inventory.Gold;
            Assert.AreEqual(QuestRejection.WrongState, quests.TryResolve(ParcelDisposition.Healer).Reason, "Nothing to deliver yet.");
            Accept();
            Assert.AreEqual(QuestRejection.WrongState, quests.TryResolve(ParcelDisposition.Garrison).Reason);
            Assert.AreEqual(QuestRejection.WrongState, quests.TryRecoverParcel(RecoveryRoute.None).Reason);
            Recover();
            Assert.AreEqual(QuestRejection.WrongState, quests.TryRecoverParcel(RecoveryRoute.Combat).Reason, "Recovered once only.");
            Assert.AreEqual(QuestRejection.WrongState, quests.TryResolve(ParcelDisposition.None).Reason);
            Assert.AreEqual(QuestRejection.WrongState, quests.TryResolve((ParcelDisposition)42).Reason);
            Assert.AreEqual(1, inventory.CountOf(ItemCatalog.MedicalParcel));
            Assert.IsTrue(quests.TryResolve(ParcelDisposition.Garrison).Applied);
            Assert.AreEqual(QuestRejection.WrongState, quests.TryResolve(ParcelDisposition.Healer).Reason);
            Assert.AreEqual(QuestRejection.WrongState, quests.TryRecoverParcel(RecoveryRoute.Combat).Reason);
            Assert.AreEqual(gold + QuestLog.GarrisonGold, inventory.Gold, "Paid exactly once.");
            Assert.AreEqual(QuestLog.MainQuestXp, progression.Xp);
        }

        [Test]
        public void TheDivisionNeedsBothRecordedAgreements()
        {
            Accept();
            Recover();
            Assert.AreEqual(QuestRejection.NoAgreement, quests.TryResolve(ParcelDisposition.Division).Reason);
            quests.RecordClue(QuestLog.AgreementHealer);
            Assert.AreEqual(QuestRejection.NoAgreement, quests.TryResolve(ParcelDisposition.Division).Reason);
            quests.RecordClue(QuestLog.AgreementGarrison);
            int potions = inventory.CountOf(ItemCatalog.HealingPotion);
            Assert.IsTrue(quests.TryResolve(ParcelDisposition.Division).Applied);
            Assert.AreEqual(potions + QuestLog.DivisionPotions, inventory.CountOf(ItemCatalog.HealingPotion));
        }

        [Test]
        public void EachDispositionPaysItsAuthoredRewardAnd120Xp()
        {
            foreach (var (disposition, gold, potions) in new[]
            {
                (ParcelDisposition.Healer, QuestLog.HealerGold, QuestLog.HealerPotions),
                (ParcelDisposition.Garrison, QuestLog.GarrisonGold, 0),
                (ParcelDisposition.Division, QuestLog.DivisionGold, QuestLog.DivisionPotions)
            })
            {
                SetUp();
                Accept();
                Recover();
                quests.RecordClue(QuestLog.AgreementHealer);
                quests.RecordClue(QuestLog.AgreementGarrison);
                int goldBefore = inventory.Gold;
                int potionsBefore = inventory.CountOf(ItemCatalog.HealingPotion);
                Assert.IsTrue(quests.TryResolve(disposition).Applied, disposition.ToString());
                Assert.AreEqual(goldBefore + gold, inventory.Gold, disposition.ToString());
                Assert.AreEqual(potionsBefore + potions, inventory.CountOf(ItemCatalog.HealingPotion), disposition.ToString());
                Assert.AreEqual(120, progression.Xp, disposition.ToString());
                Assert.AreEqual(2, progression.Level, "Any route offers one level-up.");
            }
        }

        [Test]
        public void CluesAreStoredApartAndSurviveAccepting()
        {
            Assert.AreEqual(QuestRejection.None, quests.RecordClue(QuestLog.ClueWreckSeen));
            Assert.AreEqual(QuestRejection.UnknownClue, quests.RecordClue("clue_i_have_proof"), "A claimed fact is not a clue.");
            Accept();
            Assert.IsTrue(quests.HasClue(QuestLog.ClueWreckSeen));
            Assert.AreEqual(MainQuestState.Active, quests.MainState, "A clue never advances a state.");
            quests.RecordClue(QuestLog.ClueFenTestimony);
            Assert.AreEqual(MainQuestState.Active, quests.MainState);
        }

        [Test]
        public void ResolvingWithAFullPackIsRefusedAndCanBeRetried()
        {
            Accept();
            Recover();
            // Fill every free general slot so the healer's potions cannot fit.
            for (int i = inventory.FreeSlots; i > 0; i--)
            {
                Assert.IsTrue(inventory.TryApply(0, new[] { new ItemChange(ItemCatalog.IronScrap, 20) }, out _));
            }

            // Top up the potion stack so no partial stack has room either.
            inventory.TryApply(0, new[] { new ItemChange(ItemCatalog.HealingPotion, 4) }, out _);
            int gold = inventory.Gold;
            Assert.AreEqual(QuestRejection.NoRoom, quests.TryResolve(ParcelDisposition.Healer).Reason);
            Assert.AreEqual(MainQuestState.ParcelRecovered, quests.MainState);
            Assert.AreEqual(1, inventory.CountOf(ItemCatalog.MedicalParcel));
            Assert.AreEqual(gold, inventory.Gold);
            Assert.AreEqual(0, progression.Xp);

            Assert.IsTrue(quests.TryResolve(ParcelDisposition.Garrison).Applied, "The refusal did not use up the event.");
        }

        [Test]
        public void TheParcelIsProtectedFromOrdinaryChanges()
        {
            Recover();
            Assert.IsFalse(inventory.TryApply(0, new[] { new ItemChange(ItemCatalog.MedicalParcel, -1) }, out var rejection));
            Assert.AreEqual(InventoryRejection.ProtectedItem, rejection);
        }

        [Test]
        public void HerbsBeforeSundownTakesThreeHerbsOnce()
        {
            Assert.AreEqual(QuestRejection.WrongState, quests.TryCompleteHerbs().Reason, "Not started.");
            Assert.AreEqual(QuestRejection.UnknownQuest, quests.TryStartSideQuest("quest_dragons"));
            Assert.AreEqual(QuestRejection.None, quests.TryStartSideQuest(QuestLog.HerbsQuestId));
            Assert.AreEqual(QuestRejection.WrongState, quests.TryStartSideQuest(QuestLog.HerbsQuestId));
            GiveHerbs(2);
            Assert.AreEqual(QuestRejection.MissingItem, quests.TryCompleteHerbs().Reason);
            Assert.AreEqual(2, inventory.CountOf(ItemCatalog.HerbBundle));
            GiveHerbs(2);
            Assert.IsTrue(quests.TryCompleteHerbs().Applied);
            Assert.AreEqual(SideQuestState.Completed, quests.HerbsState);
            Assert.AreEqual(1, inventory.CountOf(ItemCatalog.HerbBundle));
            Assert.AreEqual(QuestLog.SideQuestXp, progression.Xp);
            Assert.AreEqual(QuestRejection.WrongState, quests.TryCompleteHerbs().Reason);
            Assert.AreEqual(QuestLog.SideQuestXp, progression.Xp);
        }

        [Test]
        public void TheBadgeIsFoundOnceAndReturnedOnce()
        {
            Assert.IsTrue(quests.TryFindBadge().Applied, "It can be found before anyone asks.");
            Assert.AreEqual(QuestRejection.DuplicateEvent, quests.TryFindBadge().Reason);
            Assert.AreEqual(1, inventory.CountOf(ItemCatalog.LostBadge));
            Assert.AreEqual(QuestRejection.WrongState, quests.TryCompleteBadge().Reason);
            quests.TryStartSideQuest(QuestLog.BadgeQuestId);
            int gold = inventory.Gold;
            Assert.IsTrue(quests.TryCompleteBadge().Applied);
            Assert.AreEqual(gold + QuestLog.BadgeGold, inventory.Gold);
            Assert.AreEqual(0, inventory.CountOf(ItemCatalog.LostBadge));
            Assert.AreEqual(SideQuestState.Completed, quests.BadgeState);
            Assert.AreEqual(QuestRejection.WrongState, quests.TryCompleteBadge().Reason);
            Assert.AreEqual(QuestRejection.WrongState, quests.TryFindBadge().Reason);
            Assert.AreEqual(QuestLog.SideQuestXp, progression.Xp);
        }

        [Test]
        public void TheFullBudgetReachesLevelThree()
        {
            Accept();
            Recover();
            quests.TryResolve(ParcelDisposition.Healer);
            quests.TryStartSideQuest(QuestLog.HerbsQuestId);
            GiveHerbs(3);
            quests.TryCompleteHerbs();
            quests.TryFindBadge();
            quests.TryStartSideQuest(QuestLog.BadgeQuestId);
            quests.TryCompleteBadge();
            foreach (var landmark in new[] { "discover_wreck", "discover_hollow", "discover_watch" })
            {
                progression.TryAward(landmark, 10);
            }

            Assert.AreEqual(250, progression.Xp);
            Assert.AreEqual(3, progression.Level);
        }

        [Test]
        public void ReceiptsReachSubscribersForAppliedAndRefusedSteps()
        {
            var receipts = new System.Collections.Generic.List<QuestReceipt>();
            quests.Completed += receipts.Add;
            quests.TryResolve(ParcelDisposition.Healer);
            Accept();
            Assert.AreEqual(2, receipts.Count);
            Assert.IsFalse(receipts[0].Applied);
            Assert.IsTrue(receipts[1].Applied);
            Assert.That(receipts.Select(r => r.Line), Has.All.Not.Empty);
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LivingVale.Game;
using NUnit.Framework;

namespace LivingVale.Tests
{
    public sealed class SaveGameTests
    {
        private const string PotionStock = "stock_mara_potion_one";
        private const string LootEvent = "save_test_loot";
        private const string XpEvent = "save_test_xp";
        private const string ResolveEvent = "quest_missing_wagon_resolved";
        private const string HerbsEvent = "quest_herbs_completed";

        private string directory;
        private Services game;

        private sealed class Session : ITradeSession
        {
            public long Generation { get; set; } = 17;
            public string ActiveNpcId { get; set; } = VendorStock.MaraId;
        }

        private sealed class Services
        {
            public readonly PlayerInventory Inventory;
            public readonly VendorStock Mara;
            public readonly Session Session;
            public readonly TradeService Trade;
            public readonly Progression Progression;
            public readonly QuestLog Quests;
            public readonly SaveGame Save;

            public Services(string directory)
            {
                var catalog = ItemCatalog.CreateDefault();
                Inventory = PlayerInventory.CreateStarting(catalog);
                Mara = VendorStock.CreateMara(catalog);
                Session = new Session();
                Trade = new TradeService(Inventory, new[] { Mara }, Session,
                    () => new DateTime(2026, 11, 5, 12, 0, 0, DateTimeKind.Utc));
                Progression = new Progression();
                Quests = new QuestLog(Inventory, Progression, Session);
                Save = new SaveGame(Inventory, Trade, Quests, Progression, directory);
            }
        }

        [SetUp]
        public void SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), "LivingValeSaveTests_" + Guid.NewGuid().ToString("N"));
            game = new Services(directory);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }

        private static TradeQuote QuotePotion(Services services)
        {
            services.Session.ActiveNpcId = VendorStock.MaraId;
            Assert.That(services.Trade.TryQuotePurchase(VendorStock.MaraId, PotionStock,
                services.Session.Generation, out var quote), Is.EqualTo(TradeRejection.None));
            Assert.That(quote, Is.Not.Null);
            return quote;
        }

        private static QuestOffer OfferQuest(Services services)
        {
            services.Session.ActiveNpcId = QuestLog.IvenId;
            services.Session.Generation++;
            Assert.That(services.Quests.TryOffer(QuestLog.IvenId, QuestLog.IvenOfferId,
                services.Session.Generation, out var offer), Is.EqualTo(QuestRejection.None));
            Assert.That(offer.ConversationGeneration, Is.EqualTo(services.Session.Generation));
            return offer;
        }

        private static void MakeProgress(Services services)
        {
            Assert.That(services.Inventory.TryGiveLoot(LootEvent, new[]
            {
                new ItemChange(ItemCatalog.IronSword, 1),
                new ItemChange(ItemCatalog.PaddedArmor, 1),
                new ItemChange(ItemCatalog.HerbBundle, QuestLog.HerbsNeeded),
                new ItemChange(ItemCatalog.WolfPelt, 2),
                new ItemChange(ItemCatalog.IronScrap, 21)
            }, 27), Is.EqualTo(InventoryRejection.None));
            Assert.That(services.Inventory.TryEquip(ItemCatalog.IronSword), Is.True);
            Assert.That(services.Inventory.TryEquip(ItemCatalog.PaddedArmor), Is.True);
            Assert.That(services.Inventory.AssignQuickUse(0, null), Is.True);
            Assert.That(services.Inventory.AssignQuickUse(1, ItemCatalog.Bread), Is.True);
            Assert.That(services.Inventory.AssignQuickUse(2, ItemCatalog.HealingPotion), Is.True);
            Assert.That(services.Trade.Confirm(QuotePotion(services).Id).Applied, Is.True);

            var offer = OfferQuest(services);
            Assert.That(services.Quests.MainState, Is.EqualTo(MainQuestState.Offered));
            Assert.That(services.Quests.AcceptOffer(offer.Id).Applied, Is.True);
            Assert.That(services.Quests.MainState, Is.EqualTo(MainQuestState.Active));
            Assert.That(services.Quests.RecordClue(QuestLog.ClueWreckSeen), Is.EqualTo(QuestRejection.None));
            Assert.That(services.Quests.RecordClue(QuestLog.ClueCampLocated), Is.EqualTo(QuestRejection.None));
            Assert.That(services.Quests.RecordClue(QuestLog.AgreementHealer), Is.EqualTo(QuestRejection.None));
            Assert.That(services.Quests.RecordClue(QuestLog.AgreementGarrison), Is.EqualTo(QuestRejection.None));
            Assert.That(services.Quests.TryRecoverParcel(RecoveryRoute.Investigation).Applied, Is.True);
            Assert.That(services.Inventory.CountOf(ItemCatalog.MedicalParcel), Is.EqualTo(1));
            Assert.That(services.Quests.TryResolve(ParcelDisposition.Division).Applied, Is.True);

            Assert.That(services.Quests.TryStartSideQuest(QuestLog.HerbsQuestId), Is.EqualTo(QuestRejection.None));
            Assert.That(services.Quests.TryCompleteHerbs().Applied, Is.True);
            Assert.That(services.Quests.TryStartSideQuest(QuestLog.BadgeQuestId), Is.EqualTo(QuestRejection.None));
            Assert.That(services.Quests.TryFindBadge().Applied, Is.True);
            Assert.That(services.Progression.TryAward(XpEvent, 90), Is.EqualTo(ProgressionRejection.None));
            Assert.That(services.Progression.TryChoosePerk(Perk.TrustedFace), Is.EqualTo(ProgressionRejection.None));
            Assert.That(services.Progression.TryChoosePerk(Perk.Hardy), Is.EqualTo(ProgressionRejection.None));
        }

        [Test]
        public void RoundTrip_IntoFreshServices_RestoresEveryDurableValue()
        {
            MakeProgress(game);
            Assert.That(game.Inventory.Gold, Is.EqualTo(82));
            Assert.That(game.Progression.Xp, Is.EqualTo(260));
            Assert.That(game.Save.TrySave(out var error), Is.True, error);
            Assert.That(File.Exists(game.Save.SavePath + ".tmp"), Is.False);

            var loaded = new Services(directory);
            Assert.That(loaded.Inventory.Gold, Is.EqualTo(40));
            Assert.That(loaded.Mara.FindEntry(PotionStock).Remaining, Is.EqualTo(6));
            Assert.That(loaded.Save.TryLoad(out error), Is.True, error);

            Assert.That(loaded.Inventory.Gold, Is.EqualTo(82));
            Assert.That(loaded.Inventory.Weapon, Is.EqualTo(ItemCatalog.IronSword));
            Assert.That(loaded.Inventory.Armor, Is.EqualTo(ItemCatalog.PaddedArmor));
            Assert.That(loaded.Inventory.CountOf(ItemCatalog.StarterSword), Is.EqualTo(1));
            Assert.That(loaded.Inventory.CountOf(ItemCatalog.TravelerArmor), Is.EqualTo(1));
            Assert.That(loaded.Inventory.CountOf(ItemCatalog.HealingPotion), Is.EqualTo(4));
            Assert.That(loaded.Inventory.CountOf(ItemCatalog.IronScrap), Is.EqualTo(21));
            Assert.That(loaded.Inventory.CountOf(ItemCatalog.WolfPelt), Is.EqualTo(2));
            Assert.That(loaded.Inventory.CountOf(ItemCatalog.HerbBundle), Is.Zero);
            Assert.That(loaded.Inventory.CountOf(ItemCatalog.MedicalParcel), Is.Zero);
            Assert.That(loaded.Inventory.QuestItems[ItemCatalog.LostBadge], Is.EqualTo(1));
            Assert.That(loaded.Inventory.Slots.Count, Is.EqualTo(16));
            for (int i = 0; i < PlayerInventory.SlotCount; i++)
            {
                Assert.That(loaded.Inventory.Slots[i].ItemId, Is.EqualTo(game.Inventory.Slots[i].ItemId), $"slot {i}");
                Assert.That(loaded.Inventory.Slots[i].Quantity, Is.EqualTo(game.Inventory.Slots[i].Quantity), $"slot {i}");
            }

            Assert.That(loaded.Inventory.QuickUse, Is.EqualTo(new[] { null, ItemCatalog.Bread, ItemCatalog.HealingPotion }));
            Assert.That(loaded.Inventory.AppliedEvents, Is.EquivalentTo(game.Inventory.AppliedEvents));
            Assert.That(loaded.Mara.Selling.Select(e => e.Remaining), Is.EqualTo(new[] { 5, 10, 8 }));
            Assert.That(loaded.Quests.MainState, Is.EqualTo(MainQuestState.Resolved));
            Assert.That(loaded.Quests.Route, Is.EqualTo(RecoveryRoute.Investigation));
            Assert.That(loaded.Quests.Disposition, Is.EqualTo(ParcelDisposition.Division));
            Assert.That(loaded.Quests.HerbsState, Is.EqualTo(SideQuestState.Completed));
            Assert.That(loaded.Quests.BadgeState, Is.EqualTo(SideQuestState.Active));
            Assert.That(loaded.Quests.Clues, Is.EquivalentTo(game.Quests.Clues));
            Assert.That(loaded.Progression.Xp, Is.EqualTo(260));
            Assert.That(loaded.Progression.Perks, Is.EqualTo(new[] { Perk.TrustedFace, Perk.Hardy }));
            Assert.That(loaded.Progression.AwardedEvents, Is.EquivalentTo(new[] { ResolveEvent, HerbsEvent, XpEvent }));
            Assert.That(loaded.Save.ToJson(loaded.Save.Capture()), Is.EqualTo(game.Save.ToJson(game.Save.Capture())));
        }

        [Test]
        public void LoadedRewardLedgers_RefuseQuestXpAndLootReplays()
        {
            MakeProgress(game);
            Assert.That(game.Save.TrySave(out var error), Is.True, error);
            var loaded = new Services(directory);
            Assert.That(loaded.Save.TryLoad(out error), Is.True, error);
            string before = loaded.Save.ToJson(loaded.Save.Capture());

            Assert.That(loaded.Quests.TryResolve(ParcelDisposition.Division).Applied, Is.False);
            Assert.That(loaded.Quests.TryCompleteHerbs().Applied, Is.False);
            foreach (string eventId in new[] { ResolveEvent, HerbsEvent, XpEvent })
            {
                Assert.That(loaded.Progression.TryAward(eventId, 100), Is.EqualTo(ProgressionRejection.DuplicateEvent));
            }

            Assert.That(loaded.Inventory.TryGiveLoot(LootEvent, new[] { new ItemChange(ItemCatalog.IronSword, 1) }, 27),
                Is.EqualTo(InventoryRejection.DuplicateEvent));
            Assert.That(loaded.Inventory.Gold, Is.EqualTo(82));
            Assert.That(loaded.Progression.Xp, Is.EqualTo(260));
            Assert.That(loaded.Save.ToJson(loaded.Save.Capture()), Is.EqualTo(before));
        }

        [TestCase("garbage")]
        [TestCase("truncated")]
        [TestCase("trailing-object")]
        [TestCase("version")]
        [TestCase("unknown-item")]
        [TestCase("negative-gold")]
        [TestCase("quest-name")]
        [TestCase("resolved-without-disposition")]
        [TestCase("numeric-main")]
        [TestCase("numeric-route")]
        [TestCase("numeric-disposition")]
        [TestCase("numeric-herbs")]
        [TestCase("numeric-badge")]
        [TestCase("null-slots")]
        [TestCase("slot-count")]
        [TestCase("negative-stack")]
        [TestCase("empty-positive-stack")]
        [TestCase("duplicate-quest-item")]
        [TestCase("unknown-vendor")]
        [TestCase("duplicate-vendor")]
        [TestCase("unknown-stock")]
        [TestCase("negative-stock")]
        [TestCase("duplicate-stock")]
        [TestCase("unknown-clue")]
        [TestCase("negative-xp")]
        [TestCase("unknown-perk")]
        [TestCase("too-many-perks")]
        [TestCase("empty-xp-event")]
        [TestCase("empty-inventory-event")]
        [TestCase("missing-version")]
        [TestCase("active-with-parcel-event")]
        [TestCase("recovered-without-parcel")]
        [TestCase("resolved-without-xp")]
        [TestCase("badge-without-event")]
        [TestCase("missing-vendor")]
        [TestCase("missing-stock")]
        public void CorruptSave_LeavesAllServicesAndPendingCardsUntouched(string corruption)
        {
            var different = new Services(directory);
            var snapshot = different.Save.Capture();
            string json;
            switch (corruption)
            {
                case "version": snapshot.version = 99; break;
                case "unknown-item": snapshot.slots[0].id = "item_nonexistent"; break;
                case "negative-gold": snapshot.gold = -1; break;
                case "quest-name": snapshot.mainState = "not_a_quest_state"; break;
                case "resolved-without-disposition":
                    snapshot.mainState = "Resolved";
                    snapshot.route = "Combat";
                    snapshot.disposition = "None";
                    break;
                case "numeric-main": snapshot.mainState = "0"; break;
                case "numeric-route": snapshot.route = "0"; break;
                case "numeric-disposition": snapshot.disposition = "0"; break;
                case "numeric-herbs": snapshot.herbsState = "0"; break;
                case "numeric-badge": snapshot.badgeState = "0"; break;
                case "null-slots": snapshot.slots = null; break;
                case "slot-count": snapshot.slots.RemoveAt(15); break;
                case "negative-stack": snapshot.slots[0].count = -1; break;
                case "empty-positive-stack": snapshot.slots[0].id = null; break;
                case "duplicate-quest-item":
                    snapshot.questItems.Add(new SavedItem { id = ItemCatalog.LostBadge, count = 1 });
                    snapshot.questItems.Add(new SavedItem { id = ItemCatalog.LostBadge, count = 1 });
                    break;
                case "unknown-vendor": snapshot.vendors[0].id = "npc_nonexistent"; break;
                case "duplicate-vendor": snapshot.vendors.Add(snapshot.vendors[0]); break;
                case "unknown-stock": snapshot.vendors[0].entries[0].id = "stock_nonexistent"; break;
                case "negative-stock": snapshot.vendors[0].entries[0].remaining = -1; break;
                case "duplicate-stock": snapshot.vendors[0].entries.Add(snapshot.vendors[0].entries[0]); break;
                case "unknown-clue": snapshot.clues.Add("clue_nonexistent"); break;
                case "negative-xp": snapshot.xp = -1; break;
                case "unknown-perk": snapshot.perks.Add("Hardy"); break;
                case "too-many-perks": snapshot.perks.Add("perk_hardy"); break;
                case "empty-xp-event": snapshot.awardedEvents.Add(""); break;
                case "empty-inventory-event": snapshot.appliedEvents.Add(""); break;
                // Cross-service contradictions: each would load a quest that can never finish or pays twice.
                case "active-with-parcel-event":
                    snapshot.mainState = "Active";
                    snapshot.appliedEvents.Add(QuestLog.ParcelEvent);
                    break;
                case "recovered-without-parcel":
                    snapshot.mainState = "ParcelRecovered";
                    snapshot.route = "Combat";
                    snapshot.appliedEvents.Add(QuestLog.ParcelEvent);
                    break;
                case "resolved-without-xp":
                    snapshot.mainState = "Resolved";
                    snapshot.route = "Combat";
                    snapshot.disposition = "Healer";
                    snapshot.appliedEvents.Add(QuestLog.ParcelEvent);
                    snapshot.appliedEvents.Add(QuestLog.ResolvedEvent);
                    break;
                case "badge-without-event": snapshot.questItems.Add(new SavedItem { id = ItemCatalog.LostBadge, count = 1 }); break;
                case "missing-vendor": snapshot.vendors.Clear(); break;
                case "missing-stock": snapshot.vendors[0].entries.RemoveAt(0); break;
            }

            json = different.Save.ToJson(snapshot);
            if (corruption == "garbage") json = "this is not JSON";
            if (corruption == "truncated") json = json.Substring(0, json.Length / 2);
            if (corruption == "trailing-object") json += "{}";
            if (corruption == "missing-version") json = json.Replace("\"version\"", "\"discardedVersion\"");
            Directory.CreateDirectory(directory);
            File.WriteAllText(game.Save.SavePath, json);

            Assert.That(game.Inventory.TryGiveLoot("live_loot", new[] { new ItemChange(ItemCatalog.WolfPelt, 3) }, 7),
                Is.EqualTo(InventoryRejection.None));
            Assert.That(game.Trade.Confirm(QuotePotion(game).Id).Applied, Is.True);
            Assert.That(game.Progression.TryAward("live_xp", 100), Is.EqualTo(ProgressionRejection.None));
            Assert.That(game.Quests.RecordClue(QuestLog.ClueWreckSeen), Is.EqualTo(QuestRejection.None));
            var quote = QuotePotion(game);
            var offer = OfferQuest(game);
            string before = game.Save.ToJson(game.Save.Capture());
            long inventoryRevision = game.Inventory.Revision;
            long vendorRevision = game.Mara.Revision;

            Assert.That(game.Save.TryLoad(out var error), Is.False, corruption);
            Assert.That(error, Is.Not.Null.And.Not.Empty);
            Assert.That(game.Inventory.Gold, Is.EqualTo(37));
            Assert.That(game.Inventory.CountOf(ItemCatalog.WolfPelt), Is.EqualTo(3));
            Assert.That(game.Inventory.CountOf(ItemCatalog.HealingPotion), Is.EqualTo(2));
            Assert.That(game.Quests.MainState, Is.EqualTo(MainQuestState.Offered));
            Assert.That(game.Progression.Xp, Is.EqualTo(100));
            Assert.That(game.Save.ToJson(game.Save.Capture()), Is.EqualTo(before));
            Assert.That(game.Inventory.Revision, Is.EqualTo(inventoryRevision));
            Assert.That(game.Mara.Revision, Is.EqualTo(vendorRevision));
            Assert.That(game.Trade.ActiveQuote, Is.SameAs(quote));
            Assert.That(quote.State, Is.EqualTo(TradeQuoteState.Pending));
            Assert.That(game.Quests.ActiveOffer, Is.SameAs(offer));
            Assert.That(offer.State, Is.EqualTo(QuestOfferState.Pending));
        }

        [Test]
        public void Load_InvalidatesQuoteAndOffer_AndOldButtonsCannotApply()
        {
            var quote = QuotePotion(game);
            var offer = OfferQuest(game);
            Assert.That(game.Save.TrySave(out var error), Is.True, error);
            Assert.That(game.Trade.ActiveQuote, Is.SameAs(quote), "saving must not invalidate a live card");
            Assert.That(game.Quests.ActiveOffer, Is.SameAs(offer));
            string json = File.ReadAllText(game.Save.SavePath);
            Assert.That(json, Does.Not.Contain("quote_"));
            Assert.That(json, Does.Not.Contain(offer.Id));
            Assert.That(json, Does.Not.Contain("ConversationGeneration"));

            Assert.That(game.Save.TryLoad(out error), Is.True, error);
            Assert.That(game.Trade.ActiveQuote, Is.Null);
            Assert.That(game.Quests.ActiveOffer, Is.Null);
            Assert.That(quote.State, Is.EqualTo(TradeQuoteState.Invalidated));
            Assert.That(offer.State, Is.EqualTo(QuestOfferState.Invalidated));
            Assert.That(game.Trade.Confirm(quote.Id).Reason, Is.EqualTo(TradeRejection.NotPending));
            Assert.That(game.Quests.AcceptOffer(offer.Id).Reason, Is.EqualTo(QuestRejection.OfferNotPending));
            Assert.That(game.Inventory.Gold, Is.EqualTo(40));
            Assert.That(game.Inventory.CountOf(ItemCatalog.HealingPotion), Is.EqualTo(1));
            Assert.That(game.Mara.FindEntry(PotionStock).Remaining, Is.EqualTo(6));
            Assert.That(game.Quests.MainState, Is.EqualTo(MainQuestState.Offered));
        }

        [Test]
        public void SecondSave_KeepsFirstAsBackup_AndBackupLoadRestoresIt()
        {
            Assert.That(game.Save.SavePath, Is.EqualTo(Path.Combine(directory, "livingvale_save.json")));
            Assert.That(game.Save.BackupPath, Is.EqualTo(Path.Combine(directory, "livingvale_save.bak.json")));
            Assert.That(Directory.Exists(directory), Is.False);
            Assert.That(game.Save.TrySave(out var error), Is.True, error);
            string first = File.ReadAllText(game.Save.SavePath);
            Assert.That(File.Exists(game.Save.BackupPath), Is.False);
            MakeProgress(game);
            Assert.That(game.Save.TrySave(out error), Is.True, error);
            Assert.That(File.ReadAllText(game.Save.SavePath), Is.Not.EqualTo(first));
            Assert.That(File.ReadAllText(game.Save.BackupPath), Is.EqualTo(first));

            var loaded = new Services(directory);
            Assert.That(loaded.Save.TryLoad(out error), Is.True, error);
            Assert.That(loaded.Inventory.Gold, Is.EqualTo(82));
            Assert.That(loaded.Quests.MainState, Is.EqualTo(MainQuestState.Resolved));
            Assert.That(loaded.Save.TryLoadBackup(out error), Is.True, error);
            Assert.That(loaded.Inventory.Gold, Is.EqualTo(40));
            Assert.That(loaded.Inventory.Weapon, Is.EqualTo(ItemCatalog.StarterSword));
            Assert.That(loaded.Inventory.CountOf(ItemCatalog.HealingPotion), Is.EqualTo(1));
            Assert.That(loaded.Mara.FindEntry(PotionStock).Remaining, Is.EqualTo(6));
            Assert.That(loaded.Quests.MainState, Is.EqualTo(MainQuestState.Undiscovered));
            Assert.That(loaded.Progression.Xp, Is.Zero);
            Assert.That(loaded.Progression.Perks, Is.Empty);
            Assert.That(loaded.Inventory.AppliedEvents, Is.Empty);
        }

        [TestCase(Perk.Hardy, "perk_hardy")]
        [TestCase(Perk.Skirmisher, "perk_skirmisher")]
        [TestCase(Perk.TrustedFace, "perk_trusted_face")]
        public void Perks_UseStableStringIds(Perk perk, string id)
        {
            Assert.That(game.Progression.TryAward("level_up", 100), Is.EqualTo(ProgressionRejection.None));
            Assert.That(game.Progression.TryChoosePerk(perk), Is.EqualTo(ProgressionRejection.None));
            string json = game.Save.ToJson(game.Save.Capture());
            Assert.That(json, Does.Contain("\"" + id + "\""));
            Assert.That(game.Save.TryParse(json, out var snapshot, out var error), Is.True, error);
            Assert.That(snapshot.perks, Is.EqualTo(new[] { id }));
            var loaded = new Services(directory);
            Assert.That(loaded.Save.TryApply(snapshot, out error), Is.True, error);
            Assert.That(loaded.Progression.Perks, Is.EqualTo(new[] { perk }));
            Assert.That(loaded.Progression.Xp, Is.EqualTo(100));
        }

        [Test]
        public void EmptyEquipmentAndQuickUse_RoundTripAsNullIds()
        {
            var snapshot = game.Save.Capture();
            snapshot.weapon = null;
            snapshot.armor = null;
            snapshot.quickUse = new List<string> { null, null, null };
            Assert.That(game.Save.TryApply(snapshot, out var error), Is.True, error);
            Assert.That(game.Save.TrySave(out error), Is.True, error);
            var loaded = new Services(directory);
            Assert.That(loaded.Save.TryLoad(out error), Is.True, error);
            Assert.That(loaded.Inventory.Weapon, Is.Null);
            Assert.That(loaded.Inventory.Armor, Is.Null);
            Assert.That(loaded.Inventory.QuickUse, Is.EqualTo(new string[3]));
            Assert.That(loaded.Inventory.Slots.Count(s => s.IsEmpty), Is.EqualTo(14));
        }

        [Test]
        public void CaptureAndValidate_AreDetachedAndDoNotInvalidateCards()
        {
            var quote = QuotePotion(game);
            var offer = OfferQuest(game);
            var snapshot = game.Save.Capture();
            long revision = game.Inventory.Revision;
            Assert.That(game.Save.Validate(snapshot), Is.Null);
            Assert.That(game.Inventory.Revision, Is.EqualTo(revision));
            Assert.That(game.Trade.ActiveQuote, Is.SameAs(quote));
            Assert.That(game.Quests.ActiveOffer, Is.SameAs(offer));
            Assert.That(game.Inventory.TryGiveLoot("later", null, 9), Is.EqualTo(InventoryRejection.None));
            Assert.That(snapshot.gold, Is.EqualTo(40));
            Assert.That(snapshot.appliedEvents, Is.Empty);
            snapshot.slots[0].count = 4;
            Assert.That(game.Inventory.CountOf(ItemCatalog.HealingPotion), Is.EqualTo(1));

            string before = game.Save.ToJson(game.Save.Capture());
            Assert.That(game.Save.Validate(null), Is.Not.Null);
            Assert.That(game.Save.TryApply(null, out var error), Is.False);
            Assert.That(error, Is.Not.Null.And.Not.Empty);
            Assert.That(game.Save.ToJson(game.Save.Capture()), Is.EqualTo(before));
            Assert.That(game.Trade.ActiveQuote, Is.SameAs(quote));
            Assert.That(game.Quests.ActiveOffer, Is.SameAs(offer));
        }

        [Test]
        public void MissingAndUnreadableFiles_ReturnErrorsWithoutChangingState()
        {
            MakeProgress(game);
            string before = game.Save.ToJson(game.Save.Capture());
            Assert.That(game.Save.TryLoad(out var error), Is.False);
            Assert.That(error, Is.Not.Null.And.Not.Empty);
            Assert.That(game.Save.TryLoadBackup(out error), Is.False);
            Assert.That(error, Is.Not.Null.And.Not.Empty);
            Assert.That(game.Save.TrySave(out error), Is.True, error);
            using (new FileStream(game.Save.SavePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                Assert.That(game.Save.TryLoad(out error), Is.False);
                Assert.That(error, Is.Not.Null.And.Not.Empty);
            }

            Assert.That(game.Save.ToJson(game.Save.Capture()), Is.EqualTo(before));
        }

        [Test]
        public void SaveWriteFailure_ReturnsAnErrorAndPreservesTheExistingSave()
        {
            Assert.That(game.Save.TrySave(out var error), Is.True, error);
            string first = File.ReadAllText(game.Save.SavePath);
            Assert.That(game.Inventory.TryGiveLoot("later", null, 9), Is.EqualTo(InventoryRejection.None));
            // The temporary path cannot be opened as a file; the primary file must remain intact.
            Directory.CreateDirectory(game.Save.SavePath + ".tmp");
            Assert.That(game.Save.TrySave(out error), Is.False);
            Assert.That(error, Is.Not.Null.And.Not.Empty);
            Assert.That(File.ReadAllText(game.Save.SavePath), Is.EqualTo(first));
            Assert.That(game.Inventory.Gold, Is.EqualTo(49));
        }
    }
}

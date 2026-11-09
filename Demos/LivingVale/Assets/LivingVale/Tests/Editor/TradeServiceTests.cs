using System;
using System.Linq;
using LivingVale.Game;
using NUnit.Framework;

namespace LivingVale.Tests
{
    internal sealed class FakeTradeSession : ITradeSession
    {
        public long Generation { get; set; } = 1;
        public string ActiveNpcId { get; set; } = VendorStock.MaraId;
    }

    public sealed class TradeServiceTests
    {
        private const string Potion = "stock_mara_potion_one";
        private const string PeltSale = "sale_mara_wolf_pelt_one";

        private ItemCatalog catalog;
        private PlayerInventory inventory;
        private VendorStock mara;
        private FakeTradeSession session;
        private DateTime now;
        private TradeService trade;

        [SetUp]
        public void SetUp()
        {
            catalog = ItemCatalog.CreateDefault();
            inventory = new PlayerInventory(catalog);
            mara = VendorStock.CreateMara(catalog);
            session = new FakeTradeSession();
            now = new DateTime(2026, 11, 5, 12, 0, 0, DateTimeKind.Utc);
            trade = new TradeService(inventory, new[] { mara }, session, () => now);
        }

        private TradeQuote QuotePotion()
        {
            Assert.That(trade.TryQuotePurchase(VendorStock.MaraId, Potion, session.Generation, out var quote), Is.EqualTo(TradeRejection.None));
            return quote;
        }

        private int PotionStock => mara.FindEntry(Potion).Remaining;

        private void AssertNothingMoved(int gold, int potions, int stock, string because)
        {
            Assert.That(inventory.Gold, Is.EqualTo(gold), because);
            Assert.That(inventory.CountOf(ItemCatalog.HealingPotion), Is.EqualTo(potions), because);
            Assert.That(PotionStock, Is.EqualTo(stock), because);
        }

        [Test]
        public void Quote_ChangesNothing_ConfirmTransfersExactlyOnce()
        {
            var quote = QuotePotion();
            Assert.That(quote.Total, Is.EqualTo(10));
            Assert.That(quote.Quantity, Is.EqualTo(1));
            Assert.That(quote.ItemId, Is.EqualTo(ItemCatalog.HealingPotion));
            Assert.That(quote.ConversationGeneration, Is.EqualTo(1));
            Assert.That(quote.State, Is.EqualTo(TradeQuoteState.Pending));
            AssertNothingMoved(40, 0, 6, "a quote is not a transaction");

            var receipt = trade.Confirm(quote.Id);
            Assert.That(receipt.Applied, Is.True, receipt.ToString());
            Assert.That(receipt.GoldDelta, Is.EqualTo(-10));
            Assert.That(receipt.ItemDelta, Is.EqualTo(1));
            Assert.That(receipt.GoldAfter, Is.EqualTo(30));
            Assert.That(quote.State, Is.EqualTo(TradeQuoteState.Consumed));
            Assert.That(trade.ActiveQuote, Is.Null);
            AssertNothingMoved(30, 1, 5, "one confirmed purchase");
        }

        [Test]
        public void ReplayedConfirmation_DuplicatesNeitherGoldNorItems()
        {
            var quote = QuotePotion();
            trade.Confirm(quote.Id);

            for (int i = 0; i < 3; i++)
            {
                var replay = trade.Confirm(quote.Id);
                Assert.That(replay.Applied, Is.False);
                Assert.That(replay.Reason, Is.EqualTo(TradeRejection.NotPending));
                Assert.That(replay.GoldDelta, Is.Zero);
            }

            AssertNothingMoved(30, 1, 5, "replays change nothing");
        }

        [Test]
        public void ReplayedSale_DoesNotPayTwice()
        {
            inventory.TryGiveLoot("wolf", new[] { new ItemChange(ItemCatalog.WolfPelt, 1) }, 0);
            Assert.That(trade.TryQuoteSale(VendorStock.MaraId, PeltSale, 1, out var quote), Is.EqualTo(TradeRejection.None));
            Assert.That(trade.Confirm(quote.Id).Applied, Is.True);
            Assert.That(trade.Confirm(quote.Id).Applied, Is.False);
            Assert.That(inventory.Gold, Is.EqualTo(44));
            Assert.That(inventory.CountOf(ItemCatalog.WolfPelt), Is.Zero);
        }

        [Test]
        public void ExpiredQuote_ChangesNothing()
        {
            var quote = QuotePotion();
            now += trade.QuoteLifetime;

            var receipt = trade.Confirm(quote.Id);
            Assert.That(receipt.Applied, Is.False);
            Assert.That(receipt.Reason, Is.EqualTo(TradeRejection.Expired));
            Assert.That(quote.State, Is.EqualTo(TradeQuoteState.Expired));
            AssertNothingMoved(40, 0, 6, "expired");
        }

        [Test]
        public void QuoteFromAnEarlierConversation_ChangesNothing()
        {
            var quote = QuotePotion();
            session.Generation = 3; // closed and reopened

            var receipt = trade.Confirm(quote.Id);
            Assert.That(receipt.Reason, Is.EqualTo(TradeRejection.StaleConversation));
            Assert.That(quote.State, Is.EqualTo(TradeQuoteState.Invalidated));
            AssertNothingMoved(40, 0, 6, "stale conversation");
        }

        [Test]
        public void QuoteAfterSwitchingSpeaker_ChangesNothing()
        {
            var quote = QuotePotion();
            session.ActiveNpcId = "npc_iven";
            Assert.That(trade.Confirm(quote.Id).Reason, Is.EqualTo(TradeRejection.StaleConversation));
            AssertNothingMoved(40, 0, 6, "switched speaker");
        }

        [Test]
        public void QuoteAfterHoldingsChanged_IsRefusedNotRecalculated()
        {
            var quote = QuotePotion();
            inventory.TryGiveLoot("chest", null, 5);

            var receipt = trade.Confirm(quote.Id);
            Assert.That(receipt.Reason, Is.EqualTo(TradeRejection.StaleRevision));
            AssertNothingMoved(45, 0, 6, "stale revision");
        }

        [Test]
        public void NewQuoteReplacesTheOld_OnlyTheVisibleOneConfirms()
        {
            var first = QuotePotion();
            var second = QuotePotion();
            Assert.That(first.State, Is.EqualTo(TradeQuoteState.Invalidated), "a new quote replaces the old one");
            Assert.That(trade.Confirm(first.Id).Reason, Is.EqualTo(TradeRejection.NotPending));
            Assert.That(trade.Confirm(second.Id).Applied, Is.True);
            AssertNothingMoved(30, 1, 5, "only the visible quote confirms");
        }

        [Test]
        public void UnaffordableQuote_IsShownButConfirmsNothing()
        {
            inventory.TryApply(-35, null, out _);
            var quote = QuotePotion();
            Assert.That(quote.Total, Is.GreaterThan(inventory.Gold));

            var receipt = trade.Confirm(quote.Id);
            Assert.That(receipt.Applied, Is.False);
            Assert.That(receipt.Reason, Is.EqualTo(TradeRejection.NotEnoughGold));
            AssertNothingMoved(5, 0, 6, "unaffordable");
            Assert.That(inventory.Gold, Is.GreaterThanOrEqualTo(0));
        }

        [Test]
        public void PurchaseIntoAFullInventory_MovesNoGoldAndNoStock()
        {
            inventory.TryApply(0, new[] { new ItemChange(ItemCatalog.IronScrap, 20 * 16) }, out _);
            var quote = QuotePotion();

            var receipt = trade.Confirm(quote.Id);
            Assert.That(receipt.Reason, Is.EqualTo(TradeRejection.NoRoom));
            AssertNothingMoved(40, 0, 6, "all or nothing");
        }

        [Test]
        public void OutOfStock_CannotBeQuoted_AndDropsOutOfTheTargets()
        {
            inventory.TryGiveLoot("rich", null, 1000);
            for (int i = 0; i < 6; i++)
            {
                Assert.That(trade.Confirm(QuotePotion().Id).Applied, Is.True);
            }

            Assert.That(trade.TryQuotePurchase(VendorStock.MaraId, Potion, 1, out _), Is.EqualTo(TradeRejection.OutOfStock));
            Assert.That(trade.PurchaseTargets(VendorStock.MaraId).Select(e => e.Id), Has.No.Member(Potion));
            Assert.That(PotionStock, Is.Zero);
        }

        [Test]
        public void DeclinedOrInvalidatedQuote_CannotBeConfirmed()
        {
            var declined = QuotePotion();
            Assert.That(trade.Decline(declined.Id), Is.True);
            Assert.That(trade.Confirm(declined.Id).Applied, Is.False);

            var closed = QuotePotion();
            trade.InvalidatePending();
            Assert.That(closed.State, Is.EqualTo(TradeQuoteState.Invalidated));
            Assert.That(trade.Confirm(closed.Id).Applied, Is.False);
            Assert.That(trade.Confirm("quote_9999").Reason, Is.EqualTo(TradeRejection.UnknownTarget));
            AssertNothingMoved(40, 0, 6, "nothing confirmed");
        }

        [Test]
        public void QuotesNeedTheOpenConversationAndTheRightTargetType()
        {
            Assert.That(trade.TryQuotePurchase(VendorStock.MaraId, Potion, 0, out _), Is.EqualTo(TradeRejection.StaleConversation));
            Assert.That(trade.TryQuotePurchase("npc_iven", Potion, 1, out _), Is.EqualTo(TradeRejection.StaleConversation));
            Assert.That(trade.TryQuotePurchase(VendorStock.MaraId, PeltSale, 1, out _), Is.EqualTo(TradeRejection.WrongTarget),
                "a sale bundle is not a stock entry");
            Assert.That(trade.TryQuoteSale(VendorStock.MaraId, Potion, 1, out _), Is.EqualTo(TradeRejection.WrongTarget));
            Assert.That(trade.TryQuotePurchase(VendorStock.MaraId, "stock_mara_castle", 1, out _), Is.EqualTo(TradeRejection.UnknownTarget));
            Assert.That(trade.ActiveQuote, Is.Null);
        }

        [Test]
        public void SaleNeedsAWholeBundleInTheSlots()
        {
            Assert.That(trade.SaleTargets(VendorStock.MaraId), Is.Empty);
            Assert.That(trade.TryQuoteSale(VendorStock.MaraId, PeltSale, 1, out _), Is.EqualTo(TradeRejection.NotEnoughItems));

            inventory.TryGiveLoot("wolf", new[] { new ItemChange(ItemCatalog.WolfPelt, 1) }, 0);
            Assert.That(trade.SaleTargets(VendorStock.MaraId).Select(b => b.Id), Is.EqualTo(new[] { PeltSale }));
        }

        [Test]
        public void SalePricesAreBelowPurchasePrices_SoNoArbitrage()
        {
            foreach (var bundle in mara.Buying)
            {
                var item = catalog.Get(bundle.ItemId);
                Assert.That(bundle.UnitPrice, Is.GreaterThanOrEqualTo(1), bundle.Id);
                Assert.That(bundle.UnitPrice, Is.LessThan(item.BasePrice), bundle.Id);
                foreach (var entry in mara.Selling.Where(e => e.ItemId == bundle.ItemId))
                {
                    Assert.That(bundle.UnitPrice, Is.LessThan(entry.UnitPrice), $"{bundle.Id} vs {entry.Id}");
                }
            }

            Assert.That(mara.Selling.Select(e => e.ItemId),
                Is.EquivalentTo(new[] { ItemCatalog.HealingPotion, ItemCatalog.Bread, ItemCatalog.HerbBundle }));
            Assert.That(mara.Buying.Select(b => b.ItemId),
                Is.EquivalentTo(new[] { ItemCatalog.WolfPelt, ItemCatalog.IronScrap, ItemCatalog.HerbBundle }));
        }

        [Test]
        public void ThrowingOrReentrantObservers_SeeOnlyAFullyCommittedTrade()
        {
            var errors = new System.Collections.Generic.List<Exception>();
            var report = GameEvents.ReportSubscriberError;
            GameEvents.ReportSubscriberError = errors.Add;
            try
            {
                var quote = QuotePotion();
                int stockSeen = -1;
                TradeQuoteState stateSeen = TradeQuoteState.Pending;
                inventory.Changed += () => throw new InvalidOperationException("broken view");
                inventory.Changed += () =>
                {
                    stockSeen = PotionStock;
                    stateSeen = quote.State;
                };

                var receipt = trade.Confirm(quote.Id);
                Assert.That(receipt.Applied, Is.True);
                Assert.That(errors, Has.Count.EqualTo(1), "the broken view was reported, not thrown into the trade");
                Assert.That(stockSeen, Is.EqualTo(5), "the observer ran after the stock was taken");
                Assert.That(stateSeen, Is.EqualTo(TradeQuoteState.Consumed), "and after the quote was consumed");
                Assert.That(trade.ActiveQuote, Is.Null);
                AssertNothingMoved(30, 1, 5, "a full purchase despite the broken view");
            }
            finally
            {
                GameEvents.ReportSubscriberError = report;
            }
        }

        [Test]
        public void QuestItemsCannotBeStockedOrBought()
        {
            Assert.Throws<ArgumentException>(() => mara.Buy("sale_mara_badge", catalog.Get(ItemCatalog.LostBadge), 1));
            Assert.Throws<ArgumentException>(() => mara.Sell("stock_mara_parcel", catalog.Get(ItemCatalog.MedicalParcel), 1, 1));
        }
    }
}

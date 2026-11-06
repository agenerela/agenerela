using System;
using System.Collections.Generic;

namespace LivingVale.Game
{
    /// <summary>
    /// Quotes and transactions (design §9). A <c>quote_*</c> handler asks for a quote, which changes
    /// nothing; the card's button calls <see cref="Confirm"/>, which re-checks everything at commit
    /// time and moves gold, items and stock together or not at all. At most one quote is pending: a
    /// new one replaces the old, and closing or switching the conversation invalidates it.
    /// </summary>
    /// <remarks>Plain C#, no scene. Main thread only.</remarks>
    public sealed class TradeService
    {
        public static readonly TimeSpan DefaultQuoteLifetime = TimeSpan.FromSeconds(60);

        private readonly PlayerInventory inventory;
        private readonly ITradeSession session;
        private readonly Func<DateTime> utcNow;
        private readonly Dictionary<string, VendorStock> vendors = new Dictionary<string, VendorStock>();
        private readonly Dictionary<string, TradeQuote> quotes = new Dictionary<string, TradeQuote>();
        private long nextQuote;

        public TradeService(PlayerInventory inventory, IEnumerable<VendorStock> vendorStocks, ITradeSession session,
            Func<DateTime> utcNow = null)
        {
            this.inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            this.utcNow = utcNow ?? (() => DateTime.UtcNow);
            foreach (var vendor in vendorStocks)
            {
                vendors.Add(vendor.VendorId, vendor);
            }
        }

        public PlayerInventory Inventory => inventory;

        /// <summary>How long a quote can be confirmed after it was made.</summary>
        public TimeSpan QuoteLifetime { get; set; } = DefaultQuoteLifetime;

        /// <summary>The quote the card shows, or null. Only a pending quote is active.</summary>
        public TradeQuote ActiveQuote { get; private set; }

        /// <summary>Raised when the active quote appears, changes state or goes away.</summary>
        public event Action QuotesChanged;

        /// <summary>Raised for every confirmation, applied or refused.</summary>
        public event Action<TradeReceipt> Confirmed;

        public bool IsVendor(string npcId) => npcId != null && vendors.ContainsKey(npcId);

        /// <summary>Every vendor this service trades for, so a save can capture and require all of them.</summary>
        public IEnumerable<VendorStock> Vendors => vendors.Values;

        public VendorStock VendorFor(string npcId) => npcId != null && vendors.TryGetValue(npcId, out var v) ? v : null;

        /// <summary>Stock entries this NPC can sell right now: the targets of <c>quote_purchase</c>.</summary>
        public IReadOnlyList<StockEntry> PurchaseTargets(string npcId)
        {
            var result = new List<StockEntry>();
            var vendor = VendorFor(npcId);
            if (vendor != null)
            {
                foreach (var entry in vendor.Selling)
                {
                    if (entry.Available)
                    {
                        result.Add(entry);
                    }
                }
            }

            return result;
        }

        /// <summary>Sale bundles the player holds a whole bundle of: the targets of <c>quote_sale</c>.</summary>
        public IReadOnlyList<SaleBundle> SaleTargets(string npcId)
        {
            var result = new List<SaleBundle>();
            var vendor = VendorFor(npcId);
            if (vendor != null)
            {
                foreach (var bundle in vendor.Buying)
                {
                    if (inventory.CountOf(bundle.ItemId) >= bundle.BundleSize)
                    {
                        result.Add(bundle);
                    }
                }
            }

            return result;
        }

        public TradeQuote FindQuote(string quoteId) => quoteId != null && quotes.TryGetValue(quoteId, out var q) ? q : null;

        /// <summary>
        /// Quotes a stock entry. Changes no holdings. An unaffordable quote is still made, so the card
        /// can show the price and the player's gold; its confirmation is refused.
        /// </summary>
        public TradeRejection TryQuotePurchase(string npcId, string targetId, long generation, out TradeQuote quote)
        {
            quote = null;
            var rejection = CheckSession(npcId, generation);
            if (rejection != TradeRejection.None)
            {
                return rejection;
            }

            var vendor = VendorFor(npcId);
            var entry = vendor?.FindEntry(targetId);
            if (entry == null)
            {
                return IsAnyVendorTarget(targetId) ? TradeRejection.WrongTarget : TradeRejection.UnknownTarget;
            }

            if (!entry.Available)
            {
                return TradeRejection.OutOfStock;
            }

            quote = Issue(generation, npcId, TradeDirection.Purchase, entry.Id, entry.ItemId, entry.BundleSize, entry.BundlePrice, vendor);
            return TradeRejection.None;
        }

        /// <summary>Quotes a sale bundle the player holds. Changes no holdings.</summary>
        public TradeRejection TryQuoteSale(string npcId, string targetId, long generation, out TradeQuote quote)
        {
            quote = null;
            var rejection = CheckSession(npcId, generation);
            if (rejection != TradeRejection.None)
            {
                return rejection;
            }

            var vendor = VendorFor(npcId);
            var bundle = vendor?.FindBundle(targetId);
            if (bundle == null)
            {
                return IsAnyVendorTarget(targetId) ? TradeRejection.WrongTarget : TradeRejection.UnknownTarget;
            }

            if (inventory.Catalog.Get(bundle.ItemId).IsProtected)
            {
                return TradeRejection.ProtectedItem;
            }

            if (inventory.CountOf(bundle.ItemId) < bundle.BundleSize)
            {
                return TradeRejection.NotEnoughItems;
            }

            quote = Issue(generation, npcId, TradeDirection.Sale, bundle.Id, bundle.ItemId, bundle.BundleSize, bundle.BundlePrice, vendor);
            return TradeRejection.None;
        }

        /// <summary>
        /// Carries out a pending quote, re-checking it first: still pending (a replay or duplicate
        /// does nothing), not expired, made in the conversation that is still open, holdings and
        /// stock unchanged since, and gold, room and stock enough. All transfers happen together.
        /// </summary>
        public TradeReceipt Confirm(string quoteId)
        {
            var quote = FindQuote(quoteId);
            if (quote == null)
            {
                return Refuse(quoteId, TradeDirection.Purchase, null, TradeRejection.UnknownTarget);
            }

            if (quote.State != TradeQuoteState.Pending)
            {
                return Refuse(quote, TradeRejection.NotPending);
            }

            if (utcNow() >= quote.ExpiresAtUtc)
            {
                End(quote, TradeQuoteState.Expired);
                return Refuse(quote, TradeRejection.Expired);
            }

            if (CheckSession(quote.NpcId, quote.ConversationGeneration) != TradeRejection.None)
            {
                End(quote, TradeQuoteState.Invalidated);
                return Refuse(quote, TradeRejection.StaleConversation);
            }

            var vendor = VendorFor(quote.NpcId);
            if (inventory.Revision != quote.InventoryRevision || vendor.Revision != quote.VendorRevision)
            {
                End(quote, TradeQuoteState.Invalidated);
                return Refuse(quote, TradeRejection.StaleRevision);
            }

            StockEntry entry = null;
            int goldDelta;
            int itemDelta;
            if (quote.Direction == TradeDirection.Purchase)
            {
                entry = vendor.FindEntry(quote.TargetId);
                if (entry == null || entry.Remaining < quote.Quantity)
                {
                    return Refuse(quote, TradeRejection.OutOfStock);
                }

                goldDelta = -quote.Total;
                itemDelta = quote.Quantity;
            }
            else
            {
                goldDelta = quote.Total;
                itemDelta = -quote.Quantity;
            }

            // The inventory change validates everything it touches on a copy; stock was checked
            // above and cannot fail, so the two commit together. Observers hear about it only
            // after the stock and the quote are settled too, so none sees, or can interrupt, a
            // half-committed trade.
            using (inventory.DeferNotifications())
            {
                if (!inventory.TryApply(goldDelta, new[] { new ItemChange(quote.ItemId, itemDelta) }, out var refused))
                {
                    return Refuse(quote, Map(refused));
                }

                if (entry != null)
                {
                    vendor.Take(entry, quote.Quantity);
                }

                quote.State = TradeQuoteState.Consumed;
                if (ActiveQuote == quote)
                {
                    ActiveQuote = null;
                }
            }

            GameEvents.Raise(QuotesChanged);
            var receipt = new TradeReceipt(quote.Id, true, TradeRejection.None, quote.Direction, quote.ItemId,
                itemDelta, goldDelta, inventory.Gold);
            GameEvents.Raise(Confirmed, receipt);
            return receipt;
        }

        /// <summary>The player turned the quote down. False when it was not pending.</summary>
        public bool Decline(string quoteId)
        {
            var quote = FindQuote(quoteId);
            if (quote == null || quote.State != TradeQuoteState.Pending)
            {
                return false;
            }

            End(quote, TradeQuoteState.Declined);
            return true;
        }

        /// <summary>Invalidates the pending quote, if any: the conversation closed, switched or left range.</summary>
        public void InvalidatePending()
        {
            if (ActiveQuote != null)
            {
                End(ActiveQuote, TradeQuoteState.Invalidated);
            }
        }

        private TradeRejection CheckSession(string npcId, long generation)
        {
            return npcId != null && session.ActiveNpcId == npcId && session.Generation == generation
                ? TradeRejection.None
                : TradeRejection.StaleConversation;
        }

        private bool IsAnyVendorTarget(string targetId)
        {
            foreach (var vendor in vendors.Values)
            {
                if (vendor.FindEntry(targetId) != null || vendor.FindBundle(targetId) != null)
                {
                    return true;
                }
            }

            return false;
        }

        private TradeQuote Issue(long generation, string npcId, TradeDirection direction, string targetId, string itemId,
            int quantity, int total, VendorStock vendor)
        {
            if (ActiveQuote != null)
            {
                ActiveQuote.State = TradeQuoteState.Invalidated;
            }

            var quote = new TradeQuote($"quote_{++nextQuote:D4}", generation, npcId, direction, targetId, itemId,
                quantity, total, inventory.Revision, vendor.Revision, utcNow() + QuoteLifetime);
            quotes.Add(quote.Id, quote);
            ActiveQuote = quote;
            GameEvents.Raise(QuotesChanged);
            return quote;
        }

        private void End(TradeQuote quote, TradeQuoteState state)
        {
            quote.State = state;
            if (ActiveQuote == quote)
            {
                ActiveQuote = null;
            }

            GameEvents.Raise(QuotesChanged);
        }

        private TradeReceipt Refuse(TradeQuote quote, TradeRejection reason)
        {
            return Refuse(quote.Id, quote.Direction, quote.ItemId, reason);
        }

        private TradeReceipt Refuse(string quoteId, TradeDirection direction, string itemId, TradeRejection reason)
        {
            var receipt = new TradeReceipt(quoteId, false, reason, direction, itemId, 0, 0, inventory.Gold);
            GameEvents.Raise(Confirmed, receipt);
            return receipt;
        }

        private static TradeRejection Map(InventoryRejection rejection)
        {
            switch (rejection)
            {
                case InventoryRejection.NotEnoughGold: return TradeRejection.NotEnoughGold;
                case InventoryRejection.NoRoom: return TradeRejection.NoRoom;
                case InventoryRejection.NotEnoughItems: return TradeRejection.NotEnoughItems;
                case InventoryRejection.ProtectedItem: return TradeRejection.ProtectedItem;
                default: return TradeRejection.UnknownTarget;
            }
        }
    }
}

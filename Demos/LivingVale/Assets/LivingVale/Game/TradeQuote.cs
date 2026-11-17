using System;

namespace LivingVale.Game
{
    public enum TradeDirection
    {
        /// <summary>The player buys from the vendor.</summary>
        Purchase,

        /// <summary>The player sells to the vendor.</summary>
        Sale
    }

    public enum TradeQuoteState
    {
        /// <summary>Shown on the card; can still be confirmed.</summary>
        Pending,

        /// <summary>Confirmed and applied. Never applies again.</summary>
        Consumed,
        Declined,

        /// <summary>The conversation closed or switched, or a newer quote replaced it.</summary>
        Invalidated,
        Expired
    }

    /// <summary>Why a quote or a confirmation did nothing.</summary>
    public enum TradeRejection
    {
        None,

        /// <summary>No such quote, or no such stock entry or sale bundle.</summary>
        UnknownTarget,

        /// <summary>The target belongs to another vendor, or the action does not fit its type.</summary>
        WrongTarget,

        /// <summary>The quote was already consumed, declined or invalidated: a replay or a duplicate.</summary>
        NotPending,
        Expired,

        /// <summary>The conversation the quote was made in is no longer the open one, or is with someone else.</summary>
        StaleConversation,

        /// <summary>Stock or the player's holdings changed since the quote; a new quote needs a new confirmation.</summary>
        StaleRevision,
        OutOfStock,
        NotEnoughGold,
        NoRoom,
        NotEnoughItems,
        ProtectedItem
    }

    /// <summary>
    /// An exact offer, made by a <c>quote_purchase</c> or <c>quote_sale</c> handler and shown on the
    /// card before anything moves (design §9 step 3). Confirming it goes to <see cref="TradeService.Confirm"/>
    /// directly, never back through the model.
    /// </summary>
    public sealed class TradeQuote
    {
        public string Id { get; }

        /// <summary>The conversation generation it was made in; any other generation cannot confirm it.</summary>
        public long ConversationGeneration { get; }
        public string NpcId { get; }
        public TradeDirection Direction { get; }

        /// <summary>The stock entry or sale bundle it was made from.</summary>
        public string TargetId { get; }
        public string ItemId { get; }
        public int Quantity { get; }

        /// <summary>Integer gold the player pays (purchase) or receives (sale).</summary>
        public int Total { get; }

        public long InventoryRevision { get; }
        public long VendorRevision { get; }
        public DateTime ExpiresAtUtc { get; }
        public TradeQuoteState State { get; internal set; }

        internal TradeQuote(string id, long generation, string npcId, TradeDirection direction, string targetId,
            string itemId, int quantity, int total, long inventoryRevision, long vendorRevision, DateTime expiresAtUtc)
        {
            Id = id;
            ConversationGeneration = generation;
            NpcId = npcId;
            Direction = direction;
            TargetId = targetId;
            ItemId = itemId;
            Quantity = quantity;
            Total = total;
            InventoryRevision = inventoryRevision;
            VendorRevision = vendorRevision;
            ExpiresAtUtc = expiresAtUtc;
            State = TradeQuoteState.Pending;
        }

        public override string ToString() => $"{Id} {Direction} {ItemId} ×{Quantity} for {Total} ({State})";
    }

    /// <summary>
    /// What a confirmation did: applied with the authoritative changes, or refused with the reason
    /// and no change at all. The game-owned record design §12 calls an interaction receipt.
    /// </summary>
    public sealed class TradeReceipt
    {
        public string QuoteId { get; }
        public bool Applied { get; }
        public TradeRejection Reason { get; }
        public TradeDirection Direction { get; }
        public string ItemId { get; }

        /// <summary>Units the player gained (positive) or gave (negative). Zero when refused.</summary>
        public int ItemDelta { get; }

        /// <summary>Gold the player gained or paid. Zero when refused.</summary>
        public int GoldDelta { get; }
        public int GoldAfter { get; }

        internal TradeReceipt(string quoteId, bool applied, TradeRejection reason, TradeDirection direction,
            string itemId, int itemDelta, int goldDelta, int goldAfter)
        {
            QuoteId = quoteId;
            Applied = applied;
            Reason = reason;
            Direction = direction;
            ItemId = itemId;
            ItemDelta = itemDelta;
            GoldDelta = goldDelta;
            GoldAfter = goldAfter;
        }

        public override string ToString() => Applied
            ? $"{QuoteId} applied: {ItemId} {ItemDelta:+0;-0}, gold {GoldDelta:+0;-0} → {GoldAfter}"
            : $"{QuoteId} refused: {Reason}";
    }

    /// <summary>
    /// The conversation a confirmation must still belong to. The conversation layer provides it;
    /// game code only reads it, so trade does not depend on how conversations work.
    /// </summary>
    public interface ITradeSession
    {
        /// <summary>The open conversation's generation; it changes on every open and close.</summary>
        long Generation { get; }

        /// <summary>The NPC the player is talking to, or null.</summary>
        string ActiveNpcId { get; }
    }
}

using System;
using LivingVale.Game;

namespace LivingVale.Interaction
{
    /// <summary>
    /// Ties trade to the conversation layer: gives <see cref="TradeService"/> the open conversation
    /// as its <see cref="ITradeSession"/>, invalidates the pending quote whenever a conversation
    /// opens or closes (close, switch, leaving range, interruption), and writes each receipt into
    /// the chat as a system line, so the player and the next turn both see what really happened.
    /// </summary>
    /// <remarks>
    /// Build it first and pass it to the <see cref="TradeService"/>, then call <see cref="Attach"/>
    /// once the coordinator exists. Until then no conversation is open, so every quote is refused.
    /// </remarks>
    public sealed class ConversationTradeLink : ITradeSession, IDisposable
    {
        private ConversationCoordinator coordinator;
        private TradeService trade;
        private QuestLog quests;
        private QuestRoutes routes;

        public long Generation => coordinator?.Generation ?? -1;

        public string ActiveNpcId => coordinator?.ActiveNpc?.Id;

        /// <summary>
        /// Ends the NPCs' pending route cards, the framework's proposals held by their agents (#236),
        /// whenever a conversation opens or closes, as the quote and offer cards end. Null ends none.
        /// </summary>
        public Action EndPendingCards { get; set; }

        public void Attach(ConversationCoordinator conversations, TradeService service)
        {
            if (trade != null)
            {
                throw new InvalidOperationException("Already attached.");
            }

            coordinator = conversations ?? throw new ArgumentNullException(nameof(conversations));
            trade = service ?? throw new ArgumentNullException(nameof(service));
            coordinator.Changed += OnConversationChanged;
            trade.Confirmed += OnConfirmed;
        }

        /// <summary>
        /// Also ties the quest log to the conversation (G4): a quest offer's card goes away whenever a
        /// conversation opens or closes, and every quest receipt is written into the chat.
        /// </summary>
        public void AttachQuests(QuestLog questLog)
        {
            if (quests != null)
            {
                throw new InvalidOperationException("Quests already attached.");
            }

            quests = questLog ?? throw new ArgumentNullException(nameof(questLog));
            quests.Completed += OnQuestStep;
        }

        /// <summary>
        /// Also writes every G4c route receipt (a clue told, a permit granted, a card carried out) into
        /// the chat. The route cards themselves are the NPCs' proposals, ended through
        /// <see cref="EndPendingCards"/>.
        /// </summary>
        public void AttachRoutes(QuestRoutes questRoutes)
        {
            if (routes != null)
            {
                throw new InvalidOperationException("Routes already attached.");
            }

            routes = questRoutes ?? throw new ArgumentNullException(nameof(questRoutes));
            routes.Completed += OnQuestStep;
        }

        public void Dispose()
        {
            if (routes != null)
            {
                routes.Completed -= OnQuestStep;
                routes = null;
            }

            if (quests != null)
            {
                quests.Completed -= OnQuestStep;
                quests = null;
            }

            if (trade == null)
            {
                return;
            }

            coordinator.Changed -= OnConversationChanged;
            trade.Confirmed -= OnConfirmed;
            trade = null;
            coordinator = null;
        }

        /// <summary>The chat line for a receipt. Game-authored, never the model's statement.</summary>
        public static string ReceiptLine(TradeReceipt receipt, ItemCatalog catalog)
        {
            string item = receipt.ItemId != null && catalog.TryGet(receipt.ItemId, out var definition)
                ? definition.DisplayName
                : "the item";
            if (receipt.Applied)
            {
                return receipt.Direction == TradeDirection.Purchase
                    ? $"Bought {item} ×{receipt.ItemDelta} for {-receipt.GoldDelta} gold. Gold: {receipt.GoldAfter}."
                    : $"Sold {item} ×{-receipt.ItemDelta} for {receipt.GoldDelta} gold. Gold: {receipt.GoldAfter}.";
            }

            switch (receipt.Reason)
            {
                case TradeRejection.NotEnoughGold: return $"Not enough gold. Nothing changed hands. Gold: {receipt.GoldAfter}.";
                case TradeRejection.NoRoom: return "No room in your pack. Nothing changed hands.";
                case TradeRejection.NotEnoughItems: return $"You no longer have {item}. Nothing changed hands.";
                case TradeRejection.OutOfStock: return $"{item} is out of stock. Nothing changed hands.";
                case TradeRejection.Expired: return "That offer expired. Nothing changed hands; ask again for a new price.";
                case TradeRejection.StaleRevision: return "Things changed since that offer. Nothing changed hands; ask again for a new price.";
                default: return "That offer is no longer valid. Nothing changed hands.";
            }
        }

        private void OnConversationChanged(ConversationEvent change)
        {
            if (change.Kind == ConversationEventKind.Opened || change.Kind == ConversationEventKind.Closed)
            {
                trade.InvalidatePending();
                quests?.InvalidatePending();
                EndPendingCards?.Invoke();
            }
        }

        private void OnQuestStep(QuestReceipt receipt)
        {
            coordinator?.AddSystemLine(receipt.Line);
        }

        private void OnConfirmed(TradeReceipt receipt)
        {
            // A replay or a click on a stale card says so once, in the conversation still open, if any.
            coordinator.AddSystemLine(ReceiptLine(receipt, trade.Inventory.Catalog));
        }
    }
}

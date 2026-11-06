using System;
using System.Collections.Generic;

namespace LivingVale.Game
{
    /// <summary>
    /// The game's action and target IDs. Plain strings, so game code needs no framework type;
    /// <see cref="None"/> and <see cref="NoTarget"/> match the framework's own sentinels (a test checks).
    /// </summary>
    public static class GameActions
    {
        /// <summary>The framework's fallback, general conversation. Not registered by the game.</summary>
        public const string None = "none";
        public const string NoTarget = "no_target";

        public const string QuotePurchase = "quote_purchase";
        public const string QuoteSale = "quote_sale";

        /// <summary>Puts an authored quest contract on an inline card (G4). Accepting is the card's job, never the model's.</summary>
        public const string OfferQuest = "offer_quest";
    }

    /// <summary>A target as a driver would show it to a model: stable ID, short name, one-line description.</summary>
    public sealed class GameTargetOption
    {
        public string Id { get; }
        public string Name { get; }
        public string Description { get; }

        public GameTargetOption(string id, string name, string description)
        {
            Id = id;
            Name = name;
            Description = description ?? "";
        }

        public override string ToString() => Id;
    }

    /// <summary>One action an NPC can take right now, with only the targets it can take it on.</summary>
    public sealed class GameActionOption
    {
        public string ActionId { get; }
        public string Description { get; }
        public IReadOnlyList<GameTargetOption> Targets { get; }

        public GameActionOption(string actionId, string description, IReadOnlyList<GameTargetOption> targets)
        {
            ActionId = actionId;
            Description = description ?? "";
            Targets = targets;
        }

        public override string ToString() => $"{ActionId} [{string.Join(", ", Targets)}]";
    }

    /// <summary>What executing a decision did. <see cref="Executed"/> false means nothing changed.</summary>
    public sealed class ActionOutcome
    {
        public bool Executed { get; }
        public string ActionId { get; }
        public TradeRejection Reason { get; }

        /// <summary>The quote a <c>quote_*</c> action made, or null.</summary>
        public TradeQuote Quote { get; }

        /// <summary>The offer an <c>offer_quest</c> action put on a card, or null.</summary>
        public QuestOffer Offer { get; }

        /// <summary>Why an <c>offer_quest</c> was refused; <see cref="QuestRejection.None"/> otherwise.</summary>
        public QuestRejection QuestReason { get; }

        internal ActionOutcome(bool executed, string actionId, TradeRejection reason, TradeQuote quote)
        {
            Executed = executed;
            ActionId = actionId;
            Reason = reason;
            Quote = quote;
        }

        internal ActionOutcome(string actionId, QuestRejection questReason, QuestOffer offer)
        {
            Executed = questReason == QuestRejection.None;
            ActionId = actionId;
            Reason = Executed ? TradeRejection.None : TradeRejection.WrongTarget;
            QuestReason = questReason;
            Offer = offer;
        }

        public override string ToString() => Executed ? $"{ActionId} executed"
            : QuestReason != QuestRejection.None ? $"{ActionId} refused: {QuestReason}" : $"{ActionId} refused: {Reason}";
    }

    /// <summary>
    /// The one game-side executor every NPC decision driver shares, scripted or model-backed. It takes
    /// plain IDs (NPC, action, target, conversation generation), knows nothing about where the
    /// decision came from, and re-checks the action–target pair against current game state itself
    /// (design §9: one target list is never assumed to enforce valid pairs).
    /// </summary>
    /// <remarks>
    /// <see cref="AvailableFor"/> is what a driver registers: actions with no eligible target are
    /// left out, so an illegal option is absent rather than forbidden in prose.
    /// </remarks>
    public sealed class NpcActionExecutor
    {
        private readonly TradeService trade;
        private readonly QuestLog quests;

        /// <param name="quests">The quest log behind <c>offer_quest</c>; null leaves G3's trade-only executor.</param>
        public NpcActionExecutor(TradeService trade, QuestLog quests = null)
        {
            this.trade = trade ?? throw new ArgumentNullException(nameof(trade));
            this.quests = quests;
        }

        public TradeService Trade => trade;

        /// <summary>The quest log, or null when built without one.</summary>
        public QuestLog Quests => quests;

        /// <summary>
        /// The actions this NPC can take now and their targets, from game state at the time of the
        /// call. Excludes <see cref="GameActions.None"/>, which is always available. Ask again each
        /// turn: buying or selling changes it.
        /// </summary>
        public IReadOnlyList<GameActionOption> AvailableFor(string npcId)
        {
            var result = new List<GameActionOption>();
            AddQuestOffers(npcId, result);
            if (!trade.IsVendor(npcId))
            {
                return result;
            }

            var catalog = trade.Inventory.Catalog;
            var purchase = new List<GameTargetOption>();
            foreach (var entry in trade.PurchaseTargets(npcId))
            {
                var item = catalog.Get(entry.ItemId);
                purchase.Add(new GameTargetOption(entry.Id, $"{item.DisplayName} ×{entry.BundleSize}",
                    $"Buy {item.DisplayName.ToLowerInvariant()} ×{entry.BundleSize} for {entry.BundlePrice} gold ({entry.Remaining} in stock)"));
            }

            if (purchase.Count > 0)
            {
                result.Add(new GameActionOption(GameActions.QuotePurchase,
                    "Offer the player an exact price for one of your stock entries. Nothing changes hands until the player confirms.", purchase));
            }

            var sale = new List<GameTargetOption>();
            foreach (var bundle in trade.SaleTargets(npcId))
            {
                var item = catalog.Get(bundle.ItemId);
                sale.Add(new GameTargetOption(bundle.Id, $"Player's {item.DisplayName.ToLowerInvariant()} ×{bundle.BundleSize}",
                    $"Buy the player's {item.DisplayName.ToLowerInvariant()} ×{bundle.BundleSize} for {bundle.BundlePrice} gold"));
            }

            if (sale.Count > 0)
            {
                result.Add(new GameActionOption(GameActions.QuoteSale,
                    "Offer to buy something the player carries at an exact price. Nothing changes hands until the player confirms.", sale));
            }

            return result;
        }

        /// <summary>
        /// Executes a decision that the conversation layer has already found current. <c>none</c>
        /// always executes; a <c>quote_*</c> action makes a quote (no holdings change); anything else,
        /// or a target that does not fit, is refused with nothing changed.
        /// </summary>
        public ActionOutcome Execute(string npcId, string actionId, string targetId, long generation)
        {
            switch (actionId)
            {
                case GameActions.None:
                    return new ActionOutcome(true, actionId, TradeRejection.None, null);

                case GameActions.QuotePurchase:
                {
                    var rejection = trade.TryQuotePurchase(npcId, targetId, generation, out var quote);
                    return new ActionOutcome(rejection == TradeRejection.None, actionId, rejection, quote);
                }

                case GameActions.QuoteSale:
                {
                    var rejection = trade.TryQuoteSale(npcId, targetId, generation, out var quote);
                    return new ActionOutcome(rejection == TradeRejection.None, actionId, rejection, quote);
                }

                case GameActions.OfferQuest:
                {
                    if (quests == null)
                    {
                        return new ActionOutcome(actionId, QuestRejection.UnknownOffer, null);
                    }

                    var rejection = quests.TryOffer(npcId, targetId, generation, out var offer);
                    return new ActionOutcome(actionId, rejection, offer);
                }

                default:
                    return new ActionOutcome(false, actionId, TradeRejection.WrongTarget, null);
            }
        }

        private void AddQuestOffers(string npcId, List<GameActionOption> result)
        {
            if (quests == null)
            {
                return;
            }

            var targets = new List<GameTargetOption>();
            foreach (var offer in quests.OffersFor(npcId))
            {
                targets.Add(new GameTargetOption(offer.Id, offer.Title, $"Ask the traveler to take on \"{offer.Title}\": {offer.Summary}"));
            }

            if (targets.Count > 0)
            {
                result.Add(new GameActionOption(GameActions.OfferQuest,
                    "Ask the traveler for help with your task. The traveler decides on the card; nothing is agreed until they accept.", targets));
            }
        }
    }
}

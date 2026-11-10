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

        /// <summary>G4c: tells the traveler a fact (a <see cref="KnowledgeCard"/>), which records its clue. Executes at once.</summary>
        public const string ShareInformation = "share_information";

        /// <summary>G4c: proposes a deal with fixed terms on a card (Kest's release, a division agreement).</summary>
        public const string OfferExchange = "offer_exchange";

        /// <summary>G4c: proposes handing a quest item over on a card (the parcel's three dispositions, the side quests' hand-ins).</summary>
        public const string ResolveDelivery = "resolve_delivery";

        /// <summary>G4c: grants one access flag (Brann's delivery permit). Executes at once.</summary>
        public const string GrantAccess = "grant_access";
    }

    /// <summary>
    /// A target as a driver would show it to a model: stable ID, short name, one-line description,
    /// and the words a few-shot example calls it by (<see cref="global::Agenerela.INamedTarget"/>, #234).
    /// </summary>
    public sealed class GameTargetOption : global::Agenerela.IDescribedTarget, global::Agenerela.INamedTarget
    {
        public string Id { get; }
        public string Name { get; }
        public string Description { get; }

        /// <summary>What a player would call it inside a sentence: "healing potion ×1", "wolf pelt ×1". Defaults to <see cref="Name"/>.</summary>
        public string ExampleName { get; }

        public GameTargetOption(string id, string name, string description, string exampleName = null)
        {
            Id = id;
            Name = name;
            Description = description ?? "";
            ExampleName = string.IsNullOrWhiteSpace(exampleName) ? name : exampleName;
        }

        string global::Agenerela.INamedTarget.Name => ExampleName;

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

        /// <summary>The card a G4c <c>offer_quest</c>, <c>offer_exchange</c> or <c>resolve_delivery</c> made, or null.</summary>
        public RouteProposal Proposal { get; private set; }

        internal static ActionOutcome ForRoute(string actionId, QuestRejection questReason, RouteProposal proposal) =>
            new ActionOutcome(actionId, questReason, (QuestOffer)null) { Proposal = proposal };

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
        private readonly QuestRoutes routes;

        /// <param name="quests">The quest log behind <c>offer_quest</c>; null leaves G3's trade-only executor.</param>
        /// <param name="routes">G4c's routes and side quests over the same quest log; null leaves G4a's executor.</param>
        public NpcActionExecutor(TradeService trade, QuestLog quests = null, QuestRoutes routes = null)
        {
            this.trade = trade ?? throw new ArgumentNullException(nameof(trade));
            this.quests = quests;
            if (routes != null && routes.Quests != quests)
            {
                throw new ArgumentException("The routes must wrap the same quest log.", nameof(routes));
            }

            this.routes = routes;
        }

        /// <summary>G4c's routes, or null when built without them.</summary>
        public QuestRoutes Routes => routes;

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
            AddRouteActions(npcId, result);
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
                    $"Buy {item.DisplayName.ToLowerInvariant()} ×{entry.BundleSize} for {entry.BundlePrice} gold ({entry.Remaining} in stock)",
                    $"{LowerFirst(item.DisplayName)} ×{entry.BundleSize}"));
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
                    $"Buy the player's {item.DisplayName.ToLowerInvariant()} ×{bundle.BundleSize} for {bundle.BundlePrice} gold",
                    $"{item.DisplayName.ToLowerInvariant()} ×{bundle.BundleSize}"));
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

                    if (routes != null && routes.Owns(actionId, targetId))
                    {
                        var routed = routes.TryPropose(npcId, actionId, targetId, generation, out var proposal);
                        return ActionOutcome.ForRoute(actionId, routed, proposal);
                    }

                    var rejection = quests.TryOffer(npcId, targetId, generation, out var offer);
                    return new ActionOutcome(actionId, rejection, offer);
                }

                case GameActions.OfferExchange:
                case GameActions.ResolveDelivery:
                {
                    if (routes == null)
                    {
                        return ActionOutcome.ForRoute(actionId, QuestRejection.UnknownOffer, null);
                    }

                    var rejection = routes.TryPropose(npcId, actionId, targetId, generation, out var proposal);
                    return ActionOutcome.ForRoute(actionId, rejection, proposal);
                }

                case GameActions.ShareInformation:
                    return ActionOutcome.ForRoute(actionId,
                        routes == null ? QuestRejection.UnknownOffer : routes.TryShare(npcId, targetId, generation), null);

                case GameActions.GrantAccess:
                    return ActionOutcome.ForRoute(actionId,
                        routes == null ? QuestRejection.UnknownOffer : routes.TryGrant(npcId, targetId, generation), null);

                default:
                    return new ActionOutcome(false, actionId, TradeRejection.WrongTarget, null);
            }
        }

        private static string LowerFirst(string text) => string.IsNullOrEmpty(text) ? "" : char.ToLowerInvariant(text[0]) + text.Substring(1);

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

            // G4c's side-quest offers join the same action, so one offer_quest covers every quest an NPC gives.
            if (routes != null)
            {
                foreach (var contract in routes.ContractsFor(npcId, GameActions.OfferQuest))
                {
                    targets.Add(new GameTargetOption(contract.Id, contract.Title, $"Ask the traveler to take on \"{contract.Title}\": {contract.Description}"));
                }
            }

            if (targets.Count > 0)
            {
                result.Add(new GameActionOption(GameActions.OfferQuest,
                    "Ask the traveler for help with your task. The traveler decides on the card; nothing is agreed until they accept.", targets));
            }
        }
        // G4c: the route actions, each listed only with the targets this NPC can use now.
        private void AddRouteActions(string npcId, List<GameActionOption> result)
        {
            if (routes == null)
            {
                return;
            }

            var share = new List<GameTargetOption>();
            foreach (var card in routes.KnowledgeFor(npcId))
            {
                share.Add(new GameTargetOption(card.Id, card.Title, card.Description));
            }

            if (share.Count > 0)
            {
                result.Add(new GameActionOption(GameActions.ShareInformation,
                    "Tell the traveler something you saw or know, when they ask about it. Telling it is what makes it count.", share));
            }

            AddContracts(npcId, GameActions.OfferExchange,
                "Propose a deal with fixed terms; the traveler accepts or declines on a card. Nothing is agreed until they do.", result);
            AddContracts(npcId, GameActions.ResolveDelivery,
                "Take something the traveler brings you; they confirm the hand-over on a card. Nothing changes hands until they do.", result);

            var access = new List<GameTargetOption>();
            foreach (var grant in routes.GrantsFor(npcId))
            {
                access.Add(new GameTargetOption(grant.Id, grant.Title, grant.Description));
            }

            if (access.Count > 0)
            {
                result.Add(new GameActionOption(GameActions.GrantAccess,
                    "Let the traveler through: grant the access below. Only when they have earned it.", access));
            }
        }

        private void AddContracts(string npcId, string actionId, string description, List<GameActionOption> result)
        {
            var targets = new List<GameTargetOption>();
            foreach (var contract in routes.ContractsFor(npcId, actionId))
            {
                targets.Add(new GameTargetOption(contract.Id, contract.Title, $"{contract.Description} Terms: {contract.Terms}"));
            }

            if (targets.Count > 0)
            {
                result.Add(new GameActionOption(actionId, description, targets));
            }
        }
    }
}

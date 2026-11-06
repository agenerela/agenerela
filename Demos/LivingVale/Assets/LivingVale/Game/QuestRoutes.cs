using System;
using System.Collections.Generic;

namespace LivingVale.Game
{
    /// <summary>What a route card asks the player to confirm.</summary>
    public enum RouteProposalKind
    {
        /// <summary>A side quest, accepted on the card (<c>offer_quest</c>).</summary>
        QuestOffer,

        /// <summary>A deal with fixed terms: Kest's release, or one party's agreement to the division (<c>offer_exchange</c>).</summary>
        Exchange,

        /// <summary>Handing a quest item to its recipient (<c>resolve_delivery</c>).</summary>
        Delivery
    }

    /// <summary>
    /// One authored contract an NPC can put on a card: a side-quest offer, an exchange or a delivery.
    /// The NPC's decision only puts it on the card; <see cref="QuestRoutes.Confirm"/>, from the card's
    /// button, carries it out after checking every condition again.
    /// </summary>
    public sealed class RouteContract
    {
        public string Id { get; }
        public string NpcId { get; }
        public string ActionId { get; }
        public RouteProposalKind Kind { get; }
        public string Title { get; }

        /// <summary>What the target means, for the model's prompt and the card.</summary>
        public string Description { get; }

        /// <summary>The fixed terms the card states: cost and reward. Authored, never the model's words.</summary>
        public string Terms { get; }

        /// <summary>The card's confirm button.</summary>
        public string ConfirmLabel { get; }

        internal Func<bool> Available { get; }
        internal Func<QuestReceipt> Apply { get; }

        internal RouteContract(string id, string npcId, string actionId, RouteProposalKind kind, string title,
            string description, string terms, string confirmLabel, Func<bool> available, Func<QuestReceipt> apply)
        {
            Id = id;
            NpcId = npcId;
            ActionId = actionId;
            Kind = kind;
            Title = title;
            Description = description;
            Terms = terms;
            ConfirmLabel = confirmLabel;
            Available = available;
            Apply = apply;
        }

        public override string ToString() => Id;
    }

    /// <summary>A contract on the chat card, tied to the conversation it was proposed in, like a trade quote.</summary>
    public sealed class RouteProposal
    {
        public string Id { get; }
        public RouteContract Contract { get; }
        public long ConversationGeneration { get; }
        public QuestOfferState State { get; internal set; }

        public string NpcId => Contract.NpcId;

        internal RouteProposal(string id, RouteContract contract, long generation)
        {
            Id = id;
            Contract = contract;
            ConversationGeneration = generation;
            State = QuestOfferState.Pending;
        }

        public override string ToString() => $"{Id} ({State})";
    }

    /// <summary>A fact an NPC can tell through <c>share_information</c>. Telling it records a clue; saying "I know" does not.</summary>
    public sealed class KnowledgeCard
    {
        public string Id { get; }
        public string NpcId { get; }
        public string Title { get; }
        public string Description { get; }

        /// <summary>The clues it records, in order.</summary>
        public IReadOnlyList<string> Clues { get; }

        /// <summary>The game's line when it is first told.</summary>
        public string ReceiptLine { get; }

        internal KnowledgeCard(string id, string npcId, string title, string description, string receiptLine, params string[] clues)
        {
            Id = id;
            NpcId = npcId;
            Title = title;
            Description = description;
            ReceiptLine = receiptLine;
            Clues = clues;
        }

        public override string ToString() => Id;
    }

    /// <summary>One access flag an NPC can grant through <c>grant_access</c> while its conditions hold.</summary>
    public sealed class AccessGrant
    {
        public string Id { get; }
        public string NpcId { get; }
        public string Title { get; }
        public string Description { get; }
        internal Func<bool> Available { get; }

        internal AccessGrant(string id, string npcId, string title, string description, Func<bool> available)
        {
            Id = id;
            NpcId = npcId;
            Title = title;
            Description = description;
            Available = available;
        }

        public override string ToString() => Id;
    }

    /// <summary>A world object the player uses with E: a herb spot, the wreck, the badge, the parcel.</summary>
    public sealed class QuestPickup
    {
        public string Id { get; }
        public string PlaceId { get; }
        public string DisplayName { get; }

        /// <summary>The prompt verb: "Pick", "Investigate", "Take".</summary>
        public string Verb { get; }

        internal Func<bool> Done { get; }
        internal Func<bool> Allowed { get; }
        internal Func<QuestReceipt> Apply { get; }

        /// <summary>Shown when the pickup is not allowed yet, so the player knows why.</summary>
        public string LockedLine { get; }

        internal QuestPickup(string id, string placeId, string displayName, string verb, string lockedLine,
            Func<bool> done, Func<bool> allowed, Func<QuestReceipt> apply)
        {
            Id = id;
            PlaceId = placeId;
            DisplayName = displayName;
            Verb = verb;
            LockedLine = lockedLine;
            Done = done;
            Allowed = allowed;
            Apply = apply;
        }

        public override string ToString() => Id;
    }

    /// <summary>
    /// G4c: the main quest's recovery routes and endings, and both side quests, as authored contracts
    /// over <see cref="QuestLog"/>. Every NPC action here (<c>share_information</c>,
    /// <c>offer_exchange</c>, <c>resolve_delivery</c>, <c>grant_access</c>, and the side quests'
    /// <c>offer_quest</c>) is listed only while game state allows it, and every effect goes through
    /// <see cref="QuestLog"/> and the inventory with fixed event IDs, so a repeat, a double click or a
    /// reload never pays twice. Nothing an NPC says changes state: Fen's clue is real only once she
    /// tells it through <c>share_information</c>, and Kest's testimony deal is absent until then.
    /// </summary>
    /// <remarks>
    /// Saved state lives in <see cref="QuestLog"/>: clues, agreements and the permit are clue IDs,
    /// pickups are inventory event IDs. Cards are never saved; loading invalidates the pending one.
    /// Plain C#, no scene. Main thread only.
    /// </remarks>
    public sealed class QuestRoutes
    {
        public const string FenId = "npc_fen";
        public const string KestId = "npc_kest";
        public const string BrannId = "npc_brann";
        public const string ElraId = "npc_elra";
        public const string IvenId = QuestLog.IvenId;

        public const string KnowledgeFenRaid = "knowledge_fen_raid";

        public const string ExchangeKestPayment = "exchange_kest_payment";
        public const string ExchangeKestFavour = "exchange_kest_favour";
        public const string ExchangeKestTestimony = "exchange_kest_testimony";
        public const string ExchangeDivisionIven = "exchange_division_iven";
        public const string ExchangeDivisionElra = "exchange_division_elra";

        public const string DeliveryParcelIven = "delivery_parcel_iven";
        public const string DeliveryParcelElra = "delivery_parcel_elra";
        public const string DeliveryDivisionIven = "delivery_division_iven";
        public const string DeliveryDivisionElra = "delivery_division_elra";
        public const string DeliveryHerbsIven = "delivery_herbs_iven";
        public const string DeliveryBadgeBrann = "delivery_badge_brann";

        public const string OfferHerbsIven = "offer_iven_herbs";
        public const string OfferBadgeBrann = "offer_brann_badge";

        public const string PickupWreck = "pickup_wreck";
        public const string PickupBadge = "pickup_badge";
        public const string PickupHerb1 = "pickup_herb_1";
        public const string PickupHerb2 = "pickup_herb_2";
        public const string PickupHerb3 = "pickup_herb_3";
        public const string PickupParcel = "pickup_parcel";

        /// <summary>The wreck's landmark XP (content list: 10 per landmark).</summary>
        public const string DiscoverWreckEvent = "discover_wreck";
        public const int LandmarkXp = 10;

        /// <summary>Kest's fixed supply payment: affordable from the starting 40 gold.</summary>
        public const int KestPayment = 20;

        /// <summary>The camp defenders at Broken Watch. All defeated records <see cref="QuestLog.ClueCampCleared"/>.</summary>
        public static readonly IReadOnlyList<string> DefenderIds = new[] { "enemy_camp_defender_1", "enemy_camp_defender_2", "enemy_camp_defender_3" };

        private readonly QuestLog quests;
        private readonly PlayerInventory inventory;
        private readonly ITradeSession session;
        private readonly List<RouteContract> contracts = new List<RouteContract>();
        private readonly List<KnowledgeCard> knowledge = new List<KnowledgeCard>();
        private readonly List<AccessGrant> grants = new List<AccessGrant>();
        private readonly List<QuestPickup> pickups = new List<QuestPickup>();
        private readonly HashSet<string> defeatedDefenders = new HashSet<string>();
        private RouteProposal pending;
        private int proposalCount;

        public QuestRoutes(QuestLog quests, ITradeSession session)
        {
            this.quests = quests ?? throw new ArgumentNullException(nameof(quests));
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            inventory = quests.Inventory;
            AddKnowledge();
            AddContracts();
            AddGrants();
            AddPickups();

            // A load can restore a save from before the camp fell while the defenders stay down in
            // this session (G2's enemies never get up). Re-derive the clue so the combat route stays open.
            quests.Changed += ReconcileCamp;
        }

        public QuestLog Quests => quests;

        /// <summary>The proposal on the chat card, or null.</summary>
        public RouteProposal ActiveProposal => pending != null && pending.State == QuestOfferState.Pending ? pending : null;

        public IReadOnlyList<QuestPickup> Pickups => pickups;

        /// <summary>Raised after every committed change, a card appearing or ending included.</summary>
        public event Action Changed;

        /// <summary>Raised for every step attempted here, applied or refused: the line the chat or the HUD shows.</summary>
        public event Action<QuestReceipt> Completed;

        public bool HasPermit => quests.HasClue(QuestLog.AccessDeliveryPermit);
        public bool CampCleared => quests.HasClue(QuestLog.ClueCampCleared);
        private bool Recovered => quests.MainState == MainQuestState.ParcelRecovered;
        private bool NotYetRecovered => quests.MainState != MainQuestState.ParcelRecovered && quests.MainState != MainQuestState.Resolved;

        // ---- What each NPC can do now ----

        public IReadOnlyList<KnowledgeCard> KnowledgeFor(string npcId)
        {
            var result = new List<KnowledgeCard>();
            foreach (var card in knowledge)
            {
                if (card.NpcId == npcId && !AllRecorded(card))
                {
                    result.Add(card);
                }
            }

            return result;
        }

        /// <summary>The contracts this NPC can put on a card now through <paramref name="actionId"/>.</summary>
        public IReadOnlyList<RouteContract> ContractsFor(string npcId, string actionId)
        {
            var result = new List<RouteContract>();
            foreach (var contract in contracts)
            {
                if (contract.NpcId == npcId && contract.ActionId == actionId && contract.Available())
                {
                    result.Add(contract);
                }
            }

            return result;
        }

        public IReadOnlyList<AccessGrant> GrantsFor(string npcId)
        {
            var result = new List<AccessGrant>();
            foreach (var grant in grants)
            {
                if (grant.NpcId == npcId && grant.Available())
                {
                    result.Add(grant);
                }
            }

            return result;
        }

        /// <summary>True when <paramref name="targetId"/> is one of this class's targets for that action, available or not.</summary>
        public bool Owns(string actionId, string targetId)
        {
            if (actionId == GameActions.ShareInformation)
            {
                return knowledge.Exists(k => k.Id == targetId);
            }

            if (actionId == GameActions.GrantAccess)
            {
                return grants.Exists(g => g.Id == targetId);
            }

            return contracts.Exists(c => c.Id == targetId && c.ActionId == actionId);
        }

        // ---- Executing NPC actions ----

        /// <summary><c>share_information</c>: records the card's clues. Refused unless this NPC can tell it now, in this conversation.</summary>
        public QuestRejection TryShare(string npcId, string cardId, long generation)
        {
            var card = knowledge.Find(k => k.Id == cardId);
            if (card == null || card.NpcId != npcId)
            {
                return QuestRejection.UnknownOffer;
            }

            if (!IsCurrent(npcId, generation))
            {
                return QuestRejection.StaleConversation;
            }

            if (AllRecorded(card))
            {
                return QuestRejection.WrongState;
            }

            foreach (var clue in card.Clues)
            {
                quests.RecordClue(clue);
            }

            Report(QuestLog.MainQuestId, null, QuestRejection.None, card.ReceiptLine);
            return QuestRejection.None;
        }

        /// <summary><c>grant_access</c>: sets the flag. Refused unless its conditions hold now, in this conversation.</summary>
        public QuestRejection TryGrant(string npcId, string grantId, long generation)
        {
            var grant = grants.Find(g => g.Id == grantId);
            if (grant == null || grant.NpcId != npcId)
            {
                return QuestRejection.UnknownOffer;
            }

            if (!IsCurrent(npcId, generation))
            {
                return QuestRejection.StaleConversation;
            }

            if (!grant.Available())
            {
                return QuestRejection.WrongState;
            }

            quests.RecordClue(grant.Id);
            Report(QuestLog.MainQuestId, null, QuestRejection.None, $"Granted: {grant.Title}.");
            return QuestRejection.None;
        }

        /// <summary>
        /// <c>offer_quest</c>, <c>offer_exchange</c> or <c>resolve_delivery</c>: puts the contract on a
        /// card for this conversation, replacing any earlier card. Nothing else changes.
        /// </summary>
        public QuestRejection TryPropose(string npcId, string actionId, string contractId, long generation, out RouteProposal proposal)
        {
            proposal = null;
            var contract = contracts.Find(c => c.Id == contractId && c.ActionId == actionId);
            if (contract == null || contract.NpcId != npcId)
            {
                return QuestRejection.UnknownOffer;
            }

            if (!IsCurrent(npcId, generation))
            {
                return QuestRejection.StaleConversation;
            }

            if (!contract.Available())
            {
                return QuestRejection.WrongState;
            }

            if (pending != null && pending.State == QuestOfferState.Pending)
            {
                pending.State = QuestOfferState.Invalidated;
            }

            proposalCount++;
            pending = new RouteProposal($"{contract.Id}#{proposalCount}", contract, generation);
            proposal = pending;
            GameEvents.Raise(Changed);
            return QuestRejection.None;
        }

        /// <summary>
        /// The card's confirm button: carries out the pending proposal if it is still this
        /// conversation's and its conditions still hold. The effect's own event ID makes a repeat a no-op.
        /// </summary>
        public QuestReceipt Confirm(string proposalId)
        {
            var proposal = ActiveProposal;
            string questId = proposal != null ? QuestFor(proposal.Contract) : QuestLog.MainQuestId;
            if (proposal == null || proposal.Id != proposalId)
            {
                return Report(questId, null, QuestRejection.OfferNotPending, "That offer is no longer open.");
            }

            if (!IsCurrent(proposal.NpcId, proposal.ConversationGeneration))
            {
                proposal.State = QuestOfferState.Invalidated;
                return Report(questId, null, QuestRejection.StaleConversation, "That offer is no longer open.");
            }

            if (!proposal.Contract.Available())
            {
                proposal.State = QuestOfferState.Invalidated;
                return Report(questId, null, QuestRejection.WrongState, "Things changed; that offer no longer stands.");
            }

            var receipt = proposal.Contract.Apply();
            if (receipt.Applied)
            {
                proposal.State = QuestOfferState.Accepted;
                GameEvents.Raise(Changed);
            }

            return receipt;
        }

        /// <summary>The card's decline button.</summary>
        public bool Decline(string proposalId)
        {
            var proposal = ActiveProposal;
            if (proposal == null || proposal.Id != proposalId)
            {
                return false;
            }

            proposal.State = QuestOfferState.Declined;
            GameEvents.Raise(Changed);
            return true;
        }

        /// <summary>The conversation opened, closed or a save loaded: the card goes away.</summary>
        public void InvalidatePending()
        {
            if (pending != null && pending.State == QuestOfferState.Pending)
            {
                pending.State = QuestOfferState.Invalidated;
                GameEvents.Raise(Changed);
            }
        }

        // ---- World ----

        public QuestPickup FindPickup(string pickupId) => pickups.Find(p => p.Id == pickupId);

        /// <summary>True once the pickup was used; it then stays used, across saves too.</summary>
        public bool IsDone(string pickupId) => FindPickup(pickupId)?.Done() ?? false;

        /// <summary>True when E would do something now.</summary>
        public bool CanUse(string pickupId)
        {
            var pickup = FindPickup(pickupId);
            return pickup != null && !pickup.Done() && pickup.Allowed();
        }

        /// <summary>E on a world object. Refused, with its locked line, while not allowed; a used one does nothing.</summary>
        public QuestReceipt TryPickUp(string pickupId)
        {
            var pickup = FindPickup(pickupId);
            if (pickup == null)
            {
                return Report(QuestLog.MainQuestId, null, QuestRejection.UnknownOffer, "There is nothing to take.");
            }

            if (pickup.Done())
            {
                return Report(QuestLog.MainQuestId, pickup.Id, QuestRejection.DuplicateEvent, $"{pickup.DisplayName}: already done.");
            }

            if (!pickup.Allowed())
            {
                return Report(QuestLog.MainQuestId, pickup.Id, QuestRejection.WrongState, pickup.LockedLine);
            }

            return pickup.Apply();
        }

        /// <summary>
        /// Combat reports each defeated enemy. Once every camp defender is down, the camp is cleared
        /// (a saved clue) and the parcel can be taken. Other enemies are ignored.
        /// </summary>
        public void ReportDefeated(string enemyId)
        {
            if (enemyId == null || !Contains(DefenderIds, enemyId) || !defeatedDefenders.Add(enemyId) || CampCleared || !AllDefendersDown())
            {
                return;
            }

            quests.RecordClue(QuestLog.ClueCampCleared);
            Report(QuestLog.MainQuestId, null, QuestRejection.None, "Broken Watch is clear. The parcel is unguarded.");
        }

        private void ReconcileCamp()
        {
            if (!CampCleared && AllDefendersDown())
            {
                quests.RecordClue(QuestLog.ClueCampCleared);
            }
        }

        private bool AllDefendersDown()
        {
            foreach (var id in DefenderIds)
            {
                if (!defeatedDefenders.Contains(id))
                {
                    return false;
                }
            }

            return true;
        }

        // ---- Journal ----

        /// <summary>The journal: the main quest, how far the routes have got, and the side quests, in the player's words.</summary>
        public IReadOnlyList<string> JournalLines()
        {
            var lines = new List<string> { quests.JournalLine };
            switch (quests.MainState)
            {
                case MainQuestState.Undiscovered:
                case MainQuestState.Offered:
                case MainQuestState.Active:
                    var leads = new List<string>();
                    if (quests.HasClue(QuestLog.ClueWreckSeen)) leads.Add("wreck searched");
                    if (quests.HasClue(QuestLog.ClueFenTestimony)) leads.Add("Fen's account");
                    if (quests.HasClue(QuestLog.ClueCampLocated)) leads.Add("camp at Broken Watch");
                    if (CampCleared) leads.Add("camp cleared");
                    if (leads.Count > 0)
                    {
                        lines.Add("  Leads: " + string.Join(", ", leads) + ".");
                    }

                    break;
                case MainQuestState.ParcelRecovered:
                    lines.Add($"  Recovered by {RouteName(quests.Route)}.");
                    var choices = new List<string> { "Iven" };
                    choices.Add(HasPermit ? "Elra" : "Elra (permit from Brann)");
                    if (quests.HasClue(QuestLog.AgreementHealer) || quests.HasClue(QuestLog.AgreementGarrison))
                    {
                        choices.Add($"division (Iven {(quests.HasClue(QuestLog.AgreementHealer) ? "agreed" : "not yet")}, " +
                                    $"Elra {(quests.HasClue(QuestLog.AgreementGarrison) ? "agreed" : "not yet")})");
                    }

                    lines.Add("  Deliver to: " + string.Join(" · ", choices) + ".");
                    break;
                default:
                    lines.Add($"  Recovered by {RouteName(quests.Route)}.");
                    break;
            }

            if (quests.HerbsState != SideQuestState.Undiscovered)
            {
                lines.Add(quests.HerbsState == SideQuestState.Completed
                    ? "Herbs Before Sundown: done."
                    : $"Herbs Before Sundown: herbs {Math.Min(inventory.CountOf(ItemCatalog.HerbBundle), QuestLog.HerbsNeeded)}/{QuestLog.HerbsNeeded}, then Iven (no deadline).");
            }

            if (quests.BadgeState != SideQuestState.Undiscovered)
            {
                lines.Add(quests.BadgeState == SideQuestState.Completed
                    ? "A Dented Badge: done."
                    : inventory.HasApplied(QuestLog.BadgeFoundEvent)
                        ? "A Dented Badge: return the badge to Brann."
                        : "A Dented Badge: search the Old Road wreck.");
            }

            return lines;
        }

        public static string RouteName(RecoveryRoute route)
        {
            switch (route)
            {
                case RecoveryRoute.Combat: return "force (camp cleared)";
                case RecoveryRoute.Negotiation: return "negotiation with Kest";
                case RecoveryRoute.Investigation: return "Fen's evidence";
                case RecoveryRoute.FoundEarly: return "finding it early";
                default: return "—";
            }
        }

        // ---- Content ----

        private void AddKnowledge()
        {
            knowledge.Add(new KnowledgeCard(KnowledgeFenRaid, FenId, "What Fen saw on the Old Road",
                "Tell the traveler what you saw: camp raiders from Broken Watch took the wagon, and their leader Kest kept the medicine for the camp's own sick.",
                "Clue recorded: Fen saw Kest's raiders take the wagon to Broken Watch, for their own sick.",
                QuestLog.ClueFenTestimony, QuestLog.ClueCampLocated));
        }

        private void AddContracts()
        {
            // Kest's release: three authored conditions, one parcel, one event ID.
            contracts.Add(new RouteContract(ExchangeKestPayment, KestId, GameActions.OfferExchange, RouteProposalKind.Exchange,
                "Buy back the parcel",
                $"Release the medical parcel for a fixed supply payment of {KestPayment} gold.",
                $"You pay {KestPayment} gold; Kest hands over the medical parcel.", $"Pay {KestPayment} gold",
                () => NotYetRecovered,
                () => quests.TryRecoverParcel(RecoveryRoute.Negotiation, -KestPayment)));
            contracts.Add(new RouteContract(ExchangeKestFavour, KestId, GameActions.OfferExchange, RouteProposalKind.Exchange,
                "Release for the herb favour",
                "Release the medical parcel because the traveler gathered herbs for Iven's patients, a favour Iven vouches for.",
                "Kest hands over the medical parcel, free, for the herbs you brought Iven.", "Take the parcel",
                () => NotYetRecovered && quests.HerbsState == SideQuestState.Completed,
                () => quests.TryRecoverParcel(RecoveryRoute.Negotiation)));
            contracts.Add(new RouteContract(ExchangeKestTestimony, KestId, GameActions.OfferExchange, RouteProposalKind.Exchange,
                "Release on Fen's account",
                "Release the medical parcel because a witness, Fen, saw your raid; holding it now only brings the garrison down on the camp.",
                "Kest hands over the medical parcel, free, rather than face Fen's account at the castle.", "Take the parcel",
                () => NotYetRecovered && quests.HasClue(QuestLog.ClueFenTestimony),
                () => quests.TryRecoverParcel(RecoveryRoute.Investigation)));

            // The division needs both parties' recorded agreement, each on its own card.
            contracts.Add(new RouteContract(ExchangeDivisionIven, IvenId, GameActions.OfferExchange, RouteProposalKind.Exchange,
                "Agree to divide the parcel",
                "Agree that the medical parcel is divided between your patients and the castle garrison.",
                "Iven agrees to the division; the garrison must agree too.", "Record Iven's agreement",
                () => Recovered && !quests.HasClue(QuestLog.AgreementHealer),
                () => Agree(QuestLog.AgreementHealer, "Iven agreed to divide the parcel.")));
            contracts.Add(new RouteContract(ExchangeDivisionElra, ElraId, GameActions.OfferExchange, RouteProposalKind.Exchange,
                "Agree to divide the parcel",
                "Agree that the medical parcel is divided between the garrison and Iven's patients.",
                "Elra agrees to the division for the garrison; Iven must agree too.", "Record Elra's agreement",
                () => Recovered && HasPermit && !quests.HasClue(QuestLog.AgreementGarrison),
                () => Agree(QuestLog.AgreementGarrison, "Elra agreed to divide the parcel for the garrison.")));

            // The three dispositions.
            contracts.Add(new RouteContract(DeliveryParcelIven, IvenId, GameActions.ResolveDelivery, RouteProposalKind.Delivery,
                "Deliver the parcel to Iven",
                "Take the whole medical parcel for your patients.",
                $"The parcel goes to Iven. +{QuestLog.HealerGold} gold, +{QuestLog.HealerPotions} potions, +{QuestLog.MainQuestXp} XP.", "Give Iven the parcel",
                () => Recovered,
                () => quests.TryResolve(ParcelDisposition.Healer)));
            contracts.Add(new RouteContract(DeliveryParcelElra, ElraId, GameActions.ResolveDelivery, RouteProposalKind.Delivery,
                "Deliver the parcel to the garrison",
                "Take the whole medical parcel for the castle garrison.",
                $"The parcel goes to the garrison. +{QuestLog.GarrisonGold} gold, +{QuestLog.MainQuestXp} XP.", "Give Elra the parcel",
                () => Recovered && HasPermit,
                () => quests.TryResolve(ParcelDisposition.Garrison)));
            Func<bool> divisionReady = () => Recovered && quests.HasClue(QuestLog.AgreementHealer) && quests.HasClue(QuestLog.AgreementGarrison);
            string divisionTerms = $"The parcel is split as both agreed. +{QuestLog.DivisionGold} gold, +{QuestLog.DivisionPotions} potion, +{QuestLog.MainQuestXp} XP.";
            contracts.Add(new RouteContract(DeliveryDivisionIven, IvenId, GameActions.ResolveDelivery, RouteProposalKind.Delivery,
                "Divide the parcel as agreed",
                "Receive your agreed share of the medical parcel; the rest goes to the garrison.",
                divisionTerms, "Divide the parcel",
                divisionReady,
                () => quests.TryResolve(ParcelDisposition.Division)));
            contracts.Add(new RouteContract(DeliveryDivisionElra, ElraId, GameActions.ResolveDelivery, RouteProposalKind.Delivery,
                "Divide the parcel as agreed",
                "Receive the garrison's agreed share of the medical parcel; the rest goes to Iven.",
                divisionTerms, "Divide the parcel",
                () => divisionReady() && HasPermit,
                () => quests.TryResolve(ParcelDisposition.Division)));

            // Side quests: an offer each, then a hand-in.
            contracts.Add(new RouteContract(OfferHerbsIven, IvenId, GameActions.OfferQuest, RouteProposalKind.QuestOffer,
                "Herbs Before Sundown",
                $"Ask the traveler to gather {QuestLog.HerbsNeeded} herbs in Herb Hollow for your patients. There is no deadline.",
                $"Bring Iven {QuestLog.HerbsNeeded} herb bundles. +{QuestLog.HerbsPotions} potion, +{QuestLog.SideQuestXp} XP. No deadline.", "Accept",
                () => quests.HerbsState == SideQuestState.Undiscovered,
                () => StartSide(QuestLog.HerbsQuestId, "Quest accepted: Herbs Before Sundown.")));
            contracts.Add(new RouteContract(OfferBadgeBrann, BrannId, GameActions.OfferQuest, RouteProposalKind.QuestOffer,
                "A Dented Badge",
                "Ask the traveler to look for your lost badge near the wagon wreck on the Old Road.",
                $"Find Brann's badge at the Old Road wreck. +{QuestLog.BadgeGold} gold, +{QuestLog.SideQuestXp} XP.", "Accept",
                () => quests.BadgeState == SideQuestState.Undiscovered && !inventory.HasApplied(QuestLog.BadgeFoundEvent),
                () => StartSide(QuestLog.BadgeQuestId, "Quest accepted: A Dented Badge.")));
            contracts.Add(new RouteContract(DeliveryHerbsIven, IvenId, GameActions.ResolveDelivery, RouteProposalKind.Delivery,
                "Hand Iven the herbs",
                $"Take the {QuestLog.HerbsNeeded} herb bundles the traveler gathered.",
                $"You give {QuestLog.HerbsNeeded} herb bundles. +{QuestLog.HerbsPotions} potion, +{QuestLog.SideQuestXp} XP.", "Give the herbs",
                () => quests.HerbsState == SideQuestState.Active && inventory.CountOf(ItemCatalog.HerbBundle) >= QuestLog.HerbsNeeded,
                () => quests.TryCompleteHerbs()));
            contracts.Add(new RouteContract(DeliveryBadgeBrann, BrannId, GameActions.ResolveDelivery, RouteProposalKind.Delivery,
                "Return the badge",
                "Take back your lost badge from the traveler.",
                $"You return the badge. +{QuestLog.BadgeGold} gold, +{QuestLog.SideQuestXp} XP.", "Return the badge",
                () => quests.BadgeState != SideQuestState.Completed && inventory.CountOf(ItemCatalog.LostBadge) > 0,
                ReturnBadge));
        }

        private void AddGrants()
        {
            grants.Add(new AccessGrant(QuestLog.AccessDeliveryPermit, BrannId, "a delivery permit to the steward",
                "Give the traveler a delivery permit, so they can hand the recovered medical parcel to steward Elra.",
                () => Recovered && !HasPermit));
        }

        private void AddPickups()
        {
            pickups.Add(new QuestPickup(PickupWreck, ValePlaces.OldRoad, "the wagon wreck", "Investigate", "",
                () => quests.HasClue(QuestLog.ClueWreckSeen),
                () => true,
                InvestigateWreck));
            pickups.Add(new QuestPickup(PickupBadge, ValePlaces.OldRoad, "a dented badge", "Take", "",
                () => inventory.HasApplied(QuestLog.BadgeFoundEvent),
                () => true,
                () => quests.TryFindBadge()));
            foreach (var id in new[] { PickupHerb1, PickupHerb2, PickupHerb3 })
            {
                string eventId = id;
                pickups.Add(new QuestPickup(id, ValePlaces.HerbHollow, "marked herbs", "Pick", "",
                    () => inventory.HasApplied(eventId),
                    () => true,
                    () => PickHerb(eventId)));
            }

            pickups.Add(new QuestPickup(PickupParcel, ValePlaces.BrokenWatch, "the medical parcel", "Take",
                "The camp's defenders guard the parcel. Clear them, or talk to Kest.",
                () => quests.MainState == MainQuestState.ParcelRecovered || quests.MainState == MainQuestState.Resolved,
                () => CampCleared,
                () => quests.TryRecoverParcel(RecoveryRoute.Combat)));
        }

        private QuestReceipt InvestigateWreck()
        {
            quests.RecordClue(QuestLog.ClueWreckSeen);
            quests.Progression.TryAward(DiscoverWreckEvent, LandmarkXp);
            return Report(QuestLog.MainQuestId, DiscoverWreckEvent, QuestRejection.None,
                $"Clue recorded: the wagon was looted, not lost; tracks lead south-west. +{LandmarkXp} XP.");
        }

        private QuestReceipt PickHerb(string eventId)
        {
            var rejection = inventory.TryApplyQuestEvent(eventId, 0, new[] { new ItemChange(ItemCatalog.HerbBundle, 1) });
            if (rejection != InventoryRejection.None)
            {
                return Report(QuestLog.HerbsQuestId, eventId, rejection == InventoryRejection.NoRoom ? QuestRejection.NoRoom : QuestRejection.DuplicateEvent,
                    rejection == InventoryRejection.NoRoom ? "No room in your pack for the herbs." : "Already picked.");
            }

            return Report(QuestLog.HerbsQuestId, eventId, QuestRejection.None,
                $"Picked a herb bundle ({inventory.CountOf(ItemCatalog.HerbBundle)} carried).");
        }

        private QuestReceipt Agree(string agreementId, string line)
        {
            quests.RecordClue(agreementId);
            return Report(QuestLog.MainQuestId, agreementId, QuestRejection.None, line);
        }

        private QuestReceipt StartSide(string questId, string line)
        {
            var rejection = quests.TryStartSideQuest(questId);
            return Report(questId, null, rejection, rejection == QuestRejection.None ? line : "That quest is already under way.");
        }

        // The badge may be found before Brann asks: returning it then starts and completes the quest in one step.
        private QuestReceipt ReturnBadge()
        {
            if (quests.BadgeState == SideQuestState.Undiscovered)
            {
                quests.TryStartSideQuest(QuestLog.BadgeQuestId);
            }

            return quests.TryCompleteBadge();
        }

        private bool AllRecorded(KnowledgeCard card)
        {
            foreach (var clue in card.Clues)
            {
                if (!quests.HasClue(clue))
                {
                    return false;
                }
            }

            return true;
        }

        private bool IsCurrent(string npcId, long generation) => session.ActiveNpcId == npcId && session.Generation == generation;

        private static string QuestFor(RouteContract contract)
        {
            switch (contract.Id)
            {
                case OfferHerbsIven:
                case DeliveryHerbsIven:
                    return QuestLog.HerbsQuestId;
                case OfferBadgeBrann:
                case DeliveryBadgeBrann:
                    return QuestLog.BadgeQuestId;
                default:
                    return QuestLog.MainQuestId;
            }
        }

        private static bool Contains(IReadOnlyList<string> list, string value)
        {
            foreach (var item in list)
            {
                if (item == value)
                {
                    return true;
                }
            }

            return false;
        }

        private QuestReceipt Report(string questId, string eventId, QuestRejection rejection, string line)
        {
            var receipt = new QuestReceipt(questId, eventId, rejection == QuestRejection.None, rejection, line);
            if (receipt.Applied)
            {
                GameEvents.Raise(Changed);
            }

            GameEvents.Raise(Completed, receipt);
            return receipt;
        }
    }
}

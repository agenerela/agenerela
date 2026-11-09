using System;
using System.Collections.Generic;

namespace LivingVale.Game
{
    /// <summary>The main quest's authoritative states (design §3). Discovery may skip ahead to <see cref="ParcelRecovered"/>.</summary>
    public enum MainQuestState
    {
        Undiscovered,
        Offered,
        Active,
        ParcelRecovered,
        Resolved
    }

    /// <summary>The three recovery routes; recorded, but every route leads to the same state.</summary>
    public enum RecoveryRoute
    {
        None,
        Combat,
        Negotiation,
        Investigation,

        /// <summary>The parcel was picked up before the quest was formally accepted.</summary>
        FoundEarly
    }

    /// <summary>The three authored endings. Each is one code-defined resolution with fixed effects.</summary>
    public enum ParcelDisposition
    {
        None,
        Healer,
        Garrison,

        /// <summary>The agreed division; needs both parties' recorded agreement first.</summary>
        Division
    }

    public enum SideQuestState
    {
        Undiscovered,
        Active,
        Completed
    }

    public enum QuestRejection
    {
        None,

        /// <summary>The quest is not in a state this step starts from.</summary>
        WrongState,

        UnknownQuest,
        UnknownOffer,
        UnknownClue,

        /// <summary>The offer was declined, accepted, or invalidated by closing the conversation.</summary>
        OfferNotPending,

        /// <summary>The offer belongs to another conversation or NPC.</summary>
        StaleConversation,

        /// <summary>The player does not hold what the step takes.</summary>
        MissingItem,

        NoRoom,

        /// <summary>The division needs both parties' recorded agreement.</summary>
        NoAgreement,

        /// <summary>The step's event ID was already applied; nothing happened twice.</summary>
        DuplicateEvent,

        /// <summary>The step costs gold the player does not have (G4c: Kest's payment).</summary>
        NotEnoughGold
    }

    /// <summary>
    /// An authored quest contract an NPC can offer through <c>offer_quest</c>, and what the inline card
    /// shows. Accepting goes through <see cref="QuestLog.AcceptOffer"/>, from the card, never from a
    /// model's reply. Tied to the conversation it was made in, like a trade quote.
    /// </summary>
    public sealed class QuestOffer
    {
        public string Id { get; }
        public string NpcId { get; }
        public string QuestId { get; }
        public string Title { get; }
        public string Summary { get; }

        /// <summary>The reward the card states, authored, never the model's words.</summary>
        public string RewardText { get; }

        public long ConversationGeneration { get; internal set; }
        public QuestOfferState State { get; internal set; }

        internal QuestOffer(string id, string npcId, string questId, string title, string summary, string rewardText)
        {
            Id = id;
            NpcId = npcId;
            QuestId = questId;
            Title = title;
            Summary = summary;
            RewardText = rewardText;
        }

        public override string ToString() => $"{Id} ({State})";
    }

    public enum QuestOfferState
    {
        /// <summary>Not on a card right now.</summary>
        Idle,
        Pending,
        Accepted,
        Declined,
        Invalidated
    }

    /// <summary>What a quest step did: the receipt the chat shows.</summary>
    public sealed class QuestReceipt
    {
        public string QuestId { get; }
        public string EventId { get; }
        public bool Applied { get; }
        public QuestRejection Reason { get; }
        public string Line { get; }

        internal QuestReceipt(string questId, string eventId, bool applied, QuestRejection reason, string line)
        {
            QuestId = questId;
            EventId = eventId;
            Applied = applied;
            Reason = reason;
            Line = line;
        }

        public override string ToString() => Applied ? $"{EventId} applied" : $"{QuestId} refused: {Reason}";
    }

    /// <summary>
    /// The quest log (design §3, §13): the main quest <c>quest_missing_wagon</c>, its clues and its
    /// three dispositions, and the side quests <c>quest_herbs</c> and <c>quest_badge</c>. Every
    /// transition is a method that checks the current state first; there is no setter, and nothing a
    /// character says advances a state. Every reward is applied once per unique event ID through
    /// <see cref="PlayerInventory.TryApplyQuestEvent"/> and <see cref="Progression.TryAward"/>, so a
    /// replay, a double click or a reloaded save cannot pay twice.
    /// </summary>
    /// <remarks>
    /// A step that hands items over runs the inventory change first, which can be refused (no room,
    /// missing item) with nothing changed; only then does the state move and XP arrive. Plain C#, no
    /// scene. Main thread only.
    /// </remarks>
    public sealed class QuestLog
    {
        public const string MainQuestId = "quest_missing_wagon";
        public const string HerbsQuestId = "quest_herbs";
        public const string BadgeQuestId = "quest_badge";

        /// <summary>The reward event IDs, one per step that pays. Saves check them against the quest states.</summary>
        public const string ParcelEvent = "quest_missing_wagon_parcel";
        public const string ResolvedEvent = "quest_missing_wagon_resolved";
        public const string HerbsEvent = "quest_herbs_completed";
        public const string BadgeFoundEvent = "quest_badge_found";
        public const string BadgeCompletedEvent = "quest_badge_completed";

        public const string IvenId = "npc_iven";
        public const string IvenOfferId = "offer_iven_missing_wagon";

        /// <summary>Clues, stored apart from the state so accepting the quest cannot erase a discovery.</summary>
        public const string ClueWreckSeen = "clue_wreck_seen";
        public const string ClueFenTestimony = "clue_fen_testimony";
        public const string ClueCampLocated = "clue_camp_located";

        /// <summary>The two recorded agreements an agreed division needs.</summary>
        public const string AgreementHealer = "agreement_division_iven";
        public const string AgreementGarrison = "agreement_division_elra";

        /// <summary>G4c: every camp defender at Broken Watch is down, so the parcel can be taken (combat route).</summary>
        public const string ClueCampCleared = "clue_camp_cleared";

        /// <summary>G4c: Brann's delivery permit, an access flag stored with the clues so saves carry it.</summary>
        public const string AccessDeliveryPermit = "access_delivery_permit";

        public const int MainQuestXp = 120;
        public const int SideQuestXp = 50;
        public const int HerbsNeeded = 3;

        /// <summary>Gold per disposition. Provisional balance: the garrison pays most, the healer gives potions, the division a little of both.</summary>
        public const int HealerGold = 15;
        public const int HealerPotions = 2;
        public const int GarrisonGold = 45;
        public const int DivisionGold = 25;
        public const int DivisionPotions = 1;
        public const int HerbsPotions = 1;
        public const int BadgeGold = 15;

        public static readonly IReadOnlyList<string> KnownClues = new[]
        {
            ClueWreckSeen, ClueFenTestimony, ClueCampLocated, AgreementHealer, AgreementGarrison,
            ClueCampCleared, AccessDeliveryPermit
        };

        private readonly PlayerInventory inventory;
        private readonly Progression progression;
        private readonly ITradeSession session;
        private readonly HashSet<string> clues = new HashSet<string>();
        private readonly QuestOffer ivenOffer;

        public QuestLog(PlayerInventory inventory, Progression progression, ITradeSession session)
        {
            this.inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
            this.progression = progression ?? throw new ArgumentNullException(nameof(progression));
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            ivenOffer = new QuestOffer(IvenOfferId, IvenId, MainQuestId, "The Missing Wagon",
                "Find the medicine wagon that never reached Briar Glen and bring its parcel back.",
                $"{MainQuestXp} XP; gold and goodwill depend on where the parcel goes.");
        }

        public PlayerInventory Inventory => inventory;
        public Progression Progression => progression;

        public MainQuestState MainState { get; private set; }
        public RecoveryRoute Route { get; private set; }
        public ParcelDisposition Disposition { get; private set; }
        public SideQuestState HerbsState { get; private set; }
        public SideQuestState BadgeState { get; private set; }

        public IReadOnlyCollection<string> Clues => clues;

        /// <summary>The offer on the chat card, or null.</summary>
        public QuestOffer ActiveOffer => ivenOffer.State == QuestOfferState.Pending ? ivenOffer : null;

        /// <summary>Raised after every committed change, including an offer appearing or ending.</summary>
        public event Action Changed;

        /// <summary>Raised for every step the player attempted, applied or refused.</summary>
        public event Action<QuestReceipt> Completed;

        public bool HasClue(string clueId) => clueId != null && clues.Contains(clueId);

        /// <summary>One line for the journal: the main quest's state in the player's words.</summary>
        public string JournalLine
        {
            get
            {
                switch (MainState)
                {
                    case MainQuestState.Undiscovered: return "The Missing Wagon: not yet heard of.";
                    case MainQuestState.Offered: return "The Missing Wagon: Iven asked for help (not accepted).";
                    case MainQuestState.Active: return "The Missing Wagon: find the medicine wagon.";
                    case MainQuestState.ParcelRecovered: return "The Missing Wagon: parcel recovered; decide where it goes.";
                    default: return $"The Missing Wagon: resolved ({Disposition}).";
                }
            }
        }

        // ---- Offers (offer_quest) ----

        /// <summary>
        /// The offers this NPC can make now: Iven's, while the main quest is Undiscovered or Offered.
        /// What <see cref="NpcActionExecutor.AvailableFor"/> lists as <c>offer_quest</c> targets.
        /// </summary>
        public IReadOnlyList<QuestOffer> OffersFor(string npcId)
        {
            if (npcId == IvenId && (MainState == MainQuestState.Undiscovered || MainState == MainQuestState.Offered))
            {
                return new[] { ivenOffer };
            }

            return Array.Empty<QuestOffer>();
        }

        /// <summary>
        /// Executes <c>offer_quest</c>: puts the offer on a card tied to this conversation, and moves the
        /// main quest from Undiscovered to Offered (already Offered stays so). Accepting is the card's job.
        /// </summary>
        public QuestRejection TryOffer(string npcId, string offerId, long generation, out QuestOffer offer)
        {
            offer = null;
            bool known = false;
            foreach (var candidate in OffersFor(npcId))
            {
                if (candidate.Id == offerId)
                {
                    offer = candidate;
                    known = true;
                }
            }

            if (!known)
            {
                // Iven's own offer outside its states is WrongState; anyone else's is not theirs to make.
                return npcId == IvenId && offerId == IvenOfferId ? QuestRejection.WrongState : QuestRejection.UnknownOffer;
            }

            if (session.ActiveNpcId != npcId || session.Generation != generation)
            {
                offer = null;
                return QuestRejection.StaleConversation;
            }

            if (MainState == MainQuestState.Undiscovered)
            {
                MainState = MainQuestState.Offered;
            }

            offer.ConversationGeneration = generation;
            offer.State = QuestOfferState.Pending;
            GameEvents.Raise(Changed);
            return QuestRejection.None;
        }

        /// <summary>The card's Accept button: Offered → Active, only for a pending offer in the conversation it was made in.</summary>
        public QuestReceipt AcceptOffer(string offerId)
        {
            var rejection = CheckPendingOffer(offerId);
            if (rejection == QuestRejection.None && MainState != MainQuestState.Offered)
            {
                rejection = QuestRejection.WrongState;
            }

            if (rejection != QuestRejection.None)
            {
                return Report(MainQuestId, null, rejection, "That offer is no longer open.");
            }

            ivenOffer.State = QuestOfferState.Accepted;
            MainState = MainQuestState.Active;
            return Report(MainQuestId, null, QuestRejection.None, "Quest accepted: The Missing Wagon.");
        }

        /// <summary>The card's Decline button. The quest stays Offered and can be offered again.</summary>
        public bool DeclineOffer(string offerId)
        {
            if (CheckPendingOffer(offerId) != QuestRejection.None)
            {
                return false;
            }

            ivenOffer.State = QuestOfferState.Declined;
            GameEvents.Raise(Changed);
            return true;
        }

        /// <summary>The conversation closed or switched: the card goes away. Nothing else changes.</summary>
        public void InvalidatePending()
        {
            if (ivenOffer.State == QuestOfferState.Pending)
            {
                ivenOffer.State = QuestOfferState.Invalidated;
                GameEvents.Raise(Changed);
            }
        }

        private QuestRejection CheckPendingOffer(string offerId)
        {
            if (offerId != ivenOffer.Id)
            {
                return QuestRejection.UnknownOffer;
            }

            if (ivenOffer.State != QuestOfferState.Pending)
            {
                return QuestRejection.OfferNotPending;
            }

            return session.ActiveNpcId == ivenOffer.NpcId && session.Generation == ivenOffer.ConversationGeneration
                ? QuestRejection.None
                : QuestRejection.StaleConversation;
        }

        // ---- Clues and agreements ----

        /// <summary>Records a game-verified clue or agreement. Never call it from dialogue text alone.</summary>
        public QuestRejection RecordClue(string clueId)
        {
            if (!IsKnownClue(clueId))
            {
                return QuestRejection.UnknownClue;
            }

            if (clues.Add(clueId))
            {
                GameEvents.Raise(Changed);
            }

            return QuestRejection.None;
        }

        public static bool IsKnownClue(string clueId)
        {
            foreach (var known in KnownClues)
            {
                if (known == clueId)
                {
                    return true;
                }
            }

            return false;
        }

        // ---- Main quest ----

        /// <summary>
        /// The parcel reaches the player by one of the routes: Undiscovered, Offered or Active →
        /// ParcelRecovered, with the parcel put in protected quest storage once. A negative
        /// <paramref name="goldDelta"/> is a price paid in the same step (Kest's payment), all or nothing.
        /// </summary>
        public QuestReceipt TryRecoverParcel(RecoveryRoute route, int goldDelta = 0)
        {
            const string eventId = ParcelEvent;
            if (route == RecoveryRoute.None || !Enum.IsDefined(typeof(RecoveryRoute), route))
            {
                return Report(MainQuestId, eventId, QuestRejection.WrongState, "No route recovers the parcel that way.");
            }

            if (MainState == MainQuestState.ParcelRecovered || MainState == MainQuestState.Resolved)
            {
                return Report(MainQuestId, eventId, QuestRejection.WrongState, "The parcel was already recovered.");
            }

            var effective = MainState == MainQuestState.Active ? route : RecoveryRoute.FoundEarly;
            var rejection = Map(inventory.TryApplyQuestEvent(eventId, goldDelta, new[] { new ItemChange(ItemCatalog.MedicalParcel, 1) }));
            if (rejection != QuestRejection.None)
            {
                return Report(MainQuestId, eventId, rejection, rejection == QuestRejection.NotEnoughGold
                    ? $"Not enough gold: the price is {-goldDelta}. Nothing changed."
                    : "You could not take the parcel.");
            }

            ivenOffer.State = ivenOffer.State == QuestOfferState.Pending ? QuestOfferState.Invalidated : ivenOffer.State;
            Route = effective;
            MainState = MainQuestState.ParcelRecovered;
            return Report(MainQuestId, eventId, QuestRejection.None, "Recovered: the medical parcel.");
        }

        /// <summary>
        /// ParcelRecovered → Resolved by one of the three authored dispositions: the parcel leaves the
        /// player, the disposition's fixed gold and items arrive, and the quest's 120 XP is awarded, each once.
        /// </summary>
        public QuestReceipt TryResolve(ParcelDisposition disposition)
        {
            const string eventId = ResolvedEvent;
            if (disposition == ParcelDisposition.None || !Enum.IsDefined(typeof(ParcelDisposition), disposition))
            {
                return Report(MainQuestId, eventId, QuestRejection.WrongState, "That is not a way to resolve it.");
            }

            if (MainState != MainQuestState.ParcelRecovered)
            {
                return Report(MainQuestId, eventId, QuestRejection.WrongState, "There is no parcel to deliver.");
            }

            if (disposition == ParcelDisposition.Division && !(HasClue(AgreementHealer) && HasClue(AgreementGarrison)))
            {
                return Report(MainQuestId, eventId, QuestRejection.NoAgreement, "Both Iven and the garrison must agree to a division first.");
            }

            int gold;
            var changes = new List<ItemChange> { new ItemChange(ItemCatalog.MedicalParcel, -1) };
            switch (disposition)
            {
                case ParcelDisposition.Healer:
                    gold = HealerGold;
                    changes.Add(new ItemChange(ItemCatalog.HealingPotion, HealerPotions));
                    break;
                case ParcelDisposition.Garrison:
                    gold = GarrisonGold;
                    break;
                default:
                    gold = DivisionGold;
                    changes.Add(new ItemChange(ItemCatalog.HealingPotion, DivisionPotions));
                    break;
            }

            var rejection = Map(inventory.TryApplyQuestEvent(eventId, gold, changes));
            if (rejection != QuestRejection.None)
            {
                return Report(MainQuestId, eventId, rejection, rejection == QuestRejection.NoRoom
                    ? "No room in your pack for the reward. Nothing changed."
                    : "You no longer have the parcel.");
            }

            Disposition = disposition;
            MainState = MainQuestState.Resolved;
            progression.TryAward(eventId, MainQuestXp);
            return Report(MainQuestId, eventId, QuestRejection.None,
                $"The Missing Wagon resolved ({disposition}). +{gold} gold, +{MainQuestXp} XP.");
        }

        // ---- Side quests ----

        /// <summary>Undiscovered → Active for a side quest, once a character has asked.</summary>
        public QuestRejection TryStartSideQuest(string questId)
        {
            if (questId != HerbsQuestId && questId != BadgeQuestId)
            {
                return QuestRejection.UnknownQuest;
            }

            if ((questId == HerbsQuestId ? HerbsState : BadgeState) != SideQuestState.Undiscovered)
            {
                return QuestRejection.WrongState;
            }

            if (questId == HerbsQuestId)
            {
                HerbsState = SideQuestState.Active;
            }
            else
            {
                BadgeState = SideQuestState.Active;
            }

            GameEvents.Raise(Changed);
            return QuestRejection.None;
        }

        /// <summary>Herbs Before Sundown: three herb bundles to Iven, for a potion and 50 XP. Active → Completed.</summary>
        public QuestReceipt TryCompleteHerbs()
        {
            const string eventId = HerbsEvent;
            if (HerbsState != SideQuestState.Active)
            {
                return Report(HerbsQuestId, eventId, QuestRejection.WrongState, "Iven has not asked for herbs.");
            }

            var rejection = Map(inventory.TryApplyQuestEvent(eventId, 0, new[]
            {
                new ItemChange(ItemCatalog.HerbBundle, -HerbsNeeded),
                new ItemChange(ItemCatalog.HealingPotion, HerbsPotions)
            }));
            if (rejection != QuestRejection.None)
            {
                return Report(HerbsQuestId, eventId, rejection, $"Iven needs {HerbsNeeded} herb bundles.");
            }

            HerbsState = SideQuestState.Completed;
            progression.TryAward(eventId, SideQuestXp);
            return Report(HerbsQuestId, eventId, QuestRejection.None, $"Herbs Before Sundown complete. +{HerbsPotions} potion, +{SideQuestXp} XP.");
        }

        /// <summary>The badge is picked up at the wreck: into protected storage, once. Works before the quest starts.</summary>
        public QuestReceipt TryFindBadge()
        {
            const string eventId = BadgeFoundEvent;
            if (BadgeState == SideQuestState.Completed)
            {
                return Report(BadgeQuestId, eventId, QuestRejection.WrongState, "The badge was already returned.");
            }

            var rejection = Map(inventory.TryApplyQuestEvent(eventId, 0, new[] { new ItemChange(ItemCatalog.LostBadge, 1) }));
            return Report(BadgeQuestId, eventId, rejection, rejection == QuestRejection.None ? "Found: a dented badge." : "You could not take the badge.");
        }

        /// <summary>A Dented Badge: the badge back to Brann, for gold and 50 XP. Active → Completed.</summary>
        public QuestReceipt TryCompleteBadge()
        {
            const string eventId = BadgeCompletedEvent;
            if (BadgeState != SideQuestState.Active)
            {
                return Report(BadgeQuestId, eventId, QuestRejection.WrongState, "Nobody has asked about a badge.");
            }

            var rejection = Map(inventory.TryApplyQuestEvent(eventId, BadgeGold, new[] { new ItemChange(ItemCatalog.LostBadge, -1) }));
            if (rejection != QuestRejection.None)
            {
                return Report(BadgeQuestId, eventId, rejection, "You don't have the badge.");
            }

            BadgeState = SideQuestState.Completed;
            progression.TryAward(eventId, SideQuestXp);
            return Report(BadgeQuestId, eventId, QuestRejection.None, $"A Dented Badge complete. +{BadgeGold} gold, +{SideQuestXp} XP.");
        }

        // ---- Save ----

        /// <summary>
        /// Why a saved quest state could not have been reached, or null. Checks combinations, not only
        /// values: a resolved quest has a disposition and a route, an unresolved one has no disposition.
        /// </summary>
        public static string ValidateState(MainQuestState main, RecoveryRoute route, ParcelDisposition disposition,
            SideQuestState herbs, SideQuestState badge, IEnumerable<string> savedClues)
        {
            if (!Enum.IsDefined(typeof(MainQuestState), main) || !Enum.IsDefined(typeof(RecoveryRoute), route) ||
                !Enum.IsDefined(typeof(ParcelDisposition), disposition) || !Enum.IsDefined(typeof(SideQuestState), herbs) ||
                !Enum.IsDefined(typeof(SideQuestState), badge))
            {
                return "unknown quest state";
            }

            bool recovered = main == MainQuestState.ParcelRecovered || main == MainQuestState.Resolved;
            if (recovered != (route != RecoveryRoute.None))
            {
                return "recovery route does not match the main quest state";
            }

            if ((main == MainQuestState.Resolved) != (disposition != ParcelDisposition.None))
            {
                return "disposition does not match the main quest state";
            }

            if (savedClues == null)
            {
                return "missing clues";
            }

            foreach (var clue in savedClues)
            {
                if (!IsKnownClue(clue))
                {
                    return $"unknown clue {clue}";
                }
            }

            return null;
        }

        /// <summary>
        /// Why saved quest states disagree with the saved inventory and XP ledgers, or null. Each step that
        /// pays records its event ID, so a state and its events must match: a quest marked Active whose
        /// parcel event was already applied could never recover the parcel again, and a resolved quest
        /// without its XP would never pay it. Also checks the parcel and badge are held exactly when the
        /// states say so.
        /// </summary>
        public static string ValidateConsistency(MainQuestState main, SideQuestState herbs, SideQuestState badge,
            IReadOnlyDictionary<string, int> questItems, ICollection<string> inventoryEvents, ICollection<string> xpEvents)
        {
            if (questItems == null || inventoryEvents == null || xpEvents == null)
            {
                return "missing quest items or event IDs";
            }

            bool recovered = main == MainQuestState.ParcelRecovered || main == MainQuestState.Resolved;
            bool resolved = main == MainQuestState.Resolved;
            bool herbsDone = herbs == SideQuestState.Completed;
            bool badgeDone = badge == SideQuestState.Completed;
            bool badgeFound = inventoryEvents.Contains(BadgeFoundEvent);
            questItems.TryGetValue(ItemCatalog.MedicalParcel, out int parcels);
            questItems.TryGetValue(ItemCatalog.LostBadge, out int badges);

            if (inventoryEvents.Contains(ParcelEvent) != recovered)
            {
                return "the parcel's event does not match the main quest state";
            }

            if (inventoryEvents.Contains(ResolvedEvent) != resolved || xpEvents.Contains(ResolvedEvent) != resolved)
            {
                return "the main quest's reward events do not match its state";
            }

            if (parcels != (main == MainQuestState.ParcelRecovered ? 1 : 0))
            {
                return "the parcel held does not match the main quest state";
            }

            if (inventoryEvents.Contains(HerbsEvent) != herbsDone || xpEvents.Contains(HerbsEvent) != herbsDone)
            {
                return "the herb quest's reward events do not match its state";
            }

            if (inventoryEvents.Contains(BadgeCompletedEvent) != badgeDone || xpEvents.Contains(BadgeCompletedEvent) != badgeDone)
            {
                return "the badge quest's reward events do not match its state";
            }

            if ((badgeDone && !badgeFound) || badges != (badgeFound && !badgeDone ? 1 : 0))
            {
                return "the badge held does not match the badge quest";
            }

            return null;
        }

        /// <summary>
        /// Replaces the quest state with a saved one, after <see cref="ValidateState"/> passes; on
        /// failure nothing changes. Any offer on a card is invalidated: offers are never saved.
        /// </summary>
        public bool TryRestore(MainQuestState main, RecoveryRoute route, ParcelDisposition disposition,
            SideQuestState herbs, SideQuestState badge, IEnumerable<string> savedClues, out string error)
        {
            error = ValidateState(main, route, disposition, herbs, badge, savedClues);
            if (error != null)
            {
                return false;
            }

            if (ivenOffer.State == QuestOfferState.Pending)
            {
                ivenOffer.State = QuestOfferState.Invalidated;
            }

            MainState = main;
            Route = route;
            Disposition = disposition;
            HerbsState = herbs;
            BadgeState = badge;
            clues.Clear();
            clues.UnionWith(savedClues);
            GameEvents.Raise(Changed);
            return true;
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

        private static QuestRejection Map(InventoryRejection rejection)
        {
            switch (rejection)
            {
                case InventoryRejection.None: return QuestRejection.None;
                case InventoryRejection.NoRoom: return QuestRejection.NoRoom;
                case InventoryRejection.DuplicateEvent: return QuestRejection.DuplicateEvent;
                case InventoryRejection.NotEnoughGold: return QuestRejection.NotEnoughGold;
                default: return QuestRejection.MissingItem;
            }
        }
    }
}

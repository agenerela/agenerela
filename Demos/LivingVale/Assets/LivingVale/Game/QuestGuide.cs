using System;
using System.Collections.Generic;

namespace LivingVale.Game
{
    /// <summary>What the guide points at: a speaking NPC, a world pickup, or just a place.</summary>
    public enum QuestGoalKind
    {
        Npc,
        Pickup,
        Place
    }

    /// <summary>
    /// The one thing the compass, the map and the journal point the player at next. <see cref="TargetId"/>
    /// is an NPC ID, a pickup ID or a place ID by <see cref="Kind"/>; <see cref="PlaceId"/> is where the
    /// target normally is, so a view that cannot find the NPC or object in the scene still has a point.
    /// </summary>
    public sealed class QuestGoal
    {
        public QuestGoalKind Kind { get; }
        public string TargetId { get; }
        public string PlaceId { get; }

        /// <summary>The short label on the compass and the map ("Iven").</summary>
        public string Label { get; }

        /// <summary>The journal's line, an instruction in the player's words ("Talk to Iven in Briar Glen").</summary>
        public string Instruction { get; }

        public QuestGoal(QuestGoalKind kind, string targetId, string placeId, string label, string instruction)
        {
            Kind = kind;
            TargetId = targetId;
            PlaceId = placeId;
            Label = label;
            Instruction = instruction;
        }

        public override string ToString() => $"{Kind} {TargetId} ({Instruction})";
    }

    /// <summary>The quest state the guide reads, as plain values, so every combination is testable.</summary>
    public struct QuestGuideInput
    {
        public MainQuestState Main;
        public bool WreckSeen;
        public bool FenTestimony;
        public bool CampLocated;
        public bool CampCleared;
        public bool HasPermit;
        public bool AgreementHealer;
        public bool AgreementGarrison;
        public SideQuestState Herbs;
        public int HerbsCarried;

        /// <summary>The herb pickups not yet picked, in order.</summary>
        public IReadOnlyList<string> HerbsLeft;

        public SideQuestState Badge;
        public bool BadgeFound;
    }

    /// <summary>
    /// G8a: maps quest state to the next goal. The main quest comes first while it is open; once it is
    /// resolved, an active side quest (herbs, then the badge). Nothing to do, or a state this does not
    /// know, gives no goal rather than a wrong one. Plain C#, no scene: the HUD resolves the goal's
    /// target to a position (<c>NavigationTargets</c>).
    /// </summary>
    public static class QuestGuide
    {
        public const string MaraId = "npc_mara";

        /// <summary>The goal for the game's current state, or null.</summary>
        public static QuestGoal For(QuestRoutes routes)
        {
            return routes == null ? null : For(InputFrom(routes));
        }

        public static QuestGuideInput InputFrom(QuestRoutes routes)
        {
            QuestLog quests = routes.Quests;
            PlayerInventory inventory = quests.Inventory;
            var herbsLeft = new List<string>();
            foreach (var id in new[] { QuestRoutes.PickupHerb1, QuestRoutes.PickupHerb2, QuestRoutes.PickupHerb3 })
            {
                if (!routes.IsDone(id))
                {
                    herbsLeft.Add(id);
                }
            }

            return new QuestGuideInput
            {
                Main = quests.MainState,
                WreckSeen = quests.HasClue(QuestLog.ClueWreckSeen),
                FenTestimony = quests.HasClue(QuestLog.ClueFenTestimony),
                CampLocated = quests.HasClue(QuestLog.ClueCampLocated),
                CampCleared = routes.CampCleared,
                HasPermit = routes.HasPermit,
                AgreementHealer = quests.HasClue(QuestLog.AgreementHealer),
                AgreementGarrison = quests.HasClue(QuestLog.AgreementGarrison),
                Herbs = quests.HerbsState,
                HerbsCarried = inventory.CountOf(ItemCatalog.HerbBundle),
                HerbsLeft = herbsLeft,
                Badge = quests.BadgeState,
                BadgeFound = inventory.HasApplied(QuestLog.BadgeFoundEvent)
            };
        }

        public static QuestGoal For(QuestGuideInput state)
        {
            switch (state.Main)
            {
                case MainQuestState.Undiscovered:
                    return Npc(QuestLog.IvenId, "Iven", ValePlaces.BriarGlen, "Talk to Iven, the healer, in Briar Glen");
                case MainQuestState.Offered:
                    return Npc(QuestLog.IvenId, "Iven", ValePlaces.BriarGlen, "Accept Iven's request in Briar Glen");
                case MainQuestState.Active:
                    return ActiveGoal(state);
                case MainQuestState.ParcelRecovered:
                    return DeliveryGoal(state);
                case MainQuestState.Resolved:
                    return SideGoal(state);
                default:
                    return null;
            }
        }

        private static QuestGoal ActiveGoal(QuestGuideInput state)
        {
            if (state.CampCleared)
            {
                return Pickup(QuestRoutes.PickupParcel, "Parcel", ValePlaces.BrokenWatch, "Take the parcel at Broken Watch");
            }

            if (state.FenTestimony || state.CampLocated)
            {
                return Npc(QuestRoutes.KestId, "Kest", ValePlaces.BrokenWatch, "Ask Kest for the parcel at Broken Watch");
            }

            if (state.WreckSeen)
            {
                return Npc(QuestRoutes.FenId, "Fen", ValePlaces.Reedbank, "Ask Fen at Reedbank what she saw");
            }

            return Pickup(QuestRoutes.PickupWreck, "Wreck", ValePlaces.OldRoad, "Investigate the wagon wreck on the Old Road");
        }

        private static QuestGoal DeliveryGoal(QuestGuideInput state)
        {
            // Halfway through a division, finish it; otherwise Iven, who needs no permit.
            if (state.AgreementHealer && state.AgreementGarrison)
            {
                return Npc(QuestLog.IvenId, "Iven", ValePlaces.BriarGlen, "Divide the parcel with Iven in Briar Glen");
            }

            if (state.AgreementHealer)
            {
                return state.HasPermit
                    ? Npc(QuestRoutes.ElraId, "Elra", ValePlaces.Ashwatch, "Ask Elra at Ashwatch to agree to the division")
                    : Npc(QuestRoutes.BrannId, "Brann", ValePlaces.Ashwatch, "Get Brann's delivery permit at Ashwatch");
            }

            if (state.AgreementGarrison)
            {
                return Npc(QuestLog.IvenId, "Iven", ValePlaces.BriarGlen, "Ask Iven in Briar Glen to agree to the division");
            }

            return Npc(QuestLog.IvenId, "Iven", ValePlaces.BriarGlen,
                state.HasPermit
                    ? "Deliver the parcel: Iven in Briar Glen, or Elra at Ashwatch"
                    : "Deliver the parcel to Iven in Briar Glen (or Elra, with Brann's permit)");
        }

        private static QuestGoal SideGoal(QuestGuideInput state)
        {
            if (state.Herbs == SideQuestState.Active)
            {
                if (state.HerbsCarried >= QuestLog.HerbsNeeded)
                {
                    return Npc(QuestLog.IvenId, "Iven", ValePlaces.BriarGlen, "Hand Iven the herbs in Briar Glen");
                }

                if (state.HerbsLeft != null && state.HerbsLeft.Count > 0)
                {
                    return Pickup(state.HerbsLeft[0], "Herbs", ValePlaces.HerbHollow,
                        $"Pick herbs in Herb Hollow ({Math.Min(state.HerbsCarried, QuestLog.HerbsNeeded)}/{QuestLog.HerbsNeeded})");
                }

                // Every spot is picked but some herbs were sold or used: Mara sells herb bundles.
                return Npc(MaraId, "Mara", ValePlaces.BriarGlen,
                    $"Buy the missing herbs from Mara in Briar Glen ({Math.Max(0, state.HerbsCarried)}/{QuestLog.HerbsNeeded})");
            }

            if (state.Badge == SideQuestState.Active)
            {
                return state.BadgeFound
                    ? Npc(QuestRoutes.BrannId, "Brann", ValePlaces.Ashwatch, "Return the badge to Brann at Ashwatch")
                    : Pickup(QuestRoutes.PickupBadge, "Badge", ValePlaces.OldRoad, "Search the Old Road wreck for Brann's badge");
            }

            return null;
        }

        private static QuestGoal Npc(string id, string label, string placeId, string instruction) =>
            new QuestGoal(QuestGoalKind.Npc, id, placeId, label, instruction);

        private static QuestGoal Pickup(string id, string label, string placeId, string instruction) =>
            new QuestGoal(QuestGoalKind.Pickup, id, placeId, label, instruction);
    }
}

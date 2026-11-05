using LivingVale.Game;
using UnityEngine;
using CombatLootSink = LivingVale.Combat.ILootSink;
using IDefeatPenalty = LivingVale.Combat.IDefeatPenalty;
using LootDrop = LivingVale.Combat.LootDrop;

namespace LivingVale.Unity
{
    /// <summary>
    /// Joins combat (G2) to the player's holdings (G3): a defeated enemy's drop goes into the
    /// inventory, and a defeat costs a little gold. Combat and the inventory were built in
    /// parallel, each behind its own interface, so neither knows the other.
    /// </summary>
    /// <remarks>Plain C#: the bootstrap hands it to <c>CombatDirector</c>.</remarks>
    public sealed class CombatInventoryBridge : CombatLootSink, IDefeatPenalty
    {
        /// <summary>The share of gold a defeat costs (design §4, "a modest gold penalty").</summary>
        public const int DefeatPenaltyPercent = 10;

        private readonly PlayerInventory inventory;

        public CombatInventoryBridge(PlayerInventory inventory)
        {
            this.inventory = inventory ?? throw new System.ArgumentNullException(nameof(inventory));
        }

        /// <summary>The last result, for tests and the log.</summary>
        public InventoryRejection LastLootResult { get; private set; }

        public void Receive(LootDrop drop)
        {
            // One defeated enemy drops each item once, so its ID and the item make the event: a
            // repeat (a respawn, a duplicate call) is refused by the inventory, never given twice.
            string eventId = $"loot:{drop.SourceId}:{drop.ItemId}";
            LastLootResult = inventory.TryGiveLoot(eventId, new[] { new ItemChange(drop.ItemId, drop.Count) }, 0);
            if (LastLootResult == InventoryRejection.None)
            {
                Debug.Log($"[LivingVale] Picked up {drop}.");
            }
            else
            {
                // No ground pickups yet: loot that does not fit is lost, and the log says so.
                Debug.Log($"[LivingVale] Could not pick up {drop}: {LastLootResult}.");
            }
        }

        /// <summary>The gold a defeat costs with <paramref name="gold"/> on hand: a tenth, at least one, never more than there is.</summary>
        public static int PenaltyFor(int gold)
        {
            return gold <= 0 ? 0 : Mathf.Max(1, gold * DefeatPenaltyPercent / 100);
        }

        public void Apply(string playerId)
        {
            // Gold only: equipment and quest items are never lost on defeat (design §4).
            int penalty = PenaltyFor(inventory.Gold);
            if (penalty > 0 && inventory.TryApply(-penalty, System.Array.Empty<ItemChange>(), out _))
            {
                Debug.Log($"[LivingVale] Defeat cost {penalty} gold.");
            }
        }
    }
}

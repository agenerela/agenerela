using UnityEngine;

namespace LivingVale.Combat
{
    /// <summary>Stable item IDs combat drops. The inventory (milestone G3) owns what they mean.</summary>
    public static class LootIds
    {
        public const string WolfPelt = "item_wolf_pelt";
        public const string IronScrap = "item_iron_scrap";
    }

    /// <summary>One item stack dropped by a defeated enemy.</summary>
    public readonly struct LootDrop
    {
        public LootDrop(string itemId, int count, string sourceId, Vector3 position)
        {
            ItemId = itemId;
            Count = count;
            SourceId = sourceId;
            Position = position;
        }

        public string ItemId { get; }
        public int Count { get; }

        /// <summary>The enemy that dropped it, such as <c>enemy_wolf_1</c>.</summary>
        public string SourceId { get; }
        public Vector3 Position { get; }

        public override string ToString() => $"{Count}× {ItemId} from {SourceId}";
    }

    /// <summary>
    /// Where loot goes. Combat raises each drop exactly once per defeated enemy; the inventory
    /// decides whether to pick it up straight away or leave it on the ground.
    /// </summary>
    public interface ILootSink
    {
        void Receive(LootDrop drop);
    }

    /// <summary>The default sink until the inventory is wired in: it only logs.</summary>
    public sealed class LogLootSink : ILootSink
    {
        public void Receive(LootDrop drop) => Debug.Log($"[LivingVale] Loot: {drop}.");
    }

    /// <summary>What a player defeat costs. Design §4: a modest gold penalty, never equipment or quest items.</summary>
    public interface IDefeatPenalty
    {
        /// <summary>Called once per defeat, before the player respawns.</summary>
        void Apply(string playerId);
    }

    /// <summary>The default penalty until the inventory is wired in: none.</summary>
    public sealed class NoDefeatPenalty : IDefeatPenalty
    {
        public void Apply(string playerId)
        {
        }
    }

    /// <summary>What each enemy type drops. Plain data so the loot rule is testable.</summary>
    public static class LootTable
    {
        public static LootDrop[] For(EnemyArchetype archetype, string sourceId, Vector3 position)
        {
            switch (archetype)
            {
                case EnemyArchetype.Wolf:
                    return new[] { new LootDrop(LootIds.WolfPelt, 1, sourceId, position) };
                case EnemyArchetype.Raider:
                    return new[] { new LootDrop(LootIds.IronScrap, 2, sourceId, position) };
                default:
                    return new LootDrop[0];
            }
        }
    }

    public enum EnemyArchetype
    {
        Raider,
        Wolf
    }
}

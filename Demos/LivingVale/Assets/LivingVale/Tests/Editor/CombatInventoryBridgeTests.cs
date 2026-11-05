using LivingVale.Combat;
using LivingVale.Game;
using LivingVale.Unity;
using NUnit.Framework;
using UnityEngine;

namespace LivingVale.Tests
{
    public sealed class CombatInventoryBridgeTests
    {
        private PlayerInventory inventory;
        private CombatInventoryBridge bridge;

        [SetUp]
        public void SetUp()
        {
            inventory = new PlayerInventory(ItemCatalog.CreateDefault());
            bridge = new CombatInventoryBridge(inventory);
        }

        // Combat and the catalog were written in parallel; their IDs must agree or loot is refused.
        [Test]
        public void CombatLootIds_AreCatalogItems()
        {
            Assert.That(LootIds.WolfPelt, Is.EqualTo(ItemCatalog.WolfPelt));
            Assert.That(LootIds.IronScrap, Is.EqualTo(ItemCatalog.IronScrap));
        }

        [Test]
        public void Loot_GoesIntoTheInventory_OncePerEnemy()
        {
            var drop = new LootDrop(LootIds.IronScrap, 2, "enemy_raider_1", Vector3.zero);

            bridge.Receive(drop);
            bridge.Receive(drop);

            Assert.That(inventory.CountOf(ItemCatalog.IronScrap), Is.EqualTo(2));
            Assert.That(bridge.LastLootResult, Is.EqualTo(InventoryRejection.DuplicateEvent));
        }

        [Test]
        public void SameItemFromAnotherEnemy_IsGivenAgain()
        {
            bridge.Receive(new LootDrop(LootIds.WolfPelt, 1, "enemy_wolf_1", Vector3.zero));
            bridge.Receive(new LootDrop(LootIds.WolfPelt, 1, "enemy_wolf_2", Vector3.zero));

            Assert.That(inventory.CountOf(ItemCatalog.WolfPelt), Is.EqualTo(2));
        }

        [Test]
        public void Loot_IntoAFullInventory_GivesNothing()
        {
            // Swords stack to one, so sixteen of them fill every general slot.
            Assert.That(inventory.TryApply(0, new[] { new ItemChange(ItemCatalog.IronSword, PlayerInventory.SlotCount) }, out var fill), Is.True, fill.ToString());

            bridge.Receive(new LootDrop(LootIds.WolfPelt, 1, "enemy_wolf_1", Vector3.zero));

            Assert.That(bridge.LastLootResult, Is.EqualTo(InventoryRejection.NoRoom));
            Assert.That(inventory.CountOf(ItemCatalog.WolfPelt), Is.Zero);
        }

        [TestCase(40, 4)]
        [TestCase(9, 1)]
        [TestCase(1, 1)]
        [TestCase(0, 0)]
        public void DefeatPenalty_IsATenth_AtLeastOne_NeverBelowZero(int gold, int expectedPenalty)
        {
            inventory = new PlayerInventory(ItemCatalog.CreateDefault(), gold);
            bridge = new CombatInventoryBridge(inventory);

            bridge.Apply("player");

            Assert.That(inventory.Gold, Is.EqualTo(gold - expectedPenalty));
        }

        [Test]
        public void DefeatPenalty_TakesNoItems()
        {
            var starting = PlayerInventory.CreateStarting(ItemCatalog.CreateDefault());
            int potions = starting.CountOf(ItemCatalog.HealingPotion);
            new CombatInventoryBridge(starting).Apply("player");

            Assert.That(starting.CountOf(ItemCatalog.HealingPotion), Is.EqualTo(potions));
            Assert.That(starting.Weapon, Is.EqualTo(ItemCatalog.StarterSword));
            Assert.That(starting.Gold, Is.EqualTo(PlayerInventory.StartingGold - 4));
        }
    }
}

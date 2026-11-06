using System.Linq;
using LivingVale.Game;
using NUnit.Framework;

namespace LivingVale.Tests
{
    public sealed class PlayerInventoryTests
    {
        private ItemCatalog catalog;
        private PlayerInventory inventory;

        [SetUp]
        public void SetUp()
        {
            catalog = ItemCatalog.CreateDefault();
            inventory = new PlayerInventory(catalog);
        }

        private static ItemChange[] Give(string itemId, int quantity) => new[] { new ItemChange(itemId, quantity) };

        [Test]
        public void CatalogHasTheTwelveDesignItemsWithStableIds()
        {
            Assert.That(catalog.All, Has.Count.EqualTo(12));
            Assert.That(catalog.All.Select(i => i.Id), Is.Unique);
            Assert.That(catalog.All.Select(i => i.Id), Has.All.StartWith("item_"));
            Assert.That(catalog.Get(ItemCatalog.HealingPotion).BasePrice, Is.EqualTo(10));
            Assert.That(catalog.Get(ItemCatalog.IronSword).BasePrice, Is.EqualTo(30));
            Assert.That(catalog.All.Where(i => i.IsProtected).Select(i => i.Id),
                Is.EquivalentTo(new[] { ItemCatalog.LostBadge, ItemCatalog.MedicalParcel }));
        }

        [Test]
        public void StartingInventory_FortyGold_StarterKitWorn()
        {
            var start = PlayerInventory.CreateStarting(catalog);
            Assert.That(start.Gold, Is.EqualTo(40));
            Assert.That(start.Weapon, Is.EqualTo(ItemCatalog.StarterSword));
            Assert.That(start.Armor, Is.EqualTo(ItemCatalog.TravelerArmor));
            Assert.That(start.Slots.Count, Is.EqualTo(16));
            Assert.That(start.QuickUse.Count, Is.EqualTo(3));
            Assert.That(start.CountOf(ItemCatalog.StarterSword), Is.Zero, "a worn item is not in the slots, so it cannot be sold");
        }

        [Test]
        public void GoldNeverGoesNegative_AndTheRefusedChangeMovesNothing()
        {
            Assert.That(inventory.TryApply(-41, Give(ItemCatalog.Bread, 1), out var rejection), Is.False);
            Assert.That(rejection, Is.EqualTo(InventoryRejection.NotEnoughGold));
            Assert.That(inventory.Gold, Is.EqualTo(40));
            Assert.That(inventory.CountOf(ItemCatalog.Bread), Is.Zero);
            Assert.That(inventory.Revision, Is.Zero);

            Assert.That(inventory.TryApply(-40, null, out _), Is.True);
            Assert.That(inventory.Gold, Is.Zero);
        }

        [Test]
        public void StacksFillUpToTheirCapThenTakeNewSlots()
        {
            Assert.That(inventory.TryApply(0, Give(ItemCatalog.HealingPotion, 12), out _), Is.True);
            var potions = inventory.Slots.Where(s => s.ItemId == ItemCatalog.HealingPotion).Select(s => s.Quantity);
            Assert.That(potions, Is.EqualTo(new[] { 5, 5, 2 }));
            Assert.That(inventory.FreeSlots, Is.EqualTo(13));
        }

        [Test]
        public void FullInventory_RefusesTheWholeChange()
        {
            Assert.That(inventory.TryApply(0, Give(ItemCatalog.IronScrap, 20 * 16), out _), Is.True);
            Assert.That(inventory.FreeSlots, Is.Zero);
            long revision = inventory.Revision;

            // Gold would be fine, the item would not fit: neither happens.
            Assert.That(inventory.TryApply(-5, Give(ItemCatalog.Bread, 1), out var rejection), Is.False);
            Assert.That(rejection, Is.EqualTo(InventoryRejection.NoRoom));
            Assert.That(inventory.Gold, Is.EqualTo(40));
            Assert.That(inventory.Revision, Is.EqualTo(revision));

            // Swapping a whole stack out frees the slot the new item needs, in one change.
            Assert.That(inventory.TryApply(0, new[] { new ItemChange(ItemCatalog.IronScrap, -20), new ItemChange(ItemCatalog.Bread, 1) }, out _), Is.True);
            Assert.That(inventory.CountOf(ItemCatalog.Bread), Is.EqualTo(1));
        }

        [Test]
        public void SeveralChanges_AllOrNothing()
        {
            inventory.TryApply(0, Give(ItemCatalog.WolfPelt, 2), out _);

            // The second removal fails, so the first removal and the gold do not happen either.
            var changes = new[] { new ItemChange(ItemCatalog.WolfPelt, -2), new ItemChange(ItemCatalog.HerbBundle, -1) };
            Assert.That(inventory.TryApply(10, changes, out var rejection), Is.False);
            Assert.That(rejection, Is.EqualTo(InventoryRejection.NotEnoughItems));
            Assert.That(inventory.CountOf(ItemCatalog.WolfPelt), Is.EqualTo(2));
            Assert.That(inventory.Gold, Is.EqualTo(40));
        }

        [Test]
        public void QuestItems_GoToProtectedStorage_AndCannotBeRemovedByOrdinaryChanges()
        {
            Assert.That(inventory.TryGiveLoot("loot_badge", Give(ItemCatalog.LostBadge, 1), 0), Is.EqualTo(InventoryRejection.None));
            Assert.That(inventory.QuestItems[ItemCatalog.LostBadge], Is.EqualTo(1));
            Assert.That(inventory.FreeSlots, Is.EqualTo(16), "quest storage takes no general slot");

            Assert.That(inventory.TryApply(5, Give(ItemCatalog.LostBadge, -1), out var rejection), Is.False);
            Assert.That(rejection, Is.EqualTo(InventoryRejection.ProtectedItem));
            Assert.That(inventory.CountOf(ItemCatalog.LostBadge), Is.EqualTo(1));
        }

        [Test]
        public void Loot_IsAppliedOncePerEventId()
        {
            var pelts = Give(ItemCatalog.WolfPelt, 1);
            Assert.That(inventory.TryGiveLoot("wolf_1_death", pelts, 3), Is.EqualTo(InventoryRejection.None));
            Assert.That(inventory.TryGiveLoot("wolf_1_death", pelts, 3), Is.EqualTo(InventoryRejection.DuplicateEvent));
            Assert.That(inventory.CountOf(ItemCatalog.WolfPelt), Is.EqualTo(1));
            Assert.That(inventory.Gold, Is.EqualTo(43));
        }

        [Test]
        public void Loot_ThatDoesNotFit_GivesNothing_AndCanBeRetried()
        {
            inventory.TryApply(0, Give(ItemCatalog.IronScrap, 20 * 16), out _);
            Assert.That(inventory.TryGiveLoot("chest_1", Give(ItemCatalog.WolfPelt, 1), 7), Is.EqualTo(InventoryRejection.NoRoom));
            Assert.That(inventory.Gold, Is.EqualTo(40), "the gold in the same grant is not given either");

            inventory.TryApply(0, Give(ItemCatalog.IronScrap, -20), out _);
            Assert.That(inventory.TryGiveLoot("chest_1", Give(ItemCatalog.WolfPelt, 1), 7), Is.EqualTo(InventoryRejection.None),
                "a refused grant did not use up its event ID");
        }

        [Test]
        public void Loot_RefusesNegativeAmounts()
        {
            Assert.That(inventory.TryGiveLoot("bad", Give(ItemCatalog.WolfPelt, -1), 0), Is.EqualTo(InventoryRejection.InvalidQuantity));
            Assert.That(inventory.TryGiveLoot("bad2", null, -5), Is.EqualTo(InventoryRejection.InvalidQuantity));
            Assert.That(inventory.Gold, Is.EqualTo(40));
        }

        [Test]
        public void Equip_SwapsTheWornItemBackIntoTheSlots()
        {
            var start = PlayerInventory.CreateStarting(catalog);
            start.TryApply(0, Give(ItemCatalog.IronSword, 1), out _);
            Assert.That(start.TryEquip(ItemCatalog.IronSword), Is.True);
            Assert.That(start.Weapon, Is.EqualTo(ItemCatalog.IronSword));
            Assert.That(start.CountOf(ItemCatalog.StarterSword), Is.EqualTo(1));
            Assert.That(start.CountOf(ItemCatalog.IronSword), Is.Zero);
            Assert.That(start.TryEquip(ItemCatalog.Bread), Is.False);
        }
    }
}

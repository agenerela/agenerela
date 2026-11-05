using System;
using System.Collections.Generic;

namespace LivingVale.Game
{
    /// <summary>Units of one item in one general slot. <see cref="IsEmpty"/> when the slot is free.</summary>
    public readonly struct ItemStack
    {
        public string ItemId { get; }
        public int Quantity { get; }
        public bool IsEmpty => ItemId == null || Quantity <= 0;

        public ItemStack(string itemId, int quantity)
        {
            ItemId = quantity > 0 ? itemId : null;
            Quantity = quantity > 0 ? quantity : 0;
        }

        public override string ToString() => IsEmpty ? "(empty)" : $"{ItemId} ×{Quantity}";
    }

    /// <summary>A signed change to how many units of one item the player holds.</summary>
    public readonly struct ItemChange
    {
        public string ItemId { get; }
        public int Delta { get; }

        public ItemChange(string itemId, int delta)
        {
            ItemId = itemId;
            Delta = delta;
        }

        public override string ToString() => $"{ItemId} {Delta:+0;-0}";
    }

    /// <summary>Why an inventory change was refused. A refused change leaves the inventory exactly as it was.</summary>
    public enum InventoryRejection
    {
        None,
        UnknownItem,
        InvalidQuantity,
        NotEnoughGold,
        NotEnoughItems,
        NoRoom,

        /// <summary>A quest item cannot be removed by an ordinary change (design §5).</summary>
        ProtectedItem,

        /// <summary>The loot event ID was already applied; nothing was given twice.</summary>
        DuplicateEvent
    }

    /// <summary>
    /// Where combat, chests and quest rewards hand items and gold to the player. Each grant carries
    /// a unique event ID, and a repeated ID is refused, so a reward cannot apply twice (design §12).
    /// All or nothing: when the items do not fit, nothing is given.
    /// </summary>
    public interface ILootSink
    {
        InventoryRejection TryGiveLoot(string eventId, IReadOnlyList<ItemChange> items, int gold);
    }

    /// <summary>
    /// The player's holdings: 16 general slots of stacks, a weapon and an armor slot, three quick-use
    /// assignments, integer gold, and protected quest storage (design §5). Every change goes
    /// through <see cref="TryApply"/>, which computes the result on a copy and commits it whole or
    /// not at all, then raises <see cref="Revision"/> so a quote built on older state goes stale.
    /// </summary>
    /// <remarks>Plain C#, no scene. Main thread only.</remarks>
    public sealed class PlayerInventory : ILootSink
    {
        public const int SlotCount = 16;
        public const int QuickUseCount = 3;
        public const int StartingGold = 40;

        private readonly ItemCatalog catalog;
        private ItemStack[] slots = new ItemStack[SlotCount];
        private Dictionary<string, int> quest = new Dictionary<string, int>();
        private readonly string[] quickUse = new string[QuickUseCount];
        private readonly HashSet<string> appliedEvents = new HashSet<string>();
        private int deferDepth;
        private bool changedWhileDeferred;

        public PlayerInventory(ItemCatalog catalog, int gold = StartingGold)
        {
            this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            if (gold < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(gold), "Gold is never negative.");
            }

            Gold = gold;
        }

        /// <summary>The first adventure's start: starter sword and traveler armor worn, 40 gold, one potion on quick-use 1.</summary>
        public static PlayerInventory CreateStarting(ItemCatalog catalog)
        {
            var inventory = new PlayerInventory(catalog);
            inventory.Weapon = ItemCatalog.StarterSword;
            inventory.Armor = ItemCatalog.TravelerArmor;
            inventory.TryApply(0, new[] { new ItemChange(ItemCatalog.HealingPotion, 1), new ItemChange(ItemCatalog.Bread, 2) }, out _);
            inventory.AssignQuickUse(0, ItemCatalog.HealingPotion);
            inventory.Revision = 0;
            return inventory;
        }

        public ItemCatalog Catalog => catalog;

        public int Gold { get; private set; }

        /// <summary>Goes up on every committed change to gold or items. Quotes record it and are refused once it moves.</summary>
        public long Revision { get; private set; }

        public IReadOnlyList<ItemStack> Slots => slots;

        /// <summary>Equipped weapon's item ID, or null. Equipped items are not in <see cref="Slots"/> and cannot be sold.</summary>
        public string Weapon { get; private set; }

        public string Armor { get; private set; }

        /// <summary>Quick-use assignments by item ID: shortcuts to general slots, not storage.</summary>
        public IReadOnlyList<string> QuickUse => quickUse;

        public IReadOnlyDictionary<string, int> QuestItems => quest;

        /// <summary>Raised after every committed change.</summary>
        public event Action Changed;

        /// <summary>Units held: general slots, or quest storage for a quest item. Equipped items do not count.</summary>
        public int CountOf(string itemId)
        {
            if (itemId == null)
            {
                return 0;
            }

            if (quest.TryGetValue(itemId, out int held))
            {
                return held;
            }

            int count = 0;
            foreach (var stack in slots)
            {
                if (!stack.IsEmpty && stack.ItemId == itemId)
                {
                    count += stack.Quantity;
                }
            }

            return count;
        }

        public int FreeSlots
        {
            get
            {
                int free = 0;
                foreach (var stack in slots)
                {
                    if (stack.IsEmpty)
                    {
                        free++;
                    }
                }

                return free;
            }
        }

        /// <summary>Whether <see cref="TryApply"/> would succeed, without changing anything.</summary>
        public InventoryRejection Check(int goldDelta, IReadOnlyList<ItemChange> changes)
        {
            return Simulate(goldDelta, changes, false, out _, out _, out _);
        }

        /// <summary>
        /// Applies a gold change and item changes together, or none of them. Removing quest items is
        /// refused here; the quest system will get its own entry point.
        /// </summary>
        public bool TryApply(int goldDelta, IReadOnlyList<ItemChange> changes, out InventoryRejection rejection)
        {
            rejection = Simulate(goldDelta, changes, false, out var newSlots, out var newQuest, out int newGold);
            if (rejection != InventoryRejection.None)
            {
                return false;
            }

            Commit(newSlots, newQuest, newGold);
            return true;
        }

        public InventoryRejection TryGiveLoot(string eventId, IReadOnlyList<ItemChange> items, int gold)
        {
            if (string.IsNullOrWhiteSpace(eventId))
            {
                throw new ArgumentException("Loot needs a unique event ID.", nameof(eventId));
            }

            if (appliedEvents.Contains(eventId))
            {
                return InventoryRejection.DuplicateEvent;
            }

            if (gold < 0 || (items != null && HasNonPositive(items)))
            {
                return InventoryRejection.InvalidQuantity;
            }

            var rejection = Simulate(gold, items, true, out var newSlots, out var newQuest, out int newGold);
            if (rejection != InventoryRejection.None)
            {
                return rejection;
            }

            appliedEvents.Add(eventId);
            Commit(newSlots, newQuest, newGold);
            return InventoryRejection.None;
        }

        /// <summary>Equips a weapon or armor from a general slot, moving whatever was worn into its place.</summary>
        public bool TryEquip(string itemId)
        {
            if (!catalog.TryGet(itemId, out var item) || (item.Kind != ItemKind.Weapon && item.Kind != ItemKind.Armor) || CountOf(itemId) < 1)
            {
                return false;
            }

            string worn = item.Kind == ItemKind.Weapon ? Weapon : Armor;
            var changes = new List<ItemChange> { new ItemChange(itemId, -1) };
            if (worn != null)
            {
                changes.Add(new ItemChange(worn, 1));
            }

            if (Simulate(0, changes, false, out var newSlots, out var newQuest, out int newGold) != InventoryRejection.None)
            {
                return false;
            }

            if (item.Kind == ItemKind.Weapon)
            {
                Weapon = itemId;
            }
            else
            {
                Armor = itemId;
            }

            Commit(newSlots, newQuest, newGold);
            return true;
        }

        /// <summary>Points a quick-use slot at a consumable, or clears it with null.</summary>
        public bool AssignQuickUse(int index, string itemId)
        {
            if (index < 0 || index >= QuickUseCount)
            {
                return false;
            }

            if (itemId != null && (!catalog.TryGet(itemId, out var item) || item.Kind != ItemKind.Consumable))
            {
                return false;
            }

            // A shortcut, not a holding: a pending quote stays valid.
            quickUse[index] = itemId;
            NotifyChanged();
            return true;
        }

        private static bool HasNonPositive(IReadOnlyList<ItemChange> items)
        {
            foreach (var change in items)
            {
                if (change.Delta <= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private void Commit(ItemStack[] newSlots, Dictionary<string, int> newQuest, int newGold)
        {
            slots = newSlots;
            quest = newQuest;
            Gold = newGold;
            Revision++;
            NotifyChanged();
        }

        /// <summary>
        /// Holds <see cref="Changed"/> until the returned scope is disposed, then raises it once if
        /// anything changed. A transaction that also updates stock and its quote uses it, so
        /// observers never see the inventory committed and the rest not yet.
        /// </summary>
        public IDisposable DeferNotifications()
        {
            deferDepth++;
            return new DeferScope(this);
        }

        private void NotifyChanged()
        {
            if (deferDepth > 0)
            {
                changedWhileDeferred = true;
                return;
            }

            GameEvents.Raise(Changed);
        }

        private void EndDefer()
        {
            if (--deferDepth > 0 || !changedWhileDeferred)
            {
                return;
            }

            changedWhileDeferred = false;
            GameEvents.Raise(Changed);
        }

        private sealed class DeferScope : IDisposable
        {
            private PlayerInventory owner;

            public DeferScope(PlayerInventory owner)
            {
                this.owner = owner;
            }

            public void Dispose()
            {
                var o = owner;
                owner = null;
                o?.EndDefer();
            }
        }

        // Works on copies only; the live state changes in Commit, after every check has passed.
        private InventoryRejection Simulate(int goldDelta, IReadOnlyList<ItemChange> changes, bool allowQuest,
            out ItemStack[] newSlots, out Dictionary<string, int> newQuest, out int newGold)
        {
            newSlots = (ItemStack[])slots.Clone();
            newQuest = new Dictionary<string, int>(quest);
            newGold = 0;

            long gold = (long)Gold + goldDelta;
            if (gold < 0)
            {
                return InventoryRejection.NotEnoughGold;
            }

            if (gold > int.MaxValue)
            {
                return InventoryRejection.InvalidQuantity;
            }

            newGold = (int)gold;
            if (changes == null)
            {
                return InventoryRejection.None;
            }

            // Removals first, so a swap (sell a stack, receive another) can reuse the freed slot.
            foreach (var change in changes)
            {
                if (!catalog.TryGet(change.ItemId, out var item))
                {
                    return InventoryRejection.UnknownItem;
                }

                if (change.Delta == 0)
                {
                    return InventoryRejection.InvalidQuantity;
                }

                if (change.Delta < 0)
                {
                    var rejection = item.IsProtected
                        ? (allowQuest ? RemoveQuest(newQuest, item, -change.Delta) : InventoryRejection.ProtectedItem)
                        : Remove(newSlots, item, -change.Delta);
                    if (rejection != InventoryRejection.None)
                    {
                        return rejection;
                    }
                }
            }

            foreach (var change in changes)
            {
                if (change.Delta > 0)
                {
                    var item = catalog.Get(change.ItemId);
                    var rejection = item.IsProtected ? AddQuest(newQuest, item, change.Delta) : Add(newSlots, item, change.Delta);
                    if (rejection != InventoryRejection.None)
                    {
                        return rejection;
                    }
                }
            }

            return InventoryRejection.None;
        }

        private static InventoryRejection Add(ItemStack[] target, ItemDefinition item, int quantity)
        {
            int left = quantity;
            for (int i = 0; i < target.Length && left > 0; i++)
            {
                if (!target[i].IsEmpty && target[i].ItemId == item.Id && target[i].Quantity < item.MaxStack)
                {
                    int moved = Math.Min(left, item.MaxStack - target[i].Quantity);
                    target[i] = new ItemStack(item.Id, target[i].Quantity + moved);
                    left -= moved;
                }
            }

            for (int i = 0; i < target.Length && left > 0; i++)
            {
                if (target[i].IsEmpty)
                {
                    int moved = Math.Min(left, item.MaxStack);
                    target[i] = new ItemStack(item.Id, moved);
                    left -= moved;
                }
            }

            return left > 0 ? InventoryRejection.NoRoom : InventoryRejection.None;
        }

        private static InventoryRejection Remove(ItemStack[] target, ItemDefinition item, int quantity)
        {
            int held = 0;
            foreach (var stack in target)
            {
                if (!stack.IsEmpty && stack.ItemId == item.Id)
                {
                    held += stack.Quantity;
                }
            }

            if (held < quantity)
            {
                return InventoryRejection.NotEnoughItems;
            }

            int left = quantity;
            for (int i = target.Length - 1; i >= 0 && left > 0; i--)
            {
                if (!target[i].IsEmpty && target[i].ItemId == item.Id)
                {
                    int taken = Math.Min(left, target[i].Quantity);
                    target[i] = new ItemStack(item.Id, target[i].Quantity - taken);
                    left -= taken;
                }
            }

            return InventoryRejection.None;
        }

        private static InventoryRejection AddQuest(Dictionary<string, int> target, ItemDefinition item, int quantity)
        {
            target.TryGetValue(item.Id, out int held);
            if (held + quantity > item.MaxStack)
            {
                return InventoryRejection.NoRoom;
            }

            target[item.Id] = held + quantity;
            return InventoryRejection.None;
        }

        private static InventoryRejection RemoveQuest(Dictionary<string, int> target, ItemDefinition item, int quantity)
        {
            target.TryGetValue(item.Id, out int held);
            if (held < quantity)
            {
                return InventoryRejection.NotEnoughItems;
            }

            if (held == quantity)
            {
                target.Remove(item.Id);
            }
            else
            {
                target[item.Id] = held - quantity;
            }

            return InventoryRejection.None;
        }
    }
}

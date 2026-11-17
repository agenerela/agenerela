using System;
using System.Collections.Generic;

namespace LivingVale.Game
{
    /// <summary>Where an item can live: equipment slots, general slots, or protected quest storage.</summary>
    public enum ItemKind
    {
        Weapon,
        Armor,
        Consumable,
        Material,

        /// <summary>Kept in protected quest storage: never sold, dropped or destroyed (design §5).</summary>
        Quest
    }

    /// <summary>
    /// One item type. <see cref="Id"/> is the stable content ID saves, targets and receipts key on;
    /// <see cref="DisplayName"/> is only shown.
    /// </summary>
    public sealed class ItemDefinition
    {
        public string Id { get; }
        public string DisplayName { get; }
        public ItemKind Kind { get; }

        /// <summary>Integer gold a vendor charges for one unit. Sale prices are derived below it.</summary>
        public int BasePrice { get; }

        /// <summary>Most units one general slot holds. Equipment and quest items are 1.</summary>
        public int MaxStack { get; }

        /// <summary>True for quest items: protected storage, never traded.</summary>
        public bool IsProtected => Kind == ItemKind.Quest;

        public ItemDefinition(string id, string displayName, ItemKind kind, int basePrice, int maxStack)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("An item needs a stable ID.", nameof(id));
            }

            if (basePrice < 0 || maxStack < 1)
            {
                throw new ArgumentException($"Item {id}: price must be ≥ 0 and stack ≥ 1.");
            }

            Id = id;
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? id : displayName;
            Kind = kind;
            BasePrice = basePrice;
            MaxStack = maxStack;
        }

        public override string ToString() => Id;
    }

    /// <summary>The item definitions, by stable ID. <see cref="Default"/> is design §5's twelve.</summary>
    public sealed class ItemCatalog
    {
        public const string StarterSword = "item_starter_sword";
        public const string IronSword = "item_iron_sword";
        public const string SturdySword = "item_sturdy_sword";
        public const string TravelerArmor = "item_traveler_armor";
        public const string PaddedArmor = "item_padded_armor";
        public const string HealingPotion = "item_healing_potion";
        public const string Bread = "item_bread";
        public const string HerbBundle = "item_herb_bundle";
        public const string IronScrap = "item_iron_scrap";
        public const string WolfPelt = "item_wolf_pelt";
        public const string LostBadge = "item_lost_badge";
        public const string MedicalParcel = "item_medical_parcel";

        private readonly Dictionary<string, ItemDefinition> items = new Dictionary<string, ItemDefinition>();
        private readonly List<ItemDefinition> ordered = new List<ItemDefinition>();

        public ItemCatalog(IEnumerable<ItemDefinition> definitions)
        {
            foreach (var item in definitions)
            {
                if (items.ContainsKey(item.Id))
                {
                    throw new ArgumentException($"Duplicate item ID {item.Id}.");
                }

                items.Add(item.Id, item);
                ordered.Add(item);
            }
        }

        /// <summary>A fresh copy of the first build's catalog (initial tuning: potion 10, iron sword 30).</summary>
        public static ItemCatalog CreateDefault() => new ItemCatalog(new[]
        {
            new ItemDefinition(StarterSword, "Starter sword", ItemKind.Weapon, 8, 1),
            new ItemDefinition(IronSword, "Iron sword", ItemKind.Weapon, 30, 1),
            new ItemDefinition(SturdySword, "Sturdy sword", ItemKind.Weapon, 55, 1),
            new ItemDefinition(TravelerArmor, "Traveler armor", ItemKind.Armor, 12, 1),
            new ItemDefinition(PaddedArmor, "Padded armor", ItemKind.Armor, 35, 1),
            new ItemDefinition(HealingPotion, "Healing potion", ItemKind.Consumable, 10, 5),
            new ItemDefinition(Bread, "Bread", ItemKind.Consumable, 3, 10),
            new ItemDefinition(HerbBundle, "Herb bundle", ItemKind.Material, 4, 10),
            new ItemDefinition(IronScrap, "Iron scrap", ItemKind.Material, 5, 20),
            new ItemDefinition(WolfPelt, "Wolf pelt", ItemKind.Material, 8, 10),
            new ItemDefinition(LostBadge, "Lost badge", ItemKind.Quest, 0, 1),
            new ItemDefinition(MedicalParcel, "Medical parcel", ItemKind.Quest, 0, 1)
        });

        public IReadOnlyList<ItemDefinition> All => ordered;

        public bool TryGet(string id, out ItemDefinition item)
        {
            item = null;
            return id != null && items.TryGetValue(id, out item);
        }

        public ItemDefinition Get(string id)
        {
            if (!TryGet(id, out var item))
            {
                throw new KeyNotFoundException($"Unknown item {id}.");
            }

            return item;
        }
    }
}

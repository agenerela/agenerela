using System;
using System.Collections.Generic;

namespace LivingVale.Game
{
    /// <summary>
    /// A trade target the vendor sells: an exact item and bundle size at an integer unit price, with
    /// finite remaining stock. Its <see cref="Id"/> (such as <c>stock_mara_potion_one</c>) is what a
    /// <c>quote_purchase</c> decision names, since the decision has no quantity field (design §9).
    /// </summary>
    public sealed class StockEntry
    {
        public string Id { get; }
        public string VendorId { get; }
        public string ItemId { get; }

        /// <summary>Units in one purchase of this entry.</summary>
        public int BundleSize { get; }

        public int UnitPrice { get; }

        /// <summary>Units the vendor still has. Saved; no restock exploit (design §5).</summary>
        public int Remaining { get; internal set; }

        public int BundlePrice => UnitPrice * BundleSize;

        public bool Available => Remaining >= BundleSize;

        public StockEntry(string id, string vendorId, string itemId, int bundleSize, int unitPrice, int remaining)
        {
            if (string.IsNullOrWhiteSpace(id) || bundleSize < 1 || unitPrice < 1 || remaining < 0)
            {
                throw new ArgumentException($"Stock entry {id}: needs an ID, bundle ≥ 1, price ≥ 1 and stock ≥ 0.");
            }

            Id = id;
            VendorId = vendorId;
            ItemId = itemId;
            BundleSize = bundleSize;
            UnitPrice = unitPrice;
            Remaining = remaining;
        }
    }

    /// <summary>
    /// A trade target the vendor buys from the player: an exact item and bundle size at an integer
    /// unit price. Offered only while the player holds a whole bundle (see <see cref="TradeService.SaleTargets"/>).
    /// </summary>
    public sealed class SaleBundle
    {
        public string Id { get; }
        public string VendorId { get; }
        public string ItemId { get; }
        public int BundleSize { get; }
        public int UnitPrice { get; }
        public int BundlePrice => UnitPrice * BundleSize;

        public SaleBundle(string id, string vendorId, string itemId, int bundleSize, int unitPrice)
        {
            if (string.IsNullOrWhiteSpace(id) || bundleSize < 1 || unitPrice < 1)
            {
                throw new ArgumentException($"Sale bundle {id}: needs an ID, bundle ≥ 1 and price ≥ 1.");
            }

            Id = id;
            VendorId = vendorId;
            ItemId = itemId;
            BundleSize = bundleSize;
            UnitPrice = unitPrice;
        }
    }

    /// <summary>
    /// One vendor's stock and buy list. Its <see cref="Revision"/> goes up whenever stock changes, so a
    /// quote made before is refused rather than recalculated (design §12). Vendors have unlimited
    /// gold in the first build.
    /// </summary>
    public sealed class VendorStock
    {
        public const string MaraId = "npc_mara";

        private readonly List<StockEntry> selling = new List<StockEntry>();
        private readonly List<SaleBundle> buying = new List<SaleBundle>();

        public VendorStock(string vendorId)
        {
            if (string.IsNullOrWhiteSpace(vendorId))
            {
                throw new ArgumentException("A vendor needs its NPC's stable ID.", nameof(vendorId));
            }

            VendorId = vendorId;
        }

        public string VendorId { get; }
        public long Revision { get; private set; }
        public IReadOnlyList<StockEntry> Selling => selling;
        public IReadOnlyList<SaleBundle> Buying => buying;

        /// <summary>
        /// Sale price for one unit: half the base price rounded down, at least one, and always below
        /// what any vendor charges for it, so buying and selling back never makes gold (design §5).
        /// </summary>
        public static int SalePriceFor(ItemDefinition item) => Math.Max(1, item.BasePrice / 2);

        /// <summary>
        /// Mara at the start of the first adventure: sells potions, bread and herb bundles; buys wolf
        /// pelts, iron scrap and herb bundles. Bundles of one, as design §9's initial catalog.
        /// </summary>
        public static VendorStock CreateMara(ItemCatalog catalog)
        {
            var mara = new VendorStock(MaraId);
            mara.Sell("stock_mara_potion_one", catalog.Get(ItemCatalog.HealingPotion), 1, 6);
            mara.Sell("stock_mara_bread_one", catalog.Get(ItemCatalog.Bread), 1, 10);
            mara.Sell("stock_mara_herb_one", catalog.Get(ItemCatalog.HerbBundle), 1, 8);
            mara.Buy("sale_mara_wolf_pelt_one", catalog.Get(ItemCatalog.WolfPelt), 1);
            mara.Buy("sale_mara_iron_scrap_one", catalog.Get(ItemCatalog.IronScrap), 1);
            mara.Buy("sale_mara_herb_one", catalog.Get(ItemCatalog.HerbBundle), 1);
            return mara;
        }

        public StockEntry Sell(string entryId, ItemDefinition item, int bundleSize, int stock)
        {
            if (item.IsProtected)
            {
                throw new ArgumentException($"Quest item {item.Id} cannot be stocked.");
            }

            var entry = new StockEntry(entryId, VendorId, item.Id, bundleSize, Math.Max(1, item.BasePrice), stock);
            selling.Add(entry);
            return entry;
        }

        public SaleBundle Buy(string bundleId, ItemDefinition item, int bundleSize)
        {
            if (item.IsProtected)
            {
                throw new ArgumentException($"Quest item {item.Id} cannot be bought from the player.");
            }

            var bundle = new SaleBundle(bundleId, VendorId, item.Id, bundleSize, SalePriceFor(item));
            buying.Add(bundle);
            return bundle;
        }

        public StockEntry FindEntry(string id) => selling.Find(e => e.Id == id);

        public SaleBundle FindBundle(string id) => buying.Find(b => b.Id == id);

        /// <summary>Why <see cref="TryRestoreRemaining"/> would refuse these counts, or null. Changes nothing.</summary>
        public string ValidateRemaining(IReadOnlyDictionary<string, int> remaining)
        {
            if (remaining == null)
            {
                return $"{VendorId}: missing stock";
            }

            foreach (var pair in remaining)
            {
                if (FindEntry(pair.Key) == null || pair.Value < 0)
                {
                    return $"{VendorId}: bad stock entry {pair.Key}";
                }
            }

            return null;
        }

        /// <summary>
        /// Sets remaining stock from a save, entry ID → units, checking every entry before changing
        /// any. Entries the save does not name keep their count. <see cref="Revision"/> goes up, so a
        /// quote made before the load is refused after it.
        /// </summary>
        public bool TryRestoreRemaining(IReadOnlyDictionary<string, int> remaining, out string error)
        {
            error = ValidateRemaining(remaining);
            if (error != null)
            {
                return false;
            }

            foreach (var pair in remaining)
            {
                FindEntry(pair.Key).Remaining = pair.Value;
            }

            Revision++;
            return true;
        }

        internal void Take(StockEntry entry, int units)
        {
            if (units < 0 || entry.Remaining < units)
            {
                throw new InvalidOperationException($"Stock {entry.Id} cannot give {units}; checked before committing.");
            }

            entry.Remaining -= units;
            Revision++;
        }
    }
}

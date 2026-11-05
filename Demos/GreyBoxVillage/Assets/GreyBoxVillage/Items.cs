using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Demos.GreyBoxVillage
{
    /// <summary>Everything a player can carry: its name, a line about it, and what Odile asks for it.</summary>
    public static class ItemCatalog
    {
        public sealed class Item
        {
            public string Id, Name, Description;
            public int Price;
        }

        public static readonly Item[] All =
        {
            new Item { Id = "bread", Name = "Bread", Description = "A round loaf, still warm.", Price = 2 },
            new Item { Id = "apple", Name = "Apple", Description = "Red and crisp.", Price = 1 },
            new Item { Id = "rope", Name = "Rope", Description = "Ten metres of good hemp rope.", Price = 6 },
            new Item { Id = "torch", Name = "Torch", Description = "Pitch-soaked; burns for an hour.", Price = 3 },
            new Item { Id = "lantern", Name = "Lantern", Description = "Iron and glass, with a fresh wick.", Price = 12 },
            new Item { Id = "moonpetal", Name = "Moonpetal", Description = "A pale flower from the woods that glows after dark.", Price = 0 },
            new Item { Id = "pelt", Name = "Wolf pelt", Description = "Grey fur, left behind by the wolf.", Price = 8 },
            new Item { Id = "old_lantern", Name = "Wren's lantern", Description = "Dented, with W scratched into the base.", Price = 0 },
        };

        public static Item Get(string id) => All.FirstOrDefault(i => i.Id == id);
    }

    /// <summary>The player's purse and pack. Raises <see cref="Changed"/> on every change.</summary>
    public sealed class PlayerInventory : MonoBehaviour
    {
        public int Coins = 8;
        private readonly Dictionary<string, int> items = new Dictionary<string, int>();

        /// <summary>Raised with the item id (or "coins") and the change in amount.</summary>
        public event Action<string, int> Changed;

        public int Count(string id) => items.TryGetValue(id, out int n) ? n : 0;
        public bool Has(string id, int count = 1) => Count(id) >= count;
        public IEnumerable<(string id, int count)> Items => items.Where(kv => kv.Value > 0).Select(kv => (kv.Key, kv.Value));

        public void Add(string id, int count = 1)
        {
            items[id] = Count(id) + count;
            Changed?.Invoke(id, count);
        }

        public bool Remove(string id, int count = 1)
        {
            if (!Has(id, count)) return false;
            items[id] = Count(id) - count;
            Changed?.Invoke(id, -count);
            return true;
        }

        public void AddCoins(int amount)
        {
            Coins += amount;
            Changed?.Invoke("coins", amount);
        }

        public bool Pay(int amount)
        {
            if (Coins < amount) return false;
            AddCoins(-amount);
            return true;
        }

        /// <summary>Empties the pack and sets the purse. Used between tests.</summary>
        public void Reset(int coins)
        {
            items.Clear();
            Coins = coins;
        }

        /// <summary>The pack as a sentence for an agent's observations, such as "2 moonpetal, 1 rope".</summary>
        public string Describe()
        {
            var parts = Items.Select(i => i.count + " " + i.id).ToList();
            return parts.Count == 0 ? "nothing" : string.Join(", ", parts);
        }
    }

    /// <summary>Something lying in the world the player can pick up with E. Bobs and turns so it is noticed.</summary>
    public sealed class Pickup : MonoBehaviour
    {
        public string ItemId;
        public int Count = 1;
        public Transform Visual;
        private Vector3 rest;

        public string Name => ItemCatalog.Get(ItemId)?.Name ?? ItemId;

        private void Start()
        {
            if (Visual != null) rest = Visual.localPosition;
        }

        private void Update()
        {
            if (Visual == null) return;
            Visual.localPosition = rest + Vector3.up * (0.12f + Mathf.Sin(Time.time * 2f + transform.position.x) * 0.06f);
            Visual.Rotate(0, 50f * Time.deltaTime, 0, Space.World);
        }

        /// <summary>Puts it in the inventory and takes it out of the world.</summary>
        public void Collect(PlayerInventory inventory)
        {
            inventory.Add(ItemId, Count);
            gameObject.SetActive(false);
        }

        /// <summary>Drops it here, back in the world. The wolf's pelt is dropped this way.</summary>
        public void Drop(Vector3 at)
        {
            transform.position = at;
            gameObject.SetActive(true);
        }
    }
}

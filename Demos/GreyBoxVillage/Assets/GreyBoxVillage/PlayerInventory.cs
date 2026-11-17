using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Demos.GreyBoxVillage
{
    /// <summary>The player's purse and pack. Raises <see cref="Changed"/> on every change.</summary>
    public sealed class PlayerInventory : MonoBehaviour
    {
        public int Coins = 8;
        // Made on first use: Unity can hand back a component restored after a domain reload whose
        // non-serialized fields were never initialised.
        private Dictionary<string, int> itemsField;
        private Dictionary<string, int> items => itemsField ??= new Dictionary<string, int>();

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
}

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Demos.GreyBoxVillage
{
    /// <summary>Everything a player can carry: its name, a line about it, and what Odile asks for it.</summary>
    public static class ItemCatalog
    {
        /// <summary>One item. Read-only, because <see cref="All"/> is static: a change made in one play session would outlive it when the editor skips the domain reload.</summary>
        public sealed class Item
        {
            public readonly string Id, Name, Description;
            public readonly int Price;

            public Item(string id, string name, string description, int price)
            {
                Id = id;
                Name = name;
                Description = description;
                Price = price;
            }
        }

        public static readonly IReadOnlyList<Item> All = Array.AsReadOnly(new[]
        {
            new Item("bread", "Bread", "A round loaf, still warm.", 2),
            new Item("apple", "Apple", "Red and crisp.", 1),
            new Item("rope", "Rope", "Ten metres of good hemp rope.", 6),
            new Item("torch", "Torch", "Pitch-soaked; burns for an hour.", 3),
            new Item("lantern", "Lantern", "Iron and glass, with a fresh wick.", 12),
            new Item("moonpetal", "Moonpetal", "A pale flower from the woods that glows after dark.", 0),
            new Item("pelt", "Wolf pelt", "Grey fur, left behind by the wolf.", 8),
            new Item("old_lantern", "Wren's lantern", "Dented, with W scratched into the base.", 0),
        });

        public static Item Get(string id) => All.FirstOrDefault(i => i.Id == id);
    }
}

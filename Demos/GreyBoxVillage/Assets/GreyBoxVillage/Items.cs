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
}

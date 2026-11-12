using System;

namespace GreyBoxStrategy
{
    /// <summary>
    /// A province's land, the five kinds of rules v2 (<c>docs/demos/painted-realm-rules-v2.md</c>,
    /// section 3.2). Carried by every map since R0; no rule reads it yet (R3 gives it its effects).
    /// </summary>
    public enum Terrain
    {
        Plains,
        Forest,
        Hills,
        Marsh,
        Mountains,
    }

    /// <summary>The numbers v2's tables give each terrain, used by the generator's fairness checks.</summary>
    /// <remarks>
    /// Sections 3.2 and 4.1 of the rules page, every number *to tune*. The rules do not read them yet;
    /// the generator does, so a world is judged fair by the same table the economy (R6) will use.
    /// </remarks>
    public static class TerrainTable
    {
        public static readonly Terrain[] All = { Terrain.Plains, Terrain.Forest, Terrain.Hills, Terrain.Marsh, Terrain.Mountains };

        /// <summary>Food a province of this terrain makes (3.2).</summary>
        public static int Food(Terrain t)
        {
            switch (t)
            {
                case Terrain.Plains: return 3;
                case Terrain.Forest: return 2;
                case Terrain.Hills: return 2;
                default: return 1;
            }
        }

        /// <summary>Population at the start, before coast and capital (4.1).</summary>
        public static int StartPopulation(Terrain t)
        {
            switch (t)
            {
                case Terrain.Plains: return 4;
                case Terrain.Forest: return 3;
                case Terrain.Hills: return 3;
                default: return 2;
            }
        }

        /// <summary>The goods a province of this terrain may make (4.5), in a fixed order; salt also on any coast.</summary>
        public static string[] Goods(Terrain t, bool coastal)
        {
            string[] own;
            switch (t)
            {
                case Terrain.Plains: own = new[] { "grain", "horses", "cloth", "wine" }; break;
                case Terrain.Forest: own = new[] { "timber", "cloth" }; break;
                case Terrain.Hills: own = new[] { "iron", "stone", "wine" }; break;
                case Terrain.Marsh: return new[] { "salt" };
                default: own = new[] { "iron", "stone" }; break;
            }
            if (!coastal) return own;
            var withSalt = new string[own.Length + 1];
            own.CopyTo(withSalt, 0);
            withSalt[own.Length] = "salt";
            return withSalt;
        }

        /// <summary>The goods v2 calls scarce: no starting realm holds more than one of them.</summary>
        public static readonly string[] Scarce = { "horses", "iron" };

        /// <summary>The name a map file uses: <c>plains</c>, <c>mountains</c>...</summary>
        public static string Id(Terrain t) => t.ToString().ToLowerInvariant();

        public static Terrain Parse(string id)
        {
            foreach (var t in All)
                if (string.Equals(Id(t), id, StringComparison.Ordinal)) return t;
            throw new ArgumentException($"Unknown terrain '{id}'. Expected one of plains, forest, hills, marsh, mountains.");
        }
    }
}

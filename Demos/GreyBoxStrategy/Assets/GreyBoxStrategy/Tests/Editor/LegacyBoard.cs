using System.Collections.Generic;

namespace GreyBoxStrategy.Tests
{
    /// <summary>
    /// The standard map exactly as <c>MapDefinition.Standard()</c> built it in code before rules v2's
    /// R0 moved it into data (<see cref="ClassicMap"/>). Kept so a test can compare the two field by
    /// field. Do not edit: it is the reference, not a map in use.
    /// </summary>
    internal static class LegacyBoard
    {
        public static MapDefinition Standard()
        {
            var countries = new[]
            {
                new CountryInfo("valen", "Valen", "Proud and quick to take offence; believes strength earns respect.", 40, 30),
                new CountryInfo("orsk", "Orsk", "A merchant republic; prefers coin to blood, never forgets a betrayal.", 40, 30),
                new CountryInfo("merrow", "Merrow", "Small and devout; defends what it has and trusts no one.", 40, 30),
                new CountryInfo("kesh", "Kesh", "Hill clans under one banner; fierce in war, quick to make peace when it pays.", 40, 30),
                new CountryInfo("ilvar", "Ilvar", "Ambitious and patient; strikes whoever is weakest.", 40, 30),
            };

            string[,] ids =
            {
                { "highmoor", "ashby", "stonereach", "corwick", "orskhaven" },
                { "brindle", "fenmark", "templegate", "saltmere", "redcliff" },
                { "westmarch", "harrow", "merrowdeep", "glimmerford", "eastwatch" },
                { "thornbury", "ravenholt", "pilgrimsrest", "duskvale", "ilmoor" },
                { "keshtar", "marrowfield", "southgate", "oakhollow", "ilvarion" },
            };
            string[,] names =
            {
                { "Highmoor", "Ashby", "Stonereach", "Corwick", "Orskhaven" },
                { "Brindle", "Fenmark", "Templegate", "Saltmere", "Redcliff" },
                { "Westmarch", "Harrow", "Merrowdeep", "Glimmerford", "Eastwatch" },
                { "Thornbury", "Ravenholt", "Pilgrim's Rest", "Duskvale", "Ilmoor" },
                { "Keshtar", "Marrowfield", "Southgate", "Oakhollow", "Ilvarion" },
            };
            var owners = new Dictionary<string, string>
            {
                ["highmoor"] = "valen", ["ashby"] = "valen", ["brindle"] = "valen",
                ["orskhaven"] = "orsk", ["corwick"] = "orsk", ["redcliff"] = "orsk",
                ["merrowdeep"] = "merrow", ["templegate"] = "merrow", ["pilgrimsrest"] = "merrow",
                ["keshtar"] = "kesh", ["thornbury"] = "kesh", ["marrowfield"] = "kesh",
                ["ilvarion"] = "ilvar", ["ilmoor"] = "ilvar", ["oakhollow"] = "ilvar",
            };
            var capitals = new HashSet<string> { "highmoor", "orskhaven", "merrowdeep", "keshtar", "ilvarion" };

            var provinces = new List<ProvinceInfo>();
            for (int r = 0; r < 5; r++)
            for (int c = 0; c < 5; c++)
            {
                string id = ids[r, c];
                owners.TryGetValue(id, out string owner);
                bool capital = capitals.Contains(id);
                provinces.Add(new ProvinceInfo(id, names[r, c], c, r, owner, capital, capital ? 1 : 0));
            }

            var borders = new List<(string, string)>();
            for (int r = 0; r < 5; r++)
            for (int c = 0; c < 5; c++)
            {
                if (c + 1 < 5) borders.Add((ids[r, c], ids[r, c + 1]));
                if (r + 1 < 5) borders.Add((ids[r, c], ids[r + 1, c]));
            }

            return new MapDefinition(countries, provinces, borders);
        }
    }
}

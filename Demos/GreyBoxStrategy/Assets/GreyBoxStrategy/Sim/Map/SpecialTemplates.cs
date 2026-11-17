using System;
using System.Collections.Generic;
using System.Linq;

namespace GreyBoxStrategy
{
    /// <summary>A province the code chose to hold a special place, and the templates its terrain allows.</summary>
    public sealed class SpecialSlot
    {
        public string Province;
        public Terrain Terrain;
        public string Region;
        public IReadOnlyList<string> Neighbours;
        /// <summary>The template ids this slot may carry, in table order.</summary>
        public IReadOnlyList<string> Allowed;
    }

    /// <summary>
    /// Fills a special-place slot: which template it carries, its name and its line of story. The
    /// tables do it today (<see cref="TableSpecialPlaces"/>); the world-maker agent (R2's agent half)
    /// and the Cartographer (R9) do it through the framework, one decision a slot: action = a template
    /// the slot allows, target = the slot's province, statement = <c>Name: story</c> (v2 section 2.5).
    /// </summary>
    /// <remarks>
    /// The generator re-checks what comes back: a template the slot does not allow, an empty name or a
    /// story that fails <see cref="SpecialTemplates.WhyNotStory"/> falls back to the tables, so a maker
    /// can make a world duller but never unfair or broken. A maker that needs a model call can do its
    /// calls first and return the answers from here.
    /// </remarks>
    public interface ISpecialPlaceMaker
    {
        SpecialPlace Make(SpecialSlot slot, WorldMap world);
    }

    /// <summary>One special-place template (v2 section 2.5): where it may stand, and its effect while held.</summary>
    public sealed class SpecialTemplate
    {
        public string Id;
        /// <summary>The terrains it may stand on.</summary>
        public Terrain[] Terrains;
        /// <summary>Its effect in words, from the page. No rule applies it until R3 and R6.</summary>
        public string Effect;
        /// <summary>The table's names and stories, used when no maker supplies one.</summary>
        public string[] Names;
        public string[] Stories;
    }

    /// <summary>The closed list of special-place templates, and the checks on what a maker writes.</summary>
    public static class SpecialTemplates
    {
        private static readonly Terrain[] Any = TerrainTable.All;

        /// <summary>
        /// The templates the generator may roll. <c>crown_seat</c> belongs to the Crownhold victory mode
        /// and is placed by it, not rolled, so it is not here.
        /// </summary>
        public static readonly SpecialTemplate[] All =
        {
            new SpecialTemplate
            {
                Id = "old_castle", Terrains = Any.Where(t => t != Terrain.Marsh).ToArray(),
                Effect = "walls level 2 from the start; +10% defence; a neutral garrison of 30",
                Names = new[] { "The Broken Keep", "Greywall", "The Old Watch", "Kingsrest Tower", "The Hollow Fort" },
                Stories = new[]
                {
                    "A keep older than any crown, its gate still barred from the inside.",
                    "Walls raised by a forgotten king, mended by every lord who held them since.",
                    "A ruined fort whose cellars are said to run under the whole valley.",
                    "The last tower of an empire nobody here remembers the name of.",
                },
            },
            new SpecialTemplate
            {
                Id = "silver_mine", Terrains = new[] { Terrain.Hills, Terrain.Mountains },
                Effect = "+8 gold a turn",
                Names = new[] { "The Bright Vein", "Silverdeep", "The Moonlit Shafts", "Coinhollow" },
                Stories = new[]
                {
                    "A seam of silver so pure the miners work it by its own shine.",
                    "Old shafts reopened after a landslide bared the ore again.",
                    "The mine paid for three wars and is not empty yet.",
                },
            },
            new SpecialTemplate
            {
                Id = "iron_deep", Terrains = new[] { Terrain.Hills, Terrain.Mountains },
                Effect = "provides iron; recruits there cost 25% less",
                Names = new[] { "The Red Pits", "Ironmouth", "The Smiths' Deep", "Anvil Gorge" },
                Stories = new[]
                {
                    "Red rock and black smoke; every blade in the region was born here.",
                    "Forges burn day and night beside pits that go down past the light.",
                    "A gorge where ore lies so near the surface that children dig it.",
                },
            },
            new SpecialTemplate
            {
                Id = "sacred_lake", Terrains = new[] { Terrain.Plains, Terrain.Forest, Terrain.Hills },
                Effect = "+1 stability for its holder, at most once",
                Names = new[] { "The Drowned Abbey", "Stillwater", "The Mirror Mere", "Saint's Pool" },
                Stories = new[]
                {
                    "A lake over a sunken chapel whose bells still ring in winter.",
                    "Pilgrims walk for weeks to wash in water that never freezes.",
                    "Its surface stays still even in a gale, and no fish live in it.",
                },
            },
            new SpecialTemplate
            {
                Id = "healing_spring", Terrains = Any,
                Effect = "armies there refill all stores and lose no men to attrition",
                Names = new[] { "The Warm Wells", "Mendwater", "The Green Spring", "Hale Fountain" },
                Stories = new[]
                {
                    "Hot water from deep rock that soldiers swear closes wounds overnight.",
                    "A spring the herb-women guard and every army leaves in peace.",
                    "Its water tastes of iron and is said to cure the marsh fever.",
                },
            },
            new SpecialTemplate
            {
                Id = "horse_plains", Terrains = new[] { Terrain.Plains },
                Effect = "provides horses; riders recruited there cost half",
                Names = new[] { "The Long Grass", "Stallion Downs", "The Wild Run", "Mare's Meadow" },
                Stories = new[]
                {
                    "Grass to a rider's knee and herds that nobody owns until they are caught.",
                    "Every spring the wild horses come down from the hills to foal here.",
                    "A plain so wide that its riders say the sky is closer here.",
                },
            },
            new SpecialTemplate
            {
                Id = "great_wood", Terrains = new[] { Terrain.Forest },
                Effect = "an army there is invisible to rivals; +15% defence",
                Names = new[] { "The Deepwood", "Greymantle Forest", "The Elder Trees", "Nightshade Wood" },
                Stories = new[]
                {
                    "Trees so old and close that an army can vanish between two scouts.",
                    "The woodfolk know paths here that no map has ever shown.",
                    "Hunters go in by day only, and never alone.",
                },
            },
            new SpecialTemplate
            {
                Id = "old_battlefield", Terrains = new[] { Terrain.Plains, Terrain.Hills },
                Effect = "the first battle fought there each war gives the winner's general a trait",
                Names = new[] { "The Field of Crows", "Bonehill", "The Quiet Field", "Last Stand Ridge" },
                Stories = new[]
                {
                    "Ploughs still turn up helmets from a battle the songs disagree about.",
                    "Two kings died here on the same day, and both sides claimed the victory.",
                    "Nothing grows taller than the grass on the old mass graves.",
                },
            },
        };

        public static SpecialTemplate Get(string id) => All.FirstOrDefault(t => t.Id == id);

        /// <summary>The templates a province of this terrain may carry, in table order.</summary>
        public static List<string> AllowedOn(Terrain t) => All.Where(x => x.Terrains.Contains(t)).Select(x => x.Id).ToList();

        /// <summary>
        /// Why a special place's story cannot be used, or null: at most 40 words, no digits, and no
        /// other place's name (the storyteller's story check, as v2 section 2.5 asks).
        /// </summary>
        public static string WhyNotStory(string story, IEnumerable<string> otherNames)
        {
            if (string.IsNullOrWhiteSpace(story)) return "the story is empty";
            if (story.Any(char.IsDigit)) return "the story has a digit";
            int words = story.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Length;
            if (words > 40) return $"the story is {words} words, over 40";
            string lower = story.ToLowerInvariant();
            foreach (string n in otherNames)
                if (!string.IsNullOrEmpty(n) && ContainsWord(lower, n.ToLowerInvariant())) return $"the story names {n}";
            return null;
        }

        private static bool ContainsWord(string text, string word)
        {
            int i = 0;
            while ((i = text.IndexOf(word, i, StringComparison.Ordinal)) >= 0)
            {
                bool before = i == 0 || !char.IsLetter(text[i - 1]);
                bool after = i + word.Length >= text.Length || !char.IsLetter(text[i + word.Length]);
                if (before && after) return true;
                i += word.Length;
            }
            return false;
        }
    }

    /// <summary>Fills slots from the tables: a template the slot allows, a name and a story, all by the seed.</summary>
    public sealed class TableSpecialPlaces : ISpecialPlaceMaker
    {
        private readonly int seed;

        public TableSpecialPlaces(int seed)
        {
            this.seed = seed;
        }

        public SpecialPlace Make(SpecialSlot slot, WorldMap world)
        {
            int index = world.Provinces.FindIndex(p => p.Id == slot.Province);
            var template = SpecialTemplates.Get(slot.Allowed[MapRandom.Int(seed, "special-template", index, slot.Allowed.Count)]);
            var used = new HashSet<string>(world.Specials.Select(s => s.Name), StringComparer.Ordinal);
            int n0 = MapRandom.Int(seed, "special-name", index, template.Names.Length);
            string name = template.Names[n0];
            for (int k = 0; k < template.Names.Length && used.Contains(name); k++) name = template.Names[(n0 + k) % template.Names.Length];
            if (used.Contains(name)) name = template.Names[n0] + " of " + NameRules.Display(slot.Province);
            return new SpecialPlace
            {
                Province = slot.Province,
                Template = template.Id,
                Name = name,
                Story = template.Stories[MapRandom.Int(seed, "special-story", index, template.Stories.Length)],
                MadeBy = "table",
            };
        }
    }
}

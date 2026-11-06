using System;
using System.Collections.Generic;

namespace LivingVale.Game
{
    /// <summary>One named place in the valley: a stable ID, the name the HUD shows, and where it is.</summary>
    public sealed class ValePlace
    {
        public string Id { get; }
        public string DisplayName { get; }

        /// <summary>Centre in world metres, on the ground plane (y is 0).</summary>
        public float X { get; }
        public float Z { get; }

        /// <summary>Roughly how far the place reaches from its centre, for discovery and labels.</summary>
        public float Radius { get; }

        public ValePlace(string id, string displayName, float x, float z, float radius)
        {
            Id = id;
            DisplayName = displayName;
            X = x;
            Z = z;
            Radius = radius;
        }
    }

    /// <summary>
    /// The region layout from the content list (<c>docs/demos/living-vale-content.md</c>, "Region
    /// layout"), as data, so the world builders, quest triggers, art placement and the HUD all use
    /// one map. Plain C#, no scene: positions are numbers, not Transforms.
    /// </summary>
    public static class ValePlaces
    {
        public const string BriarGlen = "place_briar_glen";
        public const string OldRoad = "place_old_road";
        public const string Reedbank = "place_reedbank";
        public const string Ashwatch = "place_ashwatch";
        public const string BrokenWatch = "place_broken_watch";
        public const string HerbHollow = "place_herb_hollow";

        /// <summary>The South meadow is part of the Glen, where G2's first encounter waits.</summary>
        public const float SouthMeadowX = 0f;
        public const float SouthMeadowZ = -30f;

        /// <summary>Half the side of the square ground plane, centred on the Glen.</summary>
        public const float GroundHalfExtent = 300f;

        public static readonly IReadOnlyList<ValePlace> All = new[]
        {
            new ValePlace(BriarGlen, "Briar Glen", 0f, 0f, 24f),
            new ValePlace(OldRoad, "The Old Road", 40f, -150f, 30f),
            new ValePlace(Reedbank, "Reedbank", 170f, -110f, 28f),
            new ValePlace(Ashwatch, "Ashwatch Gate", 230f, 60f, 24f),
            new ValePlace(BrokenWatch, "Broken Watch", -80f, -270f, 24f),
            new ValePlace(HerbHollow, "Herb Hollow", -150f, -20f, 18f)
        };

        /// <summary>The paths, as pairs of place IDs (design §2's map).</summary>
        public static readonly IReadOnlyList<(string from, string to)> Paths = new[]
        {
            (BriarGlen, OldRoad),
            (BriarGlen, HerbHollow),
            (OldRoad, Reedbank),
            (Reedbank, Ashwatch),
            (OldRoad, BrokenWatch),
            (HerbHollow, BrokenWatch)
        };

        public static ValePlace Get(string id)
        {
            foreach (var place in All)
            {
                if (place.Id == id)
                {
                    return place;
                }
            }

            throw new KeyNotFoundException($"Unknown place {id}.");
        }

        /// <summary>The place whose footprint contains (x, z), or null between places.</summary>
        public static ValePlace At(float x, float z)
        {
            foreach (var place in All)
            {
                float dx = x - place.X, dz = z - place.Z;
                if (dx * dx + dz * dz <= place.Radius * place.Radius)
                {
                    return place;
                }
            }

            return null;
        }
    }
}

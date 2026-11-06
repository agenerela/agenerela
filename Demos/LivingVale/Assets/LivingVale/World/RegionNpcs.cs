using System.Collections.Generic;
using LivingVale.Game;

namespace LivingVale.World
{
    /// <summary>
    /// One of the six speaking NPCs added with the region (G4b): the IDs and roles of the content list,
    /// where they stand and walk, and the body colour that tells them apart in the grey box.
    /// </summary>
    public sealed class RegionNpc
    {
        public string Id { get; }
        public string Name { get; }

        /// <summary>The short role the chat header shows ("Innkeeper").</summary>
        public string Role { get; }

        public string PlaceId { get; }

        /// <summary>The routine; the first stop is where the NPC is placed.</summary>
        public IReadOnlyList<RoutineStop> Stops { get; }

        /// <summary>Body colour, red, green and blue in 0–1.</summary>
        public (float r, float g, float b) Colour { get; }

        public RegionNpc(string id, string name, string role, string placeId, (float, float, float) colour,
            params RoutineStop[] stops)
        {
            Id = id;
            Name = name;
            Role = role;
            PlaceId = placeId;
            Colour = colour;
            Stops = stops;
        }
    }

    /// <summary>
    /// The six NPCs of G4b, as data, so the scene builder, the tests and later quest work agree on who
    /// stands where. Positions are world metres around the <see cref="ValePlaces"/> centres; every
    /// routine is two or three stops with a clear straight line between them.
    /// </summary>
    public static class RegionNpcs
    {
        public const string SellaId = "npc_sella";
        public const string TomaId = "npc_toma";
        public const string FenId = "npc_fen";
        public const string BrannId = "npc_brann";
        public const string ElraId = "npc_elra";
        public const string KestId = "npc_kest";

        /// <summary>NPC walking speed in m/s, a stroll beside the player's 3 m/s walk.</summary>
        public const float WalkSpeed = 1.2f;

        public static readonly IReadOnlyList<RegionNpc> All = new[]
        {
            // In front of the inn (the Glen's east cottage), kept more than 6 m from Iven so the
            // player can switch speakers before leaving the first one's range.
            new RegionNpc(SellaId, "Sella", "Innkeeper", ValePlaces.BriarGlen, (0.72f, 0.33f, 0.52f),
                new RoutineStop(11f, 4.2f, 6f), new RoutineStop(14.5f, 3.4f, 5f), new RoutineStop(12.2f, 1.6f, 4f)),

            // Reedbank: the workshop is south-west of the centre, the fields east, both off the paths.
            new RegionNpc(TomaId, "Toma", "Smith", ValePlaces.Reedbank, (0.24f, 0.24f, 0.27f),
                new RoutineStop(158f, -119.5f, 7f), new RoutineStop(153f, -118f, 4f), new RoutineStop(163f, -118f, 4f)),
            new RegionNpc(FenId, "Fen", "Farm worker", ValePlaces.Reedbank, (0.56f, 0.68f, 0.3f),
                new RoutineStop(182f, -112f, 5f), new RoutineStop(190f, -110.5f, 6f), new RoutineStop(186f, -120f, 6f)),

            // Ashwatch: Brann keeps the closed gate, Elra paces the forecourt in front of it.
            new RegionNpc(BrannId, "Brann", "Gate guard", ValePlaces.Ashwatch, (0.58f, 0.12f, 0.12f),
                new RoutineStop(233.5f, 66f, 8f), new RoutineStop(237.5f, 64f, 5f)),
            new RegionNpc(ElraId, "Elra", "Castle steward", ValePlaces.Ashwatch, (0.86f, 0.74f, 0.34f),
                new RoutineStop(224f, 60f, 6f), new RoutineStop(220f, 64.5f, 5f), new RoutineStop(226.5f, 56f, 5f)),

            // Broken Watch: Kest walks between the camp's centre and its edges.
            new RegionNpc(KestId, "Kest", "Camp leader", ValePlaces.BrokenWatch, (0.2f, 0.53f, 0.55f),
                new RoutineStop(-80f, -266f, 7f), new RoutineStop(-74.5f, -270f, 5f), new RoutineStop(-84f, -273.5f, 5f))
        };

        public static RegionNpc Get(string id)
        {
            foreach (var npc in All)
            {
                if (npc.Id == id)
                {
                    return npc;
                }
            }

            throw new KeyNotFoundException($"Unknown region NPC {id}.");
        }
    }
}

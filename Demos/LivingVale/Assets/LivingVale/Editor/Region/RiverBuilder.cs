using LivingVale.Game;
using UnityEngine;

namespace LivingVale.Editor.Region
{
    /// <summary>
    /// The river, the natural boundary that makes Reedbank's bridge the only way to Ashwatch (design §2:
    /// "river, cliffs, forest density and closed gates"). It runs along the north edge of Reedbank from
    /// the east cliff, then turns north to the north cliff, so Ashwatch's corner of the map is reached
    /// only across the bridge, as the map's paths say. The bridge sits where the Reedbank–Ashwatch path
    /// crosses the water, computed from <see cref="ValePlaces"/>, so moving a place moves the bridge.
    /// </summary>
    internal static class RiverBuilder
    {
        /// <summary>The east–west leg's centre line, north of Reedbank's centre.</summary>
        public const float SouthLegZ = -95f;

        /// <summary>The north–south leg's centre line, between the Old Road and Reedbank.</summary>
        public const float WestLegX = 120f;

        public const float Width = 8f;
        public const float BridgeWidth = 4f;

        public static float BridgeX
        {
            get
            {
                ValePlace a = ValePlaces.Get(ValePlaces.Reedbank), b = ValePlaces.Get(ValePlaces.Ashwatch);
                float t = (SouthLegZ - a.Z) / (b.Z - a.Z);
                return a.X + t * (b.X - a.X);
            }
        }

        public static void Build(Transform region)
        {
            Transform root = RegionKit.Root("River", region);
            float half = ValePlaces.GroundHalfExtent;
            float westEdge = WestLegX - Width * 0.5f;

            // Water: a flat strip just above the ground, covering the path where it passes under the bridge.
            float southLength = half - westEdge;
            RegionKit.Primitive(root, "River south leg", PrimitiveType.Cube,
                new Vector3(westEdge + southLength * 0.5f, -0.05f, SouthLegZ),
                new Vector3(southLength, 0.1f, Width), RegionKit.Water, collider: false);
            float westLength = half - (SouthLegZ - Width * 0.5f);
            RegionKit.Primitive(root, "River west leg", PrimitiveType.Cube,
                new Vector3(WestLegX, -0.05f, SouthLegZ - Width * 0.5f + westLength * 0.5f),
                new Vector3(Width, 0.1f, westLength), RegionKit.Water, collider: false);

            // Invisible walls over the water, open only at the bridge.
            float bridge = BridgeX;
            float gapWest = bridge - BridgeWidth * 0.5f, gapEast = bridge + BridgeWidth * 0.5f;
            RegionKit.Blocker(root, "River wall south west", new Vector3((westEdge + gapWest) * 0.5f, 1.5f, SouthLegZ),
                new Vector3(gapWest - westEdge, 3f, Width));
            RegionKit.Blocker(root, "River wall south east", new Vector3((gapEast + half) * 0.5f, 1.5f, SouthLegZ),
                new Vector3(half - gapEast, 3f, Width));
            RegionKit.Blocker(root, "River wall west", new Vector3(WestLegX, 1.5f, SouthLegZ - Width * 0.5f + westLength * 0.5f),
                new Vector3(Width, 3f, westLength));

            // The bridge: a timber deck a low step above the bank, longer than the water, with rails over the water.
            float deckLength = Width + 4f;
            RegionKit.Primitive(root, "Reedbank bridge deck", PrimitiveType.Cube, new Vector3(bridge, 0.1f, SouthLegZ),
                new Vector3(BridgeWidth, 0.2f, deckLength), RegionKit.Timber);
            for (int side = -1; side <= 1; side += 2)
            {
                RegionKit.Primitive(root, side < 0 ? "Bridge rail west" : "Bridge rail east", PrimitiveType.Cube,
                    new Vector3(bridge + side * (BridgeWidth * 0.5f + 0.1f), 0.7f, SouthLegZ),
                    new Vector3(0.2f, 1f, Width), RegionKit.Timber); // Over the water only: the path meets the deck at an angle.
            }

            // Reeds along the banks, for the village's name and to mark the water from a distance.
            var random = new System.Random(6271);
            for (int i = 0; i < 40; i++)
            {
                float x = westEdge + (float)random.NextDouble() * (half - westEdge);
                if (Mathf.Abs(x - bridge) < 4f)
                {
                    continue;
                }

                float bank = random.Next(2) == 0 ? -1f : 1f;
                RegionKit.Primitive(root, $"Reeds {i}", PrimitiveType.Cylinder,
                    new Vector3(x, 0.6f, SouthLegZ + bank * (Width * 0.5f + 0.4f)),
                    new Vector3(0.25f, 0.6f, 0.25f), RegionKit.Crop, collider: false);
            }
        }
    }
}

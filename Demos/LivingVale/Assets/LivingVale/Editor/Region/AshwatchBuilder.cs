using LivingVale.Game;
using UnityEngine;

namespace LivingVale.Editor.Region
{
    /// <summary>
    /// Ashwatch Castle's gate (design §2: "silhouette, gatehouse and courtyard; most interior doors
    /// closed"): a paved forecourt at the courtyard's edge where Brann and Elra stand, the curtain wall
    /// with a gatehouse whose gate is shut, and behind it a walled-in keep and towers tall enough to be
    /// seen from Reedbank. The enclosure is closed on every side; the courtyard opens in a later build.
    /// </summary>
    internal static class AshwatchBuilder
    {
        private const float WallHeight = 8f;

        public static void Build(Transform region)
        {
            Transform root = RegionKit.Root("Ashwatch Castle", region);
            ValePlace place = ValePlaces.Get(ValePlaces.Ashwatch);
            float x = place.X, z = place.Z;
            Material paving = RegionKit.Material("RegionPaving", new Color(0.55f, 0.55f, 0.52f));
            Material wall = RegionKit.Material("RegionCastleStone", new Color(0.6f, 0.58f, 0.54f));
            Material banner = RegionKit.Material("RegionBanner", new Color(0.55f, 0.12f, 0.12f));

            // The forecourt, open to the south, where the path from Reedbank arrives.
            RegionKit.Yard(root, "Ashwatch forecourt", x, z + 2f, 38f, 20f, paving);
            RegionKit.Block(root, "Ashwatch forecourt parapet west", x - 19f, z + 2.5f, 1f, 19f, 1.2f, wall);
            RegionKit.Block(root, "Ashwatch forecourt parapet east", x + 19f, z + 2.5f, 1f, 19f, 1.2f, wall);

            // The curtain wall and the enclosure behind it: south (with the gatehouse), sides and back.
            float front = z + 13f, back = z + 70f, west = x - 40f, east = x + 40f;
            RegionKit.Block(root, "Ashwatch curtain wall west", (west + x - 6f) * 0.5f, front, x - 6f - west, 2f, WallHeight, wall);
            RegionKit.Block(root, "Ashwatch curtain wall east", (x + 6f + east) * 0.5f, front, east - x - 6f, 2f, WallHeight, wall);
            RegionKit.Block(root, "Ashwatch wall west side", west, (front + back) * 0.5f, 2f, back - front, WallHeight, wall);
            RegionKit.Block(root, "Ashwatch wall east side", east, (front + back) * 0.5f, 2f, back - front, WallHeight, wall);
            RegionKit.Block(root, "Ashwatch wall back", x, back, east - west + 2f, 2f, WallHeight, wall);
            for (int i = 0; i < 16; i++)
            {
                float mx = west + 2f + i * 5f;
                if (Mathf.Abs(mx - x) < 7f)
                {
                    continue;
                }

                RegionKit.Block(root, $"Ashwatch merlon {i + 1}", mx, front, 1.4f, 2f, 1.2f, wall, baseY: WallHeight);
            }

            // The gatehouse fills the gap in the curtain wall; its gate is closed (a solid timber panel).
            RegionKit.Block(root, "Ashwatch gatehouse", x, front + 1f, 12f, 8f, 12f, wall);
            RegionKit.Block(root, "Ashwatch gate (closed)", x, front - 3.1f, 4.4f, 0.4f, 5.5f, RegionKit.Timber);
            RegionKit.Block(root, "Ashwatch gate arch", x, front - 3.2f, 5.4f, 0.3f, 0.6f, wall, baseY: 5.5f);
            for (int side = -1; side <= 1; side += 2)
            {
                RegionKit.Primitive(root, side < 0 ? "Ashwatch gate tower west" : "Ashwatch gate tower east",
                    PrimitiveType.Cylinder, new Vector3(x + side * 7.5f, 7f, front - 1f), new Vector3(6f, 7f, 6f), wall);
                RegionKit.Block(root, side < 0 ? "Ashwatch banner west" : "Ashwatch banner east",
                    x + side * 3.6f, front - 3.2f, 1.2f, 0.1f, 3f, banner, baseY: 7f);
            }

            // Behind the wall, for the silhouette: the keep and three towers.
            RegionKit.Block(root, "Ashwatch keep", x, z + 45f, 24f, 20f, 24f, wall);
            RegionKit.Block(root, "Ashwatch keep roof", x, z + 45f, 26f, 22f, 1f, RegionKit.Rock, baseY: 24f);
            Tower(root, "Ashwatch tower west", x - 30f, z + 30f, 26f, wall);
            Tower(root, "Ashwatch tower east", x + 30f, z + 32f, 28f, wall);
            Tower(root, "Ashwatch tower high", x + 8f, z + 60f, 36f, wall);
            RegionKit.Block(root, "Ashwatch flag", x + 8f, z + 60f, 0.2f, 2.2f, 1.4f, banner, baseY: 38f);
            RegionKit.Block(root, "Ashwatch flagpole", x + 8f, z + 61.2f, 0.2f, 0.2f, 4f, RegionKit.Timber, baseY: 36f);
        }

        private static void Tower(Transform root, string name, float x, float z, float height, Material material)
        {
            RegionKit.Primitive(root, name, PrimitiveType.Cylinder, new Vector3(x, height * 0.5f, z),
                new Vector3(8f, height * 0.5f, 8f), material);
            RegionKit.Primitive(root, $"{name} cap", PrimitiveType.Cylinder, new Vector3(x, height + 0.5f, z),
                new Vector3(9.5f, 0.5f, 9.5f), RegionKit.Rock);
        }
    }
}

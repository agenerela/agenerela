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
            var walls = new[]
            {
                RegionKit.Block(root, "Ashwatch curtain wall west", (west + x - 6f) * 0.5f, front, x - 6f - west, 2f, WallHeight, wall),
                RegionKit.Block(root, "Ashwatch curtain wall east", (x + 6f + east) * 0.5f, front, east - x - 6f, 2f, WallHeight, wall),
                RegionKit.Block(root, "Ashwatch wall west side", west, (front + back) * 0.5f, 2f, back - front, WallHeight, wall),
                RegionKit.Block(root, "Ashwatch wall east side", east, (front + back) * 0.5f, 2f, back - front, WallHeight, wall),
                RegionKit.Block(root, "Ashwatch wall back", x, back, east - west + 2f, 2f, WallHeight, wall)
            };
            var merlons = new System.Collections.Generic.List<GameObject>();
            for (int i = 0; i < 16; i++)
            {
                float mx = west + 2f + i * 5f;
                if (Mathf.Abs(mx - x) < 7f)
                {
                    continue;
                }

                merlons.Add(RegionKit.Block(root, $"Ashwatch merlon {i + 1}", mx, front, 1.4f, 2f, 1.2f, wall, baseY: WallHeight));
            }

            // G7b: the kit's 6 m wall module at 1.6x, so 8 m like the blocks, outer face out. The blocks keep
            // their colliders; the modules' crenellations replace the merlons.
            bool dressed = Tile(walls[0], west, front, x - 6f, front, 180f);
            dressed &= Tile(walls[1], x + 6f, front, east, front, 180f);
            dressed &= Tile(walls[2], west, front, west, back, -90f);
            dressed &= Tile(walls[3], east, front, east, back, 90f);
            dressed &= Tile(walls[4], west, back, east, back, 0f);
            if (dressed)
            {
                foreach (GameObject merlon in merlons)
                {
                    ArtDressing.HideLook(merlon);
                }
            }

            // The gatehouse fills the gap in the curtain wall; its gate is closed (a solid timber panel).
            GameObject gatehouse = RegionKit.Block(root, "Ashwatch gatehouse", x, front + 1f, 12f, 8f, 12f, wall);
            RegionKit.Block(root, "Ashwatch gate (closed)", x, front - 3.1f, 4.4f, 0.4f, 5.5f, RegionKit.Timber);
            GameObject arch = RegionKit.Block(root, "Ashwatch gate arch", x, front - 3.2f, 5.4f, 0.3f, 0.6f, wall, baseY: 5.5f);
            var banners = new GameObject[2];
            for (int side = -1; side <= 1; side += 2)
            {
                RegionKit.Primitive(root, side < 0 ? "Ashwatch gate tower west" : "Ashwatch gate tower east",
                    PrimitiveType.Cylinder, new Vector3(x + side * 7.5f, 7f, front - 1f), new Vector3(6f, 7f, 6f), wall);
                banners[(side + 1) / 2] = RegionKit.Block(root, side < 0 ? "Ashwatch banner west" : "Ashwatch banner east",
                    x + side * 3.6f, front - 3.2f, 1.2f, 0.1f, 3f, banner, baseY: 7f);
            }

            // G7b: the kit gatehouse at 1.6x (14.7 m wide, 4.8 m deep) with its face on the closed gate's
            // line. The closed timber gate stays visible in its passage, so the gate still reads as shut.
            if (ArtDressing.Dress(gatehouse, "ashwatch_gate", new Vector3(x, 0f, front - 0.8f), 180f, 1.6f) != null)
            {
                ArtDressing.HideLook(arch);
                ArtDressing.HideLook(banners[0]);
                ArtDressing.HideLook(banners[1]);
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

        // Kit wall modules along one block, stretched a little so a whole number of them fills it.
        private static bool Tile(GameObject block, float x0, float z0, float x1, float z1, float yaw)
        {
            float dx = x1 - x0, dz = z1 - z0;
            float length = Mathf.Sqrt(dx * dx + dz * dz);
            int count = Mathf.Max(1, Mathf.RoundToInt(length / 9.6f));
            float module = length / count;
            Transform parent = block.transform.parent;
            for (int i = 0; i < count; i++)
            {
                float t = (i + 0.5f) / count;
                GameObject piece = ArtDressing.Decorate(parent, "ashwatch_wall",
                    new Vector3(x0 + dx * t, 0f, z0 + dz * t), yaw);
                if (piece == null)
                {
                    return false;
                }

                piece.transform.localScale = new Vector3(module / 6f, 1.6f, 1.6f);
                piece.name = $"{block.name} module {i + 1}";
            }

            ArtDressing.HideLook(block);
            return true;
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

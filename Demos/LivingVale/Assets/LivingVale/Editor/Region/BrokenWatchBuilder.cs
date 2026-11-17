using LivingVale.Game;
using UnityEngine;

namespace LivingVale.Editor.Region
{
    internal static class BrokenWatchBuilder
    {
        public static void Build(Transform region)
        {
            Transform root = RegionKit.Root("Broken Watch", region);
            ValePlace place = ValePlaces.Get(ValePlaces.BrokenWatch);
            RegionKit.Yard(root, "Broken Watch camp floor", place.X, place.Z, 40f, 40f, RegionKit.Dirt);
            var random = new System.Random(2907);
            string[] sides = { "north", "east", "south", "west" };

            for (int side = 0; side < sides.Length; side++)
            {
                bool horizontal = side % 2 == 0;
                float edge = side < 2 ? 15f : -15f;
                for (int segment = 0; segment < 6; segment++)
                {
                    float along = -12.5f + segment * 5f;
                    float x = place.X + (horizontal ? along : edge);
                    float z = place.Z + (horizontal ? edge : along);
                    float height = 1f + (float)random.NextDouble() * 3f;
                    // Include each segment's half-diagonal and the path's half-width.
                    if (RegionKit.NearPath(x, z, 4.1f))
                    {
                        continue;
                    }

                    GameObject block = RegionKit.Block(root, $"Broken Watch wall {sides[side]} {segment + 1}", x, z,
                        horizontal ? 4f : 1f, horizontal ? 1f : 4f, height, RegionKit.Stone);
                    // G7b: a ruined wall scaled to the block's length and height; the block keeps its collider.
                    ArtDressing.Dress(block, "ruin_wall", new Vector3(x, 0f, z), (horizontal ? 0f : 90f) + (segment % 2) * 180f,
                        new Vector3(0.8f, height / 2.67f, 0.8f));
                }
            }

            float towerX = place.X - 15f;
            float towerZ = place.Z - 15f;
            if (!RegionKit.NearPath(towerX, towerZ, 5f))
            {
                // Unity cylinders have an unscaled height of two metres.
                RegionKit.Primitive(root, "Broken Watch tower stump", PrimitiveType.Cylinder,
                    new Vector3(towerX, 3f, towerZ), new Vector3(5f, 3f, 5f), RegionKit.Rock);
            }

            Vector2[] tentOffsets = { new Vector2(-10f, -8f), new Vector2(10f, -8f), new Vector2(11f, 3f) };
            for (int i = 0; i < tentOffsets.Length; i++)
            {
                float x = place.X + tentOffsets[i].x;
                float z = place.Z + tentOffsets[i].y;
                // The margin covers both roof panels, including their rotated colliders.
                if (RegionKit.NearPath(x, z, 5.5f))
                {
                    continue;
                }

                var panels = new GameObject[2];
                for (int panel = 0; panel < 2; panel++)
                {
                    float side = panel == 0 ? -1f : 1f;
                    string slope = panel == 0 ? "west roof" : "east roof";
                    GameObject roof = RegionKit.Primitive(root, $"Broken Watch tent {i + 1} {slope}",
                        PrimitiveType.Cube, new Vector3(x + side * 1.06f, 1.13f, z),
                        new Vector3(3f, 0.12f, 5f), RegionKit.Canvas);
                    roof.transform.rotation = Quaternion.Euler(0f, 0f, -side * 45f);
                    panels[panel] = roof;
                }

                // G7b: the kit tent at 1.5x (3.75 m wide, the panels' span), its opening toward the fire.
                ArtDressing.DressAll(panels, "camp_tent", new Vector3(x, 0f, z), i < 2 ? 0f : -90f, Vector3.one * 1.5f);
            }

            // Keep the entire fire ring outside the central eight-metre walking area.
            var fireStones = new System.Collections.Generic.List<GameObject>();
            float fireX = place.X;
            float fireZ = place.Z - 10.5f;
            for (int i = 0; i < 8; i++)
            {
                float angle = i * Mathf.PI / 4f;
                float x = fireX + Mathf.Cos(angle) * 1.25f;
                float z = fireZ + Mathf.Sin(angle) * 1.25f;
                if (RegionKit.NearPath(x, z, 4f))
                {
                    continue;
                }

                fireStones.Add(RegionKit.Block(root, $"Broken Watch fire stone {i + 1}", x, z,
                    0.45f, 0.4f, 0.25f, RegionKit.Stone, yaw: i * 45f));
            }

            GameObject embers = RegionKit.Primitive(root, "Broken Watch campfire embers", PrimitiveType.Cylinder,
                new Vector3(fireX, 0.08f, fireZ), new Vector3(1.7f, 0.08f, 1.7f),
                RegionKit.Ember, collider: false);
            // G7b: the kit campfire at 2.5x, its ring on the stones' ring; the stones keep their colliders.
            if (ArtDressing.Dress(embers, "campfire", new Vector3(fireX, 0f, fireZ), 0f, 2.5f) != null)
            {
                foreach (GameObject stone in fireStones)
                {
                    ArtDressing.HideLook(stone);
                }
            }

            for (int i = 0; i < 3; i++)
            {
                float x = place.X - 11f;
                float z = place.Z + 1f + i * 1.5f;
                if (!RegionKit.NearPath(x, z, 4f))
                {
                    GameObject crate = RegionKit.Block(root, $"Broken Watch supply crate {i + 1}", x, z,
                        1f, 1f, 1f, RegionKit.Timber);
                    ArtDressing.Dress(crate, i == 1 ? "barrel" : "crate", new Vector3(x, 0f, z), i * 35f, i == 1 ? 1f : 1.43f);
                }
            }
        }
    }
}

using LivingVale.Game;
using UnityEngine;

namespace LivingVale.Editor.Region
{
    /// <summary>
    /// Reedbank (design §2: "workshop, farm plots, river crossing"): Toma's workshop south-west of the
    /// centre, Fen's fields to the east, two cottages, and the river along the north edge, whose bridge
    /// <see cref="RiverBuilder"/> builds. The paths enter from the west-south-west (Old Road) and leave
    /// north for the bridge, so everything solid stays off those lines.
    /// </summary>
    internal static class ReedbankBuilder
    {
        public static void Build(Transform region)
        {
            Transform root = RegionKit.Root("Reedbank", region);
            ValePlace place = ValePlaces.Get(ValePlaces.Reedbank);
            float x = place.X, z = place.Z;
            Material earth = RegionKit.Material("RegionVillageEarth", new Color(0.5f, 0.46f, 0.36f));
            RegionKit.Yard(root, "Reedbank yard", x, z - 5f, 50f, 30f, earth);

            // Toma's workshop: door on the north side, a forge and an anvil out front.
            var workshop = new[]
            {
                RegionKit.Block(root, "Reedbank workshop", x - 12f, z - 14f, 8f, 6f, 3.6f, RegionKit.Stone),
                RegionKit.Block(root, "Reedbank workshop roof", x - 12f, z - 14f, 8.8f, 6.8f, 0.4f, RegionKit.Timber, baseY: 3.6f),
                RegionKit.Block(root, "Reedbank workshop door", x - 12f, z - 10.95f, 1.4f, 0.12f, 2.2f, RegionKit.Timber)
            };
            // G7b: cottage_a (walls 5 x 4, door on the long side) fitted to the 8 x 6 block, door north
            // like the primitive's.
            ArtDressing.DressAll(workshop, "cottage_a", new Vector3(x - 12f, 0f, z - 14f), 0f, new Vector3(1.6f, 1.3f, 1.5f));
            ArtDressing.Decorate(root, "barrel", new Vector3(x - 6.4f, 0f, z - 10.6f), 10f);
            ArtDressing.Decorate(root, "crate", new Vector3(x - 6.6f, 0f, z - 13.4f), 25f);
            ArtDressing.Decorate(root, "sack", new Vector3(x + 12.6f, 0f, z - 3f), 70f);
            RegionKit.Primitive(root, "Reedbank forge chimney", PrimitiveType.Cylinder, new Vector3(x - 9f, 4.5f, z - 15.5f),
                new Vector3(1f, 2f, 1f), RegionKit.Rock);
            RegionKit.Block(root, "Reedbank forge", x - 8f, z - 12f, 1.5f, 1.5f, 1.2f, RegionKit.Rock);
            RegionKit.Primitive(root, "Reedbank forge coals", PrimitiveType.Cube, new Vector3(x - 8f, 1.22f, z - 12f),
                new Vector3(1.1f, 0.05f, 1.1f), RegionKit.Ember, collider: false);
            RegionKit.Block(root, "Reedbank anvil", x - 14f, z - 10.3f, 0.6f, 1f, 0.8f, RegionKit.Rock);

            // Fen's fields: low crop rows without colliders, so walking through them is like walking a field.
            for (int field = 0; field < 2; field++)
            {
                float fz = z - 6f - field * 9f;
                // A thin raised bed: a patch level with the yard would z-fight with it.
                RegionKit.Block(root, $"Reedbank field {field + 1}", x + 18f, fz, 10f, 6f, 0.06f, RegionKit.Dirt);
                for (int row = 0; row < 4; row++)
                {
                    RegionKit.Primitive(root, $"Reedbank field {field + 1} row {row + 1}", PrimitiveType.Cube,
                        new Vector3(x + 18f, 0.2f, fz - 2.25f + row * 1.5f), new Vector3(9f, 0.4f, 0.5f),
                        RegionKit.Crop, collider: false);
                }
            }

            RegionKit.Block(root, "Reedbank scarecrow post", x + 24f, z - 10.5f, 0.2f, 0.2f, 2.2f, RegionKit.Timber);
            RegionKit.Block(root, "Reedbank scarecrow arms", x + 24f, z - 10.5f, 1.6f, 0.15f, 0.15f, RegionKit.Timber, baseY: 1.6f);

            Cottage(root, "Reedbank cottage east", x + 8f, z - 18f, -90f);
            Cottage(root, "Reedbank cottage north west", x - 22f, z + 6.5f, 90f);
        }

        private static void Cottage(Transform root, string name, float x, float z, float doorYaw)
        {
            var parts = new[]
            {
                RegionKit.Block(root, name, x, z, 6f, 5f, 3.2f, RegionKit.Stone),
                RegionKit.Block(root, $"{name} roof", x, z, 6.6f, 5.6f, 0.4f, RegionKit.Timber, baseY: 3.2f)
            };
            // G7b: cottage_b (stone, slate; walls 4.4 x 6, door in the gable) turned a quarter so its length
            // runs along the 6 x 5 block's X, door toward the yard.
            ArtDressing.DressAll(parts, "cottage_b", new Vector3(x, 0f, z), doorYaw, new Vector3(5f / 4.4f, 1f, 1f));
        }
    }
}

using LivingVale.Game;
using UnityEngine;

namespace LivingVale.Editor.Region
{
    internal static class HerbHollowBuilder
    {
        public static void Build(Transform region)
        {
            Transform root = RegionKit.Root("Herb Hollow", region);
            ValePlace place = ValePlaces.Get(ValePlaces.HerbHollow);
            Material meadow = RegionKit.Material("RegionMeadow", new Color(0.43f, 0.54f, 0.33f));
            RegionKit.Yard(root, "Herb Hollow clearing", place.X, place.Z, 30f, 30f, meadow);

            RegionKit.TreeRing(root, "Hollow tree", place.X, place.Z,
                inner: 16f, outer: 30f, count: 90, seed: 1703,
                trunk: RegionKit.Bark, crown: RegionKit.Leaves);
            RegionKit.TreeRing(root, "Hollow outer tree", place.X, place.Z,
                inner: 30f, outer: 38f, count: 35, seed: 1704,
                trunk: RegionKit.Bark, crown: RegionKit.Leaves);

            // The western landmark leaves the central eight metres and both approaches open.
            float stoneX = place.X - 11f;
            float stoneZ = place.Z;
            if (!RegionKit.NearPath(stoneX, stoneZ, 4f))
            {
                RegionKit.Block(root, "Herb Hollow standing stone", stoneX, stoneZ,
                    1.5f, 1f, 7f, RegionKit.Rock, yaw: 12f);
            }

            for (int i = 0; i < 6; i++)
            {
                float angle = i * Mathf.PI / 3f;
                float x = stoneX + Mathf.Cos(angle) * 2f;
                float z = stoneZ + Mathf.Sin(angle) * 2f;
                if (RegionKit.NearPath(x, z, 4f))
                {
                    continue;
                }

                RegionKit.Block(root, $"Herb Hollow circle stone {i + 1}", x, z,
                    0.65f, 0.6f, 0.45f, RegionKit.Stone, yaw: i * 60f);
            }

            // Reserve empty herb spots at offsets (-7, 10), (7, 10), (-9, -10).
        }
    }
}

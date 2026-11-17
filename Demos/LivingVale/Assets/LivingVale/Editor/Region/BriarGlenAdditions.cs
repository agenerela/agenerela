using UnityEngine;

namespace LivingVale.Editor.Region
{
    /// <summary>
    /// What the region adds to the G1 village: the inn's sign and lantern on the east cottage, where
    /// Sella works. The village itself stays <see cref="VillageSceneBuilder"/>'s.
    /// </summary>
    internal static class BriarGlenAdditions
    {
        public static void Build(Transform region)
        {
            Transform root = RegionKit.Root("Briar Glen additions", region);
            Material sign = RegionKit.Material("RegionInnSign", new Color(0.62f, 0.2f, 0.35f));

            // The east cottage is at (11, 8) with its door facing south at z = 5.95.
            RegionKit.Block(root, "Inn sign post", 14.2f, 5.4f, 0.2f, 0.2f, 3f, RegionKit.Timber);
            RegionKit.Block(root, "Inn sign arm", 14.2f, 5.4f, 1.4f, 0.15f, 0.15f, RegionKit.Timber, baseY: 2.8f);
            RegionKit.Block(root, "Inn sign board", 14.6f, 5.4f, 1f, 0.1f, 0.7f, sign, baseY: 2f);
            RegionKit.Primitive(root, "Inn lantern", PrimitiveType.Sphere, new Vector3(9.8f, 2.6f, 5.75f),
                new Vector3(0.3f, 0.35f, 0.3f), RegionKit.Ember, collider: false);
            RegionKit.Block(root, "Inn bench", 8.6f, 4.6f, 1.8f, 0.5f, 0.45f, RegionKit.Timber);
        }
    }
}

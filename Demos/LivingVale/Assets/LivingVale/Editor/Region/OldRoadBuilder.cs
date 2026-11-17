using LivingVale.Game;
using UnityEngine;

namespace LivingVale.Editor.Region
{
    /// <summary>
    /// The Old Road's woodland bend (design §2: "wagon wreck, woodland bend and short ambush area"),
    /// where the paths from the Glen, Reedbank and Broken Watch meet. The wreck site at the centre is
    /// left empty, a bare patch and an anchor: the quest routes build the wreck and its pickups there.
    /// </summary>
    internal static class OldRoadBuilder
    {
        public const string WreckAnchorName = "Old Road wreck site";

        public static void Build(Transform region)
        {
            Transform root = RegionKit.Root("Old Road", region);
            ValePlace place = ValePlaces.Get(ValePlaces.OldRoad);
            Material trampled = RegionKit.Material("RegionTrampled", new Color(0.46f, 0.4f, 0.3f));

            // The bend: a trampled clearing about 60 × 30 along the road, the wreck site at its middle.
            RegionKit.Yard(root, "Old Road bend", place.X, place.Z, 60f, 30f, trampled, yaw: 15f);
            var anchor = new GameObject(WreckAnchorName).transform;
            anchor.SetParent(root, false);
            anchor.position = new Vector3(place.X, 0f, place.Z);

            // Woodland close on both sides; TreeRing keeps the three roads clear.
            RegionKit.TreeRing(root, "Old Road tree", place.X, place.Z, 17f, 45f, 140, 3301, RegionKit.Bark, RegionKit.Leaves);

            // Boulders by the roadside: cover for a short ambush, off the road itself.
            var random = new System.Random(3302);
            int placed = 0;
            for (int i = 0; i < 30 && placed < 7; i++)
            {
                double angle = random.NextDouble() * Mathf.PI * 2f;
                float radius = 9f + (float)random.NextDouble() * 7f;
                float bx = place.X + Mathf.Cos((float)angle) * radius;
                float bz = place.Z + Mathf.Sin((float)angle) * radius;
                if (RegionKit.NearPath(bx, bz, 5f))
                {
                    continue;
                }

                float size = 1.2f + (float)random.NextDouble() * 1.6f;
                float yaw = (float)random.NextDouble() * 90f;
                GameObject boulder = RegionKit.Block(root, $"Old Road boulder {++placed}", bx, bz, size * 1.3f, size, size,
                    RegionKit.Rock, yaw: yaw);
                // G7b: a kit rock about the block's size (rock_a is 2.9 x 1.4 x 1.8 m); the block keeps its collider.
                ArtDressing.Dress(boulder, placed % 2 == 0 ? "rock_b" : "rock_a", new Vector3(bx, 0f, bz), yaw,
                    new Vector3(size * 1.3f / (placed % 2 == 0 ? 1.6f : 2.9f) * 1.15f, size / (placed % 2 == 0 ? 0.72f : 1.41f),
                        size / (placed % 2 == 0 ? 1.3f : 1.84f) * 1.15f));
            }

            // A milestone where the Glen's road meets the bend, to name the place from the path.
            RegionKit.Block(root, "Old Road milestone", place.X - 6f, place.Z + 12f, 0.6f, 0.4f, 1.3f, RegionKit.Stone);
        }
    }
}

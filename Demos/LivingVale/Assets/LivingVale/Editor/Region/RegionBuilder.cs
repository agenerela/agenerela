using LivingVale.Game;
using LivingVale.UI;
using LivingVale.Unity;
using LivingVale.World;
using UnityEngine;

namespace LivingVale.Editor.Region
{
    /// <summary>
    /// Builds Briar Vale around the G1 village (G4b): the 600 × 600 ground, the paths between places, the
    /// river, the natural boundaries, distant mountains for scale, one builder per place, the six region
    /// NPCs and the place HUD. <see cref="VillageSceneBuilder.Build"/> calls it once; the layout is
    /// <see cref="ValePlaces"/>, the NPCs <see cref="RegionNpcs"/>, so nothing here invents a position
    /// another session needs.
    /// </summary>
    internal static class RegionBuilder
    {
        public static void Build(ValeBootstrap bootstrap)
        {
            RegionKit.Reset();
            Transform region = RegionKit.Root("Briar Vale region");

            Ground(region);
            Paths(region);
            RiverBuilder.Build(region);
            Boundaries(region);
            DistantScenery(region);

            BriarGlenAdditions.Build(region);
            OldRoadBuilder.Build(region);
            ReedbankBuilder.Build(region);
            AshwatchBuilder.Build(region);
            BrokenWatchBuilder.Build(region);
            HerbHollowBuilder.Build(region);

            Transform npcs = RegionKit.Root("Region NPCs");
            foreach (var npc in RegionNpcs.All)
            {
                Speaker(npcs, npc);
            }

            // Distance haze: hides the far clip and makes the mountains read as far away.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.55f, 0.63f, 0.7f);
            RenderSettings.fogStartDistance = 140f;
            RenderSettings.fogEndDistance = 950f;

            new GameObject("Place HUD", typeof(RectTransform)).AddComponent<PlaceHud>().Configure(bootstrap);
        }

        private static void Ground(Transform region)
        {
            float side = ValePlaces.GroundHalfExtent * 2f;
            RegionKit.Primitive(region, "Vale ground", PrimitiveType.Cube, new Vector3(0f, RegionKit.GroundTop - 0.5f, 0f),
                new Vector3(side, 1f, side), RegionKit.Grass);
        }

        private static void Paths(Transform region)
        {
            Transform paths = RegionKit.Root("Paths", region);
            foreach (var (from, to) in ValePlaces.Paths)
            {
                ValePlace a = ValePlaces.Get(from), b = ValePlaces.Get(to);
                float dx = b.X - a.X, dz = b.Z - a.Z;
                float length = Mathf.Sqrt(dx * dx + dz * dz);
                float yaw = Mathf.Atan2(dx, dz) * Mathf.Rad2Deg;
                RegionKit.Primitive(paths, $"Path {a.DisplayName} to {b.DisplayName}", PrimitiveType.Cube,
                    new Vector3((a.X + b.X) * 0.5f, RegionKit.PathTop - 0.05f, (a.Z + b.Z) * 0.5f),
                    new Vector3(RegionKit.PathWidth, 0.1f, length + RegionKit.PathWidth), RegionKit.Dirt, yaw);
            }
        }

        // Cliffs on all four sides: the player cannot walk off the world, and the camera sees rock, not void.
        private static void Boundaries(Transform region)
        {
            Transform edge = RegionKit.Root("Boundary cliffs", region);
            float half = ValePlaces.GroundHalfExtent;
            const float thickness = 10f, height = 22f;
            float length = half * 2f + thickness * 2f;
            RegionKit.Block(edge, "Cliff north", 0f, half + thickness * 0.5f, length, thickness, height, RegionKit.Rock);
            RegionKit.Block(edge, "Cliff south", 0f, -half - thickness * 0.5f, length, thickness, height, RegionKit.Rock);
            RegionKit.Block(edge, "Cliff east", half + thickness * 0.5f, 0f, thickness, length, height, RegionKit.Rock);
            RegionKit.Block(edge, "Cliff west", -half - thickness * 0.5f, 0f, thickness, length, height, RegionKit.Rock);

            // Broken crests along each cliff, so the edge is not one flat wall.
            var random = new System.Random(4031);
            for (int i = 0; i < 48; i++)
            {
                float along = -half + (float)random.NextDouble() * half * 2f;
                float h = height + 4f + (float)random.NextDouble() * 14f;
                float w = 14f + (float)random.NextDouble() * 20f;
                int side = i % 4;
                float x = side == 0 ? along : side == 1 ? along : side == 2 ? half + 6f : -half - 6f;
                float z = side == 0 ? half + 6f : side == 1 ? -half - 6f : along;
                RegionKit.Block(edge, $"Cliff crest {i}", x, z, side < 2 ? w : 12f, side < 2 ? 12f : w, h,
                    RegionKit.Rock, yaw: (float)random.NextDouble() * 20f - 10f);
            }
        }

        // Mountains beyond the cliffs: scale without acreage (design §2). No colliders, never reachable.
        private static void DistantScenery(Transform region)
        {
            Transform far = RegionKit.Root("Distant mountains", region);
            var random = new System.Random(5113);
            for (int i = 0; i < 28; i++)
            {
                float angle = i / 28f * Mathf.PI * 2f + (float)random.NextDouble() * 0.15f;
                float distance = 430f + (float)random.NextDouble() * 140f;
                float width = 120f + (float)random.NextDouble() * 120f;
                float height = 90f + (float)random.NextDouble() * 150f;
                RegionKit.Primitive(far, $"Mountain {i}", PrimitiveType.Sphere,
                    new Vector3(Mathf.Cos(angle) * distance, 0f, Mathf.Sin(angle) * distance),
                    new Vector3(width, height * 2f, width * 0.8f), RegionKit.Mountain,
                    yaw: (float)random.NextDouble() * 180f, collider: false);
            }
        }

        // NPC bodies move, so unlike the scenery they are not static.
        private static void Speaker(Transform parent, RegionNpc npc)
        {
            RoutineStop first = npc.Stops[0];
            var root = new GameObject(npc.Name);
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(new Vector3(first.X, 0f, first.Z), Quaternion.Euler(0f, 180f, 0f));
            root.AddComponent<NpcSpeaker>().Configure(npc.Id, npc.Name, npc.Role);
            root.AddComponent<NpcRoutineMover>().Configure(npc.Stops, RegionNpcs.WalkSpeed);

            var (r, g, b) = npc.Colour;
            Material material = RegionKit.Material($"Npc{npc.Name}", new Color(r, g, b));
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = $"{npc.Name} body";
            body.transform.SetParent(root.transform, false);
            body.transform.localPosition = Vector3.up;
            body.transform.localScale = new Vector3(0.8f, 1f, 0.8f);
            body.GetComponent<Renderer>().sharedMaterial = material;

            // A small block on the chest shows which way the NPC faces as it walks and turns to talk.
            GameObject front = GameObject.CreatePrimitive(PrimitiveType.Cube);
            front.name = $"{npc.Name} front";
            front.transform.SetParent(root.transform, false);
            front.transform.localPosition = new Vector3(0f, 1.35f, 0.38f);
            front.transform.localScale = new Vector3(0.3f, 0.2f, 0.1f);
            front.GetComponent<Renderer>().sharedMaterial = RegionKit.Canvas;
            Object.DestroyImmediate(front.GetComponent<Collider>());
        }
    }
}

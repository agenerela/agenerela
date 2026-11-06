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
            Scatter(region);
            Signposts(region);

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

        // G7b: trees and rocks along the paths and near the cliffs, off every path, yard, the river and the
        // castle. They keep their kit colliders: none stands where anyone walks, talks or fights.
        private static void Scatter(Transform region)
        {
            Transform root = RegionKit.Root("Scattered trees and rocks", region);
            var random = new System.Random(7207);
            int n = 0;
            foreach (var (from, to) in ValePlaces.Paths)
            {
                ValePlace a = ValePlaces.Get(from), b = ValePlaces.Get(to);
                float dx = b.X - a.X, dz = b.Z - a.Z;
                float length = Mathf.Sqrt(dx * dx + dz * dz);
                float ux = dx / length, uz = dz / length;
                for (float t = a.Radius; t < length - b.Radius; t += 7f)
                {
                    for (int side = -1; side <= 1; side += 2)
                    {
                        double roll = random.NextDouble();
                        float offset = 7f + (float)random.NextDouble() * 12f;
                        float along = t + (float)random.NextDouble() * 5f;
                        float variety = (float)random.NextDouble();
                        if (roll < 0.35)
                        {
                            continue;
                        }

                        Put(root, roll, a.X + ux * along - uz * offset * side, a.Z + uz * along + ux * offset * side,
                            variety, ref n);
                    }
                }
            }

            float half = ValePlaces.GroundHalfExtent;
            for (int i = 0; i < 160; i++)
            {
                float along = -half + (float)random.NextDouble() * half * 2f;
                float inset = 8f + (float)random.NextDouble() * 22f;
                double roll = 0.4 + random.NextDouble() * 0.6;
                float variety = (float)random.NextDouble();
                int edge = i % 4;
                float px = edge < 2 ? along : edge == 2 ? half - inset : -half + inset;
                float pz = edge == 0 ? half - inset : edge == 1 ? -half + inset : along;
                Put(root, roll, px, pz, variety, ref n);
            }
        }

        private static void Put(Transform root, double roll, float x, float z, float variety, ref int n)
        {
            if (!Clear(x, z))
            {
                return;
            }

            string id = roll < 0.5 ? (variety < 0.5f ? "rock_a" : "rock_b")
                : roll < 0.8 ? "tree_pine" : "tree_broadleaf";
            bool rock = roll < 0.5;
            float scale = rock ? 0.8f + variety * 1.4f : 0.8f + variety * 0.6f;
            GameObject model = ArtDressing.Decorate(root, id, new Vector3(x, RegionKit.GroundTop, z), variety * 360f,
                scale, collide: true);
            if (model != null)
            {
                model.name = $"{id} {++n}";
            }
        }

        // Off the paths, outside every place, away from the river and the castle, inside the cliffs.
        private static bool Clear(float x, float z)
        {
            if (RegionKit.NearPath(x, z, RegionKit.PathWidth * 0.5f + 3f) || !RegionKit.InsideBoundary(x, z, 6f))
            {
                return false;
            }

            foreach (ValePlace place in ValePlaces.All)
            {
                float px = x - place.X, pz = z - place.Z;
                if (px * px + pz * pz < (place.Radius + 10f) * (place.Radius + 10f))
                {
                    return false;
                }
            }

            const float riverMargin = RiverBuilder.Width * 0.5f + 4f;
            bool river = (Mathf.Abs(x - RiverBuilder.WestLegX) < riverMargin && z > RiverBuilder.SouthLegZ - riverMargin)
                || (Mathf.Abs(z - RiverBuilder.SouthLegZ) < riverMargin && x > RiverBuilder.WestLegX - riverMargin);
            ValePlace castle = ValePlaces.Get(ValePlaces.Ashwatch);
            bool keep = x > castle.X - 50f && x < castle.X + 50f && z > castle.Z - 5f && z < castle.Z + 80f;
            return !river && !keep;
        }

        // A signpost where each path leaves a place, beside the road and turned to face along it.
        private static void Signposts(Transform region)
        {
            Transform root = RegionKit.Root("Signposts", region);
            foreach (var (from, to) in ValePlaces.Paths)
            {
                for (int end = 0; end < 2; end++)
                {
                    ValePlace a = ValePlaces.Get(end == 0 ? from : to), b = ValePlaces.Get(end == 0 ? to : from);
                    float dx = b.X - a.X, dz = b.Z - a.Z;
                    float length = Mathf.Sqrt(dx * dx + dz * dz);
                    float ux = dx / length, uz = dz / length;
                    float along = a.Radius + 3f;
                    var position = new Vector3(a.X + ux * along + uz * 3.6f, RegionKit.GroundTop, a.Z + uz * along - ux * 3.6f);
                    GameObject post = ArtDressing.Decorate(root, "signpost", position, Mathf.Atan2(dx, dz) * Mathf.Rad2Deg + 90f);
                    if (post != null)
                    {
                        post.name = $"Signpost {a.DisplayName} to {b.DisplayName}";
                    }
                }
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

            // G7b: the kit humanoid in the NPC's outfit; the capsule keeps its collider for sight lines.
            // The humanoid's face shows which way it looks, so the front block is only the fallback's.
            if (ArtKit.SwapBody(body, ArtDressing.OutfitFor(npc.Id)) != null)
            {
                return;
            }

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

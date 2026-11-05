using System.Collections.Generic;
using System.Linq;
using Demos.Shared;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Demos.GreyBoxVillage.Editor
{
    /// <summary>
    /// The art pass: dresses the grey-box village in the Blender kit (<c>ArtSource/build_kit.py</c>)
    /// and gives it light, sky, fog, post-processing, fire, smoke and a day.
    /// </summary>
    /// <remarks>
    /// The grey boxes stay exactly where they were, as invisible colliders: the NavMesh is baked from
    /// them and every test of paths, places and sight still runs against them. The models are
    /// visual-only children of <c>VillageArt</c>, with no colliders, so dressing the village can never
    /// change where anyone can walk or what anyone can see.
    /// </remarks>
    public static partial class VillageSceneBuilder
    {
        public const string ArtFolder = Root + "/Art";
        public const string ModelsFolder = ArtFolder + "/Models";
        public const string PalettePath = ArtFolder + "/VillagePalette.mat";

        // The houses: position, footprint, model and which way the door faces (for the chimney smoke).
        private static readonly (float x, float z, float w, float d, string model, bool chimneyEast)[] Houses =
        {
            (-8f, 8f, 5f, 4f, "cottage_a", false), (8f, 10f, 5f, 4f, "cottage_b", true),
            (-32f, 6f, 6f, 5f, "cottage_c", false), (31f, 9f, 5f, 5f, "cottage_d", false),
            (-6f, -9f, 4f, 4f, "cottage_e", true), (32f, -9f, 5f, 4f, "cottage_f", true)
        };

        private static readonly List<(string name, float a, float b, float at, float height, bool alongX)> walls =
            new List<(string, float, float, float, float, bool)>();
        private static readonly List<Vector3> trees = new List<Vector3>();

        // The roads the terrain paints, as in build_kit.py: decoration keeps off them.
        private static readonly Vector2[][] Paths =
        {
            new[] { V(0, -40), V(0, -15), V(0, -2.4f), V(16, -0.5f), V(33, 0), V(60, 0) },
            new[] { V(0, -2.4f), V(0, 23), V(0, 30), V(-20, 33), V(-40, 38) },
            new[] { V(0, 30), V(18, 32.6f) },
            new[] { V(0, -2.4f), V(-12.6f, -8) },
            new[] { V(0, -2.4f), V(-18, 8) },
            new[] { V(0, -6), V(14, -8.2f), V(21, -12) },
            new[] { V(16, -0.5f), V(30, 18.5f) },
        };

        private static Material palette, waterMaterial, sky, particles;
        private static Transform art;
        private static readonly List<FlickerLight> lamps = new List<FlickerLight>();
        private static readonly List<ParticleSystem> nightOnly = new List<ParticleSystem>();

        private static Vector2 V(float x, float z) => new Vector2(x, z);

        private static void LoadArt()
        {
            palette = AssetDatabase.LoadAssetAtPath<Material>(PalettePath);
            if (palette == null)
            {
                palette = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "VillagePalette" };
                AssetDatabase.CreateAsset(palette, PalettePath);
            }
            palette.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(ArtFolder + "/Palette.png"));
            palette.SetColor("_BaseColor", Color.white);
            palette.SetTexture("_EmissionMap", AssetDatabase.LoadAssetAtPath<Texture2D>(ArtFolder + "/PaletteEmission.png"));
            palette.SetColor("_EmissionColor", Color.white * 0.12f);
            palette.EnableKeyword("_EMISSION");
            palette.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            palette.SetFloat("_Smoothness", 0.08f);
            palette.SetFloat("_EnvironmentReflections", 0f);
            palette.SetFloat("_SpecularHighlights", 0f);
            palette.DisableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            palette.enableInstancing = true;
            EditorUtility.SetDirty(palette);

            waterMaterial = Asset(ArtFolder + "/VillageWater.mat", () =>
                new Material(AssetDatabase.LoadAssetAtPath<Shader>(ArtFolder + "/Shaders/VillageWater.shader")));
            sky = Asset(ArtFolder + "/VillageSky.mat", () =>
            {
                var m = new Material(Shader.Find("Skybox/Procedural"));
                m.SetFloat("_SunSize", 0.035f);
                m.SetFloat("_AtmosphereThickness", 0.85f);
                m.SetColor("_GroundColor", new Color(0.33f, 0.36f, 0.3f));
                return m;
            });
            particles = AssetDatabase.LoadAssetAtPath<Material>(
                "Packages/com.unity.render-pipelines.universal/Runtime/Materials/ParticlesUnlit.mat");
        }

        private static T Asset<T>(string path, System.Func<T> make) where T : Object
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;
            var created = make();
            AssetDatabase.CreateAsset(created, path);
            return created;
        }

        // ---- Placing models ---------------------------------------------------------------------

        /// <summary>
        /// Places a kit model. Models are authored in the village's own axes; the FBX import turns
        /// them half round, which the extra 180 degrees undoes.
        /// </summary>
        private static Transform Model(string name, Transform parent, Vector3 position, float yaw = 0f,
            float scale = 1f, bool isStatic = true)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(ModelsFolder + "/" + name + ".fbx");
            if (asset == null) throw new System.InvalidOperationException("Missing kit model " + name + ": run ArtSource/build_kit.py.");
            var go = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localRotation = Quaternion.Euler(0, 180f + yaw, 0);
            go.transform.localScale = Vector3.one * scale;
            foreach (var r in go.GetComponentsInChildren<MeshRenderer>(true))
            {
                r.sharedMaterial = palette;
                r.shadowCastingMode = ShadowCastingMode.On;
            }
            if (isStatic)
                foreach (var t in go.GetComponentsInChildren<Transform>(true))
                    GameObjectUtility.SetStaticEditorFlags(t.gameObject,
                        StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
            return go.transform;
        }

        /// <summary>Gives a character or creature its segmented figure, animated by <see cref="SegmentedFigure"/>.</summary>
        private static void Figure(GameObject owner, string model)
        {
            var figure = Model(model, owner.transform, Vector3.zero, isStatic: false);
            // A character's front is +Z; FBX import and the extra half turn already agree on that.
            var animator = owner.AddComponent<SegmentedFigure>();
            animator.Model = figure;
            if (model == "wolf")
            {
                animator.Swing = 30f;
                animator.StepsPerMetre = 1.6f;
            }
        }

        // ---- The dressing -----------------------------------------------------------------------

        private static DayNightCycle Dress(Camera camera, Light sun, VillageAlarm alarm)
        {
            lamps.Clear();
            nightOnly.Clear();
            var geometry = GameObject.Find("VillageGeometry").transform;
            foreach (var r in geometry.GetComponentsInChildren<MeshRenderer>(true)) r.enabled = false;
            foreach (var r in GameObject.Find("RestrictedZones").GetComponentsInChildren<MeshRenderer>(true)) r.enabled = false;

            art = new GameObject("VillageArt").transform;
            var ground = Group("Ground");
            Model("terrain", ground, Vector3.zero);
            var water = Model("water", ground, new Vector3(0, -0.35f, 0));
            foreach (var r in water.GetComponentsInChildren<MeshRenderer>()) { r.sharedMaterial = waterMaterial; r.shadowCastingMode = ShadowCastingMode.Off; }

            var buildings = Group("Buildings");
            Model("gate_house", buildings, P("village_gate"));
            Model("well", buildings, P("well") + new Vector3(0, 0, 2.4f));
            var market = P("market");
            Model("stall_bread", buildings, market + new Vector3(-4, 0, 4));
            Model("stall_goods", buildings, market + new Vector3(0, 0, 4));
            Model("stall_fruit", buildings, market + new Vector3(4, 0, 4));
            Model("shelter", buildings, P("shelter"));
            Model("bridge", buildings, P("bridge"));
            Model("docks", buildings, P("docks"));
            Model("north_tower", buildings, P("north_tower"));
            Model("armory", buildings, new Vector3(-25, 0, -8));
            Model("keep", buildings, new Vector3(18, 0, 43.5f));
            Model("cave", buildings, new Vector3(-40, 0, 45));
            foreach (var (x, z, _, _, model, _) in Houses) Model(model, buildings, new Vector3(x, 0, z));
            DressWalls(Group("Walls"));

            var camp = Group("Camp");
            var fire = P("campfire") + new Vector3(0, 0, -1.8f);
            Model("campfire", camp, fire);
            Model("seat_log", camp, P("campfire") + new Vector3(0, 0, -3.2f));
            Model("seat_log", camp, fire + new Vector3(-1.6f, 0, 0.4f), 90f);
            Model("guard_tent", camp, new Vector3(17.5f, 0, -13.3f));
            Model("weapon_rack", camp, new Vector3(11.8f, 0, -14f), 180f);
            Model("training_dummy", camp, P("training_dummy") + new Vector3(0, 0, 1.3f));
            Model("woodpile", camp, new Vector3(10.6f, 0, -6.5f), 90f);
            Model("bell_frame", camp, P("alarm_bell"));
            var bell = Model("bell", camp, P("alarm_bell") + new Vector3(0, 3.15f, 0), isStatic: false);
            alarm.Bell = bell.GetComponentInChildren<MeshRenderer>();
            alarm.BellPivot = bell;
            Fire(camp, fire + new Vector3(0, 0.15f, 0), 1f, 3.2f, 9f, false);

            var props = Group("Props");
            DressMarket(props, market);
            DressStreets(props);
            DressRiver(props);
            DressNature(Group("Nature"));
            Smoke(props);
            Fireflies(props);

            return Atmosphere(camera, sun);
        }

        private static Transform Group(string name)
        {
            var go = new GameObject(name).transform;
            go.SetParent(art, false);
            return go;
        }

        private static Vector3 P(string id)
        {
            var go = GameObject.Find(id);
            if (go == null) throw new System.InvalidOperationException("No place called " + id);
            return go.transform.position;
        }

        private static void DressWalls(Transform parent)
        {
            foreach (var (name, a, b, at, height, alongX) in walls)
            {
                string model = name == "village_wall" || name == "keep_wall" ? "wall_stone"
                    : name == "armory_fence" ? "wall_palisade" : null;
                if (model == null) continue;
                float length = Mathf.Abs(b - a);
                int pieces = Mathf.Max(1, Mathf.RoundToInt(length / 2f));
                float piece = length / pieces;
                float lo = Mathf.Min(a, b);
                for (int i = 0; i < pieces; i++)
                {
                    float c = lo + piece * (i + 0.5f);
                    var position = alongX ? new Vector3(c, 0, at) : new Vector3(at, 0, c);
                    var t = Model(model, parent, position, alongX ? 0f : 90f);
                    float tall = model == "wall_stone" ? height / 2f : height / 1.6f;
                    t.localScale = new Vector3(piece / 2f, tall, 1f);
                }
            }
            // Corner towers where the village wall turns north.
            foreach (var x in new[] { -45f, 45f })
            {
                var t = Model("wall_stone", parent, new Vector3(x, 0, -15), 0f);
                t.localScale = new Vector3(0.6f, 1.45f, 1.3f);
            }
        }

        private static void DressMarket(Transform parent, Vector3 market)
        {
            Model("barrel", parent, market + new Vector3(-6.2f, 0, 4.8f));
            Model("barrel", parent, market + new Vector3(-6.4f, 0, 3.8f), 40f, 0.9f);
            Model("crate", parent, market + new Vector3(6.3f, 0, 4.9f), 15f);
            Model("crate", parent, market + new Vector3(6.4f, 0, 3.7f), -10f, 0.8f);
            Model("sack", parent, market + new Vector3(-2f, 0, 5.6f));
            Model("sack", parent, market + new Vector3(2.1f, 0, 5.7f), 50f, 0.9f);
            Model("cart", parent, market + new Vector3(8.5f, 0, -3.5f), -35f);
            Model("hay_bale", parent, market + new Vector3(10.3f, 0, -1.8f), 20f);
            Model("banner_red", parent, market + new Vector3(-6.6f, 0, 2.2f));
            Model("banner_blue", parent, market + new Vector3(6.6f, 0, 2.2f));
            Lamp(parent, market + new Vector3(-2.2f, 0, 1.6f));
            Lamp(parent, market + new Vector3(2.2f, 0, 1.6f));
        }

        private static void DressStreets(Transform parent)
        {
            // Lamps along the roads from the gate to the square, the market and the bridge.
            foreach (var p in new[] { V(2.6f, -12.5f), V(-2.6f, -7f), V(2.6f, -2.5f), V(7f, 1.6f), V(-2.6f, 6f),
                         V(2.6f, 12f), V(-2.6f, 18.4f), V(-3.5f, 28.5f), V(14f, 31.2f), V(-12f, -6.4f), V(22.5f, -2.6f) })
                Lamp(parent, new Vector3(p.x, 0, p.y));
            Model("bench", parent, new Vector3(-3.4f, 0, 1.2f), 90f);
            Model("bench", parent, new Vector3(3.4f, 0, -4.4f), 90f);
            Model("signpost", parent, new Vector3(40f, 0, 3.2f), 180f);
            Model("signpost", parent, new Vector3(-3.4f, 0, -17.5f), 0f);
            Model("cart", parent, new Vector3(-4.6f, 0, -24f), 20f);
            Model("hay_bale", parent, new Vector3(-6.5f, 0, -21.5f), 70f);
            Model("hay_bale", parent, new Vector3(-6.2f, 0, -22.7f), 75f);
            Model("woodpile", parent, new Vector3(-11.2f, 0, 8.5f), 90f);
            Model("woodpile", parent, new Vector3(34.6f, 0, 9f), 0f);
            Model("barrel", parent, new Vector3(-27.9f, 0, 3.2f));
            Model("crate", parent, new Vector3(-28.6f, 0, 4.2f), 30f);
            Model("banner_red", parent, new Vector3(15.6f, 0, 33.2f));
            Model("banner_red", parent, new Vector3(20.4f, 0, 33.2f));
            Model("banner_blue", parent, new Vector3(-12.4f, 0, -10.2f));
            // A field of fences and hay outside the south wall, either side of the road.
            for (int i = 0; i < 5; i++)
            {
                Model("fence", parent, new Vector3(-3.8f, 0, -18.5f - i * 2f), 90f);
                Model("fence", parent, new Vector3(3.8f, 0, -18.5f - i * 2f), 90f);
            }
            Model("rowboat", parent, new Vector3(34.6f, -0.45f, 24.2f), 80f, 1f, false);
        }

        private static void DressRiver(Transform parent)
        {
            var rng = new System.Random(5);
            for (float x = -64f; x < 64f; x += 2.2f + (float)rng.NextDouble() * 3f)
            {
                if (Mathf.Abs(x) < 4.5f || (x > 25f && x < 35f)) continue;
                foreach (var z in new[] { 19.5f, 26.5f })
                {
                    float jitter = (float)rng.NextDouble() * 0.6f - 0.3f;
                    double roll = rng.NextDouble();
                    if (roll < 0.45) Model("rock_" + rng.Next(0, 2), parent, new Vector3(x, -0.25f, z + jitter), rng.Next(0, 360), 0.6f + (float)rng.NextDouble() * 0.5f);
                    else if (roll < 0.8) Model("reeds", parent, new Vector3(x, -0.3f, z + (z < 23 ? 0.6f : -0.6f)), rng.Next(0, 360));
                    else Model("grass_1", parent, new Vector3(x, 0, z + (z < 23 ? -0.8f : 0.8f)), rng.Next(0, 360));
                }
            }
        }

        private static void DressNature(Transform parent)
        {
            var rng = new System.Random(9);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);

            // The trees the grey box placed (their trunks are still the colliders), then more of the
            // woods, and a forest round the edge of the world so it never ends in a wall.
            int i = 0;
            foreach (var t in trees)
                Model(i++ % 3 == 0 ? "oak_" + rng.Next(0, 2) : "pine_" + rng.Next(0, 3), parent, new Vector3(t.x, 0, t.z), R(0, 360), R(0.9f, 1.25f));
            for (int k = 0; k < 26; k++)
            {
                var p = new Vector3(R(24, 49), 0, R(28, 49));
                if (NearPath(p, 2.5f) || Near(p, new[] { V(32, 33), V(39, 34), V(44, 44), V(34, 44), V(38, 39), V(47, 47) }, 2.2f)) continue;
                Model(rng.NextDouble() < 0.7 ? "pine_" + rng.Next(0, 3) : "oak_" + rng.Next(0, 2), parent, p, R(0, 360), R(0.8f, 1.3f));
            }
            for (int k = 0; k < 700; k++)
            {
                var p = new Vector3(R(-110, 110), 0, R(-80, 110));
                bool inside = p.x > -52 && p.x < 52 && p.z > -32 && p.z < 52;
                if (inside || (p.z > 18 && p.z < 28) || NearPath(p, 3f)) continue;
                float h = TerrainHeight(p.x, p.z);
                Model(rng.NextDouble() < 0.65 ? "pine_" + rng.Next(0, 3) : "oak_" + rng.Next(0, 2), parent, new Vector3(p.x, h - 0.2f, p.z), R(0, 360), R(0.9f, 1.6f));
            }
            // Inside the walls and on the north bank: bushes, flowers, grass and rocks, off the roads.
            for (int k = 0; k < 420; k++)
            {
                var p = new Vector3(R(-49, 49), 0, R(-29, 49));
                if ((p.z > 18.6f && p.z < 27.4f) || NearPath(p, 2.6f) || Blocked(p, 0.9f)) continue;
                double roll = rng.NextDouble();
                string model = roll < 0.12 ? "bush_" + rng.Next(0, 2) : roll < 0.3 ? "flowers_" + rng.Next(0, 2)
                    : roll < 0.9 ? "grass_" + rng.Next(0, 2) : roll < 0.96 ? "rock_0" : "stump";
                Model(model, parent, p, R(0, 360), R(0.8f, 1.2f));
            }
        }

        private static bool Near(Vector3 p, Vector2[] points, float distance) =>
            points.Any(q => Vector2.Distance(new Vector2(p.x, p.z), q) < distance);

        private static bool NearPath(Vector3 p, float distance)
        {
            var q = new Vector2(p.x, p.z);
            foreach (var line in Paths)
                for (int i = 0; i + 1 < line.Length; i++)
                {
                    var a = line[i];
                    var ab = line[i + 1] - a;
                    float t = Mathf.Clamp01(Vector2.Dot(q - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-4f));
                    if (Vector2.Distance(q, a + ab * t) < distance) return true;
                }
            return false;
        }

        // Anything solid nearby: a building, a wall, a tree trunk, a stall.
        private static bool Blocked(Vector3 p, float radius)
        {
            Physics.SyncTransforms();
            foreach (var c in Physics.OverlapSphere(p + Vector3.up * 0.6f, radius))
                if (!c.isTrigger) return true;
            return false;
        }

        // Matches the hills build_kit.py raises outside the play area.
        private static float TerrainHeight(float x, float z)
        {
            float outX = Mathf.Max(0, Mathf.Abs(x) - 50), outZ = Mathf.Max(0, Mathf.Max(-30 - z, z - 50));
            float d = Mathf.Sqrt(outX * outX + outZ * outZ);
            if (d <= 0) return 0;
            return Mathf.Min(26f, d * 0.34f) + Mathf.Sin(x * 0.11f) * Mathf.Cos(z * 0.09f) * Mathf.Min(4f, d * 0.12f);
        }

        // ---- Light, fire and smoke ---------------------------------------------------------------

        private static void Lamp(Transform parent, Vector3 position)
        {
            Model("lamp_post", parent, position);
            var light = PointLight(parent, position + new Vector3(0, 2.2f, 0.45f), new Color(1f, 0.78f, 0.45f), 1.6f, 7f);
            var flicker = light.gameObject.AddComponent<FlickerLight>();
            flicker.Amount = 0.08f;
            flicker.OnlyAtNight = true;
            lamps.Add(flicker);
        }

        private static Light PointLight(Transform parent, Vector3 position, Color color, float intensity, float range)
        {
            var light = new GameObject("light").AddComponent<Light>();
            light.transform.SetParent(parent, false);
            light.transform.position = position;
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            light.shadows = LightShadows.None;
            return light;
        }

        private static void Fire(Transform parent, Vector3 position, float size, float lightIntensity, float lightRange, bool nightOnly)
        {
            var go = new GameObject("fire");
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f * size, 1.4f * size);
            main.startSize = new ParticleSystem.MinMaxCurve(0.25f * size, 0.5f * size);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.55f, 0.15f), new Color(1f, 0.85f, 0.3f));
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 80;
            var emission = ps.emission;
            emission.rateOverTime = 26f * size;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 8f;
            shape.radius = 0.22f * size;
            shape.rotation = new Vector3(-90, 0, 0);
            var size_ = ps.sizeOverLifetime;
            size_.enabled = true;
            size_.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 1, 1, 0));
            var color = ps.colorOverLifetime;
            color.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(new Color(1f, 0.9f, 0.5f), 0), new GradientColorKey(new Color(1f, 0.35f, 0.08f), 1) },
                new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(0.9f, 0.6f), new GradientAlphaKey(0, 1) });
            color.color = g;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = FireMaterial();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            var light = PointLight(parent, position + Vector3.up * 0.6f, new Color(1f, 0.6f, 0.28f), lightIntensity, lightRange);
            var flicker = light.gameObject.AddComponent<FlickerLight>();
            flicker.Amount = 0.3f;
            flicker.Speed = 8f;
            flicker.OnlyAtNight = nightOnly;
            lamps.Add(flicker);
        }

        private static Material FireMaterial() => Asset(ArtFolder + "/VillageFire.mat", () =>
        {
            var m = new Material(particles);
            m.SetTexture("_BaseMap", SoftParticle());
            m.SetColor("_BaseColor", new Color(1f, 0.85f, 0.6f, 1f));
            return m;
        });

        private static Material SmokeMaterial() => Asset(ArtFolder + "/VillageSmoke.mat", () =>
        {
            var m = new Material(particles);
            m.SetTexture("_BaseMap", SoftParticle());
            m.SetColor("_BaseColor", new Color(0.88f, 0.88f, 0.9f, 0.55f));
            return m;
        });

        // A soft round dot for fire, smoke and fireflies, made once and kept as a PNG.
        private static Texture2D SoftParticle()
        {
            string path = ArtFolder + "/SoftParticle.png";
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing != null) return existing;
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(size / 2f, size / 2f)) / (size / 2f);
                    float a = Mathf.Clamp01(1f - d);
                    texture.SetPixel(x, y, new Color(1, 1, 1, a * a * (3 - 2 * a)));
                }
            System.IO.File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static void Smoke(Transform parent)
        {
            foreach (var (x, z, w, d, _, east) in Houses)
            {
                // The chimney sits as build_kit.py put it: on the east or west end, a little north.
                var top = new Vector3(x + (east ? 1 : -1) * (w / 2f - 0.8f), 4.75f, z + d / 8f);
                var go = new GameObject("chimney_smoke");
                go.transform.SetParent(parent, false);
                go.transform.position = top;
                var ps = go.AddComponent<ParticleSystem>();
                var main = ps.main;
                main.startLifetime = new ParticleSystem.MinMaxCurve(3f, 5f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
                main.startColor = new Color(0.9f, 0.9f, 0.92f, 0.45f);
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.maxParticles = 40;
                var emission = ps.emission;
                emission.rateOverTime = 3.5f;
                var shape = ps.shape;
                shape.shapeType = ParticleSystemShapeType.Cone;
                shape.angle = 6f;
                shape.radius = 0.15f;
                shape.rotation = new Vector3(-90, 0, 0);
                var velocity = ps.velocityOverLifetime;
                velocity.enabled = true;
                velocity.space = ParticleSystemSimulationSpace.World;
                velocity.x = new ParticleSystem.MinMaxCurve(0.3f, 0.6f);
                velocity.y = new ParticleSystem.MinMaxCurve(0f, 0f);
                velocity.z = new ParticleSystem.MinMaxCurve(0.1f, 0.2f);
                var sizeOver = ps.sizeOverLifetime;
                sizeOver.enabled = true;
                sizeOver.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 0.6f, 1, 2.6f));
                var color = ps.colorOverLifetime;
                color.enabled = true;
                var g = new Gradient();
                g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                    new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(0.5f, 0.2f), new GradientAlphaKey(0, 1) });
                color.color = g;
                var renderer = go.GetComponent<ParticleSystemRenderer>();
                renderer.sharedMaterial = SmokeMaterial();
                renderer.shadowCastingMode = ShadowCastingMode.Off;
            }
        }

        private static void Fireflies(Transform parent)
        {
            foreach (var centre in new[] { new Vector3(37, 1.2f, 39), new Vector3(-10, 1.2f, 31), new Vector3(12, 1f, 20.5f), new Vector3(-30, 1f, 21f) })
            {
                var go = new GameObject("fireflies");
                go.transform.SetParent(parent, false);
                go.transform.position = centre;
                var ps = go.AddComponent<ParticleSystem>();
                var main = ps.main;
                main.startLifetime = new ParticleSystem.MinMaxCurve(4f, 7f);
                main.startSpeed = 0.15f;
                main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.12f);
                main.startColor = new Color(0.75f, 1f, 0.55f, 1f);
                main.maxParticles = 60;
                var emission = ps.emission;
                emission.rateOverTime = 7f;
                emission.enabled = false;
                var shape = ps.shape;
                shape.shapeType = ParticleSystemShapeType.Box;
                shape.scale = new Vector3(14, 2, 10);
                var noise = ps.noise;
                noise.enabled = true;
                noise.strength = 0.5f;
                noise.frequency = 0.4f;
                var color = ps.colorOverLifetime;
                color.enabled = true;
                var g = new Gradient();
                g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                    new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, 0.3f), new GradientAlphaKey(0.2f, 0.6f), new GradientAlphaKey(1, 0.8f), new GradientAlphaKey(0, 1) });
                color.color = g;
                var renderer = go.GetComponent<ParticleSystemRenderer>();
                renderer.sharedMaterial = FireMaterial();
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                nightOnly.Add(ps);
            }
            // The gate's torches burn every night.
            foreach (var sx in new[] { -1f, 1f })
                Fire(parent, new Vector3(sx * (3.8f - 1.12f), 2.75f, -16.25f), 0.35f, 1.6f, 6f, true);
        }

        private static DayNightCycle Atmosphere(Camera camera, Light sun)
        {
            camera.clearFlags = CameraClearFlags.Skybox;
            var data = camera.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            camera.farClipPlane = 260f;

            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.85f;
            RenderSettings.sun = sun;
            RenderSettings.skybox = sky;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.0085f;

            var volume = new GameObject("PostProcessing").AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = Asset(ArtFolder + "/VillageVolume.asset", () =>
            {
                var profile = ScriptableObject.CreateInstance<VolumeProfile>();
                return profile;
            });
            var p = volume.sharedProfile;
            if (!p.Has<Tonemapping>())
            {
                p.Add<Tonemapping>(true).mode.Override(TonemappingMode.ACES);
                var bloom = p.Add<Bloom>(true);
                bloom.threshold.Override(1.0f);
                bloom.intensity.Override(0.55f);
                bloom.scatter.Override(0.65f);
                var colour = p.Add<ColorAdjustments>(true);
                colour.postExposure.Override(0.35f);
                colour.contrast.Override(14f);
                colour.saturation.Override(14f);
                var vignette = p.Add<Vignette>(true);
                vignette.intensity.Override(0.24f);
                vignette.smoothness.Override(0.45f);
                p.Add<WhiteBalance>(true).temperature.Override(6f);
                foreach (var component in p.components)
                    if (!AssetDatabase.IsSubAsset(component)) AssetDatabase.AddObjectToAsset(component, p);
                EditorUtility.SetDirty(p);
            }

            var cycle = new GameObject("DayNightCycle").AddComponent<DayNightCycle>();
            cycle.Sun = sun;
            cycle.Palette = palette;
            cycle.Sky = sky;
            cycle.Lamps = lamps.ToArray();
            cycle.NightOnly = nightOnly.ToArray();
            cycle.TimeOfDay = 0.36f;
            cycle.Apply();
            return cycle;
        }
    }
}

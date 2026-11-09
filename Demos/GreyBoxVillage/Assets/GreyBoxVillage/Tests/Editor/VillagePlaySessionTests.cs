using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using Demos.Shared;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace Demos.GreyBoxVillage.Tests
{
    /// <summary>
    /// What a play session must leave behind: nothing. Pressing Play must not change a saved asset,
    /// and a second session must start as clean as the first even when the editor skips the domain
    /// reload (Enter Play Mode Options), which keeps every static field as the last session left it.
    /// </summary>
    public sealed class VillagePlaySessionTests
    {
        private const string Root = "Assets/GreyBoxVillage";

        // Every asset a running village could write to: the materials the day and night cycle
        // drives, the post-processing profiles and the default one URP keeps, the agents' profiles
        // and provider config, and the UI's panel settings.
        private static readonly string[] Watched =
        {
            Root + "/Art/VillageSky.mat", Root + "/Art/VillagePalette.mat", Root + "/Art/VillageWater.mat",
            Root + "/Art/VillageVolume.asset", "Assets/Settings/DefaultVolumeProfile.asset",
            Root + "/Agents/Bram.asset", Root + "/Agents/Wren.asset", Root + "/Agents/Odile.asset",
            Root + "/Agents/Hale.asset", Root + "/Agents/VillageOllama.asset", Root + "/Resources/VillagePanelSettings.asset"
        };

        private FakeVillageConfig config;

        private static string Snapshot(string path)
        {
            var b = new StringBuilder();
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path).Where(o => o != null).OrderBy(o => o.name, StringComparer.Ordinal).ThenBy(o => o.GetType().Name))
                // Content only: Unity marks the panel settings dirty when a document attaches to
                // them, which changes nothing on disk.
                b.Append(o.GetType().Name).Append(' ').Append(EditorJsonUtility.ToJson(o)).Append('\n');
            return b.ToString();
        }

        [UnityTest]
        public IEnumerator PlayingThroughTheNightLeavesEverySavedAssetAsItWas()
        {
            EditorSceneManager.OpenScene(DemoLauncher.VillagePath);
            // Kept in SessionState, which outlives the domain reloads entering and leaving Play mode.
            foreach (var path in Watched) SessionState.SetString("gbv-test " + path, Snapshot(path));

            yield return new EnterPlayMode();
            yield return null;
            var commands = UnityEngine.Object.FindFirstObjectByType<VillageCommands>();
            config = ScriptableObject.CreateInstance<FakeVillageConfig>();
            config.Fake = new FakeVillageProvider(FakeVillageProvider.Decision("none", "no_target"));
            commands.UseProvider(config);
            var cycle = UnityEngine.Object.FindFirstObjectByType<DayNightCycle>();
            var sky = AssetDatabase.LoadAssetAtPath<Material>(Watched[0]);
            var palette = AssetDatabase.LoadAssetAtPath<Material>(Watched[1]);
            Assert.That(RenderSettings.skybox, Is.Not.SameAs(sky), "The scene's skybox must be the cycle's copy during Play.");
            Assert.That(cycle.PaletteInUse, Is.Not.SameAs(palette), "The window glow must go to the cycle's copy during Play.");
            foreach (var hour in new[] { 0.95f, 0.75f, 0.5f, 0.05f })
            {
                cycle.TimeOfDay = hour;
                for (int frame = 0; frame < 3; frame++) yield return null;
            }
            yield return new ExitPlayMode();

            foreach (var path in Watched)
                Assert.That(Snapshot(path), Is.EqualTo(SessionState.GetString("gbv-test " + path, "")), path + " changed during Play.");
        }

        [UnityTearDown]
        public IEnumerator LeavePlayMode()
        {
            if (EditorApplication.isPlaying) yield return new ExitPlayMode();
            foreach (var path in Watched) SessionState.EraseString("gbv-test " + path);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            if (config != null) UnityEngine.Object.DestroyImmediate(config);
        }

        [Test]
        public void TheDefaultVolumeProfileListsNoComponentWithAMissingScript()
        {
            // URP drops a listed component whose script is missing when it loads the profile, which
            // dirtied the asset in every checkout that pressed Play. It does so in memory, before any
            // test runs, so the file itself is read. (The profile also keeps a few unlisted test
            // components from URP's template, which URP leaves alone.)
            const string path = "Assets/Settings/DefaultVolumeProfile.asset";
            string text = File.ReadAllText(path).Replace("\r\n", "\n");
            var scripts = Regex.Matches(text, @"^--- !u!114 &(-?\d+)\n(?:(?!--- ).*\n)*?  m_Script: \{fileID: \d+, guid: ([0-9a-f]{32})", RegexOptions.Multiline)
                .Cast<Match>().ToDictionary(m => m.Groups[1].Value, m => m.Groups[2].Value);
            var list = Regex.Match(text, @"^  components:\n((?:  - \{fileID: -?\d+\}\n)+)", RegexOptions.Multiline);
            Assert.That(list.Success, Is.True, "No components list in " + path);
            var listed = Regex.Matches(list.Groups[1].Value, @"fileID: (-?\d+)").Cast<Match>().Select(m => m.Groups[1].Value).ToList();
            Assert.That(listed, Is.Not.Empty);
            var missing = listed.Where(id => !scripts.TryGetValue(id, out var guid) || string.IsNullOrEmpty(AssetDatabase.GUIDToAssetPath(guid))).ToList();
            Assert.That(missing, Is.Empty, path + " lists components whose scripts this project does not have.");
        }

        // ---- Static state ------------------------------------------------------------------------

        private static IEnumerable<Assembly> GameAssemblies => new[] { typeof(VillageCommands).Assembly, typeof(DayNightCycle).Assembly };

        private static bool IsSubsystemReset(MethodInfo m) =>
            m.GetCustomAttribute<RuntimeInitializeOnLoadMethodAttribute>()?.loadType == RuntimeInitializeLoadType.SubsystemRegistration;

        /// <summary>
        /// Every static field that can change at run time: not a constant, not readonly unless it
        /// holds a collection that can grow, and not a compiler cache (a lambda's, for instance).
        /// Field-like static events and static auto-properties are fields too.
        /// </summary>
        private static IEnumerable<FieldInfo> MutableStatics(Assembly assembly) =>
            StaticFields(assembly).Where(f => !f.IsLiteral && (!f.IsInitOnly || IsGrowable(f.FieldType)));

        private static IEnumerable<FieldInfo> StaticFields(Assembly assembly) =>
            assembly.GetTypes()
                .Where(t => t.GetCustomAttribute<CompilerGeneratedAttribute>() == null && !t.Name.StartsWith("<"))
                .SelectMany(t => t.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly));

        // Whether a value of this type can be changed in place: anything but a string, a primitive,
        // an enum, or a struct whose fields are all of those (a tuple of strings, a Color).
        private static bool IsMutable(Type type) =>
            !(type == typeof(string) || type.IsPrimitive || type.IsEnum ||
              type.IsValueType && type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).All(f => !IsMutable(f.FieldType)));

        [Test]
        public void NoStaticTableHoldsObjectsThatCanChange()
        {
            // A readonly array of objects with settable fields is static state all the same: the
            // catalog's items were exactly that before they became read-only.
            var tables = GameAssemblies.SelectMany(StaticFields)
                .Where(f => f.IsInitOnly && f.GetValue(null) is Array a && IsMutable(a.GetType().GetElementType()))
                .Select(f => f.DeclaringType.FullName + "." + f.Name).ToList();
            Assert.That(tables, Is.Empty, "Static arrays whose elements can be changed in place.");
            Assert.That(ItemCatalog.All, Is.Not.InstanceOf<Array>(), "The catalog must not be an array a caller can write to.");
            Assert.That(typeof(ItemCatalog.Item).GetFields().Where(f => !f.IsInitOnly).Select(f => f.Name), Is.Empty,
                "An item's fields must be readonly.");
        }

        [Test]
        public void TheScatterFollowsItsTransform()
        {
            var parent = new GameObject("parent");
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                var field = new GameObject("field").AddComponent<ScatterField>();
                field.transform.SetParent(parent.transform, false);
                field.Kinds = new[]
                {
                    new ScatterField.Kind
                    {
                        Model = "cube",
                        Parts = new[] { new ScatterField.Part { Mesh = cube.GetComponent<MeshFilter>().sharedMesh } },
                        Placements = new[] { new Vector4(0, 0, 0, 0) },
                        Scales = new[] { 1f }
                    }
                };
                Assert.That(field.Bounds.center.x, Is.EqualTo(0f).Within(0.01f));
                parent.transform.position = new Vector3(10, 0, 0);
                Assert.That(field.Bounds.center.x, Is.EqualTo(10f).Within(0.01f), "The copies stayed where the field was first drawn.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(parent);
                UnityEngine.Object.DestroyImmediate(cube);
            }
        }

        private static bool IsGrowable(Type type) =>
            !type.IsArray && type.GetInterfaces().Append(type).Any(i => i.IsGenericType &&
                (i.GetGenericTypeDefinition() == typeof(ICollection<>)) && !IsReadOnlyWrapper(type));

        private static bool IsReadOnlyWrapper(Type type) =>
            type.IsGenericType && type.GetGenericTypeDefinition() == typeof(System.Collections.ObjectModel.ReadOnlyCollection<>);

        [Test]
        public void EveryStaticFieldIsResetBeforeEachPlaySession()
        {
            var resets = GameAssemblies.SelectMany(a => a.GetTypes())
                .SelectMany(t => t.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                .Where(IsSubsystemReset).ToList();
            foreach (var m in resets)
                Assert.That(m.GetParameters(), Is.Empty, m.DeclaringType + "." + m.Name + " must take no parameters.");

            var problems = new List<string>();
            foreach (var field in GameAssemblies.SelectMany(MutableStatics))
            {
                string name = field.DeclaringType.FullName + "." + field.Name;
                object saved = field.GetValue(null);
                try
                {
                    if (!Dirty(field, out object dirty))
                    {
                        // Nothing to set it to here, so the most the test can ask is that its type resets something.
                        if (!resets.Any(r => r.DeclaringType == field.DeclaringType))
                            problems.Add(name + " is static state with no SubsystemRegistration reset in its type.");
                        continue;
                    }
                    foreach (var reset in resets) reset.Invoke(null, null);
                    object after = field.GetValue(null);
                    bool stillDirty = field.IsInitOnly ? Count(after) > 0 : Equals(after, dirty) || ReferenceEquals(after, dirty);
                    if (stillDirty) problems.Add(name + " keeps its value from the last play session: reset it in a [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] method.");
                }
                finally
                {
                    if (!field.IsInitOnly) field.SetValue(null, saved);
                }
            }
            Assert.That(problems, Is.Empty);
        }

        // Puts a value in the field that a reset must undo; false when the type has none to offer.
        private static bool Dirty(FieldInfo field, out object dirty)
        {
            var type = field.FieldType;
            object current = field.GetValue(null);
            dirty = null;
            if (field.IsInitOnly)
            {
                // A readonly collection: give it an element, which the reset must clear.
                var add = type.GetMethod("Add", type.IsGenericType ? type.GetGenericArguments() : Type.EmptyTypes);
                if (current == null || add == null || add.GetParameters().Length != 1) return false;
                var element = add.GetParameters()[0].ParameterType;
                add.Invoke(current, new[] { element.IsValueType ? Activator.CreateInstance(element) : element == typeof(string) ? "dirty" : null });
                dirty = current;
                return true;
            }
            if (type == typeof(bool)) dirty = !(bool)(current ?? false);
            else if (type == typeof(string)) dirty = (current as string ?? "") + "dirty";
            else if (type.IsEnum) dirty = Enum.ToObject(type, Convert.ToInt64(current) + 1);
            else if (type.IsPrimitive) dirty = Convert.ChangeType(Convert.ToDouble(current) + 1, type);
            else if (typeof(Delegate).IsAssignableFrom(type))
            {
                var invoke = type.GetMethod("Invoke");
                if (invoke.ReturnType != typeof(void)) return false;
                var ps = invoke.GetParameters().Select(p => Expression.Parameter(p.ParameterType)).ToArray();
                dirty = Expression.Lambda(type, Expression.Empty(), ps).Compile();
            }
            else if (!type.IsAbstract && !type.IsInterface && !typeof(UnityEngine.Object).IsAssignableFrom(type) && type.IsClass)
                dirty = RuntimeHelpers.GetUninitializedObject(type);
            else return false;
            field.SetValue(null, dirty);
            return true;
        }

        private static int Count(object collection) =>
            collection == null ? 0 : (int)collection.GetType().GetProperty("Count").GetValue(collection);

        // ---- The scene file --------------------------------------------------------------------

        [Test]
        public void TheSceneIsSmallBatchedAndInstanced()
        {
            string text = File.ReadAllText(DemoLauncher.VillagePath);
            int prefabs = Regex.Matches(text, @"^--- !u!1001 ", RegexOptions.Multiline).Count;
            int materialOverrides = Regex.Matches(text, @"propertyPath: m_Materials\.Array\.data").Count;
            // The water is the one model with a material of its own.
            Assert.That(materialOverrides, Is.LessThanOrEqualTo(1), "Placed models override the palette the import already gives them.");
            Assert.That(prefabs, Is.LessThan(250), "Small scattered models belong in a ScatterField, not one prefab each.");

            var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(DemoLauncher.VillagePath);
            bool opened = !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(DemoLauncher.VillagePath, OpenSceneMode.Additive);
            try
            {
                // The buildings and walls are static, so they are batched: the flags are prefab overrides,
                // which a scene builder can lose without a sound.
                var art = scene.GetRootGameObjects().Single(g => g.name == "VillageArt").transform;
                foreach (var group in new[] { "Buildings", "Walls" })
                {
                    var renderers = art.Find(group).GetComponentsInChildren<MeshRenderer>(true);
                    Assert.That(renderers, Is.Not.Empty, group);
                    var moving = renderers.Where(r => !GameObjectUtility.GetStaticEditorFlags(r.gameObject).HasFlag(StaticEditorFlags.BatchingStatic))
                        .Select(r => r.name).ToList();
                    Assert.That(moving, Is.Empty, group + " has renderers that are not batching static.");
                    // And they stand where they were placed: a builder that loses its overrides saves
                    // every model at the origin.
                    int places = renderers.Select(r => Vector3Int.RoundToInt(r.bounds.center)).Distinct().Count();
                    Assert.That(places, Is.GreaterThan(renderers.Length * 0.8), group + " models share their positions.");
                }
                var gate = art.Find("Buildings").Cast<Transform>().Single(t => t.name == "gate_house");
                var place = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).First(t => t.name == "village_gate");
                Assert.That(Vector3.Distance(gate.position, place.position), Is.LessThan(1f), "The gate house is not at the village gate.");

                var fields = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<ScatterField>(true)).ToList();
                Assert.That(fields.Sum(f => f.Count), Is.GreaterThan(500));
                foreach (var f in fields)
                {
                    Assert.That(f.Material, Is.Not.Null.And.Property("enableInstancing").True, f.name);
                    Assert.That(f.Kinds.SelectMany(k => k.Parts).All(p => p.Mesh != null), Is.True, f.name + " has a part with no mesh.");
                }
            }
            finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
            Assert.That(new FileInfo(DemoLauncher.VillagePath).Length, Is.LessThan(1_500_000), "The scene file is meant to stay small.");
        }
    }
}

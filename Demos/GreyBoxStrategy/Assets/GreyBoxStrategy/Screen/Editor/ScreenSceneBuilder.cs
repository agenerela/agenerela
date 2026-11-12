using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace GreyBoxStrategy.Realm.Editor
{
    /// <summary>
    /// Builds the watch-mode scene from the assets in <c>Screen/</c>, so the scene file is never
    /// edited by hand. From the Unity CLI:
    /// <c>unity command script eval -- --code 'return GreyBoxStrategy.Realm.Editor.ScreenSceneBuilder.Build();'</c>
    /// </summary>
    public static class ScreenSceneBuilder
    {
        public const string ScenePath = "Assets/GreyBoxStrategy/Screen/Watch.unity";
        private const string Root = "Assets/GreyBoxStrategy/Screen/";

        [MenuItem("GreyBoxStrategy/Build the watch-mode scene")]
        public static string Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 5;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color32(29, 33, 32, 255);
            camGo.transform.position = new Vector3(0, 0, -10);

            var mapGo = new GameObject("Map");
            var map = mapGo.AddComponent<MapView>();
            var so = new SerializedObject(map);
            so.FindProperty("shader").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Shader>(Root + "Map/PaintedMap.shader");
            so.FindProperty("painting").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "Art/sea-chart-map.jpg");
            so.FindProperty("provinceIds").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "Art/province-ids.png");
            so.FindProperty("provinceEdge").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "Art/province-edge.png");
            so.FindProperty("provincePoints").objectReferenceValue = AssetDatabase.LoadAssetAtPath<TextAsset>(Root + "Art/provinces.json");
            so.FindProperty("seaShader").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Shader>(Root + "Map/SeaBackdrop.shader");
            so.FindProperty("seaTile").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "Art/sea-tile.jpg");
            so.ApplyModifiedPropertiesWithoutUndo();

            var director = new GameObject("Watch").AddComponent<ReplayDirector>();
            so = new SerializedObject(director);
            so.FindProperty("recording").objectReferenceValue = AssetDatabase.LoadAssetAtPath<TextAsset>(Root + "Replays/seed-0004-personas-4b.json");
            so.FindProperty("map").objectReferenceValue = map;
            so.FindProperty("style").objectReferenceValue = AssetDatabase.LoadAssetAtPath<StyleSheet>(Root + "UI/PaintedRealm.uss");
            so.FindProperty("theme").objectReferenceValue = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(Root + "UI/PaintedRealm.tss");
            so.FindProperty("portraits").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "Art/persona-portraits.jpg");
            // A live game (five model seats on a server), off by default: the screen plays the recording.
            so.FindProperty("live").objectReferenceValue = director.gameObject.AddComponent<LiveGame>();
            so.ApplyModifiedPropertiesWithoutUndo();

            foreach (var name in new[] { "shader", "painting", "provinceIds", "provinceEdge", "provincePoints", "seaShader", "seaTile" })
                if (new SerializedObject(map).FindProperty(name).objectReferenceValue == null) return "missing on the map: " + name;
            foreach (var name in new[] { "recording", "style", "theme", "portraits" })
                if (new SerializedObject(director).FindProperty(name).objectReferenceValue == null) return "missing on the director: " + name;

            EditorSceneManager.SaveScene(scene, ScenePath);
            return "saved " + ScenePath;
        }
    }
}

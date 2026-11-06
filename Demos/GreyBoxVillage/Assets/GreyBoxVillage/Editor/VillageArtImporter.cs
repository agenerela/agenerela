using UnityEditor;
using UnityEngine;

namespace Demos.GreyBoxVillage.Editor
{
    /// <summary>
    /// Import settings for the Blender kit (<c>ArtSource/build_kit.py</c>): models come in at
    /// metre scale with the palette material already on every renderer (the water gets its own), so a
    /// model placed in the scene overrides nothing, and the palette textures keep their exact swatch
    /// colours.
    /// </summary>
    /// <remarks>
    /// The FBX files carry no material, so none is imported; the palette is assigned after the import,
    /// which depends on the material's artifact, so the material is imported first.
    /// </remarks>
    internal sealed class VillageArtImporter : AssetPostprocessor
    {
        private const string ArtFolder = "Assets/GreyBoxVillage/Art/";
        public const string PalettePath = ArtFolder + "VillagePalette.mat";
        public const string WaterPath = ArtFolder + "VillageWater.mat";

        // Bumped when what the import does changes, so every model is imported again.
        public override uint GetVersion() => 2;

        private void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(ArtFolder + "Models/")) return;
            var model = (ModelImporter)assetImporter;
            model.globalScale = 1f;
            model.useFileScale = true;
            model.materialImportMode = ModelImporterMaterialImportMode.None;
            model.importAnimation = false;
            model.animationType = ModelImporterAnimationType.None;
            model.importCameras = false;
            model.importLights = false;
            model.importBlendShapes = false;
            model.importVisibility = false;
            model.addCollider = false;
            model.isReadable = false;
            model.importNormals = ModelImporterNormals.Import;
            model.meshCompression = ModelImporterMeshCompression.Off;
        }

        private void OnPostprocessModel(GameObject root)
        {
            if (!assetPath.StartsWith(ArtFolder + "Models/")) return;
            string path = System.IO.Path.GetFileNameWithoutExtension(assetPath) == "water" ? WaterPath : PalettePath;
            context.DependsOnArtifact(path);
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) return;
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                var shared = r.sharedMaterials;
                for (int i = 0; i < shared.Length; i++) shared[i] = material;
                r.sharedMaterials = shared.Length > 0 ? shared : new[] { material };
            }
        }

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(ArtFolder + "Palette")) return;
            var texture = (TextureImporter)assetImporter;
            texture.textureType = TextureImporterType.Default;
            texture.sRGBTexture = true;
            texture.mipmapEnabled = true;
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.textureCompression = TextureImporterCompression.Uncompressed;
        }
    }
}

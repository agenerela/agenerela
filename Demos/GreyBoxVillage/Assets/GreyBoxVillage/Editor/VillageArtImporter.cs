using UnityEditor;
using UnityEngine;

namespace Demos.GreyBoxVillage.Editor
{
    /// <summary>
    /// Import settings for the Blender kit (<c>ArtSource/build_kit.py</c>): models come in at
    /// metre scale with no materials of their own, because every model shares the palette material
    /// the scene builder assigns, and the palette textures keep their exact swatch colours.
    /// </summary>
    internal sealed class VillageArtImporter : AssetPostprocessor
    {
        private const string ArtFolder = "Assets/GreyBoxVillage/Art/";

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

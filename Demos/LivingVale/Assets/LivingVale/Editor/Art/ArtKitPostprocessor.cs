using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace LivingVale.Editor
{
    /// <summary>Applies the art kit's import settings and shared palette material.</summary>
    public sealed class ArtKitPostprocessor : AssetPostprocessor
    {
        /// <summary>Checks exact folder boundaries using Unity's forward-slash asset paths.</summary>
        public static bool Handles(string assetPath)
        {
            return assetPath != null &&
                (assetPath.StartsWith(ArtKit.ModelFolder + "/", StringComparison.Ordinal) ||
                    assetPath.StartsWith(ArtKit.TextureFolder + "/", StringComparison.Ordinal));
        }

        private void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(ArtKit.ModelFolder + "/", StringComparison.Ordinal))
            {
                return;
            }

            var importer = (ModelImporter)assetImporter;
            importer.globalScale = 1f;
            importer.useFileScale = true;
            importer.bakeAxisConversion = true;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importVisibility = false;
            importer.importBlendShapes = false;
            importer.importAnimation = false;
            importer.animationType = ModelImporterAnimationType.None;
            importer.importConstraints = false;
            importer.importNormals = ModelImporterNormals.Import;
            importer.importTangents = ModelImporterTangents.CalculateMikk;
            importer.isReadable = false;
            importer.meshCompression = ModelImporterMeshCompression.Off;
            importer.generateSecondaryUV = false;
            importer.addCollider = ArtKit.TryGetEntry(Path.GetFileNameWithoutExtension(assetPath), out ArtKitEntry entry)
                && entry.Collidable;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            Material material = ArtKit.PaletteMaterial;
            if (material != null)
            {
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "LV_Palette"), material);
            }
        }

        private void OnPostprocessModel(GameObject root)
        {
            if (!assetPath.StartsWith(ArtKit.ModelFolder + "/", StringComparison.Ordinal))
            {
                return;
            }

            context.DependsOnSourceAsset(ArtKit.PaletteMaterialPath);
            Material material = ArtKit.PaletteMaterial;
            if (material == null)
            {
                return;
            }

            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                Material[] materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    materials[i] = material;
                }

                renderer.sharedMaterials = materials;
            }
        }

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(ArtKit.TextureFolder + "/", StringComparison.Ordinal) ||
                !string.Equals(assetPath, ArtKit.PalettePath, StringComparison.Ordinal))
            {
                return;
            }

            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.alphaSource = TextureImporterAlphaSource.None;
            importer.isReadable = false;
        }
    }
}

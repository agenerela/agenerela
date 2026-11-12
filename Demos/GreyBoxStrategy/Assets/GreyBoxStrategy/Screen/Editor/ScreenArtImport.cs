using UnityEditor;
using UnityEngine;

namespace GreyBoxStrategy.Realm.Editor
{
    /// <summary>
    /// Import settings for the screen's art, set by name so a re-baked map imports right:
    /// the province maps are data (linear, uncompressed, no mipmaps; ids point-sampled and
    /// readable), the painting and the portraits are pictures.
    /// </summary>
    public sealed class ScreenArtImport : AssetPostprocessor
    {
        private const string Folder = "Assets/GreyBoxStrategy/Screen/Art/";

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Folder)) return;
            var t = (TextureImporter)assetImporter;
            t.textureType = TextureImporterType.Default;
            t.npotScale = TextureImporterNPOTScale.None;
            t.wrapMode = TextureWrapMode.Clamp;
            t.mipmapEnabled = false;
            t.textureCompression = TextureImporterCompression.Uncompressed;
            t.maxTextureSize = 4096;
            string name = System.IO.Path.GetFileNameWithoutExtension(assetPath);
            if (name == "province-ids" || name == "province-edge")
            {
                t.sRGBTexture = false;
                t.alphaSource = TextureImporterAlphaSource.None;
                t.filterMode = name == "province-ids" ? FilterMode.Point : FilterMode.Bilinear;
                t.isReadable = name == "province-ids";
                var s = t.GetDefaultPlatformTextureSettings();
                s.format = TextureImporterFormat.R8;
                t.SetPlatformTextureSettings(s);
            }
            else
            {
                t.sRGBTexture = true;
                t.filterMode = FilterMode.Bilinear;
                t.mipmapEnabled = name == "sea-chart-map";
            }
        }
    }
}

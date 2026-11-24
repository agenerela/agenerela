using Agenerela.Providers;
using UnityEngine;

namespace Agenerela
{
    /// <summary>Turns an <see cref="ImageResult"/>'s PNG into a <see cref="Texture2D"/> (DR-024): the one place images meet Unity's asset types.</summary>
    public static class ImageTextures
    {
        /// <summary>
        /// A new texture from <paramref name="result"/>'s PNG, or null for a failed result or bytes
        /// Unity cannot decode. Call it on the main thread; the caller owns the texture and destroys it.
        /// </summary>
        /// <param name="mipmaps">True for a picture drawn much smaller than it was made, such as a portrait in a 60 px frame.</param>
        public static Texture2D ToTexture(ImageResult result, bool mipmaps = true, string name = null)
        {
            return result != null && result.Succeeded ? ToTexture(result.Png, mipmaps, name) : null;
        }

        /// <summary>A new texture from PNG bytes, or null when they are not a picture Unity can decode.</summary>
        public static Texture2D ToTexture(byte[] png, bool mipmaps = true, string name = null)
        {
            if (!ImageResult.IsPng(png))
            {
                return null;
            }

            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, mipmaps)
            {
                name = name ?? "Generated image",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 2,
            };

            if (!texture.LoadImage(png, markNonReadable: true))
            {
                Object.DestroyImmediate(texture);
                return null;
            }

            return texture;
        }
    }
}

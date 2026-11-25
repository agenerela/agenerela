using System;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace Agenerela.Providers.SdCpp
{
    /// <summary>
    /// A picture as sd.cpp takes and gives it: 8-bit RGB, rows top first, channels interleaved
    /// (a U8 tensor shaped <c>[3, width, height]</c>).
    /// </summary>
    internal sealed class RgbImage
    {
        internal RgbImage(int width, int height, byte[] rgb)
        {
            if (width < 1 || height < 1 || rgb == null || rgb.Length != width * height * 3)
            {
                throw new ArgumentException($"An RGB image of {width}x{height} needs {width * height * 3} bytes.");
            }

            Width = width;
            Height = height;
            Rgb = rgb;
        }

        internal int Width { get; }

        internal int Height { get; }

        internal byte[] Rgb { get; }
    }

    /// <summary>Turns PNG bytes into sd.cpp's pixels and back, and scales them to the request's size.</summary>
    internal static class SdCppPixels
    {
        /// <summary>
        /// A PNG's pixels, top row first, scaled to <paramref name="width"/> by <paramref name="height"/>,
        /// with its alpha channel in <paramref name="alpha"/>. Main thread only (it uses a Texture2D).
        /// </summary>
        internal static RgbImage Decode(byte[] png, int width, int height, out byte[] alpha)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!texture.LoadImage(png, false))
                {
                    throw new ArgumentException("A picture could not be decoded as a PNG.");
                }

                Color32[] pixels = texture.GetPixels32(); // bottom row first
                int w = texture.width, h = texture.height;
                var rgb = new byte[w * h * 3];
                var a = new byte[w * h];
                for (int y = 0; y < h; y++)
                {
                    int from = (h - 1 - y) * w;
                    for (int x = 0; x < w; x++)
                    {
                        Color32 c = pixels[from + x];
                        int i = y * w + x;
                        rgb[i * 3] = c.r;
                        rgb[i * 3 + 1] = c.g;
                        rgb[i * 3 + 2] = c.b;
                        a[i] = c.a;
                    }
                }

                var image = new RgbImage(w, h, rgb);
                if (w == width && h == height)
                {
                    alpha = a;
                    return image;
                }

                alpha = ScaleNearest(a, w, h, 1, width, height);
                return new RgbImage(width, height, ScaleBilinear(rgb, w, h, width, height));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        /// <summary>
        /// sd.cpp's mask from the framework's (DR-028, Q2): fully transparent pixels of the PNG are
        /// the ones to change, and sd.cpp wants those white (255) in a one-channel mask.
        /// </summary>
        internal static byte[] MaskFromAlpha(byte[] alpha)
        {
            var mask = new byte[alpha.Length];
            for (int i = 0; i < alpha.Length; i++)
            {
                mask[i] = alpha[i] == 0 ? (byte)255 : (byte)0;
            }

            return mask;
        }

        /// <summary>A PNG of <paramref name="image"/>. Safe off the main thread (ImageConversion.EncodeArrayToPNG).</summary>
        internal static byte[] EncodePng(RgbImage image)
        {
            // EncodeArrayToPNG takes rows bottom first, as a texture holds them.
            int w = image.Width, h = image.Height, stride = w * 3;
            var flipped = new byte[image.Rgb.Length];
            for (int y = 0; y < h; y++)
            {
                Buffer.BlockCopy(image.Rgb, y * stride, flipped, (h - 1 - y) * stride, stride);
            }

            return ImageConversion.EncodeArrayToPNG(flipped, GraphicsFormat.R8G8B8_SRGB, (uint)w, (uint)h);
        }

        internal static byte[] ScaleBilinear(byte[] rgb, int w, int h, int width, int height)
        {
            var output = new byte[width * height * 3];
            for (int y = 0; y < height; y++)
            {
                float sy = Math.Max(0f, (y + 0.5f) * h / height - 0.5f);
                int y0 = Math.Min((int)sy, h - 1), y1 = Math.Min(y0 + 1, h - 1);
                float fy = sy - y0;
                for (int x = 0; x < width; x++)
                {
                    float sx = Math.Max(0f, (x + 0.5f) * w / width - 0.5f);
                    int x0 = Math.Min((int)sx, w - 1), x1 = Math.Min(x0 + 1, w - 1);
                    float fx = sx - x0;
                    for (int c = 0; c < 3; c++)
                    {
                        float top = rgb[(y0 * w + x0) * 3 + c] * (1 - fx) + rgb[(y0 * w + x1) * 3 + c] * fx;
                        float bottom = rgb[(y1 * w + x0) * 3 + c] * (1 - fx) + rgb[(y1 * w + x1) * 3 + c] * fx;
                        output[(y * width + x) * 3 + c] = (byte)Math.Round(top * (1 - fy) + bottom * fy);
                    }
                }
            }

            return output;
        }

        internal static byte[] ScaleNearest(byte[] values, int w, int h, int channels, int width, int height)
        {
            var output = new byte[width * height * channels];
            for (int y = 0; y < height; y++)
            {
                int sy = Math.Min(h - 1, y * h / height);
                for (int x = 0; x < width; x++)
                {
                    int sx = Math.Min(w - 1, x * w / width);
                    Buffer.BlockCopy(values, (sy * w + sx) * channels, output, (y * width + x) * channels, channels);
                }
            }

            return output;
        }
    }
}

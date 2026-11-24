using System;

namespace Agenerela.Providers
{
    /// <summary>
    /// What an <see cref="IImageProvider"/> made: a PNG and how long it took, or why it made none
    /// (DR-025). Never null from a provider.
    /// </summary>
    public sealed class ImageResult
    {
        private static readonly byte[] PngSignature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

        private ImageResult()
        {
        }

        /// <summary>True when <see cref="Png"/> holds a picture.</summary>
        public bool Succeeded => Error == null;

        /// <summary>The picture as PNG bytes, or null on a failure.</summary>
        public byte[] Png { get; private set; }

        /// <summary>The seed the backend used, when it reports one; null otherwise.</summary>
        public long? Seed { get; private set; }

        /// <summary>Seconds from the call to the bytes in hand, waiting for a busy backend included. 0 for a cache hit.</summary>
        public float Seconds { get; private set; }

        /// <summary>Seconds the backend itself spent making it, when it reports that; null otherwise.</summary>
        public float? GenerateSeconds { get; private set; }

        /// <summary>The <see cref="IImageProvider.Name"/> that made it, or that failed.</summary>
        public string ProviderName { get; private set; } = "";

        /// <summary>True when it came from an <see cref="IImageCache"/> and no backend was asked.</summary>
        public bool FromCache { get; private set; }

        /// <summary>Why no picture was made, or null when one was.</summary>
        public ProviderErrorKind? Error { get; private set; }

        /// <summary>What failed and what to do, for a log. Never contains a key (hard rule 1). Empty on success.</summary>
        public string ErrorMessage { get; private set; } = "";

        /// <summary>A picture made by <paramref name="providerName"/>.</summary>
        /// <exception cref="ArgumentException"><paramref name="png"/> is null, empty, or not a PNG.</exception>
        public static ImageResult Made(byte[] png, string providerName, float seconds, long? seed = null, float? generateSeconds = null)
        {
            if (!IsPng(png))
            {
                throw new ArgumentException("The bytes are not a PNG: they do not start with the PNG signature.", nameof(png));
            }

            return new ImageResult
            {
                Png = png,
                ProviderName = providerName ?? "",
                Seconds = Math.Max(0f, seconds),
                Seed = seed,
                GenerateSeconds = generateSeconds,
            };
        }

        /// <summary>No picture, for the reason <paramref name="kind"/> names.</summary>
        public static ImageResult Failed(ProviderErrorKind kind, string providerName, string message, float seconds = 0f)
        {
            return new ImageResult
            {
                Error = kind,
                ProviderName = providerName ?? "",
                ErrorMessage = message ?? "",
                Seconds = Math.Max(0f, seconds),
            };
        }

        /// <summary>A picture an <see cref="IImageCache"/> held, made earlier by <paramref name="providerName"/>.</summary>
        public static ImageResult Cached(byte[] png, string providerName)
        {
            var result = Made(png, providerName, 0f);
            result.FromCache = true;
            return result;
        }

        /// <summary>True when <paramref name="bytes"/> start with the PNG signature.</summary>
        public static bool IsPng(byte[] bytes)
        {
            if (bytes == null || bytes.Length < PngSignature.Length)
            {
                return false;
            }

            for (int i = 0; i < PngSignature.Length; i++)
            {
                if (bytes[i] != PngSignature[i])
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>"made by comfyui in 14.2 s" or "failed (Timeout): ...", for a log line.</summary>
        public override string ToString()
        {
            if (!Succeeded)
            {
                return $"failed ({Error}): {ErrorMessage}";
            }

            return FromCache ? $"from the cache ({ProviderName})" : $"made by {ProviderName} in {Seconds:0.0} s";
        }
    }
}

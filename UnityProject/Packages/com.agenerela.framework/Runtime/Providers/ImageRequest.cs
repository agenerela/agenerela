using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Agenerela.Providers
{
    /// <summary>
    /// One picture to make: a prompt, a size, an optional seed and optional backend parameters
    /// (DR-025). Plain data, so a request can be built, compared and keyed with no backend.
    /// </summary>
    public sealed class ImageRequest
    {
        /// <summary>What to draw. <see cref="ImagePromptBuilder"/> composes one with the game's style first.</summary>
        public string Prompt = "";

        /// <summary>Pixels across. The backend's <see cref="ImageCapabilities"/> says what it takes.</summary>
        public int Width = 1024;

        /// <summary>Pixels down.</summary>
        public int Height = 1024;

        /// <summary>
        /// The seed, or null to let the backend choose one. Set it for a picture that a second run,
        /// or a replay, should make again; <see cref="ImageResult.Seed"/> reports the one used.
        /// </summary>
        public long? Seed;

        /// <summary>
        /// Backend settings beyond the four above, as open name and value strings (DR-008: no enum of
        /// what a backend may take). <c>ComfyUIProvider</c> reads a workflow
        /// parameter's name, or <c>node.input</c> such as <c>28.unet_name</c>. A backend ignores a name
        /// it does not know, or fails the request if it cannot, and says which.
        /// </summary>
        public Dictionary<string, string> Parameters = new Dictionary<string, string>(StringComparer.Ordinal);

        /// <summary>
        /// A key for this request on <paramref name="providerName"/>: a SHA-256 over the provider's
        /// name, the prompt, the size, the seed and every parameter in name order, as 64 hex
        /// characters. The same request on the same backend has the same key on every machine and
        /// run; anything that could change the picture changes it.
        /// </summary>
        /// <remarks>A request with no seed is keyed as such, so caching it pins the first picture made.</remarks>
        public string Key(string providerName)
        {
            var text = new StringBuilder();
            void Field(string name, string value)
            {
                // Length-prefixed, so no value can run into the next field.
                value = value ?? "";
                text.Append(name).Append(':').Append(value.Length.ToString(CultureInfo.InvariantCulture)).Append(':').Append(value).Append('\n');
            }

            Field("provider", providerName);
            Field("prompt", Prompt);
            Field("width", Width.ToString(CultureInfo.InvariantCulture));
            Field("height", Height.ToString(CultureInfo.InvariantCulture));
            Field("seed", Seed.HasValue ? Seed.Value.ToString(CultureInfo.InvariantCulture) : "none");
            foreach (var pair in (Parameters ?? new Dictionary<string, string>()).OrderBy(p => p.Key, StringComparer.Ordinal))
            {
                Field("param " + pair.Key, pair.Value);
            }

            using (var sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(new UTF8Encoding(false).GetBytes(text.ToString()));
                var hex = new StringBuilder(hash.Length * 2);
                foreach (byte b in hash)
                {
                    hex.Append(b.ToString("x2", CultureInfo.InvariantCulture));
                }

                return hex.ToString();
            }
        }
    }
}

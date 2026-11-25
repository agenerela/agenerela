using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Agenerela.Providers
{
    /// <summary>
    /// One picture to make, or to change: a prompt, a size, an optional seed, optional backend
    /// parameters (DR-025) and, for an edit, the pictures to start from and an optional mask
    /// (DR-028). Plain data, so a request can be built, compared and keyed with no backend.
    /// </summary>
    /// <remarks>
    /// <b>An edit</b> is a request with <see cref="Images"/>: <see cref="Prompt"/> is then the
    /// instruction ("give him a crown"), <c>Images[0]</c> the picture to change and any later ones
    /// references, and <see cref="Mask"/> limits the change to part of it. A backend that cannot edit
    /// says so before sending (<see cref="ImageCapabilities.WhyNot"/>). How strongly the picture
    /// changes is the backend's open parameter, by convention named <c>strength</c>.
    /// </remarks>
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
        /// For an edit, the pictures to start from as PNG bytes: the first is the one changed, any
        /// later ones are references (an edit model may take several). Empty for a new picture.
        /// <see cref="Width"/> and <see cref="Height"/> stay the size of the result.
        /// </summary>
        public List<byte[]> Images = new List<byte[]>();

        /// <summary>
        /// For an edit, which part of <c>Images[0]</c> may change, or null for all of it: a PNG of the
        /// same size whose fully transparent pixels are the ones to change and whose opaque pixels are
        /// kept, as OpenAI's images API takes it (DR-028).
        /// </summary>
        public byte[] Mask;

        /// <summary>True when the request changes a picture: it has at least one of <see cref="Images"/>.</summary>
        public bool IsEdit => Images != null && Images.Count > 0;

        /// <summary>
        /// A key for this request on <paramref name="providerName"/>: a SHA-256 over the provider's
        /// name, the prompt, the size, the seed, every parameter in name order and, for an edit, a
        /// hash of each input picture in order and of the mask, as 64 hex characters. The same request on the same backend has the same key on every machine and
        /// run; anything that could change the picture changes it.
        /// </summary>
        /// <remarks>
        /// A request with no seed is keyed as such, so caching it pins the first picture made. A
        /// request with no input pictures and no mask has the key it had before edits existed, so a
        /// cache made then still serves it.
        /// </remarks>
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

            // Added only when present, so a plain request keys exactly as before (DR-028).
            var images = Images ?? new List<byte[]>();
            for (int i = 0; i < images.Count; i++)
            {
                Field("image " + i.ToString(CultureInfo.InvariantCulture), images[i] == null ? "none" : Hex(images[i]));
            }

            if (Mask != null)
            {
                Field("mask", Hex(Mask));
            }

            return Hex(new UTF8Encoding(false).GetBytes(text.ToString()));
        }

        // A SHA-256 as 64 lower-case hex characters.
        private static string Hex(byte[] bytes)
        {
            using (var sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(bytes);
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

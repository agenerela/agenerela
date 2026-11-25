using System;
using System.Collections.Generic;
using System.IO;
using Agenerela;
using Agenerela.Providers;
using Agenerela.Providers.ComfyUI;
using UnityEngine;

namespace GreyBoxStrategy.Realm
{
    /// <summary>
    /// Where Painted Realm's generated portraits and flags come from (DR-025): the team's ComfyUI server through the
    /// framework's <see cref="ComfyUIProvider"/>, a cache on disk per world seed, and the textures the screen shows.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The server is <c>COMFY_URL</c> and the key <c>COMFY_API_TOKEN</c> from the environment Unity started in. In the
    /// Editor only, when they are not set, the two lines are read from the main checkout's <c>tools/comfyui/.env</c>,
    /// as <c>comfy.py</c> reads them, so the lead's machine needs nothing more. Neither is ever logged or shown.
    /// </para>
    /// <para>
    /// The workflow is <c>Resources/ComfyWorkflows/image.json</c>, a copy of <c>tools/comfyui/workflows/image.json</c>. The
    /// home server has the int8 model, not the NVFP4 one the workflow names, so every request names
    /// <see cref="UnetFile"/> (<c>PAINTED_REALM_COMFY_UNET</c> overrides it).
    /// </para>
    /// </remarks>
    public static class RulerArtServer
    {
        public const string UrlVariable = ComfyUISettings.DefaultUrlVariable;
        public const string KeyVariable = ComfyUISettings.DefaultKeyVariable;
        public const string UnetVariable = "PAINTED_REALM_COMFY_UNET";

        /// <summary>The diffusion model's file on the home server (tools/comfyui/AGENT_USAGE.md, "On the home server").</summary>
        public static string UnetFile => Environment.GetEnvironmentVariable(UnetVariable) is string u && u.Trim().Length > 0 ? u.Trim() : "z_image_turbo_int8_convrot.safetensors";

        /// <summary>The longest the whole court may take to paint before the rest fall back to the art pack.</summary>
        public const float BudgetSeconds = 300f;

        /// <summary>The textures the screen shows this game, by realm id; empty when nothing was generated.</summary>
        public static readonly Dictionary<string, Texture2D> Portraits = new Dictionary<string, Texture2D>(StringComparer.Ordinal);
        public static readonly Dictionary<string, Texture2D> Flags = new Dictionary<string, Texture2D>(StringComparer.Ordinal);

        /// <summary>For the Unity CLI and the log: what the last world's painting did.</summary>
        public static string LastReport { get; private set; } = "no world painted yet";

        /// <summary>The folder a world's pictures are kept in: <c>&lt;persistentDataPath&gt;/painted-realm-art/&lt;seed&gt;</c>.</summary>
        public static string CacheFolder(int worldSeed) => Path.Combine(Application.persistentDataPath, "painted-realm-art", worldSeed.ToString(System.Globalization.CultureInfo.InvariantCulture));

        /// <summary>
        /// The ComfyUI provider, or null with the reason in <paramref name="why"/> when no server is set or the workflow
        /// is missing.
        /// </summary>
        public static ComfyUIProvider Provider(out string why)
        {
            why = null;
            string url = Environment.GetEnvironmentVariable(UrlVariable), key = Environment.GetEnvironmentVariable(KeyVariable);
#if UNITY_EDITOR
            if (string.IsNullOrWhiteSpace(url)) ReadDotEnv(ref url, ref key);
#endif
            if (string.IsNullOrWhiteSpace(url)) { why = $"{UrlVariable} is not set"; return null; }
            var workflow = Resources.Load<TextAsset>("ComfyWorkflows/image");
            if (workflow == null) { why = "the image workflow is missing from Resources/ComfyWorkflows"; return null; }
            try
            {
                var settings = new ComfyUISettings
                {
                    Url = url.Trim(),
                    ApiKey = (key ?? "").Trim(),
                    WorkflowJson = workflow.text,
                    TimeoutSeconds = 150f,
                    PollSeconds = 1f,
                    FreeAfterEach = false, // one batch, freed once at the end
                };
                settings.DefaultParameters["28.unet_name"] = UnetFile;
                settings.DefaultParameters["filename_prefix"] = "agenerela/painted-realm";
                return new ComfyUIProvider(settings);
            }
            catch (ArgumentException e)
            {
                why = e.Message;
                return null;
            }
        }

        /// <summary>Turns a world's pictures into the textures the screen shows, dropping the last world's.</summary>
        public static void Use(RulerArtSet set)
        {
            Clear();
            if (set == null) return;
            foreach (var kv in set.Portraits)
                if (ImageTextures.ToTexture(kv.Value, true, "portrait " + kv.Key) is Texture2D t) Portraits[kv.Key] = t;
            foreach (var kv in set.Flags)
                if (ImageTextures.ToTexture(kv.Value, true, "flag " + kv.Key) is Texture2D t) Flags[kv.Key] = t;
            LastReport = set.ToString();
        }

        public static void Clear()
        {
            foreach (var t in Portraits.Values) if (t != null) UnityEngine.Object.Destroy(t);
            foreach (var t in Flags.Values) if (t != null) UnityEngine.Object.Destroy(t);
            Portraits.Clear();
            Flags.Clear();
        }

        /// <summary>
        /// True when a generated picture carries a light painted frame of its own, which the image model sometimes adds
        /// despite the prompt: at least two of its top, left and right edges are a bright band. The bottom is left out,
        /// since a white fur mantle may reach it. Pictures here have a dark brown ground, so a bright edge is a frame.
        /// </summary>
        public static bool LooksFramed(byte[] png)
        {
            var t = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!t.LoadImage(png)) return false;
                var px = t.GetPixels32();
                int w = t.width, h = t.height, band = Math.Max(2, Math.Min(w, h) / 64);
                // Unity's rows run from the bottom, so the top edge is the last rows.
                float Mean(int x0, int x1, int y0, int y1)
                {
                    double sum = 0; int n = 0;
                    for (int y = y0; y < y1; y++)
                        for (int x = x0; x < x1; x++) { var c = px[y * w + x]; sum += 0.299 * c.r + 0.587 * c.g + 0.114 * c.b; n++; }
                    return n == 0 ? 0 : (float)(sum / n);
                }
                int bright = 0;
                if (Mean(0, w, h - band, h) > FrameLuminance) bright++;
                if (Mean(0, band, 0, h) > FrameLuminance) bright++;
                if (Mean(w - band, w, 0, h) > FrameLuminance) bright++;
                return bright >= 2;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(t);
            }
        }

        /// <summary>The mean brightness (0 to 255) above which an edge band counts as a painted frame.</summary>
        public const float FrameLuminance = 175f;

        public static Texture2D PortraitOf(string realm) => realm != null && Portraits.TryGetValue(realm, out var t) && t != null ? t : null;
        public static Texture2D FlagOf(string realm) => realm != null && Flags.TryGetValue(realm, out var t) && t != null ? t : null;

#if UNITY_EDITOR
        // The two lines comfy.py reads, from the main checkout's tools/comfyui/.env. Nothing else is read, and nothing
        // read is logged: the key stays in this process.
        private static void ReadDotEnv(ref string url, ref string key)
        {
            try
            {
                string repo = Gateway.MainCheckout(Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "..")));
                string path = Path.Combine(repo, "tools", "comfyui", ".env");
                if (!File.Exists(path)) return;
                foreach (string raw in File.ReadAllLines(path))
                {
                    string line = raw.Trim();
                    int eq = line.IndexOf('=');
                    if (line.StartsWith("#", StringComparison.Ordinal) || eq <= 0) continue;
                    string name = line.Substring(0, eq).Trim(), value = line.Substring(eq + 1).Trim().Trim('"', '\'');
                    if (name == UrlVariable) url = value;
                    else if (name == KeyVariable && string.IsNullOrWhiteSpace(key)) key = value;
                }
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
#endif
    }
}

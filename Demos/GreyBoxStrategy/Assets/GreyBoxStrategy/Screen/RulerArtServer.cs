using System;
using System.Collections.Generic;
using System.IO;
using Agenerela;
using Agenerela.Providers;
using Agenerela.Providers.ComfyUI;
using Agenerela.Providers.SdCpp;
using GreyBoxStrategy.Replay;
using UnityEngine;

namespace GreyBoxStrategy.Realm
{
    /// <summary>
    /// Where Painted Realm's generated portraits and flags come from (DR-025): the team's ComfyUI server through the
    /// framework's <see cref="ComfyUIProvider"/>, or an image model inside the game through the
    /// <c>com.agenerela.sdcpp</c> package's <see cref="SdCppImageProvider"/> (DR-028), as Settings → Painted art
    /// chooses; a cache on disk per world seed; and the textures the screen shows.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Local</b> loads Z-Image Turbo, the package's own recipe (<see cref="SdCppModel.UseZImageTurbo"/>) with the text encoder on the card: its three
    /// files in <c>StreamingAssets/Agenerela/models/</c>, where a built game carries them, or in the folder
    /// <c>AGENERELA_SDCPP_MODELS</c> names (the variable the package's live tests read); the native build from
    /// <c>StreamingAssets/Agenerela/sdcpp/</c>, or the folder <c>AGENERELA_SDCPP_LIBRARY</c> names. One provider lives
    /// for the whole run and keeps the model loaded between pictures; it is freed when the game quits or the setting
    /// moves off Local. It paints on its own worker thread, so a turn never waits for it.
    /// </para>
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

        /// <summary>Where pictures are painted, from Settings → Painted art; the director sets it.</summary>
        public static ArtBackend Backend { get; set; } = ArtBackend.Server;

        public const string ModelsVariable = "AGENERELA_SDCPP_MODELS";
        public const string LibraryVariable = "AGENERELA_SDCPP_LIBRARY";

        /// <summary>
        /// The provider <see cref="Backend"/> names, or null with the reason in <paramref name="why"/>: Off, no server
        /// set, the local model's files or native build missing.
        /// </summary>
        public static IImageProvider Provider(out string why)
        {
            switch (Backend)
            {
                case ArtBackend.Off: why = "Painted art is off in Settings"; return null;
                case ArtBackend.Local: return LocalProvider(out why);
                default: return ServerProvider(out why);
            }
        }

        /// <summary>A line for Settings and the log: where pictures come from now, and why not when they cannot.</summary>
        public static string Describe(ArtBackend backend)
        {
            switch (backend)
            {
                case ArtBackend.Off: return "Off: the art pack's pictures only, nothing painted.";
                case ArtBackend.Local:
                    return LocalSettings(out string why) == null
                        ? "Local: not ready, " + why + "."
                        : "Local: Z-Image Turbo inside the game, on this machine's card, about 20 s a picture; the rulers' model stays on its server.";
                default:
                    return "Server: the image server" + (HasServerUrl() ? "" : $" ({UrlVariable} is not set, so only pictures already painted hang)") + ".";
            }
        }

        private static bool HasServerUrl()
        {
            string url = Environment.GetEnvironmentVariable(UrlVariable), key = null;
#if UNITY_EDITOR
            if (string.IsNullOrWhiteSpace(url)) ReadDotEnv(ref url, ref key);
#endif
            return !string.IsNullOrWhiteSpace(url);
        }

        // ---- Local: stable-diffusion.cpp in the game (DR-028) ----

        private static SdCppImageProvider local;

        /// <summary>
        /// The settings for the local model: the package's Z-Image Turbo recipe, its files in the models folder. Null
        /// with the reason when a file or the native build is missing.
        /// </summary>
        public static SdCppSettings LocalSettings(out string why)
        {
            why = null;
            var recipe = ScriptableObject.CreateInstance<SdCppModel>();
            try
            {
                recipe.UseZImageTurbo();
                var settings = recipe.ToSettings();
                string folder = Environment.GetEnvironmentVariable(ModelsVariable);
                if (!string.IsNullOrWhiteSpace(folder))
                {
                    folder = folder.Trim();
                    settings.DiffusionModel = Path.Combine(folder, Path.GetFileName(settings.DiffusionModel));
                    settings.TextEncoder = Path.Combine(folder, Path.GetFileName(settings.TextEncoder));
                    settings.Vae = Path.Combine(folder, Path.GetFileName(settings.Vae));
                }
                string library = Environment.GetEnvironmentVariable(LibraryVariable);
                if (!string.IsNullOrWhiteSpace(library)) settings.LibraryFolder = library.Trim();
                foreach (string file in new[] { settings.DiffusionModel, settings.TextEncoder, settings.Vae })
                    if (!File.Exists(file)) { why = $"{Path.GetFileName(file)} is not in {Path.GetDirectoryName(file)}"; return null; }
                if (SdCppLibrary.Candidates(settings.LibraryFolder, Application.streamingAssetsPath).Count == 0)
                {
                    why = "no native build of stable-diffusion.cpp is installed (Window → Agenerela → Local Image Models)";
                    return null;
                }
                // The text encoder on the card, as in the measured run (22 s a picture, 6.7 GB peak on the RTX 4060,
                // DR-028 step 3): the rulers' model is on its server, so the card is the image model's. On the CPU,
                // the recipe's default, sd.cpp refuses to encode the prompt when the machine is short of RAM, which
                // an editor and a game beside it make likely (seen in the live tests and here, 25 November).
                settings.TextEncoderOnCpu = false;
                settings.LogNative = false; // its warnings are not the game's
                return settings;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(recipe);
            }
        }

        /// <summary>The one local provider of this run, made on first use; null with the reason when it cannot be.</summary>
        public static SdCppImageProvider LocalProvider(out string why)
        {
            why = null;
            if (local != null) return local;
            var settings = LocalSettings(out why);
            if (settings == null) return null;
            local = new SdCppImageProvider(settings);
            return local;
        }

        /// <summary>Frees the local model and its memory on the card: the game quits, or Painted art moved off Local.</summary>
        public static void FreeLocal()
        {
            var p = local;
            local = null;
            p?.Dispose();
        }

        /// <summary>
        /// The ComfyUI provider, or null with the reason in <paramref name="why"/> when no server is set or the workflow
        /// is missing.
        /// </summary>
        public static ComfyUIProvider ServerProvider(out string why, bool freeAfterEach = false)
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
                    FreeAfterEach = freeAfterEach, // the court: one batch, freed once at the end; an event: freed after each
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

        /// <summary>One more picture, painted in the background after the world began: it shows from the next time its frame is drawn.</summary>
        public static void Hang(string realm, bool flag, byte[] png)
        {
            if (realm == null || !(ImageTextures.ToTexture(png, true, (flag ? "flag " : "portrait ") + realm) is Texture2D t)) return;
            var into = flag ? Flags : Portraits;
            if (into.TryGetValue(realm, out var old) && old != null && old != t) UnityEngine.Object.Destroy(old);
            into[realm] = t;
        }

        /// <summary>Sets <see cref="LastReport"/> for a world painted in two parts (Local: yours first, the rest in the background).</summary>
        public static void Report(string line) => LastReport = line;

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

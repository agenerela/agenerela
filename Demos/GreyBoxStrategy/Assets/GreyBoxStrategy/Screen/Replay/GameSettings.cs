using System;
using System.IO;
using Newtonsoft.Json;

namespace GreyBoxStrategy.Replay
{
    /// <summary>How the game is drawn on screen; the names match Unity's <c>FullScreenMode</c>.</summary>
    public enum DisplayMode { Windowed, Borderless, ExclusiveFullscreen }

    /// <summary>
    /// The player's settings, kept between runs in <c>settings.json</c> in the game's persistent
    /// data folder (<see cref="SettingsStore"/>). Plain values, so the store can be tested without a
    /// screen; the screen applies them.
    /// </summary>
    public sealed class GameSettings
    {
        public const int Version = 1;
        /// <summary>The UI scale steps besides automatic: 0 means automatic, from the screen's size.</summary>
        public static readonly float[] UiScaleSteps = { 0f, 1f, 1.25f, 1.5f, 1.75f, 2f };
        /// <summary>Frame caps offered: 0 means none.</summary>
        public static readonly int[] FrameCaps = { 0, 30, 60, 120, 144, 240 };
        public static readonly float[] Speeds = { 1f, 2f, 4f };

        public int SettingsVersion = Version;
        public DisplayMode Display = DisplayMode.Borderless;
        /// <summary>0 × 0 means the display's own resolution.</summary>
        public int Width, Height;
        /// <summary>0 for automatic (1 at 1080 pixels tall, 2 at 2160), otherwise one of <see cref="UiScaleSteps"/>.</summary>
        public float UiScale;
        public bool VSync = true;
        public int FrameCap;
        public float PlaybackSpeed = 1f;
        public float MasterVolume = 0.8f, MusicVolume = 0.6f, EffectsVolume = 0.8f, VoiceVolume = 0.8f;

        public GameSettings Clone() => (GameSettings)MemberwiseClone();

        /// <summary>Brings every value back into its range, so a hand-edited or old file can never break the game.</summary>
        public GameSettings Normalised()
        {
            var s = Clone();
            s.SettingsVersion = Version;
            if (!Enum.IsDefined(typeof(DisplayMode), s.Display)) s.Display = DisplayMode.Borderless;
            if (s.Width < 640 || s.Height < 480 || s.Width > 16384 || s.Height > 16384) { s.Width = 0; s.Height = 0; }
            s.UiScale = Nearest(UiScaleSteps, s.UiScale);
            s.FrameCap = (int)Nearest(Array.ConvertAll(FrameCaps, x => (float)x), s.FrameCap);
            s.PlaybackSpeed = Nearest(Speeds, s.PlaybackSpeed);
            s.MasterVolume = Clamp01(s.MasterVolume); s.MusicVolume = Clamp01(s.MusicVolume);
            s.EffectsVolume = Clamp01(s.EffectsVolume); s.VoiceVolume = Clamp01(s.VoiceVolume);
            return s;
        }

        /// <summary>Whether two settings differ in what the display shows: mode or resolution.</summary>
        public bool DisplayDiffers(GameSettings other) =>
            other == null || Display != other.Display || Width != other.Width || Height != other.Height;

        private static float Clamp01(float v) => float.IsNaN(v) ? 0.8f : Math.Max(0f, Math.Min(1f, v));

        private static float Nearest(float[] steps, float v)
        {
            if (float.IsNaN(v)) return steps[0];
            float best = steps[0];
            foreach (float x in steps) if (Math.Abs(x - v) < Math.Abs(best - v)) best = x;
            return best;
        }
    }

    /// <summary>Reads and writes <see cref="GameSettings"/> as JSON. A missing or unreadable file gives the defaults.</summary>
    public sealed class SettingsStore
    {
        public SettingsStore(string path) { Path = path; }

        public string Path { get; }

        /// <summary>The saved settings, or the defaults when there are none or they cannot be read.</summary>
        public GameSettings Load()
        {
            try
            {
                if (!File.Exists(Path)) return new GameSettings();
                var s = JsonConvert.DeserializeObject<GameSettings>(File.ReadAllText(Path));
                return (s ?? new GameSettings()).Normalised();
            }
            catch (Exception e) when (e is IOException || e is JsonException || e is UnauthorizedAccessException)
            {
                return new GameSettings();
            }
        }

        /// <summary>Writes through a temporary file, so a crash mid-write never leaves half a file.</summary>
        public void Save(GameSettings settings)
        {
            string dir = System.IO.Path.GetDirectoryName(Path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            string tmp = Path + ".tmp";
            File.WriteAllText(tmp, JsonConvert.SerializeObject(settings.Normalised(), Formatting.Indented,
                new Newtonsoft.Json.Converters.StringEnumConverter()));
            if (File.Exists(Path)) File.Replace(tmp, Path, null);
            else File.Move(tmp, Path);
        }
    }
}

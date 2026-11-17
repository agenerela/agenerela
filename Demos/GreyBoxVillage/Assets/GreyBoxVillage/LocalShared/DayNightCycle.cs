using System;
using System.Globalization;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Demos.Shared
{
    /// <summary>
    /// A compressed day: the sun crosses the sky and sets, the moon takes over, the ambient light,
    /// fog and sky follow it, windows and lamps glow after dusk and night-only particles (fireflies)
    /// come out. One <see cref="TimeOfDay"/> drives all of it, so a game or a presenter key can jump
    /// to any hour.
    /// </summary>
    /// <remarks>
    /// The palette and the sky are assets, shared by every model and every scene that uses them, so
    /// in Play mode the cycle writes only to copies it makes in <c>Awake</c>
    /// (<see cref="UseRuntimeCopies"/>): the palette's copy goes on every renderer and every
    /// <see cref="ScatterField"/> that used the asset, and the sky's becomes the scene's skybox. A play
    /// session leaves no change behind in the project. In Edit mode, without copies, it writes to the
    /// assets themselves, which is how the scene builder sets their starting look.
    /// </remarks>
    public sealed class DayNightCycle : MonoBehaviour
    {
        public Light Sun;
        [Tooltip("The material every model shares; its emission is the window and lamp glow.")]
        public Material Palette;
        public Material Sky;
        [Tooltip("Lights that only shine at night: lamps, torches.")]
        public FlickerLight[] Lamps = Array.Empty<FlickerLight>();
        [Tooltip("Particle systems that only play at night: fireflies.")]
        public ParticleSystem[] NightOnly = Array.Empty<ParticleSystem>();
        [Tooltip("Real seconds in one whole day.")]
        public float DayLengthSeconds = 600f;
        [Range(0f, 1f), Tooltip("0 is midnight, 0.25 sunrise, 0.5 noon, 0.75 sunset.")]
        public float TimeOfDay = 0.36f;
        public bool Running = true;
        public float SunYaw = -35f;
        [Tooltip("The fog's density in clear weather; the weather adds its own haze to it.")]
        public float FogDensity = 0.0085f;

        /// <summary>
        /// How much the weather dims the day: 0 in sunshine, 1 under the darkest sky. Set by the game's
        /// weather each frame and never saved, like <see cref="Haze"/> and <see cref="Wetness"/>.
        /// </summary>
        [NonSerialized] public float Gloom;
        /// <summary>Fog the weather adds to <see cref="FogDensity"/>.</summary>
        [NonSerialized] public float Haze;
        /// <summary>How wet everything looks, 0 to 1: it darkens the palette's runtime copy.</summary>
        [NonSerialized] public float Wetness;

        /// <summary>Which day it is, from 1.</summary>
        public int Day { get; private set; } = 1;
        /// <summary>0 at noon, 1 at midnight.</summary>
        public float Darkness { get; private set; }
        /// <summary>
        /// Whether it is night at <see cref="TimeOfDay"/>: read from the hour itself, so setting the
        /// hour answers at once, before the next <see cref="Apply()"/> (which, in Edit mode, writes
        /// to the palette and sky assets).
        /// </summary>
        public bool IsNight => DarknessAt(TimeOfDay) > 0.6f;

        /// <summary>How dark it is at this time of day: 0 at noon, 1 at midnight.</summary>
        public static float DarknessAt(float timeOfDay) =>
            Mathf.Clamp01(0.5f - Mathf.Sin((timeOfDay - 0.25f) * 360f * Mathf.Deg2Rad) * 1.6f);
        /// <summary>The time as the HUD shows it, such as "07:40".</summary>
        public string Clock
        {
            get
            {
                int minutes = Mathf.FloorToInt(TimeOfDay * 24f * 60f) % (24 * 60);
                return (minutes / 60).ToString("00", CultureInfo.InvariantCulture) + ":" +
                       (minutes % 60 / 10 * 10).ToString("00", CultureInfo.InvariantCulture);
            }
        }
        /// <summary>A word for the hour: dawn, morning, midday, afternoon, dusk, evening or night.</summary>
        public string Phase
        {
            get
            {
                float h = TimeOfDay * 24f;
                if (h < 5f || h >= 21.5f) return "night";
                if (h < 7f) return "dawn";
                if (h < 11f) return "morning";
                if (h < 14f) return "midday";
                if (h < 17.5f) return "afternoon";
                if (h < 19.5f) return "dusk";
                return "evening";
            }
        }

        /// <summary>Raised when night falls (true) and when it ends (false).</summary>
        public event Action<bool> NightChanged;

        private Material glow, sky;
        private bool wasNight;

        /// <summary>The material the window glow is written to: the palette's copy once there is one.</summary>
        public Material PaletteInUse => glow != null ? glow : Palette;
        /// <summary>The skybox the sky colours are written to: the sky's copy once there is one.</summary>
        public Material SkyInUse => sky != null ? sky : Sky;

        // Key colours for noon, dusk and night; everything between is blended by the sun's height.
        private static readonly Color SunNoon = new Color(1f, 0.95f, 0.86f), SunDusk = new Color(1f, 0.6f, 0.36f), Moon = new Color(0.55f, 0.66f, 1f);
        private static readonly Color SkyNoon = new Color(0.66f, 0.76f, 0.9f), SkyDusk = new Color(0.66f, 0.52f, 0.58f), SkyNight = new Color(0.13f, 0.16f, 0.3f);
        private static readonly Color EqNoon = new Color(0.62f, 0.64f, 0.58f), EqDusk = new Color(0.55f, 0.42f, 0.4f), EqNight = new Color(0.09f, 0.11f, 0.19f);
        private static readonly Color GroundNoon = new Color(0.36f, 0.33f, 0.26f), GroundDusk = new Color(0.27f, 0.21f, 0.19f), GroundNight = new Color(0.04f, 0.05f, 0.09f);
        private static readonly Color FogNoon = new Color(0.73f, 0.82f, 0.9f), FogDusk = new Color(0.86f, 0.63f, 0.52f), FogNight = new Color(0.07f, 0.09f, 0.17f);

        private void Awake()
        {
            if (Application.isPlaying) UseRuntimeCopies();
            Apply();
            wasNight = IsNight;
        }

        /// <summary>
        /// Copies the palette and the sky, and puts the copies where the assets were: on every
        /// renderer and <see cref="ScatterField"/> using the palette, and as the scene's skybox. Every
        /// later <see cref="Apply()"/> writes to the copies. Play mode does this in <c>Awake</c>.
        /// Calling it again does nothing.
        /// </summary>
        public void UseRuntimeCopies()
        {
            if (Palette != null && glow == null)
            {
                glow = new Material(Palette) { name = Palette.name + " (runtime)", hideFlags = HideFlags.DontSave };
                foreach (var r in FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    var shared = r.sharedMaterials;
                    bool changed = false;
                    for (int i = 0; i < shared.Length; i++)
                        if (shared[i] == Palette) { shared[i] = glow; changed = true; }
                    if (changed) r.sharedMaterials = shared;
                }
                foreach (var field in FindObjectsByType<ScatterField>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    if (field.Material == Palette) field.Material = glow;
            }
            if (Sky != null && sky == null)
            {
                sky = new Material(Sky) { name = Sky.name + " (runtime)", hideFlags = HideFlags.DontSave };
                if (RenderSettings.skybox == Sky || RenderSettings.skybox == null) RenderSettings.skybox = sky;
            }
        }

        private void OnDestroy()
        {
            // Whatever outlives this component gets the assets back.
            if (sky != null)
            {
                if (RenderSettings.skybox == sky) RenderSettings.skybox = Sky;
                Release(sky);
            }
            if (glow != null)
            {
                foreach (var field in FindObjectsByType<ScatterField>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    if (field.Material == glow) field.Material = Palette;
                Release(glow);
            }
        }

        private static void Release(Material m)
        {
            if (Application.isPlaying) Object.Destroy(m);
            else DestroyImmediate(m);
        }

        private void Update()
        {
            if (Running && DayLengthSeconds > 0)
            {
                TimeOfDay += Time.deltaTime / DayLengthSeconds;
                if (TimeOfDay >= 1f)
                {
                    TimeOfDay -= 1f;
                    Day++;
                }
            }
            Apply();
            if (IsNight != wasNight)
            {
                wasNight = IsNight;
                NightChanged?.Invoke(IsNight);
            }
        }

        /// <summary>Sets every light, colour and effect for <see cref="TimeOfDay"/>. Works in Edit mode too.</summary>
        public void Apply() => Apply(PaletteInUse);

        /// <summary>As <see cref="Apply()"/>, writing the glow to the given material.</summary>
        public void Apply(Material glowTarget)
        {
            float elevation = (TimeOfDay - 0.25f) * 360f;
            float height = Mathf.Sin(elevation * Mathf.Deg2Rad);
            Darkness = DarknessAt(TimeOfDay);

            Color Blend(Color noon, Color dusk, Color night) => height >= 0
                ? Color.Lerp(dusk, noon, Mathf.Clamp01(height * 2.2f))
                : Color.Lerp(dusk, night, Mathf.Clamp01(-height * 4f));
            // The weather greys a colour toward its own brightness, a little darker.
            float gloom = Mathf.Clamp01(Gloom), haze = Mathf.Max(0f, Haze);
            Color Grey(Color c, float amount, float brightness = 0.82f)
            {
                float l = c.grayscale * brightness;
                return Color.Lerp(c, new Color(l, l, l, c.a), amount);
            }

            if (Sun != null)
            {
                bool day = height > -0.08f;
                float pitch = day ? Mathf.Clamp(elevation, 4f, 176f) : Mathf.Clamp(elevation - 180f, 20f, 160f);
                Sun.transform.rotation = Quaternion.Euler(pitch, SunYaw + (day ? 0 : 60), 0);
                Sun.color = day ? Color.Lerp(SunDusk, SunNoon, Mathf.Clamp01(height * 2.5f)) : Moon;
                Sun.intensity = (day ? Mathf.Lerp(0.45f, 1.35f, Mathf.Clamp01(height * 2f)) : Mathf.Lerp(0.12f, 0.32f, Mathf.Clamp01(-height * 3f)))
                    * (1f - 0.55f * gloom);
                Sun.shadowStrength = (day ? 0.85f : 0.55f) * (1f - 0.75f * gloom);
            }

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = Grey(Blend(SkyNoon, SkyDusk, SkyNight), gloom * 0.7f);
            RenderSettings.ambientEquatorColor = Grey(Blend(EqNoon, EqDusk, EqNight), gloom * 0.6f);
            RenderSettings.ambientGroundColor = Grey(Blend(GroundNoon, GroundDusk, GroundNight), gloom * 0.5f);
            // Fog and rain wash the fog colour out to a pale grey by day, a dark one by night.
            RenderSettings.fogColor = Grey(Blend(FogNoon, FogDusk, FogNight), Mathf.Max(gloom * 0.8f, Mathf.Clamp01(haze * 20f)), 1.05f);
            RenderSettings.fogDensity = FogDensity + haze;

            var skyTarget = SkyInUse;
            // Never an asset during Play: without its copy, the sky is left as it is.
            if (skyTarget != null && (sky != null || !Application.isPlaying))
            {
                skyTarget.SetFloat("_Exposure", Mathf.Lerp(0.12f, 1.25f, Mathf.Clamp01(height * 2f + 0.25f)) * (1f - 0.45f * gloom));
                skyTarget.SetColor("_SkyTint", Grey(Blend(new Color(0.5f, 0.6f, 0.75f), new Color(0.75f, 0.5f, 0.45f), new Color(0.2f, 0.25f, 0.45f)), gloom * 0.85f, 1f));
            }

            if (glowTarget != null && (glowTarget != Palette || !Application.isPlaying))
            {
                glowTarget.SetColor("_EmissionColor", Color.white * Mathf.Lerp(0.12f, 2.4f, Darkness));
                // Wet ground and roofs read darker. Weather is only ever set in Play mode, so the
                // asset itself always gets plain white.
                glowTarget.SetColor("_BaseColor", Color.Lerp(Color.white, new Color(0.68f, 0.7f, 0.74f), Mathf.Clamp01(Wetness)));
            }

            foreach (var lamp in Lamps)
                if (lamp != null) lamp.Darkness = Darkness;
            foreach (var ps in NightOnly)
            {
                if (ps == null) continue;
                var emission = ps.emission;
                emission.enabled = Darkness > 0.55f;
            }
        }
    }
}

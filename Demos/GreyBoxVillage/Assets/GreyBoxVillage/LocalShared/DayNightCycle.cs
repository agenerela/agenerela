using System;
using System.Globalization;
using UnityEngine;

namespace Demos.Shared
{
    /// <summary>
    /// A compressed day: the sun crosses the sky and sets, the moon takes over, the ambient light,
    /// fog and sky follow it, windows and lamps glow after dusk and night-only particles (fireflies)
    /// come out. One <see cref="TimeOfDay"/> drives all of it, so a game or a presenter key can jump
    /// to any hour.
    /// </summary>
    /// <remarks>
    /// The palette material is shared by every model, so its window glow is changed on a copy made at
    /// start, never on the asset: a play session leaves no change behind in the project.
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

        /// <summary>Which day it is, from 1.</summary>
        public int Day { get; private set; } = 1;
        /// <summary>0 at noon, 1 at midnight.</summary>
        public float Darkness { get; private set; }
        public bool IsNight => Darkness > 0.6f;
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

        private Material glow;
        private bool wasNight;

        // Key colours for noon, dusk and night; everything between is blended by the sun's height.
        private static readonly Color SunNoon = new Color(1f, 0.95f, 0.86f), SunDusk = new Color(1f, 0.6f, 0.36f), Moon = new Color(0.55f, 0.66f, 1f);
        private static readonly Color SkyNoon = new Color(0.66f, 0.76f, 0.9f), SkyDusk = new Color(0.66f, 0.52f, 0.58f), SkyNight = new Color(0.13f, 0.16f, 0.3f);
        private static readonly Color EqNoon = new Color(0.62f, 0.64f, 0.58f), EqDusk = new Color(0.55f, 0.42f, 0.4f), EqNight = new Color(0.09f, 0.11f, 0.19f);
        private static readonly Color GroundNoon = new Color(0.36f, 0.33f, 0.26f), GroundDusk = new Color(0.27f, 0.21f, 0.19f), GroundNight = new Color(0.04f, 0.05f, 0.09f);
        private static readonly Color FogNoon = new Color(0.73f, 0.82f, 0.9f), FogDusk = new Color(0.86f, 0.63f, 0.52f), FogNight = new Color(0.07f, 0.09f, 0.17f);

        private void Awake()
        {
            if (Palette != null)
            {
                glow = new Material(Palette) { name = Palette.name + " (runtime)" };
                foreach (var r in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
                {
                    var shared = r.sharedMaterials;
                    bool changed = false;
                    for (int i = 0; i < shared.Length; i++)
                        if (shared[i] == Palette) { shared[i] = glow; changed = true; }
                    if (changed) r.sharedMaterials = shared;
                }
            }
            Apply();
            wasNight = IsNight;
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
        public void Apply() => Apply(glow != null ? glow : Palette);

        /// <summary>As <see cref="Apply()"/>, writing the glow to the given material.</summary>
        public void Apply(Material glowTarget)
        {
            float elevation = (TimeOfDay - 0.25f) * 360f;
            float height = Mathf.Sin(elevation * Mathf.Deg2Rad);
            Darkness = Mathf.Clamp01(0.5f - height * 1.6f);

            Color Blend(Color noon, Color dusk, Color night) => height >= 0
                ? Color.Lerp(dusk, noon, Mathf.Clamp01(height * 2.2f))
                : Color.Lerp(dusk, night, Mathf.Clamp01(-height * 4f));

            if (Sun != null)
            {
                bool day = height > -0.08f;
                float pitch = day ? Mathf.Clamp(elevation, 4f, 176f) : Mathf.Clamp(elevation - 180f, 20f, 160f);
                Sun.transform.rotation = Quaternion.Euler(pitch, SunYaw + (day ? 0 : 60), 0);
                Sun.color = day ? Color.Lerp(SunDusk, SunNoon, Mathf.Clamp01(height * 2.5f)) : Moon;
                Sun.intensity = day ? Mathf.Lerp(0.45f, 1.35f, Mathf.Clamp01(height * 2f)) : Mathf.Lerp(0.12f, 0.32f, Mathf.Clamp01(-height * 3f));
                Sun.shadowStrength = day ? 0.85f : 0.55f;
            }

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = Blend(SkyNoon, SkyDusk, SkyNight);
            RenderSettings.ambientEquatorColor = Blend(EqNoon, EqDusk, EqNight);
            RenderSettings.ambientGroundColor = Blend(GroundNoon, GroundDusk, GroundNight);
            RenderSettings.fogColor = Blend(FogNoon, FogDusk, FogNight);

            if (Sky != null)
            {
                Sky.SetFloat("_Exposure", Mathf.Lerp(0.12f, 1.25f, Mathf.Clamp01(height * 2f + 0.25f)));
                Sky.SetColor("_SkyTint", Blend(new Color(0.5f, 0.6f, 0.75f), new Color(0.75f, 0.5f, 0.45f), new Color(0.2f, 0.25f, 0.45f)));
            }

            if (glowTarget != null)
                glowTarget.SetColor("_EmissionColor", Color.white * Mathf.Lerp(0.12f, 2.4f, Darkness));

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

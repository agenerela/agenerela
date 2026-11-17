using System;
using System.Collections.Generic;
using System.Globalization;
using Demos.Shared;
using UnityEngine;

namespace Demos.GreyBoxVillage
{
    /// <summary>The weather over Ashford.</summary>
    public enum WeatherKind
    {
        Clear,
        Overcast,
        Rain,
        Fog
    }

    /// <summary>
    /// Clear, overcast, rain and fog, changing on its own a few times a day and on the presenter's
    /// F6. It shows (rain around the camera, mist, wet ground, a darker sky) and it changes what the
    /// agents notice: rain and fog shorten a guard's sight (<see cref="SightLimit"/>, which
    /// <see cref="GuardSenses.CanSee"/> applies), and every agent is told so in its observations.
    /// </summary>
    /// <remarks>
    /// The weather is never an availability input: it offers no action and takes none away. A guard
    /// in fog is offered what it would be offered in sunshine for what it can see; it only sees less.
    /// The look is written through <see cref="DayNightCycle"/>'s weather fields, which reach only the
    /// runtime copies of the palette and the sky, so a rainy play session changes no file.
    /// </remarks>
    public sealed class VillageWeather : MonoBehaviour
    {
        public DayNightCycle Day;
        [Tooltip("The camera: the rain and the mist stay over the ground around it.")]
        public Transform Follow;
        [Tooltip("Falls around the camera while it rains.")]
        public ParticleSystem Rain;
        [Tooltip("Drifts low around the camera in fog.")]
        public ParticleSystem Mist;
        [Tooltip("A dome round the camera that greys the sky over as the weather closes in. Its material is copied when Play starts.")]
        public Renderer SkyDome;
        [Tooltip("Whether the weather changes by itself through the day.")]
        public bool Changes = true;
        [Tooltip("Game hours between the changes it makes by itself.")]
        public float ChangeEveryHours = 5f;
        [Tooltip("How far a guard can see in the rain, in metres.")]
        public float RainSight = 22f;
        [Tooltip("How far a guard can see in fog, in metres.")]
        public float FogSight = 15f;
        [Tooltip("Real seconds a change takes to come in.")]
        public float BlendSeconds = 4f;

        /// <summary>The order the weather turns in by itself, round and round.</summary>
        public static readonly IReadOnlyList<WeatherKind> Pattern = Array.AsReadOnly(new[]
            { WeatherKind.Clear, WeatherKind.Overcast, WeatherKind.Rain, WeatherKind.Clear, WeatherKind.Fog, WeatherKind.Overcast });

        public WeatherKind Current { get; private set; } = WeatherKind.Clear;

        /// <summary>How hard it is raining as the look blends in, 0 to 1: the rain's sound follows it.</summary>
        public float RainAmount => rain;

        /// <summary>Raised when the weather changes, by itself or by <see cref="Set"/>.</summary>
        public event Action<WeatherKind> Changed;

        /// <summary>How far anyone can see in this weather, in metres; infinity when it limits nothing.</summary>
        public float SightLimit => Current == WeatherKind.Fog ? FogSight
            : Current == WeatherKind.Rain ? RainSight : float.PositiveInfinity;

        /// <summary>The weather in a word, for the presenter and the HUD.</summary>
        public string Word => Current == WeatherKind.Clear ? "clear" : Current == WeatherKind.Overcast ? "overcast"
            : Current == WeatherKind.Rain ? "rain" : "fog";

        /// <summary>
        /// The line every agent is told, or null in clear weather, which adds nothing to the prompt.
        /// Where sight is short, it says how far.
        /// </summary>
        public string Observation =>
            Current == WeatherKind.Overcast ? "The sky is overcast."
            : Current == WeatherKind.Rain ? "It is raining; you can see about " + Metres(RainSight) + "."
            : Current == WeatherKind.Fog ? "Fog lies over the village; you can see about " + Metres(FogSight) + "."
            : null;

        private int step;
        private float hoursSince;
        private float gloom, haze, wet, rain, mist, cover;
        private Material dome;

        private static string Metres(float m) => m.ToString("0", CultureInfo.InvariantCulture) + " m";

        /// <summary>Sets the weather now; the next change it makes by itself is <see cref="ChangeEveryHours"/> away.</summary>
        public void Set(WeatherKind weather)
        {
            hoursSince = 0f;
            for (int i = 0; i < Pattern.Count; i++)
                if (Pattern[(step + i) % Pattern.Count] == weather)
                {
                    step = (step + i) % Pattern.Count;
                    break;
                }
            if (Current == weather) return;
            Current = weather;
            Changed?.Invoke(weather);
        }

        /// <summary>The next weather in the list, clear to overcast to rain to fog and round: the presenter's F6.</summary>
        public WeatherKind Cycle()
        {
            Set((WeatherKind)(((int)Current + 1) % 4));
            return Current;
        }

        /// <summary>
        /// Counts game hours toward the next change, and makes it when they are up. Driven by the
        /// running clock, never by a jump in time, so the presenter's F5 to dusk does not change it.
        /// </summary>
        public void Advance(float hours)
        {
            if (!Changes || hours <= 0f) return;
            hoursSince += hours;
            while (hoursSince >= ChangeEveryHours)
            {
                hoursSince -= ChangeEveryHours;
                step = (step + 1) % Pattern.Count;
                if (Pattern[step] == Current) continue;
                Current = Pattern[step];
                Changed?.Invoke(Current);
            }
        }

        /// <summary>Puts the look of the weather in at once, with no blend: for captures and the presenter's reset.</summary>
        public void Snap() => Blend(float.PositiveInfinity);

        // The dome's material is shared with the scene file: Play writes its colour to a copy only.
        private void Awake()
        {
            if (!Application.isPlaying || SkyDome == null || SkyDome.sharedMaterial == null) return;
            dome = new Material(SkyDome.sharedMaterial) { name = SkyDome.sharedMaterial.name + " (runtime)", hideFlags = HideFlags.DontSave };
            SkyDome.sharedMaterial = dome;
        }

        private void OnDestroy()
        {
            if (dome != null) Destroy(dome);
        }

        private void OnDisable()
        {
            if (Day == null) return;
            Day.Gloom = Day.Haze = Day.Wetness = 0f;
        }

        private void Update()
        {
            if (Day != null && Day.Running && Day.DayLengthSeconds > 0)
                Advance(Time.deltaTime / Day.DayLengthSeconds * 24f);
            Blend(Time.deltaTime);
        }

        // Over the ground below the camera, never turned with it, so the rain falls straight down.
        private void LateUpdate()
        {
            if (Follow != null) transform.position = new Vector3(Follow.position.x, 0f, Follow.position.z);
        }

        // Eases the look toward this weather's; wet ground dries more slowly than it gets wet.
        private void Blend(float dt)
        {
            float tGloom = 0f, tHaze = 0f, tWet = 0f, tRain = 0f, tMist = 0f, tCover = 0f;
            switch (Current)
            {
                case WeatherKind.Overcast: tGloom = 0.55f; tHaze = 0.008f; tCover = 0.7f; break;
                case WeatherKind.Rain: tGloom = 0.7f; tHaze = 0.012f; tWet = 1f; tRain = 1f; tCover = 0.9f; break;
                case WeatherKind.Fog: tGloom = 0.35f; tHaze = 0.042f; tWet = 0.3f; tMist = 1f; tCover = 1f; break;
            }
            float k = BlendSeconds <= 0f ? 1f : Mathf.Clamp01(dt / BlendSeconds);
            gloom = Mathf.Lerp(gloom, tGloom, k);
            haze = Mathf.Lerp(haze, tHaze, k);
            rain = Mathf.Lerp(rain, tRain, k);
            mist = Mathf.Lerp(mist, tMist, k);
            cover = Mathf.Lerp(cover, tCover, k);
            wet = Mathf.Lerp(wet, tWet, tWet > wet ? k : k * 0.2f);
            if (float.IsPositiveInfinity(dt)) wet = tWet;
            if (Day != null)
            {
                Day.Gloom = gloom;
                Day.Haze = haze;
                Day.Wetness = wet;
            }
            Emit(Rain, rain, 900f);
            Emit(Mist, mist, 6f);
            if (SkyDome != null && dome != null)
            {
                // The fog's own colour, so the clouds meet the horizon without a seam.
                var c = RenderSettings.fogColor;
                c.a = cover;
                dome.color = c;
                SkyDome.enabled = cover > 0.01f;
            }
        }

        private static void Emit(ParticleSystem ps, float amount, float rate)
        {
            if (ps == null) return;
            var emission = ps.emission;
            emission.rateOverTime = amount * rate;
            bool on = amount > 0.02f;
            if (on && !ps.isPlaying) ps.Play();
            else if (!on && ps.isPlaying) ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }
}

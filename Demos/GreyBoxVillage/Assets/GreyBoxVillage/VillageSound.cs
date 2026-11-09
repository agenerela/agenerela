using System.Collections.Generic;
using Demos.Shared;
using UnityEngine;

namespace Demos.GreyBoxVillage
{
    /// <summary>
    /// Ashford's sound: birds by day, crickets and an owl at night, the river by the bridge, market
    /// chatter while people are there, the campfire, the rain; and the bell, footsteps on dirt and
    /// wood, the gate, a bucket thrown on the fire, the wolf's howl and the fire itself.
    /// </summary>
    /// <remarks>
    /// It only listens: it reads the hour, the weather, the routine and the state of the bell, the
    /// gate, the fire and the wolf every frame, and changes none of them, so nothing else in the
    /// game knows it is there. Its sources are made when Play starts and never saved. A clip that
    /// is missing stays silent, so the village plays the same with no sound at all. The menu's
    /// volume and mute (<see cref="DemoMenu.Gain"/>) scale every source. <see cref="Build"/> and
    /// <see cref="Tick"/> are public so the EditMode tests can make the sources and step them.
    /// </remarks>
    public sealed class VillageSound : MonoBehaviour
    {
        [Header("Ambience, looped")]
        public AudioClip Birds;
        public AudioClip Crickets;
        public AudioClip River;
        public AudioClip Market;
        public AudioClip Campfire;
        public AudioClip Rain;
        [Header("Effects")]
        public AudioClip Owl;
        public AudioClip Bell;
        public AudioClip StepsDirt;
        public AudioClip StepsWood;
        public AudioClip Gate;
        public AudioClip Splash;
        public AudioClip Howl;
        public AudioClip FireRoar;

        [Header("What it listens to")]
        public DemoMenu Menu;
        public DayNightCycle Day;
        public VillageWeather Weather;
        public VillageRoutine Routine;
        public VillageAlarm Alarm;
        public VillageGate GateDoors;
        public VillageFire StallFire;
        public VillageWolf Wolf;
        public Transform Player;
        [Header("Where")]
        public Transform[] RiverAt = new Transform[0];
        public Transform MarketAt;
        public Transform CampfireAt;
        [Tooltip("Where the owl calls from at night: somewhere in the woods.")]
        public Transform[] OwlAt = new Transform[0];
        [Tooltip("Planks underfoot: the bridge and the docks, as boxes in world space (centre, size).")]
        public Bounds[] Wood = new Bounds[0];

        private AudioSource birds, crickets, rain, steps, market, campfire, bell, fire;
        private readonly List<AudioSource> rivers = new List<AudioSource>();
        private readonly Dictionary<string, AudioSource> sources = new Dictionary<string, AudioSource>();
        // The effects play on voices of their own, so two at once never share one source (the second
        // would move the first), and each keeps its own volume, scaled by the menu every frame.
        private const int VoiceCount = 4;
        private readonly List<AudioSource> voices = new List<AudioSource>();
        private readonly List<float> voiceVolumes = new List<float>();
        private int nextVoice;

        /// <summary>Every looped source by name (birds, crickets, rain, steps, river, market, campfire, bell, fire), once built.</summary>
        public IReadOnlyDictionary<string, AudioSource> Sources => sources;

        /// <summary>The voices the effects play on (the gate, the bucket, the howl, the owl), once built.</summary>
        public IReadOnlyList<AudioSource> Effects => voices;
        private bool gateWasOpen = true, wolfWasHidden = true;
        private int splashes;
        private float nextOwl, nextHowl;
        private Vector3 lastPlayer;
        private float moving;

        /// <summary>How loud everything is: the menu's volume, or nothing when muted.</summary>
        public float Gain => Menu != null ? Menu.Gain : 1f;

        private void Start() => Build();

        private void OnDestroy() => Release();

        /// <summary>Makes the sources, once. Play mode does it in <c>Start</c>.</summary>
        public void Build()
        {
            if (birds != null) return;
            birds = Loop("birds", Birds, null, 0f);
            crickets = Loop("crickets", Crickets, null, 0f);
            rain = Loop("rain", Rain, null, 0f);
            steps = Loop("steps", StepsDirt, Player, 0f, 2f, 12f);
            foreach (var at in RiverAt) rivers.Add(Loop("river", River, at, 0f, 4f, 26f));
            market = Loop("market", Market, MarketAt, 0f, 5f, 30f);
            campfire = Loop("campfire", Campfire, CampfireAt, 0f, 2f, 16f);
            bell = Loop("bell", Bell, Alarm != null ? Alarm.transform : null, 0f, 8f, 120f);
            fire = Loop("fire", FireRoar, StallFire != null ? StallFire.transform : null, 0f, 4f, 40f);
            for (int i = 0; i < VoiceCount; i++)
            {
                voices.Add(Source("effect " + i, null, 1f, 3f, 60f));
                voiceVolumes.Add(0f);
            }
            nextVoice = 0;
            if (GateDoors != null) gateWasOpen = GateDoors.IsOpen;
            if (Wolf != null) wolfWasHidden = Wolf.IsHidden;
            if (StallFire != null) splashes = StallFire.Trips;
            if (Player != null) lastPlayer = Player.position;
            nextOwl = Time.time + Random.Range(8f, 20f);
            nextHowl = Time.time + Random.Range(40f, 80f);
        }

        // A source of its own, made at run time and never saved.
        private AudioSource Source(string name, Transform at, float spatial, float near, float far)
        {
            var go = new GameObject("sound " + name) { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(at != null ? at : transform, false);
            var s = go.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.spatialBlend = spatial;
            s.rolloffMode = AudioRolloffMode.Linear;
            s.minDistance = near;
            s.maxDistance = far;
            s.dopplerLevel = 0f;
            return s;
        }

        private AudioSource Loop(string name, AudioClip clip, Transform at, float volume, float near = 1f, float far = 40f)
        {
            var s = Source(name, at, at != null ? 1f : 0f, near, far);
            s.clip = clip;
            s.loop = true;
            s.volume = volume;
            sources[sources.ContainsKey(name) ? name + " " + sources.Count : name] = s;
            return s;
        }

        /// <summary>Destroys the sources it made. Used by the tests; Play mode does it when the village ends.</summary>
        public void Release()
        {
            foreach (var s in sources.Values)
                if (s != null) Destroy(s.gameObject);
            foreach (var s in voices)
                if (s != null) Destroy(s.gameObject);
            sources.Clear();
            rivers.Clear();
            voices.Clear();
            voiceVolumes.Clear();
            birds = crickets = rain = steps = market = campfire = bell = fire = null;
        }

        private static void Destroy(GameObject go)
        {
            if (Application.isPlaying) Object.Destroy(go);
            else DestroyImmediate(go);
        }

        // Plays while it should be heard, at that volume; stopped when silent, so a missing clip costs nothing.
        private void Set(AudioSource s, float volume)
        {
            if (s == null) return;
            s.volume = Mathf.Clamp01(volume) * Gain;
            bool on = s.clip != null && s.volume > 0.001f;
            if (on && !s.isPlaying) s.Play();
            else if (!on && s.isPlaying) s.Stop();
        }

        // An effect on a voice of its own: a free one from the cursor on, or, with all of them
        // playing, the one started longest ago. It starts even when muted, so unmuting brings it back.
        private void Once(AudioClip clip, Vector3 at, float volume)
        {
            if (clip == null || voices.Count == 0) return;
            int pick = nextVoice;
            for (int i = 0; i < voices.Count; i++)
            {
                int k = (nextVoice + i) % voices.Count;
                if (!voices[k].isPlaying) { pick = k; break; }
            }
            nextVoice = (pick + 1) % voices.Count;
            var s = voices[pick];
            s.Stop();
            s.transform.position = at;
            s.clip = clip;
            voiceVolumes[pick] = Mathf.Clamp01(volume);
            s.volume = voiceVolumes[pick] * Gain;
            s.Play();
        }

        private void Update() => Tick();

        /// <summary>One frame: every source's volume from the hour, the weather and the village, and the effects that are due.</summary>
        public void Tick()
        {
            if (birds == null) return;
            // Read from the hour itself, so a test can set it without Apply().
            float dark = Day != null ? DayNightCycle.DarknessAt(Day.TimeOfDay) : 0f;
            float wet = Weather != null ? Weather.RainAmount : 0f;
            // Rain drowns the birds and the crickets; the village goes quiet under it.
            float hush = 1f - 0.7f * wet;
            Set(birds, (1f - dark) * 0.45f * hush);
            Set(crickets, Mathf.Clamp01((dark - 0.5f) * 2f) * 0.4f * hush);
            Set(rain, wet * 0.7f);
            foreach (var r in rivers) Set(r, 0.55f);
            Set(campfire, 0.6f);

            // The market is as loud as it is busy: Odile alone, or the villagers too.
            int shoppers = 0;
            if (Routine != null)
                foreach (var v in Routine.Villagers)
                    if (v != null && !v.IsHome && Routine.SpotOf(v)?.Id == VillageRoutine.Market) shoppers++;
            Set(market, (1f - dark) * (0.15f + 0.2f * shoppers));

            Set(bell, Alarm != null && Alarm.IsSounding ? 0.9f : 0f);
            Set(fire, StallFire != null && StallFire.IsBurning ? 0.8f : 0f);

            if (GateDoors != null && GateDoors.IsOpen != gateWasOpen)
            {
                gateWasOpen = GateDoors.IsOpen;
                Once(Gate, GateDoors.transform.position, 0.9f);
            }
            if (StallFire != null && StallFire.Trips > splashes)
            {
                splashes = StallFire.Trips;
                Once(Splash, StallFire.transform.position, 1f);
            }
            if (StallFire != null && StallFire.Trips < splashes) splashes = StallFire.Trips;

            // The wolf howls when it comes out, and now and then at night from wherever it is.
            if (Wolf != null)
            {
                if (wolfWasHidden && !Wolf.IsHidden) Once(Howl, Wolf.transform.position, 1f);
                wolfWasHidden = Wolf.IsHidden;
                if (dark > 0.6f && !Wolf.IsHidden && Time.time >= nextHowl)
                {
                    nextHowl = Time.time + Random.Range(50f, 110f);
                    Once(Howl, Wolf.transform.position, 0.8f);
                }
            }
            if (dark > 0.6f && OwlAt.Length > 0 && Time.time >= nextOwl)
            {
                nextOwl = Time.time + Random.Range(18f, 40f);
                Once(Owl, OwlAt[Random.Range(0, OwlAt.Length)].position, 0.7f * hush);
            }
            // The menu reaches an effect already playing, as it reaches every loop.
            for (int i = 0; i < voices.Count; i++)
                if (voices[i] != null) voices[i].volume = voiceVolumes[i] * Gain;
            Footsteps();
        }

        // The player's steps while it walks: planks on the bridge and the docks, dirt everywhere else.
        private void Footsteps()
        {
            if (Player == null || steps == null || Time.deltaTime <= 0f) return;
            var d = Player.position - lastPlayer;
            d.y = 0;
            lastPlayer = Player.position;
            float speed = d.magnitude / Time.deltaTime;
            moving = Mathf.MoveTowards(moving, speed > 0.5f ? 1f : 0f, Time.deltaTime * 6f);
            var clip = OnWood(Player.position) ? StepsWood : StepsDirt;
            if (steps.clip != clip)
            {
                steps.Stop();
                steps.clip = clip;
            }
            steps.pitch = speed > 4f ? 1.35f : 1f;
            Set(steps, moving * 0.55f);
        }

        /// <summary>Whether this point is on planks: the bridge or the docks.</summary>
        public bool OnWood(Vector3 at)
        {
            foreach (var b in Wood)
                if (b.Contains(new Vector3(at.x, b.center.y, at.z))) return true;
            return false;
        }
    }
}

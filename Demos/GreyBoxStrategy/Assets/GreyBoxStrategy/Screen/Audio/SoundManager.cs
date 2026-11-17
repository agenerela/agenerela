using System.Collections.Generic;
using GreyBoxStrategy.Replay;
using UnityEngine;

namespace GreyBoxStrategy.Realm
{
    /// <summary>
    /// The game's sound: one call plays a cue by name (<see cref="Play"/>), at the level its bus and the master give
    /// (<see cref="SoundCues"/>). It lives across the scene's reloads, so leaving a game never cuts a sound short.
    /// </summary>
    /// <remarks>
    /// <para>Nothing plays when the window has no focus. Headless runs and tests make no sound and need no audio device:
    /// in batch mode no manager is made, and <see cref="Play"/> with no manager does nothing.</para>
    /// <para>A fourth bus, the voice, is in the table for the text-to-speech work: it plays through <see cref="Play"/>
    /// like any other, at the Voices volume.</para>
    /// </remarks>
    public sealed class SoundManager : MonoBehaviour
    {
        private const int Voices = 8;

        private static SoundManager instance;
        private readonly List<AudioSource> sources = new List<AudioSource>();
        private readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        private readonly Dictionary<string, float> lastPlayed = new Dictionary<string, float>();
        private AudioSource music;
        private SoundCue musicCue;
        private GameSettings settings = new GameSettings();
        private int next;

        /// <summary>True when sound plays: a manager exists (never in batch mode or tests).</summary>
        public static bool Active => instance != null;

        /// <summary>The settings' volumes, now and for every sound after. Called when the settings load and change.</summary>
        public static void Apply(GameSettings s)
        {
            if (s == null) return;
            if (instance == null) Create();
            if (instance == null) return;
            instance.settings = s.Clone();
            if (instance.music != null && instance.musicCue != null) instance.music.volume = SoundCues.Gain(instance.musicCue, instance.settings);
        }

        /// <summary>Plays a cue by its name from <see cref="SoundCues"/>. A cue with no sound yet, or no manager, plays nothing.</summary>
        public static void Play(string cue) => instance?.PlayCue(SoundCues.Named(cue));

        /// <summary>Starts a looping cue on the music bus, or keeps it if it is already playing; null stops the music.</summary>
        public static void Music(string cue) => instance?.PlayMusic(SoundCues.Named(cue));

        private static void Create()
        {
            if (Application.isBatchMode || !Application.isPlaying) return;
            var go = new GameObject("Painted Realm sound");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<SoundManager>();
        }

        private void Awake()
        {
            for (int i = 0; i < Voices; i++) sources.Add(NewSource());
            music = NewSource();
            music.loop = true;
            // The scene has no listener of its own: the manager brings one, kept with it across reloads.
            if (FindAnyObjectByType<AudioListener>() == null) gameObject.AddComponent<AudioListener>();
            AudioListener.pause = !Application.isFocused;
        }

        private AudioSource NewSource()
        {
            var s = gameObject.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.spatialBlend = 0;
            return s;
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        // Nothing plays when the window has no focus; what was playing waits for it.
        private void OnApplicationFocus(bool focus) => AudioListener.pause = !focus;

        private AudioClip Clip(SoundCue cue)
        {
            if (cue?.File == null) return null;
            if (!clips.TryGetValue(cue.File, out var clip))
            {
                clip = Resources.Load<AudioClip>("Sounds/" + cue.File);
                clips[cue.File] = clip;
                if (clip == null) Debug.LogWarning($"Painted Realm: the sound '{cue.Name}' names Resources/Sounds/{cue.File}, which is not there.");
            }
            return clip;
        }

        private void PlayCue(SoundCue cue)
        {
            if (cue == null || cue.Loop) return;
            var clip = Clip(cue);
            if (clip == null) return;
            float now = Time.unscaledTime;
            if (lastPlayed.TryGetValue(cue.Name, out float last) && now - last < cue.MinGap) return;
            lastPlayed[cue.Name] = now;
            float gain = SoundCues.Gain(cue, settings);
            if (gain <= 0.001f) return;
            // The next free source, or the oldest one when all eight are busy.
            AudioSource s = null;
            for (int i = 0; i < sources.Count && s == null; i++) { var c = sources[(next + i) % sources.Count]; if (!c.isPlaying) s = c; }
            s = s ?? sources[next % sources.Count];
            next = (next + 1) % sources.Count;
            s.pitch = 1 + Random.Range(-cue.PitchJitter, cue.PitchJitter);
            s.PlayOneShot(clip, gain);
        }

        private void PlayMusic(SoundCue cue)
        {
            if (cue == musicCue && music.isPlaying) return;
            music.Stop();
            musicCue = cue;
            var clip = Clip(cue);
            if (clip == null) return;
            music.clip = clip;
            music.volume = SoundCues.Gain(cue, settings);
            music.Play();
        }
    }
}

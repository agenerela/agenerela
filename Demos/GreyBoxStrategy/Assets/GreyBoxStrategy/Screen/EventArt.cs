using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Agenerela;
using Agenerela.Providers;
using GreyBoxStrategy.Replay;
using UnityEngine;

namespace GreyBoxStrategy.Realm
{
    /// <summary>
    /// Live pictures for events (#20): when an event fires, from the random table or the storyteller, a picture of its
    /// own story is painted in the background by the backend Settings → Painted art names, and the event card shows it
    /// when it arrives. Until then, and when it never comes, the card shows the art pack's picture for the event's
    /// kind (<see cref="ArtCatalogue.Event"/>), so it is always readable without it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Pictures are cached by the event's text, the title and the story, in one folder for every world
    /// (<c>&lt;persistentDataPath&gt;/painted-realm-art/events</c>): the same words are the same request, seed included,
    /// so a story told twice is painted once. Nothing waits on a picture. They are painted one at a time, in the order
    /// the events fire, behind any of the court still being painted on the same model.
    /// </para>
    /// <para>
    /// The storyteller's title and story are a model's words, so they are cleaned and capped
    /// (<see cref="ImagePromptBuilder.Untrusted"/>) before they reach the image model. On the image server each picture
    /// is freed after it is made (<c>FreeAfterEach</c>), since that card also serves the rulers' model.
    /// </para>
    /// </remarks>
    public static class EventArt
    {
        /// <summary>The art pack's event size, 16:9, a multiple of 16 for every backend.</summary>
        public const int Width = 1024, Height = 576;

        /// <summary>The pack's scene line (<c>ArtSource/art-pack/pack.json</c>), word for word.</summary>
        public const string Scene = "A wide scene seen from a middle distance, small figures.";

        public const int TitleLength = 80, StoryLength = 240;

        /// <summary>On when Settings has Event pictures on and Painted art not Off; the director sets it.</summary>
        public static bool Enabled { get; set; } = true;

        /// <summary>Raised on the main thread when a picture arrives, with its key (<see cref="KeyOf"/>).</summary>
        public static event Action<string> Arrived;

        /// <summary>For the Unity CLI and the log: what the event painter has done this world.</summary>
        public static string Report => $"{made} painted, {fromCache} from the cache, {failed} failed, {queue.Count} waiting" +
                                       (current != null ? ", one being painted" : "") + (stoppedBecause != null ? "; stopped: " + stoppedBecause : "") +
                                       (lastSeconds > 0 ? $"; the last took {lastSeconds:0.0} s" : "");

        public static string CacheFolder => Path.Combine(Application.persistentDataPath, "painted-realm-art", "events");

        private static readonly Dictionary<string, Texture2D> Painted = new Dictionary<string, Texture2D>(StringComparer.Ordinal);
        private static readonly Queue<(string Key, ImageRequest Request)> queue = new Queue<(string, ImageRequest)>();
        private static readonly HashSet<string> waiting = new HashSet<string>(StringComparer.Ordinal);
        private static string current, stoppedBecause;
        private static int generation, made, fromCache, failed;
        private static float lastSeconds;
        private static CancellationTokenSource cancel;
        private static IImageProvider serverForEvents;

        // ---- the request: plain functions of the event's words, so tests need no scene ----

        /// <summary>The event's title: the storyteller's, or its kind's own for a table event.</summary>
        public static string TitleOf(EventRecord r) =>
            r == null ? "" : r.Title ?? EventRules.Template(r.Template)?.FallbackTitle ?? (r.Template ?? "").Replace('_', ' ');

        /// <summary>The event's story: the storyteller's, or its kind's own for a table event, with no names in it.</summary>
        public static string StoryOf(EventRecord r)
        {
            if (r == null) return "";
            if (!string.IsNullOrWhiteSpace(r.Story)) return r.Story;
            string fallback = EventRules.Template(r.Template)?.FallbackStory ?? "";
            return fallback.Replace("{who}", "a realm").Replace("{where}", "the land");
        }

        /// <summary>The cache's key for an event: its text. The same words, the same picture.</summary>
        public static string KeyOf(EventRecord r) => r == null ? null : TitleOf(r) + "\n" + StoryOf(r);

        /// <summary>The picture's request: the pack's style and scene lines, then the event's own words, cleaned.</summary>
        public static ImageRequest RequestFor(EventRecord r)
        {
            string prompt = new ImagePromptBuilder(RulerArtPrompts.Style)
                .Detail(Scene)
                .Detail("It shows this event from a chronicle:")
                .Untrusted(TitleOf(r), TitleLength)
                .Untrusted(StoryOf(r), StoryLength)
                .Exclude(RulerArtPrompts.Exclusions)
                .Build();
            return new ImageRequest { Prompt = prompt, Width = Width, Height = Height, Seed = ArtCatalogue.Hash("event:" + KeyOf(r)) % 1000000007L };
        }

        // ---- the painter ----

        /// <summary>The picture painted for <paramref name="r"/>, or null while none has arrived.</summary>
        public static Texture2D PictureFor(EventRecord r) => r != null && Painted.TryGetValue(KeyOf(r), out var t) && t != null ? t : null;

        /// <summary>The picture painted for the event whose key is <paramref name="key"/>, or null.</summary>
        public static Texture2D PictureFor(string key) => key != null && Painted.TryGetValue(key, out var t) && t != null ? t : null;

        /// <summary>Whether a picture for <paramref name="r"/> is being painted or waits its turn.</summary>
        public static bool Painting(EventRecord r) => r != null && waiting.Contains(KeyOf(r));

        /// <summary>A new world: what waits is dropped and the last world's pictures are let go (the files stay cached).</summary>
        public static void Begin(int worldSeed)
        {
            Stop();
            foreach (var t in Painted.Values) if (t != null) UnityEngine.Object.Destroy(t);
            Painted.Clear();
            made = fromCache = failed = 0;
            lastSeconds = 0;
            stoppedBecause = null;
        }

        /// <summary>Stops painting: the picture being made is cancelled and nothing waiting is asked for.</summary>
        public static void Stop()
        {
            generation++;
            cancel?.Cancel();
            cancel?.Dispose();
            cancel = null;
            queue.Clear();
            waiting.Clear();
            current = null;
            (serverForEvents as IDisposable)?.Dispose();
            serverForEvents = null;
        }

        /// <summary>An event fired: its picture is asked for, unless it is here, waiting, or painting is off.</summary>
        public static void Request(EventRecord r)
        {
            if (!Enabled || r == null || RulerArtServer.Backend == ArtBackend.Off) return;
            string key = KeyOf(r);
            if (Painted.ContainsKey(key) || waiting.Contains(key)) return;
            waiting.Add(key);
            queue.Enqueue((key, RequestFor(r)));
            if (current == null) Pump(generation);
        }

        private static async void Pump(int run)
        {
            if (cancel == null) cancel = new CancellationTokenSource();
            var token = cancel.Token;
            var cache = new ImageFileCache(CacheFolder);
            while (run == generation && queue.Count > 0)
            {
                var (key, request) = queue.Dequeue();
                current = key;
                var backend = stoppedBecause == null ? Backend(out string why) : null;
                string keyName = RulerArtServer.Backend == ArtBackend.Local ? "sdcpp" : "comfyui";
                var source = new CachedImageProvider(backend, cache, backend?.Name ?? keyName);
                var clock = System.Diagnostics.Stopwatch.StartNew();
                ImageResult result;
                try
                {
                    result = await source.GenerateAsync(request, token);
                }
                catch (OperationCanceledException) { return; }
                catch (ObjectDisposedException) { return; }
                catch (ArgumentException e) { result = ImageResult.Failed(ProviderErrorKind.Rejected, keyName, e.Message); }
                if (run != generation) return;
                waiting.Remove(key);
                current = null;
                if (result.Succeeded && ImageTextures.ToTexture(result.Png, true, "event picture") is Texture2D t)
                {
                    Painted[key] = t;
                    if (result.FromCache) fromCache++;
                    else { made++; lastSeconds = (float)clock.Elapsed.TotalSeconds; }
                    if (!result.FromCache) Debug.Log($"Painted Realm: an event's picture painted ({RulerArtServer.Backend}) in {lastSeconds:0.0} s: {key.Split('\n')[0]}");
                    Arrived?.Invoke(key);
                }
                else
                {
                    failed++;
                    // A backend that failed once fails every later picture just as slowly: ask only the cache from now on.
                    if (backend != null) stoppedBecause = $"{result.Error}: {result.ErrorMessage}";
                }
            }
            if (run == generation) current = null;
        }

        // The backend for events: the local model the court uses, or a server provider of its own that frees the card
        // after each picture.
        private static IImageProvider Backend(out string why)
        {
            why = null;
            switch (RulerArtServer.Backend)
            {
                case ArtBackend.Local: return RulerArtServer.LocalProvider(out why);
                case ArtBackend.Server: return serverForEvents ?? (serverForEvents = RulerArtServer.ServerProvider(out why, freeAfterEach: true));
                default: why = "off"; return null;
            }
        }
    }
}

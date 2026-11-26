using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Agenerela;
using Agenerela.Providers;
using Agenerela.Providers.ComfyUI;
using Agenerela.Providers.SdCpp;
using GreyBoxStrategy.Chart;
using GreyBoxStrategy.Replay;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GreyBoxStrategy.Realm
{
    /// <summary>
    /// The generated court (DR-025): at New game, behind the loading card, each ruler's portrait and banner from the
    /// backend Settings → Painted art names, or from this world's cache, or else the art pack's. Escape skips what is
    /// left. On the image server the whole court is painted behind the card, then the server is freed for the decision
    /// model. Local paints only your own ruler behind the card and the rest in the background while you play, since
    /// its model sits on this machine's card and the rulers' model on the server (DR-028).
    /// </summary>
    public sealed partial class ReplayDirector
    {
        /// <summary>
        /// The rulers of the world just made, for the image prompts: name, colour, slot, and persona where one rules.
        /// Your own realm first, so a backend that paints one at a time paints yours before any rival's.
        /// </summary>
        private List<RulerSubject> CourtOf(string you)
        {
            var w = world;
            var subjects = new List<RulerSubject>();
            for (int i = 0; i < w.Countries.Count; i++)
            {
                var c = w.Countries[i];
                string persona = game?.Players != null && game.Players.TryGetValue(c.Id, out var p) ? p?.Persona : null;
                // Backstory: the loremaster's line about this ruler (#20, world lore), checked; null when none.
                var s = new RulerSubject { Id = c.Id, Name = c.Name ?? c.Id, Persona = persona, Colour = ChartPalette.Owner(i), Slot = i, Backstory = c.RulerLine };
                subjects.Add(s);
            }
            return subjects.OrderBy(s => s.Id == you ? 0 : 1).ToList();
        }

        /// <summary>Whether the last <see cref="PaintCourt"/> made a picture on the image server, which unloads the decision model.</summary>
        private bool courtPainted;

        // The rest of a Local court, painted while the game plays; cancelled when another world begins.
        private CancellationTokenSource backgroundCourt;

        private void StopBackgroundCourt()
        {
            backgroundCourt?.Cancel();
            backgroundCourt?.Dispose();
            backgroundCourt = null;
        }

        /// <summary>Settings → Painted art: what paints from now on. Moving off Local frees the local model's memory.</summary>
        private void UseArtBackend(ArtBackend backend)
        {
            if (RulerArtServer.Backend == ArtBackend.Local && backend != ArtBackend.Local) { StopBackgroundCourt(); RulerArtServer.FreeLocal(); }
            RulerArtServer.Backend = backend;
        }

        private IEnumerator PaintCourt(LoadingCard loading, int worldSeed)
        {
            courtPainted = false;
            StopBackgroundCourt();
            RulerArtServer.Clear();
            // A generated world reloads the scene and paints before this director loads its settings (StartScreen):
            // the backend the menu set, kept across the reload, is the one that counts.
            UseArtBackend(settings?.Art ?? RulerArtServer.Backend);
            if (RulerArtServer.Backend == ArtBackend.Off)
            {
                RulerArtServer.Report("off in Settings: the art pack's pictures");
                yield break;
            }
            var backend = RulerArtServer.Provider(out string why);
            bool local = backend is SdCppImageProvider;
            var cache = new ImageFileCache(RulerArtServer.CacheFolder(worldSeed));
            var maker = new RulerArtMaker(backend, cache) { Accept = png => !RulerArtServer.LooksFramed(png) };
            string you = earlyRule?.You;
            var subjects = CourtOf(you);
            // Local: your ruler behind the card (the first seat's when you watch); every other in the background.
            var front = local ? subjects.Take(1).ToList() : subjects;
            var rest = local ? subjects.Skip(1).ToList() : new List<RulerSubject>();
            string line = local ? "Painting your ruler on this machine..." : "Painting the court...";
            float progress = 0;
            using (var skip = new CancellationTokenSource())
            {
                Task<RulerArtSet> painting = Paint(maker, worldSeed, front, (l, f) => { line = l; progress = f; }, skip.Token);
                while (!painting.IsCompleted)
                {
                    var kb = Keyboard.current;
                    if (!skip.IsCancellationRequested && kb != null && kb.escapeKey.wasPressedThisFrame) skip.Cancel();
                    loading.Set(skip.IsCancellationRequested ? "Skipping: the art pack's pictures hang instead..." : $"{line}. Esc: skip", 0.68f + 0.26f * progress);
                    yield return null;
                }
                var set = painting.IsFaulted ? null : painting.Result;
                if (painting.IsFaulted) Debug.LogWarning("Painted Realm: the court could not be painted: " + painting.Exception.GetBaseException().Message);
                if (backend == null && set != null) set.StoppedBecause = why;
                RulerArtServer.Use(set);
                Debug.Log($"Painted Realm: court art for world {worldSeed} ({RulerArtServer.Backend}): {RulerArtServer.LastReport} (cache {cache.Folder})");
                if (backend is ComfyUIProvider server && set != null && set.Made > 0)
                {
                    courtPainted = true;
                    // Give the card back to the decision model before the first turn.
                    var freeing = Free(server);
                    float until = Time.realtimeSinceStartup + 10f;
                    while (!freeing.IsCompleted && Time.realtimeSinceStartup < until) yield return null;
                }
                bool skipped = set == null || set.StoppedBecause != null;
                if (local && rest.Count > 0) PaintRestInBackground(maker, worldSeed, rest, set, skipped);
            }
        }

        // The rest of a Local court: never waited on. Each picture hangs as it comes; a skip at the card leaves the rest
        // to the cache alone.
        private async void PaintRestInBackground(RulerArtMaker frontMaker, int worldSeed, List<RulerSubject> rest, RulerArtSet front, bool cacheOnly)
        {
            backgroundCourt = new CancellationTokenSource();
            var token = backgroundCourt.Token;
            var backend = cacheOnly ? null : RulerArtServer.Provider(out _);
            var maker = new RulerArtMaker(backend, new ImageFileCache(RulerArtServer.CacheFolder(worldSeed)), "sdcpp")
            {
                Accept = frontMaker.Accept,
                Painted = (realm, flag, png) => { if (!token.IsCancellationRequested) RulerArtServer.Hang(realm, flag, png); },
            };
            try
            {
                var set = await maker.MakeAsync(worldSeed, rest, null, float.MaxValue, token);
                if (token.IsCancellationRequested) return;
                RulerArtServer.Report($"yours: {front}; the rest in the background: {set}");
                Debug.Log($"Painted Realm: the rest of world {worldSeed}'s court, painted in the background: {set}");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("Painted Realm: the court's background painting stopped: " + e.Message);
            }
        }

        private void OnApplicationQuit()
        {
            StopBackgroundCourt();
            RulerArtServer.FreeLocal();
        }

        private static async Task<RulerArtSet> Paint(RulerArtMaker maker, int seed, List<RulerSubject> subjects, System.Action<string, float> progress, CancellationToken skip) =>
            await maker.MakeAsync(seed, subjects, progress, RulerArtServer.BudgetSeconds, skip);

        private static async Task<bool> Free(ComfyUIProvider backend) => await backend.FreeAsync(CancellationToken.None);
    }
}

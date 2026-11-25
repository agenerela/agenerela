using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Agenerela;
using GreyBoxStrategy.Chart;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GreyBoxStrategy.Realm
{
    /// <summary>
    /// The generated court (DR-025): at New game, behind the loading card, each ruler's portrait and banner from the
    /// image server, or from this world's cache, or else the art pack's. Escape skips what is left.
    /// </summary>
    public sealed partial class ReplayDirector
    {
        /// <summary>The rulers of the world just made, for the image prompts: name, colour, slot, and persona where one rules.</summary>
        private List<RulerSubject> CourtOf()
        {
            var w = world;
            var subjects = new List<RulerSubject>();
            for (int i = 0; i < w.Countries.Count; i++)
            {
                var c = w.Countries[i];
                string persona = game?.Players != null && game.Players.TryGetValue(c.Id, out var p) ? p?.Persona : null;
                // Backstory: the loremaster's line about this ruler (#20, world lore), checked; null when none.
                subjects.Add(new RulerSubject { Id = c.Id, Name = c.Name ?? c.Id, Persona = persona, Colour = ChartPalette.Owner(i), Slot = i, Backstory = c.RulerLine });
            }
            return subjects;
        }

        /// <summary>Whether the last <see cref="PaintCourt"/> made a picture on the image server, which unloads the decision model.</summary>
        private bool courtPainted;

        private IEnumerator PaintCourt(LoadingCard loading, int worldSeed)
        {
            courtPainted = false;
            RulerArtServer.Clear();
            var backend = RulerArtServer.Provider(out string why);
            var cache = new ImageFileCache(RulerArtServer.CacheFolder(worldSeed));
            var maker = new RulerArtMaker(backend, cache) { Accept = png => !RulerArtServer.LooksFramed(png) };
            var subjects = CourtOf();
            string line = "Painting the court...";
            float progress = 0;
            using (var skip = new CancellationTokenSource())
            {
                Task<RulerArtSet> painting = Paint(maker, worldSeed, subjects, (l, f) => { line = l; progress = f; }, skip.Token);
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
                Debug.Log($"Painted Realm: court art for world {worldSeed}: {RulerArtServer.LastReport} (cache {cache.Folder})");
                if (backend != null && set != null && set.Made > 0)
                {
                    courtPainted = true;
                    // Give the card back to the decision model before the first turn.
                    var freeing = Free(backend);
                    float until = Time.realtimeSinceStartup + 10f;
                    while (!freeing.IsCompleted && Time.realtimeSinceStartup < until) yield return null;
                }
            }
        }

        private static async Task<RulerArtSet> Paint(RulerArtMaker maker, int seed, List<RulerSubject> subjects, System.Action<string, float> progress, CancellationToken skip) =>
            await maker.MakeAsync(seed, subjects, progress, RulerArtServer.BudgetSeconds, skip);

        private static async Task<bool> Free(Agenerela.Providers.ComfyUI.ComfyUIProvider backend) => await backend.FreeAsync(CancellationToken.None);
    }
}

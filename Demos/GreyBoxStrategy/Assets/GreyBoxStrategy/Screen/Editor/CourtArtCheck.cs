using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Agenerela;
using GreyBoxStrategy.Chart;
using UnityEditor;
using UnityEngine;

namespace GreyBoxStrategy.Realm.Editor
{
    /// <summary>
    /// Paints the court of generated worlds headless, through the same prompts, server, cache and fallback the loading
    /// card uses (DR-025), for a contact sheet. Slot 0 is the player's realm (no persona), the rest wear the personas in
    /// seat order, as New game seats them.
    /// </summary>
    /// <remarks>
    /// <c>Unity -batchmode -projectPath Demos/GreyBoxStrategy -executeMethod GreyBoxStrategy.Realm.Editor.CourtArtCheck.RunFromCommandLine
    /// -courtSeeds 7,21 -courtOut &lt;folder&gt;</c>. The server comes from <c>COMFY_URL</c> and <c>COMFY_API_TOKEN</c>,
    /// or in the Editor from the main checkout's <c>tools/comfyui/.env</c>. It writes each world's PNGs into
    /// <c>&lt;folder&gt;/&lt;seed&gt;/</c> as <c>&lt;realm&gt;-portrait.png</c> and <c>&lt;realm&gt;-flag.png</c>, and
    /// <c>court.json</c> with every prompt and the report. The pictures are also kept in the game's own cache, so a New
    /// game on the same seed finds them. With <c>-courtLore &lt;model&gt;</c> the loremaster names the world first (#20),
    /// through the game's provider, cache and checks, and <c>court.json</c> carries the world's name, its backstory and
    /// each ruler's line, which the portraits read.
    /// </remarks>
    public static class CourtArtCheck
    {
        public static void RunFromCommandLine()
        {
            var seeds = (Arg("-courtSeeds") ?? "7").Split(',').Select(s => int.Parse(s.Trim(), CultureInfo.InvariantCulture)).ToList();
            string folder = Arg("-courtOut") ?? Path.Combine(Application.temporaryCachePath, "court-art");
            Run(seeds, folder, Arg("-courtLore"));
        }

        private static async void Run(List<int> seeds, string folder, string loreModel)
        {
            int code = 0;
            try
            {
                foreach (int seed in seeds) await Paint(seed, Path.Combine(folder, seed.ToString(CultureInfo.InvariantCulture)), loreModel);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                code = 1;
            }
            EditorApplication.Exit(code);
        }

        private static async Task Paint(int seed, string folder, string loreModel)
        {
            var made = MapGenerator.Generate(seed, 5);
            if (!made.Playable) throw new InvalidOperationException($"seed {seed}: no playable world");
            var personas = Enumerable.Range(0, made.Map.Countries.Count).Select(i => i == 0 ? null : Personas.All[i % 5].Id).ToList();
            var tableNames = made.Map.Countries.Select(c => c.Name).ToList();
            string loreReport = loreModel != null ? await Name(made.Map, seed, loreModel, personas) : null;
            var subjects = new List<RulerSubject>();
            for (int i = 0; i < made.Map.Countries.Count; i++)
            {
                var c = made.Map.Countries[i];
                subjects.Add(new RulerSubject { Id = c.Id, Name = c.Name ?? c.Id, Persona = personas[i], Colour = ChartPalette.Owner(i), Slot = i, Backstory = c.RulerLine });
            }

            var backend = RulerArtServer.ServerProvider(out string why);
            if (backend == null) Debug.LogWarning($"Court art check: no image server ({why}); the cache alone is used.");
            var maker = new RulerArtMaker(backend, new ImageFileCache(RulerArtServer.CacheFolder(seed))) { Accept = png => !RulerArtServer.LooksFramed(png) };
            var set = await maker.MakeAsync(seed, subjects, (line, f) => Debug.Log($"Court art check, seed {seed}: {line} ({f:P0})"), RulerArtServer.BudgetSeconds, CancellationToken.None);
            if (backend != null) await backend.FreeAsync(CancellationToken.None);

            Directory.CreateDirectory(folder);
            var json = new StringBuilder("{\n  \"seed\": " + seed + ",\n  \"report\": " + Quote(set.ToString()) + ",\n");
            if (loreModel != null)
                json.Append("  \"world\": ").Append(Quote(made.Map.Name)).Append(",\n  \"backstory\": ").Append(Quote(made.Map.Backstory))
                    .Append(",\n  \"lore\": ").Append(Quote(loreReport)).Append(",\n");
            json.Append("  \"realms\": [\n");
            for (int i = 0; i < subjects.Count; i++)
            {
                var s = subjects[i];
                if (set.Portraits.TryGetValue(s.Id, out var p)) File.WriteAllBytes(Path.Combine(folder, s.Id + "-portrait.png"), p);
                if (set.Flags.TryGetValue(s.Id, out var f)) File.WriteAllBytes(Path.Combine(folder, s.Id + "-flag.png"), f);
                json.Append("    {\"id\": ").Append(Quote(s.Id)).Append(", \"name\": ").Append(Quote(s.Name)).Append(", \"tableName\": ").Append(Quote(tableNames[i]))
                    .Append(", \"line\": ").Append(Quote(s.Backstory)).Append(", \"persona\": ").Append(Quote(s.Persona ?? "(you)"))
                    .Append(", \"portrait\": ").Append(Quote(RulerArtPrompts.Portrait(seed, s).Prompt))
                    .Append(", \"flag\": ").Append(Quote(RulerArtPrompts.Flag(seed, s).Prompt)).Append(i + 1 < subjects.Count ? "},\n" : "}\n");
            }
            json.Append("  ]\n}\n");
            File.WriteAllText(Path.Combine(folder, "court.json"), json.ToString());
            Debug.Log($"Court art check, seed {seed}: {set} -> {folder}");
        }

        // The loremaster through the game's provider, cache and checks, as New game asks it (ReplayDirector.Lore).
        private static async Task<string> Name(WorldMap world, int seed, string model, List<string> personas)
        {
            var cache = new WorldLoreCache(RulerArtServer.CacheFolder(seed));
            string key = WorldLoreCache.Key(world, model);
            var proposal = cache.Read(key);
            string from = "the cache";
            if (proposal == null)
            {
                from = "the model";
                var watch = System.Diagnostics.Stopwatch.StartNew();
                var master = new WorldLoremaster(world, Gateway.Provider(model), personas);
                using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(180)))
                    proposal = await master.AskAsync(seed, cts.Token);
                var t = master.LastResult?.Telemetry;
                Debug.Log($"Court art check, seed {seed}: the loremaster {(proposal != null ? "answered" : "gave nothing")} in {watch.Elapsed.TotalSeconds:0.0} s" +
                          (t != null ? $", {t.PromptTokens} prompt and {t.CompletionTokens} reply tokens" : "") + $"; its reply: {master.LastResult?.Reply?.Text}");
                if (proposal != null) cache.Write(key, proposal);
            }
            if (proposal == null) return "no lore: the loremaster gave nothing";
            var lore = LoreRules.Check(world, proposal);
            LoreRules.Apply(world, lore);
            return $"from {from}: {lore.Summary(world.Countries.Count)}";
        }

        private static string Quote(string s) => Newtonsoft.Json.JsonConvert.ToString(s ?? "");

        private static string Arg(string name)
        {
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }
    }
}

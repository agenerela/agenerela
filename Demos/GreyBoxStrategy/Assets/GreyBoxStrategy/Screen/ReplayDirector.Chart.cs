using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using GreyBoxStrategy.Chart;
using GreyBoxStrategy.Replay;
using UnityEngine;
using UnityEngine.UIElements;

namespace GreyBoxStrategy.Realm
{
    /// <summary>
    /// A generated world on screen, the drawn-chart spike (rules v2, R2): the world is made from a
    /// seed, a game is played on it at once by the rules' scripted rulers, and the screen replays
    /// that game over a chart drawn by code at the screen's size. Started from New game or the dev
    /// menu; the Classic board and its painting are untouched.
    /// </summary>
    public sealed partial class ReplayDirector
    {
        /// <summary>Set before the scene reloads: the next start shows this world instead of the menu.</summary>
        private static GameSetup pendingWorld;

        private WorldMap world;
        private List<MapLabel> mapLabels;
        private float[] labelSizes;
        private float labelZoom = -1;
        private float zoomMin = 1.6f, zoomMax = 7f;

        /// <summary>The world being shown, or null on the Classic board.</summary>
        public WorldMap World => world;

        /// <summary>For the menu, the dev menu and the Unity CLI: reload the scene and watch scripted rulers play a generated world.</summary>
        public void StartGeneratedWatch(MapSize size, LandShape shape, int seed, int countries = 0, bool writs = false, string victory = GameSetup.ConquestPreset)
        {
            if (countries <= 0) countries = MapSettings.DefaultCountries(size);
            pendingWorld = writs ? GameSetup.Writs(seed, GameSetup.GeneratedMapId, victory) : new GameSetup { Seed = seed, Map = GameSetup.GeneratedMapId };
            pendingWorld.Countries = countries;
            pendingWorld.Generator = new MapSettings { Size = size, Shape = shape };
            pendingWorld.Validate();
            LeaveToMenu();
        }

        /// <summary>Set before the scene reloads: the next generated world is played live, by this model ("scripted" for stand-ins through the agents).</summary>
        private static string pendingLiveModel;
        private static int pendingLiveTurns;

        /// <summary>
        /// For the menu, the dev menu and the Unity CLI: a generated world played live, the writs turn with model
        /// rulers (checked first, as on the Classic board) or stand-ins through the same agents.
        /// </summary>
        public void StartGeneratedLive(MapSize size, int seed, string model, string victory = GameSetup.ConquestPreset, int turns = 0, string arm = null) =>
            StartCoroutine(GeneratedLive(size, seed, model, victory, turns, arm));

        private static string pendingLiveArm;

        private IEnumerator GeneratedLive(MapSize size, int seed, string model, string victory, int turns, string arm)
        {
            pendingLiveArm = arm;
            if (model != "scripted")
            {
                string why = LiveGame.WhyNotNow();
                if (why != null) { ui?.ShowMenuStatus(why); yield break; }
                ui?.ShowMenuStatus("Asking the server...", true);
                string serverWhy = null;
                yield return Gateway.CheckServer(w => serverWhy = w);
                if (serverWhy != null) { ui?.ShowMenuStatus(serverWhy); yield break; }
            }
            pendingLiveModel = model;
            pendingLiveTurns = turns;
            StartGeneratedWatch(size, LandShape.Continent, seed, 0, true, victory);
        }

        /// <summary>Set before the scene reloads: the next start plays the writs turn on the Classic board with scripted rulers.</summary>
        private static GameSetup pendingClassic;

        /// <summary>For the menu, the dev menu and the Unity CLI: reload the scene and watch scripted rulers play the writs turn on the Classic board.</summary>
        public void StartWritsWatch(int seed, string victory = GameSetup.ConquestPreset)
        {
            pendingClassic = GameSetup.Writs(seed, GameSetup.ClassicMapId, victory);
            LeaveToMenu();
        }

        /// <summary>The Classic board's writs game, played by scripted rulers off the main thread behind the loading card.</summary>
        private IEnumerator LoadClassicWrits()
        {
            var setup = pendingClassic;
            pendingClassic = null;
            var loading = LoadingCard.Show(theme, style, $"The writs turn on the Classic board, seed {setup.Seed}");
            loading.Set("The rulers play the game...", 0.3f);
            var playing = System.Threading.Tasks.Task.Run(() =>
            {
                var mapDef = setup.BuildMap();
                var rules = setup.BuildRules(mapDef);
                var rec = ScriptedWorldGame.Play(setup, mapDef, rules);
                scripted = (setup, mapDef, rules);
                return (rec, ReplayBuilder.Build(rec, mapDef, rules));
            });
            while (!playing.IsCompleted) yield return null;
            loading.Close();
            if (playing.IsFaulted) { Debug.LogException(playing.Exception.GetBaseException()); yield break; }
            (sourceRecording, game) = playing.Result;
            prebuilt = true;
        }

        /// <summary>True when a game was built before the screen started (the Classic writs game): no menu, no recording asset.</summary>
        private bool prebuilt;

        /// <summary>How long the generator may look for a fair world before it takes the best one found.</summary>
        public static readonly System.TimeSpan GeneratorBudget = System.TimeSpan.FromSeconds(12);

        /// <summary>For the Unity CLI and the frames: what the last generated world cost, step by step.</summary>
        public string LoadReport { get; private set; }

        /// <summary>
        /// Makes the queued world, plays its game with the rules' scripted rulers and prepares its chart
        /// on a background thread, with a loading card saying what it is doing; then draws the chart on
        /// the GPU (or the CPU where the GPU cannot) and builds the map from it.
        /// </summary>
        private IEnumerator LoadGeneratedWorld()
        {
            var setup = pendingWorld;
            pendingWorld = null;
            var size = setup.Generator?.Size ?? MapSize.Medium;
            var loading = LoadingCard.Show(theme, style, $"Making a {size.ToString().ToLowerInvariant()} world from seed {setup.MapSeed ?? setup.Seed}");
            var watch = System.Diagnostics.Stopwatch.StartNew();
            GenerationProgress last = null;
            var making = System.Threading.Tasks.Task.Run(() => MapGenerator.Generate(setup.MapSeed ?? setup.Seed, setup.Countries, setup.Generator,
                progress: p => last = p, budget: GeneratorBudget));
            while (!making.IsCompleted)
            {
                var p = last;
                loading.Set(p == null ? "Raising the land..." : $"Raising the land: {p.Line}", p == null ? 0.05f : 0.05f + 0.55f * Mathf.Min(1, p.ElapsedMilliseconds / (float)GeneratorBudget.TotalMilliseconds));
                yield return null;
            }
            if (making.IsFaulted || !making.Result.Playable)
            {
                string why = making.IsFaulted ? making.Exception.GetBaseException().Message : string.Join("; ", making.Result.Problems.Take(2));
                Debug.LogWarning("Painted Realm: no playable world: " + why);
                loading.Close();
                yield break; // the menu, on the Classic board, says nothing worse happened
            }
            long made = watch.ElapsedMilliseconds; watch.Restart();
            world = making.Result.Map;

            bool gpu = ChartGpu.Supported;
            int chartWidth = ChartImage.WidthFor(Screen.width);
            loading.Set("Playing the game and drawing the chart...", 0.65f);
            string liveModel = pendingLiveModel;
            pendingLiveModel = null;
            MapDefinition liveMap = null; GameRules liveRules = null;
            var preparing = System.Threading.Tasks.Task.Run(() =>
            {
                var mapDef = world.ToDefinition();
                var rules = setup.BuildRules(mapDef);
                if (liveModel != null)
                {
                    // Played live: only the chart is made here; the game starts once the world is drawn.
                    liveMap = mapDef; liveRules = rules;
                    return ((Recording)null, (ReplayGame)null, ChartImage.Make(world, chartWidth, setup.Seed, gpu));
                }
                var rec = ScriptedWorldGame.Play(setup, mapDef, rules);
                scripted = (setup, mapDef, rules);
                var replay = ReplayBuilder.Build(rec, mapDef, rules);
                var chart = ChartImage.Make(world, chartWidth, setup.Seed, gpu);
                return (rec, replay, chart);
            });
            while (!preparing.IsCompleted) { yield return null; }
            if (preparing.IsFaulted)
            {
                Debug.LogException(preparing.Exception.GetBaseException());
                world = null;
                loading.Close();
                yield break;
            }
            long prepared = watch.ElapsedMilliseconds; watch.Restart();
            var (recording, replayed, chartImage) = preparing.Result;
            sourceRecording = recording;
            game = replayed;
            if (liveModel != null)
            {
                live.Configure(true, setup.Seed, pendingLiveTurns, liveModel == "scripted" ? null : liveModel);
                live.BeginWrits(liveMap, liveRules, setup.Seed, liveModel == "scripted", pendingLiveArm);
                pendingLiveArm = null;
                if (live.Error != null) Debug.LogWarning("Painted Realm: the live game could not start: " + live.Error);
                game = live.Replay();
            }
            loading.Set("Laying out the chart...", 0.95f);
            yield return null;
            CountryLook.UseWorld(world);
            map.BuildChart(chartImage, world, setup.Seed);
            chartSeed = setup.Seed;
            long drawn = watch.ElapsedMilliseconds;
            mapLabels = ChartLabels.For(world);
            labelSizes = new float[world.Provinces.Count];
            loading.Close();
            LoadReport = $"world in {made} ms{(making.Result.TimedOut ? " (the budget ran out: the best world found)" : "")}, game and chart prepared in {prepared} ms off the main thread, " +
                         $"drawn in {drawn} ms on the {(gpu ? "GPU" : "CPU")}; {map.ChartReport}";
            Debug.Log($"Painted Realm: a generated world, seed {setup.Seed}, {world.Provinces.Count} provinces, {world.Countries.Count} countries, {game.Turns.Count} turns; {LoadReport}");
        }

        private float redrawAt = -1;
        private bool redrawing;
        private int chartSeed;

        /// <summary>The window changed size: draw the chart again for it, once the size has settled (a drag sends many).</summary>
        private void ChartResized() { if (world != null) redrawAt = Time.realtimeSinceStartup + 0.6f; }

        private void RedrawChartIfDue()
        {
            if (redrawAt < 0 || redrawing || Time.realtimeSinceStartup < redrawAt || world == null) return;
            redrawAt = -1;
            int want = ChartImage.WidthFor(Screen.width);
            if (Mathf.Abs(want - map.ChartWidth) < map.ChartWidth * 0.1f) return; // close enough: mipmaps cover it
            StartCoroutine(RedrawChart(want));
        }

        // The chart's inputs off the main thread, then a GPU draw swapped in: the game plays on meanwhile.
        private IEnumerator RedrawChart(int width)
        {
            redrawing = true;
            var w = world; int seed = chartSeed;
            var making = System.Threading.Tasks.Task.Run(() => ChartImage.Make(w, width, seed, true));
            while (!making.IsCompleted) yield return null;
            redrawing = false;
            if (making.IsFaulted || world != w) yield break;
            if (map.RedrawChart(making.Result, seed)) Debug.Log($"Painted Realm: the chart drawn again at {width} wide for a {Screen.width} × {Screen.height} window.");
        }

        /// <summary>The zoom range: the Classic board's numbers, or the same proportions of a generated world's fitted view.</summary>
        private void SetZoomRange(float fitted)
        {
            if (world == null) return;
            zoomMin = fitted * 0.16f;
            zoomMax = fitted * 1.35f;
        }

        /// <summary>Screen pixels per grid cell at the current zoom, in panel units (the UI scale divided out).</summary>
        private float PanelPixelsPerCell()
        {
            float screenPerUnit = Screen.height / (2 * cam.orthographicSize);
            float unitsPerCell = map.SizeUnits.x / world.Geometry.Width;
            return screenPerUnit * unitsPerCell / CurrentUiScale();
        }

        /// <summary>Sizes the names for the zoom, then places the regions'; the provinces' are placed by the screen with the rest.</summary>
        private void FitChartLabels()
        {
            if (world == null || ui == null) return;
            float ppc = PanelPixelsPerCell();
            if (Mathf.Abs(ppc - labelZoom) > 0.01f)
            {
                labelZoom = ppc;
                ChartLabels.Fit(mapLabels, ppc);
                foreach (var l in mapLabels) if (!l.Region) labelSizes[l.Province] = l.Visible ? l.Size : 0;
            }
            var regions = new List<(string, Vector2, float)>();
            if (document?.rootVisualElement?.panel == null) return;
            foreach (var l in mapLabels)
            {
                if (!l.Region || !l.Visible) continue;
                var sp = cam.WorldToScreenPoint(map.GridWorld(l.U, l.V, world.Geometry.Width, world.Geometry.Height));
                var at = RuntimePanelUtils.ScreenToPanel(document.rootVisualElement.panel, new Vector2(sp.x, Screen.height - sp.y));
                regions.Add((l.Text, at, l.Size));
            }
            ui.PlaceRegionLabels(regions);
        }

        /// <summary>What the readouts say about the map.</summary>
        private string MapReadout() => world == null ? "map: Classic, the painting" :
            $"map: generated, {world.Generated?.Settings?.Size} {world.Generated?.Settings?.Shape}, seed {world.Generated?.Seed}; {map.ChartReport}; " +
            $"{mapLabels?.Count(l => l.Visible && !l.Region)} province names and {mapLabels?.Count(l => l.Visible && l.Region)} region names showing at {labelZoom:0.0} px a cell";
    }
}

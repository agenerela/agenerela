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
        public void StartGeneratedWatch(MapSize size, LandShape shape, int seed, int countries = 0)
        {
            if (countries <= 0) countries = MapSettings.DefaultCountries(size);
            pendingWorld = new GameSetup
            {
                Seed = seed,
                Map = GameSetup.GeneratedMapId,
                Countries = countries,
                Generator = new MapSettings { Size = size, Shape = shape },
            };
            pendingWorld.Validate();
            LeaveToMenu();
        }

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
            var preparing = System.Threading.Tasks.Task.Run(() =>
            {
                var mapDef = world.ToDefinition();
                var rules = setup.BuildRules(mapDef);
                var rec = ScriptedWorldGame.Play(setup, mapDef, rules);
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
            loading.Set("Laying out the chart...", 0.95f);
            yield return null;
            CountryLook.UseWorld(world);
            map.BuildChart(chartImage, world, setup.Seed);
            long drawn = watch.ElapsedMilliseconds;
            mapLabels = ChartLabels.For(world);
            labelSizes = new float[world.Provinces.Count];
            loading.Close();
            LoadReport = $"world in {made} ms{(making.Result.TimedOut ? " (the budget ran out: the best world found)" : "")}, game and chart prepared in {prepared} ms off the main thread, " +
                         $"drawn in {drawn} ms on the {(gpu ? "GPU" : "CPU")}; {map.ChartReport}";
            Debug.Log($"Painted Realm: a generated world, seed {setup.Seed}, {world.Provinces.Count} provinces, {world.Countries.Count} countries, {game.Turns.Count} turns; {LoadReport}");
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

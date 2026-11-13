using System;
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

        /// <summary>Takes the world queued by <see cref="StartGeneratedWatch"/> and builds its game, chart and looks; false when none is queued.</summary>
        private bool BuildGeneratedGame()
        {
            var setup = pendingWorld;
            pendingWorld = null;
            if (setup == null) return false;
            var watch = System.Diagnostics.Stopwatch.StartNew();
            world = setup.BuildWorld();
            long made = watch.ElapsedMilliseconds; watch.Restart();
            var mapDef = world.ToDefinition();
            var rules = setup.BuildRules(mapDef);
            sourceRecording = ScriptedWorldGame.Play(setup, mapDef, rules);
            game = ReplayBuilder.Build(sourceRecording, mapDef, rules);
            long played = watch.ElapsedMilliseconds;
            CountryLook.UseWorld(world);
            var chart = ChartImage.Make(world, ChartImage.WidthFor(Screen.width), setup.Seed);
            map.BuildChart(chart, world);
            mapLabels = ChartLabels.For(world);
            labelSizes = new float[world.Provinces.Count];
            Debug.Log($"Painted Realm: a generated world, seed {setup.Seed}, {world.Provinces.Count} provinces, {world.Countries.Count} countries; " +
                      $"world in {made} ms, game in {played} ms ({game.Turns.Count} turns); {map.ChartReport}");
            return true;
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

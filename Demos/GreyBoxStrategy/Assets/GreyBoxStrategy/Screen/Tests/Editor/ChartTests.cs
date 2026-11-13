using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using GreyBoxStrategy.Chart;
using GreyBoxStrategy.Realm;
using GreyBoxStrategy.Replay;
using NUnit.Framework;
using UnityEngine;

namespace GreyBoxStrategy.Screen.Tests
{
    /// <summary>
    /// The drawn chart's non-visual parts (rules v2, R2's screen spike): the province map and the
    /// state texture for any number of provinces, names placed inside their provinces and never on
    /// each other, ten owner colours that stay apart on the map, and a generated world's game
    /// replayed on screen.
    /// </summary>
    public class ChartTests
    {
        private static readonly Dictionary<MapSize, WorldMap> worlds = new Dictionary<MapSize, WorldMap>();

        private static WorldMap World(MapSize size)
        {
            if (!worlds.TryGetValue(size, out var w))
            {
                var made = MapGenerator.Generate(7, MapSettings.DefaultCountries(size), new MapSettings { Size = size });
                Assert.That(made.Playable, Is.True, $"seed 7 makes a playable {size} world");
                worlds[size] = w = made.Map;
            }
            return w;
        }

        [TestCase(MapSize.Small)]
        [TestCase(MapSize.Huge)]
        public void The_province_map_holds_every_province_where_the_grid_puts_it(MapSize size)
        {
            var world = World(size);
            var geo = world.Geometry;
            var raster = ChartRaster.Build(geo, geo.Width * 4, 7);
            var ids = raster.Downsample(2, out int w, out int h);
            int n = world.Provinces.Count;

            var area = new int[n];
            foreach (var id in ids)
            {
                if (ChartRaster.IsWater(id)) continue;
                Assert.That(id, Is.LessThan(n), "no id beyond the last province");
                area[id]++;
            }
            var cells = geo.DecodeCells();
            float pixelsPerCell = (w / (float)geo.Width) * (h / (float)geo.Height);
            for (int p = 0; p < n; p++)
            {
                int cellArea = cells.Count(c => c == p);
                Assert.That(area[p], Is.GreaterThan(0), $"{world.Provinces[p].Id} is drawn");
                Assert.That(area[p] / pixelsPerCell, Is.EqualTo(cellArea).Within(cellArea * 0.2 + 2),
                    $"{world.Provinces[p].Id} keeps its size: the smoothing moves borders, it does not move land");
            }
            // Water stays water and land stays land at every cell centre more than a cell from the coast.
            int wrong = 0, checkedCells = 0;
            for (int y = 1; y < geo.Height - 1; y++)
            for (int x = 1; x < geo.Width - 1; x++)
            {
                int c = cells[y * geo.Width + x];
                bool inner = true;
                for (int dy = -1; dy <= 1 && inner; dy++)
                for (int dx = -1; dx <= 1 && inner; dx++)
                    inner = cells[(y + dy) * geo.Width + x + dx] == c;
                if (!inner) continue;
                checkedCells++;
                ushort drawn = ids[(int)((y + 0.5f) / geo.Height * h) * w + (int)((x + 0.5f) / geo.Width * w)];
                int expect = c >= 0 ? c : -1;
                int got = ChartRaster.IsWater(drawn) ? -1 : drawn;
                if (got != expect) wrong++;
            }
            Assert.That(wrong, Is.EqualTo(0), $"every cell inside a province (or the sea) is drawn as it ({checkedCells} checked)");
        }

        [Test]
        public void The_state_texture_has_a_column_per_province_and_the_map_finds_each()
        {
            var world = World(MapSize.Huge);
            Assert.That(world.Provinces.Count, Is.GreaterThan(MapView.StateColumns), "more provinces than the Classic board's state texture holds");
            var chart = ChartImage.Make(world, world.Geometry.Width * 3, 7);
            var go = new GameObject("chart test");
            try
            {
                var view = go.AddComponent<MapView>();
                typeof(MapView).GetField("shader", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(view, Shader.Find("GreyBoxStrategy/PaintedMap"));
                view.BuildChart(chart, world);
                Assert.That(view.Count, Is.EqualTo(world.Provinces.Count));
                Assert.That(view.StateSize, Is.EqualTo(new Vector2Int(world.Provinces.Count, 4)));
                for (int p = 0; p < view.Count; p++)
                    Assert.That(view.ProvinceAt(view.ProvinceWorld(p)), Is.EqualTo(p), $"the name of {world.Provinces[p].Id} stands on it");
                // Owners for every province, ten countries: nothing falls off the texture.
                var owners = world.Provinces.Select(x => x.Owner).ToList();
                CountryLook.UseWorld(world);
                view.SetOwners(owners, id => CountryLook.Of(id).Colour, id => CountryLook.Of(id).Slot);
                view.BeginCapture(view.Count - 1, world.Countries[9].Id, owners[view.Count - 1], id => CountryLook.Of(id).Colour, id => CountryLook.Of(id).Slot, owners);
                view.EndCapture(view.Count - 1);
            }
            finally
            {
                CountryLook.UseWorld(null);
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [TestCase(MapSize.Small)]
        [TestCase(MapSize.Medium)]
        [TestCase(MapSize.Huge)]
        public void Every_name_stands_inside_its_province_and_none_overlap(MapSize size)
        {
            var world = World(size);
            var geo = world.Geometry;
            var chart = ChartImage.Make(world, geo.Width * 3, 7);
            var cells = geo.DecodeCells();
            var labels = ChartLabels.For(world);
            foreach (var l in labels.Where(x => !x.Region))
            {
                string id = world.Provinces[l.Province].Id;
                Assert.That(cells[(int)l.V * geo.Width + (int)l.U], Is.EqualTo(l.Province), $"{id}'s label point is in its own cells");
                Assert.That(chart.ProvinceAtCell(l.U, l.V), Is.EqualTo(l.Province), $"{id}'s label point is in its province as drawn");
            }
            foreach (float zoom in new[] { 2.5f, 6f, 12f, 24f })
            {
                int shown = ChartLabels.Fit(labels, zoom);
                Assert.That(shown, Is.GreaterThan(0), $"something is named at {zoom} px a cell");
                var boxes = labels.Where(l => l.Visible).Select(l =>
                {
                    float w = ChartLabels.Width(l), h = l.Size * 1.25f, cx = l.U * zoom, cy = l.V * zoom;
                    return (l.Text, x0: cx - w / 2, y0: cy - h / 2, x1: cx + w / 2, y1: cy + h / 2);
                }).ToList();
                for (int i = 0; i < boxes.Count; i++)
                for (int j = i + 1; j < boxes.Count; j++)
                {
                    var a = boxes[i]; var b = boxes[j];
                    bool overlap = a.x0 < b.x1 && b.x0 < a.x1 && a.y0 < b.y1 && b.y0 < a.y1;
                    Assert.That(overlap, Is.False, $"{a.Text} and {b.Text} collide at {zoom} px a cell");
                }
                if (zoom < ChartLabels.RegionZoom) Assert.That(labels.Where(l => l.Visible).All(l => l.Region), Is.True, "zoomed out, regions are named instead");
                else Assert.That(labels.Any(l => l.Visible && l.Region), Is.False, "zoomed in, regions step back");
            }
        }

        [Test]
        public void Of_two_names_that_would_collide_the_larger_shows()
        {
            // Names sized to their room rarely meet on a real world, so the rule is checked on two that must.
            var labels = new List<MapLabel>
            {
                new MapLabel { Text = "Westmarch", U = 10, V = 10, Room = 6, Province = 0 },
                new MapLabel { Text = "Eastholm", U = 11, V = 10.2f, Room = 3, Province = 1 },
                new MapLabel { Text = "Farshore", U = 40, V = 30, Room = 3, Province = 2 },
            };
            Assert.That(ChartLabels.Fit(labels, 8), Is.EqualTo(2));
            Assert.That(labels.Select(l => l.Visible), Is.EqualTo(new[] { true, false, true }), "the smaller of the two colliding names hides; the far one stays");
        }

        [Test]
        public void Ten_owner_colours_stay_apart_once_washed_over_the_chart()
        {
            double closest = ChartPalette.ClosestPair(10, out string which);
            // About 15 is what the Classic five already give among themselves.
            Assert.That(closest, Is.GreaterThanOrEqualTo(12), $"the closest pair, {which}, is {closest:0.0} apart");
            var distinct = ChartPalette.Owners.Select(c => $"{c[0]},{c[1]},{c[2]}").Distinct().Count();
            Assert.That(distinct, Is.EqualTo(10));
        }

        [Test]
        public void A_generated_world_plays_and_replays_exactly_on_screen()
        {
            var setup = new GameSetup { Seed = 7, Map = GameSetup.GeneratedMapId, Countries = 6, Generator = new MapSettings { Size = MapSize.Medium } };
            var world = setup.BuildWorld();
            var map = world.ToDefinition();
            var rules = setup.BuildRules(map);
            var rec = ScriptedWorldGame.Play(setup, map, rules);
            var g = ReplayBuilder.Build(rec, map, rules);
            Assert.That(g.Result, Is.Not.Null, "the game ends");
            Assert.That(g.Dev, Is.True, "a scripted game is never a measured one");
            Assert.That(g.ProvinceIds, Is.EqualTo(world.Provinces.Select(p => p.Id).ToArray()), "the screen's province order is the map's, which the drawn province map uses");
            Assert.That(g.Map, Is.SameAs(map));
            Assert.That(g.Rules.ProvincesToWin, Is.EqualTo((world.Provinces.Count * 60 + 99) / 100));
            // The Classic board keeps its grid order, which its baked province map uses.
            var classic = MapDefinition.Standard();
            var order = ReplayBuilder.ProvinceOrder(classic);
            foreach (var p in classic.Provinces) Assert.That(order[p.Row * classic.Columns + p.Column], Is.EqualTo(p.Id));
        }
    }
}

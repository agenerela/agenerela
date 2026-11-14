using System;
using System.Collections.Generic;
using System.Linq;
using GreyBoxStrategy.Chart;
using NUnit.Framework;

namespace GreyBoxStrategy.Screen.Tests
{
    /// <summary>
    /// What the drawn chart puts where, from the world's data (rules v2, the chart's second slice):
    /// development read as settlements and farmland, rivers and roads with their bridges, scenery
    /// islands drawn but never played, and the atlas the GPU draws the glyphs from.
    /// </summary>
    public class ChartSceneTests
    {
        private static WorldMap world;
        private static ChartRaster raster;
        private static ChartScene scene;

        [OneTimeSetUp]
        public void Make()
        {
            var made = MapGenerator.Generate(7, 6, new MapSettings { Size = MapSize.Medium });
            Assert.That(made.Playable, Is.True);
            world = made.Map;
            raster = ChartRaster.Build(world.Geometry, world.Geometry.Width * 3, 7);
            scene = ChartLayout.Build(world, raster, 7);
        }

        [Test]
        public void Development_decides_the_settlement_and_the_fields_around_it()
        {
            var settlements = scene.Glyphs.Where(g => g.Kind == GlyphKind.Settlement).ToList();
            Assert.That(settlements.Count, Is.EqualTo(world.Provinces.Count), "one settlement a province");
            for (int p = 0; p < world.Provinces.Count; p++)
            {
                var pr = world.Provinces[p];
                var (u, v) = scene.Settlements[p];
                var g = settlements.Single(x => x.U == u && x.V == v);
                int tier = SettlementLook.Tier(g.Param);
                Assert.That(tier, Is.EqualTo(pr.Capital ? 4 : Math.Min(3, ChartLayout.DevelopmentOf(pr))), $"{pr.Id}: its tier follows its development");
                Assert.That(SettlementLook.Capital(g.Param), Is.EqualTo(pr.Capital), $"{pr.Id}: a capital reads as a city");
                Assert.That(SettlementLook.Forts(g.Param), Is.EqualTo(Math.Min(3, pr.Forts)), $"{pr.Id}: its forts are drawn");
                Assert.That(SettlementLook.Port(g.Param), Is.EqualTo(pr.Port));
            }
            // More fields where there is more: the farmland around each settlement grows with its development.
            float Fields(int p)
            {
                var (su, sv) = scene.Settlements[p];
                float sum = 0;
                int gw = world.Geometry.Width;
                for (int i = 0; i < scene.Farmland.Length; i++)
                    if (raster.Cells[i] == p && ChartLayout.Dist(i % gw + 0.5f, i / gw + 0.5f, su, sv) < 6) sum += scene.Farmland[i];
                return sum;
            }
            var plains = Enumerable.Range(0, world.Provinces.Count).Where(p => world.Provinces[p].Terrain == Terrain.Plains && !world.Provinces[p].Capital).ToList();
            var poor = plains.Where(p => ChartLayout.DevelopmentOf(world.Provinces[p]) <= 1).ToList();
            var rich = plains.Where(p => ChartLayout.DevelopmentOf(world.Provinces[p]) >= 3).ToList();
            Assume.That(poor.Count > 0 && rich.Count > 0, "the world has poor and rich plains");
            Assert.That(rich.Average(Fields), Is.GreaterThan(poor.Average(Fields) * 2.5), "a developed province is farmed far more than a poor one, so it shows");
        }

        [Test]
        public void Every_river_and_road_is_drawn_and_every_bridge_stands_on_its_road()
        {
            var geo = world.Geometry;
            Assume.That(geo.Rivers.Count, Is.GreaterThan(0));
            Assert.That(scene.Strokes.Count(s => s.Kind == StrokeKind.River), Is.EqualTo(geo.Rivers.Count(r => r.Line.Length >= 4)));
            Assert.That(scene.Strokes.Count(s => s.Kind != StrokeKind.River), Is.EqualTo(geo.Roads.Count));
            Assert.That(scene.Strokes.Where(s => s.Kind == StrokeKind.River).All(s => s.Width > 0.1f), Is.True);
            var bridges = geo.Roads.SelectMany(r => r.Bridges).ToList();
            var drawn = scene.Glyphs.Where(g => g.Kind == GlyphKind.Bridge).ToList();
            Assert.That(drawn.Count, Is.EqualTo(bridges.Count), "a bridge for every crossing");
            foreach (var b in bridges)
            {
                Assert.That(drawn.Any(g => Math.Abs(g.U - b.At[0]) < 0.01f && Math.Abs(g.V - b.At[1]) < 0.1f), Is.True, $"the bridge over {b.River} between {b.A} and {b.B}");
                // Its road passes over it.
                var road = geo.Roads.First(r => r.Bridges.Contains(b));
                var stroke = scene.Strokes.Where(s => s.Kind != StrokeKind.River).ElementAt(geo.Roads.IndexOf(road));
                Assert.That(stroke.Points.Min(p => ChartLayout.Dist(p.u, p.v, b.At[0], b.At[1])), Is.LessThan(0.6f), $"{road.Id} crosses at its bridge");
            }
            // A road runs from settlement to settlement along its provinces.
            foreach (var (road, stroke) in geo.Roads.Zip(scene.Strokes.Where(s => s.Kind != StrokeKind.River), (r, s) => (r, s)))
            {
                int first = world.Provinces.FindIndex(p => p.Id == road.Provinces[0]), last = world.Provinces.FindIndex(p => p.Id == road.Provinces[road.Provinces.Count - 1]);
                Assert.That(ChartLayout.Dist(stroke.Points[0].u, stroke.Points[0].v, scene.Settlements[first].u, scene.Settlements[first].v), Is.LessThan(0.6f));
                Assert.That(ChartLayout.Dist(stroke.Points[stroke.Points.Count - 1].u, stroke.Points[stroke.Points.Count - 1].v, scene.Settlements[last].u, scene.Settlements[last].v), Is.LessThan(0.6f));
            }
        }

        [Test]
        public void Scenery_islands_are_drawn_as_land_and_never_played()
        {
            var cells = world.Geometry.DecodeCells();
            Assume.That(cells.Count(c => c == MapGeometry.Island), Is.GreaterThan(0), "the world has scenery islands");
            int islandPixels = raster.Ids.Count(id => id == ChartRaster.Island);
            Assert.That(islandPixels, Is.GreaterThan(0), "the chart draws them");
            Assert.That(ChartRaster.IsWater(ChartRaster.Island), Is.False, "as land, with a coast");
            Assert.That(ChartRaster.IsProvince(ChartRaster.Island), Is.False);
            var shaderIds = raster.Downsample(1, out _, out _);
            Assert.That(shaderIds.Contains(ChartRaster.Island), Is.False, "the province map, and so the rules and the wash, see sea there");
            Assert.That(scene.TerrainOf.Count(t => t == -2), Is.EqualTo(cells.Count(c => c == MapGeometry.Island)));
        }

        [Test]
        public void The_atlas_holds_each_look_once_and_a_quad_for_every_glyph()
        {
            float s = raster.Scale;
            var atlas = ChartAtlas.Build(scene.Glyphs, s);
            Assert.That(atlas.Quads.Count, Is.EqualTo(scene.Glyphs.Count), "a quad a glyph, in the scene's order");
            Assert.That(atlas.Entries, Is.EqualTo(scene.Glyphs.Select(g => g.AtlasKey).Distinct().Count()));
            Assert.That(atlas.Entries, Is.LessThan(scene.Glyphs.Count / 5), "looks are shared: sizes in quarter cells, eight variants");
            for (int k = 0; k < scene.Glyphs.Count; k++)
            {
                var g = scene.Glyphs[k]; var q = atlas.Quads[k];
                var (l, u, r, d) = ChartGlyphs.Extent(g);
                Assert.That(q.X0, Is.LessThanOrEqualTo(g.U * s - l * s + 0.01f));
                Assert.That(q.Y1, Is.GreaterThanOrEqualTo(g.V * s + d * s - 0.01f), "the quad covers the glyph's extent");
                Assert.That(q.U0 >= 0 && q.U1 <= 1 && q.V0 >= 0 && q.V1 <= 1, Is.True);
            }
            // Each atlas cell holds ink: nothing was packed off the sheet.
            int inked = 0;
            for (int k = 0; k < atlas.Quads.Count; k += Math.Max(1, atlas.Quads.Count / 50))
            {
                var q = atlas.Quads[k];
                int y0 = (int)(q.V0 * atlas.Height), y1 = (int)(q.V1 * atlas.Height);
                bool any = false;
                for (int y = y0; y < y1 && !any; y++)
                for (int xx = (int)(q.U0 * atlas.Width); xx < (int)(q.U1 * atlas.Width) && !any; xx++)
                    any = atlas.Rgba[(y * atlas.Width + xx) * 4 + 3] > 0;
                if (any) inked++;
            }
            Assert.That(inked, Is.GreaterThan(45));
        }
    }
}

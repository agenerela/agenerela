using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace GreyBoxStrategy.Chart
{
    /// <summary>
    /// Everything the map needs to show a generated world, made in one call: the drawn chart, the
    /// province map and edge field the map shader reads, and the open sea's tile. Arrays run top
    /// row first; the screen flips them into textures.
    /// </summary>
    /// <remarks>
    /// The province map is half the chart's width, so its border lines (drawn by the shader one to
    /// three of its pixels wide) come out about as thick on screen as the Classic board's, while
    /// the chart under them stays sharp. Both are cut from one raster, so they never disagree.
    /// </remarks>
    public sealed class ChartImage
    {
        public int ChartWidth, ChartHeight;
        /// <summary>RGBA, top row first.</summary>
        public byte[] Chart;
        public int IdWidth, IdHeight;
        /// <summary>Province index per pixel, <see cref="ChartRaster.Sea"/> or <see cref="ChartRaster.Water"/>, top row first.</summary>
        public ushort[] Ids;
        /// <summary>Distance to the nearest border × 16, capped at 255, as <c>_EdgeTex</c> wants it.</summary>
        public byte[] Edge;
        public int SeaTileSize;
        public byte[] SeaTile;
        public int GridWidth, GridHeight;
        public int Glyphs;
        public readonly Dictionary<string, long> Milliseconds = new Dictionary<string, long>();

        /// <summary>The chart <paramref name="chartWidth"/> pixels wide (even); the province map is half that.</summary>
        public static ChartImage Make(WorldMap world, int chartWidth, int seed)
        {
            if (world?.Geometry == null) throw new ArgumentException("Only a generated world has the geometry a chart is drawn from.");
            if (world.Geometry.Provinces.Count >= ChartRaster.Water) throw new ArgumentException("Too many provinces for a 16-bit province map.");
            chartWidth -= chartWidth % 2;
            var o = new ChartImage { GridWidth = world.Geometry.Width, GridHeight = world.Geometry.Height };
            var watch = Stopwatch.StartNew();
            var raster = ChartRaster.Build(world.Geometry, chartWidth, seed);
            o.Milliseconds["raster"] = watch.ElapsedMilliseconds; watch.Restart();
            o.Ids = raster.Downsample(2, out o.IdWidth, out o.IdHeight);
            o.Edge = ChartRaster.EdgeField(o.Ids, o.IdWidth, o.IdHeight);
            o.Milliseconds["edge"] = watch.ElapsedMilliseconds;
            var painter = new ChartPainter(world, raster, seed);
            var canvas = painter.Paint();
            foreach (var kv in painter.Milliseconds) o.Milliseconds["paint " + kv.Key] = kv.Value;
            o.Chart = canvas.Rgba; o.ChartWidth = canvas.Width; o.ChartHeight = canvas.Height;
            o.Glyphs = painter.GlyphCount;
            watch.Restart();
            o.SeaTileSize = 512;
            o.SeaTile = ChartPainter.SeaTile(o.SeaTileSize);
            o.Milliseconds["sea tile"] = watch.ElapsedMilliseconds;
            return o;
        }

        /// <summary>For tests and the screen: which province a point in grid units shows, on the province map.</summary>
        public int ProvinceAtCell(float u, float v)
        {
            int x = (int)(u / GridWidth * IdWidth), y = (int)(v / GridHeight * IdHeight);
            if (x < 0 || y < 0 || x >= IdWidth || y >= IdHeight) return -1;
            ushort id = Ids[y * IdWidth + x];
            return ChartRaster.IsWater(id) ? -1 : id;
        }

        /// <summary>The chart's width for a screen <paramref name="screenWidth"/> pixels wide: about 1.25 times it, 2048 to 6144.</summary>
        public static int WidthFor(int screenWidth) => Math.Max(2048, Math.Min(6144, (int)(screenWidth * 1.25f) / 2 * 2));
    }

    /// <summary>A name on the map: a province's or a region's, placed by <see cref="ChartLabels"/>.</summary>
    public sealed class MapLabel
    {
        public string Text;
        /// <summary>Where it stands, in grid units.</summary>
        public float U, V;
        /// <summary>The largest circle around it inside its province, in grid units; 0 for a region.</summary>
        public float Room;
        public int Province = -1;
        public bool Region, Capital;
        /// <summary>Set by <see cref="ChartLabels.Fit"/>: its font size in screen pixels, and whether it shows.</summary>
        public float Size;
        public bool Visible;
    }

    /// <summary>
    /// Where names go on a generated world, and which of them fit at the current zoom (rules v2
    /// section 2.6): a province's name at the point farthest from its border, sized to the room
    /// there; a region's in the middle of its provinces, shown only zoomed out; larger and more
    /// important names first, and a name that would collide hides.
    /// </summary>
    public static class ChartLabels
    {
        /// <summary>Province names below this size in screen pixels hide.</summary>
        public const float MinSize = 9;
        /// <summary>Below this many screen pixels per grid cell, regions are named instead of provinces.</summary>
        public const float RegionZoom = 4.2f;

        public static List<MapLabel> For(WorldMap world)
        {
            var geo = world.Geometry;
            var list = new List<MapLabel>();
            for (int p = 0; p < world.Provinces.Count; p++)
            {
                var shape = geo.Provinces[p];
                list.Add(new MapLabel
                {
                    Text = world.Provinces[p].Name ?? world.Provinces[p].Id,
                    U = shape.Label[0] + 0.5f, V = shape.Label[1] + 0.5f,
                    Room = Math.Max(1, shape.LabelRadius),
                    Province = p,
                    Capital = world.Provinces[p].Capital,
                });
            }
            foreach (var r in world.Regions)
            {
                var members = r.Provinces.Select(id => world.Provinces.FindIndex(x => x.Id == id)).Where(i => i >= 0).ToList();
                if (members.Count == 0) continue;
                list.Add(new MapLabel
                {
                    Text = r.Name ?? r.Id,
                    U = members.Average(i => list[i].U), V = members.Average(i => list[i].V),
                    Region = true,
                });
            }
            return list;
        }

        /// <summary>
        /// Sizes every label for <paramref name="pxPerCell"/> screen pixels per grid cell and hides
        /// the ones that do not fit or would collide. Returns how many show.
        /// </summary>
        public static int Fit(IReadOnlyList<MapLabel> labels, float pxPerCell, float uiScale = 1)
        {
            bool regions = pxPerCell < RegionZoom;
            foreach (var l in labels)
            {
                if (l.Region) l.Size = regions ? 13 * uiScale : 0;
                else
                {
                    float room = l.Room * pxPerCell * 0.62f;
                    float size = Math.Min(room, (l.Capital ? 16 : 13.5f) * uiScale);
                    l.Size = regions || size < MinSize * uiScale ? 0 : size;
                }
                l.Visible = false;
            }
            var placed = new List<(float x0, float y0, float x1, float y1)>();
            int shown = 0;
            foreach (var l in labels.Where(l => l.Size > 0).OrderByDescending(l => l.Region).ThenByDescending(l => l.Capital).ThenByDescending(l => l.Size))
            {
                float w = Width(l), h = l.Size * 1.25f;
                float cx = l.U * pxPerCell, cy = l.V * pxPerCell;
                var box = (cx - w / 2, cy - h / 2, cx + w / 2, cy + h / 2);
                if (placed.Any(b => b.x0 < box.Item3 && box.Item1 < b.x1 && b.y0 < box.Item4 && box.Item2 < b.y1)) continue;
                placed.Add(box);
                l.Visible = true;
                shown++;
            }
            return shown;
        }

        /// <summary>A label's width in screen pixels: book serif capitals for capitals and regions (widely spaced), mixed case otherwise.</summary>
        public static float Width(MapLabel l) => l.Text.Length * l.Size * (l.Region ? 0.95f : l.Capital ? 0.72f : 0.56f);
    }
}

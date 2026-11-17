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
        /// <summary>CPU mode: the drawn chart, RGBA, top row first. Null in GPU mode, which draws it from the fields below.</summary>
        public byte[] Chart;
        public int IdWidth, IdHeight;
        /// <summary>Province index per pixel, <see cref="ChartRaster.Sea"/> or <see cref="ChartRaster.Water"/>, top row first. Scenery islands are sea here.</summary>
        public ushort[] Ids;
        /// <summary>Distance to the nearest border × 16, capped at 255, as <c>_EdgeTex</c> wants it.</summary>
        public byte[] Edge;
        public int SeaTileSize;
        public byte[] SeaTile;
        public int GridWidth, GridHeight;
        public int Glyphs;
        public readonly Dictionary<string, long> Milliseconds = new Dictionary<string, long>();

        // ---- GPU mode: what the ground shader and the quads need ----
        /// <summary>GPU mode: the land's signed distance to the coast in province-map pixels (positive on land, islands included), top row first.</summary>
        public float[] CoastSdf;
        public ChartScene Scene;
        public ChartFields Fields;
        public ChartAtlas Atlas;
        /// <summary>Per province: its terrain's index.</summary>
        public byte[] TerrainOf;
        public float[] Grain;
        /// <summary>Chart pixels per grid cell.</summary>
        public float Scale;
        public bool Gpu => Chart == null;

        /// <summary>
        /// The chart <paramref name="chartWidth"/> pixels wide (even); the province map is half that.
        /// <paramref name="gpu"/>: build only the inputs the GPU renderer draws from (a raster at half
        /// size, the scene, its fields and atlas), which takes a fraction of the time; otherwise draw
        /// the whole chart on the CPU.
        /// </summary>
        public static ChartImage Make(WorldMap world, int chartWidth, int seed, bool gpu = false, byte[] paper = null, int paperSize = 0)
        {
            if (world?.Geometry == null) throw new ArgumentException("Only a generated world has the geometry a chart is drawn from.");
            if (world.Geometry.Provinces.Count >= ChartRaster.Island) throw new ArgumentException("Too many provinces for a 16-bit province map.");
            chartWidth -= chartWidth % 2;
            var o = new ChartImage { GridWidth = world.Geometry.Width, GridHeight = world.Geometry.Height };
            var watch = Stopwatch.StartNew();
            if (gpu)
            {
                var half = ChartRaster.Build(world.Geometry, chartWidth / 2, seed);
                o.Milliseconds["raster"] = watch.ElapsedMilliseconds; watch.Restart();
                o.Ids = half.Downsample(1, out o.IdWidth, out o.IdHeight);
                o.Edge = ChartRaster.EdgeField(o.Ids, o.IdWidth, o.IdHeight);
                var dist = ChartRaster.DistanceToOther(half.Ids, half.Width, half.Height, (a, c) => ChartRaster.IsWater(a) != ChartRaster.IsWater(c));
                o.CoastSdf = new float[dist.Length];
                for (int i = 0; i < dist.Length; i++) o.CoastSdf[i] = ChartRaster.IsWater(half.Ids[i]) ? -(dist[i] - 0.5f) : dist[i] - 0.5f;
                o.Milliseconds["fields"] = watch.ElapsedMilliseconds; watch.Restart();
                o.Scene = ChartLayout.Build(world, half, seed);
                o.Fields = ChartFields.Build(world, o.Scene, seed);
                o.Milliseconds["layout"] = watch.ElapsedMilliseconds; watch.Restart();
                o.Scale = chartWidth / (float)world.Geometry.Width;
                o.ChartWidth = chartWidth;
                o.ChartHeight = (int)Math.Round(world.Geometry.Height * o.Scale);
                o.Atlas = ChartAtlas.Build(o.Scene.Glyphs, o.Scale);
                o.Glyphs = o.Scene.Glyphs.Count;
                o.Milliseconds["atlas"] = watch.ElapsedMilliseconds; watch.Restart();
                o.TerrainOf = world.Provinces.Select(p => (byte)p.Terrain).ToArray();
                o.Grain = ChartPainter.Grain(seed);
            }
            else
            {
                var raster = ChartRaster.Build(world.Geometry, chartWidth, seed);
                o.Milliseconds["raster"] = watch.ElapsedMilliseconds; watch.Restart();
                o.Ids = raster.Downsample(2, out o.IdWidth, out o.IdHeight);
                o.Edge = ChartRaster.EdgeField(o.Ids, o.IdWidth, o.IdHeight);
                o.Milliseconds["edge"] = watch.ElapsedMilliseconds;
                var painter = new ChartPainter(world, raster, seed);
                if (paper != null) painter.UsePaper(paper, paperSize);
                var canvas = painter.Paint();
                foreach (var kv in painter.Milliseconds) o.Milliseconds["paint " + kv.Key] = kv.Value;
                o.Chart = canvas.Rgba; o.ChartWidth = canvas.Width; o.ChartHeight = canvas.Height;
                o.Glyphs = painter.GlyphCount;
                o.Scene = painter.Scene; o.Fields = painter.Fields;
                o.Scale = raster.Scale;
            }
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
    /// <summary>A realm's name on the map, set along its land: where, at what angle, and how long a line it has.</summary>
    public sealed class RealmLabel
    {
        public string Country, Text;
        /// <summary>Its centre in grid units, on the realm's own land.</summary>
        public float U, V;
        /// <summary>Degrees, clockwise on screen, within ±<see cref="RealmLabels.MaxAngle"/>.</summary>
        public float Angle;
        /// <summary>The length of land along that angle, in grid units.</summary>
        public float Span;
        /// <summary>Set by <see cref="RealmLabels.Fit"/>: its font size in screen pixels, and whether it shows.</summary>
        public float Size;
        public bool Visible;
    }

    /// <summary>
    /// Realm names (rules v2 section 2.6): widely spaced capitals along a realm's longest axis, recomputed when its
    /// land changes. They show zoomed out, where province names step back, and only for a realm of two provinces or
    /// more: a one-province realm is named by its province.
    /// </summary>
    public static class RealmLabels
    {
        public const float MaxAngle = 35, MinSize = 12, MaxSize = 30;

        public static List<RealmLabel> For(WorldMap world, IReadOnlyList<string> owners, Func<string, string> nameOf)
        {
            var geo = world.Geometry;
            var list = new List<RealmLabel>();
            if (geo == null || owners == null) return list;
            var point = new (float u, float v, float w)[world.Provinces.Count];
            for (int p = 0; p < point.Length; p++)
            {
                var shape = geo.Provinces[p];
                float room = Math.Max(1, shape.LabelRadius);
                point[p] = (shape.Label[0] + 0.5f, shape.Label[1] + 0.5f, room * room);
            }
            var index = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int p = 0; p < world.Provinces.Count; p++) index[world.Provinces[p].Id] = p;
            var next = Enumerable.Range(0, point.Length).Select(_ => new List<int>()).ToArray();
            foreach (var b in world.Borders ?? new List<string[]>())
                if (index.TryGetValue(b[0], out int x) && index.TryGetValue(b[1], out int y)) { next[x].Add(y); next[y].Add(x); }
            foreach (var g in Enumerable.Range(0, Math.Min(owners.Count, point.Length)).Where(p => owners[p] != null).GroupBy(p => owners[p]).OrderBy(g => g.Key, StringComparer.Ordinal))
            {
                // Named on its largest stretch of joined land: a realm in two pieces is not named across the land between.
                var mine = Largest(g.ToList(), next, point, owners, g.Key);
                if (mine.Count < 2) continue;
                double sw = mine.Sum(p => point[p].w);
                double cu = mine.Sum(p => point[p].u * point[p].w) / sw, cv = mine.Sum(p => point[p].v * point[p].w) / sw;
                double suu = 0, svv = 0, suv = 0;
                foreach (int p in mine)
                {
                    double du = point[p].u - cu, dv = point[p].v - cv;
                    suu += point[p].w * du * du; svv += point[p].w * dv * dv; suv += point[p].w * du * dv;
                }
                // The principal axis, kept readable: never steeper than MaxAngle, never upside down.
                double angle = 0.5 * Math.Atan2(2 * suv, suu - svv) * 180 / Math.PI;
                if (angle > 90) angle -= 180; else if (angle < -90) angle += 180;
                angle = Math.Max(-MaxAngle, Math.Min(MaxAngle, angle));
                double ax = Math.Cos(angle * Math.PI / 180), ay = Math.Sin(angle * Math.PI / 180);
                double lo = double.MaxValue, hi = double.MinValue;
                foreach (int p in mine)
                {
                    double t = (point[p].u - cu) * ax + (point[p].v - cv) * ay, r = Math.Sqrt(point[p].w);
                    lo = Math.Min(lo, t - r); hi = Math.Max(hi, t + r);
                }
                // On its own land: the centre where the realm holds it, else the realm's province nearest to it.
                int nearest = Nearest(point, Enumerable.Range(0, point.Length), cu, cv);
                if (owners[nearest] != g.Key)
                {
                    int own = Nearest(point, mine, cu, cv);
                    cu = point[own].u; cv = point[own].v;
                }
                list.Add(new RealmLabel { Country = g.Key, Text = nameOf(g.Key), U = (float)cu, V = (float)cv, Angle = (float)angle, Span = (float)(hi - lo) });
            }
            return list;
        }

        // The largest piece of a realm's land, by area, its provinces joined by shared borders.
        private static List<int> Largest(List<int> all, List<int>[] next, (float u, float v, float w)[] point, IReadOnlyList<string> owners, string who)
        {
            var seen = new HashSet<int>();
            List<int> best = new List<int>(); double bestArea = -1;
            foreach (int start in all)
            {
                if (!seen.Add(start)) continue;
                var part = new List<int> { start };
                for (int k = 0; k < part.Count; k++)
                    foreach (int q in next[part[k]])
                        if (owners[q] == who && seen.Add(q)) part.Add(q);
                double area = part.Sum(p => point[p].w);
                if (area > bestArea) { bestArea = area; best = part; }
            }
            return best;
        }

        private static int Nearest((float u, float v, float w)[] point, IEnumerable<int> among, double u, double v)
        {
            int best = -1; double d = double.MaxValue;
            foreach (int p in among)
            {
                double e = (point[p].u - u) * (point[p].u - u) + (point[p].v - v) * (point[p].v - v);
                if (e < d) { d = e; best = p; }
            }
            return best;
        }

        /// <summary>
        /// Sizes each name to most of its realm's length at <paramref name="pxPerCell"/>, between
        /// <see cref="MinSize"/> and <see cref="MaxSize"/>; shown only zoomed out (<see cref="ChartLabels.RegionZoom"/>).
        /// </summary>
        public static void Fit(IReadOnlyList<RealmLabel> realms, float pxPerCell, float uiScale = 1)
        {
            bool far = pxPerCell < ChartLabels.RegionZoom;
            foreach (var r in realms)
            {
                // The text's width is about 0.75 of its size a letter, plus the letter spacing: most of the land's length.
                float size = (r.Span * pxPerCell * 0.85f / Math.Max(1, r.Text.Length) - LetterSpacing * uiScale) / 0.75f;
                r.Size = Math.Min(MaxSize * uiScale, size);
                r.Visible = far && r.Size >= MinSize * uiScale;
            }
        }

        /// <summary>A realm name's width along its line, in screen pixels: widely spaced capitals.</summary>
        public static float Width(RealmLabel r) => r.Text.Length * (r.Size * 0.75f + LetterSpacing);

        /// <summary>The realm name's letter spacing in panel pixels, as the style sheet's <c>.realmlab</c> has it.</summary>
        public const float LetterSpacing = 9;
    }
}

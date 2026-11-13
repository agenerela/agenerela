using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace GreyBoxStrategy.Chart
{
    /// <summary>
    /// Draws a generated world as a sea chart, by code: the sea with its ripples and rhumb lines,
    /// cliffs and the coast line, land toned by terrain with a soft relief, terrain drawn as ink
    /// glyphs, ridges with their passes, settlements, ports, special places and a compass rose
    /// (rules v2 section 2.6). No owner colour and no text: the map shader washes owners over it,
    /// and names are screen text.
    /// </summary>
    /// <remarks>
    /// Every size here is in grid cells and turned into pixels by <see cref="ChartRaster.Scale"/>,
    /// so a chart drawn for a 1366-wide screen and one for a 3840-wide screen are the same drawing.
    /// </remarks>
    public sealed class ChartPainter
    {
        // ---- the palette: the painting's tones, read off it ----
        public static readonly Ink SeaDeep = new Ink(31, 38, 42);
        static readonly Ink SeaShallow = new Ink(52, 64, 66);
        static readonly Ink SeaLine = new Ink(118, 128, 118);
        static readonly Ink LakeTone = new Ink(66, 86, 92);
        static readonly Ink InkDark = new Ink(38, 28, 18);
        static readonly Ink InkSoft = new Ink(70, 54, 36);
        static readonly Ink Cliff = new Ink(104, 84, 58);
        static readonly Ink CliffDark = new Ink(62, 48, 32);
        static readonly Ink Sand = new Ink(176, 158, 112);
        static readonly Ink Gilt = new Ink(204, 166, 92);

        static Ink TerrainTone(Terrain t)
        {
            switch (t)
            {
                case Terrain.Plains: return new Ink(140, 130, 76);
                case Terrain.Forest: return new Ink(96, 102, 58);
                case Terrain.Hills: return new Ink(138, 120, 82);
                case Terrain.Marsh: return new Ink(108, 114, 86);
                default: return new Ink(132, 118, 98);
            }
        }

        static float TerrainHeight(Terrain t)
        {
            switch (t)
            {
                case Terrain.Mountains: return 1f;
                case Terrain.Hills: return 0.55f;
                case Terrain.Forest: return 0.3f;
                case Terrain.Plains: return 0.18f;
                default: return 0.05f;
            }
        }

        /// <summary>The sea pattern tiles every this many cells, so the open sea behind the chart can repeat it.</summary>
        public const int SeaPeriodCells = 24;

        private readonly WorldMap map;
        private readonly ChartRaster raster;
        private readonly int seed;
        private readonly float s;
        private ChartCanvas canvas;
        private float[] coastDist;     // pixels to the nearest pixel of the other kind (land / water)
        private int[] cliff;           // for a water pixel: pixels below the land above it, or 0
        private float[] terrainField;  // per cell, rgb, blurred
        private float[] heightField;   // per cell, blurred
        private float[] litField;      // per cell, rgb: terrain tone times the relief's light
        private float[] grain;         // paper grain, GrainSize² samples tiling every GrainPeriod cells
        private byte[] seaTile;        // the sea pattern, SeaTileSize² RGBA, tiling every SeaPeriodCells
        private const int GrainPeriod = 8, GrainSize = 256, SeaTileSize = 512;
        private readonly List<(float x, float y)> roses = new List<(float, float)>();
        private readonly List<(float u, float v, float rx, float ry)> keepClear = new List<(float, float, float, float)>();

        /// <summary>What the painter measured, for the readouts and the pull request.</summary>
        public readonly Dictionary<string, long> Milliseconds = new Dictionary<string, long>();
        public int GlyphCount { get; private set; }

        public ChartPainter(WorldMap map, ChartRaster raster, int seed)
        {
            this.map = map; this.raster = raster; this.seed = seed; s = raster.Scale;
        }

        public ChartCanvas Paint()
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            canvas = new ChartCanvas(raster.Width, raster.Height);
            coastDist = ChartRaster.DistanceToOther(raster.Ids, raster.Width, raster.Height, (a, b) => ChartRaster.IsWater(a) != ChartRaster.IsWater(b));
            FindCliffs();
            BuildFields();
            BuildTables();
            PlaceRoses();
            Milliseconds["fields"] = watch.ElapsedMilliseconds; watch.Restart();
            Ground();
            Milliseconds["ground"] = watch.ElapsedMilliseconds; watch.Restart();
            Glyphs();
            Rose(roses[0].x, roses[0].y, Math.Min(5.5f * s, coastDistAt(roses[0]) * 0.6f));
            Milliseconds["glyphs"] = watch.ElapsedMilliseconds;
            return canvas;
        }

        private float coastDistAt((float x, float y) p) => coastDist[(int)p.y * raster.Width + (int)p.x];

        // ---- fields ----

        private void FindCliffs()
        {
            // A south-facing coast shows a cliff face below it, as on the painting: for each water
            // pixel, how far below the nearest land pixel straight above it.
            int w = raster.Width, h = raster.Height, depth = Math.Max(2, (int)(0.55f * s));
            cliff = new int[w * h];
            Parallel.For(0, w, x =>
            {
                int lastLand = -100000;
                for (int y = 0; y < h; y++)
                {
                    int i = y * w + x;
                    if (!ChartRaster.IsWater(raster.Ids[i])) { lastLand = y; continue; }
                    int k = y - lastLand;
                    if (k <= depth) cliff[i] = k;
                }
            });
        }

        private void BuildFields()
        {
            int gw = raster.GridWidth, gh = raster.GridHeight;
            terrainField = new float[gw * gh * 3];
            heightField = new float[gw * gh];
            var cells = raster.Cells;
            for (int i = 0; i < cells.Length; i++)
            {
                Ink c; float hgt;
                if (cells[i] >= 0)
                {
                    var p = map.Provinces[cells[i]];
                    c = TerrainTone(p.Terrain);
                    float n = ChartNoise.Fbm((i % gw) * 0.21f, (i / gw) * 0.21f, seed + 71);
                    c = Ink.Lerp(c, c.Times(0.82f), n);
                    hgt = TerrainHeight(p.Terrain) * (0.7f + 0.6f * ChartNoise.Value((i % gw) * 0.5f, (i / gw) * 0.5f, seed + 72));
                }
                else { c = Sand; hgt = -0.2f; }
                terrainField[i * 3] = c.R; terrainField[i * 3 + 1] = c.G; terrainField[i * 3 + 2] = c.B;
                heightField[i] = hgt;
            }
            foreach (var ridge in map.Geometry.Ridges)
            {
                // Ridges lift the land along their line.
                float ax = ridge.Line[0], ay = ridge.Line[1], bx = ridge.Line[2], by = ridge.Line[3];
                for (int y = 0; y < gh; y++)
                for (int x = 0; x < gw; x++)
                {
                    if (cells[y * gw + x] < 0) continue;
                    float d = SegmentDistance(x + 0.5f, y + 0.5f, ax, ay, bx, by);
                    if (d < 3) heightField[y * gw + x] += 0.7f * (1 - d / 3);
                }
            }
            for (int pass = 0; pass < 2; pass++) { Blur(terrainField, gw, gh, 3, 1); Blur(heightField, gw, gh, 1, 2); }
        }

        private static void Blur(float[] f, int w, int h, int channels, int radius)
        {
            var tmp = new float[f.Length];
            int n = 2 * radius + 1;
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            for (int c = 0; c < channels; c++)
            {
                float sum = 0;
                for (int k = -radius; k <= radius; k++) sum += f[(y * w + Math.Max(0, Math.Min(w - 1, x + k))) * channels + c];
                tmp[(y * w + x) * channels + c] = sum / n;
            }
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            for (int c = 0; c < channels; c++)
            {
                float sum = 0;
                for (int k = -radius; k <= radius; k++) sum += tmp[(Math.Max(0, Math.Min(h - 1, y + k)) * w + x) * channels + c];
                f[(y * w + x) * channels + c] = sum / n;
            }
        }

        /// <summary>
        /// The per-pixel work done once: the relief's light folded into the terrain tone per cell,
        /// the paper grain and the sea pattern as tiles. The ground pass then only looks them up,
        /// which keeps a 4800-wide chart to a few seconds in the editor's Mono runtime.
        /// </summary>
        private void BuildTables()
        {
            int gw = raster.GridWidth, gh = raster.GridHeight;
            litField = new float[gw * gh * 3];
            for (int y = 0; y < gh; y++)
            for (int x = 0; x < gw; x++)
            {
                float u = x + 0.5f, v = y + 0.5f;
                float hx = Sample(heightField, 1, 0, u + 0.7f, v) - Sample(heightField, 1, 0, u - 0.7f, v);
                float hy = Sample(heightField, 1, 0, u, v + 0.7f) - Sample(heightField, 1, 0, u, v - 0.7f);
                float shade = Math.Max(0.7f, Math.Min(1.25f, 1 - 0.55f * (hx + hy)));
                int i = (y * gw + x) * 3;
                for (int c = 0; c < 3; c++) litField[i + c] = terrainField[i + c] * shade;
            }
            grain = new float[GrainSize * GrainSize];
            float perSample = GrainPeriod / (float)GrainSize;
            Parallel.For(0, GrainSize, y =>
            {
                for (int x = 0; x < GrainSize; x++)
                {
                    float u = x * perSample, v = y * perSample;
                    // 1.7 and 9 waves a cell, made to tile: 14 and 72 over the 8-cell period.
                    float g = ChartNoise.Fbm(u * 14f / GrainPeriod, v * 14f / GrainPeriod, seed + 3, 14);
                    float fine = ChartNoise.Value(u * 72f / GrainPeriod, v * 72f / GrainPeriod, seed + 4, 72);
                    grain[y * GrainSize + x] = 0.86f + 0.2f * g + 0.08f * fine;
                }
            });
            seaTile = SeaTile(SeaTileSize);
        }

        private float Grain(float u, float v)
        {
            int x = (int)(u * (GrainSize / (float)GrainPeriod)) & (GrainSize - 1), y = (int)(v * (GrainSize / (float)GrainPeriod)) & (GrainSize - 1);
            return grain[y * GrainSize + x];
        }

        private Ink Sea(float u, float v)
        {
            int x = (int)(u * (SeaTileSize / (float)SeaPeriodCells)) & (SeaTileSize - 1), y = (int)(v * (SeaTileSize / (float)SeaPeriodCells)) & (SeaTileSize - 1);
            int k = (y * SeaTileSize + x) * 4;
            return new Ink(seaTile[k], seaTile[k + 1], seaTile[k + 2]);
        }

        private float Sample(float[] f, int channels, int c, float u, float v)
        {
            int gw = raster.GridWidth, gh = raster.GridHeight;
            u -= 0.5f; v -= 0.5f;
            int x0 = (int)Math.Floor(u), y0 = (int)Math.Floor(v);
            float fx = u - x0, fy = v - y0;
            int X(int x) => x < 0 ? 0 : x >= gw ? gw - 1 : x;
            int Y(int y) => y < 0 ? 0 : y >= gh ? gh - 1 : y;
            float a = f[(Y(y0) * gw + X(x0)) * channels + c], b = f[(Y(y0) * gw + X(x0 + 1)) * channels + c];
            float d = f[(Y(y0 + 1) * gw + X(x0)) * channels + c], e = f[(Y(y0 + 1) * gw + X(x0 + 1)) * channels + c];
            return (a + (b - a) * fx) * (1 - fy) + (d + (e - d) * fx) * fy;
        }

        private void PlaceRoses()
        {
            // The rose in the widest open sea toward the bottom right; rhumb lines also from the top left.
            int w = raster.Width, h = raster.Height;
            (float, float) Best(int x0, int y0, int x1, int y1)
            {
                float best = -1; (float, float) at = ((x0 + x1) / 2f, (y0 + y1) / 2f);
                int step = Math.Max(1, (int)(s / 2));
                for (int y = y0; y < y1; y += step)
                for (int x = x0; x < x1; x += step)
                {
                    int i = y * w + x;
                    if (raster.Ids[i] != ChartRaster.Sea) continue;
                    // Keep off the very edge, where the chart fades into the open sea.
                    float edge = Math.Min(Math.Min(x, w - x), Math.Min(y, h - y));
                    float score = Math.Min(coastDist[i], edge * 0.8f);
                    if (score > best) { best = score; at = (x, y); }
                }
                return at;
            }
            roses.Add(Best(w / 2, h / 2, w, h));
            roses.Add(Best(0, 0, w / 2, h / 2));
        }

        // ---- the ground: one pass over every pixel ----

        private void Ground()
        {
            int w = raster.Width;
            float coastLine = Math.Max(1.1f, 0.09f * s);
            Parallel.For(0, raster.Height, y =>
            {
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;
                    ushort id = raster.Ids[i];
                    float u = (x + 0.5f) / s, v = (y + 0.5f) / s;
                    float d = coastDist[i];
                    Ink c;
                    if (!ChartRaster.IsWater(id))
                    {
                        // The terrain's tone under the relief's light (upper left), then paper grain and pigment.
                        c = new Ink(Sample(litField, 3, 0, u, v), Sample(litField, 3, 1, u, v), Sample(litField, 3, 2, u, v));
                        c = c.Times(Grain(u, v));
                        // The coast: a pale rim, then the ink line.
                        float dc = d / s;
                        if (dc < 0.6f) c = Ink.Lerp(c, Sand, 0.35f * (1 - dc / 0.6f));
                        float line = 1 - Math.Max(0, Math.Min(1, d - coastLine));
                        if (line > 0) c = Ink.Lerp(c, InkDark, 0.85f * line);
                    }
                    else
                    {
                        bool lake = id == ChartRaster.Water;
                        c = Sea(u, v);
                        float dc = d / s;
                        if (lake) c = Ink.Lerp(c, LakeTone, 0.75f);
                        // Shallows lighter near the shore.
                        if (dc < 1.6f) c = Ink.Lerp(c, lake ? LakeTone.Times(1.15f) : SeaShallow, 0.55f * (1 - dc / 1.6f));
                        // Ripples: contour lines following the coast, fading outward.
                        if (!lake || dc < 1.0f)
                        {
                            float wob = dc + 0.12f * (ChartNoise.Value(u * 0.8f, v * 0.8f, seed + 9) - 0.5f);
                            float[] rings = { 0.45f, 1.0f, 1.65f, 2.45f, 3.4f };
                            for (int r = 0; r < rings.Length; r++)
                            {
                                float px = Math.Abs(wob - rings[r]) * s;
                                float lw = Math.Max(0.9f, 0.05f * s);
                                float a = Math.Max(0, Math.Min(1, lw / 2 - px + 0.5f)) * (0.42f - r * 0.07f);
                                if (a > 0) c = Ink.Lerp(c, SeaLine, a);
                            }
                        }
                        // Rhumb lines from the roses, only over the sea.
                        if (!lake)
                            foreach (var (rx, ry) in roses)
                            {
                                float dx = x + 0.5f - rx, dy = y + 0.5f - ry;
                                float dist = (float)Math.Sqrt(dx * dx + dy * dy);
                                if (dist < 1) continue;
                                float ang = (float)Math.Atan2(dy, dx);
                                float step = (float)(Math.PI / 8);
                                float off = ang / step; off -= (float)Math.Round(off);
                                float perp = Math.Abs(off) * step * dist;
                                float lw = Math.Max(0.8f, 0.035f * s);
                                float a = Math.Max(0, Math.Min(1, lw / 2 - perp + 0.5f)) * 0.22f * Math.Min(1, 30 * s / (dist + 30 * s) * 1.6f);
                                if (a > 0) c = Ink.Lerp(c, Gilt, a);
                            }
                        // A cliff face under a south-facing coast.
                        int k = cliff[i];
                        if (k > 0 && !lake)
                        {
                            float depth = Math.Max(2, 0.55f * s);
                            float t = k / depth;
                            float stripe = ChartNoise.Value(u * 6f, v * 1.2f, seed + 5);
                            var face = Ink.Lerp(Cliff, CliffDark, t * 0.8f + 0.3f * stripe);
                            c = Ink.Lerp(c, face, 0.92f * (1 - t * t));
                        }
                        float line = 1 - Math.Max(0, Math.Min(1, d - coastLine));
                        if (line > 0) c = Ink.Lerp(c, InkDark, 0.85f * line);
                    }
                    canvas.Set(i, c);
                }
            });
        }

        /// <summary>The sea's own texture: a dark chart tone with engraved wave marks, tiling every <paramref name="period"/> cells.</summary>
        public static Ink SeaPattern(float u, float v, int period, float pixelCells)
        {
            float tu = u / period, tv = v / period;
            float n = ChartNoise.Fbm(tu * 6, tv * 6, 401, 6);
            var c = Ink.Lerp(SeaDeep, SeaDeep.Times(1.22f), n);
            // Wave marks: small arcs on a staggered grid, 30 rows to a tile.
            const int rows = 30, cols = 22;
            float gy = tv * rows, gyFloor = (float)Math.Floor(gy);
            int row = (int)gyFloor;
            float gx = tu * cols + (row % 2) * 0.5f, gxFloor = (float)Math.Floor(gx);
            int col = (int)gxFloor;
            int hr = ((row % rows) + rows) % rows, hc = ((col % cols) + cols) % cols;
            float pick = ChartNoise.Hash01(hc, hr, 402);
            if (pick < 0.55f)
            {
                float fx = gx - gxFloor - 0.5f + (ChartNoise.Hash01(hc, hr, 403) - 0.5f) * 0.4f, fy = gy - gyFloor - 0.62f;
                // In cell units of the wave grid: an arc of radius 0.28, upper half only.
                float ax = fx * period / cols, ay = fy * period / rows;
                float r = 0.26f * period / cols * (0.8f + 0.4f * pick);
                float dist = Math.Abs((float)Math.Sqrt(ax * ax + ay * ay) - r);
                float hw = Math.Max(0.03f, 0.55f * pixelCells);
                if (ay < 0.02f && dist < hw) c = Ink.Lerp(c, SeaLine, 0.3f * (1 - dist / hw) * (0.6f + 0.6f * n) * Math.Min(1, 0.05f / hw));
            }
            return c;
        }

        // ---- glyphs ----

        private sealed class Glyph
        {
            public float U, V, Size;
            public Action<float, float, float> Draw;
        }

        private void Glyphs()
        {
            var glyphs = new List<Glyph>();
            var provinces = map.Provinces;
            var geo = map.Geometry;

            // What must stay clear: each province's name, its settlement, its special place.
            var settlementAt = new (float u, float v)[provinces.Count];
            for (int p = 0; p < provinces.Count; p++)
            {
                var shape = geo.Provinces[p];
                float lu = shape.Label[0] + 0.5f, lv = shape.Label[1] + 0.5f;
                string name = provinces[p].Name ?? provinces[p].Id;
                keepClear.Add((lu, lv, Math.Max(1.4f, 0.36f * name.Length), 0.95f));
                // The settlement stands above the name.
                settlementAt[p] = (lu, lv - 1.75f);
                keepClear.Add((settlementAt[p].u, settlementAt[p].v - 1.0f, 3.8f, 2.6f));
            }

            // Special places beside their province's name, where they fit.
            var specials = new List<(float u, float v, string template)>();
            foreach (var sp in map.Specials)
            {
                int p = provinces.FindIndex(x => x.Id == sp.Province);
                if (p < 0) continue;
                var shape = geo.Provinces[p];
                float lu = shape.Label[0] + 0.5f, lv = shape.Label[1] + 0.5f;
                (float u, float v) at = (lu + 3.4f, lv - 1.4f);
                foreach (var (du, dv) in new[] { (3.4f, -1.4f), (-3.4f, -1.4f), (0f, 2.2f), (3.6f, 1.0f), (-3.6f, 1.0f), (0f, -4.2f) })
                    if (Inside(lu + du, lv + dv, p, 0.6f)) { at = (lu + du, lv + dv); break; }
                specials.Add((at.u, at.v, sp.Template));
                keepClear.Add((at.u, at.v - 0.7f, 1.6f, 1.6f));
            }

            // Ridges: a chain of peaks along each line, open at its passes.
            var passPoints = new List<(float u, float v)>();
            foreach (var b in geo.Borders.Where(b => b.Kind == BorderLine.Pass))
            {
                var line = b.Lines.OrderByDescending(l => l.Length).First();
                int mid = (line.Length / 4) * 2;
                passPoints.Add((line[mid], line[mid + 1]));
            }
            var ridgeCells = new List<(float u, float v)>();
            foreach (var ridge in geo.Ridges)
            {
                float ax = ridge.Line[0], ay = ridge.Line[1], bx = ridge.Line[2], by = ridge.Line[3];
                float len = (float)Math.Sqrt((bx - ax) * (bx - ax) + (by - ay) * (by - ay));
                int count = (int)(len / 1.7f);
                float nx = -(by - ay) / Math.Max(1e-3f, len), ny = (bx - ax) / Math.Max(1e-3f, len);
                int salt = (int)(ax * 31 + ay * 17);
                for (int k = 0; k <= count; k++)
                {
                    float t = count == 0 ? 0 : k / (float)count;
                    // The line bends with slow noise and the peaks scatter across it, so it reads as a range, not a rule.
                    float bend = (ChartNoise.Value(t * len * 0.09f, 3.7f, seed + 82 + salt) - 0.5f) * 5f;
                    float across = bend + (ChartNoise.Hash01(k, 1, seed + 81 + salt) - 0.5f) * 2.2f;
                    float u = ax + (bx - ax) * t + nx * across + (ChartNoise.Hash01(k, 2, seed + 81 + salt) - 0.5f) * 0.8f;
                    float v = ay + (by - ay) * t + ny * across;
                    ridgeCells.Add((u, v));
                    if (IsWaterAt(u, v) || IsWaterAt(u, v + 0.8f)) continue;
                    if (passPoints.Any(pp => Dist(pp.u, pp.v, u, v) < 2.6f)) continue;
                    if (Clear(u, v, 0.5f) == false) continue;
                    float size = 3.0f + 1.6f * ChartNoise.Hash01(k, 3, seed + 81 + salt);
                    glyphs.Add(new Glyph { U = u, V = v, Size = size, Draw = (gu, gv, gs) => Mountain(gu, gv, gs, true) });
                }
            }
            foreach (var (pu, pv) in passPoints) glyphs.Add(new Glyph { U = pu, V = pv + 0.01f, Size = 1, Draw = (gu, gv, gs) => Pass(gu, gv) });

            // Terrain, by Poisson rejection over a jittered grid of candidates in each province.
            var cells = raster.Cells;
            int gw = raster.GridWidth;
            var accepted = new List<(float u, float v, float r)>[provinces.Count];
            for (int p = 0; p < provinces.Count; p++) accepted[p] = new List<(float, float, float)>();
            for (int i = 0; i < cells.Length; i++)
            {
                int p = cells[i];
                if (p < 0) continue;
                var t = provinces[p].Terrain;
                float density, spacing, margin;
                switch (t)
                {
                    case Terrain.Forest: density = 0.8f; spacing = 1.05f; margin = 0.7f; break;
                    case Terrain.Mountains: density = 0.35f; spacing = 2.5f; margin = 1.6f; break;
                    case Terrain.Hills: density = 0.3f; spacing = 2.6f; margin = 1.4f; break;
                    case Terrain.Marsh: density = 0.3f; spacing = 1.7f; margin = 0.9f; break;
                    default: density = 0.13f; spacing = 2.8f; margin = 1.3f; break;
                }
                int gx = i % gw, gy = i / gw;
                if (ChartNoise.Hash01(gx, gy, seed + 90) > density) continue;
                float u = gx + 0.15f + 0.7f * ChartNoise.Hash01(gx, gy, seed + 91);
                float v = gy + 0.15f + 0.7f * ChartNoise.Hash01(gx, gy, seed + 92);
                if (accepted[p].Any(a => Dist(a.u, a.v, u, v) < Math.Max(spacing, a.r))) continue;
                if (ridgeCells.Any(rc => Dist(rc.u, rc.v, u, v) < 2.4f)) continue;
                if (InKeepClear(u, v)) continue;
                if (!Inside(u, v, p, margin)) continue;
                accepted[p].Add((u, v, spacing));
                float roll = ChartNoise.Hash01(gx, gy, seed + 93);
                switch (t)
                {
                    case Terrain.Forest:
                        glyphs.Add(new Glyph { U = u, V = v, Size = 1.45f + 0.5f * roll, Draw = roll < 0.68f ? (Action<float, float, float>)Conifer : RoundTree });
                        break;
                    case Terrain.Mountains:
                        glyphs.Add(new Glyph { U = u, V = v, Size = 2.9f + 1.2f * roll, Draw = (gu, gv, gs) => Mountain(gu, gv, gs, false) });
                        break;
                    case Terrain.Hills:
                        glyphs.Add(new Glyph { U = u, V = v, Size = 2.3f + 0.8f * roll, Draw = Hill });
                        break;
                    case Terrain.Marsh:
                        glyphs.Add(new Glyph { U = u, V = v, Size = 1.6f + 0.5f * roll, Draw = Marsh });
                        break;
                    default:
                        if (roll < 0.35f) glyphs.Add(new Glyph { U = u, V = v, Size = 1.3f + 0.4f * roll, Draw = RoundTree });
                        else if (roll < 0.75f) glyphs.Add(new Glyph { U = u, V = v, Size = 1f, Draw = (gu, gv, gs) => Fields(gu, gv, roll) });
                        break;
                }
            }

            // Settlements by population, capitals as towns with walls; ports; special places.
            for (int p = 0; p < provinces.Count; p++)
            {
                var pr = provinces[p];
                var (su, sv) = settlementAt[p];
                if (!Inside(su, sv, p, 0.2f)) { su = geo.Provinces[p].Centre[0] + 0.5f; sv = geo.Provinces[p].Centre[1] + 0.5f; }
                int tier = pr.Capital ? 3 : pr.Population >= 6 ? 2 : pr.Population >= 4 ? 1 : 0;
                int pp = p;
                glyphs.Add(new Glyph { U = su, V = sv, Size = 1, Draw = (gu, gv, gs) => Settlement(gu, gv, tier, pp) });
                if (pr.Port && FindShore(p, out float hu, out float hv)) glyphs.Add(new Glyph { U = hu, V = hv, Size = 1, Draw = (gu, gv, gs) => Anchor(gu, gv) });
            }
            foreach (var (su, sv, template) in specials)
                glyphs.Add(new Glyph { U = su, V = sv + 0.02f, Size = 1, Draw = (gu, gv, gs) => Special(gu, gv, template) });

            // Back to front, so a lower glyph overlaps the one above it.
            foreach (var g in glyphs.OrderBy(g => g.V)) g.Draw(g.U * s, g.V * s, g.Size);
            GlyphCount = glyphs.Count;
        }

        private bool InKeepClear(float u, float v)
        {
            foreach (var (cu, cv, rx, ry) in keepClear)
            {
                float dx = (u - cu) / rx, dy = (v - cv) / ry;
                if (dx * dx + dy * dy < 1) return true;
            }
            return false;
        }

        private bool IsWaterAt(float u, float v)
        {
            int x = (int)(u * s), y = (int)(v * s);
            if (x < 0 || y < 0 || x >= raster.Width || y >= raster.Height) return true;
            return ChartRaster.IsWater(raster.Ids[y * raster.Width + x]);
        }

        /// <summary>Whether a point and a ring around it all lie in province <paramref name="p"/>, as drawn.</summary>
        private bool Inside(float u, float v, int p, float margin)
        {
            if (Id(u, v) != p) return false;
            for (int k = 0; k < 8; k++)
            {
                double a = k * Math.PI / 4;
                if (Id(u + margin * (float)Math.Cos(a), v + margin * (float)Math.Sin(a)) != p) return false;
            }
            return true;
        }

        private bool? Clear(float u, float v, float margin)
        {
            int id = Id(u, v);
            if (id < 0) return false;
            for (int k = 0; k < 8; k++)
            {
                double a = k * Math.PI / 4;
                if (Id(u + margin * (float)Math.Cos(a), v + margin * (float)Math.Sin(a)) < 0) return false;
            }
            return true;
        }

        private int Id(float u, float v)
        {
            int x = (int)(u * s), y = (int)(v * s);
            if (x < 0 || y < 0 || x >= raster.Width || y >= raster.Height) return -1;
            ushort id = raster.Ids[y * raster.Width + x];
            return ChartRaster.IsWater(id) ? -1 : id;
        }

        /// <summary>A point of province <paramref name="p"/> on its sea coast nearest its centre, for the port's anchor.</summary>
        private bool FindShore(int p, out float u, out float v)
        {
            var c = map.Geometry.Provinces[p].Centre;
            u = v = 0;
            float best = float.MaxValue;
            int gw = raster.GridWidth, gh = raster.GridHeight;
            var cells = raster.Cells;
            for (int y = 1; y < gh - 1; y++)
            for (int x = 1; x < gw - 1; x++)
            {
                if (cells[y * gw + x] != p) continue;
                bool shore = cells[y * gw + x - 1] == MapGeometry.Sea || cells[y * gw + x + 1] == MapGeometry.Sea || cells[(y - 1) * gw + x] == MapGeometry.Sea || cells[(y + 1) * gw + x] == MapGeometry.Sea;
                if (!shore) continue;
                float d = Dist(x, y, c[0], c[1]);
                if (d < best) { best = d; u = x + 0.5f; v = y + 0.5f; }
            }
            if (best == float.MaxValue) return false;
            // Step inland until the anchor sits fully on land.
            float du = c[0] + 0.5f - u, dv = c[1] + 0.5f - v, len = (float)Math.Sqrt(du * du + dv * dv);
            if (len > 0) { u += du / len * 0.9f; v += dv / len * 0.9f; }
            return true;
        }

        private static float Dist(float au, float av, float bu, float bv) => (float)Math.Sqrt((au - bu) * (au - bu) + (av - bv) * (av - bv));

        private static float SegmentDistance(float px, float py, float ax, float ay, float bx, float by)
        {
            float vx = bx - ax, vy = by - ay, l2 = vx * vx + vy * vy;
            float t = l2 > 0 ? Math.Max(0, Math.Min(1, ((px - ax) * vx + (py - ay) * vy) / l2)) : 0;
            return Dist(px, py, ax + vx * t, ay + vy * t);
        }

        // ---- the glyphs themselves; x, y in pixels at the glyph's foot, size in cells ----

        private float Stroke(float cells) => Math.Max(0.7f, cells * s);

        private void Conifer(float x, float y, float size)
        {
            float h = 1.0f * size * s, w = 0.36f * size * s;
            var dark = new Ink(44, 62, 34); var light = new Ink(84, 104, 52);
            canvas.Disc(x + w * 0.3f, y + 0.05f * s, w * 0.75f, InkDark, 0.18f); // shadow
            canvas.Line(x, y, x, y - h * 0.25f, Stroke(0.07f), InkSoft);
            canvas.Triangle(x, y - h, x - w, y - h * 0.2f, x, y - h * 0.2f, light);
            canvas.Triangle(x, y - h, x, y - h * 0.2f, x + w, y - h * 0.2f, dark);
            canvas.Line(x, y - h, x - w, y - h * 0.2f, Stroke(0.045f), InkDark, 0.55f);
            canvas.Line(x, y - h, x + w, y - h * 0.2f, Stroke(0.045f), InkDark, 0.7f);
            canvas.Line(x - w, y - h * 0.2f, x + w, y - h * 0.2f, Stroke(0.045f), InkDark, 0.6f);
        }

        private void RoundTree(float x, float y, float size)
        {
            float r = 0.34f * size * s;
            canvas.Disc(x + r * 0.35f, y - r * 0.2f, r * 0.95f, InkDark, 0.2f);
            canvas.Line(x, y, x, y - r * 1.2f, Stroke(0.07f), InkSoft);
            canvas.Disc(x, y - r * 1.5f, r, new Ink(66, 86, 40));
            canvas.Disc(x - r * 0.28f, y - r * 1.75f, r * 0.58f, new Ink(108, 124, 60), 0.85f);
            canvas.Ring(x, y - r * 1.5f, r, Stroke(0.045f), InkDark, 0.6f);
        }

        private void Mountain(float x, float y, float size, bool ridge)
        {
            float h = (ridge ? 1.05f : 0.85f) * size * s, w = 0.62f * size * s;
            float ax = x + (ridge ? 0 : 0.08f * w), ay = y - h;
            var lit = new Ink(178, 162, 130); var shade = new Ink(102, 88, 70);
            canvas.Triangle(x - w * 0.2f, y, x + w * 1.15f, y, ax + w * 0.2f, ay + h * 0.3f, InkDark, 0.16f); // shadow
            canvas.Triangle(ax, ay, x - w, y, ax + w * 0.05f, y, lit);
            canvas.Triangle(ax, ay, ax + w * 0.05f, y, x + w, y, shade);
            // Hatching on the shaded side.
            for (int k = 1; k <= 3; k++)
            {
                float t = k / 4f;
                float sx = ax + (x + w - ax) * t, sy = ay + (y - ay) * t;
                canvas.Line(sx, sy, sx - w * 0.28f, sy + h * 0.32f, Stroke(0.035f), InkDark, 0.45f);
            }
            if (size > 1.45f) canvas.Triangle(ax, ay, ax - w * 0.22f, ay + h * 0.24f, ax + w * 0.2f, ay + h * 0.22f, new Ink(226, 220, 204), 0.9f);
            canvas.Line(ax, ay, x - w, y, Stroke(0.055f), InkDark, 0.85f);
            canvas.Line(ax, ay, x + w, y, Stroke(0.055f), InkDark, 0.85f);
        }

        private void Hill(float x, float y, float size)
        {
            float rx = 0.7f * size * s, ry = 0.42f * size * s;
            canvas.Dome(x + rx * 0.15f, y + 0.04f * s, rx, ry, InkDark, 0.14f);
            canvas.Dome(x, y, rx, ry, new Ink(162, 142, 96), 1, -1);
            canvas.Dome(x, y, rx, ry, new Ink(120, 102, 68), 1, 1);
            canvas.Arc(x, y, rx, ry, 0, (float)Math.PI, Stroke(0.05f), InkDark, 0.8f);
            canvas.Arc(x + rx * 0.1f, y, rx * 0.55f, ry * 0.55f, 0.2f, 1.3f, Stroke(0.035f), InkDark, 0.4f, 5);
        }

        private void Marsh(float x, float y, float size)
        {
            float w = 0.42f * size * s;
            var water = new Ink(74, 98, 102); var reed = new Ink(56, 70, 40);
            canvas.Line(x - w, y, x + w, y, Stroke(0.06f), water, 0.85f);
            canvas.Line(x - w * 0.6f, y + 0.18f * s, x + w * 0.4f, y + 0.18f * s, Stroke(0.05f), water, 0.7f);
            for (int k = -1; k <= 1; k++)
                canvas.Line(x + k * 0.12f * s * size, y - 0.02f * s, x + k * 0.22f * s * size, y - 0.42f * s * size, Stroke(0.045f), reed, 0.9f);
        }

        private void Fields(float x, float y, float roll)
        {
            // A patch of two or three strips, each with furrows, at a slant.
            float ang = (roll - 0.5f) * 1.2f;
            float cos = (float)Math.Cos(ang), sin = (float)Math.Sin(ang);
            int strips = roll > 0.65f ? 3 : 2;
            float len = 2.3f * s, wid = 0.62f * s;
            for (int k = 0; k < strips; k++)
            {
                float ox = -sin * wid * (k - strips / 2f + 0.5f), oy = cos * wid * (k - strips / 2f + 0.5f);
                var tone = k % 2 == 0 ? new Ink(184, 164, 92) : new Ink(132, 138, 68);
                float cx = x + ox, cy = y + oy;
                float ax = cx - cos * len / 2, ay = cy - sin * len / 2, bx = cx + cos * len / 2, by = cy + sin * len / 2;
                canvas.Line(ax, ay, bx, by, wid * 0.92f, tone, 0.55f);
                for (int f = -1; f <= 1; f++)
                {
                    float fx = -sin * wid * 0.28f * f, fy = cos * wid * 0.28f * f;
                    canvas.Line(ax + fx, ay + fy, bx + fx, by + fy, Stroke(0.025f), InkSoft, 0.35f);
                }
            }
        }

        private void House(float x, float y, float w, Ink roof)
        {
            float h = w * 0.7f;
            canvas.Rect(x - w / 2, y - h, x + w / 2, y, new Ink(206, 188, 152));
            canvas.Triangle(x - w * 0.62f, y - h, x + w * 0.62f, y - h, x, y - h - w * 0.62f, roof);
            canvas.Line(x - w * 0.62f, y - h, x, y - h - w * 0.62f, Stroke(0.03f), InkDark, 0.7f);
            canvas.Line(x + w * 0.62f, y - h, x, y - h - w * 0.62f, Stroke(0.03f), InkDark, 0.7f);
            canvas.Line(x - w / 2, y, x + w / 2, y, Stroke(0.03f), InkDark, 0.6f);
        }

        private void Settlement(float x, float y, int tier, int province)
        {
            var roof = new Ink(142, 62, 42); var roof2 = new Ink(116, 70, 50);
            float u = 1.15f * s;
            switch (tier)
            {
                case 0:
                    canvas.Disc(x, y - 0.12f * s, 0.16f * s, InkDark, 0.85f);
                    House(x, y, u * 0.9f, roof);
                    break;
                case 1:
                    House(x - u * 0.9f, y - u * 0.15f, u, roof2);
                    House(x + u * 0.85f, y - u * 0.3f, u * 0.9f, roof);
                    House(x, y + u * 0.2f, u, roof);
                    break;
                default:
                    bool capital = tier == 3;
                    float k = capital ? 1.15f : 1f;
                    if (capital) canvas.Ring(x, y - u * 0.6f, u * 2.5f, Stroke(0.07f), InkDark, 0.75f);
                    House(x - u * 1.35f * k, y - u * 0.5f, u * k, roof2);
                    House(x + u * 1.3f * k, y - u * 0.55f, u * 0.95f * k, roof);
                    // The spire.
                    float sx = x + u * 0.1f, sy = y - u * 0.4f;
                    canvas.Rect(sx - u * 0.28f, sy - u * 1.1f, sx + u * 0.28f, sy, new Ink(198, 182, 150));
                    canvas.Triangle(sx - u * 0.36f, sy - u * 1.1f, sx + u * 0.36f, sy - u * 1.1f, sx, sy - u * 2.4f * k, roof2);
                    canvas.Line(sx, sy - u * 2.4f * k, sx, sy - u * 2.75f * k, Stroke(0.03f), InkDark);
                    House(x - u * 0.6f, y + u * 0.35f, u * 1.05f, roof);
                    House(x + u * 0.75f, y + u * 0.3f, u, roof2);
                    if (capital) House(x - u * 1.9f, y + u * 0.25f, u * 0.85f, roof);
                    break;
            }
        }

        private void Anchor(float x, float y)
        {
            float u = 0.6f * s, lw = Stroke(0.11f);
            var ink = new Ink(30, 36, 44);
            canvas.Disc(x, y, u * 1.35f, new Ink(214, 200, 164), 0.55f);
            canvas.Line(x, y - u, x, y + u * 0.8f, lw, ink);
            canvas.Ring(x, y - u * 1.15f, u * 0.22f, lw * 0.8f, ink);
            canvas.Line(x - u * 0.45f, y - u * 0.55f, x + u * 0.45f, y - u * 0.55f, lw, ink);
            canvas.Arc(x, y + u * 0.15f, u * 0.75f, u * 0.65f, (float)Math.PI, (float)(2 * Math.PI), lw, ink, 1, 8);
        }

        private void Pass(float x, float y)
        {
            // Two brackets facing each other: the way through.
            float u = 0.7f * s, lw = Stroke(0.1f);
            canvas.Arc(x - u * 1.1f, y, u * 0.55f, u * 0.9f, -1.1f, 1.1f, lw, InkDark, 0.9f, 6);
            canvas.Arc(x + u * 1.1f, y, u * 0.55f, u * 0.9f, (float)Math.PI - 1.1f, (float)Math.PI + 1.1f, lw, InkDark, 0.9f, 6);
        }

        private void Special(float x, float y, string template)
        {
            float u = 0.8f * s, lw = Stroke(0.12f);
            // A gilt seal under every special place, so they read as one family.
            canvas.Disc(x, y - u * 0.9f, u * 1.5f, InkDark, 0.35f);
            canvas.Disc(x, y - u * 0.9f, u * 1.38f, Gilt, 0.95f);
            canvas.Ring(x, y - u * 0.9f, u * 1.38f, lw * 0.8f, InkDark, 0.9f);
            float cx = x, cy = y - u * 0.9f;
            var ink = InkDark;
            switch (template)
            {
                case "old_castle":
                    canvas.Rect(cx - u * 0.55f, cy - u * 0.5f, cx + u * 0.55f, cy + u * 0.75f, ink);
                    for (int k = -1; k <= 1; k++) canvas.Rect(cx + k * u * 0.4f - u * 0.13f, cy - u * 0.8f, cx + k * u * 0.4f + u * 0.13f, cy - u * 0.45f, ink);
                    break;
                case "silver_mine":
                case "iron_deep":
                    var head = template == "silver_mine" ? new Ink(232, 232, 236) : ink;
                    canvas.Line(cx - u * 0.7f, cy + u * 0.7f, cx + u * 0.55f, cy - u * 0.55f, lw, ink);
                    canvas.Line(cx + u * 0.7f, cy + u * 0.7f, cx - u * 0.55f, cy - u * 0.55f, lw, ink);
                    canvas.Arc(cx + u * 0.5f, cy - u * 0.5f, u * 0.5f, u * 0.5f, 0.4f, 2.3f, lw * 1.2f, head, 1, 5);
                    canvas.Arc(cx - u * 0.5f, cy - u * 0.5f, u * 0.5f, u * 0.5f, 0.84f, 2.74f, lw * 1.2f, head, 1, 5);
                    break;
                case "sacred_lake":
                    canvas.Disc(cx, cy + u * 0.1f, u * 0.75f, new Ink(60, 96, 120));
                    canvas.Ring(cx, cy + u * 0.1f, u * 0.75f, lw, ink);
                    canvas.Disc(cx, cy - u * 0.7f, u * 0.16f, new Ink(250, 244, 220));
                    break;
                case "healing_spring":
                    canvas.Disc(cx, cy + u * 0.25f, u * 0.48f, new Ink(60, 110, 140));
                    canvas.Triangle(cx - u * 0.42f, cy + u * 0.05f, cx + u * 0.42f, cy + u * 0.05f, cx, cy - u * 0.75f, new Ink(60, 110, 140));
                    break;
                case "horse_plains":
                    canvas.Arc(cx, cy, u * 0.6f, u * 0.7f, (float)Math.PI * 0.95f, (float)Math.PI * 2.05f, lw * 1.6f, ink, 1, 10);
                    break;
                case "great_wood":
                    canvas.Disc(cx, cy - u * 0.15f, u * 0.62f, new Ink(54, 80, 36));
                    canvas.Line(cx, cy + u * 0.8f, cx, cy + u * 0.2f, lw * 1.3f, ink);
                    break;
                case "old_battlefield":
                    canvas.Line(cx - u * 0.7f, cy + u * 0.7f, cx + u * 0.7f, cy - u * 0.7f, lw, ink);
                    canvas.Line(cx + u * 0.7f, cy + u * 0.7f, cx - u * 0.7f, cy - u * 0.7f, lw, ink);
                    canvas.Line(cx - u * 0.55f, cy + u * 0.15f, cx - u * 0.15f, cy + u * 0.55f, lw, ink);
                    canvas.Line(cx + u * 0.55f, cy + u * 0.15f, cx + u * 0.15f, cy + u * 0.55f, lw, ink);
                    break;
                case "crown_seat":
                default:
                    canvas.Rect(cx - u * 0.65f, cy + u * 0.15f, cx + u * 0.65f, cy + u * 0.5f, ink);
                    canvas.Triangle(cx - u * 0.65f, cy + u * 0.2f, cx - u * 0.35f, cy + u * 0.2f, cx - u * 0.6f, cy - u * 0.55f, ink);
                    canvas.Triangle(cx - u * 0.2f, cy + u * 0.2f, cx + u * 0.2f, cy + u * 0.2f, cx, cy - u * 0.7f, ink);
                    canvas.Triangle(cx + u * 0.35f, cy + u * 0.2f, cx + u * 0.65f, cy + u * 0.2f, cx + u * 0.6f, cy - u * 0.55f, ink);
                    break;
            }
        }

        private void Rose(float x, float y, float r)
        {
            if (r < 2 * s) return;
            var light = new Ink(224, 200, 146); var dark = new Ink(120, 86, 44);
            canvas.Disc(x, y, r * 0.78f, SeaDeep, 0.55f);
            canvas.Ring(x, y, r * 0.78f, Stroke(0.06f), Gilt, 0.8f);
            canvas.Ring(x, y, r * 0.7f, Stroke(0.035f), Gilt, 0.6f);
            for (int k = 0; k < 16; k++)
            {
                double a = k * Math.PI / 8 - Math.PI / 2;
                float len = k % 4 == 0 ? r : k % 2 == 0 ? r * 0.66f : r * 0.42f;
                float half = (k % 4 == 0 ? 0.13f : 0.1f) * r;
                float tx = x + len * (float)Math.Cos(a), ty = y + len * (float)Math.Sin(a);
                float px = -(float)Math.Sin(a), py = (float)Math.Cos(a);
                if (k % 2 == 1) { half *= 0.7f; }
                canvas.Triangle(x, y, tx, ty, x + px * half, y + py * half, k == 0 ? new Ink(196, 70, 50) : light);
                canvas.Triangle(x, y, x - px * half, y - py * half, tx, ty, dark);
                canvas.Line(x + px * half, y + py * half, tx, ty, Stroke(0.03f), InkDark, 0.8f);
                canvas.Line(x - px * half, y - py * half, tx, ty, Stroke(0.03f), InkDark, 0.8f);
            }
            canvas.Disc(x, y, r * 0.06f, Gilt);
            canvas.Ring(x, y, r * 0.06f, Stroke(0.03f), InkDark);
        }

        /// <summary>
        /// The sea pattern as one square tile, <paramref name="size"/> pixels a side, for the open sea
        /// behind the chart; it repeats every <see cref="SeaPeriodCells"/> cells, as the chart's own sea does.
        /// </summary>
        public static byte[] SeaTile(int size)
        {
            var c = new ChartCanvas(size, size);
            Parallel.For(0, size, y =>
            {
                for (int x = 0; x < size; x++) c.Set(y * size + x, SeaPattern((x + 0.5f) / size * SeaPeriodCells, (y + 0.5f) / size * SeaPeriodCells, SeaPeriodCells, SeaPeriodCells / (float)size));
            });
            return c.Rgba;
        }
    }
}

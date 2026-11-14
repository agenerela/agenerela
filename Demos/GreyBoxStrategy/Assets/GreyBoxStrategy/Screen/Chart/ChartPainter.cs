using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace GreyBoxStrategy.Chart
{
    /// <summary>
    /// The per-cell fields the ground is toned by, shared by the CPU renderer and the GPU one: the
    /// terrain's tone under a soft relief, the farmland, the terrain per cell. Grid-sized, so cheap.
    /// </summary>
    public sealed class ChartFields
    {
        public int Width, Height;
        /// <summary>RGB per cell, 0 to 255: the terrain's tone times the relief's light.</summary>
        public float[] Lit;
        /// <summary>0 to 1 per cell.</summary>
        public float[] Farmland;
        /// <summary>Per cell: <see cref="Terrain"/>'s index, -1 water, -2 a scenery island.</summary>
        public int[] Terrain;

        public static Ink TerrainTone(int t)
        {
            switch (t)
            {
                case 0: return new Ink(140, 130, 76);   // plains
                case 1: return new Ink(84, 96, 52);     // forest
                case 2: return new Ink(138, 120, 82);   // hills
                case 3: return new Ink(108, 114, 86);   // marsh
                case 4: return new Ink(128, 116, 98);   // mountains
                case -2: return new Ink(134, 122, 90);  // a scenery island
                default: return new Ink(176, 158, 112); // the shore's sand, under water cells
            }
        }

        private static float TerrainHeight(int t)
        {
            switch (t)
            {
                case 4: return 1f;
                case 2: return 0.55f;
                case 1: return 0.3f;
                case 0: return 0.18f;
                case -2: return 0.4f;
                case 3: return 0.05f;
                default: return -0.2f;
            }
        }

        public static ChartFields Build(WorldMap map, ChartScene scene, int seed)
        {
            var geo = map.Geometry;
            int gw = geo.Width, gh = geo.Height;
            var f = new ChartFields { Width = gw, Height = gh, Farmland = scene.Farmland, Terrain = scene.TerrainOf };
            var tone = new float[gw * gh * 3];
            var height = new float[gw * gh];
            for (int i = 0; i < tone.Length / 3; i++)
            {
                int t = scene.TerrainOf[i];
                var c = TerrainTone(t);
                float n = ChartNoise.Fbm((i % gw) * 0.21f, (i / gw) * 0.21f, seed + 71);
                c = Ink.Lerp(c, c.Times(0.82f), n);
                tone[i * 3] = c.R; tone[i * 3 + 1] = c.G; tone[i * 3 + 2] = c.B;
                height[i] = TerrainHeight(t) * (t >= -2 && t != -1 ? 0.7f + 0.6f * ChartNoise.Value((i % gw) * 0.5f, (i / gw) * 0.5f, seed + 72) : 1);
            }
            foreach (var ridge in geo.Ridges)
            {
                float ax = ridge.Line[0], ay = ridge.Line[1], bx = ridge.Line[2], by = ridge.Line[3];
                for (int y = 0; y < gh; y++)
                for (int x = 0; x < gw; x++)
                {
                    if (scene.TerrainOf[y * gw + x] < 0) continue;
                    float d = SegmentDistance(x + 0.5f, y + 0.5f, ax, ay, bx, by);
                    if (d < 3) height[y * gw + x] += 0.7f * (1 - d / 3);
                }
            }
            for (int pass = 0; pass < 2; pass++) { Blur(tone, gw, gh, 3, 1); Blur(height, gw, gh, 1, 2); }
            f.Lit = new float[tone.Length];
            for (int y = 0; y < gh; y++)
            for (int x = 0; x < gw; x++)
            {
                float u = x + 0.5f, v = y + 0.5f;
                float hx = Sample(height, gw, gh, 1, 0, u + 0.7f, v) - Sample(height, gw, gh, 1, 0, u - 0.7f, v);
                float hy = Sample(height, gw, gh, 1, 0, u, v + 0.7f) - Sample(height, gw, gh, 1, 0, u, v - 0.7f);
                float shade = Math.Max(0.7f, Math.Min(1.25f, 1 - 0.55f * (hx + hy)));
                int i = (y * gw + x) * 3;
                for (int c = 0; c < 3; c++) f.Lit[i + c] = tone[i + c] * shade;
            }
            return f;
        }

        private static float SegmentDistance(float px, float py, float ax, float ay, float bx, float by)
        {
            float vx = bx - ax, vy = by - ay, l2 = vx * vx + vy * vy;
            float t = l2 > 0 ? Math.Max(0, Math.Min(1, ((px - ax) * vx + (py - ay) * vy) / l2)) : 0;
            return ChartLayout.Dist(px, py, ax + vx * t, ay + vy * t);
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

        /// <summary>Bilinear, centres of cells at +0.5.</summary>
        public static float Sample(float[] f, int gw, int gh, int channels, int c, float u, float v)
        {
            u -= 0.5f; v -= 0.5f;
            int x0 = (int)Math.Floor(u), y0 = (int)Math.Floor(v);
            float fx = u - x0, fy = v - y0;
            int xa = x0 < 0 ? 0 : x0 >= gw ? gw - 1 : x0, xb = x0 + 1 < 0 ? 0 : x0 + 1 >= gw ? gw - 1 : x0 + 1;
            int ya = y0 < 0 ? 0 : y0 >= gh ? gh - 1 : y0, yb = y0 + 1 < 0 ? 0 : y0 + 1 >= gh ? gh - 1 : y0 + 1;
            float a = f[(ya * gw + xa) * channels + c], b = f[(ya * gw + xb) * channels + c];
            float d = f[(yb * gw + xa) * channels + c], e = f[(yb * gw + xb) * channels + c];
            return (a + (b - a) * fx) * (1 - fy) + (d + (e - d) * fx) * fy;
        }
    }

    /// <summary>
    /// Draws a generated world's chart on the CPU, from its scene (<see cref="ChartLayout"/>): the
    /// ground (sea, coast and cliffs, land toned by terrain, farmland, forest canopy, mountain rock,
    /// paper grain), then rivers and roads, then every glyph back to front, then the rose. Owner
    /// colours and names are not here: the map shader and the screen's text add them.
    /// </summary>
    /// <remarks>Used by the tests and the .NET harness, and in the game where the GPU renderer is not available.</remarks>
    public sealed class ChartPainter
    {
        public static readonly Ink SeaDeep = new Ink(31, 38, 42);
        public static readonly Ink SeaShallow = new Ink(52, 64, 66);
        public static readonly Ink SeaLine = new Ink(118, 128, 118);
        public static readonly Ink LakeTone = new Ink(66, 86, 92);
        public static readonly Ink RiverTone = new Ink(70, 98, 112);
        static readonly Ink InkDark = new Ink(38, 28, 18);
        static readonly Ink Cliff = new Ink(104, 84, 58);
        static readonly Ink CliffDark = new Ink(62, 48, 32);
        static readonly Ink Sand = new Ink(176, 158, 112);
        static readonly Ink Gilt = new Ink(204, 166, 92);

        /// <summary>The sea pattern tiles every this many cells, so the open sea behind the chart can repeat it.</summary>
        public const int SeaPeriodCells = 24;
        private const int GrainPeriod = 8, GrainSize = 256, SeaTileSize = 512;

        private readonly WorldMap map;
        private readonly ChartRaster raster;
        private readonly int seed;
        private readonly float s;
        private ChartCanvas canvas;
        private float[] coastDist;
        private int[] cliff;
        private float[] grain;
        private byte[] seaTile;
        private byte[] paper;
        private int paperSize;

        public readonly Dictionary<string, long> Milliseconds = new Dictionary<string, long>();
        public int GlyphCount { get; private set; }
        public ChartScene Scene { get; private set; }
        public ChartFields Fields { get; private set; }

        public ChartPainter(WorldMap map, ChartRaster raster, int seed) { this.map = map; this.raster = raster; this.seed = seed; s = raster.Scale; }

        /// <summary>A tileable paper texture (RGBA, square) laid over the land, or none.</summary>
        public void UsePaper(byte[] rgba, int size)
        {
            paper = rgba; paperSize = size;
            double sum = 0;
            for (int i = 0; i < rgba.Length; i += 4) sum += rgba[i] + rgba[i + 1] + rgba[i + 2];
            paperMean = (float)(sum / (rgba.Length / 4 * 3.0));
        }

        private float paperMean = 1;

        public ChartCanvas Paint()
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            Scene = ChartLayout.Build(map, raster, seed);
            Fields = ChartFields.Build(map, Scene, seed);
            Milliseconds["layout"] = watch.ElapsedMilliseconds; watch.Restart();
            canvas = new ChartCanvas(raster.Width, raster.Height);
            coastDist = ChartRaster.DistanceToOther(raster.Ids, raster.Width, raster.Height, (a, b) => ChartRaster.IsWater(a) != ChartRaster.IsWater(b));
            FindCliffs();
            grain = Grain(seed);
            seaTile = SeaTile(SeaTileSize);
            Milliseconds["fields"] = watch.ElapsedMilliseconds; watch.Restart();
            Ground();
            Milliseconds["ground"] = watch.ElapsedMilliseconds; watch.Restart();
            foreach (var st in Scene.Strokes.Where(x => x.Kind == StrokeKind.River)) DrawStroke(st);
            foreach (var st in Scene.Strokes.Where(x => x.Kind != StrokeKind.River)) DrawStroke(st);
            foreach (var g in Scene.Glyphs) ChartGlyphs.Draw(canvas, g, g.U * s, g.V * s, s);
            GlyphCount = Scene.Glyphs.Count;
            Milliseconds["glyphs"] = watch.ElapsedMilliseconds;
            return canvas;
        }

        private void FindCliffs()
        {
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

        /// <summary>The paper grain: tiles every <see cref="GrainPeriod"/> cells, <see cref="GrainSize"/> samples a side; values about 0.86 to 1.14.</summary>
        public static float[] Grain(int seed)
        {
            var g = new float[GrainSize * GrainSize];
            float perSample = GrainPeriod / (float)GrainSize;
            Parallel.For(0, GrainSize, y =>
            {
                for (int x = 0; x < GrainSize; x++)
                {
                    float u = x * perSample, v = y * perSample;
                    float a = ChartNoise.Fbm(u * 14f / GrainPeriod, v * 14f / GrainPeriod, seed + 3, 14);
                    float b = ChartNoise.Value(u * 72f / GrainPeriod, v * 72f / GrainPeriod, seed + 4, 72);
                    g[y * GrainSize + x] = 0.86f + 0.2f * a + 0.08f * b;
                }
            });
            return g;
        }

        private float GrainAt(float u, float v)
        {
            int x = (int)(u * (GrainSize / (float)GrainPeriod)) & (GrainSize - 1), y = (int)(v * (GrainSize / (float)GrainPeriod)) & (GrainSize - 1);
            return grain[y * GrainSize + x];
        }

        private Ink SeaAt(float u, float v)
        {
            int x = (int)(u * (SeaTileSize / (float)SeaPeriodCells)) & (SeaTileSize - 1), y = (int)(v * (SeaTileSize / (float)SeaPeriodCells)) & (SeaTileSize - 1);
            int k = (y * SeaTileSize + x) * 4;
            return new Ink(seaTile[k], seaTile[k + 1], seaTile[k + 2]);
        }

        // ---- the ground: one pass over every pixel ----

        private void Ground()
        {
            int w = raster.Width;
            float coastLine = Math.Max(1.1f, 0.09f * s);
            var f = Fields;
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
                        c = new Ink(ChartFields.Sample(f.Lit, f.Width, f.Height, 3, 0, u, v), ChartFields.Sample(f.Lit, f.Width, f.Height, 3, 1, u, v), ChartFields.Sample(f.Lit, f.Width, f.Height, 3, 2, u, v));
                        int t = ChartRaster.IsProvince(id) ? (int)map.Provinces[id].Terrain : -2;
                        c = LandTexture(c, t, u, v, f, seed);
                        c = c.Times(GrainAt(u, v));
                        c = Paper(c, u, v);
                        float dc = d / s;
                        if (dc < 0.6f) c = Ink.Lerp(c, Sand, 0.35f * (1 - dc / 0.6f));
                        float line = 1 - Math.Max(0, Math.Min(1, d - coastLine));
                        if (line > 0) c = Ink.Lerp(c, InkDark, 0.85f * line);
                    }
                    else
                    {
                        bool lake = id == ChartRaster.Water;
                        c = SeaAt(u, v);
                        float dc = d / s;
                        if (lake) c = Ink.Lerp(c, LakeTone, 0.75f);
                        if (dc < 1.6f) c = Ink.Lerp(c, lake ? LakeTone.Times(1.15f) : SeaShallow, 0.55f * (1 - dc / 1.6f));
                        if (!lake || dc < 1.0f)
                        {
                            float wob = dc + 0.12f * (ChartNoise.Value(u * 0.8f, v * 0.8f, seed + 9) - 0.5f);
                            for (int r = 0; r < Rings.Length; r++)
                            {
                                float px = Math.Abs(wob - Rings[r]) * s;
                                float lw = Math.Max(0.9f, 0.05f * s);
                                float a = Math.Max(0, Math.Min(1, lw / 2 - px + 0.5f)) * (0.42f - r * 0.07f);
                                if (a > 0) c = Ink.Lerp(c, SeaLine, a);
                            }
                        }
                        if (!lake)
                            foreach (var (ru, rv) in Scene.Rhumbs)
                            {
                                float dx = u - ru, dy = v - rv;
                                float dist = (float)Math.Sqrt(dx * dx + dy * dy) * s;
                                if (dist < 1) continue;
                                float ang = (float)Math.Atan2(dy, dx), step = (float)(Math.PI / 8);
                                float off = ang / step; off -= (float)Math.Round(off);
                                float perp = Math.Abs(off) * step * dist;
                                float lw = Math.Max(0.8f, 0.035f * s);
                                float a = Math.Max(0, Math.Min(1, lw / 2 - perp + 0.5f)) * 0.22f * Math.Min(1, 30 * s / (dist + 30 * s) * 1.6f);
                                if (a > 0) c = Ink.Lerp(c, Gilt, a);
                            }
                        int k = cliff[i];
                        if (k > 0 && !lake)
                        {
                            float depth = Math.Max(2, 0.55f * s), t = k / depth;
                            float stripe = ChartNoise.Value(u * 6f, v * 1.2f, seed + 5);
                            c = Ink.Lerp(c, Ink.Lerp(Cliff, CliffDark, t * 0.8f + 0.3f * stripe), 0.92f * (1 - t * t));
                        }
                        float line = 1 - Math.Max(0, Math.Min(1, d - coastLine));
                        if (line > 0) c = Ink.Lerp(c, InkDark, 0.85f * line);
                    }
                    canvas.Set(i, c);
                }
            });
        }

        private static readonly float[] Rings = { 0.45f, 1.0f, 1.65f, 2.45f, 3.4f };

        private Ink Paper(Ink c, float u, float v)
        {
            if (paper == null) return c;
            // The paper tile repeats every 16 cells; it multiplies the land, keeping its mean.
            float t = 16f;
            int x = (int)((u / t - Math.Floor(u / t)) * paperSize), y = (int)((v / t - Math.Floor(v / t)) * paperSize);
            int k = (Math.Min(paperSize - 1, y) * paperSize + Math.Min(paperSize - 1, x)) * 4;
            // As the ground shader: the paper relative to its own mean, so the land keeps its tone.
            float m = (paper[k] + paper[k + 1] + paper[k + 2]) / (3f * paperMean);
            return c.Times(1 + 0.6f * (m - 1));
        }

        /// <summary>
        /// The land's own texture, by terrain: fields where the farmland reaches (rectangular plots in
        /// a slowly turning grid, each its own crop, with furrows and hedges), a canopy under forest,
        /// rock hatching on mountains, standing water in marsh.
        /// </summary>
        public static Ink LandTexture(Ink c, int terrain, float u, float v, ChartFields f, int seed)
        {
            if (terrain == 1)
            {
                // Canopy: crowns as soft bumps, lit from the upper left.
                float a = ChartNoise.Value(u * 2.6f, v * 2.6f, seed + 41), b = ChartNoise.Value(u * 2.6f - 0.18f, v * 2.6f - 0.18f, seed + 41);
                float crown = Math.Max(0, Math.Min(1, (a - 0.3f) * 2.2f));
                c = Ink.Lerp(c.Times(0.72f), c.Times(1.08f), crown);
                c = c.Times(1 + 0.35f * (b - a));
            }
            else if (terrain == 4)
            {
                float n = ChartNoise.Value(u * 1.3f, v * 1.3f, seed + 43);
                float hatch = (u * 2.2f + v * 3.1f) % 1f;
                if (hatch < 0.14f && n > 0.45f) c = c.Times(0.84f);
                c = Ink.Lerp(c, new Ink(150, 140, 126), 0.25f * n);
            }
            else if (terrain == 3)
            {
                float n = ChartNoise.Value(u * 1.6f, v * 1.6f, seed + 44);
                if (n > 0.62f) c = Ink.Lerp(c, new Ink(80, 104, 104), Math.Min(0.6f, (n - 0.62f) * 4));
            }
            float farm = ChartFields.Sample(f.Farmland, f.Width, f.Height, 1, 0, u, v);
            if (farm > 0.02f && terrain != 4 && terrain != 3)
            {
                // Farms of a few cells each, every farm's plots in a grid turned its own way; the farm's
                // edges wander, so neighbouring farms meet along uneven lines.
                const float farmSize = 4.2f, pw = 1.05f, ph = 0.6f;
                float wu = u + 1.6f * (ChartNoise.Value(u * 0.25f, v * 0.25f, seed + 47) - 0.5f);
                float wv = v + 1.6f * (ChartNoise.Value(u * 0.25f + 7, v * 0.25f, seed + 48) - 0.5f);
                int fx = (int)Math.Floor(wu / farmSize), fy = (int)Math.Floor(wv / farmSize);
                float th = (ChartNoise.Hash01(fx, fy, seed + 45) - 0.5f) * 1.8f;
                float cs = (float)Math.Cos(th), sn = (float)Math.Sin(th);
                float lu = u - (fx + 0.5f) * farmSize, lv = v - (fy + 0.5f) * farmSize;
                float ru = lu * cs - lv * sn, rv = lu * sn + lv * cs;
                float row = (float)Math.Floor(rv / ph);
                float shift = (((int)row & 1) == 0) ? 0 : 0.5f;
                float col = (float)Math.Floor(ru / pw + shift);
                uint h = ChartNoise.Hash((int)col + fx * 101, (int)row + fy * 103, seed + 46);
                // A farm is worked or not, by the farmland at its middle; a few of its plots lie fallow.
                float worked = ChartFields.Sample(f.Farmland, f.Width, f.Height, 1, 0, (fx + 0.5f) * farmSize, (fy + 0.5f) * farmSize);
                bool farmed = ChartNoise.Hash01(fx, fy, seed + 49) < Math.Max(worked, farm * 0.8f);
                float pick = (h & 0xFFFF) / 65536f;
                if (farmed && pick < 0.9f)
                {
                    float fu = ru / pw + shift - col, fv = rv / ph - row;
                    var crop = ((h >> 16) & 3) switch
                    {
                        0 => new Ink(192, 168, 92),  // ripe grain
                        1 => new Ink(136, 146, 70),  // green crop
                        2 => new Ink(150, 112, 74),  // ploughed earth
                        _ => new Ink(170, 156, 96),  // stubble
                    };
                    // Light and dark between crops, not only hue: an owner's wash keeps the chart's light and replaces
                    // its hue, so fields must read in light alone (as the GPU's ground shader has it).
                    float tone = ((h >> 16) & 3) switch { 0 => 1.14f, 1 => 0.9f, 2 => 0.78f, _ => 1.02f };
                    c = Ink.Lerp(c, crop.Times(Math.Max(0.75f, Math.Min(1.15f, c.R / 140f)) * tone), 0.72f);
                    // Furrows along the plot, and a hedge round it.
                    if (((fv * 5f) - (float)Math.Floor(fv * 5f)) < 0.22f) c = c.Times(0.84f);
                    float edge = Math.Min(Math.Min(fu, 1 - fu) * pw, Math.Min(fv, 1 - fv) * ph);
                    if (edge < 0.05f) c = Ink.Lerp(c, new Ink(58, 60, 30), 0.72f);
                }
            }
            return c;
        }

        // ---- rivers and roads ----

        private void DrawStroke(Stroke st)
        {
            var p = st.Points;
            float w = st.Width * s;
            switch (st.Kind)
            {
                case StrokeKind.River:
                    for (int k = 1; k < p.Count; k++) canvas.Line(p[k - 1].u * s, p[k - 1].v * s, p[k].u * s, p[k].v * s, w + Math.Max(1.2f, 0.07f * s), InkDark, 0.75f);
                    for (int k = 1; k < p.Count; k++) canvas.Line(p[k - 1].u * s, p[k - 1].v * s, p[k].u * s, p[k].v * s, w, RiverTone);
                    break;
                case StrokeKind.Highway:
                    for (int k = 1; k < p.Count; k++) canvas.Line(p[k - 1].u * s, p[k - 1].v * s, p[k].u * s, p[k].v * s, w + Math.Max(1.4f, 0.09f * s), InkDark, 0.8f);
                    for (int k = 1; k < p.Count; k++) canvas.Line(p[k - 1].u * s, p[k - 1].v * s, p[k].u * s, p[k].v * s, w, new Ink(214, 192, 142));
                    break;
                default:
                    // A track: ink dashes.
                    float run = 0, dash = 0.42f, gap = 0.3f;
                    for (int k = 1; k < p.Count; k++)
                    {
                        float seg = ChartLayout.Dist(p[k - 1].u, p[k - 1].v, p[k].u, p[k].v);
                        if (((run % (dash + gap)) < dash)) canvas.Line(p[k - 1].u * s, p[k - 1].v * s, p[k].u * s, p[k].v * s, w, new Ink(58, 40, 24), 0.9f);
                        run += seg;
                    }
                    break;
            }
        }

        /// <summary>The sea's own texture: a dark chart tone with engraved wave marks, tiling every <paramref name="period"/> cells.</summary>
        public static Ink SeaPattern(float u, float v, int period, float pixelCells)
        {
            float tu = u / period, tv = v / period;
            float n = ChartNoise.Fbm(tu * 6, tv * 6, 401, 6);
            var c = Ink.Lerp(SeaDeep, SeaDeep.Times(1.22f), n);
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
                float ax = fx * period / cols, ay = fy * period / rows;
                float r = 0.26f * period / cols * (0.8f + 0.4f * pick);
                float dist = Math.Abs((float)Math.Sqrt(ax * ax + ay * ay) - r);
                float hw = Math.Max(0.03f, 0.55f * pixelCells);
                if (ay < 0.02f && dist < hw) c = Ink.Lerp(c, SeaLine, 0.3f * (1 - dist / hw) * (0.6f + 0.6f * n) * Math.Min(1, 0.05f / hw));
            }
            return c;
        }

        /// <summary>The sea pattern as one square tile, <paramref name="size"/> pixels a side, repeating every <see cref="SeaPeriodCells"/> cells.</summary>
        public static byte[] SeaTile(int size)
        {
            var c = new ChartCanvas(size, size);
            Parallel.For(0, size, y =>
            {
                for (int x = 0; x < size; x++)
                    c.Set(y * size + x, SeaPattern((x + 0.5f) / size * SeaPeriodCells, (y + 0.5f) / size * SeaPeriodCells, SeaPeriodCells, SeaPeriodCells / (float)size));
            });
            return c.Rgba;
        }
    }
}

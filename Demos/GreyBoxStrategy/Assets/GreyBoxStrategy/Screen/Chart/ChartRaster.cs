using System;
using System.Threading.Tasks;

namespace GreyBoxStrategy.Chart
{
    /// <summary>
    /// A generated world's cell grid turned into pixels at any size: which province each pixel
    /// belongs to, and how far each pixel is from a border and from the coast. The province map and
    /// the edge field the map shader reads come from here, and so does every layer of the drawn chart.
    /// </summary>
    /// <remarks>
    /// <para>The grid is a staircase of square cells (318 × 179 on a medium world). Drawn as it is, the
    /// borders would be stairs at any zoom. Each pixel instead looks up the cells around a point
    /// moved a little by noise ("noisy edges", rules v2 section 2.6) and takes the province with the
    /// most weight nearby, so corners round off and borders wander like inked ones. The weight
    /// favours a pixel's own cell, so a neck one cell wide survives; only a lone cell poking into a
    /// neighbour is smoothed away.</para>
    /// <para>Plain C#, no Unity: tests and a .NET harness draw it without an editor.</para>
    /// </remarks>
    public sealed class ChartRaster
    {
        /// <summary>The open sea in <see cref="Ids"/>.</summary>
        public const ushort Sea = 0xFFFF;
        /// <summary>Inland water (a lake) in <see cref="Ids"/>.</summary>
        public const ushort Water = 0xFFFE;

        public int Width { get; private set; }
        public int Height { get; private set; }
        /// <summary>Pixels per grid cell.</summary>
        public float Scale { get; private set; }
        public int GridWidth { get; private set; }
        public int GridHeight { get; private set; }
        public int Provinces { get; private set; }
        /// <summary>Per pixel, row by row from the top: the province's index in the map, or <see cref="Sea"/> / <see cref="Water"/>.</summary>
        public ushort[] Ids { get; private set; }
        /// <summary>The grid itself, decoded: province index, -1 sea, -2 inland water.</summary>
        public int[] Cells { get; private set; }

        public static bool IsWater(ushort id) => id >= Water;

        /// <summary>Rasterises <paramref name="geometry"/> <paramref name="width"/> pixels wide; the height keeps the grid's shape.</summary>
        public static ChartRaster Build(MapGeometry geometry, int width, int seed = 0)
        {
            if (geometry == null) throw new ArgumentNullException(nameof(geometry));
            if (width < geometry.Width) throw new ArgumentException($"A chart narrower than its grid ({width} < {geometry.Width} cells) would lose provinces.");
            var r = new ChartRaster
            {
                GridWidth = geometry.Width,
                GridHeight = geometry.Height,
                Provinces = geometry.Provinces.Count,
                Width = width,
                Scale = width / (float)geometry.Width,
                Cells = geometry.DecodeCells(),
            };
            r.Height = Math.Max(1, (int)Math.Round(geometry.Height * r.Scale));
            r.Ids = new ushort[r.Width * r.Height];
            r.Fill(seed);
            return r;
        }

        private ushort CellId(int x, int y)
        {
            if (x < 0) x = 0; else if (x >= GridWidth) x = GridWidth - 1;
            if (y < 0) y = 0; else if (y >= GridHeight) y = GridHeight - 1;
            int v = Cells[y * GridWidth + x];
            return v >= 0 ? (ushort)v : v == MapGeometry.Water ? Water : Sea;
        }

        /// <summary>The province (or water) at a point in grid units, as a pixel there is drawn.</summary>
        public ushort IdAtCell(float u, float v, int seed)
        {
            // Wander: two noise fields move the point by up to about half a cell.
            const float amp = 0.42f, freq = 0.38f;
            float wu = u + amp * (ChartNoise.Fbm(u * freq, v * freq, seed + 11) * 2 - 1);
            float wv = v + amp * (ChartNoise.Fbm(u * freq + 17.3f, v * freq - 5.1f, seed + 29) * 2 - 1);
            int cx = (int)Math.Floor(wu), cy = (int)Math.Floor(wv);
            // Weighted vote over the 3 × 3 cells around it: a cone of radius 1.2 cells from each centre.
            Span<ushort> ids = stackalloc ushort[9];
            Span<float> weights = stackalloc float[9];
            int kinds = 0;
            const float r2 = 1.2f * 1.2f;
            for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                float ex = cx + dx + 0.5f - wu, ey = cy + dy + 0.5f - wv;
                float w = r2 - (ex * ex + ey * ey);
                if (w <= 0) continue;
                ushort id = CellId(cx + dx, cy + dy);
                int k = 0;
                while (k < kinds && ids[k] != id) k++;
                if (k == kinds) { ids[k] = id; weights[k] = 0; kinds++; }
                weights[k] += w;
            }
            if (kinds == 0) return CellId(cx, cy);
            int best = 0;
            for (int k = 1; k < kinds; k++) if (weights[k] > weights[best]) best = k;
            return ids[best];
        }

        private void Fill(int seed)
        {
            float inv = 1f / Scale;
            Parallel.For(0, Height, y =>
            {
                float v = (y + 0.5f) * inv;
                int row = y * Width;
                for (int x = 0; x < Width; x++)
                    Ids[row + x] = IdAtCell((x + 0.5f) * inv, v, seed);
            });
        }

        /// <summary>
        /// The same map at a lower resolution, <paramref name="factor"/> pixels to one, each taking
        /// the id at its block's centre, so the two never disagree about a province.
        /// </summary>
        public ushort[] Downsample(int factor, out int width, out int height)
        {
            width = Width / factor; height = Height / factor;
            var o = new ushort[width * height];
            int w = width, half = factor / 2;
            for (int y = 0; y < height; y++)
            for (int x = 0; x < w; x++)
                o[y * w + x] = Ids[(y * factor + half) * Width + x * factor + half];
            return o;
        }

        /// <summary>
        /// Per pixel, the distance in pixels to the nearest pixel of another id (two-pass chamfer,
        /// 1 and √2): 1 next to a border.
        /// </summary>
        public static float[] DistanceToOther(ushort[] ids, int width, int height, Func<ushort, ushort, bool> differs = null)
        {
            differs = differs ?? ((a, b) => a != b);
            var d = new float[ids.Length];
            const float big = 1e9f;
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                int i = y * width + x;
                ushort id = ids[i];
                bool edge = (x > 0 && differs(id, ids[i - 1])) || (x < width - 1 && differs(id, ids[i + 1]))
                         || (y > 0 && differs(id, ids[i - width])) || (y < height - 1 && differs(id, ids[i + width]));
                d[i] = edge ? 1 : big;
            }
            const float diag = 1.41421356f;
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                int i = y * width + x;
                float v = d[i];
                if (x > 0) v = Math.Min(v, d[i - 1] + 1);
                if (y > 0)
                {
                    v = Math.Min(v, d[i - width] + 1);
                    if (x > 0) v = Math.Min(v, d[i - width - 1] + diag);
                    if (x < width - 1) v = Math.Min(v, d[i - width + 1] + diag);
                }
                d[i] = v;
            }
            for (int y = height - 1; y >= 0; y--)
            for (int x = width - 1; x >= 0; x--)
            {
                int i = y * width + x;
                float v = d[i];
                if (x < width - 1) v = Math.Min(v, d[i + 1] + 1);
                if (y < height - 1)
                {
                    v = Math.Min(v, d[i + width] + 1);
                    if (x < width - 1) v = Math.Min(v, d[i + width + 1] + diag);
                    if (x > 0) v = Math.Min(v, d[i + width - 1] + diag);
                }
                d[i] = v;
            }
            return d;
        }

        /// <summary>
        /// The edge field the map shader reads (<c>_EdgeTex</c>): distance to the nearest border in
        /// pixels, times 16, capped at 255, as the Classic board's bake writes it.
        /// </summary>
        public static byte[] EdgeField(ushort[] ids, int width, int height)
        {
            var d = DistanceToOther(ids, width, height);
            var o = new byte[d.Length];
            for (int i = 0; i < d.Length; i++) o[i] = (byte)Math.Min(255, (int)Math.Round((d[i] - 1) * 16));
            return o;
        }

        /// <summary>A pixel's centre in grid units.</summary>
        public void PixelToCell(int x, int y, out float u, out float v) { u = (x + 0.5f) / Scale; v = (y + 0.5f) / Scale; }
    }
}

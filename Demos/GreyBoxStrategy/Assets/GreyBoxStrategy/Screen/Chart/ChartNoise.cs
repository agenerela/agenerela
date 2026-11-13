using System;

namespace GreyBoxStrategy.Chart
{
    /// <summary>
    /// Integer-hash value noise for the drawn chart. Every frequency is in grid cells, never in
    /// pixels, so a chart drawn at 2048 and at 4800 wide shows the same strokes, only sharper.
    /// The chart is decoration: unlike the generator, nothing here has to match across machines.
    /// </summary>
    public static class ChartNoise
    {
        public static uint Hash(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)x * 374761393u + (uint)y * 668265263u + (uint)seed * 2246822519u;
                h = (h ^ (h >> 13)) * 1274126177u;
                return h ^ (h >> 16);
            }
        }

        /// <summary>0 to 1.</summary>
        public static float Hash01(int x, int y, int seed) => (Hash(x, y, seed) & 0xFFFFFF) / 16777216f;

        /// <summary>Smooth value noise, 0 to 1. <paramref name="period"/> above 0 makes it tile every <c>period</c> units.</summary>
        public static float Value(float x, float y, int seed, int period = 0)
        {
            int x0 = (int)Math.Floor(x), y0 = (int)Math.Floor(y);
            float fx = x - x0, fy = y - y0;
            fx = fx * fx * (3 - 2 * fx);
            fy = fy * fy * (3 - 2 * fy);
            int x1 = x0 + 1, y1 = y0 + 1;
            if (period > 0) { x0 = Mod(x0, period); x1 = Mod(x1, period); y0 = Mod(y0, period); y1 = Mod(y1, period); }
            float a = Hash01(x0, y0, seed), b = Hash01(x1, y0, seed), c = Hash01(x0, y1, seed), d = Hash01(x1, y1, seed);
            return (a + (b - a) * fx) * (1 - fy) + (c + (d - c) * fx) * fy;
        }

        /// <summary>Three octaves, 0 to 1.</summary>
        public static float Fbm(float x, float y, int seed, int period = 0)
        {
            return 0.55f * Value(x, y, seed, period)
                 + 0.30f * Value(x * 2 + 0.37f, y * 2 + 0.61f, seed + 1, period > 0 ? period * 2 : 0)
                 + 0.15f * Value(x * 4 + 0.13f, y * 4 + 0.29f, seed + 2, period > 0 ? period * 4 : 0);
        }

        private static int Mod(int a, int m) { int r = a % m; return r < 0 ? r + m : r; }
    }
}

namespace GreyBoxStrategy
{
    /// <summary>
    /// The map generator's randomness: integer hashes and integer value noise only, so a seed's world is
    /// the same on every machine (rules v2, section 2.3). Every step draws from the seed and its own
    /// name, so changing one step's rule does not reshuffle the others. No floating point anywhere a
    /// result depends on it, and no <c>string.GetHashCode</c>, which differs between runtimes.
    /// </summary>
    public static class MapRandom
    {
        /// <summary>A 32-bit integer mix (the "lowbias32" finaliser).</summary>
        public static uint Mix(uint a)
        {
            unchecked
            {
                a ^= a >> 16;
                a *= 0x7feb352dU;
                a ^= a >> 15;
                a *= 0x846ca68bU;
                a ^= a >> 16;
                return a;
            }
        }

        /// <summary>FNV-1a of a step's name, stable everywhere.</summary>
        public static uint Name(string s)
        {
            unchecked
            {
                uint h = 2166136261U;
                foreach (char c in s)
                {
                    h ^= c;
                    h *= 16777619U;
                }
                return h;
            }
        }

        public static uint Hash(int seed, string step, int a, int b = 0, int c = 0)
        {
            unchecked
            {
                uint h = Mix((uint)seed ^ 0x9E3779B9U);
                h = Mix(h ^ Name(step));
                h = Mix(h ^ (uint)a);
                h = Mix(h ^ ((uint)b * 0x85EBCA6BU));
                h = Mix(h ^ ((uint)c * 0xC2B2AE35U));
                return h;
            }
        }

        /// <summary>An integer in [0, n).</summary>
        public static int Int(int seed, string step, int a, int n, int b = 0) => n <= 0 ? 0 : (int)(Hash(seed, step, a, b) % (uint)n);

        /// <summary>Integer square root, rounded down.</summary>
        public static long Isqrt(long v)
        {
            if (v <= 0) return 0;
            long r = (long)System.Math.Sqrt(v);
            // Correct the estimate in integers, so the result never depends on the floating point unit.
            while (r * r > v) r--;
            while ((r + 1) * (r + 1) <= v) r++;
            return r;
        }

        /// <summary>
        /// Value noise in 0..65535 at a cell, from a lattice every <paramref name="period"/> cells,
        /// smoothed with a fixed-point smoothstep.
        /// </summary>
        public static int Value(int seed, string step, int x, int y, int period)
        {
            if (period < 1) period = 1;
            int ix = Floor(x, period), iy = Floor(y, period);
            long fx = (long)(x - ix * period) * 65536 / period;
            long fy = (long)(y - iy * period) * 65536 / period;
            fx = Smooth(fx);
            fy = Smooth(fy);
            long a = Hash(seed, step, ix, iy) & 0xFFFF;
            long b = Hash(seed, step, ix + 1, iy) & 0xFFFF;
            long c = Hash(seed, step, ix, iy + 1) & 0xFFFF;
            long d = Hash(seed, step, ix + 1, iy + 1) & 0xFFFF;
            long top = a + ((b - a) * fx >> 16);
            long bottom = c + ((d - c) * fx >> 16);
            return (int)(top + ((bottom - top) * fy >> 16));
        }

        /// <summary>Several octaves of <see cref="Value"/>, the largest first, each half the period and half the weight.</summary>
        public static int Fractal(int seed, string step, int x, int y, int period, int octaves)
        {
            long sum = 0, weight = 0;
            int w = 1 << (octaves - 1);
            for (int o = 0; o < octaves; o++)
            {
                sum += (long)Value(seed, step + o, x, y, period) * w;
                weight += w;
                period = System.Math.Max(1, period / 2);
                w = System.Math.Max(1, w / 2);
            }
            return (int)(sum / weight);
        }

        private static int Floor(int v, int p) => v >= 0 ? v / p : (v - p + 1) / p;

        // 3t^2 - 2t^3 in 16.16 fixed point.
        private static long Smooth(long t) => t * t * (3 * 65536 - 2 * t) >> 32;
    }

    /// <summary>A deterministic stream of integers for one step of the generator.</summary>
    public sealed class MapRng
    {
        private readonly int seed;
        private readonly string step;
        private int counter;

        public MapRng(int seed, string step)
        {
            this.seed = seed;
            this.step = step;
        }

        public uint Next() => MapRandom.Hash(seed, step, counter++);

        /// <summary>An integer in [0, n).</summary>
        public int Int(int n) => n <= 0 ? 0 : (int)(Next() % (uint)n);
    }
}

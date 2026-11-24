using System;
using System.Collections.Generic;

namespace GreyBoxStrategy.Chart
{
    /// <summary>
    /// Owner colours for up to ten countries. The first five are the Classic board's, in its seat
    /// order; the other five were picked so that every pair stays apart once the map shader washes
    /// them over the chart.
    /// </summary>
    /// <remarks>
    /// The wash is a "color" blend: it keeps the chart's lightness and takes only the owner's hue
    /// and saturation. So two owners that differ only in lightness would look the same on the map.
    /// <see cref="WashedDistance"/> measures what the map shows: both colours washed over the same
    /// chart tone, then the CIE 1976 distance between them. A search over hue, saturation and value
    /// for the five new colours found none that raises the closest pair above the floor the
    /// Classic five already set among themselves (about 15). The search's best ninth colour was an
    /// ultramarine (15.4), but next to Kesh's blue it read as the same country on screen; the
    /// orange here scores 14.1 and is a hue no other country has.
    /// </remarks>
    public static class ChartPalette
    {
        public static readonly byte[][] Owners =
        {
            new byte[] { 176, 46, 36 },   // Classic: Valen's red
            new byte[] { 214, 152, 40 },  // Orsk's ochre
            new byte[] { 38, 134, 104 },  // Merrow's sea green
            new byte[] { 58, 80, 170 },   // Kesh's blue
            new byte[] { 128, 60, 146 },  // Ilvar's violet
            new byte[] { 196, 72, 128 },  // rose
            new byte[] { 40, 150, 196 },  // sky
            new byte[] { 130, 160, 40 },  // leaf
            new byte[] { 214, 96, 30 },   // vermilion orange
            new byte[] { 115, 52, 90 },   // plum
        };

        /// <summary>The shader's neutral land tone (<c>PaintedMap.shader</c>, <c>neutral</c>).</summary>
        public static readonly byte[] Neutral = { 150, 140, 122 };

        /// <summary>The chart tones a wash lands on: plains, forest, mountains.</summary>
        public static readonly byte[][] ChartTones = { new byte[] { 140, 130, 76 }, new byte[] { 96, 102, 58 }, new byte[] { 132, 118, 98 } };

        public static byte[] Owner(int slot) => Owners[((slot % Owners.Length) + Owners.Length) % Owners.Length];

        /// <summary>How far apart two owners look on the map: both washed over <paramref name="chart"/> at <paramref name="alpha"/>, as CIE76 ΔE.</summary>
        public static double WashedDistance(byte[] a, byte[] b, byte[] chart, double alpha = 0.7)
            => Distance(Lab(Wash(chart, a, alpha)), Lab(Wash(chart, b, alpha)));

        /// <summary>The shader's wash of <paramref name="tint"/> over <paramref name="chart"/>, in sRGB 0 to 255.</summary>
        public static double[] Wash(byte[] chart, byte[] tint, double alpha)
        {
            var p = Linear(chart); var t = Linear(tint);
            double d = Lum(p) - Lum(t);
            var c = Clip(new[] { t[0] + d, t[1] + d, t[2] + d });
            var o = new double[3];
            for (int i = 0; i < 3; i++) o[i] = ToSrgb(p[i] + (c[i] - p[i]) * alpha);
            return o;
        }

        private static double Lum(double[] c) => 0.3 * c[0] + 0.59 * c[1] + 0.11 * c[2];

        private static double[] Clip(double[] c)
        {
            double l = Lum(c), n = Math.Min(c[0], Math.Min(c[1], c[2])), x = Math.Max(c[0], Math.Max(c[1], c[2]));
            if (n < 0) for (int i = 0; i < 3; i++) c[i] = l + (c[i] - l) * l / Math.Max(l - n, 1e-4);
            if (x > 1) for (int i = 0; i < 3; i++) c[i] = l + (c[i] - l) * (1 - l) / Math.Max(x - l, 1e-4);
            return c;
        }

        private static double[] Linear(byte[] c)
        {
            var o = new double[3];
            for (int i = 0; i < 3; i++) { double v = c[i] / 255.0; o[i] = v <= 0.04045 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4); }
            return o;
        }

        private static double ToSrgb(double v)
        {
            v = Math.Max(0, Math.Min(1, v));
            return 255 * (v <= 0.0031308 ? 12.92 * v : 1.055 * Math.Pow(v, 1 / 2.4) - 0.055);
        }

        private static double[] Lab(double[] srgb)
        {
            double L(double v) { v /= 255; return v <= 0.04045 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4); }
            double r = L(srgb[0]), g = L(srgb[1]), b = L(srgb[2]);
            double x = (0.4124 * r + 0.3576 * g + 0.1805 * b) / 0.95047, y = 0.2126 * r + 0.7152 * g + 0.0722 * b, z = (0.0193 * r + 0.1192 * g + 0.9505 * b) / 1.08883;
            double F(double t) => t > 0.008856 ? Math.Pow(t, 1.0 / 3) : 7.787 * t + 16.0 / 116;
            return new[] { 116 * F(y) - 16, 500 * (F(x) - F(y)), 200 * (F(y) - F(z)) };
        }

        private static double Distance(double[] a, double[] b) => Math.Sqrt((a[0] - b[0]) * (a[0] - b[0]) + (a[1] - b[1]) * (a[1] - b[1]) + (a[2] - b[2]) * (a[2] - b[2]));

        /// <summary>The closest pair among the first <paramref name="count"/> owners and the neutral wash, over every chart tone.</summary>
        public static double ClosestPair(int count, out string which)
        {
            double best = double.MaxValue; which = null;
            var colours = new List<(string name, byte[] c, double alpha)>();
            for (int i = 0; i < count; i++) colours.Add(($"owner {i}", Owner(i), 0.7));
            colours.Add(("neutral", Neutral, 0.75));
            foreach (var tone in ChartTones)
                for (int i = 0; i < colours.Count; i++)
                for (int j = i + 1; j < colours.Count; j++)
                {
                    double d = Distance(Lab(Wash(tone, colours[i].c, colours[i].alpha)), Lab(Wash(tone, colours[j].c, colours[j].alpha)));
                    if (d < best) { best = d; which = $"{colours[i].name} and {colours[j].name}"; }
                }
            return best;
        }
    }
}

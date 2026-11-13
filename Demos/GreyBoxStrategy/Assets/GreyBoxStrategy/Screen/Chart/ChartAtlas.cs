using System;
using System.Collections.Generic;

namespace GreyBoxStrategy.Chart
{
    /// <summary>
    /// Every distinct glyph of a scene drawn once, by the same code as the CPU renderer, into one
    /// transparent atlas, and a quad per glyph instance saying where it lands on the chart and where
    /// it is in the atlas. The GPU renderer draws the quads in the scene's order, back to front.
    /// </summary>
    public sealed class ChartAtlas
    {
        public struct Quad
        {
            /// <summary>On the chart, in pixels, top-left origin.</summary>
            public float X0, Y0, X1, Y1;
            /// <summary>In the atlas, 0 to 1, top-left origin.</summary>
            public float U0, V0, U1, V1;
        }

        public int Width, Height;
        /// <summary>RGBA, premultiplied, top row first.</summary>
        public byte[] Rgba;
        public readonly List<Quad> Quads = new List<Quad>();
        /// <summary>How many distinct glyphs it holds.</summary>
        public int Entries;

        private const int Pad = 2;

        /// <param name="s">Chart pixels per grid cell.</param>
        public static ChartAtlas Build(IReadOnlyList<GlyphInstance> glyphs, float s, int maxWidth = 4096)
        {
            var atlas = new ChartAtlas();
            // Each distinct look's cell: its extent around the foot, in pixels.
            var entries = new Dictionary<long, (int x, int y, int w, int h, float footX, float footY, GlyphInstance g)>();
            var order = new List<long>();
            foreach (var g in glyphs)
            {
                long key = g.AtlasKey;
                if (entries.ContainsKey(key)) continue;
                var (l, u, r, d) = ChartGlyphs.Extent(g);
                int w = (int)Math.Ceiling((l + r) * s) + 2 * Pad, h = (int)Math.Ceiling((u + d) * s) + 2 * Pad;
                entries[key] = (0, 0, w, h, l * s + Pad, u * s + Pad, g);
                order.Add(key);
            }
            // Shelf packing, tallest first.
            order.Sort((a, b) => entries[b].h.CompareTo(entries[a].h));
            int width = 1;
            int total = 0;
            foreach (long k in order) total += entries[k].w * entries[k].h;
            width = Math.Min(maxWidth, Math.Max(256, NextPow2((int)Math.Sqrt(total * 1.3))));
            int x = 0, y = 0, shelf = 0;
            foreach (long k in order)
            {
                var e = entries[k];
                if (x + e.w > width) { x = 0; y += shelf; shelf = 0; }
                entries[k] = (x, y, e.w, e.h, e.footX, e.footY, e.g);
                x += e.w; shelf = Math.Max(shelf, e.h);
            }
            int height = Math.Max(1, y + shelf);
            atlas.Width = width; atlas.Height = height; atlas.Entries = order.Count;
            var canvas = new ChartCanvas(width, height, premultiplied: true);
            foreach (long k in order)
            {
                var e = entries[k];
                ChartGlyphs.Draw(canvas, e.g, e.x + e.footX, e.y + e.footY, s);
            }
            atlas.Rgba = canvas.Rgba;
            foreach (var g in glyphs)
            {
                var e = entries[g.AtlasKey];
                float x0 = g.U * s - e.footX, y0 = g.V * s - e.footY;
                atlas.Quads.Add(new Quad
                {
                    X0 = x0, Y0 = y0, X1 = x0 + e.w, Y1 = y0 + e.h,
                    U0 = e.x / (float)width, V0 = e.y / (float)height, U1 = (e.x + e.w) / (float)width, V1 = (e.y + e.h) / (float)height,
                });
            }
            return atlas;
        }

        private static int NextPow2(int v) { int p = 1; while (p < v) p <<= 1; return p; }
    }
}

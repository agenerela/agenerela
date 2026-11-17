using System;

namespace GreyBoxStrategy.Chart
{
    /// <summary>An sRGB colour as three floats, 0 to 255.</summary>
    public struct Ink
    {
        public float R, G, B;
        public Ink(float r, float g, float b) { R = r; G = g; B = b; }
        public static Ink Lerp(Ink a, Ink b, float t) => new Ink(a.R + (b.R - a.R) * t, a.G + (b.G - a.G) * t, a.B + (b.B - a.B) * t);
        public Ink Times(float k) => new Ink(R * k, G * k, B * k);
    }

    /// <summary>
    /// An RGBA byte image, top row first, with the few antialiased shapes the chart's glyphs are
    /// made of. Every shape is drawn by coverage from its distance field, so an edge is as crisp at
    /// 1366 × 768 as at 3840 × 2400.
    /// </summary>
    public sealed class ChartCanvas
    {
        public readonly int Width, Height;
        public readonly byte[] Rgba;

        /// <summary>True for a transparent canvas (an atlas): colours are kept premultiplied by their coverage, and alpha grows as shapes land.</summary>
        public readonly bool Premultiplied;

        public ChartCanvas(int width, int height, bool premultiplied = false)
        {
            Width = width; Height = height; Premultiplied = premultiplied;
            Rgba = new byte[width * height * 4];
        }

        public void Set(int i, Ink c)
        {
            int k = i * 4;
            Rgba[k] = Clamp(c.R); Rgba[k + 1] = Clamp(c.G); Rgba[k + 2] = Clamp(c.B); Rgba[k + 3] = 255;
        }

        public Ink Get(int i) { int k = i * 4; return new Ink(Rgba[k], Rgba[k + 1], Rgba[k + 2]); }

        public void Blend(int x, int y, Ink c, float a)
        {
            if (a <= 0.002f || x < 0 || y < 0 || x >= Width || y >= Height) return;
            if (a > 1) a = 1;
            int k = (y * Width + x) * 4;
            if (Premultiplied)
            {
                // Over, premultiplied: what is there is covered by a.
                Rgba[k] = Clamp(c.R * a + Rgba[k] * (1 - a));
                Rgba[k + 1] = Clamp(c.G * a + Rgba[k + 1] * (1 - a));
                Rgba[k + 2] = Clamp(c.B * a + Rgba[k + 2] * (1 - a));
                Rgba[k + 3] = Clamp(255 * a + Rgba[k + 3] * (1 - a));
                return;
            }
            Rgba[k] = Clamp(Rgba[k] + (c.R - Rgba[k]) * a);
            Rgba[k + 1] = Clamp(Rgba[k + 1] + (c.G - Rgba[k + 1]) * a);
            Rgba[k + 2] = Clamp(Rgba[k + 2] + (c.B - Rgba[k + 2]) * a);
        }

        private static byte Clamp(float v) => v <= 0 ? (byte)0 : v >= 255 ? (byte)255 : (byte)(v + 0.5f);

        private static float Cover(float signedDistance) => signedDistance >= 0.5f ? 1 : signedDistance <= -0.5f ? 0 : signedDistance + 0.5f;

        /// <summary>A filled disc, in pixels.</summary>
        public void Disc(float cx, float cy, float r, Ink c, float a = 1)
        {
            int x0 = (int)Math.Floor(cx - r - 1), x1 = (int)Math.Ceiling(cx + r + 1), y0 = (int)Math.Floor(cy - r - 1), y1 = (int)Math.Ceiling(cy + r + 1);
            for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                float dx = x + 0.5f - cx, dy = y + 0.5f - cy;
                Blend(x, y, c, a * Cover(r - (float)Math.Sqrt(dx * dx + dy * dy)));
            }
        }

        /// <summary>A ring of the given stroke width.</summary>
        public void Ring(float cx, float cy, float r, float width, Ink c, float a = 1)
        {
            float o = r + width;
            int x0 = (int)Math.Floor(cx - o - 1), x1 = (int)Math.Ceiling(cx + o + 1), y0 = (int)Math.Floor(cy - o - 1), y1 = (int)Math.Ceiling(cy + o + 1);
            for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                float dx = x + 0.5f - cx, dy = y + 0.5f - cy;
                float d = Math.Abs((float)Math.Sqrt(dx * dx + dy * dy) - r);
                Blend(x, y, c, a * Cover(width / 2 - d));
            }
        }

        /// <summary>A stroke with round ends.</summary>
        public void Line(float ax, float ay, float bx, float by, float width, Ink c, float a = 1)
        {
            float h = width / 2 + 1;
            int x0 = (int)Math.Floor(Math.Min(ax, bx) - h), x1 = (int)Math.Ceiling(Math.Max(ax, bx) + h);
            int y0 = (int)Math.Floor(Math.Min(ay, by) - h), y1 = (int)Math.Ceiling(Math.Max(ay, by) + h);
            float vx = bx - ax, vy = by - ay, len2 = vx * vx + vy * vy;
            // A stroke thinner than a pixel is drawn a pixel wide, fainter, so it never breaks up.
            float w = Math.Max(width, 1f), fade = Math.Min(1f, width);
            for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                float px = x + 0.5f - ax, py = y + 0.5f - ay;
                float t = len2 > 0 ? Math.Max(0, Math.Min(1, (px * vx + py * vy) / len2)) : 0;
                float dx = px - vx * t, dy = py - vy * t;
                Blend(x, y, c, a * fade * Cover(w / 2 - (float)Math.Sqrt(dx * dx + dy * dy)));
            }
        }

        /// <summary>A filled triangle.</summary>
        public void Triangle(float ax, float ay, float bx, float by, float cx, float cy, Ink c, float a = 1)
        {
            int x0 = (int)Math.Floor(Math.Min(ax, Math.Min(bx, cx))) - 1, x1 = (int)Math.Ceiling(Math.Max(ax, Math.Max(bx, cx))) + 1;
            int y0 = (int)Math.Floor(Math.Min(ay, Math.Min(by, cy))) - 1, y1 = (int)Math.Ceiling(Math.Max(ay, Math.Max(by, cy))) + 1;
            float area = (bx - ax) * (cy - ay) - (by - ay) * (cx - ax);
            if (Math.Abs(area) < 1e-4f) return;
            float s = area > 0 ? 1 : -1;
            for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                float px = x + 0.5f, py = y + 0.5f;
                float d = Math.Min(Edge(ax, ay, bx, by, px, py, s), Math.Min(Edge(bx, by, cx, cy, px, py, s), Edge(cx, cy, ax, ay, px, py, s)));
                Blend(x, y, c, a * Cover(d));
            }
        }

        // Signed distance from the edge a→b, positive inside.
        private static float Edge(float ax, float ay, float bx, float by, float px, float py, float s)
        {
            float ex = bx - ax, ey = by - ay, len = (float)Math.Sqrt(ex * ex + ey * ey);
            return s * ((px - ax) * ey - (py - ay) * ex) / -len;
        }

        /// <summary>A filled half ellipse standing on its base (a hill), centre of the base at (cx, cy).</summary>
        public void Dome(float cx, float cy, float rx, float ry, Ink c, float a = 1, int half = 0)
        {
            int x0 = (int)Math.Floor(cx - rx - 1), x1 = (int)Math.Ceiling(cx + rx + 1), y0 = (int)Math.Floor(cy - ry - 1), y1 = (int)Math.Ceiling(cy + 1);
            for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                float px = x + 0.5f - cx, py = y + 0.5f - cy;
                if (half < 0 && px > 0.5f || half > 0 && px < -0.5f) continue;
                float k = (float)Math.Sqrt(px * px / (rx * rx) + py * py / (ry * ry));
                float d = (1 - k) * Math.Min(rx, ry);
                d = Math.Min(d, -py + 0.5f);
                float cov = Cover(d);
                if (half < 0) cov *= Cover(0.5f - px); else if (half > 0) cov *= Cover(px + 0.5f);
                Blend(x, y, c, a * cov);
            }
        }

        /// <summary>The top arc of an ellipse, as a stroke.</summary>
        public void Arc(float cx, float cy, float rx, float ry, float from, float to, float width, Ink c, float a = 1, int steps = 10)
        {
            float px = cx + rx * (float)Math.Cos(from), py = cy - ry * (float)Math.Sin(from);
            for (int k = 1; k <= steps; k++)
            {
                float t = from + (to - from) * k / steps;
                float nx = cx + rx * (float)Math.Cos(t), ny = cy - ry * (float)Math.Sin(t);
                Line(px, py, nx, ny, width, c, a);
                px = nx; py = ny;
            }
        }

        public void Rect(float x0, float y0, float x1, float y1, Ink c, float a = 1)
        {
            Triangle(x0, y0, x1, y0, x1, y1, c, a);
            Triangle(x0, y0, x1, y1, x0, y1, c, a);
        }
    }
}

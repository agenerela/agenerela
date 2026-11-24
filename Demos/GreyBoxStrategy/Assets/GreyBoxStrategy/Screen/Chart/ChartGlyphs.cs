using System;

namespace GreyBoxStrategy.Chart
{
    /// <summary>
    /// How each glyph is drawn, by code: trees, peaks, hills, reeds, settlements by development,
    /// anchors, passes, bridges, special places, the compass rose. Every one is drawn around its
    /// foot at (x, y) in pixels with <c>s</c> pixels to a grid cell, so the CPU renderer draws it in
    /// place and the GPU renderer draws it once into an atlas cell.
    /// </summary>
    public static class ChartGlyphs
    {
        public static readonly Ink InkDark = new Ink(38, 28, 18);
        public static readonly Ink InkSoft = new Ink(70, 54, 36);
        public static readonly Ink Gilt = new Ink(204, 166, 92);
        public static readonly Ink SeaDeep = new Ink(31, 38, 42);

        /// <summary>How far a glyph reaches from its foot, in cells: left, up, right, down. The atlas cell is sized from it.</summary>
        public static (float left, float up, float right, float down) Extent(GlyphInstance g)
        {
            float z = g.Size;
            switch (g.Kind)
            {
                case GlyphKind.Mountain: return (0.75f * z, 1.15f * z, 1.2f * z, 0.15f);
                case GlyphKind.Hill: return (0.75f * z, 0.5f * z, 0.9f * z, 0.15f);
                case GlyphKind.Conifer: case GlyphKind.RoundTree: return (0.55f * z, 1.2f * z, 0.75f * z, 0.25f);
                case GlyphKind.Settlement: return (2.6f * z, 3.2f * z, 2.6f * z, 1.2f * z);
                case GlyphKind.Special: return (1.3f * z, 1.9f * z, 1.3f * z, 0.5f * z);
                case GlyphKind.Bridge: return (1.1f, 1.1f, 1.1f, 1.1f);
                case GlyphKind.Pass: return (1.4f, 0.8f, 1.4f, 0.8f);
                case GlyphKind.Anchor: return (0.8f, 0.9f, 0.8f, 0.8f);
                case GlyphKind.Rock: return (0.7f * z, 0.9f * z, 0.8f * z, 0.2f);
                case GlyphKind.Rose: return (1.05f * z, 1.05f * z, 1.05f * z, 1.05f * z);
                default: return (1.2f * z, 1.2f * z, 1.2f * z, 0.6f * z);
            }
        }

        public static void Draw(ChartCanvas c, GlyphInstance g, float x, float y, float s)
        {
            float roll = (g.Variant + 0.5f) / 8f;
            switch (g.Kind)
            {
                case GlyphKind.Conifer: Conifer(c, x, y, g.Size, roll, s); break;
                case GlyphKind.RoundTree: RoundTree(c, x, y, g.Size, roll, s); break;
                case GlyphKind.Mountain: Mountain(c, x, y, g.Size, roll, g.Ridge, s); break;
                case GlyphKind.Hill: Hill(c, x, y, g.Size, roll, s); break;
                case GlyphKind.Marsh: Marsh(c, x, y, g.Size, roll, s); break;
                case GlyphKind.Rock: Rock(c, x, y, g.Size, roll, s); break;
                case GlyphKind.Settlement: Settlement(c, x, y, g.Size, g.Param, roll, s); break;
                case GlyphKind.Anchor: Anchor(c, x, y, s); break;
                case GlyphKind.Pass: Pass(c, x, y, s); break;
                case GlyphKind.Bridge: Bridge(c, x, y, g.Param, s); break;
                case GlyphKind.Special: Special(c, x, y, g.Param, g.Size, s); break;
                case GlyphKind.Rose: Rose(c, x, y, g.Size * s, s); break;
            }
        }

        private static float Stroke(float cells, float s) => Math.Max(0.7f, cells * s);

        public static void Conifer(ChartCanvas c, float x, float y, float size, float roll, float s)
        {
            float h = 1.0f * size * s, w = 0.38f * size * s;
            var dark = Ink.Lerp(new Ink(36, 54, 30), new Ink(48, 64, 34), roll);
            var light = Ink.Lerp(new Ink(74, 96, 48), new Ink(92, 108, 54), roll);
            c.Disc(x + w * 0.35f, y + 0.05f * s, w * 0.8f, InkDark, 0.22f);
            c.Line(x, y, x, y - h * 0.22f, Stroke(0.07f, s), InkSoft);
            // Two tiers, so a mass of them reads as a wood rather than a field of arrows.
            c.Triangle(x, y - h, x - w, y - h * 0.18f, x, y - h * 0.18f, light);
            c.Triangle(x, y - h, x, y - h * 0.18f, x + w, y - h * 0.18f, dark);
            c.Triangle(x, y - h * 1.02f, x - w * 0.7f, y - h * 0.55f, x + w * 0.7f, y - h * 0.55f, Ink.Lerp(light, dark, 0.35f), 0.85f);
            c.Line(x, y - h, x + w, y - h * 0.18f, Stroke(0.045f, s), InkDark, 0.6f);
            c.Line(x - w, y - h * 0.18f, x + w, y - h * 0.18f, Stroke(0.04f, s), InkDark, 0.5f);
        }

        public static void RoundTree(ChartCanvas c, float x, float y, float size, float roll, float s)
        {
            float r = 0.36f * size * s;
            c.Disc(x + r * 0.4f, y - r * 0.15f, r, InkDark, 0.22f);
            c.Line(x, y, x, y - r * 1.2f, Stroke(0.07f, s), InkSoft);
            var body = Ink.Lerp(new Ink(60, 82, 38), new Ink(80, 92, 40), roll);
            c.Disc(x, y - r * 1.5f, r, body);
            c.Disc(x - r * 0.45f, y - r * 1.35f, r * 0.62f, body.Times(1.05f));
            c.Disc(x + r * 0.42f, y - r * 1.3f, r * 0.58f, body.Times(0.88f));
            c.Disc(x - r * 0.3f, y - r * 1.8f, r * 0.5f, new Ink(116, 130, 64), 0.8f);
            c.Ring(x, y - r * 1.5f, r, Stroke(0.04f, s), InkDark, 0.5f);
        }

        public static void Mountain(ChartCanvas c, float x, float y, float size, float roll, bool ridge, float s)
        {
            float h = (ridge ? 1.08f : 0.9f) * size * s * (0.85f + 0.3f * roll), w = 0.62f * size * s;
            float ax = x + (roll - 0.5f) * 0.3f * w, ay = y - h;
            var lit = ridge ? new Ink(170, 156, 128) : new Ink(182, 166, 134);
            var shade = ridge ? new Ink(86, 74, 60) : new Ink(104, 90, 72);
            c.Triangle(x - w * 0.2f, y, x + w * 1.25f, y, ax + w * 0.25f, ay + h * 0.32f, InkDark, 0.18f); // shadow
            c.Triangle(ax, ay, x - w, y, ax + w * 0.08f, y, lit);
            c.Triangle(ax, ay, ax + w * 0.08f, y, x + w, y, shade);
            // A shoulder on one side: no two peaks the same.
            float sx = roll < 0.5f ? x - w * 0.55f : x + w * 0.5f, sy = y - h * 0.45f;
            c.Triangle(sx, sy, sx - w * 0.45f, y, sx + w * 0.45f, y, roll < 0.5f ? lit.Times(0.95f) : shade.Times(1.05f), 0.95f);
            for (int k = 1; k <= 4; k++)
            {
                float t = k / 5f;
                float hx = ax + (x + w - ax) * t, hy = ay + (y - ay) * t;
                c.Line(hx, hy, hx - w * 0.3f, hy + h * 0.3f, Stroke(0.035f, s), InkDark, 0.45f);
            }
            if (size * (0.85f + 0.3f * roll) > 2.6f) c.Triangle(ax, ay, ax - w * 0.2f, ay + h * 0.22f, ax + w * 0.22f, ay + h * 0.2f, new Ink(232, 226, 210), 0.95f);
            c.Line(ax, ay, x - w, y, Stroke(0.055f, s), InkDark, 0.85f);
            c.Line(ax, ay, x + w, y, Stroke(0.055f, s), InkDark, 0.85f);
        }

        public static void Hill(ChartCanvas c, float x, float y, float size, float roll, float s)
        {
            float rx = 0.72f * size * s, ry = (0.38f + 0.12f * roll) * size * s;
            c.Dome(x + rx * 0.15f, y + 0.04f * s, rx, ry, InkDark, 0.15f);
            c.Dome(x, y, rx, ry, new Ink(160, 140, 96), 1, -1);
            c.Dome(x, y, rx, ry, new Ink(118, 100, 68), 1, 1);
            c.Arc(x, y, rx, ry, 0, (float)Math.PI, Stroke(0.05f, s), InkDark, 0.8f);
            c.Arc(x + rx * 0.1f, y, rx * 0.55f, ry * 0.55f, 0.2f, 1.3f, Stroke(0.035f, s), InkDark, 0.4f, 5);
        }

        public static void Marsh(ChartCanvas c, float x, float y, float size, float roll, float s)
        {
            float w = 0.45f * size * s;
            var water = new Ink(74, 98, 102); var reed = new Ink(56, 70, 40);
            c.Line(x - w, y, x + w, y, Stroke(0.06f, s), water, 0.85f);
            c.Line(x - w * 0.6f, y + 0.18f * s, x + w * 0.4f, y + 0.18f * s, Stroke(0.05f, s), water, 0.7f);
            int n = roll < 0.5f ? 3 : 4;
            for (int k = 0; k < n; k++)
            {
                float kx = (k - (n - 1) / 2f) * 0.13f * s * size;
                c.Line(x + kx, y - 0.02f * s, x + kx * 1.7f, y - 0.45f * s * size, Stroke(0.045f, s), reed, 0.9f);
            }
        }

        public static void Rock(ChartCanvas c, float x, float y, float size, float roll, float s)
        {
            float w = 0.6f * size * s, h = 0.7f * size * s;
            c.Triangle(x - w, y, x + w, y, x - w * 0.1f, y - h, new Ink(150, 136, 112));
            c.Triangle(x - w * 0.1f, y - h, x + w, y, x + w * 0.4f, y - h * 0.4f, new Ink(98, 86, 70));
            c.Line(x - w, y, x - w * 0.1f, y - h, Stroke(0.05f, s), InkDark, 0.8f);
        }

        private static void House(ChartCanvas c, float x, float y, float w, Ink roof, float s)
        {
            float h = w * 0.7f;
            c.Rect(x - w / 2, y - h, x + w / 2, y, new Ink(210, 192, 156));
            c.Line(x + w / 2, y - h, x + w / 2, y, Stroke(0.03f, s), InkDark, 0.4f);
            c.Triangle(x - w * 0.62f, y - h, x + w * 0.62f, y - h, x, y - h - w * 0.6f, roof);
            c.Line(x - w * 0.62f, y - h, x, y - h - w * 0.6f, Stroke(0.03f, s), InkDark, 0.7f);
            c.Line(x + w * 0.62f, y - h, x, y - h - w * 0.6f, Stroke(0.03f, s), InkDark, 0.7f);
            c.Line(x - w / 2, y, x + w / 2, y, Stroke(0.03f, s), InkDark, 0.6f);
        }

        private static void Spire(ChartCanvas c, float x, float y, float u, float k, float s)
        {
            c.Rect(x - u * 0.28f, y - u * 1.2f * k, x + u * 0.28f, y, new Ink(200, 184, 152));
            c.Triangle(x - u * 0.36f, y - u * 1.2f * k, x + u * 0.36f, y - u * 1.2f * k, x, y - u * 2.5f * k, new Ink(110, 68, 48));
            c.Line(x, y - u * 2.5f * k, x, y - u * 2.85f * k, Stroke(0.03f, s), InkDark);
        }

        private static void Tower(ChartCanvas c, float x, float y, float u, float s)
        {
            var stone = new Ink(176, 164, 140);
            c.Rect(x - u * 0.38f, y - u * 1.3f, x + u * 0.38f, y, stone);
            for (int k = -1; k <= 1; k++) c.Rect(x + k * u * 0.26f - u * 0.1f, y - u * 1.5f, x + k * u * 0.26f + u * 0.1f, y - u * 1.28f, stone);
            c.Line(x - u * 0.38f, y - u * 1.3f, x - u * 0.38f, y, Stroke(0.035f, s), InkDark, 0.8f);
            c.Line(x + u * 0.38f, y - u * 1.3f, x + u * 0.38f, y, Stroke(0.035f, s), InkDark, 0.8f);
            c.Rect(x - u * 0.08f, y - u * 0.9f, x + u * 0.08f, y - u * 0.6f, InkDark, 0.8f);
        }

        /// <summary>
        /// A settlement by its tier: a hamlet (one or two houses), a village, a large village with
        /// its chapel, a town with a spire, a city with two spires inside walls. Forts add towers; a
        /// capital flies a banner.
        /// </summary>
        public static void Settlement(ChartCanvas c, float x, float y, float size, int look, float roll, float s)
        {
            int tier = SettlementLook.Tier(look), forts = SettlementLook.Forts(look);
            bool capital = SettlementLook.Capital(look);
            var roof = new Ink(142, 62, 42); var roof2 = new Ink(116, 70, 50); var roof3 = new Ink(96, 76, 60);
            float u = 0.95f * s * size;
            // Houses on a little patch of trodden ground, so the town reads against any wash.
            c.Disc(x, y - u * 0.4f, u * (0.9f + 0.45f * tier), new Ink(186, 168, 126), 0.22f);
            (float dx, float dy, float w, int r)[] layout;
            switch (tier)
            {
                case 0: layout = roll < 0.5f ? new[] { (0f, 0f, 1f, 0) } : new[] { (-0.6f, 0.1f, 0.9f, 0), (0.55f, -0.05f, 0.85f, 1) }; break;
                case 1: layout = new[] { (-0.9f, -0.15f, 1f, 1), (0.85f, -0.3f, 0.9f, 0), (0f, 0.25f, 1f, 2) }; break;
                case 2: layout = new[] { (-1.3f, -0.3f, 0.95f, 1), (1.2f, -0.35f, 0.9f, 0), (-0.45f, 0.35f, 1f, 2), (0.6f, 0.35f, 0.95f, 0) }; break;
                case 3: layout = new[] { (-1.7f, -0.4f, 0.9f, 1), (1.6f, -0.45f, 0.95f, 0), (-1.05f, 0.4f, 1f, 2), (1.0f, 0.4f, 0.95f, 0), (-0.3f, 0.55f, 0.9f, 1), (2.1f, 0.3f, 0.8f, 2) }; break;
                default: layout = new[] { (-2.1f, -0.55f, 0.9f, 1), (2.0f, -0.6f, 0.95f, 0), (-1.5f, 0.35f, 1f, 2), (1.45f, 0.35f, 0.95f, 0), (-0.55f, 0.6f, 0.9f, 1), (0.6f, 0.65f, 0.95f, 2), (-2.4f, 0.55f, 0.8f, 0), (2.45f, 0.5f, 0.8f, 1), (1.0f, -0.75f, 0.85f, 2) }; break;
            }
            if (tier >= 4 || capital)
            {
                // The city wall: a ring with towers, behind the houses.
                float rw = u * 3.0f, rh = u * 1.7f;
                c.Arc(x, y - u * 0.3f, rw, rh, 0, (float)(2 * Math.PI), Stroke(0.12f * size, s), new Ink(150, 136, 110), 1, 36);
                c.Arc(x, y - u * 0.3f, rw, rh, 0, (float)(2 * Math.PI), Stroke(0.04f, s), InkDark, 0.8f, 36);
            }
            // Back row first.
            foreach (var (dx, dy, w, r) in layout)
                if (dy < 0) House(c, x + dx * u, y + dy * u, w * u, r == 0 ? roof : r == 1 ? roof2 : roof3, s);
            if (tier == 2) Spire(c, x + 0.1f * u, y - 0.2f * u, u * 0.75f, 0.7f, s);
            if (tier >= 3) Spire(c, x + 0.15f * u, y - 0.3f * u, u, 1f, s);
            if (tier >= 4) Spire(c, x - 0.95f * u, y - 0.35f * u, u * 0.85f, 0.85f, s);
            foreach (var (dx, dy, w, r) in layout)
                if (dy >= 0) House(c, x + dx * u, y + dy * u, w * u, r == 0 ? roof : r == 1 ? roof2 : roof3, s);
            // Forts: a keep beside the town, two for a fortress.
            for (int k = 0; k < forts; k++)
            {
                float side = k == 0 ? 1 : -1;
                Tower(c, x + side * u * (1.4f + 0.45f * tier), y + u * 0.15f, u * 0.95f, s);
            }
            if (capital)
            {
                float px = x + 0.15f * u, py = y - 3.2f * u;
                c.Line(px, py, px, py + 0.6f * u, Stroke(0.04f, s), InkDark);
                c.Triangle(px, py, px + 0.75f * u, py + 0.17f * u, px, py + 0.34f * u, new Ink(196, 60, 44));
            }
        }

        public static void Anchor(ChartCanvas c, float x, float y, float s)
        {
            float u = 0.6f * s, lw = Stroke(0.11f, s);
            var ink = new Ink(30, 36, 44);
            c.Disc(x, y, u * 1.35f, new Ink(214, 200, 164), 0.55f);
            c.Line(x, y - u, x, y + u * 0.8f, lw, ink);
            c.Ring(x, y - u * 1.15f, u * 0.22f, lw * 0.8f, ink);
            c.Line(x - u * 0.45f, y - u * 0.55f, x + u * 0.45f, y - u * 0.55f, lw, ink);
            c.Arc(x, y + u * 0.15f, u * 0.75f, u * 0.65f, (float)Math.PI, (float)(2 * Math.PI), lw, ink, 1, 8);
        }

        public static void Pass(ChartCanvas c, float x, float y, float s)
        {
            float u = 0.7f * s, lw = Stroke(0.1f, s);
            c.Arc(x - u * 1.1f, y, u * 0.55f, u * 0.9f, -1.1f, 1.1f, lw, InkDark, 0.9f, 6);
            c.Arc(x + u * 1.1f, y, u * 0.55f, u * 0.9f, (float)Math.PI - 1.1f, (float)Math.PI + 1.1f, lw, InkDark, 0.9f, 6);
        }

        /// <summary>A bridge across the river: a plank deck with railings, across the river's run (<paramref name="angle"/> in degrees).</summary>
        public static void Bridge(ChartCanvas c, float x, float y, int angle, float s)
        {
            double a = angle * Math.PI / 180 + Math.PI / 2; // across the river
            float dx = (float)Math.Cos(a), dy = (float)Math.Sin(a), px = -dy, py = dx;
            float len = 0.95f * s, half = 0.2f * s;
            var deck = new Ink(178, 150, 104);
            c.Line(x - dx * len, y - dy * len, x + dx * len, y + dy * len, half * 2.4f, InkDark, 0.9f);
            c.Line(x - dx * len, y - dy * len, x + dx * len, y + dy * len, half * 1.6f, deck);
            c.Line(x - dx * len + px * half, y - dy * len + py * half, x + dx * len + px * half, y + dy * len + py * half, Stroke(0.035f, s), InkDark, 0.9f);
            c.Line(x - dx * len - px * half, y - dy * len - py * half, x + dx * len - px * half, y + dy * len - py * half, Stroke(0.035f, s), InkDark, 0.9f);
        }

        /// <summary>
        /// A special place as its own landmark, larger than a village and gilt-edged, so it reads at
        /// a glance: a ruined castle, a mine's adit with its pick, a lake, a spring, horses' plain,
        /// a great tree, a battlefield's crossed swords, the crown's seat.
        /// </summary>
        public static void Special(ChartCanvas c, float x, float y, int template, float size, float s)
        {
            float u = 0.55f * s * size, lw = Stroke(0.09f, s);
            var ink = InkDark;
            // A gilt halo under it, the family mark of every special place.
            c.Disc(x, y - u * 0.9f, u * 1.55f, Gilt, 0.28f);
            c.Ring(x, y - u * 0.9f, u * 1.55f, Stroke(0.06f, s), Gilt, 0.9f);
            float cx = x, cy = y - u * 0.9f;
            var stone = new Ink(170, 158, 134);
            switch (template)
            {
                case 0: // old castle, in ruins
                    c.Rect(cx - u * 0.9f, cy - u * 0.2f, cx + u * 0.9f, cy + u * 0.7f, stone);
                    c.Rect(cx - u * 0.9f, cy - u * 0.9f, cx - u * 0.35f, cy + u * 0.7f, stone);
                    c.Rect(cx + u * 0.35f, cy - u * 0.6f, cx + u * 0.9f, cy + u * 0.7f, stone);
                    for (int k = 0; k < 2; k++) c.Rect(cx - u * 0.9f + k * u * 0.33f, cy - u * 1.1f, cx - u * 0.75f + k * u * 0.33f, cy - u * 0.88f, stone);
                    c.Triangle(cx - u * 0.35f, cy - u * 0.2f, cx + u * 0.1f, cy - u * 0.2f, cx - u * 0.35f, cy - u * 0.55f, stone);
                    c.Rect(cx - u * 0.15f, cy + u * 0.15f, cx + u * 0.15f, cy + u * 0.7f, ink, 0.85f);
                    c.Line(cx - u * 0.9f, cy + u * 0.7f, cx + u * 0.9f, cy + u * 0.7f, lw, ink);
                    break;
                case 1: case 2: // a mine: the adit in the hillside, its pick
                    c.Dome(cx, cy + u * 0.7f, u * 1.1f, u * 1.0f, new Ink(140, 120, 90));
                    c.Dome(cx, cy + u * 0.7f, u * 0.42f, u * 0.55f, ink);
                    c.Arc(cx, cy + u * 0.7f, u * 1.1f, u * 1.0f, 0, (float)Math.PI, lw, ink, 0.9f);
                    var head = template == 1 ? new Ink(236, 236, 240) : new Ink(90, 90, 96);
                    c.Line(cx + u * 0.5f, cy + u * 0.4f, cx + u * 1.1f, cy - u * 0.5f, lw, InkSoft);
                    c.Arc(cx + u * 1.1f, cy - u * 0.5f, u * 0.45f, u * 0.3f, 0.3f, 2.8f, lw * 1.3f, head, 1, 6);
                    break;
                case 3: // a lake with a chapel on its shore
                    c.Disc(cx, cy + u * 0.2f, u * 1.0f, new Ink(64, 100, 122));
                    c.Ring(cx, cy + u * 0.2f, u * 1.0f, lw, ink, 0.9f);
                    c.Arc(cx, cy + u * 0.2f, u * 0.6f, u * 0.25f, 0.4f, 2.7f, Stroke(0.04f, s), new Ink(170, 196, 206), 0.9f);
                    c.Disc(cx + u * 0.2f, cy - u * 0.95f, u * 0.18f, new Ink(250, 244, 220));
                    break;
                case 4: // a spring: a pool and its rising steam
                    c.Disc(cx, cy + u * 0.45f, u * 0.75f, new Ink(70, 120, 150));
                    c.Ring(cx, cy + u * 0.45f, u * 0.75f, lw * 0.8f, ink, 0.8f);
                    for (int k = -1; k <= 1; k++) c.Arc(cx + k * u * 0.35f, cy - u * 0.3f, u * 0.15f, u * 0.5f, 1.6f, 4.6f, Stroke(0.05f, s), new Ink(220, 224, 226), 0.9f, 6);
                    break;
                case 5: // horses' plain: a horseshoe
                    c.Arc(cx, cy, u * 0.75f, u * 0.85f, (float)Math.PI * 0.95f, (float)Math.PI * 2.05f, lw * 2.0f, ink, 1, 12);
                    for (int k = -1; k <= 1; k += 2) c.Disc(cx + k * u * 0.55f, cy + u * 0.2f, u * 0.08f, Gilt);
                    break;
                case 6: // a great tree
                    c.Line(cx, cy + u * 0.9f, cx, cy, lw * 1.6f, InkSoft);
                    c.Disc(cx, cy - u * 0.25f, u * 0.95f, new Ink(48, 78, 34));
                    c.Disc(cx - u * 0.35f, cy - u * 0.45f, u * 0.5f, new Ink(96, 126, 60), 0.85f);
                    c.Ring(cx, cy - u * 0.25f, u * 0.95f, lw * 0.8f, ink, 0.7f);
                    break;
                case 7: // an old battlefield: crossed swords
                    c.Line(cx - u * 0.85f, cy + u * 0.85f, cx + u * 0.85f, cy - u * 0.85f, lw * 1.2f, new Ink(200, 200, 206));
                    c.Line(cx + u * 0.85f, cy + u * 0.85f, cx - u * 0.85f, cy - u * 0.85f, lw * 1.2f, new Ink(200, 200, 206));
                    c.Line(cx - u * 0.7f, cy + u * 0.15f, cx - u * 0.15f, cy + u * 0.7f, lw, ink);
                    c.Line(cx + u * 0.7f, cy + u * 0.15f, cx + u * 0.15f, cy + u * 0.7f, lw, ink);
                    break;
                default: // the crown's seat
                    c.Rect(cx - u * 0.8f, cy + u * 0.15f, cx + u * 0.8f, cy + u * 0.55f, Gilt);
                    c.Triangle(cx - u * 0.8f, cy + u * 0.2f, cx - u * 0.4f, cy + u * 0.2f, cx - u * 0.75f, cy - u * 0.7f, Gilt);
                    c.Triangle(cx - u * 0.25f, cy + u * 0.2f, cx + u * 0.25f, cy + u * 0.2f, cx, cy - u * 0.85f, Gilt);
                    c.Triangle(cx + u * 0.4f, cy + u * 0.2f, cx + u * 0.8f, cy + u * 0.2f, cx + u * 0.75f, cy - u * 0.7f, Gilt);
                    c.Line(cx - u * 0.8f, cy + u * 0.55f, cx + u * 0.8f, cy + u * 0.55f, lw, ink);
                    break;
            }
        }

        public static void Rose(ChartCanvas c, float x, float y, float r, float s)
        {
            var light = new Ink(224, 200, 146); var dark = new Ink(120, 86, 44);
            c.Disc(x, y, r * 0.78f, SeaDeep, 0.55f);
            c.Ring(x, y, r * 0.78f, Stroke(0.06f, s), Gilt, 0.8f);
            c.Ring(x, y, r * 0.7f, Stroke(0.035f, s), Gilt, 0.6f);
            for (int k = 0; k < 16; k++)
            {
                double a = k * Math.PI / 8 - Math.PI / 2;
                float len = k % 4 == 0 ? r : k % 2 == 0 ? r * 0.66f : r * 0.42f;
                float half = (k % 4 == 0 ? 0.13f : 0.1f) * r;
                float tx = x + len * (float)Math.Cos(a), ty = y + len * (float)Math.Sin(a);
                float px = -(float)Math.Sin(a), py = (float)Math.Cos(a);
                if (k % 2 == 1) half *= 0.7f;
                c.Triangle(x, y, tx, ty, x + px * half, y + py * half, k == 0 ? new Ink(196, 70, 50) : light);
                c.Triangle(x, y, x - px * half, y - py * half, tx, ty, dark);
                c.Line(x + px * half, y + py * half, tx, ty, Stroke(0.03f, s), InkDark, 0.8f);
                c.Line(x - px * half, y - py * half, tx, ty, Stroke(0.03f, s), InkDark, 0.8f);
            }
            c.Disc(x, y, r * 0.06f, Gilt);
            c.Ring(x, y, r * 0.06f, Stroke(0.03f, s), InkDark);
        }
    }
}

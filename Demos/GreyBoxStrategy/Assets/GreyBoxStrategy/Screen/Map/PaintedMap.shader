// The painted map: the sea-chart painting, every province washed in its owner's colour, and the
// capture of a province drawn as colour bleeding in from the attacker's side.
//
// Two layers, as the round-2 mock-up found (docs/demos/grey-box-strategy-screen.md, "What building
// it taught"): a hue-and-saturation tint that keeps the painting's light and shade (the W3C
// "color" blend), then ink over it with ordinary alpha: borders, pigment pooled at the edge,
// hatching on neutral land, the wet edge of a capture.
//
// Inputs are data, so animation is numbers changing:
//   _IdTex     province index * 10 per pixel, 255 for sea (point); with _IdWide, a 16-bit index in
//              red (low byte) and green (high byte), 65534 and 65535 for water: a generated world
//   _EdgeTex   distance to the nearest border, 16 per pixel (bilinear)
//   _StateTex  one column per province (at least 32), 4 rows: row 0 owner colour + owner slot, row 1 the
//              colour it had before a capture, row 2 (front, fall, capturing, selected),
//              row 3 (reach highlight, dim, 1 - wash strength for the map mode, hatch thickening),
//              row 4 occupier colour + hatching amount, row 5 (sweep centre u, v, sweep angle / 2pi, 1 = drawn in
//              by a sweep from that side, 0 = lifting from the edges inward): the writs turn's occupation
//   _SpreadTex distance from the attacker's border inside the province being captured, 0..1
//   _CellTex   a generated world's cell grid, 16-bit in RG as _IdTex, top row first (point). With _Geo on,
//              each pixel takes its province from the cells by the same noisy vote as ChartRaster.IdAtCell,
//              at the screen's resolution rather than the province map's, so borders stay sharp at any zoom
//              and the vote's margin gives the distance to the border for antialiased lines.
Shader "GreyBoxStrategy/PaintedMap"
{
    Properties
    {
        _MainTex ("Painting", 2D) = "white" {}
        _IdTex ("Province ids", 2D) = "white" {}
        _EdgeTex ("Distance to border", 2D) = "black" {}
        _StateTex ("Province state", 2D) = "black" {}
        _SpreadTex ("Capture spread", 2D) = "white" {}
        _Focus ("Focus (uv x, uv y, radius, amount)", Vector) = (0.5, 0.5, 0.1, 0)
        _Engraved ("Engraved style", Float) = 0
        _EdgeFade ("Fade into the open sea at the edge (uv)", Float) = 0.04
        _IdWide ("Province ids are 16-bit (a generated world)", Float) = 0
        _HatchReach ("A province's reach in uv, for the hatching's sweep", Float) = 0.09
        _CellTex ("Cell grid (a generated world)", 2D) = "black" {}
        _DetailTex ("The chart's visible window, drawn at screen scale", 2D) = "black" {}
        _DetailRect ("The window in uv: x, y, width, height", Vector) = (0, 0, 1, 1)
        _DetailOn ("Use the window", Float) = 0
        _Geo ("Provinces from the cell grid, per screen pixel", Float) = 0
        _Grid ("Grid width, height in cells, province-map pixels per cell", Vector) = (1, 1, 1, 0)
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "PaintedMap"
            ZWrite Off
            Cull Off
            Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            TEXTURE2D(_IdTex); SAMPLER(sampler_point_clamp);
            TEXTURE2D(_EdgeTex); SAMPLER(sampler_EdgeTex);
            TEXTURE2D(_StateTex);
            TEXTURE2D(_SpreadTex); SAMPLER(sampler_SpreadTex);
            float4 _IdTex_TexelSize;
            float4 _Focus;
            float _Engraved;
            float _EdgeFade;
            float _IdWide;
            float _HatchReach;
            TEXTURE2D(_CellTex);
            TEXTURE2D(_DetailTex); SAMPLER(sampler_DetailTex);
            float4 _DetailRect;
            float _DetailOn;
            float _Geo;
            float4 _Grid;
            int _Seed;

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                return o;
            }

            // ---- helpers ----
            float3 ToLinear(float3 c)
            {
                #if defined(UNITY_COLORSPACE_GAMMA)
                return c;
                #else
                return c <= 0.04045 ? c / 12.92 : pow((c + 0.055) / 1.055, 2.4);
                #endif
            }
            float Hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            float ValueNoise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                f = f * f * (3 - 2 * f);
                float a = Hash(i), b = Hash(i + float2(1, 0)), c = Hash(i + float2(0, 1)), d = Hash(i + float2(1, 1));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }
            // Paper grain and pigment granulation: three octaves, as the mock-up's noise.
            float Grain(float2 px) { return 0.5 * ValueNoise(px / 64) + 0.3 * ValueNoise(px / 18) + 0.2 * ValueNoise(px / 5); }

            float Lum(float3 c) { return dot(c, float3(0.3, 0.59, 0.11)); }
            float3 ClipColor(float3 c)
            {
                float l = Lum(c), n = min(c.r, min(c.g, c.b)), x = max(c.r, max(c.g, c.b));
                if (n < 0) c = l + (c - l) * l / max(l - n, 1e-4);
                if (x > 1) c = l + (c - l) * (1 - l) / max(x - l, 1e-4);
                return c;
            }
            // W3C "color" blend: the tint's hue and saturation, the painting's luminosity.
            float3 ColorBlend(float3 painting, float3 tint) { return ClipColor(tint + (Lum(painting) - Lum(tint))); }

            int IdAt(float2 uv)
            {
                float4 t = SAMPLE_TEXTURE2D(_IdTex, sampler_point_clamp, uv);
                if (_IdWide > 0.5)
                {
                    int w = (int)round(t.r * 255) + 256 * (int)round(t.g * 255);
                    return w >= 65534 ? -1 : w;
                }
                float v = t.r * 255;
                return v > 250 ? -1 : (int)round(v / 10);
            }
            // ---- ChartNoise and ChartRaster.IdAtCell, bit for bit (the ground shader carries the same port) ----
            uint HashI(int x, int y, int seed)
            {
                uint h = (uint)x * 374761393u + (uint)y * 668265263u + (uint)seed * 2246822519u;
                h = (h ^ (h >> 13)) * 1274126177u;
                return h ^ (h >> 16);
            }
            float HashI01(int x, int y, int seed) { return (HashI(x, y, seed) & 0xFFFFFFu) / 16777216.0; }
            float ValueI(float x, float y, int seed)
            {
                int x0 = (int)floor(x), y0 = (int)floor(y);
                float fx = x - x0, fy = y - y0;
                fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy);
                float a = HashI01(x0, y0, seed), b = HashI01(x0 + 1, y0, seed), c = HashI01(x0, y0 + 1, seed), d = HashI01(x0 + 1, y0 + 1, seed);
                return (a + (b - a) * fx) * (1 - fy) + (c + (d - c) * fx) * fy;
            }
            float FbmI(float x, float y, int seed)
            {
                return 0.55 * ValueI(x, y, seed) + 0.30 * ValueI(x * 2 + 0.37, y * 2 + 0.61, seed + 1) + 0.15 * ValueI(x * 4 + 0.13, y * 4 + 0.29, seed + 2);
            }
            // A cell's id as the province map has it: a province's index, or -1 for sea, inland water and scenery islands.
            int CellId(int x, int y)
            {
                x = clamp(x, 0, (int)_Grid.x - 1); y = clamp(y, 0, (int)_Grid.y - 1);
                float4 t = LOAD_TEXTURE2D(_CellTex, int2(x, y));
                int w = (int)round(t.r * 255) + 256 * (int)round(t.g * 255);
                return w >= 65533 ? -1 : w;
            }
            static const float BorderH = 0.02;   // the step, in cells, the vote's slope is measured over
            // The noisy point the vote is taken at, for a point in grid units (v down).
            float2 Warp(float2 g)
            {
                const float amp = 0.42, freq = 0.38;
                return float2(g.x + amp * (FbmI(g.x * freq, g.y * freq, _Seed + 11) * 2 - 1),
                              g.y + amp * (FbmI(g.x * freq + 17.3, g.y * freq - 5.1, _Seed + 29) * 2 - 1));
            }
            // Two provinces' weights at a point: the border between them is where the two are equal.
            float PairMargin(float2 g, int a, int b)
            {
                float2 wp = Warp(g);
                int cx = (int)floor(wp.x), cy = (int)floor(wp.y);
                float m = 0;
                [unroll] for (int dy = -1; dy <= 1; dy++)
                [unroll] for (int dx = -1; dx <= 1; dx++)
                {
                    float ex = cx + dx + 0.5 - wp.x, ey = cy + dy + 0.5 - wp.y;
                    float w = max(0, 1.44 - (ex * ex + ey * ey));
                    int id = CellId(cx + dx, cy + dy);
                    m += id == a ? w : id == b ? -w : 0;
                }
                return m;
            }
            // The vote at a point in grid units (v down): the winner, the runner-up (-2 for none), and the margin
            // between their weights, which is 0 on the border between them and grows smoothly away from it.
            void Vote(float2 g, out int best, out int second, out float margin)
            {
                const float r2 = 1.44;
                float2 wp = Warp(g);
                float wu = wp.x, wv = wp.y;
                int cx = (int)floor(wu), cy = (int)floor(wv);
                int ids[9]; float ws[9]; int kinds = 0;
                [unroll] for (int k = 0; k < 9; k++) { ids[k] = -3; ws[k] = 0; }
                [unroll] for (int dy = -1; dy <= 1; dy++)
                [unroll] for (int dx = -1; dx <= 1; dx++)
                {
                    float ex = cx + dx + 0.5 - wu, ey = cy + dy + 0.5 - wv;
                    float w = r2 - (ex * ex + ey * ey);
                    if (w > 0)
                    {
                        int id = CellId(cx + dx, cy + dy);
                        int slot = min(kinds, 8);
                        [unroll] for (int j = 0; j < 9; j++) if (j < kinds && ids[j] == id) slot = j;
                        if (slot == kinds) kinds++;
                        // Written through fixed indices: a computed one makes the compiler doubt the bounds.
                        [unroll] for (int q = 0; q < 9; q++) if (q == slot) { ids[q] = id; ws[q] += w; }
                    }
                }
                best = CellId(cx, cy); second = -2; margin = 1e3;
                if (kinds == 0) return;
                // Unused slots weigh 0 and every used one more, so the scan needs no count.
                float w1 = -1, w2 = -1; int i1 = -3, i2 = -2;
                [unroll] for (int k1 = 0; k1 < 9; k1++)
                {
                    if (k1 >= kinds) continue;
                    if (ws[k1] > w1) { w2 = w1; i2 = i1; w1 = ws[k1]; i1 = ids[k1]; }
                    else if (ws[k1] > w2) { w2 = ws[k1]; i2 = ids[k1]; }
                }
                best = i1;
                if (w2 >= 0) { second = i2; margin = w1 - w2; }
            }

            float4 State(int id, int row) { return LOAD_TEXTURE2D(_StateTex, int2(id, row)); }
            float OwnerSlot(int id) { return id < 0 ? -1 : State(id, 0).a; }

            // How close (in pixels) a pixel is to a border between two different owners: 0, 1 or 2 rings.
            float CountryBorder(float2 uv, int id)
            {
                float mine = OwnerSlot(id);
                float2 t = _IdTex_TexelSize.xy;
                float ring = 0;
                [unroll] for (int r = 1; r <= 3; r++)
                {
                    float2 d = t * r;
                    int a = IdAt(uv + float2(d.x, 0)), b = IdAt(uv - float2(d.x, 0)), c = IdAt(uv + float2(0, d.y)), e = IdAt(uv - float2(0, d.y));
                    bool differs = (a >= 0 && abs(OwnerSlot(a) - mine) > 0.001) || (b >= 0 && abs(OwnerSlot(b) - mine) > 0.001)
                                || (c >= 0 && abs(OwnerSlot(c) - mine) > 0.001) || (e >= 0 && abs(OwnerSlot(e) - mine) > 0.001);
                    if (differs && ring == 0) ring = r;
                }
                return ring;
            }

            // Coverage of the band [0, width) of a repeating pattern at phase f (0 to 1), antialiased over w, the
            // phase's change across one screen pixel: the hatching keeps clean edges at any zoom.
            float Band(float f, float width, float w)
            {
                if (width >= 1) return 1;
                float inside = f < width ? min(f, width - f) : -min(f - width, 1 - f);
                return saturate(0.5 + inside / max(w, 1e-5));
            }

            // Hatching repeats every 9 province-map pixels, but never more than 22 screen pixels apart: zoomed in, broad
            // bands would bury the land drawn under them.
            float HatchPeriod(float texelsPerPixel) { return min(9.0, 22.0 * texelsPerPixel); }

            half4 frag(Varyings i) : SV_Target
            {
                float3 painting = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv).rgb;
                // Zoomed in on a drawn chart, its visible window drawn again at screen scale replaces the magnified picture.
                if (_DetailOn > 0.5)
                {
                    float2 d = (i.uv - _DetailRect.xy) / _DetailRect.zw;
                    if (all(d >= 0) && all(d <= 1)) painting = SAMPLE_TEXTURE2D_LOD(_DetailTex, sampler_DetailTex, d, 0).rgb;
                }
                float2 px = i.uv * _IdTex_TexelSize.zw;
                int id = IdAt(i.uv);
                float3 colour = painting;
                // A generated world: the province, and how far the border is, from the cell grid at this pixel.
                bool geo = _Geo > 0.5;
                int across = -2; float borderTx = 1e3, texelsPerPixel = max(1e-4, max(fwidth(px.x), fwidth(px.y)));
                // Deep inside a province the province map already knows the answer: the vote runs only within 5 of its
                // pixels of a border, where the lines are, which keeps the cost to a fraction of the screen.
                if (geo && SAMPLE_TEXTURE2D_LOD(_EdgeTex, sampler_EdgeTex, i.uv, 0).r * 255 / 16 < 5)
                {
                    float2 g = float2(i.uv.x, 1 - i.uv.y) * _Grid.xy;
                    float margin;
                    Vote(g, id, across, margin);
                    // The margin over its slope is the distance to the border, in cells, then in province-map pixels, the
                    // unit every width below is written in. The slope is taken a fiftieth of a cell away for the same two
                    // provinces, so it is smooth; screen derivatives of the margin were too coarse and frayed the lines.
                    if (across != -2)
                    {
                        // Newton steps onto the border (where the two weights are equal), each at most a quarter cell, then
                        // the distance to that point: the margin over its slope alone frays the gilt, as the weights bend
                        // within a cell. Far from the border the edge field is used anyway, so the walk stops there.
                        float2 at = g; float m = margin;
                        [unroll] for (int step = 0; step < 3; step++)
                        {
                            float2 grad = (float2(PairMargin(at + float2(BorderH, 0), id, across), PairMargin(at + float2(0, BorderH), id, across)) - m) / BorderH;
                            float2 move = -m * grad / max(dot(grad, grad), 1e-6);
                            float len = length(move);
                            at += len > 0.25 ? move * (0.25 / len) : move;
                            if (length(at - g) * _Grid.z > 4) break;
                            m = PairMargin(at, id, across);
                            if (abs(m) < 1e-3) break;
                        }
                        borderTx = length(at - g) * _Grid.z;
                    }
                }

                if (id >= 0)
                {
                    float edgePx = SAMPLE_TEXTURE2D(_EdgeTex, sampler_EdgeTex, i.uv).r * 255 / 16;
                    // Near a border the vote's distance is exact and smooth; farther in, where the vote sees only one
                    // province, the edge field takes over. A pixel next to the border is 0 in the edge field, its centre
                    // half a pixel from the border.
                    if (geo) edgePx = lerp(max(0, borderTx - 0.5), edgePx, smoothstep(2, 3, borderTx));
                    float n = Grain(px);
                    float pool = exp(-edgePx / (_Engraved > 0.5 ? 5 : 7));
                    float4 now = State(id, 0), before = State(id, 1), cap = State(id, 2), extra = State(id, 3);
                    float3 tint = 0; float tintA = 0; float3 ink = 0; float inkA = 0;
                    float3 neutral = ToLinear(float3(150, 140, 122) / 255);

                    // Which colour this pixel shows: the owner's, or, mid-capture, the old one, the bare
                    // chart, or the new one behind a wet front spreading from the attacker's border.
                    float3 wash = ToLinear(now.rgb); bool owned = now.a > 0; float washScale = 1;
                    if (cap.b > 0.5)
                    {
                        float front = cap.r, fall = cap.g;
                        float sp = SAMPLE_TEXTURE2D(_SpreadTex, sampler_SpreadTex, i.uv).r + (n - 0.5) * 0.16;
                        if (sp < front)
                        {
                            if (front - sp < 0.045 && front < 0.999) { ink = ToLinear(now.rgb) * 0.42; inkA = 0.55; }
                        }
                        else if (before.a > 0 && fall < 1 && n > fall * 1.05) { wash = ToLinear(before.rgb); washScale = 1 - fall * 0.8; }
                        else owned = false;
                    }

                    float reach = extra.r; float dimOthers = extra.g; float strength = 1 - extra.b;
                    if (owned)
                    {
                        tint = wash;
                        tintA = saturate((0.52 + 0.34 * pool) * (0.82 + 0.36 * n) * washScale * (1 - 0.8 * dimOthers) * strength);
                        if (inkA == 0) { ink = wash * 0.45; inkA = (0.10 + 0.30 * pool) * (0.7 + 0.6 * n) * washScale * (1 - 0.8 * dimOthers) * strength; }
                    }
                    else
                    {
                        tint = neutral; tintA = 0.75;
                        float np = HatchPeriod(texelsPerPixel);
                        float hatch = Band(frac((px.x + px.y) / np), 0.145, 2 * texelsPerPixel / np);
                        ink = _Engraved > 0.5 ? float3(0.05, 0.03, 0.015) : float3(0.01, 0.007, 0.004); inkA = 0.28 * hatch;
                    }
                    // The lit stripes keep 8 screen pixels apart at any zoom, and frac, not fmod: fmod of a negative
                    // number stays negative, which filled every lit province west of the diagonal solid red. In texture pixels, a far view of a big
                    // world blurred them into a flat red that read as a realm's colour.
                    float stripeTexels = max(1.0, fwidth(px.x) + fwidth(px.y));
                    if (reach > 0.5 && frac((px.x - px.y) / (8 * stripeTexels)) < 0.25) { ink = ToLinear(float3(0.82, 0.27, 0.2)); inkA = 0.5; }

                    colour = lerp(painting, ColorBlend(painting, tint), tintA);
                    colour = lerp(colour, ink, inkA);

                    // Occupation: the owner's wash stays, the occupier's colour goes over it as inked
                    // diagonal hatching, a pattern rather than only a colour. Drawn in from the side the
                    // army came from; lifted from the edges inward; thickened into a wash by a treaty.
                    float4 occ = State(id, 4);
                    if (occ.a > 0.003)
                    {
                        float4 sw = State(id, 5);
                        float amount = occ.a, thick = extra.a;
                        float shown;
                        if (sw.a > 0.5)
                        {
                            float ang = sw.b * 6.2831853;
                            float2 d = (i.uv - sw.rg) * float2(_IdTex_TexelSize.z / _IdTex_TexelSize.w, 1);
                            float t = 0.5 - dot(d, float2(cos(ang), sin(ang))) / (2.2 * _HatchReach);
                            shown = step(t + (n - 0.5) * 0.05, amount * 1.1);
                        }
                        else shown = step(1 - amount, saturate(edgePx / 10) + 0.02);
                        float period = HatchPeriod(texelsPerPixel), f = frac((px.x - px.y) / period);
                        float width = lerp(0.42, 1.02, thick);
                        float aa = 2 * texelsPerPixel / period;
                        float band = Band(f, width, aa);
                        // Each stroke: a dark ink edge on both sides, the occupier's colour, laid on thick.
                        float edgeW = 0.11;
                        float edge = width >= 1 ? 0 : band * (1 - Band(frac(f - edgeW), width - 2 * edgeW, aa));
                        float3 occColour = ToLinear(occ.rgb);
                        float a = shown * band * (0.85 + 0.15 * n);
                        colour = lerp(colour, lerp(ColorBlend(colour, occColour), occColour, 0.55), a * 0.92);
                        colour = lerp(colour, float3(0.006, 0.004, 0.002), shown * edge * 0.85 * (1 - thick));
                    }

                    // Ink lines: heavy between owners (with a gilt or red-ink edge), thin inside a country.
                    float3 darkInk = _Engraved > 0.5 ? float3(0.012, 0.006, 0.003) : float3(0.004, 0.002, 0.001);
                    float3 gilt = _Engraved > 0.5 ? ToLinear(float3(0.43, 0.16, 0.1)) : ToLinear(float3(0.84, 0.69, 0.39));
                    if (geo)
                    {
                        // The same widths as the province map's rings, measured from the true border and
                        // antialiased over a screen pixel: dark to 1 pixel each side, gilt to 3, a thin line inside.
                        float px1 = texelsPerPixel;
                        float country = across >= 0 && abs(OwnerSlot(across) - OwnerSlot(id)) > 0.001 ? 1 : 0;
                        float dark = saturate((1 - borderTx) / px1 + 0.5), giltA = saturate((3 - borderTx) / px1 + 0.5);
                        float thin = saturate((1.8 - borderTx) / px1 + 0.5);
                        colour = lerp(colour, gilt, 0.6 * country * (giltA - dark));
                        colour = lerp(colour, darkInk, 0.92 * country * dark);
                        colour = lerp(colour, float3(0.009, 0.005, 0.003), 0.62 * (1 - country) * thin);
                    }
                    else
                    {
                        float ring = CountryBorder(i.uv, id);
                        if (ring == 1) colour = lerp(colour, darkInk, 0.92);
                        else if (ring >= 2) colour = lerp(colour, gilt, 0.6);
                        else if (edgePx < 1.3) colour = lerp(colour, float3(0.009, 0.005, 0.003), 0.62);
                    }
                    if (cap.a > 0.5) colour = lerp(colour, ToLinear(float3(0.94, 0.84, 0.59)), 0.95 * saturate((3.2 - edgePx) / texelsPerPixel + 0.5));
                }

                // The focus: everything but the province being captured steps back.
                if (_Focus.w > 0)
                {
                    float2 d = (i.uv - _Focus.xy) * float2(_IdTex_TexelSize.z / _IdTex_TexelSize.w, 1);
                    float k = smoothstep(_Focus.z, _Focus.z * 2.3, length(d)) * _Focus.w;
                    float l = Lum(colour);
                    colour = lerp(colour, lerp(colour, l.xxx, 0.3) * 0.55, k);
                }
                // The painting's sea fades into the open sea beyond it; land never reaches the edge.
                float2 e = min(i.uv, 1 - i.uv);
                float alpha = id >= 0 ? 1 : smoothstep(0, _EdgeFade, min(e.x, e.y * 1.777));
                return half4(colour, alpha);
            }
            ENDHLSL
        }
    }
}

// The drawn chart's ground, on the GPU: the same layers as ChartPainter.Ground and LandTexture
// (Screen/Chart/ChartPainter.cs), from the same fields, so a 4800-wide chart draws in one pass.
// Colours are worked in sRGB, as the CPU renderer works them, and written to an sRGB target.
//
// Inputs (all uploaded bottom row first, so uv addresses them directly):
//   _IdTex    province index per pixel at half the chart's width, 16-bit in RG (point)
//   _SdfTex   signed distance to the coast in province-map pixels, + on land (bilinear)
//   _LitTex   per grid cell, the terrain's tone under the relief's light, /255 (bilinear)
//   _FarmTex  per grid cell, the farmland, 0 to 1 (bilinear)
//   _TerrainLut  per province, its terrain index (point, one row)
//   _GrainTex, _SeaTex, _PaperTex  tiles
Shader "GreyBoxStrategy/ChartGround"
{
    Properties
    {
        _IdTex ("Province ids", 2D) = "black" {}
        _SdfTex ("Coast distance", 2D) = "black" {}
        _LitTex ("Lit terrain", 2D) = "black" {}
        _FarmTex ("Farmland", 2D) = "black" {}
        _TerrainLut ("Terrain per province", 2D) = "black" {}
        _GrainTex ("Grain", 2D) = "white" {}
        _SeaTex ("Sea tile", 2D) = "black" {}
        _PaperTex ("Paper", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            ZWrite Off ZTest Always Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_IdTex); TEXTURE2D(_SdfTex); TEXTURE2D(_LitTex); TEXTURE2D(_FarmTex); TEXTURE2D(_TerrainLut);
            TEXTURE2D(_GrainTex); TEXTURE2D(_SeaTex); TEXTURE2D(_PaperTex);
            SAMPLER(sampler_point_clamp); SAMPLER(sampler_linear_clamp); SAMPLER(sampler_point_repeat); SAMPLER(sampler_linear_repeat);
            float4 _Grid;      // grid width, grid height, chart pixels per cell, province-map pixels per chart pixel
            float4 _Rhumbs;    // two rhumb centres in cells
            float4 _Paper;     // use, mean, period in cells, -
            int _Seed;

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
            Varyings vert(Attributes v) { Varyings o; o.positionCS = TransformObjectToHClip(v.positionOS.xyz); o.uv = v.uv; return o; }

            // ---- ChartNoise, bit for bit ----
            uint Hash(int x, int y, int seed)
            {
                uint h = (uint)x * 374761393u + (uint)y * 668265263u + (uint)seed * 2246822519u;
                h = (h ^ (h >> 13)) * 1274126177u;
                return h ^ (h >> 16);
            }
            float Hash01(int x, int y, int seed) { return (Hash(x, y, seed) & 0xFFFFFFu) / 16777216.0; }
            float Value(float x, float y, int seed)
            {
                int x0 = (int)floor(x), y0 = (int)floor(y);
                float fx = x - x0, fy = y - y0;
                fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy);
                float a = Hash01(x0, y0, seed), b = Hash01(x0 + 1, y0, seed), c = Hash01(x0, y0 + 1, seed), d = Hash01(x0 + 1, y0 + 1, seed);
                return (a + (b - a) * fx) * (1 - fy) + (c + (d - c) * fx) * fy;
            }

            float3 ToLinear(float3 c)
            {
                #if defined(UNITY_COLORSPACE_GAMMA)
                return c;
                #else
                return c <= 0.04045 ? c / 12.92 : pow((c + 0.055) / 1.055, 2.4);
                #endif
            }

            // The paper tile is an sRGB texture: in a linear project the sampler decodes it, so encode it back
            // before comparing it with its sRGB mean.
            float3 ToSrgb(float3 c)
            {
                #if defined(UNITY_COLORSPACE_GAMMA)
                return c;
                #else
                return c <= 0.0031308 ? c * 12.92 : 1.055 * pow(c, 1 / 2.4) - 0.055;
                #endif
            }

            static const float3 SeaShallow = float3(52, 64, 66);
            static const float3 SeaLine = float3(118, 128, 118);
            static const float3 LakeTone = float3(66, 86, 92);
            static const float3 InkDark = float3(38, 28, 18);
            static const float3 Cliff = float3(104, 84, 58);
            static const float3 CliffDark = float3(62, 48, 32);
            static const float3 Sand = float3(176, 158, 112);
            static const float3 Gilt = float3(204, 166, 92);

            float FarmAt(float u, float v) { return SAMPLE_TEXTURE2D_LOD(_FarmTex, sampler_linear_clamp, float2(u / _Grid.x, 1 - v / _Grid.y), 0).r; }

            // ChartPainter.LandTexture
            float3 LandTexture(float3 c, int terrain, float u, float v)
            {
                if (terrain == 1)
                {
                    float a = Value(u * 2.6, v * 2.6, _Seed + 41), b = Value(u * 2.6 - 0.18, v * 2.6 - 0.18, _Seed + 41);
                    float crown = saturate((a - 0.3) * 2.2);
                    c = lerp(c * 0.72, c * 1.08, crown);
                    c = c * (1 + 0.35 * (b - a));
                }
                else if (terrain == 4)
                {
                    float n = Value(u * 1.3, v * 1.3, _Seed + 43);
                    float hatch = frac(u * 2.2 + v * 3.1);
                    if (hatch < 0.14 && n > 0.45) c *= 0.84;
                    c = lerp(c, float3(150, 140, 126), 0.25 * n);
                }
                else if (terrain == 3)
                {
                    float n = Value(u * 1.6, v * 1.6, _Seed + 44);
                    if (n > 0.62) c = lerp(c, float3(80, 104, 104), min(0.6, (n - 0.62) * 4));
                }
                float farm = FarmAt(u, v);
                if (farm > 0.02 && terrain != 4 && terrain != 3)
                {
                    const float farmSize = 4.2, pw = 1.05, ph = 0.6;
                    float wu = u + 1.6 * (Value(u * 0.25, v * 0.25, _Seed + 47) - 0.5);
                    float wv = v + 1.6 * (Value(u * 0.25 + 7, v * 0.25, _Seed + 48) - 0.5);
                    int fx = (int)floor(wu / farmSize), fy = (int)floor(wv / farmSize);
                    float th = (Hash01(fx, fy, _Seed + 45) - 0.5) * 1.8;
                    float cs = cos(th), sn = sin(th);
                    float lu = u - (fx + 0.5) * farmSize, lv = v - (fy + 0.5) * farmSize;
                    float ru = lu * cs - lv * sn, rv = lu * sn + lv * cs;
                    float row = floor(rv / ph);
                    float shift = (((int)row & 1) == 0) ? 0 : 0.5;
                    float col = floor(ru / pw + shift);
                    uint h = Hash((int)col + fx * 101, (int)row + fy * 103, _Seed + 46);
                    float worked = FarmAt((fx + 0.5) * farmSize, (fy + 0.5) * farmSize);
                    bool farmed = Hash01(fx, fy, _Seed + 49) < max(worked, farm * 0.8);
                    float pick = (h & 0xFFFFu) / 65536.0;
                    if (farmed && pick < 0.9)
                    {
                        float fu = ru / pw + shift - col, fv = rv / ph - row;
                        uint k = (h >> 16) & 3u;
                        float3 crop = k == 0 ? float3(192, 168, 92) : k == 1 ? float3(136, 146, 70) : k == 2 ? float3(150, 112, 74) : float3(170, 156, 96);
                        // Light and dark between crops, furrows and hedges, not only hue: an owner's wash keeps the
                        // chart's light and replaces its hue, so fields must read in light alone.
                        float tone = k == 0 ? 1.14 : k == 1 ? 0.9 : k == 2 ? 0.78 : 1.02;
                        c = lerp(c, crop * clamp(c.r / 140.0, 0.75, 1.15) * tone, 0.72);
                        if (frac(fv * 5) < 0.22) c *= 0.84;
                        float edge = min(min(fu, 1 - fu) * pw, min(fv, 1 - fv) * ph);
                        if (edge < 0.05) c = lerp(c, float3(58, 60, 30), 0.72);
                    }
                }
                return c;
            }

            float Sdf(float2 uv) { return SAMPLE_TEXTURE2D_LOD(_SdfTex, sampler_linear_clamp, uv, 0).r; }

            half4 frag(Varyings i) : SV_Target
            {
                float s = _Grid.z;
                float u = i.uv.x * _Grid.x, v = (1 - i.uv.y) * _Grid.y;
                float sdf = Sdf(i.uv);
                float d = abs(sdf) / _Grid.w + 0.5; // chart pixels from the coast, as the CPU's chamfer counts them
                float4 idt = SAMPLE_TEXTURE2D_LOD(_IdTex, sampler_point_clamp, i.uv, 0);
                int id = (int)round(idt.r * 255) + 256 * (int)round(idt.g * 255);
                float coastLine = max(1.1, 0.09 * s);
                float3 c;
                if (sdf > 0)
                {
                    c = SAMPLE_TEXTURE2D_LOD(_LitTex, sampler_linear_clamp, float2(u / _Grid.x, 1 - v / _Grid.y), 0).rgb * 255;
                    int terrain = id < 65533 ? (int)round(LOAD_TEXTURE2D(_TerrainLut, int2(id, 0)).r * 255) : -2;
                    c = LandTexture(c, terrain, u, v);
                    c *= SAMPLE_TEXTURE2D_LOD(_GrainTex, sampler_point_repeat, float2(u, v) / 8.0, 0).r;
                    if (_Paper.x > 0.5)
                    {
                        float3 p = ToSrgb(SAMPLE_TEXTURE2D_LOD(_PaperTex, sampler_linear_repeat, float2(u, v) / _Paper.z, 0).rgb);
                        c *= 1 + 0.6 * (dot(p, 1.0 / 3) / _Paper.y - 1);
                    }
                    float dc = d / s;
                    if (dc < 0.6) c = lerp(c, Sand, 0.35 * (1 - dc / 0.6));
                    float ink = 1 - saturate(d - coastLine);
                    if (ink > 0) c = lerp(c, InkDark, 0.85 * ink);
                }
                else
                {
                    bool lake = id == 65534;
                    c = SAMPLE_TEXTURE2D_LOD(_SeaTex, sampler_point_repeat, float2(u, v) / 24.0, 0).rgb * 255;
                    float dc = d / s;
                    if (lake) c = lerp(c, LakeTone, 0.75);
                    if (dc < 1.6) c = lerp(c, lake ? LakeTone * 1.15 : SeaShallow, 0.55 * (1 - dc / 1.6));
                    if (!lake || dc < 1.0)
                    {
                        float wob = dc + 0.12 * (Value(u * 0.8, v * 0.8, _Seed + 9) - 0.5);
                        static const float rings[5] = { 0.45, 1.0, 1.65, 2.45, 3.4 };
                        [unroll] for (int r = 0; r < 5; r++)
                        {
                            float px = abs(wob - rings[r]) * s;
                            float lw = max(0.9, 0.05 * s);
                            float a = saturate(lw / 2 - px + 0.5) * (0.42 - r * 0.07);
                            c = lerp(c, SeaLine, a);
                        }
                    }
                    if (!lake)
                    {
                        [unroll] for (int rk = 0; rk < 2; rk++)
                        {
                            float2 rc = rk == 0 ? _Rhumbs.xy : _Rhumbs.zw;
                            float dx = u - rc.x, dy = v - rc.y;
                            float dist = sqrt(dx * dx + dy * dy) * s;
                            if (dist < 1) continue;
                            float step = PI / 8;
                            float off = atan2(dy, dx) / step; off -= round(off);
                            float perp = abs(off) * step * dist;
                            float lw = max(0.8, 0.035 * s);
                            float a = saturate(lw / 2 - perp + 0.5) * 0.22 * min(1, 30 * s / (dist + 30 * s) * 1.6);
                            c = lerp(c, Gilt, a);
                        }
                        // A cliff face under a south-facing coast: land straight above within its depth.
                        float depth = max(2, 0.55 * s);
                        float cliffAt = 0;
                        [unroll] for (int t = 1; t <= 6; t++)
                        {
                            float up = depth * t / 6;
                            if (cliffAt == 0 && Sdf(i.uv + float2(0, up / (_Grid.y * s))) > 0) cliffAt = up;
                        }
                        if (cliffAt > 0)
                        {
                            float tt = cliffAt / depth;
                            float stripe = Value(u * 6, v * 1.2, _Seed + 5);
                            c = lerp(c, lerp(Cliff, CliffDark, tt * 0.8 + 0.3 * stripe), 0.92 * (1 - tt * tt));
                        }
                    }
                    float ink = 1 - saturate(d - coastLine);
                    if (ink > 0) c = lerp(c, InkDark, 0.85 * ink);
                }
                return half4(ToLinear(saturate(c / 255)), 1);
            }
            ENDHLSL
        }
    }
}

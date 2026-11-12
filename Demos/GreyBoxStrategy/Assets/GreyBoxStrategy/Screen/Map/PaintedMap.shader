// The painted map: the sea-chart painting, every province washed in its owner's colour, and the
// capture of a province drawn as colour bleeding in from the attacker's side.
//
// Two layers, as the round-2 mock-up found (docs/demos/grey-box-strategy-screen.md, "What building
// it taught"): a hue-and-saturation tint that keeps the painting's light and shade (the W3C
// "color" blend), then ink over it with ordinary alpha: borders, pigment pooled at the edge,
// hatching on neutral land, the wet edge of a capture.
//
// Inputs are data, so animation is numbers changing:
//   _IdTex     province index * 10 per pixel, 255 for sea (point)
//   _EdgeTex   distance to the nearest border, 16 per pixel (bilinear)
//   _StateTex  32 x 4, one column per province: row 0 owner colour + owner slot, row 1 the
//              colour it had before a capture, row 2 (front, fall, capturing, selected),
//              row 3 (reach highlight, dim, -, -)
//   _SpreadTex distance from the attacker's border inside the province being captured, 0..1
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
                float v = SAMPLE_TEXTURE2D(_IdTex, sampler_point_clamp, uv).r * 255;
                return v > 250 ? -1 : (int)round(v / 10);
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

            half4 frag(Varyings i) : SV_Target
            {
                float3 painting = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv).rgb;
                float2 px = i.uv * _IdTex_TexelSize.zw;
                int id = IdAt(i.uv);
                float3 colour = painting;

                if (id >= 0)
                {
                    float edgePx = SAMPLE_TEXTURE2D(_EdgeTex, sampler_EdgeTex, i.uv).r * 255 / 16;
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

                    float reach = extra.r; float dimOthers = extra.g;
                    if (owned)
                    {
                        tint = wash;
                        tintA = saturate((0.52 + 0.34 * pool) * (0.82 + 0.36 * n) * washScale * (1 - 0.8 * dimOthers));
                        if (inkA == 0) { ink = wash * 0.45; inkA = (0.10 + 0.30 * pool) * (0.7 + 0.6 * n) * washScale * (1 - 0.8 * dimOthers); }
                    }
                    else
                    {
                        tint = neutral; tintA = 0.75;
                        if (frac((px.x + px.y) / 9) < 0.145) { ink = _Engraved > 0.5 ? float3(0.05, 0.03, 0.015) : float3(0.01, 0.007, 0.004); inkA = 0.28; }
                    }
                    if (reach > 0.5 && fmod(floor(px.x - px.y), 8) < 2) { ink = ToLinear(float3(0.82, 0.27, 0.2)); inkA = 0.5; }

                    colour = lerp(painting, ColorBlend(painting, tint), tintA);
                    colour = lerp(colour, ink, inkA);

                    // Ink lines: heavy between owners (with a gilt or red-ink edge), thin inside a country.
                    float ring = CountryBorder(i.uv, id);
                    if (ring == 1) colour = lerp(colour, _Engraved > 0.5 ? float3(0.012, 0.006, 0.003) : float3(0.004, 0.002, 0.001), 0.92);
                    else if (ring >= 2) colour = lerp(colour, _Engraved > 0.5 ? ToLinear(float3(0.43, 0.16, 0.1)) : ToLinear(float3(0.84, 0.69, 0.39)), 0.6);
                    else if (edgePx < 1.3) colour = lerp(colour, float3(0.009, 0.005, 0.003), 0.62);
                    if (cap.a > 0.5 && edgePx < 3.2) colour = lerp(colour, ToLinear(float3(0.94, 0.84, 0.59)), 0.95);
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

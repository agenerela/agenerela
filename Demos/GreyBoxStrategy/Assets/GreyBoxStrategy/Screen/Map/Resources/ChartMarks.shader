// What the drawn chart lays over its ground, on the GPU: rivers and roads as antialiased strips,
// and glyphs as quads from the code-drawn atlas (Screen/Chart/ChartAtlas.cs). Both blend
// premultiplied, over the ground already in the target.
Shader "GreyBoxStrategy/ChartMarks"
{
    Properties
    {
        _MainTex ("Atlas (premultiplied, stored as sRGB values without decoding)", 2D) = "black" {}
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off ZTest Always Cull Off
        Blend One OneMinusSrcAlpha

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        float3 ToLinear(float3 c)
        {
            #if defined(UNITY_COLORSPACE_GAMMA)
            return c;
            #else
            return c <= 0.04045 ? c / 12.92 : pow((c + 0.055) / 1.055, 2.4);
            #endif
        }
        ENDHLSL

        // Pass 0: a stroke. uv0 = (cells along, side -1..1), uv1 = (half width in pixels, dashed), colour = sRGB and opacity.
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            struct A { float4 positionOS : POSITION; float2 uv0 : TEXCOORD0; float2 uv1 : TEXCOORD1; float4 color : COLOR; };
            struct V { float4 positionCS : SV_POSITION; float2 uv0 : TEXCOORD0; float2 uv1 : TEXCOORD1; float4 color : COLOR; };
            V vert(A a) { V o; o.positionCS = TransformObjectToHClip(a.positionOS.xyz); o.uv0 = a.uv0; o.uv1 = a.uv1; o.color = a.color; return o; }
            half4 frag(V i) : SV_Target
            {
                float half_ = i.uv1.x;
                float dist = abs(i.uv0.y) * (half_ + 1);
                // A stroke thinner than a pixel is drawn a pixel wide, fainter, so it never breaks up.
                float cover = saturate(max(half_, 0.5) + 0.5 - dist) * min(1, half_ * 2);
                if (i.uv1.y > 0.5 && frac(i.uv0.x / 0.72) > 0.42 / 0.72) cover = 0;
                float a = cover * i.color.a;
                return half4(ToLinear(i.color.rgb) * a, a);
            }
            ENDHLSL
        }

        // Pass 1: a glyph quad from the atlas.
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            struct A { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct V { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
            V vert(A a) { V o; o.positionCS = TransformObjectToHClip(a.positionOS.xyz); o.uv = a.uv; return o; }
            half4 frag(V i) : SV_Target
            {
                float4 t = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
                if (t.a <= 0.001) return 0;
                // The atlas is premultiplied in sRGB: undo, decode, premultiply again.
                return half4(ToLinear(t.rgb / t.a) * t.a, t.a);
            }
            ENDHLSL
        }
    }
}

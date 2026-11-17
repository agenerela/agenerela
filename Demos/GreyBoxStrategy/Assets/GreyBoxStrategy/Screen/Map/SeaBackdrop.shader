// The open sea beyond the painting: a seamless tile cut from the painting's own sea, repeated
// in world space behind the map, so the map fills any window edge to edge. The painting's edge
// fades into it (PaintedMap's _EdgeFade).
Shader "GreyBoxStrategy/SeaBackdrop"
{
    Properties
    {
        _MainTex ("Sea tile", 2D) = "grey" {}
        _TileUnits ("Tile size in world units", Float) = 2.0
        _Shade ("Brightness", Float) = 0.92
        _Flatten ("Pull toward the sea's mean colour, hiding the tile's mirror seams", Float) = 0.45
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry-10" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "SeaBackdrop"
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            float _TileUnits, _Shade, _Flatten;
            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 world : TEXCOORD0; };
            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.world = TransformObjectToWorld(v.positionOS.xyz).xy;
                return o;
            }
            half4 frag(Varyings i) : SV_Target
            {
                // Two samples of the tile, the second larger and turned, mixed by slow noise, so the
                // mirror tile never shows a repeating pattern.
                float2 uv = i.world / _TileUnits;
                float2 uv2 = mul(float2x2(0.81, -0.59, 0.59, 0.81), i.world / (_TileUnits * 1.73)) + 0.37;
                float3 a = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv).rgb;
                float3 b = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv2).rgb;
                float2 p = i.world / (_TileUnits * 2.9);
                float n = sin(p.x * 1.7 + sin(p.y * 1.3)) * 0.5 + 0.5;
                n = smoothstep(0.25, 0.75, n * 0.7 + 0.3 * (sin(p.y * 2.3 + p.x * 0.7) * 0.5 + 0.5));
                float3 c = lerp(a, b, n);
                c = lerp(c, float3(0.036, 0.035, 0.03), _Flatten);
                return half4(c * _Shade, 1);
            }
            ENDHLSL
        }
    }
}

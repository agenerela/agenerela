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
                float3 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.world / _TileUnits).rgb;
                c = lerp(c, float3(0.036, 0.035, 0.03), _Flatten);
                return half4(c * _Shade, 1);
            }
            ENDHLSL
        }
    }
}

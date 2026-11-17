// The stream and the well: gentle waves moved in the vertex shader, a fresnel blend from shallow to
// deep, the sun's glint and drifting streaks of light. Transparent, lit by the main light, fogged.
Shader "GreyBoxVillage/Water"
{
    Properties
    {
        _ShallowColor ("Shallow", Color) = (0.34, 0.66, 0.74, 0.82)
        _DeepColor ("Deep", Color) = (0.11, 0.33, 0.48, 0.94)
        _WaveHeight ("Wave height", Float) = 0.06
        _WaveSpeed ("Wave speed", Float) = 1.0
        _Glint ("Glint", Float) = 1.2
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _ShallowColor;
                half4 _DeepColor;
                float _WaveHeight;
                float _WaveSpeed;
                float _Glint;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float fog : TEXCOORD2;
            };

            float Wave(float2 p, float t)
            {
                return sin(p.x * 0.6 + t * 1.3) * 0.5 + sin(p.y * 0.9 - t * 1.7) * 0.35 + sin((p.x + p.y) * 1.7 + t * 2.3) * 0.15;
            }

            Varyings vert(Attributes input)
            {
                Varyings o;
                float3 ws = TransformObjectToWorld(input.positionOS.xyz);
                float t = _Time.y * _WaveSpeed;
                float h = Wave(ws.xz, t);
                const float e = 0.2;
                float hx = Wave(ws.xz + float2(e, 0), t) - h;
                float hz = Wave(ws.xz + float2(0, e), t) - h;
                ws.y += h * _WaveHeight;
                o.normalWS = normalize(float3(-hx * _WaveHeight / e * 4, 1, -hz * _WaveHeight / e * 4));
                o.positionWS = ws;
                o.positionCS = TransformWorldToHClip(ws);
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 n = normalize(i.normalWS);
                float3 v = normalize(GetWorldSpaceViewDir(i.positionWS));
                Light sun = GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                float lit = sun.shadowAttenuation;
                float fresnel = pow(1 - saturate(dot(n, v)), 3);
                half4 c = lerp(_ShallowColor, _DeepColor, saturate(fresnel * 0.7 + 0.25));
                float3 h = normalize(sun.direction + v);
                float glint = pow(saturate(dot(n, h)), 140) * _Glint;
                float t = _Time.y * _WaveSpeed;
                float streak = smoothstep(0.8, 1.0, sin(i.positionWS.x * 2.1 + t * 2.0 + sin(i.positionWS.z * 3.0 + t) * 1.5) * 0.5 + 0.5) * 0.1;
                c.rgb *= 0.45 + 0.55 * sun.color * lit;
                c.rgb += SampleSH(n) * 0.3;
                c.rgb += (glint + streak) * sun.color * lit;
                c.rgb = MixFog(c.rgb, i.fog);
                return c;
            }
            ENDHLSL
        }
    }
    FallBack Off
}

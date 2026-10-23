// 창문으로 비스듬히 들어오는 빛줄기 (MapLighting 이 창문마다 만든 면). 더하기 블렌딩, 깊이는 쓰지 않는다.
// uv.x = 창문 가로 0..1, uv.y = 창문 0 → 바닥 1. 창문 쪽이 가장 밝고 바닥으로 갈수록 흐려지며, 가운데 창살 자리는 살짝 어둡다.
// 픽셀 아트처럼: 밝기를 4 단계로 끊고 단계 사이는 4×4 Bayer 디더링. 격자 = 월드 위치를 화면 축(카메라 오른쪽 · 위)에
// 1/32 unit 로 찍은 것 — 화면 픽셀 방향으로 정렬되고, 카메라가 움직여도 무늬가 월드에 붙어 있다.
Shader "DgVillage/LightShaft"
{
    Properties
    {
        _Color ("Color × Strength", Color) = (1, 0.9, 0.7, 0.4)
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Blend One One
        Cull Off
        ZWrite Off
        ZTest LEqual

        Pass
        {
            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "DgLighting.hlsl"

            #pragma vertex Vert
            #pragma fragment Frag

            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(input.positionOS);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.uv = input.uv;
                return o;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 right = UNITY_MATRIX_V[0].xyz;
                float3 up = UNITY_MATRIX_V[1].xyz;
                float2 texel = floor(float2(dot(input.positionWS, right), dot(input.positionWS, up)) * DgTexelsPerUnit);

                float2 uv = input.uv;
                float edges = smoothstep(0.0, 0.18, uv.x) * smoothstep(1.0, 0.82, uv.x);
                float mullion = 1.0 - 0.45 * (1.0 - smoothstep(0.02, 0.06, abs(uv.x - 0.5)));
                float fade = 1.0 - uv.y * uv.y * 0.75;
                float f = saturate(edges * mullion * fade);
                f = floor(f * DgLightSteps + DgBayer4(texel)) / DgLightSteps;
                return half4(_Color.rgb * f, 0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}

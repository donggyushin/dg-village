// 건물 그림 한 장 (PixelLab 큰 소품). 그림은 세운 스프라이트지만 깊이는 "바닥 위 건물 자리(footprint)의 앞쪽 끝"으로 쓴다:
// 화면의 같은 세로줄에서 카메라에 가장 가까운 footprint 바닥 점의 깊이. 캐릭터 · 나무 스프라이트는 카메라를 정면으로 보니
// 깊이 = 발 위치 깊이 → 발이 건물 앞쪽 끝보다 앞이면 건물 위에, 뒤면 건물에 가려진다 (아이소메트릭 게임의 정렬 규칙).
// 그림 한 장의 평면 깊이로는 앞벽에 붙어 선 캐릭터가 가려진다 (판이 pivot 한 점에 서 있어서).
// - 알파 컷(0.5) + ZWrite, AlphaTest 큐 → 투명 큐의 캐릭터 · 소품이 나중에 이 깊이와 비교된다
// - ZTest Always: 그림에서 건물 앞 계단 · 땅에 그려진 부분은 footprint 보다 앞이라 바닥에 지면 안 된다
// - footprint (월드 XZ 직사각형) 는 BuildingSprite 컴포넌트가 MaterialPropertyBlock 으로 넣는다
// - 빛은 Sprite 셰이더처럼 오브젝트 원점 한 점
Shader "DgVillage/BuildingSprite"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        [HideInInspector] _Color ("Tint", Color) = (1, 1, 1, 1)
        [HideInInspector] _FootMin ("Footprint Min XZ", Vector) = (0, 0, 0, 0)
        [HideInInspector] _FootMax ("Footprint Max XZ", Vector) = (0, 0, 0, 0)
    }

    SubShader
    {
        Tags { "Queue" = "AlphaTest" "RenderType" = "TransparentCutout" "RenderPipeline" = "UniversalPipeline" }

        Cull Off
        ZWrite On
        ZTest Always

        Pass
        {
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"
            #include "DgLighting.hlsl"

            #pragma vertex Vert
            #pragma fragment Frag

            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                half3 light : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float4 _FootMin;
                float4 _FootMax;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings o = (Varyings)0;
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);
                o.positionWS = TransformObjectToWorld(input.positionOS);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.uv = input.uv;
                o.color = input.color * _Color * unity_SpriteColor;
                o.light = DgLight(TransformObjectToWorld(float3(0, 0, 0)));
                return o;
            }

            // 바닥에서 화면 세로줄 하나 = 카메라 앞 방향(수평) 직선. 그 직선이 footprint 에 들어오는 점
            float3 FrontPoint(float3 positionWS)
            {
                float2 right = normalize(UNITY_MATRIX_V[0].xz);
                float2 forward = float2(-right.y, right.x); // 화면 위 = 카메라에서 멀어지는 쪽 (수평)
                float2 lo = _FootMin.xy;
                float2 hi = _FootMax.xy;
                // 이 픽셀의 화면 가로 좌표를 footprint 의 가로 범위로 자른다 (지붕 처마 · 굴뚝이 옆으로 나간 픽셀)
                float4 corners = float4(dot(lo, right), dot(float2(hi.x, lo.y), right), dot(float2(lo.x, hi.y), right), dot(hi, right));
                float s = clamp(dot(positionWS.xz, right), min(min(corners.x, corners.y), min(corners.z, corners.w)),
                    max(max(corners.x, corners.y), max(corners.z, corners.w)));
                // 직선 s·right + t·forward 와 직사각형의 slab 교차 → 들어가는 t
                float2 origin = s * right;
                float2 invDir = 1.0 / (abs(forward) > 1e-5 ? forward : float2(1e-5, 1e-5));
                float2 t0 = (lo - origin) * invDir;
                float2 t1 = (hi - origin) * invDir;
                float tEnter = max(min(t0.x, t1.x), min(t0.y, t1.y));
                float2 p = origin + tEnter * forward;
                return float3(p.x, 0, p.y);
            }

            half4 Frag(Varyings input, out float depth : SV_Depth) : SV_Target
            {
                half4 color = input.color * SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                clip(color.a - 0.5);
                color.rgb *= input.light;
                color.a = 1.0;
                float4 front = TransformWorldToHClip(FrontPoint(input.positionWS));
                depth = front.z / front.w;
                return color;
            }
            ENDHLSL
        }
    }
    Fallback Off
}

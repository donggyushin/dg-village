// 바닥 · 벽 (FloorMesh). URP Unlit 처럼 텍스처 색 그대로 + 맵 조명(DgLighting.hlsl) + 구름 그림자.
// 조명 · 구름은 MapLighting 이 정하는 전역 값으로 켜진다 — 둘 다 꺼진 맵(이나 씬)에선 그냥 Unlit 과 같다.
// 알파가 0.75 미만인 텍셀(벽 아틀라스의 창문 유리)은 맵 조명을 받지 않는다 — 어두운 방에서도 바깥 하늘은 밝다.
// 대신 하늘 색 _DgSky (MapLighting, 낮 · 밤) 을 곱한다 — 밤엔 유리가 어두운 남색.
// 벽 머티리얼은 _Clouds = 0 (세운 벽면에 바닥 구름이 줄무늬로 지나가지 않게).
// 구름 노이즈는 월드 XZ 를 1/32 unit(= 1 texel) 격자로 끊어서 샘플한다: 그림자 가장자리도 픽셀 격자에 맞는다.
// 쿼터뷰 바닥(_IsoRows > 0, art/tilesets/make_iso_floor.py): uv = 화면 픽셀 기준 반복 좌표, uv1.x = 아틀라스 행(재질).
// 행 하나 안에서 frac 으로 반복한다 (재질별 패턴이 화면에 붙어 있어 텍셀이 돌아가지 않는다).
Shader "DgVillage/Ground"
{
    Properties
    {
        [MainTexture] _BaseMap ("Texture", 2D) = "white" {}
        [MainColor] _BaseColor ("Color", Color) = (1, 1, 1, 1)
        _Clouds ("Cloud Shadows", Range(0, 1)) = 1
        _IsoRows ("Quarter-view Atlas Rows (0 = off)", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        Pass
        {
            Name "Unlit"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "DgLighting.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half _Clouds;
                float _IsoRows;
            CBUFFER_END

            // MapLighting 이 Shader.SetGlobal* 로 넣는다
            float _DgCloudStrength; // 0 = 구름 없음
            half4 _DgShadowColor;   // 그림자 색 (곱하기)
            half4 _DgSky;           // 창문 유리에 곱할 하늘 색. a = 0 (MapLighting 없음) 이면 그대로

            static const float TexelsPerUnit = 32.0;
            static const float CloudSize = 9.0;               // 구름 하나 ≈ 9 unit
            static const float2 CloudWind = float2(0.35, -0.2); // unit/초

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float2 uv1 : TEXCOORD1;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                nointerpolation float row : TEXCOORD2;
            };

            float Hash(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float ValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), f.x),
                            lerp(Hash(i + float2(0, 1)), Hash(i + float2(1, 1)), f.x), f.y);
            }

            // 0 = 햇빛, 0.5 = 구름 가장자리, 1 = 구름 그늘 (단계로 끊어 픽셀 아트처럼)
            float CloudCover(float2 xz)
            {
                float2 snapped = floor(xz * TexelsPerUnit) / TexelsPerUnit;
                float2 p = (snapped - CloudWind * _Time.y) / CloudSize;
                float n = ValueNoise(p) * 0.65 + ValueNoise(p * 2.3 + 17.0) * 0.35;
                return step(0.56, n) * 0.5 + step(0.62, n) * 0.5;
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.uv = _IsoRows > 0.0 ? input.uv : TRANSFORM_TEX(input.uv, _BaseMap);
                output.row = input.uv1.x;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;
                if (_IsoRows > 0.0)
                {
                    // 행 0 = 이미지 맨 위
                    uv = float2(frac(uv.x), (_IsoRows - 1.0 - input.row + frac(uv.y)) / _IsoRows);
                }
                half4 color = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv) * _BaseColor;
                color.rgb *= color.a < 0.75 ? lerp(half3(1, 1, 1), _DgSky.rgb, _DgSky.a) : DgLight(input.positionWS);
                color.a = 1.0;
                if (_DgCloudStrength * _Clouds > 0.0)
                {
                    float cover = CloudCover(input.positionWS.xz) * _DgCloudStrength * _Clouds;
                    color.rgb *= lerp(half3(1, 1, 1), _DgShadowColor.rgb, cover);
                }
                return color;
            }
            ENDHLSL
        }
    }
    Fallback Off
}

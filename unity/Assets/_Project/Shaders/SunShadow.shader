// 해 그림자 (SunShadow 가 바닥에 눕힌 스프라이트 실루엣). 스프라이트 알파로 모양만 따고
// 바닥 색에 그림자 색을 곱한다 (Blend DstColor Zero) — 검은 반투명보다 바닥 색이 살아 있다.
// Stencil: 그림자끼리 겹친 곳은 한 번만 칠한다 (겹쳐서 더 진해지지 않게).
Shader "DgVillage/SunShadow"
{
    Properties
    {
        [MainTexture] _MainTex ("Sprite", 2D) = "white" {}
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" }

        Pass
        {
            Name "SunShadow"
            Blend DstColor Zero
            ZWrite Off
            Cull Off

            Stencil
            {
                Ref 1
                ReadMask 1
                WriteMask 1
                Comp NotEqual
                Pass Replace
            }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            // MapLighting 이 넣는다. 그림자 색 = lerp(흰색, _DgShadowColor, _DgShadowStrength)
            half4 _DgShadowColor;
            float _DgShadowStrength;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half alpha = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).a;
                clip(alpha - 0.5);
                return half4(lerp(half3(1, 1, 1), _DgShadowColor.rgb, _DgShadowStrength), 1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}

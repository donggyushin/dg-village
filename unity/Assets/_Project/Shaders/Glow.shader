// 불꽃 주변 빛무리 (MapLighting 이 점광원마다 세운 스프라이트). 더하기 블렌딩: 뒤에 있는 것을 밝게 덮는다.
// 텍스처(MapLighting.GlowTexture)는 계단식 + 디더링 원이라 점 필터로 찍어도 픽셀 아트처럼 보인다.
Shader "DgVillage/Glow"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Blend One One
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

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
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            Varyings Vert(Attributes input)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(input.positionOS);
                o.uv = input.uv;
                o.color = input.color * unity_SpriteColor;
                return o;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                // 알파 = 빛 세기, SpriteRenderer.color 알파 = 깜빡임
                return half4(input.color.rgb * tex.a * input.color.a, 0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}

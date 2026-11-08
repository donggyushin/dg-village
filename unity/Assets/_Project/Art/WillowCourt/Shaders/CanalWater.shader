Shader "DG Village/Willow Court/Canal Water"
{
    Properties
    {
        _DeepColor("Deep teal", Color) = (0.11,0.30,0.31,1)
        _ShallowColor("Jade surface", Color) = (0.27,0.52,0.48,1)
        _GlintColor("Soft ripple", Color) = (0.65,0.79,0.68,1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 world : TEXCOORD0; };
            CBUFFER_START(UnityPerMaterial)
            half4 _DeepColor;
            half4 _ShallowColor;
            half4 _GlintColor;
            CBUFFER_END
            Varyings vert(Attributes v) {
                Varyings o;
                o.world=TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS=TransformWorldToHClip(o.world);
                return o;
            }
            half4 frag(Varyings i) : SV_Target {
                float2 p=i.world.xz;
                float t=_Time.y;
                float cloud=sin(p.x*.67+sin(p.y*.44+t*.12))*sin(p.y*.58-t*.09);
                float wave=sin(p.x*3.2+p.y*5.8+t*.48+sin(p.x*1.3-p.y*.8));
                float broken=smoothstep(.3,.85,sin(p.x*2.3-p.y*.7+t*.15));
                float glint=smoothstep(.96,1.,wave)*broken*.32;
                half3 color=lerp(_DeepColor.rgb,_ShallowColor.rgb,cloud*.32+.43);
                color=lerp(color,_GlintColor.rgb,glint);
                return half4(color,1);
            }
            ENDHLSL
        }
    }
}

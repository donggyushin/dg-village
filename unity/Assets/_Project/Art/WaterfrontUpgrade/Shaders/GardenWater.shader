Shader "DG Village/Waterfront/Garden Water"
{
    Properties
    {
        _ShoreMap("Bank distance (linear)", 2D) = "white" {}
        _MapRect("World XZ origin and size", Vector) = (-110,-115,230,230)
        _DeepColor("Deep water", Color) = (.075,.17,.19,1)
        _ShallowColor("Shallow jade", Color) = (.20,.31,.27,1)
        _RippleColor("Sky reflection", Color) = (.49,.67,.64,1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_ShoreMap); SAMPLER(sampler_ShoreMap);
            CBUFFER_START(UnityPerMaterial)
            float4 _MapRect;
            half4 _DeepColor, _ShallowColor, _RippleColor;
            CBUFFER_END
            struct A { float4 positionOS:POSITION; };
            struct V { float4 positionCS:SV_POSITION; float3 world:TEXCOORD0; };
            V vert(A v) { V o; o.world=TransformObjectToWorld(v.positionOS.xyz); o.positionCS=TransformWorldToHClip(o.world); return o; }
            float hash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
            float noise(float2 p) {
                float2 i=floor(p), f=frac(p); f=f*f*(3-2*f);
                return lerp(lerp(hash(i),hash(i+float2(1,0)),f.x),lerp(hash(i+float2(0,1)),hash(i+1),f.x),f.y);
            }
            float3 stones(float2 p) {
                float2 cell=floor(p), f=frac(p); float nearest=10, second=10, id=0;
                [unroll] for(int y=-1;y<=1;y++) [unroll] for(int x=-1;x<=1;x++) {
                    float2 g=float2(x,y); float h=hash(cell+g);
                    float2 q=g+float2(h,hash(cell+g+17.7))*.7+.15-f;
                    float d=dot(q,q); if(d<nearest){second=nearest;nearest=d;id=h;} else second=min(second,d);
                }
                return float3(sqrt(nearest),id,second-nearest);
            }
            half4 frag(V i):SV_Target {
                float2 p=i.world.xz; float t=_Time.y;
                float bank=SAMPLE_TEXTURE2D(_ShoreMap,sampler_ShoreMap,(p-_MapRect.xy)/_MapRect.zw).r*12;
                float depth=saturate(bank/4.5+noise(p*.36)*.19);
                float2 drift=float2(t*.065,-t*.042);
                float n=noise(p*.8+drift), n2=noise(p*1.7-drift*.7);
                float2 refract=float2(n-.5,n2-.5)*.14;
                float3 rock=stones((p+refract+noise(p*1.2)*.17)*2.8);
                float pebble=smoothstep(.025,.15,rock.z)*(1-smoothstep(.30+rock.y*.08,.47+rock.y*.09,rock.x));
                half3 bed=lerp(half3(.10,.16,.16),lerp(half3(.18,.25,.23),half3(.28,.30,.24),rock.y),pebble);
                half3 color=lerp(_ShallowColor.rgb,_DeepColor.rgb,depth);
                color=lerp(color,bed,(1-depth)*.30);
                color*=.89+.18*noise(p*.37)+.055*n2;
                float wave=sin(p.x*3.8+p.y*5.1+n*5+t*.85);
                float broken=smoothstep(.55,.82,noise(p*float2(2.2,3.1)-drift));
                float rippleLine=smoothstep(.95,1,wave)*broken;
                float swell=sin(p.x*.9-p.y*1.4+n2*2+t*.36);
                float reflection=smoothstep(.75,1,swell)*smoothstep(.5,.85,n)*.10;
                color=lerp(color,_RippleColor.rgb,rippleLine*.32+reflection);
                float shore=(1-smoothstep(.08,.65,bank))*smoothstep(.35,.7,n2);
                color=lerp(color,_RippleColor.rgb,shore*.24);
                Light sun=GetMainLight(TransformWorldToShadowCoord(i.world));
                half3 ambient=SampleSH(half3(0,1,0));
                half3 illumination=clamp(ambient*.7+sun.color*(.30+.30*sun.shadowAttenuation),.10,1.15);
                return half4(color*illumination,1);
            }
            ENDHLSL
        }
    }
}

// 맵 조명 (MapLighting 이 전역 값으로 넣는다): 어두운 환경색 × 점광원(촛불 · 벽난로) 빛 웅덩이.
// 진짜 3D 조명이 아니라 바닥 평면(XZ) 거리만 본다 — 위에서 내려다보는 2D 게임처럼.
// 픽셀 아트처럼 보이게: 위치를 1/32 unit(= 1 texel) 격자로 끊고, 밝기를 몇 단계로 나누되
// 단계 사이는 4×4 Bayer 디더링 (월드 텍셀 좌표 기준이라 카메라가 움직여도 무늬가 따라오지 않는다).
#ifndef DG_LIGHTING_INCLUDED
#define DG_LIGHTING_INCLUDED

#define DG_MAX_LIGHTS 16

float _DgLightingOn;               // 0 = 조명 없음 (MapLighting 이 없는 씬) → 색 그대로
half4 _DgAmbient;                  // 빛이 안 닿는 곳에 곱할 색
float4 _DgLights[DG_MAX_LIGHTS];   // x, z, 반경, 세기 (깜빡임 반영)
half4 _DgLightColors[DG_MAX_LIGHTS];
float _DgLightCount;

static const float DgTexelsPerUnit = 32.0;
static const float DgLightSteps = 4.0;

float DgBayer4(float2 texel)
{
    int2 p = int2(fmod(abs(texel), 4.0));
    const float m[16] = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };
    return (m[p.y * 4 + p.x] + 0.5) / 16.0;
}

// 이 월드 위치에 곱할 빛 색. 1 보다 밝을 수 있다 (불 옆은 따뜻하게 과노출)
half3 DgLight(float3 positionWS)
{
    if (_DgLightingOn < 0.5)
    {
        return half3(1, 1, 1);
    }
    float2 texel = floor(positionWS.xz * DgTexelsPerUnit);
    float2 xz = (texel + 0.5) / DgTexelsPerUnit;
    float dither = DgBayer4(texel);

    half3 light = _DgAmbient.rgb;
    int count = (int)_DgLightCount;
    for (int i = 0; i < DG_MAX_LIGHTS; i++)
    {
        if (i >= count)
        {
            break;
        }
        float4 l = _DgLights[i];
        float falloff = saturate(1.0 - distance(xz, l.xy) / l.z);
        falloff *= falloff;
        falloff = floor(falloff * DgLightSteps + dither) / DgLightSteps;
        light += _DgLightColors[i].rgb * (falloff * l.w);
    }
    return min(light, 1.6);
}

#endif

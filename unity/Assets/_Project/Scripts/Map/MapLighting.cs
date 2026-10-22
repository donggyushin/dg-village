using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace DgVillage
{
    /// <summary>
    /// 맵 분위기 = 서버 맵 JSON 의 lighting + lights (GameMapData). MapBuilder 가 Map 루트 아래 "Lighting" 에 붙인다.
    /// 진짜 3D 조명이 아니라 픽셀 아트용 흉내:
    ///   점광원     어두운 환경색(ambient) 위에 촛불 · 벽난로 빛 웅덩이. 바닥 · 스프라이트 셰이더가 DgLighting.hlsl 로 계산
    ///              (전역 _DgLights 배열) + 불꽃 자리에 더하기 블렌딩 빛무리(Glow) 스프라이트. 깜빡임은 매 프레임 Perlin 노이즈
    ///   해 그림자  SunShadow(소품 · 캐릭터) 가 스프라이트 실루엣을 바닥에 비스듬히 눕힌다 → SunShadowOffset
    ///   구름 그림자 바닥 셰이더(DgVillage/Ground)가 월드 좌표 노이즈로 흘러가는 그늘을 그린다
    ///   색감      URP 후처리 Volume (밝기 · 대비 · 채도 · 색온도 · 비네트)
    /// 셰이더 값은 전역(Shader.SetGlobal*)이라 맵이 꺼지면 원래대로(= 효과 없음) 되돌린다.
    /// [ExecuteAlways]: 에디터 미리보기 방에서도 보인다. Volume · 빛무리는 저장하지 않는 자식에 매번 새로 만든다.
    /// </summary>
    [ExecuteAlways]
    public class MapLighting : MonoBehaviour
    {
        private const int MaxLights = 16; // DgLighting.hlsl 의 DG_MAX_LIGHTS 와 같게
        private const float TexelsPerUnit = 32f;
        private const string GlowMaterialName = "Glow"; // Resources/Glow.mat

        private static readonly int ShadowColorId = Shader.PropertyToID("_DgShadowColor");
        private static readonly int ShadowStrengthId = Shader.PropertyToID("_DgShadowStrength");
        private static readonly int CloudStrengthId = Shader.PropertyToID("_DgCloudStrength");
        private static readonly int LightingOnId = Shader.PropertyToID("_DgLightingOn");
        private static readonly int AmbientId = Shader.PropertyToID("_DgAmbient");
        private static readonly int LightsId = Shader.PropertyToID("_DgLights");
        private static readonly int LightColorsId = Shader.PropertyToID("_DgLightColors");
        private static readonly int LightCountId = Shader.PropertyToID("_DgLightCount");
        private static readonly Dictionary<int, Sprite> GlowSprites = new Dictionary<int, Sprite>();

        [SerializeField] private GameMapData.Lighting _lighting = new GameMapData.Lighting();
        [SerializeField] private GameMapData.Light[] _lights = new GameMapData.Light[0];

        private GameObject _volumeObject;
        private VolumeProfile _profile;
        private GameObject _glowRoot;
        private readonly List<SpriteRenderer> _glows = new List<SpriteRenderer>();
        // SetGlobalVectorArray 는 처음 넣은 길이로 고정되므로 항상 MaxLights 길이
        private readonly Vector4[] _lightData = new Vector4[MaxLights];
        private readonly Vector4[] _lightColors = new Vector4[MaxLights];
        private int _lightCount;
        private bool _flickers;

        /// <summary>해 그림자가 높이 1 unit 당 바닥에서 밀리는 거리 (x, z). zero = 해 그림자 없음</summary>
        public static Vector2 SunShadowOffset { get; private set; }

        public void Init(GameMapData.Lighting lighting, GameMapData.Light[] lights)
        {
            _lighting = lighting ?? new GameMapData.Lighting();
            _lights = lights ?? new GameMapData.Light[0];
            if (isActiveAndEnabled)
            {
                Apply();
            }
        }

        private void OnEnable() => Apply();

        // 맵을 끄면 (MapLoader 가 SetActive(false)) Volume · 빛무리 자식도 같이 꺼진다. 꺼지는 중엔 오브젝트를 지울 수 없어
        // 지우는 건 OnDestroy 에서
        private void OnDisable()
        {
            SunShadowOffset = Vector2.zero;
            Shader.SetGlobalFloat(ShadowStrengthId, 0f);
            Shader.SetGlobalFloat(CloudStrengthId, 0f);
            Shader.SetGlobalFloat(LightingOnId, 0f);
        }

        private void OnDestroy()
        {
            DestroyNow(_volumeObject);
            DestroyNow(_glowRoot);
            DestroyNow(_profile);
        }

        internal static void DestroyNow(Object target)
        {
            if (target == null)
            {
                return;
            }
            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }

        private void Update()
        {
            if (_flickers)
            {
                UpdateLights();
            }
        }

        private void Apply()
        {
            GameMapData.Lighting l = _lighting;
            SunShadowOffset = new Vector2(l.shadowX, l.shadowZ);
            Shader.SetGlobalColor(ShadowColorId, ParseColor(l.shadowColor, Color.black));
            Shader.SetGlobalFloat(ShadowStrengthId, Mathf.Clamp01(l.shadowStrength));
            Shader.SetGlobalFloat(CloudStrengthId, Mathf.Clamp01(l.clouds));
            ApplyLights(l);
            ApplyVolume(l);
        }

        private void ApplyLights(GameMapData.Lighting l)
        {
            bool on = !string.IsNullOrEmpty(l.ambient);
            Shader.SetGlobalFloat(LightingOnId, on ? 1f : 0f);
            Shader.SetGlobalColor(AmbientId, ParseColor(l.ambient, Color.white));

            DestroyNow(_glowRoot);
            _glows.Clear();
            _lightCount = on ? Mathf.Min(_lights.Length, MaxLights) : 0;
            _flickers = false;
            if (_lights.Length > MaxLights)
            {
                Debug.LogWarning($"MapLighting: 점광원은 {MaxLights}개까지 — 나머지는 무시");
            }
            if (_lightCount > 0)
            {
                _glowRoot = new GameObject("Glows") { hideFlags = HideFlags.DontSave };
                _glowRoot.transform.SetParent(transform, false);
            }
            var glowMaterial = Resources.Load<Material>(GlowMaterialName);
            for (int i = 0; i < _lightCount; i++)
            {
                GameMapData.Light light = _lights[i];
                _flickers |= light.flicker > 0f;
                _lightColors[i] = ParseColor(light.color, Color.white);
                _glows.Add(light.glow > 0f && glowMaterial != null ? CreateGlow(light, glowMaterial) : null);
            }
            UpdateLights();
        }

        private SpriteRenderer CreateGlow(GameMapData.Light light, Material material)
        {
            var go = new GameObject("Glow", typeof(SpriteRenderer)) { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(_glowRoot.transform, false);
            // 캐릭터 · 소품과 같은 기울기: 빛무리도 카메라를 정면으로 본다
            Quaternion standUp = ViewAngle.Billboard;
            go.transform.SetPositionAndRotation(
                new Vector3(light.x, 0f, light.z) + standUp * Vector3.up * light.glowY, standUp);
            var renderer = go.GetComponent<SpriteRenderer>();
            renderer.sprite = GlowSprite(Mathf.Max(2, Mathf.RoundToInt(light.glow * TexelsPerUnit)));
            renderer.sharedMaterial = material;
            renderer.color = ParseColor(light.color, Color.white);
            // 같은 거리면 소품 · 캐릭터보다 나중에 = 위에 덮는다
            renderer.sortingOrder = 1;
            return renderer;
        }

        // 깜빡임: 세기와 빛무리 밝기를 같이 흔든다 (불마다 노이즈 위치를 다르게)
        private void UpdateLights()
        {
            float time = Application.isPlaying ? Time.time : (float)Time.realtimeSinceStartupAsDouble;
            for (int i = 0; i < _lightCount; i++)
            {
                GameMapData.Light light = _lights[i];
                float flicker = 1f - Mathf.Clamp01(light.flicker) * Mathf.PerlinNoise(time * 6f, i * 7.31f);
                _lightData[i] = new Vector4(light.x, light.z, Mathf.Max(0.01f, light.radius), light.intensity * flicker);
                if (_glows[i] != null)
                {
                    Color color = _glows[i].color;
                    color.a = flicker;
                    _glows[i].color = color;
                }
            }
            Shader.SetGlobalVectorArray(LightsId, _lightData);
            Shader.SetGlobalVectorArray(LightColorsId, _lightColors);
            Shader.SetGlobalFloat(LightCountId, _lightCount);
        }

        // 지름 size 픽셀의 빛무리: 계단 4단 + 4×4 Bayer 디더링 (DgLighting.hlsl 의 빛 웅덩이와 같은 느낌). 크기별로 하나만 만든다
        private static Sprite GlowSprite(int size)
        {
            if (GlowSprites.TryGetValue(size, out Sprite cached) && cached != null)
            {
                return cached;
            }
            int[] bayer = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave,
            };
            var pixels = new Color32[size * size];
            float half = size / 2f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float r = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(half, half)) / half;
                    float f = Mathf.Clamp01(1f - r);
                    f *= f;
                    float stepped = Mathf.Floor(f * 4f + (bayer[(y % 4) * 4 + x % 4] + 0.5f) / 16f) / 4f;
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(stepped * 0.85f) * 255));
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), TexelsPerUnit);
            sprite.hideFlags = HideFlags.DontSave;
            GlowSprites[size] = sprite;
            return sprite;
        }

        private void ApplyVolume(GameMapData.Lighting l)
        {
            if (_volumeObject == null)
            {
                _volumeObject = new GameObject("Volume") { hideFlags = HideFlags.DontSave };
                _volumeObject.transform.SetParent(transform, false);
                _volumeObject.AddComponent<Volume>().isGlobal = true;
            }
            DestroyNow(_profile);
            _profile = ScriptableObject.CreateInstance<VolumeProfile>();
            _profile.hideFlags = HideFlags.DontSave;

            var color = _profile.Add<ColorAdjustments>();
            color.postExposure.Override(l.exposure);
            color.contrast.Override(l.contrast);
            color.saturation.Override(l.saturation);
            color.colorFilter.Override(ParseColor(l.colorFilter, Color.white));
            _profile.Add<WhiteBalance>().temperature.Override(l.temperature);
            if (l.vignette > 0f)
            {
                var vignette = _profile.Add<Vignette>();
                vignette.intensity.Override(l.vignette);
                vignette.smoothness.Override(0.5f);
            }

            var volume = _volumeObject.GetComponent<Volume>();
            volume.priority = 10f;
            volume.sharedProfile = _profile;
        }

        private static Color ParseColor(string hex, Color fallback) =>
            !string.IsNullOrEmpty(hex) && ColorUtility.TryParseHtmlString(hex, out Color color) ? color : fallback;
    }
}

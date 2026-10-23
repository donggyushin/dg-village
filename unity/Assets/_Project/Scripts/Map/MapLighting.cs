using System;
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
    ///   낮 · 밤    night 가 있으면 실제 한국 시각(DayCycle)에 따라 lighting(낮) 과 night 를 섞는다 (1초마다).
    ///              점광원 when = "day" / "night" 은 그 때만 켜진다 (창문 햇빛 · 달빛). 창문 유리엔 sky 색을 곱한다
    ///   저녁노을   dusk 가 있으면 해 질 무렵(DayCycle.Dusk) 그 값을 낮 · 밤 섞은 값 위에 다시 섞는다
    ///   빛줄기     창문(windows)마다 창문 윗변에서 바닥까지 비스듬히 내려오는 더하기 블렌딩 면 (DgVillage/LightShaft).
    ///              세기 · 색 · 길이(shaftReach) · 기울기(shaftSlant) 도 시각에 따라 섞인다 — 노을엔 길고 붉게
    /// 셰이더 값은 전역(Shader.SetGlobal*)이라 맵이 꺼지면 원래대로(= 효과 없음) 되돌린다.
    /// [ExecuteAlways]: 에디터 미리보기 방에서도 보인다. Volume · 빛무리는 저장하지 않는 자식에 매번 새로 만든다.
    /// </summary>
    [ExecuteAlways]
    public class MapLighting : MonoBehaviour
    {
        private const int MaxLights = 16; // DgLighting.hlsl 의 DG_MAX_LIGHTS 와 같게
        private const float TexelsPerUnit = 32f;
        private const string GlowMaterialName = "Glow"; // Resources/Glow.mat
        private const string ShaftMaterialName = "LightShaft"; // Resources/LightShaft.mat
        // 창문 그림(art/tilesets/make_floor_atlas.py draw_window)의 유리 구멍: 벽면 위 높이 (벽 4 unit = 128px) · 가운데에서 반 폭
        private const float WindowTop = (128f - 18f) / TexelsPerUnit;
        private const float WindowHalfWidth = 22f / TexelsPerUnit;

        private static readonly int ShadowColorId = Shader.PropertyToID("_DgShadowColor");
        private static readonly int ShadowStrengthId = Shader.PropertyToID("_DgShadowStrength");
        private static readonly int CloudStrengthId = Shader.PropertyToID("_DgCloudStrength");
        private static readonly int LightingOnId = Shader.PropertyToID("_DgLightingOn");
        private static readonly int AmbientId = Shader.PropertyToID("_DgAmbient");
        private static readonly int LightsId = Shader.PropertyToID("_DgLights");
        private static readonly int LightColorsId = Shader.PropertyToID("_DgLightColors");
        private static readonly int LightCountId = Shader.PropertyToID("_DgLightCount");
        private static readonly int SkyId = Shader.PropertyToID("_DgSky");
        private static readonly int ShaftColorId = Shader.PropertyToID("_Color");
        private const float DaylightCheckSeconds = 1f;
        private static readonly Dictionary<int, Sprite> GlowSprites = new Dictionary<int, Sprite>();

        [SerializeField] private GameMapData.Lighting _lighting = new GameMapData.Lighting();
        [SerializeField] private GameMapData.Lighting _night = new GameMapData.Lighting();
        [SerializeField] private GameMapData.Lighting _dusk = new GameMapData.Lighting();
        [SerializeField] private GameMapData.Light[] _lights = new GameMapData.Light[0];
        [SerializeField] private GameMapData.Window[] _windows = new GameMapData.Window[0];

        private GameObject _volumeObject;
        private VolumeProfile _profile;
        private ColorAdjustments _colorAdjustments;
        private WhiteBalance _whiteBalance;
        private Vignette _vignette;
        private GameObject _glowRoot;
        private readonly List<SpriteRenderer> _glows = new List<SpriteRenderer>();
        private GameObject _shaftObject;
        private Mesh _shaftMesh;
        private MaterialPropertyBlock _shaftBlock;
        // SetGlobalVectorArray 는 처음 넣은 길이로 고정되므로 항상 MaxLights 길이
        private readonly Vector4[] _lightData = new Vector4[MaxLights];
        private readonly Vector4[] _lightColors = new Vector4[MaxLights];
        private int _lightCount;
        private bool _flickers;
        private bool _dirty;
        private bool _hasNight;
        private bool _hasDusk;
        private float _daylight = 1f; // 0 = 밤, 1 = 낮 (DayCycle). night 가 없으면 항상 1
        private float _duskWeight; // 0..1 (DayCycle.Dusk). dusk 가 없으면 항상 0
        private float _nextDaylightCheck;

        /// <summary>해 그림자가 높이 1 unit 당 바닥에서 밀리는 거리 (x, z). zero = 해 그림자 없음</summary>
        public static Vector2 SunShadowOffset { get; private set; }

        public void Init(GameMapData.Lighting lighting, GameMapData.Lighting night, GameMapData.Lighting dusk,
            GameMapData.Light[] lights, GameMapData.Window[] windows)
        {
            _lighting = lighting ?? new GameMapData.Lighting();
            _night = night ?? new GameMapData.Lighting();
            _dusk = dusk ?? new GameMapData.Lighting();
            _lights = lights ?? new GameMapData.Light[0];
            _windows = windows ?? new GameMapData.Window[0];
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
            Shader.SetGlobalColor(SkyId, Color.clear);
        }

        private void OnDestroy()
        {
            DestroyNow(_volumeObject);
            DestroyNow(_glowRoot);
            DestroyNow(_profile);
            DestroyNow(_shaftObject);
            DestroyNow(_shaftMesh);
        }

        internal static void DestroyNow(UnityEngine.Object target)
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

        // 인스펙터에서 값을 바꾸면 바로 반영. OnValidate 안에서는 빛무리를 지울 수 없어서 다음 Update 에서 다시 만든다
        private void OnValidate() => _dirty = true;

        private void Update()
        {
            if (_dirty)
            {
                _dirty = false;
                Apply();
            }
            float now = (float)Time.realtimeSinceStartupAsDouble;
            if ((_hasNight || _hasDusk) && now >= _nextDaylightCheck)
            {
                _nextDaylightCheck = now + DaylightCheckSeconds;
                float daylight = _hasNight ? DayCycle.Daylight : 1f;
                float dusk = _hasDusk ? DayCycle.Dusk : 0f;
                if (Mathf.Abs(daylight - _daylight) > 0.0005f || Mathf.Abs(dusk - _duskWeight) > 0.0005f)
                {
                    _daylight = daylight;
                    _duskWeight = dusk;
                    ApplyTimeOfDay();
                    return;
                }
            }
            if (_flickers)
            {
                UpdateLights();
            }
        }

        private void Apply()
        {
            _hasNight = !IsEmpty(_night);
            _hasDusk = !IsEmpty(_dusk);
            _daylight = _hasNight ? DayCycle.Daylight : 1f;
            _duskWeight = _hasDusk ? DayCycle.Dusk : 0f;
            _nextDaylightCheck = (float)Time.realtimeSinceStartupAsDouble + DaylightCheckSeconds;
            // 낮이나 밤 한쪽에만 ambient 가 있어도 켠다 (야외: 낮 = 조명 없음, 밤 = 어둡게). 빈 쪽은 흰색 = 그대로
            bool on = !string.IsNullOrEmpty(_lighting.ambient) || (_hasNight && !string.IsNullOrEmpty(_night.ambient))
                || (_hasDusk && !string.IsNullOrEmpty(_dusk.ambient));
            CreateGlows(on);
            CreateVolume();
            CreateShafts();
            ApplyTimeOfDay();
        }

        // 지금 _daylight 로 낮 · 밤 값을 섞고, 그 위에 _duskWeight 만큼 노을을 섞어 셰이더 전역 값 · Volume · 점광원 · 빛줄기에 넣는다
        private void ApplyTimeOfDay()
        {
            GameMapData.Lighting day = _lighting;
            GameMapData.Lighting night = _hasNight ? _night : _lighting;
            GameMapData.Lighting dusk = _dusk;
            float t = _daylight;
            float w = _hasDusk ? _duskWeight : 0f;
            float Mix(Func<GameMapData.Lighting, float> value) =>
                Mathf.Lerp(Mathf.Lerp(value(night), value(day), t), value(dusk), w);
            Color MixColor(Func<GameMapData.Lighting, string> value, Color empty) =>
                Color.Lerp(Color.Lerp(ParseColor(value(night), empty), ParseColor(value(day), empty), t), ParseColor(value(dusk), empty), w);

            // 그림자 방향은 낮 · 밤을 섞지 않는다 (밤엔 진하기만 0 으로). 노을에 방향이 있으면 그쪽으로 (해가 낮아 길게)
            Vector2 sun = day.shadowX != 0f || day.shadowZ != 0f
                ? new Vector2(day.shadowX, day.shadowZ)
                : new Vector2(night.shadowX, night.shadowZ);
            if (dusk.shadowX != 0f || dusk.shadowZ != 0f)
            {
                sun = Vector2.Lerp(sun, new Vector2(dusk.shadowX, dusk.shadowZ), w);
            }
            SunShadowOffset = sun;
            Shader.SetGlobalColor(ShadowColorId, MixColor(l => l.shadowColor, Color.black));
            Shader.SetGlobalFloat(ShadowStrengthId, Mathf.Clamp01(Mix(l => l.shadowStrength)));
            Shader.SetGlobalFloat(CloudStrengthId, Mathf.Clamp01(Mix(l => l.clouds)));
            Shader.SetGlobalColor(AmbientId, MixColor(l => l.ambient, Color.white));
            Color sky = MixColor(l => l.sky, Color.white);
            sky.a = 1f; // a = 0 (MapLighting 없음) 이면 셰이더가 유리를 그대로 둔다
            Shader.SetGlobalColor(SkyId, sky);

            _colorAdjustments.postExposure.Override(Mix(l => l.exposure));
            _colorAdjustments.contrast.Override(Mix(l => l.contrast));
            _colorAdjustments.saturation.Override(Mix(l => l.saturation));
            _colorAdjustments.colorFilter.Override(MixColor(l => l.colorFilter, Color.white));
            _whiteBalance.temperature.Override(Mix(l => l.temperature));
            _vignette.intensity.Override(Mix(l => l.vignette));
            UpdateShafts(Mathf.Clamp01(Mix(l => l.shafts)), MixColor(l => l.shaftColor, Color.white), Mix(l => l.shaftReach), Mix(l => l.shaftSlant));
            UpdateLights();
        }

        private void CreateShafts()
        {
            if (_shaftObject != null)
            {
                return;
            }
            var material = Resources.Load<Material>(ShaftMaterialName);
            if (material == null)
            {
                return;
            }
            _shaftObject = new GameObject("Shafts", typeof(MeshFilter), typeof(MeshRenderer)) { hideFlags = HideFlags.DontSave };
            _shaftObject.transform.SetParent(transform, false);
            _shaftMesh = new Mesh { name = "LightShafts", hideFlags = HideFlags.DontSave };
            _shaftMesh.MarkDynamic();
            _shaftObject.GetComponent<MeshFilter>().sharedMesh = _shaftMesh;
            var renderer = _shaftObject.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        // 창문마다 면 하나: 유리 윗변(벽 위) → 그 윗변을 지난 햇빛이 바닥에 닿는 곳. uv = (가로 0..1, 창문 0 → 바닥 1)
        private void UpdateShafts(float strength, Color color, float reach, float slant)
        {
            if (_shaftObject == null)
            {
                return;
            }
            FloorMesh floor = transform.parent != null ? transform.parent.GetComponentInChildren<FloorMesh>() : null;
            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();
            if (strength > 0.001f && floor != null && reach > 0f)
            {
                Vector3 wallUp = ViewAngle.Billboard * Vector3.up;
                Vector3 local = _shaftObject.transform.position;
                foreach (GameMapData.Window window in _windows)
                {
                    if (!floor.TryGetWindowWall(new Vector2(window.x, window.z), out Vector3 along, out Vector3 inward))
                    {
                        continue;
                    }
                    // 벽은 스프라이트처럼 뒤로 누워 있어 유리 윗변이 실제로는 벽 선 뒤에 있다 → 바닥에 닿는 곳은 벽 선(바닥)에서 잰다
                    var foot = new Vector3(window.x, 0f, window.z);
                    Vector3 center = foot + wallUp * WindowTop;
                    Vector3 left = center - along * WindowHalfWidth;
                    Vector3 right = center + along * WindowHalfWidth;
                    Vector3 drop = foot + (inward * reach + along * slant) * WindowTop - center;
                    int v = vertices.Count;
                    vertices.Add(left - local);
                    vertices.Add(right - local);
                    vertices.Add(right + drop - local);
                    vertices.Add(left + drop - local);
                    uvs.Add(new Vector2(0f, 0f));
                    uvs.Add(new Vector2(1f, 0f));
                    uvs.Add(new Vector2(1f, 1f));
                    uvs.Add(new Vector2(0f, 1f));
                    triangles.AddRange(new[] { v, v + 1, v + 2, v, v + 2, v + 3 });
                }
            }
            _shaftMesh.Clear();
            _shaftMesh.SetVertices(vertices);
            _shaftMesh.SetUVs(0, uvs);
            _shaftMesh.SetTriangles(triangles, 0);
            _shaftMesh.RecalculateBounds();
            var renderer = _shaftObject.GetComponent<MeshRenderer>();
            renderer.enabled = vertices.Count > 0;
            _shaftBlock ??= new MaterialPropertyBlock();
            _shaftBlock.SetColor(ShaftColorId, color * strength);
            renderer.SetPropertyBlock(_shaftBlock);
        }

        private void CreateGlows(bool on)
        {
            Shader.SetGlobalFloat(LightingOnId, on ? 1f : 0f);
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

        // 깜빡임: 세기와 빛무리 밝기를 같이 흔든다 (불마다 노이즈 위치를 다르게). when 이 있는 불은 낮 · 밤에 따라 줄인다
        private void UpdateLights()
        {
            float time = Application.isPlaying ? Time.time : (float)Time.realtimeSinceStartupAsDouble;
            for (int i = 0; i < _lightCount; i++)
            {
                GameMapData.Light light = _lights[i];
                float flicker = 1f - Mathf.Clamp01(light.flicker) * Mathf.PerlinNoise(time * 6f, i * 7.31f);
                flicker *= light.when switch
                {
                    "day" => _daylight,
                    "night" => 1f - _daylight,
                    _ => 1f,
                };
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

        // 값은 ApplyTimeOfDay 가 매번 덮어쓴다 (낮 · 밤이 바뀔 때 프로필을 다시 만들지 않게)
        private void CreateVolume()
        {
            if (_volumeObject == null)
            {
                _volumeObject = new GameObject("Volume") { hideFlags = HideFlags.DontSave };
                _volumeObject.transform.SetParent(transform, false);
                _volumeObject.AddComponent<Volume>().isGlobal = true;
            }
            // 스크립트 다시 컴파일하면 _profile 만 살아남고 컴포넌트 참조가 풀릴 수 있다 → 같이 확인
            if (_profile == null || _colorAdjustments == null || _whiteBalance == null || _vignette == null)
            {
                DestroyNow(_profile);
                _profile = ScriptableObject.CreateInstance<VolumeProfile>();
                _profile.hideFlags = HideFlags.DontSave;
                _colorAdjustments = _profile.Add<ColorAdjustments>();
                _whiteBalance = _profile.Add<WhiteBalance>();
                _vignette = _profile.Add<Vignette>();
                _vignette.smoothness.Override(0.5f);
            }

            var volume = _volumeObject.GetComponent<Volume>();
            volume.priority = 10f;
            volume.sharedProfile = _profile;
        }

        // JsonUtility 는 JSON 에 없는 객체도 기본값으로 채운다 → 전부 기본값이면 "없음"
        private static bool IsEmpty(GameMapData.Lighting l) =>
            l == null || JsonUtility.ToJson(l) == JsonUtility.ToJson(new GameMapData.Lighting());

        private static Color ParseColor(string hex, Color fallback) =>
            !string.IsNullOrEmpty(hex) && ColorUtility.TryParseHtmlString(hex, out Color color) ? color : fallback;
    }
}

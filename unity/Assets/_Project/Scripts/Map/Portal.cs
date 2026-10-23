using System.Collections.Generic;
using UnityEngine;

namespace DgVillage
{
    /// <summary>
    /// 다른 맵으로 가는 바닥 영역. 맵 JSON 의 portals 를 MapBuilder 가 깔아 둔다.
    /// 위치 = 영역 가운데, Size = 가로 × 깊이. 플레이어 발이 이 안에서 E 를 누르면 PortalTraveler 가 서버에
    /// travel 을 보낸다 (도착 위치 Spawn 은 서버가 정한다). 선택하면 Scene 뷰에 하늘색 사각형으로 보인다.
    /// Play 중에는 포탈인 줄 알게 바닥에 맥동하는 빛 + 위로 옅어지는 빛기둥 (더하기 블렌딩, Resources/Glow.mat)과 떠오르는 반짝이를 그린다.
    /// </summary>
    public class Portal : MonoBehaviour
    {
        private static readonly List<Portal> Active = new();

        private const float TexelsPerUnit = 32f;
        private const string GlowMaterialName = "Glow"; // Resources/Glow.mat
        // 바닥 빛이 영역 밖으로 번지는 폭 (unit, 양쪽 각각)
        private const float FloorGlowMargin = 0.4f;
        private const int SparkleCount = 16;
        // 빛기둥 높이 (unit)
        private const float PillarHeight = 1.75f;
        private const float SparkleRise = 1.6f;

        private static Sprite _sparkleSprite;

        [Tooltip("빛 색")]
        [SerializeField] private Color _color = new(0.45f, 0.8f, 1f);

        [Tooltip("갈 맵 id (서버 maps/<id>.json)")]
        [SerializeField] private string _to;

        [Tooltip("바닥에서 차지하는 크기 (x = 가로, y = 깊이) unit")]
        [SerializeField] private Vector2 _size = Vector2.one;

        [Tooltip("도착하는 맵에서 시작할 발 위치 (x, z)")]
        [SerializeField] private Vector2 _spawn;

        public string To => _to;
        public Vector3 Spawn => new(_spawn.x, 0f, _spawn.y);
        public Vector2 Size => _size;

        /// <summary>안내 문구에 쓰는 갈 맵 이름 ("정원")</summary>
        public string Label => _to switch
        {
            "office" => "길드 홀",
            "outdoor" => "정원",
            _ => _to,
        };

        private SpriteRenderer _floorGlow;
        private Sprite _floorGlowSprite;
        private SpriteRenderer _pillar;
        private Sprite _pillarSprite;
        private SpriteRenderer[] _sparkles;
        private float[] _sparkleStart;
        private float[] _sparkleLife;

        /// <summary>런타임에 만들 때 (MapBuilder)</summary>
        public void Init(string to, Vector2 size, Vector2 spawn)
        {
            _to = to;
            _size = size;
            _spawn = spawn;
        }

        private void OnEnable() => Active.Add(this);

        private void Start()
        {
            var material = Resources.Load<Material>(GlowMaterialName);
            if (!Application.isPlaying || material == null)
            {
                return;
            }
            // 바닥에 눕힌 빛: 스프라이트의 위(+Y)가 월드 +Z 로 가게 X 90°
            _floorGlowSprite = FloorGlowSprite(_size);
            _floorGlow = CreateGlowSprite("FloorGlow", _floorGlowSprite, material);
            _floorGlow.transform.SetLocalPositionAndRotation(new Vector3(0f, 0.02f, 0f), Quaternion.Euler(90f, 0f, 0f));

            // 빛기둥: 캐릭터처럼 카메라를 보고 서 있다 (밑변 = 영역 가운데)
            _pillarSprite = PillarSprite(_size.x);
            _pillar = CreateGlowSprite("Pillar", _pillarSprite, material);

            _sparkles = new SpriteRenderer[SparkleCount];
            _sparkleStart = new float[SparkleCount];
            _sparkleLife = new float[SparkleCount];
            for (int i = 0; i < SparkleCount; i++)
            {
                _sparkles[i] = CreateGlowSprite("Sparkle", SparkleSprite(), material);
                // 처음부터 고르게 퍼져 있게 시작 시각을 흩어 둔다
                RestartSparkle(i, Time.time - Random.value * 2f);
            }
        }

        private void Update()
        {
            if (_floorGlow == null)
            {
                return;
            }
            float time = Time.time;
            Color floor = _color;
            float pulse = 0.5f + 0.5f * Mathf.Sin(time * 2.5f);
            floor.a = 0.75f + 0.25f * pulse;
            _floorGlow.color = floor;
            Color pillar = _color;
            pillar.a = 0.6f + 0.4f * pulse;
            _pillar.color = pillar;
            _pillar.transform.SetLocalPositionAndRotation(Vector3.zero, ViewAngle.Billboard);

            Quaternion standUp = ViewAngle.Billboard;
            for (int i = 0; i < SparkleCount; i++)
            {
                float t = (time - _sparkleStart[i]) / _sparkleLife[i];
                if (t >= 1f)
                {
                    RestartSparkle(i, time);
                    t = 0f;
                }
                Transform sparkle = _sparkles[i].transform;
                Vector3 local = sparkle.localPosition;
                // 높이도 텍셀 격자에 맞춰 계단식으로 오른다 (픽셀 아트)
                local.y = Mathf.Round(t * SparkleRise * TexelsPerUnit) / TexelsPerUnit;
                sparkle.SetLocalPositionAndRotation(local, standUp);
                Color color = Color.Lerp(Color.white, _color, 0.5f);
                // 빠르게 나타나 천천히 사라진다
                color.a = Mathf.Min(1f, t * 6f) * (1f - t);
                _sparkles[i].color = color;
            }
        }

        // 바닥 빛 텍스처는 포탈마다 만든다 → 맵을 바꿀 때 같이 지운다 (반짝이는 공용이라 남긴다)
        private void OnDestroy()
        {
            foreach (Sprite sprite in new[] { _floorGlowSprite, _pillarSprite })
            {
                if (sprite != null)
                {
                    Destroy(sprite.texture);
                    Destroy(sprite);
                }
            }
        }

        private void RestartSparkle(int i, float start)
        {
            _sparkleStart[i] = start;
            _sparkleLife[i] = Random.Range(1.4f, 2.4f);
            float x = Random.Range(-_size.x / 2f, _size.x / 2f);
            float z = Random.Range(-_size.y / 2f, _size.y / 2f);
            _sparkles[i].transform.localPosition = new Vector3(
                Mathf.Round(x * TexelsPerUnit) / TexelsPerUnit, 0f, Mathf.Round(z * TexelsPerUnit) / TexelsPerUnit);
        }

        private SpriteRenderer CreateGlowSprite(string name, Sprite sprite, Material material)
        {
            var go = new GameObject(name, typeof(SpriteRenderer));
            go.transform.SetParent(transform, false);
            var renderer = go.GetComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sharedMaterial = material;
            renderer.color = _color;
            // 같은 거리면 소품 · 캐릭터보다 나중에 = 위에 덮는다 (MapLighting 빛무리와 같게)
            renderer.sortingOrder = 1;
            return renderer;
        }

        // 영역보다 조금 큰 타원 빛: 계단 4단 + 4×4 Bayer 디더링 (MapLighting 빛무리와 같은 느낌)
        private static Sprite FloorGlowSprite(Vector2 size)
        {
            int width = Mathf.Max(4, Mathf.RoundToInt((size.x + FloorGlowMargin * 2f) * TexelsPerUnit));
            int height = Mathf.Max(4, Mathf.RoundToInt((size.y + FloorGlowMargin * 2f) * TexelsPerUnit));
            int[] bayer = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };
            var pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float dx = (x + 0.5f) / width * 2f - 1f;
                    float dy = (y + 0.5f) / height * 2f - 1f;
                    float f = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                    // 가운데는 평평하게 밝고 가장자리만 빠르게 어두워지게
                    f = Mathf.Sqrt(f);
                    float stepped = Mathf.Floor(f * 4f + (bayer[(y % 4) * 4 + x % 4] + 0.5f) / 16f) / 4f;
                    pixels[y * width + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(stepped * 0.8f) * 255));
                }
            }
            return CreateSprite(width, height, pixels);
        }

        // 빛기둥: 가로는 가운데가 밝고, 위로 갈수록 옅어진다. 피벗 = 밑변 가운데
        private static Sprite PillarSprite(float width)
        {
            int w = Mathf.Max(4, Mathf.RoundToInt(width * TexelsPerUnit));
            int h = Mathf.RoundToInt(PillarHeight * TexelsPerUnit);
            int[] bayer = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };
            var pixels = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float dx = (x + 0.5f) / w * 2f - 1f;
                    float f = Mathf.Sqrt(Mathf.Clamp01(1f - dx * dx)) * (1f - (y + 0.5f) / h);
                    float stepped = Mathf.Floor(f * 4f + (bayer[(y % 4) * 4 + x % 4] + 0.5f) / 16f) / 4f;
                    pixels[y * w + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(stepped * 0.4f) * 255));
                }
            }
            return CreateSprite(w, h, pixels, new Vector2(0.5f, 0f));
        }

        // 3×3 십자 반짝이 (가운데가 가장 밝다). 포탈끼리 하나를 같이 쓴다
        private static Sprite SparkleSprite()
        {
            if (_sparkleSprite != null)
            {
                return _sparkleSprite;
            }
            var pixels = new Color32[9];
            var arm = new Color32(255, 255, 255, 140);
            pixels[1] = arm;
            pixels[3] = arm;
            pixels[4] = new Color32(255, 255, 255, 255);
            pixels[5] = arm;
            pixels[7] = arm;
            _sparkleSprite = CreateSprite(3, 3, pixels);
            return _sparkleSprite;
        }

        private static Sprite CreateSprite(int width, int height, Color32[] pixels) =>
            CreateSprite(width, height, pixels, new Vector2(0.5f, 0.5f));

        private static Sprite CreateSprite(int width, int height, Color32[] pixels, Vector2 pivot)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave,
            };
            texture.SetPixels32(pixels);
            texture.Apply();
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, width, height), pivot, TexelsPerUnit);
            sprite.hideFlags = HideFlags.DontSave;
            return sprite;
        }

        /// <summary>"정원" → "으로", "길드 홀" → "로" (받침 없음 · ㄹ 받침 = 로)</summary>
        public static string DirectionParticle(string word)
        {
            if (string.IsNullOrEmpty(word))
            {
                return "로";
            }
            char last = word[^1];
            if (last < '가' || last > '힣')
            {
                return "(으)로";
            }
            int coda = (last - '가') % 28;
            return coda == 0 || coda == 8 ? "로" : "으로";
        }

        private void OnDisable() => Active.Remove(this);

        /// <summary>발 위치가 들어 있는 포탈. 없으면 null</summary>
        public static Portal At(Vector3 position)
        {
            foreach (Portal portal in Active)
            {
                Vector3 d = position - portal.transform.position;
                if (Mathf.Abs(d.x) <= portal._size.x / 2f && Mathf.Abs(d.z) <= portal._size.y / 2f)
                {
                    return portal;
                }
            }
            return null;
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.4f, 0.9f, 1f);
            Gizmos.DrawWireCube(transform.position + Vector3.up * 0.05f, new Vector3(_size.x, 0.1f, _size.y));
        }
    }
}

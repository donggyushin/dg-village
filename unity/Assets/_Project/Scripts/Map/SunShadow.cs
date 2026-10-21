using UnityEngine;

namespace DgVillage
{
    /// <summary>
    /// 해 그림자: 이 스프라이트(소품 · 캐릭터 Visual)의 실루엣을 바닥에 눕혀 비스듬히 늘인다.
    /// 스프라이트의 높이 y 인 픽셀은 바닥에서 MapLighting.SunShadowOffset × y 만큼 밀린 곳에 찍힌다
    /// (가로줄은 가로 그대로 = 기울인 사각형, 픽셀 격자가 돌아가지 않는다).
    /// 그림자 메시는 부모(발 위치 루트) 아래 저장 안 하는 자식으로 만들고, 스프라이트 · 좌우 반전 · 해 방향이 바뀌면 다시 만든다
    /// (캐릭터는 걷기 프레임마다 바뀐다 — 꼭짓점 4개라 싸다).
    /// 해 그림자 없는 맵(실내)에선 숨긴다.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(SpriteRenderer))]
    public class SunShadow : MonoBehaviour
    {
        private const string MaterialName = "SunShadow"; // Resources/SunShadow.mat
        private const float GroundLift = 0.004f; // 바닥(0)과 겹쳐 깜빡이지 않도록
        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        private static Material _material;

        private SpriteRenderer _source;
        private GameObject _shadow;
        private MeshRenderer _renderer;
        private Mesh _mesh;
        private MaterialPropertyBlock _block;

        private Sprite _builtSprite;
        private bool _builtFlip;
        private Vector2 _builtOffset;

        private void OnEnable()
        {
            _source = GetComponent<SpriteRenderer>();
        }

        // 맵째 꺼질 때(SetActive(false)) 그림자도 형제라 같이 꺼진다. 꺼지는 중엔 지울 수 없어 숨기기만 하고 지우는 건 OnDestroy
        private void OnDisable()
        {
            if (_renderer != null)
            {
                _renderer.enabled = false;
            }
        }

        private void OnDestroy()
        {
            MapLighting.DestroyNow(_shadow);
            MapLighting.DestroyNow(_mesh);
        }

        private void LateUpdate()
        {
            Vector2 offset = MapLighting.SunShadowOffset;
            Sprite sprite = _source.sprite;
            bool visible = offset != Vector2.zero && sprite != null && _source.enabled;
            if (!visible)
            {
                if (_renderer != null)
                {
                    _renderer.enabled = false;
                }
                return;
            }
            if (_shadow == null && !CreateShadow())
            {
                return;
            }
            if (sprite != _builtSprite || _source.flipX != _builtFlip || offset != _builtOffset)
            {
                Rebuild(sprite, _source.flipX, offset);
            }
            _renderer.enabled = true;
            // 바닥(부모 = 발 위치 루트의 높이)에 눕힌다. 앉을 때 Visual 을 올려도 그림자는 바닥에
            Vector3 position = transform.position;
            float groundY = transform.parent != null ? transform.parent.position.y : 0f;
            _shadow.transform.SetPositionAndRotation(new Vector3(position.x, groundY + GroundLift, position.z), Quaternion.identity);
        }

        private bool CreateShadow()
        {
            if (_material == null)
            {
                _material = Resources.Load<Material>(MaterialName);
                if (_material == null)
                {
                    return false;
                }
            }
            _shadow = new GameObject("SunShadow", typeof(MeshFilter), typeof(MeshRenderer)) { hideFlags = HideFlags.DontSave };
            _shadow.transform.SetParent(transform.parent, true);
            _mesh = new Mesh { name = "SunShadow", hideFlags = HideFlags.DontSave };
            _shadow.GetComponent<MeshFilter>().sharedMesh = _mesh;
            _renderer = _shadow.GetComponent<MeshRenderer>();
            _renderer.sharedMaterial = _material;
            _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _renderer.receiveShadows = false;
            // 투명 스프라이트들보다 먼저 (블롭 그림자 -1 보다도 먼저) = 캐릭터 · 소품 아래
            _renderer.sortingOrder = -2;
            _block = new MaterialPropertyBlock();
            return true;
        }

        private void Rebuild(Sprite sprite, bool flip, Vector2 offset)
        {
            _builtSprite = sprite;
            _builtFlip = flip;
            _builtOffset = offset;

            // 스프라이트 사각형을 pivot 기준 unit 으로 (SpriteRenderer 가 그리는 것과 같은 크기)
            Rect rect = sprite.rect;
            float ppu = sprite.pixelsPerUnit;
            float left = -sprite.pivot.x / ppu;
            float right = (rect.width - sprite.pivot.x) / ppu;
            float bottom = -sprite.pivot.y / ppu;
            float top = (rect.height - sprite.pivot.y) / ppu;
            if (flip)
            {
                (left, right) = (-right, -left);
            }

            Vector3 Ground(float x, float y) => new Vector3(x + offset.x * y, 0f, offset.y * y);

            Texture texture = sprite.texture;
            Rect uv = sprite.textureRect;
            float u0 = uv.xMin / texture.width;
            float u1 = uv.xMax / texture.width;
            float v0 = uv.yMin / texture.height;
            float v1 = uv.yMax / texture.height;
            if (flip)
            {
                (u0, u1) = (u1, u0);
            }

            _mesh.Clear();
            _mesh.SetVertices(new[] { Ground(left, bottom), Ground(left, top), Ground(right, top), Ground(right, bottom) });
            _mesh.SetUVs(0, new[] { new Vector2(u0, v0), new Vector2(u0, v1), new Vector2(u1, v1), new Vector2(u1, v0) });
            _mesh.SetTriangles(new[] { 0, 1, 2, 0, 2, 3 }, 0);
            _mesh.RecalculateBounds();

            _block.SetTexture(MainTexId, texture);
            _renderer.SetPropertyBlock(_block);
        }
    }
}

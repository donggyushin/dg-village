using UnityEngine;

namespace DgVillage
{
    /// <summary>
    /// 건물 그림(DgVillage/BuildingSprite 셰이더)에 부모 소품의 Obstacle footprint 를 넣는다 (MaterialPropertyBlock).
    /// 셰이더가 그걸로 "footprint 앞쪽 끝" 깊이를 써서 캐릭터 · 나무가 건물 앞 · 뒤에 맞게 가려진다.
    /// 맵 에디터에서 옮기면 따라가도록 위치가 바뀌면 다시 넣는다.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(SpriteRenderer))]
    public class BuildingSprite : MonoBehaviour
    {
        private static readonly int FootMin = Shader.PropertyToID("_FootMin");
        private static readonly int FootMax = Shader.PropertyToID("_FootMax");

        private MaterialPropertyBlock _block;
        private Rect _applied;

        private void OnEnable() => _applied = default;

        private void LateUpdate()
        {
            var obstacle = GetComponentInParent<Obstacle>();
            if (obstacle == null || obstacle.Footprint == _applied)
            {
                return;
            }
            _applied = obstacle.Footprint;
            var spriteRenderer = GetComponent<SpriteRenderer>();
            _block ??= new MaterialPropertyBlock();
            spriteRenderer.GetPropertyBlock(_block);
            _block.SetVector(FootMin, new Vector4(_applied.xMin, _applied.yMin, 0f, 0f));
            _block.SetVector(FootMax, new Vector4(_applied.xMax, _applied.yMax, 0f, 0f));
            spriteRenderer.SetPropertyBlock(_block);
        }
    }
}

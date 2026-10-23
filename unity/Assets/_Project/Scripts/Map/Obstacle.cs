using System.Collections.Generic;
using UnityEngine;

namespace DgVillage
{
    /// <summary>
    /// 가구가 바닥에서 차지하는 직사각형. 플레이어는 이 안으로 걸어 들어갈 수 없다 (PlayerMovement 가 확인).
    /// 스프라이트 pivot(아래 가운데) = 가구의 앞쪽 끝이라, 영역은 pivot 에서 뒤(+Z)로 Size.y 만큼 뻗는다.
    /// 선택하면 Scene 뷰에 초록 사각형으로 보인다.
    /// </summary>
    public class Obstacle : MonoBehaviour
    {
        private static readonly List<Obstacle> Active = new();

        [Tooltip("바닥에서 차지하는 크기 (x = 가로, y = 깊이) unit")]
        [SerializeField] private Vector2 _size = new(1f, 0.5f);

        // 영역의 남서 모서리가 pivot 에서 떨어진 거리 (x = X, y = Z). 기본 = 앞쪽 변 가운데가 pivot
        [SerializeField, HideInInspector] private Vector2 _cornerOffset;
        [SerializeField, HideInInspector] private bool _hasCornerOffset;

        /// <summary>런타임에 만들 때 (MapBuilder)</summary>
        public void Init(Vector2 size) => _size = size;

        /// <summary>
        /// 쿼터뷰: 화면에서 가구 그림의 맨 아래 = 남서 모서리이고, pivot(그림 아래 가운데)은
        /// 그 모서리에서 화면 가로로 영역 너비의 반쯤 떨어져 있다 → 남서 모서리를 화면 가로 방향으로 되돌려 찾는다.
        /// </summary>
        public void InitQuarterView(Vector2 size)
        {
            _size = size;
            Vector3 right = Quaternion.Euler(0f, ViewAngle.Yaw, 0f) * Vector3.right;
            // 영역 네 모서리의 화면 가로 위치는 남서 = 0 · 남동 = size.x·right.x · 북서 = size.y·right.z → 가운데
            float center = (size.x * right.x + size.y * right.z) / 2f;
            Vector3 corner = -right * center;
            _cornerOffset = new Vector2(corner.x, corner.z);
            _hasCornerOffset = true;
        }

        /// <summary>남서 모서리를 직접 (pivot 기준 월드 X · Z). 건물 그림처럼 pivot 이 영역 가운데가 아닐 때</summary>
        public void InitCorner(Vector2 size, Vector2 cornerOffset)
        {
            _size = size;
            _cornerOffset = cornerOffset;
            _hasCornerOffset = true;
        }

        private void OnEnable() => Active.Add(this);

        private void OnDisable() => Active.Remove(this);

        /// <summary>발 위치를 중심으로 반지름 radius 의 사각형이 켜져 있는 가구 하나라도 겹치면 true.</summary>
        public static bool Blocks(Vector3 position, float radius)
        {
            foreach (Obstacle obstacle in Active)
            {
                Rect footprint = obstacle.Footprint;
                if (position.x + radius > footprint.xMin && position.x - radius < footprint.xMax
                    && position.z + radius > footprint.yMin && position.z - radius < footprint.yMax)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>바닥에서 막는 직사각형. x = 월드 X, y = 월드 Z</summary>
        public Rect Footprint
        {
            get
            {
                Vector3 p = transform.position;
                Vector2 corner = _hasCornerOffset ? _cornerOffset : new Vector2(-_size.x / 2f, 0f);
                return new Rect(p.x + corner.x, p.z + corner.y, _size.x, _size.y);
            }
        }

        private void OnDrawGizmosSelected()
        {
            Rect r = Footprint;
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(new Vector3(r.center.x, 0.01f, r.center.y), new Vector3(r.width, 0f, r.height));
        }
    }
}

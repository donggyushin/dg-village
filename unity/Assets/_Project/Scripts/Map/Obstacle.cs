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

        // x = 월드 X, y = 월드 Z
        private Rect Footprint
        {
            get
            {
                Vector3 p = transform.position;
                return new Rect(p.x - _size.x / 2f, p.z, _size.x, _size.y);
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

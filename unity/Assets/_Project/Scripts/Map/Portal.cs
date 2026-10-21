using System.Collections.Generic;
using UnityEngine;

namespace DgVillage
{
    /// <summary>
    /// 다른 맵으로 가는 바닥 영역. 맵 JSON 의 portals 를 씬 빌더가 깔아 둔다.
    /// 위치 = 영역 가운데, Size = 가로 × 깊이. 플레이어 발이 이 안에 들어오면 PortalTraveler 가
    /// To 맵의 씬을 열고 Spawn 에서 시작하게 한다. 선택하면 Scene 뷰에 하늘색 사각형으로 보인다.
    /// </summary>
    public class Portal : MonoBehaviour
    {
        private static readonly List<Portal> Active = new();

        [Tooltip("갈 맵 id (서버 maps/<id>.json)")]
        [SerializeField] private string _to;

        [Tooltip("바닥에서 차지하는 크기 (x = 가로, y = 깊이) unit")]
        [SerializeField] private Vector2 _size = Vector2.one;

        [Tooltip("도착하는 맵에서 시작할 발 위치 (x, z)")]
        [SerializeField] private Vector2 _spawn;

        public string To => _to;
        public Vector3 Spawn => new(_spawn.x, 0f, _spawn.y);

        private void OnEnable() => Active.Add(this);

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

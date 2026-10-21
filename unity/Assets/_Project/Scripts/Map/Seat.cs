using System.Collections.Generic;
using UnityEngine;

namespace DgVillage
{
    /// <summary>
    /// 앉을 수 있는 자리. 맵 JSON 의 seats 를 씬 빌더가 깔아 둔다 (id 가 서버와 같아야 한다).
    /// 위치 = 앉았을 때 캐릭터 발 위치. desk = 집중 모드 자리, lounge = 그냥 앉기.
    /// </summary>
    public class Seat : MonoBehaviour
    {
        private static readonly List<Seat> Active = new();

        [SerializeField] private string _id;
        [SerializeField] private string _kind = "desk";

        public string Id => _id;
        public bool IsDesk => _kind == "desk";

        /// <summary>런타임에 만들 때 (MapBuilder)</summary>
        public void Init(string id, string kind)
        {
            _id = id;
            _kind = kind;
        }

        private void OnEnable() => Active.Add(this);

        private void OnDisable() => Active.Remove(this);

        /// <summary>position 에서 range 안의 가장 가까운 자리. 없으면 null</summary>
        public static Seat Nearest(Vector3 position, float range)
        {
            Seat nearest = null;
            float best = range * range;
            foreach (Seat seat in Active)
            {
                Vector3 d = seat.transform.position - position;
                float sqr = d.x * d.x + d.z * d.z;
                if (sqr <= best)
                {
                    best = sqr;
                    nearest = seat;
                }
            }
            return nearest;
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = IsDesk ? new Color(0.4f, 0.7f, 1f) : new Color(1f, 0.6f, 0.4f);
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.05f, 0.2f);
        }
    }
}

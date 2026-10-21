using System;

namespace DgVillage
{
    /// <summary>
    /// 서버와 같이 쓰는 맵 데이터 (python/src/dg_village_server/maps/{id}.json) 의 JsonUtility DTO.
    /// 원본은 서버 쪽 JSON 하나: 서버는 채팅 구역(휴게실 · 복도 반경)에, Unity 는 씬 빌더에서 방을 만들 때 쓴다.
    /// 필드 이름은 JSON 그대로 (camelCase — 명명 규칙 예외, NetworkMessages 와 같은 이유).
    /// </summary>
    [Serializable]
    public class GameMapData
    {
        public string id;
        public float hearingRadius;
        public string[] layout;
        public Seat[] seats;
        public Portal[] portals;
        public Prop[] props;

        [Serializable]
        public class Seat
        {
            public string id;
            public string kind; // "desk" | "lounge"
            public float x;
            public float z;
        }

        [Serializable]
        public class Portal
        {
            public float x; // 영역 가운데
            public float z;
            public float w; // 가로
            public float d; // 깊이
            public string to; // 갈 맵 id
            public float spawnX; // 도착 맵에서 시작할 발 위치
            public float spawnZ;
        }

        [Serializable]
        public class Prop
        {
            public string sprite;
            public float x;
            public float z;
            public int shadow; // 그림자 배율, 0 = 없음
        }
    }
}

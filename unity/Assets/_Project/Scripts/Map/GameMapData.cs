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
        public class Prop
        {
            public string sprite;
            public float x;
            public float z;
            public int shadow; // 그림자 배율, 0 = 없음
        }
    }
}

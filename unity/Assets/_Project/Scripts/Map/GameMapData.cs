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
        public string[] layout;
        public Seat[] seats;
        public Portal[] portals;
        public Prop[] props;
        public Lighting lighting;
        public Light[] lights;

        [Serializable]
        public class Seat
        {
            public string id;
            public string kind; // "desk" | "lounge"
            public float x;
            public float z;
            public string facing; // 앉으면 보는 방향 (의자 방향) down|up|left|right, "" = down
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
            public bool flip; // 좌우 반전 (오른쪽 보는 가구 그림으로 왼쪽 보는 가구)
        }

        /// <summary>
        /// 맵 분위기 (MapLighting). JsonUtility 는 없는 객체도 기본값으로 채우므로 모든 값은 0 · "" = 효과 없음.
        /// </summary>
        [Serializable]
        public class Lighting
        {
            public float shadowX; // 해 그림자: 높이 1 unit 당 바닥에서 밀리는 거리 (x, z). 둘 다 0 = 해 그림자 없음
            public float shadowZ;
            public float shadowStrength; // 0..1, 그림자 색을 얼마나 곱할지
            public string shadowColor; // 해 · 구름 그림자 색 (#rrggbb)
            public float clouds; // 구름 그림자 진하기 0..1
            public float exposure; // 후처리 (URP Volume): 밝기 EV
            public float contrast; // -100..100
            public float saturation; // -100..100
            public float temperature; // -100..100, + = 따뜻하게
            public string colorFilter; // 화면 전체에 곱할 색 (#rrggbb)
            public float vignette; // 0..1, 화면 가장자리 어둡게
            public string ambient; // 빛이 안 닿는 곳에 곱할 색 (#rrggbb). "" = 점광원 조명 끔 (밝은 그대로)
        }

        /// <summary>점광원 (촛불 · 벽난로). lighting.ambient 가 있을 때만 켜진다</summary>
        [Serializable]
        public class Light
        {
            public float x;
            public float z;
            public float radius; // 빛이 닿는 거리 (unit)
            public float intensity; // 가운데 밝기 (ambient 위에 더한다)
            public string color; // #rrggbb
            public float flicker; // 0..1, 촛불처럼 흔들리는 정도
            public float glow; // 불꽃 빛무리 지름 (unit), 0 = 없음
            public float glowY; // 빛무리 높이 = 불꽃이 그림에서 바닥 위로 얼마나 (unit)
        }
    }
}

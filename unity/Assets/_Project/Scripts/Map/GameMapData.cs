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
        public string view; // 카메라 각도: "quarter" = 쿼터뷰 (X 30° · Y 45°), "" = 정면 (ViewAngle.SetForMap)
        public string[] layout;
        public bool noWalls; // true = 바닥 끝에 벽 · 벽 윗면을 안 만든다 (야외)
        public Seat[] seats;
        public Portal[] portals;
        public Pet[] pets; // 혼자 돌아다니는 동물 (서버 pets.py 가 움직인다)
        public Prop[] props;
        public Window[] windows;
        public Lighting lighting; // 낮 (night 가 없으면 하루 종일)
        public Lighting night; // 밤 (DayCycle — 실제 한국 시각). 값이 전부 기본값 = 낮 · 밤 구분 없음
        public Lighting dusk; // 저녁노을 (DayCycle.Dusk 만큼 낮 · 밤 위에 섞는다). 값이 전부 기본값 = 노을 없음
        public Light[] lights;

        [Serializable]
        public class Seat
        {
            public string id;
            public string kind; // "desk" | "lounge"
            public float x;
            public float z;
            public string facing; // 앉으면 보는 방향 (의자 방향, 화면 기준 — 쿼터뷰는 대각선) FacingWire 값, "" = down
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

        /// <summary>맵을 돌아다니는 동물. 서버가 집(x, z) 둘레 roam 안에서 걷게 하고 state.pets 로 보낸다</summary>
        [Serializable]
        public class Pet
        {
            public string id;
            public string character; // Characters/Pets 의 CharacterDefinition id (PetRegistry)
            public float x; // 집 = 처음 서 있는 곳
            public float z;
            public float roam; // 집에서 이만큼 (unit) 안에서만 걷는다
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

        /// <summary>벽 창문 (FloorMesh.SetWindows). 가운데 점이 벽 선 위 칸 경계에 있어야 한다, 폭 = 2 칸</summary>
        [Serializable]
        public class Window
        {
            public float x;
            public float z;
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
            public string sky; // 창문 유리(바깥 하늘)에 곱할 색 (#rrggbb). "" = 그대로 (밤엔 어두운 남색)
            public float shafts; // 창문으로 비스듬히 들어오는 빛줄기 세기 0..1, 0 = 없음
            public string shaftColor; // 빛줄기 색 (#rrggbb)
            public float shaftReach; // 빛줄기가 1 unit 내려올 때 방 안쪽으로 나아가는 거리 (해가 낮을수록 길게)
            public float shaftSlant; // 같은 때 벽을 따라 옆으로 (+ = 창문 쪽에서 볼 때 오른쪽)
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
            public string when; // "day" = 낮에만 (창문 햇빛), "night" = 밤에만 (달빛 · 창문 불빛), "" = 언제나
        }
    }
}

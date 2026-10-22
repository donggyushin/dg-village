using UnityEngine;

namespace DgVillage
{
    /// <summary>
    /// 서버 welcome 에 실려 온 맵으로 방을 깐다 (채널 입장 · 포탈 이동 둘 다 welcome 이 온다).
    /// 씬에 미리 깔려 있던 방(에디터 미리보기)이나 이전 맵을 지우고 MapBuilder 로 새로 만든 뒤,
    /// 내 캐릭터를 서버가 정한 위치(포탈이면 도착 지점)로 옮기고 카메라를 바로 붙인다.
    /// 그래서 맵 배치를 바꿀 땐 서버 JSON 만 배포하면 된다 (새 그림은 PropCatalog = 클라이언트 업데이트).
    /// 첫 welcome 전에는 내 캐릭터를 묶어 둔다: 미리보기 방에서 걸어 다녀도 welcome 이 오면 서버 위치로 순간이동하니까.
    /// </summary>
    public class MapLoader : MonoBehaviour
    {
        [SerializeField] private NetworkClient _client;
        [SerializeField] private PropCatalog _catalog;
        [SerializeField] private PlayerMovement _localPlayer;
        [SerializeField] private CameraFollow _camera;

        [Tooltip("씬에 미리 깔린 방 (에디터 미리보기). 첫 welcome 때 서버 맵으로 바뀐다")]
        [SerializeField] private GameObject _room;

        /// <summary>지금 깔린 맵 id. 아직 서버 맵을 못 받았으면 null</summary>
        public string CurrentMapId { get; private set; }

        private void OnEnable()
        {
            if (_client != null)
            {
                _client.Welcomed += OnWelcomed;
            }
            if (_localPlayer != null)
            {
                _localPlayer.WaitingForServer = true;
            }
        }

        private void OnDisable()
        {
            if (_client != null)
            {
                _client.Welcomed -= OnWelcomed;
            }
        }

        private void OnWelcomed(WelcomeMessage welcome)
        {
            _localPlayer.WaitingForServer = false;
            // 옛 서버는 map 을 안 보낸다 (JsonUtility 는 빈 객체를 만든다) → 씬에 깔린 방을 그대로 쓴다
            if (welcome.map == null || string.IsNullOrEmpty(welcome.map.id) || _catalog == null)
            {
                return;
            }
            if (_room != null)
            {
                // Destroy 는 프레임 끝에 지운다. 그 전에 꺼서 Obstacle · Seat · Portal 목록에서 바로 빠지게
                _room.SetActive(false);
                Destroy(_room);
            }
            // 각도를 먼저: 바닥 · 벽 · 소품이 만들어질 때 이 각도를 본다
            ViewAngle.SetForMap(welcome.map.view);
            if (_camera != null)
            {
                _camera.SetRotation(ViewAngle.Billboard);
            }
            _room = MapBuilder.Build(welcome.map, _catalog);
            CurrentMapId = welcome.map.id;

            foreach (PlayerState state in welcome.players)
            {
                if (state.id == welcome.id)
                {
                    _localPlayer.transform.position = new Vector3(state.x, 0f, state.z);
                }
            }
            if (_camera != null)
            {
                _camera.Snap();
            }
        }
    }
}

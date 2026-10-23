using UnityEngine;
using UnityEngine.InputSystem;

namespace DgVillage
{
    /// <summary>
    /// 포탈 위에서 E 를 누르면 서버에 travel 을 보낸다. Network 에 붙는다 (마을 맵 메뉴가 붙인다).
    /// 서버 답(새 맵의 welcome)이 오면 MapLoader 가 방을 바꾸고 캐릭터를 도착 지점으로 옮긴다.
    /// 안내 문구("E 정원으로 가기")는 SeatInteraction 이 보여준다 (화면 아래 문구를 한 곳에서 쓰도록).
    /// 서버가 맵을 보내준 뒤에만 동작한다: 맵을 안 보내는 옛 서버는 travel 도 모른다 (씬의 미리보기 포탈은 무시).
    /// </summary>
    public class PortalTraveler : MonoBehaviour
    {
        [SerializeField] private NetworkClient _client;
        [SerializeField] private PlayerMovement _localPlayer;
        [SerializeField] private MapLoader _mapLoader;

        // 보낸 travel 에 답이 없으면 (끊김 · 거절) 이 시간 뒤 다시 누를 수 있게
        private const float RetrySeconds = 1f;

        private float _sentAt = float.NegativeInfinity;

        private void OnEnable()
        {
            if (_client != null)
            {
                _client.Welcomed += OnWelcomed;
            }
        }

        private void OnDisable()
        {
            if (_client != null)
            {
                _client.Welcomed -= OnWelcomed;
            }
        }

        private void OnWelcomed(WelcomeMessage welcome) => _sentAt = float.NegativeInfinity;

        private void Update()
        {
            if (_client == null || _localPlayer == null || _client.LocalPlayerId == null
                || _mapLoader == null || _mapLoader.CurrentMapId == null)
            {
                return;
            }
            Keyboard keyboard = Keyboard.current;
            // 채팅 입력 중에는 E 가 글자다
            if (keyboard == null || !keyboard.eKey.wasPressedThisFrame || _localPlayer.InputBlocked
                || _localPlayer.Seated || Time.time - _sentAt < RetrySeconds)
            {
                return;
            }
            Portal portal = Portal.At(_localPlayer.transform.position);
            if (portal != null)
            {
                _sentAt = Time.time;
                _client.SendTravel(portal.To);
            }
        }
    }
}

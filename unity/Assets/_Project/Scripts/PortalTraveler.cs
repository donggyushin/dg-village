using UnityEngine;

namespace DgVillage
{
    /// <summary>
    /// 내 캐릭터가 포탈 영역에 들어가면 서버에 travel 을 보낸다. Network 에 붙는다 (마을 맵 메뉴가 붙인다).
    /// 서버 답(새 맵의 welcome)이 오면 MapLoader 가 방을 바꾸고 캐릭터를 도착 지점으로 옮긴다.
    /// 답을 기다리는 동안은 다시 보내지 않는다 (서버가 거절하면 포탈 밖으로 나갔다 와야 다시 시도).
    /// 서버가 맵을 보내준 뒤에만 동작한다: 맵을 안 보내는 옛 서버는 travel 도 모른다 (씬의 미리보기 포탈은 무시).
    /// </summary>
    public class PortalTraveler : MonoBehaviour
    {
        [SerializeField] private NetworkClient _client;
        [SerializeField] private PlayerMovement _localPlayer;
        [SerializeField] private MapLoader _mapLoader;

        private Portal _sentFor;

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

        private void OnWelcomed(WelcomeMessage welcome) => _sentFor = null;

        private void Update()
        {
            if (_client == null || _localPlayer == null || _client.LocalPlayerId == null
                || _mapLoader == null || _mapLoader.CurrentMapId == null)
            {
                return;
            }
            Portal portal = Portal.At(_localPlayer.transform.position);
            if (portal == null)
            {
                _sentFor = null;
                return;
            }
            if (portal != _sentFor)
            {
                _sentFor = portal;
                _client.SendTravel(portal.To);
            }
        }
    }
}

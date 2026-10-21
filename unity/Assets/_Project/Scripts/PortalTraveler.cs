using UnityEngine;

namespace DgVillage
{
    /// <summary>
    /// 내 캐릭터가 포탈 영역에 들어가면 그 포탈의 맵으로 옮긴다 (MapClient.Travel). Network 에 붙는다
    /// (마을 맵 메뉴가 붙인다). 씬을 바꾸는 순간 이 오브젝트도 사라지므로 한 번만 부르면 된다.
    /// </summary>
    public class PortalTraveler : MonoBehaviour
    {
        [SerializeField] private PlayerMovement _localPlayer;

        private bool _traveling;

        private void Update()
        {
            if (_traveling || _localPlayer == null)
            {
                return;
            }
            Portal portal = Portal.At(_localPlayer.transform.position);
            if (portal != null)
            {
                _traveling = true;
                MapClient.Travel(portal.To, portal.Spawn);
            }
        }
    }
}

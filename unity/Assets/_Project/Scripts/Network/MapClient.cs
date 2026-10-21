using UnityEngine;
using UnityEngine.SceneManagement;

namespace DgVillage
{
    /// <summary>
    /// 채널 안의 맵 이동 (서버 maps.MAP_IDS). 맵 하나 = 씬 하나: office = SampleScene, outdoor = Outdoor.
    /// 포탈을 밟으면 Travel 이 도착 위치를 기억해 두고 그 맵의 씬을 연다. 옛 씬의 NetworkClient 는
    /// OnDestroy 에서 소켓을 닫고, 새 씬의 NetworkClient 가 도착 위치에서 그 맵으로 다시 join 한다.
    /// static 이라 씬이 바뀌어도 남는다.
    /// </summary>
    public static class MapClient
    {
        /// <summary>서버 DEFAULT_MAP_ID 와 같게 유지</summary>
        public const string DefaultMapId = "office";

        private static Vector3? _pendingSpawn;

        // Domain Reload 가 꺼져 있어 static 이 다음 Play 까지 남는다
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay() => _pendingSpawn = null;

        public static string SceneOf(string mapId) => mapId == "outdoor" ? "Outdoor" : "SampleScene";

        public static void Travel(string mapId, Vector3 spawn)
        {
            _pendingSpawn = spawn;
            SceneManager.LoadScene(SceneOf(mapId));
        }

        /// <summary>포탈로 왔으면 도착 위치를 꺼낸다 (한 번만). 채널 목록에서 바로 들어왔으면 false</summary>
        public static bool TryTakeSpawn(out Vector3 spawn)
        {
            spawn = _pendingSpawn ?? default;
            bool has = _pendingSpawn.HasValue;
            _pendingSpawn = null;
            return has;
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace DgVillage
{
    /// <summary>
    /// NetworkClient 이벤트를 듣고 다른 플레이어 캐릭터(RemotePlayer 프리팹)를 만들고 · 움직이고 · 지운다.
    /// 내 캐릭터는 씬의 Player 이므로 서버 목록에서 내 id 는 건너뛴다.
    /// </summary>
    public class RemotePlayerSpawner : MonoBehaviour
    {
        [SerializeField] private NetworkClient _client;
        [SerializeField] private RemotePlayer _remotePlayerPrefab;

        [Tooltip("서버가 보낸 characterId 로 스프라이트를 찾을 목록")]
        [SerializeField] private CharacterRegistry _characters;

        private readonly Dictionary<string, RemotePlayer> _players = new();

        private void Awake()
        {
            if (_client == null || _remotePlayerPrefab == null || _characters == null)
            {
                Debug.LogError("RemotePlayerSpawner: Client, Remote Player Prefab, Characters 중 비어 있는 칸이 있다.", this);
                enabled = false;
            }
        }

        private void OnEnable()
        {
            if (_client == null)
            {
                return;
            }
            _client.Welcomed += OnWelcomed;
            _client.PlayerJoined += Spawn;
            _client.PlayerLeft += Despawn;
            _client.StateReceived += OnStateReceived;
            _client.Disconnected += DespawnAll;
        }

        private void OnDisable()
        {
            if (_client == null)
            {
                return;
            }
            _client.Welcomed -= OnWelcomed;
            _client.PlayerJoined -= Spawn;
            _client.PlayerLeft -= Despawn;
            _client.StateReceived -= OnStateReceived;
            _client.Disconnected -= DespawnAll;
        }

        public bool TryGetPlayer(string playerId, out RemotePlayer player) =>
            _players.TryGetValue(playerId, out player);

        private void OnWelcomed(WelcomeMessage welcome)
        {
            DespawnAll();
            foreach (PlayerState state in welcome.players)
            {
                Spawn(state);
            }
        }

        private void OnStateReceived(StateMessage message)
        {
            foreach (PlayerState state in message.players)
            {
                if (_players.TryGetValue(state.id, out RemotePlayer player))
                {
                    player.ApplyState(state);
                }
                else
                {
                    // player_joined 를 놓쳤어도 state 에 있으면 만든다
                    Spawn(state);
                }
            }
        }

        private void Spawn(PlayerState state)
        {
            if (state.id == _client.LocalPlayerId || _players.ContainsKey(state.id))
            {
                return;
            }

            RemotePlayer player = Instantiate(_remotePlayerPrefab, transform);
            // 모르는 id(아직 내 빌드에 없는 캐릭터)면 레지스트리 기본 캐릭터로 대신한다
            player.Init(state, _characters.Find(state.characterId));
            _players.Add(state.id, player);
        }

        private void Despawn(string playerId)
        {
            if (_players.Remove(playerId, out RemotePlayer player))
            {
                Destroy(player.gameObject);
            }
        }

        private void DespawnAll()
        {
            foreach (RemotePlayer player in _players.Values)
            {
                Destroy(player.gameObject);
            }
            _players.Clear();
        }
    }
}

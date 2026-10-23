using System.Collections.Generic;
using UnityEngine;

namespace DgVillage
{
    /// <summary>
    /// NetworkClient 이벤트를 듣고 다른 플레이어 캐릭터(RemotePlayer 프리팹)를 만들고 · 움직이고 · 지운다.
    /// 내 캐릭터는 씬의 Player 이므로 서버 목록에서 내 id 는 건너뛴다.
    /// 맵을 돌아다니는 동물(welcome · state 의 pets)도 같은 프리팹으로 만든다 — 이름표만 숨긴다.
    /// </summary>
    public class RemotePlayerSpawner : MonoBehaviour
    {
        [SerializeField] private NetworkClient _client;
        [SerializeField] private RemotePlayer _remotePlayerPrefab;

        [Tooltip("서버가 보낸 characterId 로 스프라이트를 찾을 목록")]
        [SerializeField] private CharacterRegistry _characters;

        [Tooltip("서버가 보낸 동물 characterId 로 스프라이트를 찾을 목록 (Characters/PetRegistry)")]
        [SerializeField] private CharacterRegistry _pets;

        private readonly Dictionary<string, RemotePlayer> _players = new();
        private readonly Dictionary<string, RemotePlayer> _petObjects = new();

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
            ApplyPets(welcome.pets);
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
            ApplyPets(message.pets);
        }

        // 서버 목록에 없는 동물은 지운다 (맵을 옮기면 welcome 에서 통째로 바뀐다)
        private void ApplyPets(PlayerState[] pets)
        {
            pets ??= System.Array.Empty<PlayerState>();
            var seen = new HashSet<string>();
            foreach (PlayerState state in pets)
            {
                seen.Add(state.id);
                if (_petObjects.TryGetValue(state.id, out RemotePlayer pet))
                {
                    pet.ApplyState(state);
                    continue;
                }
                CharacterDefinition character = _pets != null ? _pets.Find(state.characterId) : null;
                if (character == null || character.Id != state.characterId)
                {
                    continue; // 이 빌드에 없는 동물은 안 그린다 (기본 캐릭터로 대신하면 사람처럼 보인다)
                }
                pet = Instantiate(_remotePlayerPrefab, transform);
                pet.Init(state, character);
                pet.HideNameTag();
                pet.gameObject.name = $"Pet {state.id}";
                _petObjects.Add(state.id, pet);
            }
            foreach (string id in new List<string>(_petObjects.Keys))
            {
                if (!seen.Contains(id) && _petObjects.Remove(id, out RemotePlayer gone))
                {
                    Destroy(gone.gameObject);
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
            foreach (RemotePlayer pet in _petObjects.Values)
            {
                Destroy(pet.gameObject);
            }
            _petObjects.Clear();
        }
    }
}

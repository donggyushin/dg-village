using System.Collections.Generic;
using UnityEngine;

public class RemotePlayerSpawner : MonoBehaviour
{
    [SerializeField] private RemotePlayer _remotePlayerPrefab;
    [SerializeField] private CharacterCatalog _catalog;

    private readonly Dictionary<int, RemotePlayer> _spawned = new();

    private GameSocket _socket => GameSocket.Instance;

    void Start()
    {
        if (_socket == null) return;

        foreach (var info in _socket.RemotePlayers.Values)
        {
            Spawn(info);
        }

        _socket.PlayerJoined += Spawn;
        _socket.PlayerMoved += Move;
        _socket.PlayerLeft += Despawn;
    }

    void OnDestroy()
    {
        if (_socket == null) return;

        _socket.PlayerJoined -= Spawn;
        _socket.PlayerMoved -= Move;
        _socket.PlayerLeft -= Despawn;
    }

    private void Spawn(GameSocket.RemotePlayerInfo info)
    {
        if (_spawned.ContainsKey(info.id)) return;

        RemotePlayer player = Instantiate(_remotePlayerPrefab, info.Position, Quaternion.identity);
        player.name = $"Remote_{info.nickname}";
        CharacterSpriteSet spriteSet = _catalog.Find(info.sprite_set_id);
        player.GetComponentInChildren<CharacterSpriteView>().SetSpriteSet(spriteSet);
        _spawned[info.id] = player;
    }

    private void Move(GameSocket.RemotePlayerInfo info)
    {
        _spawned.TryGetValue(info.id, out var player);
        if (player == null) return;

        player.SetTarget(info.Position);
    }

    private void Despawn(int id)
    {
        _spawned.TryGetValue(id, out var player);
        if (player == null) return;

        Destroy(player.gameObject);
        _spawned.Remove(id);
    }
}
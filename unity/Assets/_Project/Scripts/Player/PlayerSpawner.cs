using Unity.Cinemachine;
using UnityEngine;

public class PlayerSpawner : MonoBehaviour
{
    [SerializeField] private GameObject _playerPrefab;
    [SerializeField] private CharacterCatalog _catalog;
    [SerializeField] private CinemachineCamera _camera;

    private GameSocket _socket => GameSocket.Instance;

    void Start()
    {
        _socket.Welcomed += Spawn;
    }

    private void Spawn()
    {
        Vector3 spawnPosition = new Vector3(0f, 0f, 0f);
        GameObject player = Instantiate(_playerPrefab, spawnPosition, Quaternion.identity);
        CharacterSpriteSet spriteSet = _catalog.Find(_socket.MyCharacter.sprite_set_id);
        player.GetComponentInChildren<CharacterSpriteView>().SetSpriteSet(spriteSet);
        _camera.Target.TrackingTarget = player.transform;
        _camera.PreviousStateIsValid = false;
    }
}

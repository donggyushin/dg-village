using Unity.Cinemachine;
using UnityEngine;

public class PlayerSpawner : MonoBehaviour
{
    [SerializeField] private GameObject _playerPrefab;
    [SerializeField] private CharacterCatalog _catalog;
    [SerializeField] private string _characterId;
    [SerializeField] private CinemachineCamera _camera;

    void Start()
    {
        Vector3 spawnPosition = new Vector3(0f, 0f, 0f);
        GameObject player = Instantiate(_playerPrefab, spawnPosition, Quaternion.identity);
        CharacterSpriteSet spriteSet = _catalog.Find(_characterId);
        player.GetComponentInChildren<CharacterSpriteView>().SetSpriteSet(spriteSet);
        _camera.Target.TrackingTarget = player.transform;
        _camera.PreviousStateIsValid = false;
    }

}

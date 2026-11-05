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
        // 개발용 임시코드 START
        // #if DEBUG
        //         Vector3 spawnPosition = new Vector3(0f, 0f, 0f);
        //         GameObject player = Instantiate(_playerPrefab, spawnPosition, Quaternion.identity);
        //         CharacterSpriteSet spriteSet = _catalog.Find("lavender_girl");
        //         player.GetComponentInChildren<CharacterSpriteView>().SetSpriteSet(spriteSet);
        //         _camera.Target.TrackingTarget = player.transform;
        //         _camera.PreviousStateIsValid = false;
        // #endif
        // 개발용 임시코드 END

        var me = _socket.MyCharacter;

        if (me != null)
        {
            Spawn();
        }
    }

    private void Spawn()
    {
        var x = _socket.MyCharacter.last_x;
        var y = _socket.MyCharacter.last_y;
        var z = _socket.MyCharacter.last_z;
        Vector3 spawnPosition = new Vector3(x, y, z);
        GameObject player = Instantiate(_playerPrefab, spawnPosition, Quaternion.identity);
        CharacterSpriteSet spriteSet = _catalog.Find(_socket.MyCharacter.sprite_set_id);
        player.GetComponentInChildren<CharacterSpriteView>().SetSpriteSet(spriteSet);
        _camera.Target.TrackingTarget = player.transform;
        _camera.PreviousStateIsValid = false;
        player.AddComponent<PlayerPositionSender>();

        string nickname = _socket.MyCharacter.nickname;
        player.GetComponentInChildren<NicknameLabel>().SetName(nickname);

    }
}

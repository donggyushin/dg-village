using Unity.Cinemachine;
using UnityEngine;

public class PlayerSpawner : MonoBehaviour
{
    [SerializeField] private GameObject _playerPrefab;
    [SerializeField] private CharacterCatalog _catalog;
    [SerializeField] private CinemachineCamera _camera;

    private GameSocket _socket => GameSocket.Instance;

    private ChatBubble _chatBubble;

    void Start()
    {
        var me = _socket.MyCharacter;

        if (me != null)
        {
            Spawn();
        }

        _socket.ChatReceived += ShowChat;
    }

    void OnDestroy()
    {
        if (_socket == null) return;
        _socket.ChatReceived -= ShowChat;
    }

    private void Spawn()
    {
        var x = _socket.MyCharacter.last_x;
        var y = _socket.MyCharacter.last_y;
        var z = _socket.MyCharacter.last_z;
        Vector3 spawnPosition = new Vector3(x, y, z);
        var squarePlacement = FindAnyObjectByType<SquareSpawnPlacement>();
        if (squarePlacement != null)
            spawnPosition = squarePlacement.Resolve(spawnPosition);
        GameObject player = Instantiate(_playerPrefab, spawnPosition, Quaternion.identity);
        CharacterSpriteSet spriteSet = _catalog.Find(_socket.MyCharacter.sprite_set_id);
        player.GetComponentInChildren<CharacterSpriteView>().SetSpriteSet(spriteSet);
        _camera.Target.TrackingTarget = player.transform;
        _camera.PreviousStateIsValid = false;
        player.AddComponent<PlayerPositionSender>();

        string nickname = _socket.MyCharacter.nickname;
        player.GetComponentInChildren<NicknameLabel>().SetName(nickname);

        _chatBubble = player.GetComponentInChildren<ChatBubble>();
    }

    private void ShowChat(GameSocket.ChatInfo chat)
    {
        if (_chatBubble == null) return;
        if (chat.id != _socket.MyCharacter.id) return;

        _chatBubble.Show(chat.text);
    }
}

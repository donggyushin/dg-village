using System;
using System.Collections.Generic;
using NativeWebSocket;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

public partial class GameSocket : MonoBehaviour
{
    public static GameSocket Instance { get; private set; }

    private readonly Dictionary<int, RemotePlayerInfo> _remotePlayers = new();
    public IReadOnlyDictionary<int, RemotePlayerInfo> RemotePlayers => _remotePlayers;

    public event Action<RemotePlayerInfo> PlayerJoined;
    public event Action<RemotePlayerInfo> PlayerMoved;
    public event Action<int> PlayerLeft; // id

    public CharacterInfo MyCharacter { get; private set; }

    private WebSocket _webSocket;

    public event Action Welcomed;
    public event Action<string> JoinFailed; // error code

    private void Awake()
    {

        if (GameSocket.Instance != null)
        {
            Destroy(gameObject);
            return;
        }


        GameSocket.Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (GameSocket.Instance == this)
        {
            GameSocket.Instance = null;
        }

        if (_webSocket != null)
        {
            _ = _webSocket.Close();
        }
    }

    public async void Connect()
    {
        _webSocket = new WebSocket(ServerConfig.WebSocketUrl);

        _webSocket.OnOpen += SendJoin;
        _webSocket.OnMessage += BridgeBytesToHandleMessage;
        _webSocket.OnError += HandleErrorMessage;
        _webSocket.OnClose += HandleSocketClose;

        await _webSocket.Connect();
    }

    void Update()
    {
#if !UNITY_WEBGL || UNITY_EDITOR
        // 메시지는 백그라운드 스레드에서 받기 때문에 메인 스레드로 옮겨줘야함.
        _webSocket?.DispatchMessageQueue();
#endif
    }

    private void HandleSocketClose(WebSocketCloseCode code)
    {
        Debug.Log($"Close: {code}");
    }

    private void HandleErrorMessage(string message)
    {
        Debug.LogWarning($"error: {message}");
    }

    private async void SendJoin()
    {
        var join = new JoinMessage
        {
            account_id = Session.AccountId,
            character_id = Session.CharacterId
        };

        await _webSocket.SendText(JsonConvert.SerializeObject(join));
    }

    public async void ChangeScene(string scene)
    {
        var changeScene = new ChangeSceneMessage
        {
            scene = scene
        };

        var message = JsonConvert.SerializeObject(changeScene);

        await _webSocket.SendText(message);
    }

    public async void SendMove(Vector3 position)
    {
        var move = new MoveMessage
        {
            x = position.x,
            y = position.y,
            z = position.z
        };

        var message = JsonConvert.SerializeObject(move);

        await _webSocket.SendText(message);
    }

    private void BridgeBytesToHandleMessage(Byte[] bytes)
    {
        var message = System.Text.Encoding.UTF8.GetString(bytes);
        HandleMessage(message);
    }

    public async void SendChat(string text)
    {
        var chat = new ChatMessage
        {
            text = text
        };

        var message = JsonConvert.SerializeObject(chat);

        await _webSocket.SendText(message);
    }

    private void HandleMessage(string json)
    {
        JObject message = JObject.Parse(json);

        string type = (string)message["type"];

        switch (type)
        {
            case "welcome":
                WelcomeMessage welcomeMessage = message.ToObject<WelcomeMessage>();
                Debug.Log($"id is {welcomeMessage.character.id}");
                Debug.Log($"nickname is {welcomeMessage.character.nickname}");
                MyCharacter = welcomeMessage.character;
                Welcomed?.Invoke();
                break;
            case "scene_changed":
                var scene = (string)message["scene"];
                MyCharacter.last_visited_scene = scene;
                SceneManager.LoadSceneAsync(scene);
                break;
            case "error":
                ErrorMessage errorMessage = message.ToObject<ErrorMessage>();
                Debug.LogWarning($"code is {errorMessage.code}, message is {errorMessage.message}");
                JoinFailed?.Invoke(errorMessage.code);
                break;
            case "scene_players":
                var scenePlayers = message.ToObject<ScenePlayersMessage>();
                _remotePlayers.Clear();
                foreach (var p in scenePlayers.players)
                {
                    _remotePlayers[p.id] = p;
                }
                break;
            case "player_joined":
                var joined = message.ToObject<PlayerJoinedMessage>().player;
                _remotePlayers[joined.id] = joined;
                PlayerJoined?.Invoke(joined);
                break;
            case "player_moved":
                var moved = message.ToObject<PlayerMovedMessage>();
                _remotePlayers.TryGetValue(moved.id, out var movedPlayer);
                if (movedPlayer == null) break;

                movedPlayer.x = moved.x;
                movedPlayer.y = moved.y;
                movedPlayer.z = moved.z;

                PlayerMoved?.Invoke(movedPlayer);
                break;
            case "player_left":
                var leftId = message.ToObject<PlayerLeftMessage>().id;
                _remotePlayers.Remove(leftId);
                PlayerLeft?.Invoke(leftId);
                break;
            default:
                Debug.LogWarning("모르는 메시지");
                break;

        }
    }
}

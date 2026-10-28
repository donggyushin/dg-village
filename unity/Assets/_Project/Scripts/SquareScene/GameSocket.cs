using System;
using NativeWebSocket;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

public class GameSocket : MonoBehaviour
{
    // 서버 CharacterOut과 필드 이름 맞춤 (snake_case)
    public class CharacterInfo
    {
        public int id;
        public int server_id;
        public string nickname;
        public string sprite_set_id;
        public DateTime created_at;
    }

    private class JoinMessage
    {
        public string type = "join";
        public string account_id;
        public int character_id;
    }

    private class WelcomeMessage
    {
        public CharacterInfo character;
    }

    private class ErrorMessage
    {
        public string code;
        public string message;
    }

    private WebSocket _webSocket;

    [SerializeField] private PlayerSpawner _playerSpawner;

    async void Start()
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

    private void BridgeBytesToHandleMessage(Byte[] bytes)
    {
        var message = System.Text.Encoding.UTF8.GetString(bytes);
        HandleMessage(message);
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
                break;
            case "error":
                ErrorMessage errorMessage = message.ToObject<ErrorMessage>();
                Debug.LogWarning($"code is {errorMessage.code}, message is {errorMessage.message}");
                break;
            default:
                Debug.LogWarning("모르는 메시지");
                break;

        }
    }
}

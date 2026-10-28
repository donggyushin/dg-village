using System;
using NativeWebSocket;
using Newtonsoft.Json;
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

    async void Start()
    {
        _webSocket = new WebSocket(ServerConfig.WebSocketUrl);

        _webSocket.OnOpen += SendJoin;
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
}

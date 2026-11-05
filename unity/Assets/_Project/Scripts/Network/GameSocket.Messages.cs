using System;
using System.Collections.Generic;
using UnityEngine;

public partial class GameSocket
{
    // 서버 CharacterOut과 필드 이름 맞춤 (snake_case)
    public class CharacterInfo
    {
        public int id;
        public int server_id;
        public string nickname;
        public string sprite_set_id;
        public DateTime created_at;
        public string last_visited_scene;
        public float last_x;
        public float last_y;
        public float last_z;
    }

    public class RemotePlayerInfo
    {
        public int id;
        public string nickname;
        public string sprite_set_id;
        public float x;
        public float y;
        public float z;

        public Vector3 Position => new Vector3(x, y, z);
    }

    public class ChatInfo
    {
        public int id; // 보낸 사람의 id
        public string nickname;
        public string text;
    }

    private class ScenePlayersMessage
    {
        public List<RemotePlayerInfo> players;
    }

    // NEW
    private class PlayerJoinedMessage
    {
        public RemotePlayerInfo player;
    }

    // NEW
    private class PlayerLeftMessage
    {
        public int id;
    }

    // NEW
    private class PlayerMovedMessage
    {
        public int id;
        public float x;
        public float y;
        public float z;
    }

    private class JoinMessage
    {
        public string type = "join";
        public string account_id;
        public int character_id;
    }

    private class ChangeSceneMessage
    {
        public string type = "change_scene";
        public string scene;
        public float x;
        public float y;
        public float z;
    }

    private class MoveMessage
    {
        public string type = "move";
        public float x;
        public float y;
        public float z;
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

    private class ChatMessage
    {
        public string type = "chat";
        public string text;
    }
}
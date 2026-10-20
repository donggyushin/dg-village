using System;

namespace DgVillage
{
    // 서버 python/src/dg_village_server/protocol.py 와 1:1 대응하는 메시지 형식.
    // JsonUtility 는 필드 이름이 JSON 키와 똑같아야 해서, 여기만 예외로 camelCase public 필드를 쓴다.

    /// <summary>모든 메시지 공통. 먼저 type 만 읽어서 어떤 클래스로 다시 파싱할지 정한다.</summary>
    [Serializable]
    public class MessageHeader
    {
        public string type;
    }

    // ----- client -> server -----

    [Serializable]
    public class JoinMessage
    {
        public string type = "join";
        public string room;
        public string name;
        public string characterId;
    }

    [Serializable]
    public class MoveMessage
    {
        public string type = "move";
        public float x;
        public float z;
        public string facing;
        public bool moving;
    }

    // ----- server -> client -----

    [Serializable]
    public class PlayerState
    {
        public string id;
        public string name;
        public string characterId;
        public float x;
        public float z;
        public string facing;
        public bool moving;
    }

    [Serializable]
    public class WelcomeMessage
    {
        public string id;
        public PlayerState[] players;
    }

    [Serializable]
    public class PlayerJoinedMessage
    {
        public PlayerState player;
    }

    [Serializable]
    public class PlayerLeftMessage
    {
        public string id;
    }

    [Serializable]
    public class StateMessage
    {
        public PlayerState[] players;
    }

    [Serializable]
    public class ErrorMessage
    {
        public string message;
    }

    /// <summary>Facing enum ↔ 서버 문자열("down" 등) 변환.</summary>
    public static class FacingWire
    {
        public static string ToWire(Facing facing) => facing switch
        {
            Facing.Up => "up",
            Facing.Left => "left",
            Facing.Right => "right",
            _ => "down",
        };

        public static Facing Parse(string value) => value switch
        {
            "up" => Facing.Up,
            "left" => Facing.Left,
            "right" => Facing.Right,
            _ => Facing.Down,
        };
    }
}

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
        public string token;
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

    [Serializable]
    public class ChatMessage
    {
        public string type = "chat";
        public string text;
    }

    /// <summary>좌석(맵 JSON 의 seats, 씬의 Seat)에 앉겠다는 요청. 서버가 거리 · 빈자리를 확인하고 state 로 답한다.</summary>
    [Serializable]
    public class SitMessage
    {
        public string type = "sit";
        public string seat;
    }

    [Serializable]
    public class StandMessage
    {
        public string type = "stand";
    }

    [Serializable]
    public class TravelMessage
    {
        public string type = "travel";
        public string to;
    }

    // ----- server -> client -----

    /// <summary>방 안 누군가(나 포함)의 채팅 한 줄. 서버가 공백 정리 · 길이 검사를 마친 텍스트.</summary>
    [Serializable]
    public class ChatBroadcastMessage
    {
        public string id;
        public string name;
        public string text;
    }

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
        /// <summary>앉아 있는 좌석 id. 서 있으면 null 또는 "" (JsonUtility)</summary>
        public string seat;
        /// <summary>책상에 앉아 집중 모드: 채팅을 듣지도 보내지도 못한다</summary>
        public bool focused;

        public bool IsSeated => !string.IsNullOrEmpty(seat);
    }

    [Serializable]
    public class WelcomeMessage
    {
        public string id;
        public PlayerState[] players;
        /// <summary>서버의 maps/{id}.json 그대로. 옛 서버는 안 보낸다 (그러면 id 가 빈 객체)</summary>
        public GameMapData map;
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

    // ----- HTTP (python/src/dg_village_server/api.py) -----

    [Serializable]
    public class SignupResponse
    {
        public string userId;
        public string credential;
        public string accessToken;
    }

    [Serializable]
    public class LoginRequest
    {
        public string provider;
        public string credential;
    }

    [Serializable]
    public class LoginResponse
    {
        public string userId;
        public string accessToken;
    }

    [Serializable]
    public class Profile
    {
        public string id;
        public string displayName;
        public string characterId;
    }

    /// <summary>JsonUtility 는 null 을 "" 로 보내므로 두 칸 모두 채워서 보낸다.</summary>
    [Serializable]
    public class ProfileUpdate
    {
        public string displayName;
        public string characterId;
    }

    /// <summary>GET /channels 의 한 줄. id(32자리 hex)를 WebSocket join 의 room 으로 보낸다.</summary>
    [Serializable]
    public class ChannelInfo
    {
        public string id;
        public string name;
        /// <summary>만든 사람 계정 id. 기본 채널(광장)은 서버가 null 을 보내는데 JsonUtility 가 "" 로 읽는다.</summary>
        public string ownerId;
        public int playerCount;
    }

    /// <summary>JsonUtility 는 맨 바깥이 배열인 JSON 을 못 읽어서 서버가 {"channels": [...]} 로 감싸 보낸다.</summary>
    [Serializable]
    public class ChannelList
    {
        public ChannelInfo[] channels;
    }

    [Serializable]
    public class ChannelCreate
    {
        public string name;
    }

    /// <summary>FastAPI 에러 본문 {"detail": "..."}. 422 는 detail 이 배열이라 파싱되지 않는다.</summary>
    [Serializable]
    public class HttpError
    {
        public string detail;
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

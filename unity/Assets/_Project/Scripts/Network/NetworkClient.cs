using System;
using System.Text;
using NativeWebSocket;
using UnityEngine;

namespace DgVillage
{
    /// <summary>
    /// 서버와 WebSocket 으로 연결해 채널(ChannelClient.Current, 안 골랐으면 광장)의 이 씬 맵(MapId) 방에 들어가고,
    /// 내 위치를 보내고, 받은 메시지를 이벤트로 알린다.
    /// 다른 플레이어를 화면에 그리는 일은 RemotePlayerSpawner 가 이 이벤트를 구독해서 한다.
    /// </summary>
    public class NetworkClient : MonoBehaviour
    {
        [Tooltip("위치를 보낼 내 캐릭터 (씬의 Player)")]
        [SerializeField] private PlayerMovement _localPlayer;

        [Tooltip("계정 프로필의 characterId 로 내 캐릭터를 찾을 목록")]
        [SerializeField] private CharacterRegistry _characters;

        [Tooltip("이 씬의 맵 id (서버 maps/<id>.json). 마을 맵 메뉴가 채운다")]
        [SerializeField] private string _mapId = MapClient.DefaultMapId;

        [Tooltip("초당 위치 전송 횟수. 서버 tick(10Hz)과 맞춘다")]
        [SerializeField] private float _sendRate = 10f;

        public event Action<WelcomeMessage> Welcomed;
        public event Action<PlayerState> PlayerJoined;
        public event Action<string> PlayerLeft;
        public event Action<StateMessage> StateReceived;
        public event Action<ChatBroadcastMessage> ChatReceived;
        public event Action Disconnected;
        /// <summary>서버가 본 내 좌석 · 집중 모드가 바뀌었다 (앉기 성공, 일어남). 위치는 서버가 좌석으로 옮겨 둔 값</summary>
        public event Action<PlayerState> LocalSeatChanged;
        /// <summary>연결은 유지되는 서버 에러 (예: "seat taken")</summary>
        public event Action<string> ServerError;

        /// <summary>서버 CHAT_MAX_LENGTH 와 같게 유지</summary>
        public const int ChatMaxLength = 200;

        /// <summary>서버가 정해준 내 id (= 계정 id). 방에 들어가기 전이나 연결이 끊기면 null.</summary>
        public string LocalPlayerId { get; private set; }

        /// <summary>책상에 앉아 집중 모드인가 (서버 기준). 채팅 입력을 막는 데 쓴다</summary>
        public bool LocalFocused { get; private set; }

        private string _localSeat = "";

        private WebSocket _socket;
        private float _sendTimer;
        private MoveMessage _lastSent;

        private void Awake()
        {
            // 창이 포커스를 잃어도 계속 주고받는다 (창 두 개 띄워 테스트할 때 필수)
            Application.runInBackground = true;
        }

        private async void Start()
        {
            if (_localPlayer == null || _characters == null)
            {
                Debug.LogError("NetworkClient: Local Player 또는 Characters 가 비어 있다. Inspector 에서 연결할 것.", this);
                enabled = false;
                return;
            }

            // 포탈로 왔으면 그 도착 위치에서 시작 (CameraFollow 는 첫 프레임에 바로 따라붙는다)
            if (MapClient.TryTakeSpawn(out Vector3 spawn))
            {
                _localPlayer.transform.position = spawn;
            }

            var localAnimator = _localPlayer.GetComponentInChildren<DirectionalSpriteAnimator>();
            if (localAnimator == null)
            {
                Debug.LogError("NetworkClient: Player 아래에 DirectionalSpriteAnimator 가 없다.", this);
                enabled = false;
                return;
            }

            // 보통은 캐릭터 선택 씬에서 이미 로그인했다. 이 씬을 에디터에서 바로 Play 한 경우만 여기서 로그인
            if (!AccountClient.IsLoggedIn)
            {
                try
                {
                    await AccountClient.LoginAsync();
                }
                catch (AccountException e)
                {
                    Debug.LogError($"NetworkClient: 로그인 실패 - {e.Message}", this);
                    return;
                }
            }

            Profile profile = AccountClient.Profile;
            CharacterDefinition character = _characters.Find(profile.characterId);
            if (character != null)
            {
                localAnimator.SetCharacter(character);
            }
            var localNameTag = _localPlayer.GetComponentInChildren<NameTag>();
            if (localNameTag != null)
            {
                localNameTag.SetName(profile.displayName);
            }

            // NativeWebSocket 은 생성한 스레드(메인)로 이벤트를 넘겨주므로 핸들러에서 Unity API 를 써도 된다
            _socket = new WebSocket(ServerConfig.WebSocketUrl);
            _socket.OnOpen += OnOpen;
            _socket.OnMessage += bytes => HandleMessage(Encoding.UTF8.GetString(bytes));
            _socket.OnError += error => Debug.LogWarning($"NetworkClient: {error}", this);
            _socket.OnClose += OnClose;
            Connect();
        }

        // Connect() 는 연결이 끊길 때까지 끝나지 않는 Task 라서 기다리지 않고 흘려보낸다.
        // 연결 실패도 내부에서 OnError/OnClose 로 알려준다.
        private async void Connect()
        {
            Debug.Log($"NetworkClient: {ServerConfig.WebSocketUrl} 연결 시도", this);
            await _socket.Connect();
        }

        private void OnOpen()
        {
            Profile profile = AccountClient.Profile;
            string room = ChannelClient.CurrentId;
            Debug.Log($"NetworkClient: 연결됨. 채널 {room} 맵 {_mapId} 에 {profile.displayName}({profile.characterId}) 로 입장", this);
            // 이름 · 캐릭터는 서버가 계정에서 읽는다. 여기서는 채널 · 맵 · 토큰과 시작 위치만 보낸다
            Vector3 position = _localPlayer.transform.position;
            Send(new JoinMessage
            {
                room = room, token = AccountClient.AccessToken, map = _mapId, x = position.x, z = position.z,
            });
        }

        private void OnClose(WebSocketCloseCode code)
        {
            Debug.Log($"NetworkClient: 연결 종료 ({code})", this);
            LocalPlayerId = null;
            _lastSent = null;
            if (_localSeat != "" || LocalFocused)
            {
                _localSeat = "";
                LocalFocused = false;
                LocalSeatChanged?.Invoke(new PlayerState());
            }
            Disconnected?.Invoke();
        }

        private void Update()
        {
            if (LocalPlayerId == null)
            {
                return;
            }

            _sendTimer += Time.deltaTime;
            if (_sendTimer < 1f / _sendRate)
            {
                return;
            }
            _sendTimer = 0f;

            Vector3 position = _localPlayer.transform.position;
            var move = new MoveMessage
            {
                x = position.x,
                z = position.z,
                facing = FacingWire.ToWire(_localPlayer.Facing),
                moving = _localPlayer.IsMoving,
            };

            // 가만히 서 있으면 보내지 않는다
            if (_lastSent != null && IsSame(move, _lastSent))
            {
                return;
            }
            Send(move);
            _lastSent = move;
        }

        private static bool IsSame(MoveMessage a, MoveMessage b) =>
            Mathf.Approximately(a.x, b.x) && Mathf.Approximately(a.z, b.z)
            && a.facing == b.facing && a.moving == b.moving;

        private void HandleMessage(string json)
        {
            string type = JsonUtility.FromJson<MessageHeader>(json).type;
            switch (type)
            {
                case "welcome":
                    var welcome = JsonUtility.FromJson<WelcomeMessage>(json);
                    LocalPlayerId = welcome.id;
                    Welcomed?.Invoke(welcome);
                    UpdateLocalSeat(welcome.players);
                    break;
                case "player_joined":
                    PlayerJoined?.Invoke(JsonUtility.FromJson<PlayerJoinedMessage>(json).player);
                    break;
                case "player_left":
                    PlayerLeft?.Invoke(JsonUtility.FromJson<PlayerLeftMessage>(json).id);
                    break;
                case "state":
                    var state = JsonUtility.FromJson<StateMessage>(json);
                    StateReceived?.Invoke(state);
                    UpdateLocalSeat(state.players);
                    break;
                case "chat":
                    ChatReceived?.Invoke(JsonUtility.FromJson<ChatBroadcastMessage>(json));
                    break;
                case "error":
                    string message = JsonUtility.FromJson<ErrorMessage>(json).message;
                    Debug.LogWarning($"NetworkClient: 서버 에러 - {message}", this);
                    ServerError?.Invoke(message);
                    break;
                default:
                    Debug.LogWarning($"NetworkClient: 모르는 메시지 - {json}", this);
                    break;
            }
        }

        // 내 위치는 내가 정하지만 좌석은 서버가 정한다: 스냅샷에서 내 좌석이 바뀌었으면 알린다
        private void UpdateLocalSeat(PlayerState[] players)
        {
            foreach (PlayerState player in players)
            {
                if (player.id != LocalPlayerId)
                {
                    continue;
                }
                string seat = player.seat ?? "";
                if (seat != _localSeat || player.focused != LocalFocused)
                {
                    _localSeat = seat;
                    LocalFocused = player.focused;
                    LocalSeatChanged?.Invoke(player);
                }
                return;
            }
        }

        public void SendSit(string seatId) => Send(new SitMessage { seat = seatId });

        public void SendStand() => Send(new StandMessage());

        /// <summary>
        /// 채팅을 보낸다. 말풍선은 서버가 되돌려준 chat 메시지를 받았을 때 띄운다 (내 것도 마찬가지).
        /// </summary>
        public void SendChat(string text)
        {
            text = text?.Trim();
            if (string.IsNullOrEmpty(text) || LocalPlayerId == null)
            {
                return;
            }
            if (text.Length > ChatMaxLength)
            {
                text = text.Substring(0, ChatMaxLength);
            }
            Send(new ChatMessage { text = text });
        }

        private void Send(object message)
        {
            if (_socket == null || _socket.State != WebSocketState.Open)
            {
                return;
            }
            _ = _socket.SendText(JsonUtility.ToJson(message));
        }

        private async void OnDestroy()
        {
            if (_socket != null && _socket.State == WebSocketState.Open)
            {
                await _socket.Close();
            }
        }
    }
}

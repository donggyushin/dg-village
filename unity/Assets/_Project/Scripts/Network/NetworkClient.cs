using System;
using System.Text;
using NativeWebSocket;
using UnityEngine;

namespace DgVillage
{
    /// <summary>
    /// 서버와 WebSocket 으로 연결해 방에 들어가고, 내 위치를 보내고, 받은 메시지를 이벤트로 알린다.
    /// 다른 플레이어를 화면에 그리는 일은 RemotePlayerSpawner 가 이 이벤트를 구독해서 한다.
    /// </summary>
    public class NetworkClient : MonoBehaviour
    {
        [SerializeField] private string _serverUrl = "ws://localhost:8000/ws";
        [SerializeField] private string _room = "lobby";

        [Tooltip("비워두면 guest1234 같은 임시 이름을 쓴다")]
        [SerializeField] private string _playerName = "";

        [SerializeField] private string _characterId = "girl01";

        [Tooltip("위치를 보낼 내 캐릭터 (씬의 Player)")]
        [SerializeField] private PlayerMovement _localPlayer;

        [Tooltip("초당 위치 전송 횟수. 서버 tick(10Hz)과 맞춘다")]
        [SerializeField] private float _sendRate = 10f;

        public event Action<WelcomeMessage> Welcomed;
        public event Action<PlayerState> PlayerJoined;
        public event Action<string> PlayerLeft;
        public event Action<StateMessage> StateReceived;
        public event Action<ChatBroadcastMessage> ChatReceived;
        public event Action Disconnected;

        /// <summary>서버 CHAT_MAX_LENGTH 와 같게 유지</summary>
        public const int ChatMaxLength = 200;

        /// <summary>서버가 정해준 내 id. 방에 들어가기 전이나 연결이 끊기면 null.</summary>
        public string LocalPlayerId { get; private set; }

        private WebSocket _socket;
        private float _sendTimer;
        private MoveMessage _lastSent;

        private void Awake()
        {
            // 창이 포커스를 잃어도 계속 주고받는다 (창 두 개 띄워 테스트할 때 필수)
            Application.runInBackground = true;
        }

        private void Start()
        {
            if (_localPlayer == null)
            {
                Debug.LogError("NetworkClient: Local Player 가 비어 있다. Inspector 에서 Player 를 연결할 것.", this);
                enabled = false;
                return;
            }

            if (string.IsNullOrWhiteSpace(_playerName))
            {
                _playerName = $"guest{UnityEngine.Random.Range(1000, 10000)}";
            }

            var localNameTag = _localPlayer.GetComponentInChildren<NameTag>();
            if (localNameTag != null)
            {
                localNameTag.SetName(_playerName);
            }

            // NativeWebSocket 은 생성한 스레드(메인)로 이벤트를 넘겨주므로 핸들러에서 Unity API 를 써도 된다
            _socket = new WebSocket(_serverUrl);
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
            Debug.Log($"NetworkClient: {_serverUrl} 연결 시도", this);
            await _socket.Connect();
        }

        private void OnOpen()
        {
            Debug.Log($"NetworkClient: 연결됨. '{_room}' 방에 {_playerName} 로 입장", this);
            Send(new JoinMessage { room = _room, name = _playerName, characterId = _characterId });
        }

        private void OnClose(WebSocketCloseCode code)
        {
            Debug.Log($"NetworkClient: 연결 종료 ({code})", this);
            LocalPlayerId = null;
            _lastSent = null;
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
                    break;
                case "player_joined":
                    PlayerJoined?.Invoke(JsonUtility.FromJson<PlayerJoinedMessage>(json).player);
                    break;
                case "player_left":
                    PlayerLeft?.Invoke(JsonUtility.FromJson<PlayerLeftMessage>(json).id);
                    break;
                case "state":
                    StateReceived?.Invoke(JsonUtility.FromJson<StateMessage>(json));
                    break;
                case "chat":
                    ChatReceived?.Invoke(JsonUtility.FromJson<ChatBroadcastMessage>(json));
                    break;
                case "error":
                    Debug.LogWarning($"NetworkClient: 서버 에러 - {JsonUtility.FromJson<ErrorMessage>(json).message}", this);
                    break;
                default:
                    Debug.LogWarning($"NetworkClient: 모르는 메시지 - {json}", this);
                    break;
            }
        }

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

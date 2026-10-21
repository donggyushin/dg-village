using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DgVillage
{
    /// <summary>
    /// 앉기 · 일어나기. 자리(Seat) 근처에서 E → 서버에 앉기 요청, 앉아 있을 때 E 나 이동 키 → 일어나기 요청.
    /// 실제로 앉히고 일으키는 건 서버 답(NetworkClient.LocalSeatChanged)을 받았을 때 — 빈자리 · 거리는 서버가 판단한다.
    /// 화면 아래 안내 문구(_hint)에 지금 할 수 있는 일을 보여준다.
    /// </summary>
    public class SeatInteraction : MonoBehaviour
    {
        [SerializeField] private NetworkClient _client;
        [SerializeField] private PlayerMovement _localPlayer;
        [Tooltip("화면 아래 안내 문구 (\"E 앉기\" 등)")]
        [SerializeField] private TMP_Text _hint;

        [Tooltip("이 거리(unit) 안의 자리에만 앉을 수 있다. 서버 SIT_RANGE(1.5)보다 조금 작게")]
        [SerializeField] private float _range = 1.2f;

        // 서버 에러를 잠깐 보여주는 시간 (초)
        private const float ErrorSeconds = 2f;
        // 일어나기 요청에 답이 없으면 (끊김 등) 이 시간 뒤 다시 보낼 수 있게
        private const float StandRetrySeconds = 1f;

        private float _standRequestedAt = float.NegativeInfinity;
        private NameTag _nameTag;
        private string _error;
        private float _errorUntil;

        private void Awake()
        {
            if (_client == null || _localPlayer == null || _hint == null)
            {
                Debug.LogError("SeatInteraction: Client · Local Player · Hint 를 Inspector 에서 연결할 것.", this);
                enabled = false;
                return;
            }
            _nameTag = _localPlayer.GetComponentInChildren<NameTag>();
        }

        private void OnEnable()
        {
            if (_client != null)
            {
                _client.LocalSeatChanged += OnLocalSeatChanged;
                _client.ServerError += OnServerError;
            }
        }

        private void OnDisable()
        {
            if (_client != null)
            {
                _client.LocalSeatChanged -= OnLocalSeatChanged;
                _client.ServerError -= OnServerError;
            }
        }

        private void OnLocalSeatChanged(PlayerState me)
        {
            _standRequestedAt = float.NegativeInfinity;
            if (_nameTag != null)
            {
                _nameTag.SetFocused(me.focused);
            }
            if (me.IsSeated)
            {
                _localPlayer.SitAt(new Vector3(me.x, _localPlayer.transform.position.y, me.z));
            }
            else
            {
                _localPlayer.StandUp();
            }
        }

        private void OnServerError(string message)
        {
            _error = message switch
            {
                "seat taken" => "누가 앉아 있어요",
                "too far from the seat" => "자리에 더 가까이 가세요",
                _ => null,
            };
            _errorUntil = Time.time + ErrorSeconds;
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            // 채팅 입력 중에는 E 가 글자다
            bool ePressed = keyboard != null && keyboard.eKey.wasPressedThisFrame && !_localPlayer.InputBlocked;

            if (_localPlayer.Seated)
            {
                if ((ePressed || _localPlayer.WantsToStand) && Time.time - _standRequestedAt > StandRetrySeconds)
                {
                    _standRequestedAt = Time.time;
                    _client.SendStand();
                }
                SetHint(_client.LocalFocused ? "집중 모드 — 채팅이 들리지 않아요  ·  E 일어나기" : "E 일어나기");
                return;
            }

            Seat seat = Seat.Nearest(_localPlayer.transform.position, _range);
            if (seat != null && ePressed)
            {
                _client.SendSit(seat.Id);
            }
            if (_error != null && Time.time < _errorUntil)
            {
                SetHint(_error);
            }
            else
            {
                SetHint(seat == null ? "" : seat.IsDesk ? "E 앉아서 집중하기" : "E 앉기");
            }
        }

        private void SetHint(string text)
        {
            if (_hint.text != text)
            {
                _hint.text = text;
            }
        }
    }
}

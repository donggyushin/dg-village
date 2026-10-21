using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace DgVillage
{
    /// <summary>
    /// 화면 아래 채팅 입력창. Enter 로 입력 시작 → Enter 로 전송 / Esc 로 취소.
    /// 입력 중일 때만 보이고, 그동안 캐릭터는 움직이지 않는다.
    /// </summary>
    [RequireComponent(typeof(TMP_InputField))]
    public class ChatInput : MonoBehaviour
    {
        [SerializeField] private NetworkClient _client;
        [SerializeField] private PlayerMovement _localPlayer;

        private TMP_InputField _field;

        // 오브젝트를 꺼버리면 이 컴포넌트의 Update(Enter 감지)도 멈추므로, 투명도로만 숨긴다
        private CanvasGroup _canvasGroup;

        // 전송한 바로 그 Enter 가 입력창을 다시 여는 것 / 여는 Enter 가 곧바로 전송되는 것을 막는다
        private int _closedFrame = -1;
        private int _openedFrame = -1;

        private void Awake()
        {
            _field = GetComponent<TMP_InputField>();
            _field.lineType = TMP_InputField.LineType.SingleLine;
            _field.characterLimit = NetworkClient.ChatMaxLength;
            _field.richText = false;
            _field.onSubmit.AddListener(OnSubmit);

            if (!TryGetComponent(out _canvasGroup))
            {
                _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
            SetVisible(false);

            if (_client == null || _localPlayer == null)
            {
                Debug.LogError("ChatInput: Client 또는 Local Player 가 비어 있다.", this);
                enabled = false;
            }
        }

        private void OnDestroy() => _field.onSubmit.RemoveListener(OnSubmit);

        private void Update()
        {
            _localPlayer.InputBlocked = _field.isFocused;

            // ActivateInputField 후 실제 포커스는 다음 프레임에 잡히므로 연 직후 1프레임은 보이게 둔다.
            // 다른 곳을 클릭해 포커스를 잃은 경우도 여기서 숨겨진다
            SetVisible(_field.isFocused || Time.frameCount - _openedFrame <= 1);

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            if (_field.isFocused)
            {
                if (keyboard.escapeKey.wasPressedThisFrame)
                {
                    _field.text = "";
                    Close();
                }
                return;
            }

            bool enterPressed = keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame;
            // 집중 모드(책상에 앉음)에서는 채팅을 열지 않는다 — 보내도 아무도 못 듣는다
            if (enterPressed && Time.frameCount != _closedFrame && !_client.LocalFocused)
            {
                _openedFrame = Time.frameCount;
                SetVisible(true);
                _field.ActivateInputField();
            }
        }

        private void SetVisible(bool visible)
        {
            _canvasGroup.alpha = visible ? 1f : 0f;
            // 숨어 있을 때 마우스 클릭으로 포커스가 잡히지 않게
            _canvasGroup.interactable = visible;
            _canvasGroup.blocksRaycasts = visible;
        }

        private void OnSubmit(string text)
        {
            // 입력창을 연 Enter 가 그대로 전송으로 처리된 경우 무시하고 계속 입력 상태로 둔다
            if (Time.frameCount - _openedFrame <= 1)
            {
                _field.ActivateInputField();
                return;
            }

            _client.SendChat(text);
            _field.text = "";
            Close();
        }

        private void Close()
        {
            _closedFrame = Time.frameCount;
            SetVisible(false);
            _field.DeactivateInputField();
            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(null);
            }
        }
    }
}

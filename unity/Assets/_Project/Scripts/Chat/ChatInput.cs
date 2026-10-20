using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace DgVillage
{
    /// <summary>
    /// 화면 아래 채팅 입력창. Enter 로 입력 시작 → Enter 로 전송 / Esc 로 취소.
    /// 입력창에 포커스가 있는 동안에는 캐릭터가 움직이지 않는다.
    /// </summary>
    [RequireComponent(typeof(TMP_InputField))]
    public class ChatInput : MonoBehaviour
    {
        [SerializeField] private NetworkClient _client;
        [SerializeField] private PlayerMovement _localPlayer;

        private TMP_InputField _field;

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
            if (enterPressed && Time.frameCount != _closedFrame)
            {
                _openedFrame = Time.frameCount;
                _field.ActivateInputField();
            }
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
            _field.DeactivateInputField();
            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(null);
            }
        }
    }
}

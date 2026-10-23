using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace DgVillage
{
    /// <summary>
    /// 화면 왼쪽 아래의 반투명 채팅 기록창. 서버에서 온 채팅(나 포함)을 한 줄씩 쌓는다.
    /// 아래 버튼 또는 Tab 으로 켜고 끈다 (켜짐/꺼짐은 PlayerPrefs 에 기억). 꺼져 있는 동안 온 채팅 수를 버튼에 보여준다.
    /// 기록은 클라이언트 메모리에만 있다: 채널을 나가면 (씬이 바뀌면) 사라지고, 포탈 이동은 같은 씬이라 남는다.
    /// UI 는 코드로 만든다 (ChatCanvas 아래 이 오브젝트의 자식으로). 붙이기 · 연결은 "Set Up Village Scene" 메뉴.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class ChatLog : MonoBehaviour
    {
        private const string OpenPrefKey = "dgvillage.chatLogOpen";
        private const string ToggleLabel = "채팅 기록 (Tab)";

        [SerializeField] private NetworkClient _client;

        [Tooltip("이보다 많으면 오래된 줄부터 지운다")]
        [SerializeField] private int _maxLines = 100;

        [SerializeField] private Vector2 _panelSize = new(320f, 220f);
        [SerializeField] private float _margin = 16f;
        [SerializeField] private float _fontSize = 16f;
        [SerializeField] private Color _panelColor = new(0f, 0f, 0f, 0.35f);
        [SerializeField] private Color _nameColor = new(1f, 0.85f, 0.55f);
        [SerializeField] private Color _ownNameColor = new(0.6f, 0.9f, 1f);

        private const float ButtonHeight = 28f;
        private const float Gap = 6f;
        private const float Padding = 8f;

        private GameObject _panel;
        private ScrollRect _scroll;
        private RectTransform _content;
        private TMP_Text _buttonText;
        private int _unread;

        private void Awake()
        {
            if (_client == null)
            {
                Debug.LogError("ChatLog: Client 가 비어 있다.", this);
                enabled = false;
                return;
            }
            BuildUI();
            SetOpen(LoadOpen());
        }

        private void OnEnable()
        {
            if (_client != null)
            {
                _client.ChatReceived += OnChatReceived;
            }
        }

        private void OnDisable()
        {
            if (_client != null)
            {
                _client.ChatReceived -= OnChatReceived;
            }
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            // 채팅 입력 중(InputBlocked)의 Tab 은 무시
            if (keyboard != null && keyboard.tabKey.wasPressedThisFrame && !IsTyping())
            {
                Toggle();
            }
        }

        private static bool IsTyping()
        {
            GameObject selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            return selected != null && selected.TryGetComponent(out TMP_InputField field) && field.isFocused;
        }

        private void Toggle() => SetOpen(!_panel.activeSelf);

        private void SetOpen(bool open)
        {
            _panel.SetActive(open);
            if (open)
            {
                _unread = 0;
                ScrollToBottom();
            }
            UpdateButtonText();
            try
            {
                PlayerPrefs.SetInt(OpenPrefKey, open ? 1 : 0);
            }
            catch (System.Exception)
            {
                // 기억 못 해도 동작에는 지장 없다
            }
        }

        private static bool LoadOpen()
        {
            try
            {
                return PlayerPrefs.GetInt(OpenPrefKey, 1) == 1;
            }
            catch (System.Exception)
            {
                return true;
            }
        }

        private void UpdateButtonText() =>
            _buttonText.text = _unread > 0 ? $"{ToggleLabel}  +{_unread}" : ToggleLabel;

        private void OnChatReceived(ChatBroadcastMessage chat)
        {
            // 맨 아래를 보고 있을 때만 새 줄을 따라 내려간다 (위로 올려 읽는 중이면 그대로)
            bool atBottom = _scroll.verticalNormalizedPosition <= 0.01f
                || _content.rect.height <= _scroll.viewport.rect.height;

            AddLine(chat);
            while (_content.childCount > _maxLines)
            {
                // Destroy 는 프레임 끝에 지워지므로 먼저 떼어내야 childCount 가 줄어든다
                Transform oldest = _content.GetChild(0);
                oldest.SetParent(null, false);
                Destroy(oldest.gameObject);
            }

            if (_panel.activeSelf)
            {
                if (atBottom)
                {
                    ScrollToBottom();
                }
            }
            else
            {
                _unread++;
                UpdateButtonText();
            }
        }

        private void AddLine(ChatBroadcastMessage chat)
        {
            TMP_Text text = CreateText("Line", _content, TextAlignmentOptions.TopLeft);
            text.textWrappingMode = TextWrappingModes.Normal;
            Color nameColor = chat.id == _client.LocalPlayerId ? _ownNameColor : _nameColor;
            // 이름 · 내용은 플레이어가 쓴 글이라 태그로 해석되지 않게 감싼다 (색은 이름에만)
            text.text = $"<color=#{ColorUtility.ToHtmlStringRGB(nameColor)}>{NoParse(chat.name)}</color>: {NoParse(chat.text)}";
        }

        private static readonly Regex NoParseEnd = new("</noparse>", RegexOptions.IgnoreCase);

        // 글 안의 "</noparse>" 는 "<" 를 noparse 안에 남기고 다시 여는 식으로 쪼개서 그대로 보이게 한다
        private static string NoParse(string value) =>
            "<noparse>" + NoParseEnd.Replace(value ?? "", "<</noparse><noparse>/noparse>") + "</noparse>";

        private void ScrollToBottom()
        {
            // 방금 추가한 줄의 높이가 레이아웃에 반영된 뒤에 내린다
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(_content);
            _scroll.verticalNormalizedPosition = 0f;
        }

        // ----- UI 만들기 -----

        private void BuildUI()
        {
            var root = (RectTransform)transform;
            root.anchorMin = root.anchorMax = root.pivot = Vector2.zero;
            root.anchoredPosition = new Vector2(_margin, _margin);
            root.sizeDelta = new Vector2(_panelSize.x, ButtonHeight + Gap + _panelSize.y);

            // 토글 버튼 (맨 아래)
            var buttonRect = CreateRect("ToggleButton", root, typeof(Image), typeof(Button));
            buttonRect.anchorMin = buttonRect.anchorMax = buttonRect.pivot = Vector2.zero;
            buttonRect.sizeDelta = new Vector2(180f, ButtonHeight);
            buttonRect.GetComponent<Image>().color = _panelColor;
            var button = buttonRect.GetComponent<Button>();
            // 선택된 채로 남으면 채팅 Enter 가 이 버튼을 누른다 (WindowControls 와 같은 이유)
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.onClick.AddListener(Toggle);
            button.onClick.AddListener(() => EventSystem.current?.SetSelectedGameObject(null));
            _buttonText = CreateText("Label", buttonRect, TextAlignmentOptions.Center);
            Stretch((RectTransform)_buttonText.transform, 0f);

            // 기록창 (버튼 위)
            var panelRect = CreateRect("Panel", root, typeof(Image), typeof(ScrollRect));
            panelRect.anchorMin = panelRect.anchorMax = panelRect.pivot = Vector2.zero;
            panelRect.anchoredPosition = new Vector2(0f, ButtonHeight + Gap);
            panelRect.sizeDelta = _panelSize;
            panelRect.GetComponent<Image>().color = _panelColor;
            _panel = panelRect.gameObject;

            var viewport = CreateRect("Viewport", panelRect, typeof(RectMask2D));
            Stretch(viewport, Padding);

            _content = CreateRect("Content", viewport, typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            // 위쪽에 붙고 아래로 늘어나는 내용
            _content.anchorMin = new Vector2(0f, 1f);
            _content.anchorMax = Vector2.one;
            _content.pivot = new Vector2(0.5f, 1f);
            _content.sizeDelta = Vector2.zero;
            var layout = _content.GetComponent<VerticalLayoutGroup>();
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.spacing = 2f;
            _content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            _scroll = panelRect.GetComponent<ScrollRect>();
            _scroll.viewport = viewport;
            _scroll.content = _content;
            _scroll.horizontal = false;
            _scroll.movementType = ScrollRect.MovementType.Clamped;
            _scroll.scrollSensitivity = 24f;
        }

        private static RectTransform CreateRect(string name, Transform parent, params System.Type[] components)
        {
            var go = new GameObject(name, typeof(RectTransform));
            foreach (System.Type component in components)
            {
                go.AddComponent(component);
            }
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            return rect;
        }

        private TMP_Text CreateText(string name, Transform parent, TextAlignmentOptions alignment)
        {
            var text = CreateRect(name, parent, typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            text.fontSize = _fontSize;
            text.alignment = alignment;
            text.color = Color.white;
            text.raycastTarget = false;
            return text;
        }

        private static void Stretch(RectTransform rect, float inset)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
        }
    }
}

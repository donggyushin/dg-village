using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ChatPanelManager : MonoBehaviour
{
    [SerializeField] private TMP_InputField _input;
    [SerializeField] private GameObject _chatLine;
    [SerializeField] private GameObject _content;
    [SerializeField] private ScrollRect _scrollRect;
    [SerializeField] private RectTransform _panelTransform;
    [SerializeField] private Button _foldButton;
    private float _expandedX;

    private bool _isCollapsed = false;

    public Action<string> ChatSubmit;

    void Awake()
    {
        _expandedX = _panelTransform.anchoredPosition.x;
        _foldButton.onClick.AddListener(FoldButtonTapped);
        Fold();
    }

    public void FoldButtonTapped()
    {
        if (_isCollapsed)
        {
            Expand();
        }
        else
        {
            Fold();
        }
    }

    private void Fold()
    {
        _isCollapsed = true;

        _panelTransform.anchoredPosition = new Vector2(_panelTransform.rect.width - 100, _panelTransform.anchoredPosition.y);
        _foldButton.GetComponentInChildren<TMP_Text>().text = "<";
    }

    private void Expand()
    {
        _isCollapsed = false;

        _panelTransform.anchoredPosition = new Vector2(_expandedX, _panelTransform.anchoredPosition.y);
        _foldButton.GetComponentInChildren<TMP_Text>().text = ">";
    }

    void OnEnable()
    {
        _input.onSubmit.AddListener(HandleSubmit);
    }

    void OnDisable()
    {
        _input.onSubmit.RemoveListener(HandleSubmit);
    }

    public void AppendChatLines(string[] chats)
    {
        foreach (var chat in chats)
        {
            GameObject chatline = Instantiate(_chatLine, _content.transform);
            chatline.GetComponent<TMP_Text>().text = chat;
        }

        Canvas.ForceUpdateCanvases();
        _scrollRect.verticalNormalizedPosition = 0f;
    }

    private void HandleSubmit(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;

        _input.text = string.Empty;
        _input.ActivateInputField();

        ChatSubmit?.Invoke(text);
        Debug.Log($"채팅 입력: {text}");
    }
}

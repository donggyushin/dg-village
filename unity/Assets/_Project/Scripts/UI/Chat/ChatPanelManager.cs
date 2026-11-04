using System;
using TMPro;
using UnityEngine;

public class ChatPanelManager : MonoBehaviour
{
    [SerializeField] private TMP_InputField _input;
    [SerializeField] private GameObject _chatLine;
    [SerializeField] private GameObject _content;

    public Action<string> ChatSubmit;

    void OnEnable()
    {
        _input.onSubmit.AddListener(HandleSubmit);
    }

    void OnDisable()
    {
        _input.onSubmit.RemoveListener(HandleSubmit);
    }

    private void HandleSubmit(string text)
    {
        _input.text = string.Empty;
        _input.ActivateInputField();

        ChatSubmit?.Invoke(text);
        Debug.Log($"채팅 입력: {text}");
    }
}

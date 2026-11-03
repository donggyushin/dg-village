using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DeleteConfirmDialog : MonoBehaviour
{
    [SerializeField] private GameObject _dim;
    [SerializeField] private TMP_Text _nickname;
    [SerializeField] private Button _cancelButton;
    [SerializeField] private Button _deleteButton;

    public Action _confirmAction;

    void Awake()
    {
        Hide();

        _cancelButton.onClick.AddListener(Cancel);
        _deleteButton.onClick.AddListener(Confirm);
    }
    public void Hide()
    {
        _dim.SetActive(false);
    }

    public void Show(string nickname)
    {
        _nickname.text = nickname;
        _dim.SetActive(true);
    }

    private void Cancel()
    {
        Hide();
    }

    private void Confirm()
    {
        Hide();
        _confirmAction?.Invoke();
    }
}
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CharacterLobbySlot : MonoBehaviour
{
    private int _characterId;
    [SerializeField] private Image _illustration;
    [SerializeField] private Toggle _toggle;
    [SerializeField] private GameObject _selectionFrame;
    [SerializeField] private GameObject _emptyState;
    [SerializeField] private TMP_Text _nickname;

    public event Action SelectionChanged;

    void Awake()
    {
        _toggle.isOn = false;
        _toggle.onValueChanged.AddListener(OnToggleChanged);
        ConfigureVisuals();
    }

    public void SetNickname(string nickname)
    {
        _nickname.text = nickname;
    }

    public void SetToggle(bool value)
    {
        _toggle.isOn = value;
    }

    private void OnToggleChanged(bool value)
    {
        ConfigureVisuals();
        SelectionChanged?.Invoke();
    }

    private void ConfigureVisuals()
    {
        bool occupied = _characterId != 0 && _illustration.sprite != null;
        _illustration.enabled = occupied;
        _emptyState.SetActive(!occupied);
        _selectionFrame.SetActive(IsSelected());
        _toggle.interactable = occupied;
    }

    public void SetSprite(Sprite sprite)
    {
        _illustration.sprite = sprite;
        _illustration.preserveAspect = true;
        ConfigureVisuals();
    }

    public void Clean()
    {
        _characterId = 0;
        _illustration.sprite = null;
        _toggle.isOn = false;
        _nickname.text = "";
        ConfigureVisuals();
    }

    public bool IsSelected()
    {
        if (_characterId == 0 || _illustration.sprite == null)
        {
            return false;
        }
        return _toggle.isOn;
    }

    public void SetCharacterId(int id)
    {
        _characterId = id;
        ConfigureVisuals();
    }

    public int CharacterId()
    {
        return _characterId;
    }
}

using System;
using UnityEngine;
using UnityEngine.UI;

public class CharacterLobbySlot : MonoBehaviour
{
    private int _characterId;
    [SerializeField] private SpriteRenderer _spriteRenderer;
    [SerializeField] private Toggle _toggle;
    [SerializeField] private MeshRenderer _meshRenderer;

    public event Action SelectionChanged;

    void Awake()
    {
        _toggle.isOn = false;
        _toggle.onValueChanged.AddListener(OnToggleChanged);
        ConfigureGlowRing();
    }

    public void SetToggle(bool value)
    {
        _toggle.isOn = value;
    }

    private void OnToggleChanged(bool value)
    {
        ConfigureGlowRing();
        SelectionChanged?.Invoke();
    }

    private void ConfigureGlowRing()
    {
        _meshRenderer.enabled = IsSelected();
    }

    public void SetSprite(Sprite sprite)
    {
        _spriteRenderer.sprite = sprite;
    }

    public void Clean()
    {
        _characterId = 0;
        _spriteRenderer.sprite = null;
        _toggle.isOn = false;
    }

    public bool IsSelected()
    {
        return _toggle.isOn;
    }

    public void SetCharacterId(int id)
    {
        _characterId = id;
    }

    public int CharacterId()
    {
        return _characterId;
    }
}

using System;
using UnityEngine;
using UnityEngine.UI;

public class CharacterLobbySlot : MonoBehaviour
{
    private string _characterSpriteId;
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

    private void OnToggleChanged(bool value)
    {
        if (value == true)
        {
            if (_spriteRenderer.sprite == null || _characterSpriteId == null)
            {
                _toggle.isOn = false;
            }
        }
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
        _characterSpriteId = null;
        _spriteRenderer.sprite = null;
        _toggle.isOn = false;
    }

    public bool IsSelected()
    {
        return _toggle.isOn;
    }

    public void SetCharacterSpriteId(string id)
    {
        _characterSpriteId = id;
    }

    public string CharacterSpriteId()
    {
        return _characterSpriteId;
    }
}

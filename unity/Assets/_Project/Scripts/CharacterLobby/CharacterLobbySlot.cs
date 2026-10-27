using UnityEngine;
using UnityEngine.UI;

public class CharacterLobbySlot : MonoBehaviour
{
    private string _characterSpriteId;
    [SerializeField] private SpriteRenderer _spriteRenderer;
    [SerializeField] private Toggle _toggle;
    [SerializeField] private MeshRenderer _meshRenderer;

    void Awake()
    {
        ConfigureGlowRing();
    }

    private void ConfigureGlowRing()
    {
        _meshRenderer.enabled = IsSelected();
    }

    public void SetSprite(Sprite sprite)
    {
        _spriteRenderer.sprite = sprite;
        ConfigureGlowRing();
    }

    public void Clean()
    {
        _characterSpriteId = null;
        _spriteRenderer.sprite = null;
        ConfigureGlowRing();
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

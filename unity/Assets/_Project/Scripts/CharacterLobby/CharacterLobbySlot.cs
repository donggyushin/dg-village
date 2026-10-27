using UnityEngine;
using UnityEngine.UI;

public class CharacterLobbySlot : MonoBehaviour
{
    private string _characterSpriteId;
    [SerializeField] private SpriteRenderer _spriteRenderer;
    [SerializeField] private Toggle _toggle;

    public void SetSprite(Sprite sprite)
    {
        _spriteRenderer.sprite = sprite;
        Debug.Log(sprite);
    }

    public void Clean()
    {
        _characterSpriteId = null;
        _spriteRenderer.sprite = null;
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

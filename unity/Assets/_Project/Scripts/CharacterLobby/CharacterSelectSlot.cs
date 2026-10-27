using UnityEngine;
using UnityEngine.UI;

public class CharacterSelectSlot : MonoBehaviour
{
    [SerializeField] private Image _thumbnail;
    [SerializeField] private Toggle _toggle;
    public bool IsSelected => _toggle.isOn;
    public CharacterSpriteSet Character => _character;
    private CharacterSpriteSet _character;

    public void Show(CharacterSpriteSet character)
    {
        _character = character;
        _thumbnail.sprite = character.Idle.Get(Direction.South);
        _thumbnail.enabled = true;
        _toggle.interactable = true;
    }

    public void ShowEmpty()
    {
        _character = null;
        _thumbnail.sprite = null;
        _thumbnail.enabled = false;
        _toggle.interactable = false;
    }

    public void Select()
    {
        _toggle.isOn = true;
    }
}

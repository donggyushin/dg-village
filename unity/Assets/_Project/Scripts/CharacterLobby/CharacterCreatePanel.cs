using TMPro;
using UnityEngine;
using UnityEngine.InputSystem.Interactions;
using UnityEngine.UI;

public class CharacterCreatePanel : MonoBehaviour
{
    [SerializeField] private CharacterCatalog _catalog;
    [SerializeField] private CharacterSelectSlot[] _slots;
    [SerializeField] private TMP_InputField _nicknameInput;
    [SerializeField] private Button _createButton;

    void Start()
    {
        ShowCharacters();
        _createButton.onClick.AddListener(OnCreateButtonClicked);
    }

    private void ShowCharacters()
    {
        var characters = _catalog.Characters();

        for (int i = 0; i < _slots.Length; i++)
        {
            if (i < characters.Length)
            {
                _slots[i].Show(characters[i]);
            }
            else
            {
                _slots[i].ShowEmpty();
            }
        }

        if (characters.Length >= 1)
        {
            _slots[0].Select();
        }
    }

    private CharacterSpriteSet GetSelectedCharacter()
    {
        foreach (var slot in _slots)
        {
            if (slot.IsSelected == true)
            {
                return slot.Character;
            }
        }
        return null;
    }

    private void OnCreateButtonClicked()
    {
        string nickname = _nicknameInput.text.Trim();
        var character = GetSelectedCharacter();

        if (nickname.Length < 2)
        {
            Debug.LogWarning("닉네임은 2자 이상이어야 합니다.");
            return;
        }

        if (character == null)
        {
            Debug.LogWarning("캐릭터를 선택하세요.");
            return;
        }

        Debug.Log($"캐릭터 생성 요청: {nickname} / {character.Id}");
    }
}

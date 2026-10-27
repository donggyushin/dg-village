using System.Collections;
using Newtonsoft.Json;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

public class CharacterCreatePanel : MonoBehaviour
{
    [SerializeField] private CharacterCatalog _catalog;
    [SerializeField] private CharacterSelectSlot[] _slots;
    [SerializeField] private TMP_InputField _nicknameInput;
    [SerializeField] private Button _createButton;
    [SerializeField] private Toast _toast;
    [SerializeField] private CharacterListManager characterListManager;

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

        StartCoroutine(CreateCharacter(nickname, character.Id, Session.AccountId, Session.ServerId));
    }

    // 서버 CharacterIn과 필드 이름을 똑같이 맞춰야 함 (snake_case)
    private class CreateCharacterRequest
    {
        public string nickname;
        public string sprite_set_id;
    }

    private IEnumerator CreateCharacter(string nickname, string spriteSetId, string accountId, int serverId)
    {

        string url = $"{ServerConfig.BaseUrl}/servers/{serverId}/characters";
        string json = JsonConvert.SerializeObject(new CreateCharacterRequest
        {
            nickname = nickname,
            sprite_set_id = spriteSetId,
        });

        using (UnityWebRequest request = UnityWebRequest.Post(url, json, "application/json"))
        {
            request.SetRequestHeader("X-Account-Id", accountId);
            _createButton.interactable = false;
            yield return request.SendWebRequest();
            _createButton.interactable = true;

            if (request.responseCode == 409)
            {
                _toast.Show("이미 사용중인 닉네임입니다.");
            }

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"캐릭터 생성 실패 {request.responseCode}:{request.downloadHandler.text}");
                yield break;
            }

            _toast.Show("캐릭터를 생성하였습니다.");

            characterListManager.Refresh();
        }
    }
}

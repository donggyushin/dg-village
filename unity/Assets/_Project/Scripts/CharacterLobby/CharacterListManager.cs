using System;
using System.Collections;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

public class CharacterListManager : MonoBehaviour
{

    [SerializeField] private CharacterLobbySlot[] characterLobbySlots;
    [SerializeField] private CharacterCatalog _catalog;
    [SerializeField] private Button _enterButton;

    // 서버 CharacterOut과 필드 이름 맞춤 (snake_case)
    private class CharacterInfo
    {
        public int id;
        public int server_id;
        public string nickname;
        public string sprite_set_id;
        public DateTime created_at;   // Newtonsoft가 ISO 문자열을 DateTime으로 자동 변환
    }

    void Start()
    {
        ConfigureEnterButton();
        StartCoroutine(FetchCharacters());
        _enterButton.onClick.AddListener(EnterButtonClicked);

        foreach (var slot in characterLobbySlots)
        {
            slot.SelectionChanged += ConfigureEnterButton;
        }
    }

    public void Refresh()
    {
        CleanCharacterLobbySlots();
        StartCoroutine(FetchCharacters());
        ConfigureEnterButton();
    }

    private void CleanCharacterLobbySlots()
    {
        foreach (var slot in characterLobbySlots)
        {
            slot.Clean();
        }
    }

    private void EnterButtonClicked()
    {
        Debug.Log("Enter!");
    }

    private void ConfigureEnterButton()
    {
        var hasSelected = false;

        foreach (var slot in characterLobbySlots)
        {
            if (slot.IsSelected())
            {
                hasSelected = true;
                break;
            }
        }

        _enterButton.enabled = hasSelected;
        _enterButton.GetComponent<CanvasGroup>().alpha = hasSelected ? 1 : 0;
    }

    private IEnumerator FetchCharacters()
    {
        ConfigureEnterButton();
        var serverId = Session.ServerId;
        var accountId = Session.AccountId;

        string url = $"{ServerConfig.BaseUrl}/servers/{serverId}/characters";

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {

            request.SetRequestHeader("X-Account-Id", accountId);
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"캐릭터 목록 요청 실패 {request.responseCode}: {request.downloadHandler.text}");
                yield break;
            }

            CharacterInfo[] characters = JsonConvert.DeserializeObject<CharacterInfo[]>(request.downloadHandler.text);

            for (int i = 0; i < characters.Length; i++)
            {
                var character = characters[i];
                var spriteId = character.sprite_set_id;
                CharacterSpriteSet spriteSet = _catalog.Find(spriteId);
                Sprite sprite = spriteSet.Idle.Get(Direction.SouthWest);

                if (characterLobbySlots.Length > i)
                {
                    characterLobbySlots[i].SetSprite(sprite);
                    characterLobbySlots[i].SetCharacterSpriteId(spriteId);
                }
            }

            foreach (var slot in characterLobbySlots)
            {
                if (slot.CharacterSpriteId() != null)
                {
                    slot.SetToggle(true);
                    break;
                }
            }

            ConfigureEnterButton();
        }
    }

}

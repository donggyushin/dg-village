using UnityEngine;

namespace DgVillage
{
    /// <summary>
    /// 캐릭터 선택 씬에서 고른 닉네임 · 캐릭터를 마을 씬으로 넘겨주는 곳.
    /// static 값은 씬이 바뀌어도 남아 있고, PlayerPrefs 에도 저장해 다음 실행 때 다시 채운다.
    /// 마을 씬을 에디터에서 바로 Play 하면 마지막 저장값(없으면 빈 값)을 쓴다.
    /// 계정이 생기면 서버 쪽 저장으로 옮긴다.
    /// </summary>
    public static class PlayerSession
    {
        private const string NamePrefsKey = "dgvillage.playerName";
        private const string CharacterPrefsKey = "dgvillage.characterId";

        /// <summary>비어 있으면 NetworkClient 가 guest1234 같은 임시 이름을 쓴다.</summary>
        public static string PlayerName => PlayerPrefs.GetString(NamePrefsKey, "");

        /// <summary>비어 있거나 모르는 id 면 CharacterRegistry 기본 캐릭터.</summary>
        public static string CharacterId => PlayerPrefs.GetString(CharacterPrefsKey, "");

        public static void Save(string playerName, string characterId)
        {
            PlayerPrefs.SetString(NamePrefsKey, playerName?.Trim() ?? "");
            PlayerPrefs.SetString(CharacterPrefsKey, characterId ?? "");
            PlayerPrefs.Save();
        }
    }
}

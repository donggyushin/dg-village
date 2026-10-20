using System.Collections.Generic;
using UnityEngine;

namespace DgVillage
{
    /// <summary>
    /// 고를 수 있는 캐릭터 목록. 프로젝트에 하나 (Assets/_Project/Characters/CharacterRegistry.asset).
    /// 첫 번째 항목이 기본 캐릭터 — 모르는 id 를 받으면 그걸로 대신한다.
    /// </summary>
    [CreateAssetMenu(fileName = "CharacterRegistry", menuName = "DgVillage/Character Registry")]
    public class CharacterRegistry : ScriptableObject
    {
        [SerializeField] private List<CharacterDefinition> _characters = new();

        public IReadOnlyList<CharacterDefinition> Characters => _characters;
        public CharacterDefinition Default => _characters.Count > 0 ? _characters[0] : null;

        public CharacterDefinition Find(string id)
        {
            foreach (CharacterDefinition character in _characters)
            {
                if (character != null && character.Id == id)
                {
                    return character;
                }
            }
            return Default;
        }
    }
}

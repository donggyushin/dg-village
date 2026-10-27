using UnityEngine;

[CreateAssetMenu(fileName = "CharacterCatalog", menuName = "DG Village/Character Catalog")]
public class CharacterCatalog : ScriptableObject
{
    [SerializeField] private CharacterSpriteSet[] _characters;

    public CharacterSpriteSet TryGet(string id)
    {
        foreach (var character in _characters)
        {
            if (character.Id == id)
            {
                return character;
            }
        }
        return null;
    }
}

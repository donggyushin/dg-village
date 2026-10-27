using UnityEngine;

[CreateAssetMenu(fileName = "CharacterSpriteSet", menuName = "DG Village/Character Sprite Set")]
public class CharacterSpriteSet : ScriptableObject
{
    [SerializeField] private string _id;
    [SerializeField] private DirectionalSprites _idle;

    public string Id => _id;
    public DirectionalSprites Idle => _idle;
}

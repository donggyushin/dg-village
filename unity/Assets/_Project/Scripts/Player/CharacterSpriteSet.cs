using UnityEngine;

[CreateAssetMenu(fileName = "CharacterSpriteSet", menuName = "DG Village/Character Sprite Set")]
public class CharacterSpriteSet : ScriptableObject
{
    [SerializeField] private string _id;
    [SerializeField] private DirectionalSprites _idle;
    [SerializeField] private DirectionalWalkSprites _walk;
    [SerializeField] private Sprite _portrait;
    [SerializeField] private Sprite _illustrate;


    public string Id => _id;
    public DirectionalSprites Idle => _idle;
    public DirectionalWalkSprites Walk => _walk;
    public Sprite portrait => _portrait;
    public Sprite illustrate => _illustrate;
}

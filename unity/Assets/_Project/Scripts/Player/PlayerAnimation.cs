using UnityEngine;

public class PlayerAnimation : MonoBehaviour
{
    [SerializeField] private SpriteRenderer _spriteRenderer;

    void Awake()
    {
        _spriteRenderer = GetComponentInChildren<SpriteRenderer>();
    }
}

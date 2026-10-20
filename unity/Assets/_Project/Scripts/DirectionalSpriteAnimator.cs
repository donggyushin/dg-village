using System;
using UnityEngine;

namespace DgVillage
{
    /// <summary>
    /// PlayerMovement 의 Facing · IsMoving 을 읽어 방향별 서기/걷기 스프라이트를 재생한다.
    /// Left 는 Right 를 좌우 반전해서 쓴다 (스프라이트 1방향 절약).
    /// Visual(SpriteRenderer) 오브젝트에 붙이고, PlayerMovement 는 부모에서 찾는다.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class DirectionalSpriteAnimator : MonoBehaviour
    {
        [Serializable]
        private struct DirectionSprites
        {
            public Sprite Idle;
            public Sprite[] Walk;
        }

        [SerializeField] private DirectionSprites _down;
        [SerializeField] private DirectionSprites _up;
        [SerializeField] private DirectionSprites _right;

        [SerializeField] private float _framesPerSecond = 10f;

        private SpriteRenderer _renderer;
        private PlayerMovement _movement;
        private float _frameTimer;
        private int _frameIndex;

        private void Awake()
        {
            _renderer = GetComponent<SpriteRenderer>();
            _movement = GetComponentInParent<PlayerMovement>();

            if (_movement == null)
            {
                Debug.LogError("DirectionalSpriteAnimator: 부모에 PlayerMovement 가 없다.", this);
                enabled = false;
            }
        }

        private void Update()
        {
            Facing facing = _movement.Facing;
            DirectionSprites set = facing switch
            {
                Facing.Up => _up,
                Facing.Left or Facing.Right => _right,
                _ => _down,
            };
            _renderer.flipX = facing == Facing.Left;

            if (!_movement.IsMoving || set.Walk == null || set.Walk.Length == 0)
            {
                _frameTimer = 0f;
                _frameIndex = 0;
                if (set.Idle != null)
                {
                    _renderer.sprite = set.Idle;
                }
                return;
            }

            _frameTimer += Time.deltaTime;
            float frameDuration = 1f / _framesPerSecond;
            while (_frameTimer >= frameDuration)
            {
                _frameTimer -= frameDuration;
                _frameIndex++;
            }

            _renderer.sprite = set.Walk[_frameIndex % set.Walk.Length];
        }
    }
}

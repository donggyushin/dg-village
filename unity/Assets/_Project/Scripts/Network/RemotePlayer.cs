using UnityEngine;

namespace DgVillage
{
    /// <summary>
    /// 다른 사람의 캐릭터. 서버가 보내준 위치로 부드럽게 따라가고, Facing · IsMoving 을 animator 에 넘긴다.
    /// 서버 위치는 0.1초마다 오므로 그대로 꽂으면 뚝뚝 끊겨 보여서 보간(Lerp)한다.
    /// </summary>
    public class RemotePlayer : MonoBehaviour, ICharacterMotion
    {
        [Tooltip("목표 위치로 붙는 속도. 클수록 빨리 붙지만 덜 부드럽다")]
        [SerializeField] private float _followSharpness = 15f;

        [Tooltip("이보다 멀리 떨어지면 보간하지 않고 순간이동")]
        [SerializeField] private float _snapDistance = 5f;

        public string PlayerId { get; private set; }
        public Facing Facing { get; private set; } = Facing.Down;
        public bool IsMoving { get; private set; }
        public bool IsSeated { get; private set; }

        private Vector3 _targetPosition;
        private NameTag _nameTag;
        private DirectionalSpriteAnimator _animator;

        private void Awake()
        {
            _nameTag = GetComponentInChildren<NameTag>();
            _animator = GetComponentInChildren<DirectionalSpriteAnimator>();
        }

        public void Init(PlayerState state, CharacterDefinition character)
        {
            PlayerId = state.id;
            gameObject.name = $"Remote {state.name}";
            if (_nameTag != null)
            {
                _nameTag.SetName(state.name);
            }
            if (_animator != null && character != null)
            {
                _animator.SetCharacter(character);
            }
            ApplyState(state);
            transform.position = _targetPosition;
        }

        public void ApplyState(PlayerState state)
        {
            _targetPosition = new Vector3(state.x, transform.position.y, state.z);
            Facing = FacingWire.Parse(state.facing);
            IsMoving = state.moving;
            IsSeated = state.IsSeated;

            if ((transform.position - _targetPosition).sqrMagnitude > _snapDistance * _snapDistance)
            {
                transform.position = _targetPosition;
            }
        }

        private void Update()
        {
            // 프레임레이트와 무관하게 일정한 비율로 따라가는 지수 보간
            float t = 1f - Mathf.Exp(-_followSharpness * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, _targetPosition, t);
        }
    }
}

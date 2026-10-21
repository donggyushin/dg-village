using UnityEngine;

namespace DgVillage
{
    /// <summary>
    /// ICharacterMotion(PlayerMovement 또는 RemotePlayer)의 Facing · IsMoving 을 읽어
    /// CharacterDefinition 의 방향별 서기/걷기 스프라이트를 재생한다.
    /// Left 는 Right 를 좌우 반전해서 쓴다 (스프라이트 1방향 절약).
    /// Visual(SpriteRenderer) 오브젝트에 붙이고, ICharacterMotion 은 부모에서 찾는다.
    /// 앉아 있으면 캐릭터의 앉은 그림(SitDown)을, 없으면 서기 그림을 위로 올려 쓴다.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class DirectionalSpriteAnimator : MonoBehaviour
    {
        [SerializeField] private CharacterDefinition _character;
        [SerializeField] private float _framesPerSecond = 10f;

        [Tooltip("앉은 그림이 없는 캐릭터가 앉았을 때 서기 그림을 올리는 높이 (unit). 책상 · 소파 뒤에서 상반신이 보이도록 (32 PPU 기준 정수 픽셀)")]
        [SerializeField] private float _seatedLift = 12f / 32f;

        [Tooltip("앉은 그림을 올리는 높이 (unit). 0 = 발이 의자 앞 바닥에 닿는다 (책상 자리에선 머리만 보인다)")]
        [SerializeField] private float _sitSpriteLift = 0f;

        private Vector3 _baseLocalPosition;

        private SpriteRenderer _renderer;
        private ICharacterMotion _motion;
        private float _frameTimer;
        private int _frameIndex;

        public CharacterDefinition Character => _character;

        private void Awake()
        {
            _renderer = GetComponent<SpriteRenderer>();
            _motion = GetComponentInParent<ICharacterMotion>();
            _baseLocalPosition = transform.localPosition;
            // 해가 있는 맵(MapLighting)에선 캐릭터도 바닥에 그림자를 드리운다
            if (GetComponent<SunShadow>() == null)
            {
                gameObject.AddComponent<SunShadow>();
            }

            if (_motion == null)
            {
                Debug.LogError("DirectionalSpriteAnimator: 부모에 PlayerMovement 나 RemotePlayer 가 없다.", this);
                enabled = false;
            }
        }

        /// <summary>런타임에 캐릭터를 바꾼다 (캐릭터 선택, 원격 플레이어의 characterId).</summary>
        public void SetCharacter(CharacterDefinition character)
        {
            _character = character;
            _frameTimer = 0f;
            _frameIndex = 0;
        }

        private void Update()
        {
            if (_character == null)
            {
                return;
            }

            Sprite sitDown = _motion.IsSeated ? _character.SitDown : null;
            float lift = !_motion.IsSeated ? 0f : sitDown != null ? _sitSpriteLift : _seatedLift;
            // 스프라이트 면을 따라 위로 (Visual 이 35° 기울어 있어서 로컬 up 방향)
            transform.localPosition = _baseLocalPosition + transform.localRotation * Vector3.up * lift;

            if (sitDown != null)
            {
                _renderer.flipX = false;
                _renderer.sprite = sitDown;
                return;
            }

            Facing facing = _motion.Facing;
            CharacterDefinition.DirectionSprites set = _character.Get(facing);
            _renderer.flipX = facing == Facing.Left;

            if (!_motion.IsMoving || set.Walk == null || set.Walk.Length == 0)
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

#if UNITY_EDITOR
        // 인스펙터에서 캐릭터를 바꾸면 씬 뷰에서도 바로 정면 서기 모습이 보이도록
        private void OnValidate()
        {
            if (_character != null && _character.Preview != null && TryGetComponent(out SpriteRenderer spriteRenderer))
            {
                spriteRenderer.sprite = _character.Preview;
            }
        }
#endif
    }
}

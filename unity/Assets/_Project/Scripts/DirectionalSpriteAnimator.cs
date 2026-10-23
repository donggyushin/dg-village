using UnityEngine;

namespace DgVillage
{
    /// <summary>
    /// ICharacterMotion(PlayerMovement 또는 RemotePlayer)의 Facing · IsMoving 을 읽어
    /// CharacterDefinition 의 방향별 서기/걷기 스프라이트를 재생한다.
    /// 8방향: 왼쪽 계열은 오른쪽 그림을 좌우 반전해서 쓴다 (그림은 5방향만). 대각선이 없는 캐릭터는 위/아래로 대신.
    /// Visual(SpriteRenderer) 오브젝트에 붙이고, ICharacterMotion 은 부모에서 찾는다.
    /// 앉아 있으면 의자 방향의 앉은 그림(GetSit)을, 없으면 그 방향 서기 그림을 위로 올려 쓴다.
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

        [Tooltip("책상 자리(desk)에선 앉은 그림을 이만큼 더 올린다 (unit). 0 이면 책상 위로 머리만 겨우 보인다")]
        [SerializeField] private float _deskSitLift = 10f / 32f;

        [Tooltip("앉았을 때 깊이를 의자에서 떼는 거리 (unit). 의자 · 소파 스프라이트와 같은 자리라 깊이가 같으면 그리는 순서가 들쭉날쭉. " +
                 "카메라를 보면 의자 앞(카메라 쪽), 등을 보이면 의자 뒤(등받이가 가린다). " +
                 "직교 카메라라 화면 위치는 그대로. 책상(의자에서 0.38 이상)보다는 작게")]
        [SerializeField] private float _seatedDepthBias = 0.1f;

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

            // 카메라를 정면으로 (맵마다 각도가 바뀔 수 있다 — ViewAngle)
            transform.localRotation = ViewAngle.Billboard;

            bool sitFlip = false;
            Sprite sit = _motion.IsSeated ? _character.GetSit(_motion.Facing, out sitFlip) : null;
            float lift = 0f;
            float depthBias = 0f;
            if (_motion.IsSeated)
            {
                lift = sit != null ? _sitSpriteLift : _seatedLift;
                // 앉은 자리 = 서버가 맞춰 준 발 위치라 바로 아래에 Seat 가 있다
                Seat seat = Seat.Nearest(transform.parent.position, 0.3f);
                if (seat != null && seat.IsDesk)
                {
                    lift += _deskSitLift;
                }
                depthBias = FacesAway(_motion.Facing) ? -_seatedDepthBias : _seatedDepthBias;
            }
            // 스프라이트 면을 따라 위로 (Visual 이 35° 기울어 있어서 로컬 up 방향), 깊이는 로컬 back (= 카메라 쪽)
            transform.localPosition = _baseLocalPosition + transform.localRotation * (Vector3.up * lift + Vector3.back * depthBias);

            if (sit != null)
            {
                _renderer.flipX = sitFlip;
                _renderer.sprite = sit;
                return;
            }

            Facing facing = _motion.Facing;
            CharacterDefinition.DirectionSprites set = _character.Get(facing, out bool flipX);
            _renderer.flipX = flipX;

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

        private static bool FacesAway(Facing facing) =>
            facing is Facing.Up or Facing.UpLeft or Facing.UpRight;

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

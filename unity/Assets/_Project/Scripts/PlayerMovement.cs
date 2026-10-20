using UnityEngine;
using UnityEngine.InputSystem;

namespace DgVillage
{
    public enum Facing
    {
        Down,
        Up,
        Left,
        Right,
    }

    /// <summary>
    /// 키보드(WASD · 방향키) / 게임패드 입력으로 바닥(XZ 평면) 위를 이동한다.
    /// 이동 방향으로 4방향 Facing 을 계산해 둔다 — 나중에 방향별 걷기 스프라이트가 이 값을 쓴다.
    /// </summary>
    public class PlayerMovement : MonoBehaviour, ICharacterMotion
    {
        [SerializeField] private float _moveSpeed = 4f;

        [Tooltip("비워두면 Project-wide Input Actions 의 Player/Move 를 사용")]
        [SerializeField] private InputActionReference _moveActionReference;

        private InputAction _moveAction;

        public Facing Facing { get; private set; } = Facing.Down;
        public bool IsMoving { get; private set; }

        /// <summary>true 면 이동 입력을 무시한다 (채팅 입력 중 WASD 가 캐릭터를 움직이지 않게)</summary>
        public bool InputBlocked { get; set; }

        private void Awake()
        {
            _moveAction = _moveActionReference != null
                ? _moveActionReference.action
                : InputSystem.actions?.FindAction("Player/Move");

            if (_moveAction == null)
            {
                Debug.LogError("Move 액션을 찾지 못했다. Inspector 에서 Move Action Reference 에 Player/Move 를 연결할 것.", this);
                enabled = false;
            }
        }

        // Project-wide 액션은 다른 곳과 공유되므로 OnDisable 에서 Disable 하지 않는다
        private void OnEnable() => _moveAction?.Enable();

        private void Update()
        {
            if (InputBlocked)
            {
                IsMoving = false;
                return;
            }

            Vector2 input = Vector2.ClampMagnitude(_moveAction.ReadValue<Vector2>(), 1f);
            IsMoving = input.sqrMagnitude > 0.01f;

            if (!IsMoving)
            {
                return;
            }

            // 화면 위쪽(입력 y+) = 월드 +Z (카메라가 -Z 쪽에서 +Z 를 바라보므로)
            var delta = new Vector3(input.x, 0f, input.y) * (_moveSpeed * Time.deltaTime);
            transform.position += delta;

            Facing = Mathf.Abs(input.x) > Mathf.Abs(input.y)
                ? (input.x > 0f ? Facing.Right : Facing.Left)
                : (input.y > 0f ? Facing.Up : Facing.Down);
        }
    }
}

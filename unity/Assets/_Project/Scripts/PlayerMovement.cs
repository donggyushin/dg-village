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

        [Tooltip("벽과 이만큼(unit) 떨어져서 멈춘다 (발 둘레 반지름)")]
        [SerializeField] private float _bodyRadius = 0.25f;

        [Tooltip("비워두면 Project-wide Input Actions 의 Player/Move 를 사용")]
        [SerializeField] private InputActionReference _moveActionReference;

        private InputAction _moveAction;

        public Facing Facing { get; private set; } = Facing.Down;
        public bool IsMoving { get; private set; }

        /// <summary>true 면 이동 입력을 무시한다 (채팅 입력 중 WASD 가 캐릭터를 움직이지 않게)</summary>
        public bool InputBlocked { get; set; }

        /// <summary>true 면 움직이지 않는다 (서버 입장 전: 첫 welcome 이 정한 위치로 옮겨지므로 미리 걸어 봐야 소용없다). MapLoader 가 쓴다</summary>
        public bool WaitingForServer { get; set; }

        /// <summary>앉아 있는 동안 true. 움직이지 않고, 이동 키는 WantsToStand 로만 알린다 (SeatInteraction 이 서버에 일어나기 요청)</summary>
        public bool Seated { get; private set; }

        bool ICharacterMotion.IsSeated => Seated;

        /// <summary>앉아 있는데 이동 키를 누르고 있다</summary>
        public bool WantsToStand { get; private set; }

        /// <summary>좌석 위치로 옮겨 앉힌다 (서버가 앉기를 허락했을 때). 정면(카메라)을 본다</summary>
        public void SitAt(Vector3 position)
        {
            transform.position = position;
            Facing = Facing.Down;
            IsMoving = false;
            Seated = true;
        }

        public void StandUp()
        {
            Seated = false;
            WantsToStand = false;
        }

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
            if (InputBlocked || WaitingForServer)
            {
                IsMoving = false;
                return;
            }

            Vector2 input = Vector2.ClampMagnitude(_moveAction.ReadValue<Vector2>(), 1f);
            if (Seated)
            {
                IsMoving = false;
                WantsToStand = input.sqrMagnitude > 0.01f;
                return;
            }
            IsMoving = input.sqrMagnitude > 0.01f;

            if (!IsMoving)
            {
                return;
            }

            // 화면 위쪽(입력 y+) = 월드 +Z (카메라가 -Z 쪽에서 +Z 를 바라보므로)
            var delta = new Vector3(input.x, 0f, input.y) * (_moveSpeed * Time.deltaTime);
            transform.position = Constrain(transform.position, delta);

            Facing = Mathf.Abs(input.x) > Mathf.Abs(input.y)
                ? (input.x > 0f ? Facing.Right : Facing.Left)
                : (input.y > 0f ? Facing.Up : Facing.Down);
        }

        // 바닥(FloorMesh) 밖이나 가구(Obstacle) 안으로는 못 간다.
        // 축마다 따로 막아서 비스듬히 부딪히면 벽 · 가구를 따라 미끄러진다
        private Vector3 Constrain(Vector3 position, Vector3 delta)
        {
            // 이미 가구와 겹친 채 시작했으면(배치 실수 등) 가구는 무시해서 빠져나올 수 있게 한다
            bool checkObstacles = !Obstacle.Blocks(position, _bodyRadius);
            var alongX = new Vector3(position.x + delta.x, position.y, position.z);
            if (CanStand(alongX, checkObstacles))
            {
                position = alongX;
            }
            var alongZ = new Vector3(position.x, position.y, position.z + delta.z);
            if (CanStand(alongZ, checkObstacles))
            {
                position = alongZ;
            }
            return position;
        }

        private bool CanStand(Vector3 position, bool checkObstacles)
        {
            FloorMesh floor = FloorMesh.Current;
            return (floor == null || floor.IsWalkable(position, _bodyRadius))
                && !(checkObstacles && Obstacle.Blocks(position, _bodyRadius));
        }
    }
}

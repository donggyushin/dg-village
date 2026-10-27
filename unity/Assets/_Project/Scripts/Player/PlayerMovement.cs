using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour
{
    [SerializeField] private float _moveSpeed = 4f;

    private Rigidbody _rigidbody;
    private InputAction _moveAction;
    private Transform _camera;
    private Vector3 _moveDirection;

    void Awake()
    {
        _rigidbody = GetComponent<Rigidbody>();
        _moveAction = InputSystem.actions.FindAction("Player/Move");
        _camera = Camera.main.transform;
    }

    // Update is called once per frame
    void Update()
    {
        Vector2 input = _moveAction.ReadValue<Vector2>();
        // 카메라 방향을 바닥(XZ 평면)에 눌러 펼친다 → 화면 위 / 화면 오른쪽
        Vector3 screenUp = Vector3.ProjectOnPlane(_camera.forward, Vector3.up).normalized;
        Vector3 screenRight = Vector3.ProjectOnPlane(_camera.right, Vector3.up).normalized;

        // 대각선이 √2배 빨라지지 않게 길이를 1 이하로 자른다
        _moveDirection = Vector3.ClampMagnitude(screenRight * input.x + screenUp * input.y, 1f);
    }

    void FixedUpdate()
    {
        Vector3 velocity = _moveDirection * _moveSpeed;
        velocity.y = _rigidbody.linearVelocity.y; // 중력으로 떨어지는 속도는 유지
        _rigidbody.linearVelocity = velocity;

    }
}

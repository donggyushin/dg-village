using UnityEngine;

public class CharacterSpriteView : MonoBehaviour
{
    [SerializeField] private CharacterSpriteSet _spriteSet;
    [SerializeField] private float _minMoveSpeed = 0.1f;

    private SpriteRenderer _spriteRenderer;
    private Transform _camera;
    private Vector3 _lastPosition;
    private Direction _direction = Direction.South;

    private void Awake()
    {
        _spriteRenderer = GetComponent<SpriteRenderer>();
        _camera = Camera.main.transform;
        _lastPosition = transform.position;
    }

    public void SetSpriteSet(CharacterSpriteSet spriteSet)
    {
        _spriteSet = spriteSet;
        Refresh();
    }

    private void LateUpdate()
    {
        Vector3 delta = transform.position - _lastPosition;
        _lastPosition = transform.position;
        delta.y = 0f; // 계단을 오르내리는 높이 변화는 방향과 무관

        // 기준 속도보다 느리면(멈춤, 미세한 흔들림) 마지막 방향을 유지
        float minDistance = _minMoveSpeed * Time.deltaTime;
        if (delta.sqrMagnitude > minDistance * minDistance)
        {
            _direction = ToScreenDirection(delta);
        }
        Refresh();
    }

    private Direction ToScreenDirection(Vector3 worldDelta)
    {
        Vector3 screenUp = Vector3.ProjectOnPlane(_camera.forward, Vector3.up).normalized;
        float x = Vector3.Dot(worldDelta, _camera.right);
        float y = Vector3.Dot(worldDelta, screenUp);

        // 화면 아래 = 0°, 오른쪽 = 90°, 위 = 180°, 왼쪽 = -90°
        float angle = Mathf.Atan2(x, -y) * Mathf.Rad2Deg;
        int index = Mathf.RoundToInt(angle / 45f);
        return (Direction)((index + 8) % 8);
    }

    private void Refresh()
    {
        if (_spriteSet != null)
        {
            _spriteRenderer.sprite = _spriteSet.Idle.Get(_direction);
        }
    }
}

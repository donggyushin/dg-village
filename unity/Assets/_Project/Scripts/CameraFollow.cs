using UnityEngine;

namespace DgVillage
{
    /// <summary>
    /// 고정 각도를 유지한 채 대상을 따라간다.
    /// 씬에 배치한 카메라와 대상 사이의 거리(offset)를 Awake 에서 기억해 그대로 유지한다.
    /// 첫 프레임은 부드럽게 따라가지 않고 바로 붙는다 (포탈로 와서 대상이 Start 에서 순간이동한 경우).
    /// </summary>
    public class CameraFollow : MonoBehaviour
    {
        [SerializeField] private Transform _target;

        [Tooltip("0 이면 즉시 따라감. 클수록 부드럽게(느리게) 따라감")]
        [SerializeField, Range(0f, 0.5f)] private float _smoothTime = 0.1f;

        private Vector3 _offset;
        private Vector3 _velocity;
        private bool _snapped;

        // 모든 Start(NetworkClient 가 도착 위치로 옮기는 곳)보다 먼저: 씬에 놓인 그대로의 거리
        private void Awake()
        {
            if (_target == null)
            {
                Debug.LogError("CameraFollow: Target 이 비어 있다. Inspector 에서 Player 를 연결할 것.", this);
                enabled = false;
                return;
            }

            _offset = transform.position - _target.position;
        }

        // 대상 이동(Update)이 끝난 뒤에 따라가야 떨림이 없다
        private void LateUpdate()
        {
            Vector3 desired = _target.position + _offset;
            transform.position = _smoothTime > 0f && _snapped
                ? Vector3.SmoothDamp(transform.position, desired, ref _velocity, _smoothTime)
                : desired;
            _snapped = true;
        }
    }
}

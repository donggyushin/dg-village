using UnityEngine;

namespace DgVillage
{
    /// <summary>
    /// 고정 각도를 유지한 채 대상을 따라간다.
    /// 씬에 배치한 카메라와 대상 사이의 거리(offset)를 Awake 에서 기억해 그대로 유지한다.
    /// 첫 프레임과 Snap() 다음 프레임은 부드럽게 따라가지 않고 바로 붙는다 (포탈로 순간이동한 경우).
    /// </summary>
    public class CameraFollow : MonoBehaviour
    {
        [SerializeField] private Transform _target;

        [Tooltip("0 이면 즉시 따라감. 클수록 부드럽게(느리게) 따라감")]
        [SerializeField, Range(0f, 0.5f)] private float _smoothTime = 0.1f;

        private Vector3 _offset;
        private Vector3 _velocity;
        private bool _snapped;

        // 대상이 옮겨지기 전에: 씬에 놓인 그대로의 거리
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

        /// <summary>다음 LateUpdate 에서 대상에 바로 붙는다 (대상이 순간이동했을 때)</summary>
        public void Snap() => _snapped = false;

        /// <summary>카메라 각도를 바꾼다 (맵마다 정면 · 쿼터뷰). 대상과의 거리는 그대로, 그 각도의 반대 방향으로 물러난다</summary>
        public void SetRotation(Quaternion rotation)
        {
            transform.rotation = rotation;
            _offset = -(rotation * Vector3.forward) * _offset.magnitude;
            _snapped = false;
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

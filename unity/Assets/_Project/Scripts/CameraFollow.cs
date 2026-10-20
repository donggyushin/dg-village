using UnityEngine;

namespace DgVillage
{
    /// <summary>
    /// 고정 각도를 유지한 채 대상을 따라간다.
    /// 씬에 배치한 카메라와 대상 사이의 거리(offset)를 시작 시점에 기억해 그대로 유지한다.
    /// </summary>
    public class CameraFollow : MonoBehaviour
    {
        [SerializeField] private Transform _target;

        [Tooltip("0 이면 즉시 따라감. 클수록 부드럽게(느리게) 따라감")]
        [SerializeField, Range(0f, 0.5f)] private float _smoothTime = 0.1f;

        private Vector3 _offset;
        private Vector3 _velocity;

        private void Start()
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
            transform.position = _smoothTime > 0f
                ? Vector3.SmoothDamp(transform.position, desired, ref _velocity, _smoothTime)
                : desired;
        }
    }
}

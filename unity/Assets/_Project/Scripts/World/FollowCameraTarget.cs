using Unity.Cinemachine;
using UnityEngine;

public class FollowCameraTarget : MonoBehaviour
{
    [SerializeField] private CinemachineCamera _camera;

    private float _height = 10f;

    void LateUpdate()
    {
        var target = _camera.Target.TrackingTarget;
        if (target == null) return;

        transform.position = target.position + Vector3.up * _height;
    }
}

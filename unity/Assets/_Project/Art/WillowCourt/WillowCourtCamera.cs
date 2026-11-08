using UnityEngine;

// Used only by the standalone Willow Court scene; keeps the isometric angle fixed.
public sealed class WillowCourtCamera : MonoBehaviour
{
    [SerializeField] private Transform _target;
    [SerializeField] private Vector3 _compositionOffset = new(.2f, 1.08f, 4.3f);
    private Vector3 _velocity;

    private void LateUpdate()
    {
        if (!_target) return;
        Vector3 focus = _target.position + _compositionOffset;
        focus.x = Mathf.Clamp(focus.x, -3f, 4f);
        focus.y = 1.2f;
        focus.z = Mathf.Clamp(focus.z, -10f, 6f);
        Vector3 destination = focus - transform.forward * 25f;
        transform.position = Vector3.SmoothDamp(transform.position, destination, ref _velocity, .28f);
    }
}

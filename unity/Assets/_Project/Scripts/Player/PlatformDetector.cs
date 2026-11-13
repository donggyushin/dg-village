using UnityEngine;

// 발 아래가 platform 인지 검사한다.
public class PlatformDetector : MonoBehaviour
{
    [SerializeField] private float _rayStartHeight = 0.5f;
    [SerializeField] private float _rayLength = 0.8f;

    public Platform CurrentPlatform { get; private set; }

    void FixedUpdate()
    {
        Vector3 origin = transform.position + Vector3.up * _rayStartHeight;
        Platform found = null;

        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, _rayLength, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            found = hit.collider.GetComponentInParent<Platform>();
        }

        if (found == CurrentPlatform) return;

        if (found != null)
        {
            Debug.Log($"배에 탔음: {found.PlatformId}");
        }
        else
        {
            Debug.Log($"배에서 내림: {CurrentPlatform.PlatformId}");
        }

        CurrentPlatform = found;
    }

    void OnDrawGizmosSelected()
    {
        Vector3 origin = transform.position + Vector3.up * _rayStartHeight;
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(origin, origin + Vector3.down * _rayLength);
    }
}

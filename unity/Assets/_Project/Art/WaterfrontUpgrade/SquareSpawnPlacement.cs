using UnityEngine;

/// <summary>Projects saved arrival positions onto the remodeled square's walkable surfaces.</summary>
public sealed class SquareSpawnPlacement : MonoBehaviour
{
    [SerializeField] private Collider[] _surfaces;
    [SerializeField] private Vector3 _fallback;

    public Vector3 Resolve(Vector3 savedPosition)
    {
        float highest = float.NegativeInfinity;
        if (_surfaces == null) return _fallback;
        foreach (var surface in _surfaces)
        {
            if (!surface || !surface.enabled || !surface.gameObject.activeInHierarchy) continue;
            var bounds = surface.bounds;
            var origin = new Vector3(savedPosition.x, bounds.max.y + 2, savedPosition.z);
            if (surface.Raycast(new Ray(origin, Vector3.down), out var hit, bounds.size.y + 4) && hit.normal.y > .5f)
                highest = Mathf.Max(highest, hit.point.y);
        }
        // An old XZ coordinate may now be in the canal after the map expansion.
        return float.IsNegativeInfinity(highest)
            ? _fallback
            : new Vector3(savedPosition.x, highest + .12f, savedPosition.z);
    }
}

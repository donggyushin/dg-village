using System.Collections.Generic;
using UnityEngine;

// 카메라와 이 캐릭터 사이에서 캐릭터를 가리는 Occluder 를 찾는다.
public class OcclusionDetector : MonoBehaviour
{
    [SerializeField] private LayerMask _occluderMask;
    [SerializeField] private float _targetHeight = 0.8f;
    [SerializeField] private float _rayLength = 50f;
    [SerializeField] private Material _fadeTemplate;

    private readonly HashSet<Occluder> _current = new HashSet<Occluder>();
    private readonly HashSet<Occluder> _previous = new HashSet<Occluder>();


    private Camera _camera;

    private void Start()
    {
        _camera = Camera.main;
    }

    private void LateUpdate()
    {
        Vector3 direction = _camera.transform.forward;
        Vector3 target = transform.position + Vector3.up * _targetHeight;
        Vector3 origin = target - direction * _rayLength;

        RaycastHit[] hits = Physics.RaycastAll(
            origin, direction, _rayLength, _occluderMask, QueryTriggerInteraction.Collide);

        Debug.DrawLine(origin, target, hits.Length > 0 ? Color.red : Color.green);

        _current.Clear();
        foreach (RaycastHit hit in hits)
        {
            Occluder occluder = hit.collider.GetComponentInParent<Occluder>();
            if (occluder != null) _current.Add(occluder);
        }

        foreach (Occluder occluder in _current)
            if (!_previous.Contains(occluder)) occluder.SetOccluding(true, _fadeTemplate);

        foreach (Occluder occluder in _previous)
            if (occluder != null && !_current.Contains(occluder)) occluder.SetOccluding(false, _fadeTemplate);

        _previous.Clear();
        _previous.UnionWith(_current);
    }
}
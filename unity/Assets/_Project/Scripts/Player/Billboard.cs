using UnityEngine;

public class Billboard : MonoBehaviour
{
    private Transform _camera;

    void Awake()
    {
        _camera = Camera.main.transform;
    }

    void LateUpdate()
    {
        transform.rotation = _camera.rotation;
    }
}

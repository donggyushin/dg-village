using UnityEngine;

public class AutoGate : MonoBehaviour
{
    [SerializeField] private MeshRenderer _renderer;
    [SerializeField] private Material _closed;
    [SerializeField] private Material _opened;
    [SerializeField] private GameObject _doorCollider;

    void OnTriggerEnter(Collider other)
    {
        Debug.Log("1");
        if (other.CompareTag("Player") || other.CompareTag("OtherPlayer"))
        {
            SetOpen(true);
            Debug.Log("2");
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player") || other.CompareTag("OtherPlayer"))
        {
            SetOpen(false);
        }
    }

    private void SetOpen(bool open)
    {
        _renderer.sharedMaterial = open ? _opened : _closed;
        _doorCollider.SetActive(!open);
    }
}

using UnityEngine;

public class PlayerPositionSender : MonoBehaviour
{
    [SerializeField] private float _sendInterval = 0.1f; // 10HZ
    [SerializeField] private float _minDistance = 0.01f;

    private float _timer;
    private Vector3 _lastSent;

    void Start()
    {
        _lastSent = transform.position;
    }

    void Update()
    {
        _timer += Time.deltaTime;
        if (_timer < _sendInterval) return;
        _timer = 0f;

        if ((transform.position - _lastSent).sqrMagnitude < _minDistance * _minDistance) return;

        _lastSent = transform.position;
        GameSocket.Instance.SendMove(_lastSent);
    }
}
using UnityEngine;

public class RemotePlayer : MonoBehaviour
{
    [SerializeField] private float _sendInterval = 0.1f; // 상대 PlayerPositionSender 주기와 같게
    [SerializeField] private float _snapDistance = 5f; // 이보다 멀면 보간 없이 순간이동

    private Vector3 _target;
    private float _speed;

    void Awake()
    {
        _target = transform.position;
    }

    public void SetTarget(Vector3 target)
    {
        float distance = Vector3.Distance(transform.position, target);
        if (distance > _snapDistance)
        {
            transform.position = target;
        }

        // 다음 좌표가 올 즈음(_sendInterval 뒤) 딱 도착하는 속도
        _speed = distance / _sendInterval;
        _target = target;
    }

    void Update()
    {
        transform.position = Vector3.MoveTowards(transform.position, _target, _speed * Time.deltaTime);
    }
}
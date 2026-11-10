using UnityEngine;

public class WeatherManager : MonoBehaviour
{

    [SerializeField] private ParticleSystem _rain;
    void Awake()
    {
        // Python server 에 현재 날씨 물어보기. 아직은 호출할 필요 x. 눈, 비 등 모든 날씨 구현 이후에 연결할 예정.
    }
    [ContextMenu("Rain")]
    public void Rain()
    {
        // 비오는 날씨
        _rain.Play();
    }

    [ContextMenu("Normal")]
    public void Normal()
    {
        // 평범한 날씨
        _rain.Stop();
    }
}

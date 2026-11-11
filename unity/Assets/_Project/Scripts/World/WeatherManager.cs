using System.Collections;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public class WeatherManager : MonoBehaviour
{

    private class WeatherInfo
    {
        public string weather;
    }

    [SerializeField] private ParticleSystem _rain;
    [SerializeField] private Volume _rainVolume;
    [SerializeField] private float _fadeDuration = 3f;
    [SerializeField] private AudioSource _rainSound;
    [SerializeField, Range(0f, 1f)] private float _rainSoundVolume = 0.5f;

    private float _targetWeight;
    private float _targetVolume;

    void Start()
    {
        StartCoroutine(FetchWeather());
    }

    void Update()
    {
        _rainVolume.weight = Mathf.MoveTowards(_rainVolume.weight, _targetWeight, Time.deltaTime / _fadeDuration);

        // (기존 _rainVolume.weight 줄은 그대로)

        _rainSound.volume = Mathf.MoveTowards(
            _rainSound.volume, _targetVolume, Time.deltaTime / _fadeDuration);

        if (_targetVolume == 0f && _rainSound.volume == 0f && _rainSound.isPlaying)
            _rainSound.Stop();   // 다 작아진 뒤에야 멈춤
    }

    private IEnumerator FetchWeather()
    {
        string scene = SceneManager.GetActiveScene().name;
        string url = $"{ServerConfig.BaseUrl}/scenes/{scene}/weather";

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"날씨 요청 실패: {request.error}");
                Normal();
                yield break;
            }

            WeatherInfo info = JsonConvert.DeserializeObject<WeatherInfo>(request.downloadHandler.text);

            if (info.weather == "rain")
                Rain();
            else
                Normal();
        }
    }

    [ContextMenu("Rain")]
    public void Rain()
    {
        // 비오는 날씨
        _rain.Play();
        _targetWeight = 0.8f;

        if (!_rainSound.isPlaying)
        {
            _rainSound.volume = 0f;   // 0 에서 시작해 서서히 커지게
            _rainSound.Play();
        }
        _targetVolume = _rainSoundVolume;
    }

    [ContextMenu("Normal")]
    public void Normal()
    {
        // 평범한 날씨
        _rain.Stop();
        _targetWeight = 0;
        _targetVolume = 0f;
    }
}

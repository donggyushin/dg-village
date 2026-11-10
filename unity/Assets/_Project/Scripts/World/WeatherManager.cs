using System.Collections;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

public class WeatherManager : MonoBehaviour
{

    private class WeatherInfo
    {
        public string weather;
    }

    [SerializeField] private ParticleSystem _rain;

    void Start()
    {
        StartCoroutine(FetchWeather());
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
    }

    [ContextMenu("Normal")]
    public void Normal()
    {
        // 평범한 날씨
        _rain.Stop();
    }
}

using System;
using UnityEngine;
using UnityEngine.Rendering;

public class DayLightManager : MonoBehaviour
{
    [SerializeField] private Light _sun;

    [SerializeField] private Gradient _ambientSkyColor;      // 0=자정, 0.25=6시, 0.5=정오
    [SerializeField] private Gradient _ambientEquatorColor;  // ← 옆면 (벽, 건물 그림)
    [SerializeField] private Gradient _ambientGroundColor;   // ← 아랫면
    [SerializeField] private Gradient _sunColor;
    [SerializeField] private AnimationCurve _sunIntensity;   // x: 0~1(하루), y: 밝기
    [SerializeField] private float _updateInterval = 500f;   // 초


    private float _nextUpdateTime;

    private void Update()
    {
        if (Time.time < _nextUpdateTime) return;
        _nextUpdateTime = Time.time + _updateInterval;
        ApplyTime(GetCurrentHour());
    }

    private void Start()
    {
        ApplyTime(GetCurrentHour());
    }

    private float GetCurrentHour()
    {

        DateTime now = DateTime.Now;
        return now.Hour + now.Minute / 60f + now.Second / 3600f;
    }

    private void ApplyTime(float hour)  // hour: 0~24
    {
        float t = hour / 24f;

        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = _ambientSkyColor.Evaluate(t);
        RenderSettings.ambientEquatorColor = _ambientEquatorColor.Evaluate(t);
        RenderSettings.ambientGroundColor = _ambientGroundColor.Evaluate(t);

        _sun.intensity = _sunIntensity.Evaluate(t);
        _sun.color = _sunColor.Evaluate(t);
    }
}

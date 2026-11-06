using System.Collections;
using UnityEngine;

public class BgmManager : MonoBehaviour
{
    private AudioSource _audioSource;
    private float _fadeInDuration = 3;
    private float _targetVolume = 0.3f;

    void Awake()
    {
        _audioSource = GetComponent<AudioSource>();
        _audioSource.Play();
        _audioSource.loop = true;
        _audioSource.volume = 0;
        StartCoroutine(FadeIn());
    }

    private IEnumerator FadeIn()
    {
        float time = 0f;
        while (time < _fadeInDuration)
        {
            time += Time.deltaTime;
            float t = time / _fadeInDuration;              // 0 → 1
            _audioSource.volume = Mathf.Lerp(0f, _targetVolume, t * t);
            yield return null;                             // 다음 프레임까지 대기
        }
        _audioSource.volume = _targetVolume;               // 마지막 값을 정확히 맞추기
    }
}

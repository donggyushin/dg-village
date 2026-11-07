using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;

public class AudioMixPanelManager : MonoBehaviour
{

    [SerializeField] private Button _toggleButton;
    [SerializeField] private Slider _slider;
    [SerializeField] private AudioMixer _mixer;
    [SerializeField] private GameObject _panel;
    private bool _active = false;

    private const string MixerParam = "BgmVolume";  // Exposed Parameter 이름과 철자까지 같아야 함
    private const string PrefsKey = "bgm_volume";

    void Start()
    {
        float saved = PlayerPrefs.GetFloat(PrefsKey, 0.3f);
        _slider.value = saved;
        _toggleButton.onClick.AddListener(ToggleButtonTapped);
        _slider.onValueChanged.AddListener(SliderValueChanged);
        _panel.SetActive(_active);
        ApplyVolume(saved);
    }

    private void ToggleButtonTapped()
    {
        _active = !_active;
        _panel.SetActive(_active);
    }

    private void SliderValueChanged(float value)
    {
        PlayerPrefs.SetFloat(PrefsKey, value);
        ApplyVolume(value);
    }

    private void ApplyVolume(float volume)
    {
        _mixer.SetFloat(MixerParam, Mathf.Log10(volume) * 20f);
    }
}

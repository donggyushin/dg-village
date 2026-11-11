using System;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;

public class AudioMixPanelManager : MonoBehaviour
{

    [SerializeField] private Button _toggleButton;
    [SerializeField] private Slider _masterSlider;
    [SerializeField] private Slider _bgmSlider;
    [SerializeField] private Slider _ambinceSlider;
    [SerializeField] private AudioMixer _mixer;
    [SerializeField] private GameObject _panel;

    private bool _active = false;

    private const string BGMMixerParam = "BgmVolume";  // Exposed Parameter 이름과 철자까지 같아야 함
    private const string MasterMixerParam = "MasterVolume";  // Exposed Parameter 이름과 철자까지 같아야 함
    private const string AmbienceMixerParam = "AmbienceVolume";
    private const string BGMPrefsKey = "bgm_volume";
    private const string MasterPrefsKey = "master_volume";
    private const string AmbiencePrefsKey = "ambience_volume";

    void Start()
    {
        float bgmSaved = PlayerPrefs.GetFloat(BGMPrefsKey, 0.3f);
        _bgmSlider.value = bgmSaved;
        _bgmSlider.onValueChanged.AddListener(BGMSliderValueChanged);

        float masterSaved = PlayerPrefs.GetFloat(MasterPrefsKey, 1);
        _masterSlider.value = masterSaved;
        _masterSlider.onValueChanged.AddListener(MasterSliderValueChanged);

        float ambienceSaved = PlayerPrefs.GetFloat(AmbiencePrefsKey, 0.5f);
        _ambinceSlider.value = ambienceSaved;
        _ambinceSlider.onValueChanged.AddListener(AmbienceSliderValueChanged);

        _panel.SetActive(_active);

        ApplyBGMVolume(bgmSaved);
        ApplyMasterVolume(masterSaved);
        ApplyAmbienceVolume(ambienceSaved);

        _toggleButton.onClick.AddListener(ToggleButtonTapped);
    }

    private void ToggleButtonTapped()
    {
        _active = !_active;
        _panel.SetActive(_active);
    }

    private void BGMSliderValueChanged(float value)
    {
        PlayerPrefs.SetFloat(BGMPrefsKey, value);
        ApplyBGMVolume(value);
    }

    private void ApplyBGMVolume(float volume)
    {
        _mixer.SetFloat(BGMMixerParam, Mathf.Log10(volume) * 20f);
    }

    private void MasterSliderValueChanged(float value)
    {
        PlayerPrefs.SetFloat(MasterPrefsKey, value);
        ApplyMasterVolume(value);
    }

    private void ApplyMasterVolume(float volume)
    {
        _mixer.SetFloat(MasterMixerParam, Mathf.Log10(volume) * 20f);
    }

    private void AmbienceSliderValueChanged(float value)
    {
        PlayerPrefs.SetFloat(AmbiencePrefsKey, value);
        ApplyAmbienceVolume(value);
    }

    private void ApplyAmbienceVolume(float value)
    {
        _mixer.SetFloat(AmbienceMixerParam, Mathf.Log10(value) * 20f);
    }
}

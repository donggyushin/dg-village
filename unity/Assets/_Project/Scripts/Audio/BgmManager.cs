using UnityEngine;

public class BgmManager : MonoBehaviour
{
    private AudioSource _audioSource;

    void Awake()
    {
        _audioSource = GetComponent<AudioSource>();
        _audioSource.Play();
        _audioSource.loop = true;
        _audioSource.volume = 1;
    }
}

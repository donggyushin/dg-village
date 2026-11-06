using UnityEngine;
using UnityEngine.UI;

public class WindowButton : MonoBehaviour
{
    [SerializeField] private int _width = 1280;
    [SerializeField] private int _height = 720;
    [SerializeField] private Button _button;

    void Awake()
    {
        _button = GetComponentInChildren<Button>();
        _button.onClick.AddListener(ButtonTapped);
    }

    private void ButtonTapped()
    {
        if (Screen.fullScreen)
        {
            SetWindowed();
        }
        else
        {
            SetFullscreen();
        }
    }

    private void SetFullscreen()
    {
        Resolution monitor = Screen.currentResolution;   // 모니터 해상도
        Screen.SetResolution(monitor.width, monitor.height, FullScreenMode.FullScreenWindow);
    }

    private void SetWindowed()
    {
        Screen.SetResolution(_width, _height, FullScreenMode.Windowed);
    }
}

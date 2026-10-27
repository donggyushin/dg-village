using System.Collections;
using TMPro;
using UnityEngine;

public class Toast : MonoBehaviour
{
    [SerializeField] private CanvasGroup _canvasGroup;
    [SerializeField] private TMP_Text _message;
    private float _animationDuration = 0.3f;
    private float _showDuration = 3f;

    private Coroutine _routine;

    void Awake()
    {
        _canvasGroup.alpha = 0f;
    }

    public void Show(string message)
    {
        _message.text = message;

        if (_routine != null)
        {
            StopCoroutine(_routine);
        }

        _routine = StartCoroutine(ShowRoutine());
    }

    private IEnumerator ShowRoutine()
    {
        yield return Fade(1f);
        yield return new WaitForSeconds(_showDuration);
        yield return Fade(0f);
        _routine = null;
    }

    private IEnumerator Fade(float target)
    {
        float start = _canvasGroup.alpha;
        float time = 0f;

        while (time < _animationDuration)
        {
            time += Time.deltaTime;
            _canvasGroup.alpha = Mathf.Lerp(start, target, time / _animationDuration);
            yield return null; // 여기서 멈추고 다음 프레임에 이어서 진행해
        }

        _canvasGroup.alpha = target;
    }
}

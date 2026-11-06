using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ChatBubble : MonoBehaviour
{
    [SerializeField] private GameObject _background;
    [SerializeField] private TMP_Text _text;
    [SerializeField] private LayoutElement _textLayout;

    private float _maxWidth = 300f;
    private float _showSeconds = 5f;

    private Coroutine _hideRoutine;

    void Awake()
    {
        Hide();
    }

    public void Show(string message)
    {
        _text.text = message;
        float width = _text.GetPreferredValues(message).x; // 줄바꿈 없을 때의 폭
        _textLayout.preferredWidth = Mathf.Min(width, _maxWidth);
        _background.SetActive(true);

        if (_hideRoutine != null)
        {
            StopCoroutine(_hideRoutine);
        }
        _hideRoutine = StartCoroutine(HideAfterDelay());
    }

    private IEnumerator HideAfterDelay()
    {
        yield return new WaitForSeconds(_showSeconds);
        Hide();
    }

    private void Hide()
    {
        _hideRoutine = null;
        _background.SetActive(false);
    }
}

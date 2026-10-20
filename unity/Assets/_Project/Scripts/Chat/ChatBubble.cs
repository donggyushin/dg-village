using TMPro;
using UnityEngine;

namespace DgVillage
{
    /// <summary>
    /// 캐릭터 머리 위 말풍선 하나. 화면(Screen Space) UI 라서 매 프레임 캐릭터의 화면 좌표를 따라간다.
    /// 텍스트 길이에 맞춰 크기를 정하고, 시간이 지나거나 캐릭터가 사라지면 스스로 없어진다.
    /// </summary>
    public class ChatBubble : MonoBehaviour
    {
        [SerializeField] private TMP_Text _text;

        [Tooltip("이보다 길면 줄바꿈 (UI 픽셀 단위)")]
        [SerializeField] private float _maxTextWidth = 240f;

        [SerializeField] private Vector2 _padding = new(12f, 8f);

        [Tooltip("따라갈 대상(이름표) 위로 띄우는 거리 (UI 픽셀 단위)")]
        [SerializeField] private float _offsetY = 6f;

        private RectTransform _rect;
        private Transform _anchor;
        private Camera _camera;
        private Canvas _canvas;
        private float _hideAt;

        private void Awake()
        {
            _rect = (RectTransform)transform;
            _camera = Camera.main;
            _canvas = GetComponentInParent<Canvas>().rootCanvas;
        }

        public void Show(Transform anchor, string message, float duration)
        {
            _anchor = anchor;
            _text.text = message;

            // 한 줄로 쓸 때의 폭이 최대 폭보다 넓으면 최대 폭에서 줄바꿈한 높이를 쓴다
            float width = Mathf.Min(_text.GetPreferredValues(message).x, _maxTextWidth);
            float height = _text.GetPreferredValues(message, width, 0f).y;
            // 계산한 폭과 딱 맞으면 반올림 오차로 마지막 글자가 줄바꿈될 수 있어 1px 여유
            _text.rectTransform.sizeDelta = new Vector2(width + 1f, height);
            _rect.sizeDelta = new Vector2(width + 1f + _padding.x * 2f, height + _padding.y * 2f);

            _hideAt = Time.time + duration;
            FollowAnchor();
        }

        private void LateUpdate()
        {
            if (_anchor == null || Time.time >= _hideAt)
            {
                Destroy(gameObject);
                return;
            }
            FollowAnchor();
        }

        private void FollowAnchor()
        {
            // Screen Space - Overlay 캔버스에서는 화면 픽셀 좌표가 곧 UI 위치
            Vector3 screen = _camera.WorldToScreenPoint(_anchor.position);
            _rect.position = screen + new Vector3(0f, _offsetY * _canvas.scaleFactor, 0f);
        }
    }
}

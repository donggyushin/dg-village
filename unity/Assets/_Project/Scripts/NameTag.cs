using TMPro;
using UnityEngine;

namespace DgVillage
{
    /// <summary>
    /// 캐릭터 머리 위 이름표. Visual 의 자식으로 두면 스프라이트와 같은 각도(카메라를 향함)를 그대로 따른다.
    /// 이름은 PlayerMovement 쪽은 NetworkClient 가, RemotePlayer 쪽은 RemotePlayer.Init 이 넣어준다.
    /// </summary>
    [RequireComponent(typeof(TextMeshPro))]
    public class NameTag : MonoBehaviour
    {
        private TextMeshPro _text;

        private void Awake()
        {
            _text = GetComponent<TextMeshPro>();

            // 이름에 <size=99> 같은 태그를 넣어 장난치지 못하게 한다
            _text.richText = false;

            // 바닥 색과 상관없이 읽히도록 검은 외곽선. 런타임에만 material 복제본이 생긴다
            _text.outlineWidth = 0.25f;
            _text.outlineColor = Color.black;
        }

        public void SetName(string playerName) => _text.text = playerName;

        // 에디터에서 컴포넌트를 처음 붙일 때 한 번 호출된다. 이후 Inspector 에서 자유롭게 바꿔도 된다
        private void Reset()
        {
            var text = GetComponent<TextMeshPro>();
            text.text = "name";
            text.fontSize = 3f;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.Bottom;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.rectTransform.sizeDelta = new Vector2(4f, 1f);

            // 스프라이트 머리(발 기준 약 1.7 유닛) 바로 위
            transform.localPosition = new Vector3(0f, 1.75f, 0f);
            transform.localRotation = Quaternion.identity;
        }
    }
}

using UnityEngine;

namespace DgVillage
{
    /// <summary>
    /// 맵 JSON 의 소품 하나 (MapBuilder 가 소품 루트에 붙인다). 위치는 이 오브젝트의 위치.
    /// 맵 에디터(DgVillage → Map Editor)가 JSON 으로 다시 쓸 때 읽는다.
    /// SelectionBase: Scene 뷰에서 그림(자식 Sprite)을 클릭해도 이 루트가 선택된다 → 끌면 소품 전체가 움직인다.
    /// </summary>
    [SelectionBase]
    public class MapProp : MonoBehaviour
    {
        [Tooltip("PropCatalog id (= Sprites/Environment/Props 의 파일 이름)")]
        [SerializeField] private string _sprite;
        [Tooltip("바닥 그림자 배율, 0 = 없음")]
        [SerializeField] private int _shadow;
        [Tooltip("좌우 반전")]
        [SerializeField] private bool _flip;

        // 지금 그림이 어떤 값으로 만들어졌는지. 인스펙터 · Undo 로 값이 바뀌면 달라진다 → 맵 에디터가 다시 입힌다
        [System.NonSerialized] private string _builtWith;

        private string Signature => $"{_sprite}|{_shadow}|{_flip}";

        /// <summary>값이 그림과 달라졌다 (맵 에디터가 MapBuilder.DressProp 으로 다시 입힌 뒤 MarkBuilt)</summary>
        public bool NeedsRebuild
        {
            get
            {
                // 스크립트 다시 컴파일 뒤엔 기억이 없다 → 지금 그림이 지금 값이라고 본다
                _builtWith ??= Signature;
                return _builtWith != Signature;
            }
        }

        public void MarkBuilt() => _builtWith = Signature;

        public void Init(GameMapData.Prop prop)
        {
            _sprite = prop.sprite;
            _shadow = prop.shadow;
            _flip = prop.flip;
            MarkBuilt();
        }

        public GameMapData.Prop ToData() => new()
        {
            sprite = _sprite,
            x = transform.position.x,
            z = transform.position.z,
            shadow = _shadow,
            flip = _flip,
        };
    }
}

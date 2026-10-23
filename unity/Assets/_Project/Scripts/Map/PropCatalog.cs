using System;
using System.Collections.Generic;
using UnityEngine;

namespace DgVillage
{
    /// <summary>
    /// 맵을 런타임에 깔 때 쓰는 재료 목록 (ScriptableObject, Assets/_Project/Maps/PropCatalog.asset).
    /// 맵 JSON 의 props[].sprite 이름 → 스프라이트 + 바닥에서 막는 크기, 그리고 바닥 · 벽 · 스프라이트 머티리얼.
    /// 서버가 보낸 맵에 새 배치는 얼마든지 올 수 있지만, 그림은 빌드에 들어 있는 이 목록 안에서만 고른다.
    /// 손으로 고치지 말고 DgVillage → Update Prop Catalog (마을 맵 메뉴도 먼저 부른다)로 다시 만든다.
    /// </summary>
    public class PropCatalog : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            public string Id;
            public Sprite Sprite;
            [Tooltip("바닥에서 막는 크기 (x = 가로, y = 깊이) unit. 0 = 안 막음")]
            public Vector2 Footprint;
        }

        [SerializeField] private Material _floorMaterial;
        [Tooltip("쿼터뷰 바닥 (화면 기준 패턴 아틀라스, make_iso_floor.py). 비면 FloorMaterial")]
        [SerializeField] private Material _quarterViewFloorMaterial;
        [SerializeField] private Material _wallMaterial;
        [SerializeField] private Material _spriteMaterial;
        [SerializeField] private Sprite _shadow;
        [SerializeField] private List<Entry> _entries = new();

        public IReadOnlyList<Entry> Entries => _entries;

        public Material FloorMaterial => _floorMaterial;
        public Material QuarterViewFloorMaterial => _quarterViewFloorMaterial != null ? _quarterViewFloorMaterial : _floorMaterial;
        public Material WallMaterial => _wallMaterial;
        public Material SpriteMaterial => _spriteMaterial;
        public Sprite Shadow => _shadow;

        public Entry Find(string id) => _entries.Find(e => e.Id == id);

#if UNITY_EDITOR
        /// <summary>에디터 메뉴(PropCatalogBuilder)가 채운다</summary>
        public void Set(Material floor, Material quarterViewFloor, Material wall, Material sprite, Sprite shadow, List<Entry> entries)
        {
            _floorMaterial = floor;
            _quarterViewFloorMaterial = quarterViewFloor;
            _wallMaterial = wall;
            _spriteMaterial = sprite;
            _shadow = shadow;
            _entries = entries;
        }
#endif
    }
}

using UnityEngine;

namespace DgVillage
{
    /// <summary>
    /// 맵 데이터(서버의 maps/{id}.json = GameMapData)로 방을 만든다. 런타임(MapLoader, 서버 welcome)과
    /// 에디터(씬 미리보기 · MapTest) 둘 다 이걸 쓴다. 만드는 것 — 루트 "Map" 아래:
    ///   Floor   FloorMesh (재질별 타일 바닥 + 가장자리 벽)
    ///   Props   가구 (35° 세운 스프라이트 + 바닥에 눕힌 그림자 + Obstacle = 못 지나감)
    ///   Seats   앉을 자리 (Seat — id 가 서버와 같다)
    ///   Portals 다른 맵으로 가는 영역 (Portal)
    /// </summary>
    public static class MapBuilder
    {
        public const string RootName = "Map";

        // 캐릭터 Visual 과 같은 기울기: 카메라(X 35°)를 정면으로 본다
        private static readonly Quaternion StandUp = Quaternion.Euler(35f, 0f, 0f);

        public static GameObject Build(GameMapData map, PropCatalog catalog)
        {
            var root = new GameObject(RootName).transform;

            var floor = new GameObject("Floor", typeof(MeshFilter), typeof(MeshRenderer), typeof(FloorMesh));
            floor.transform.SetParent(root, false);
            // submesh 0 = 바닥, 1 = 벽 (FloorMesh 가 바닥 끝에 자동으로 만든다)
            floor.GetComponent<MeshRenderer>().sharedMaterials = new[] { catalog.FloorMaterial, catalog.WallMaterial };
            floor.GetComponent<FloorMesh>().SetLayout(string.Join("\n", map.layout));

            var props = Child(root, "Props");
            foreach (GameMapData.Prop prop in map.props ?? new GameMapData.Prop[0])
            {
                PropCatalog.Entry entry = catalog.Find(prop.sprite);
                if (entry == null)
                {
                    // 서버 맵이 이 빌드에 없는 그림을 쓴다 (새 소품은 클라이언트 업데이트가 필요)
                    Debug.LogWarning($"MapBuilder: 소품 '{prop.sprite}' 가 PropCatalog 에 없다 — 건너뜀");
                    continue;
                }
                var item = new GameObject(prop.sprite).transform;
                item.SetParent(props, false);
                item.position = new Vector3(prop.x, 0f, prop.z);
                CreateSprite("Sprite", item, entry.Sprite, catalog.SpriteMaterial);
                if (entry.Footprint != Vector2.zero)
                {
                    item.gameObject.AddComponent<Obstacle>().Init(entry.Footprint);
                }
                if (prop.shadow > 0)
                {
                    CreateShadow(item, prop.shadow, catalog);
                }
            }

            var seats = Child(root, "Seats");
            foreach (GameMapData.Seat seat in map.seats ?? new GameMapData.Seat[0])
            {
                var go = new GameObject(seat.id);
                go.transform.SetParent(seats, false);
                go.transform.position = new Vector3(seat.x, 0f, seat.z);
                go.AddComponent<Seat>().Init(seat.id, seat.kind);
            }

            var portals = Child(root, "Portals");
            foreach (GameMapData.Portal portal in map.portals ?? new GameMapData.Portal[0])
            {
                var go = new GameObject($"to {portal.to}");
                go.transform.SetParent(portals, false);
                go.transform.position = new Vector3(portal.x, 0f, portal.z);
                go.AddComponent<Portal>().Init(portal.to, new Vector2(portal.w, portal.d), new Vector2(portal.spawnX, portal.spawnZ));
            }
            return root.gameObject;
        }

        public static GameObject CreateSprite(string name, Transform parent, Sprite sprite, Material material)
        {
            var go = new GameObject(name, typeof(SpriteRenderer));
            go.transform.SetParent(parent, false);
            go.transform.localRotation = StandUp;
            var renderer = go.GetComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sharedMaterial = material;
            return go;
        }

        private static Transform Child(Transform parent, string name)
        {
            var child = new GameObject(name).transform;
            child.SetParent(parent, false);
            return child;
        }

        // 바닥에 눕힌 타원 그림자. 스프라이트 pivot 이 아래 가운데라 반 높이만큼 당겨 발밑 중앙에 맞춘다
        private static void CreateShadow(Transform parent, int scale, PropCatalog catalog)
        {
            Sprite sprite = catalog.Shadow;
            var go = new GameObject("Shadow", typeof(SpriteRenderer));
            go.transform.SetParent(parent, false);
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            go.transform.localScale = new Vector3(scale, scale, 1f);
            float halfDepth = sprite != null ? sprite.bounds.size.y * scale / 2f : 0f;
            go.transform.localPosition = new Vector3(0f, 0.01f, -halfDepth);
            var renderer = go.GetComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sharedMaterial = catalog.SpriteMaterial;
            // 투명 스프라이트끼리는 거리순으로 그리는데, 그림자는 항상 먼저 (= 캐릭터 · 소품 아래)
            renderer.sortingOrder = -1;
        }
    }
}

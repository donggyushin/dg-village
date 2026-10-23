using System;
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
    ///   Lighting 해 · 구름 그림자와 색감 (MapLighting)
    /// </summary>
    public static class MapBuilder
    {
        public const string RootName = "Map";

        public static GameObject Build(GameMapData map, PropCatalog catalog)
        {
            var root = new GameObject(RootName).transform;

            var floor = new GameObject("Floor", typeof(MeshFilter), typeof(MeshRenderer), typeof(FloorMesh));
            floor.transform.SetParent(root, false);
            // submesh 0 = 바닥, 1 = 벽 (FloorMesh 가 바닥 끝에 자동으로 만든다)
            Material floorMaterial = ViewAngle.IsQuarterView ? catalog.QuarterViewFloorMaterial : catalog.FloorMaterial;
            floor.GetComponent<MeshRenderer>().sharedMaterials = new[] { floorMaterial, catalog.WallMaterial };
            floor.GetComponent<FloorMesh>().SetLayout(string.Join("\n", map.layout));
            floor.GetComponent<FloorMesh>().SetNoWalls(map.noWalls);
            floor.GetComponent<FloorMesh>().SetWindows(Array.ConvertAll(map.windows ?? new GameMapData.Window[0], w => new Vector2(w.x, w.z)));

            var props = Child(root, "Props");
            foreach (GameMapData.Prop prop in map.props ?? new GameMapData.Prop[0])
            {
                BuildProp(prop, catalog, props);
            }

            var seats = Child(root, "Seats");
            foreach (GameMapData.Seat seat in map.seats ?? new GameMapData.Seat[0])
            {
                var go = new GameObject(seat.id);
                go.transform.SetParent(seats, false);
                go.transform.position = new Vector3(seat.x, 0f, seat.z);
                go.AddComponent<Seat>().Init(seat.id, seat.kind, seat.facing);
            }

            var portals = Child(root, "Portals");
            foreach (GameMapData.Portal portal in map.portals ?? new GameMapData.Portal[0])
            {
                var go = new GameObject($"to {portal.to}");
                go.transform.SetParent(portals, false);
                go.transform.position = new Vector3(portal.x, 0f, portal.z);
                go.AddComponent<Portal>().Init(portal.to, new Vector2(portal.w, portal.d), new Vector2(portal.spawnX, portal.spawnZ));
            }

            Child(root, "Lighting").gameObject.AddComponent<MapLighting>().Init(map.lighting, map.night, map.lights);
            return root.gameObject;
        }

        /// <summary>소품 하나를 parent 아래에 만든다 (맵 에디터도 소품을 추가 · 다시 만들 때 쓴다). 카탈로그에 없으면 null</summary>
        public static GameObject BuildProp(GameMapData.Prop prop, PropCatalog catalog, Transform parent)
        {
            if (catalog.Find(prop.sprite) == null)
            {
                // 서버 맵이 이 빌드에 없는 그림을 쓴다 (새 소품은 클라이언트 업데이트가 필요)
                Debug.LogWarning($"MapBuilder: 소품 '{prop.sprite}' 가 PropCatalog 에 없다 — 건너뜀");
                return null;
            }
            var item = new GameObject(prop.sprite).transform;
            item.SetParent(parent, false);
            item.position = new Vector3(prop.x, 0f, prop.z);
            item.gameObject.AddComponent<MapProp>().Init(prop);
            DressProp(item, prop, catalog);
            return item.gameObject;
        }

        /// <summary>
        /// 소품 루트에 그림 · 바닥 그림자 · Obstacle 을 단다. 맵 에디터는 소품 값(그림 · 반전 · 그림자)이 바뀌면
        /// 자식과 Obstacle 을 지우고 이걸 다시 부른다. 카탈로그에 없으면 아무것도 안 단다
        /// </summary>
        public static void DressProp(Transform item, GameMapData.Prop prop, PropCatalog catalog)
        {
            PropCatalog.Entry entry = catalog.Find(prop.sprite);
            if (entry == null)
            {
                return;
            }
            GameObject sprite = CreateSprite("Sprite", item, entry.Sprite, entry.Building ? catalog.BuildingMaterial : catalog.SpriteMaterial);
            sprite.GetComponent<SpriteRenderer>().flipX = prop.flip;
            if (entry.Building)
            {
                // 깊이 = footprint 앞쪽 끝 (BuildingSprite). 해 그림자는 그림 실루엣을 눕히는 방식이라 건물엔 안 맞아서 뺀다
                sprite.AddComponent<BuildingSprite>();
            }
            else
            {
                sprite.AddComponent<SunShadow>();
            }
            if (entry.Footprint != Vector2.zero)
            {
                var obstacle = item.gameObject.AddComponent<Obstacle>();
                if (entry.Building)
                {
                    obstacle.InitCorner(entry.Footprint, BuildingCorner(entry.FootprintCornerPx));
                }
                else if (ViewAngle.IsQuarterView)
                {
                    obstacle.InitQuarterView(QuarterViewFootprint(prop, entry, catalog));
                }
                else
                {
                    obstacle.Init(entry.Footprint);
                }
            }
            if (prop.shadow > 0)
            {
                CreateShadow(item, prop.shadow, catalog);
            }
        }

        public static GameObject CreateSprite(string name, Transform parent, Sprite sprite, Material material)
        {
            var go = new GameObject(name, typeof(SpriteRenderer));
            go.transform.SetParent(parent, false);
            // 캐릭터 Visual 과 같은 기울기: 카메라를 정면으로 본다
            go.transform.localRotation = ViewAngle.Billboard;
            var renderer = go.GetComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sharedMaterial = material;
            return go;
        }

        // 쿼터뷰(카메라 yaw 45°)에선 월드 축에 맞춘 가구가 화면에 대각선으로 보인다:
        // 남쪽을 보는 가구 = _southeast 그림, 동쪽 = _northeast, 반전하면 서쪽 · 북쪽.
        // 그래서 크기는 정면(남쪽) 크기에서, 반전 · northeast 면 가로 · 깊이를 맞바꾼다
        private static Vector2 QuarterViewFootprint(GameMapData.Prop prop, PropCatalog.Entry entry, PropCatalog catalog)
        {
            foreach ((string suffix, bool swap) in new[] { ("_southeast", false), ("_northeast", true) })
            {
                if (!prop.sprite.EndsWith(suffix))
                {
                    continue;
                }
                PropCatalog.Entry front = catalog.Find(prop.sprite[..^suffix.Length]);
                if (front == null || front.Footprint == Vector2.zero)
                {
                    break;
                }
                Vector2 size = front.Footprint;
                return swap != prop.flip ? new Vector2(size.y, size.x) : size;
            }
            return entry.Footprint;
        }

        // 건물 그림의 픽셀 (pivot 기준 오른쪽 x, 위 y) → 바닥 위 점 (pivot 기준 월드 X · Z).
        // 화면 오른쪽 = 카메라 오른쪽 수평, 화면 위로 1 px = 바닥에서 카메라 앞 방향으로 1 / (32 · sin pitch) unit
        private static Vector2 BuildingCorner(Vector2 px)
        {
            Quaternion yaw = Quaternion.Euler(0f, ViewAngle.Yaw, 0f);
            Vector3 offset = yaw * Vector3.right * (px.x / 32f)
                + yaw * Vector3.forward * (px.y / (32f * Mathf.Sin(ViewAngle.Pitch * Mathf.Deg2Rad)));
            return new Vector2(offset.x, offset.z);
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
            // 화면 가로로 넓은 타원이 되도록 카메라 yaw 만큼 돌린다
            Quaternion yaw = Quaternion.Euler(0f, ViewAngle.Yaw, 0f);
            go.transform.localRotation = yaw * Quaternion.Euler(90f, 0f, 0f);
            go.transform.localScale = new Vector3(scale, scale, 1f);
            float halfDepth = sprite != null ? sprite.bounds.size.y * scale / 2f : 0f;
            go.transform.localPosition = yaw * new Vector3(0f, 0.01f, -halfDepth);
            var renderer = go.GetComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sharedMaterial = catalog.SpriteMaterial;
            // 투명 스프라이트끼리는 거리순으로 그리는데, 그림자는 항상 먼저 (= 캐릭터 · 소품 아래)
            renderer.sortingOrder = -1;
        }
    }
}

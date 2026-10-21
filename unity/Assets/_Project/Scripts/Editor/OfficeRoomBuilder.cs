using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace DgVillage.Editor
{
    /// <summary>
    /// 서버와 같이 쓰는 맵 JSON(python/src/dg_village_server/maps/{id}.json)으로 열린 씬에 방을 깐다.
    /// MapTestSceneBuilder(오프라인 테스트 씬)와 VillageMapBuilder(온라인 마을 씬)가 같이 쓴다:
    ///   Floor   FloorMesh (재질별 타일 바닥 + 가장자리 벽) + Floor_Office · Wall_Office 머티리얼 (URP Unlit)
    ///   Props   가구 (35° 세운 스프라이트 + 바닥에 눕힌 그림자 + Obstacle = 못 지나감)
    /// 방을 고칠 땐 JSON 을 고치고 메뉴를 다시 실행한다 (씬의 Layout 을 직접 고치면 서버의 채팅 구역과 어긋난다).
    /// </summary>
    internal static class OfficeRoomBuilder
    {
        private const string FloorTexturePath = "Assets/_Project/Sprites/Environment/Ground/office_floor.png";
        private const string FloorMaterialPath = "Assets/_Project/Materials/Floor_Office.mat";
        private const string WallTexturePath = "Assets/_Project/Sprites/Environment/Ground/office_wall.png";
        private const string WallMaterialPath = "Assets/_Project/Materials/Wall_Office.mat";
        private const string PropFolder = "Assets/_Project/Sprites/Environment/Props/";
        private const string SpriteMaterialPath = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat";
        // Application.dataPath(= unity/Assets) 기준
        private const string MapFolder = "../../python/src/dg_village_server/maps/";

        internal const string FloorName = "Floor";
        internal const string PropsName = "Props";
        internal static readonly Color BackgroundColor = new(0.106f, 0.129f, 0.118f);

        // 캐릭터 Visual 과 같은 기울기: 카메라(X 35°)를 정면으로 본다
        private static readonly Quaternion StandUp = Quaternion.Euler(35f, 0f, 0f);

        // 가구가 바닥에서 차지하는 크기 (가로, 깊이) unit → Obstacle. 가로 ≈ 스프라이트에 그려진 폭, 깊이는 눈대중
        private static readonly Dictionary<string, Vector2> Footprints = new()
        {
            ["desk_quill"] = new(1.4f, 0.6f), ["desk_spellbook"] = new(1.4f, 0.6f), ["desk_crystal"] = new(1.4f, 0.6f),
            ["chair_cushion"] = new(0.7f, 0.4f), ["chair_high"] = new(0.8f, 0.4f),
            ["bookshelf"] = new(1.1f, 0.4f), ["bookshelf_potion"] = new(1.1f, 0.4f), ["fireplace"] = new(1.4f, 0.4f),
            ["notice_board"] = new(1.1f, 0.3f), ["globe"] = new(0.8f, 0.5f),
            ["sofa"] = new(1.5f, 0.6f), ["armchair"] = new(1.1f, 0.6f), ["tea_table"] = new(0.9f, 0.6f),
            ["plant_pot"] = new(0.7f, 0.4f), ["candelabra"] = new(0.4f, 0.3f), ["owl_perch"] = new(0.4f, 0.3f),
        };

        internal static string MapPath(string mapId) =>
            Path.GetFullPath(Path.Combine(Application.dataPath, MapFolder, $"{mapId}.json"));

        /// <summary>맵 JSON 을 읽는다. 없으면 null.</summary>
        internal static GameMapData LoadMap(string mapId)
        {
            string path = MapPath(mapId);
            return File.Exists(path) ? JsonUtility.FromJson<GameMapData>(File.ReadAllText(path)) : null;
        }

        internal static bool AssetsReady(out string missing)
        {
            missing = AssetDatabase.LoadAssetAtPath<Texture2D>(FloorTexturePath) == null ? FloorTexturePath
                : AssetDatabase.LoadAssetAtPath<Texture2D>(WallTexturePath) == null ? WallTexturePath
                : null;
            return missing == null;
        }

        internal static Material SpriteMaterial => AssetDatabase.LoadAssetAtPath<Material>(SpriteMaterialPath);

        /// <summary>
        /// 열린 씬에 Floor 와 Props 를 만든다. (NewScene 다음에 불러야 한다 — 에셋을 여기서 불러오므로)
        /// </summary>
        internal static GameObject CreateRoom(GameMapData map)
        {
            Material floorMaterial = LoadOrCreateUnlitMaterial(FloorMaterialPath, FloorTexturePath);
            Material wallMaterial = LoadOrCreateUnlitMaterial(WallMaterialPath, WallTexturePath);
            Material spriteMaterial = SpriteMaterial;

            var floor = new GameObject(FloorName, typeof(MeshFilter), typeof(MeshRenderer), typeof(FloorMesh));
            // submesh 0 = 바닥, 1 = 벽 (FloorMesh 가 바닥 끝에 자동으로 만든다)
            floor.GetComponent<MeshRenderer>().sharedMaterials = new[] { floorMaterial, wallMaterial };
            var floorSo = new SerializedObject(floor.GetComponent<FloorMesh>());
            floorSo.FindProperty("_layout").stringValue = string.Join("\n", map.layout);
            floorSo.ApplyModifiedPropertiesWithoutUndo();

            var props = new GameObject(PropsName).transform;
            foreach (GameMapData.Prop prop in map.props)
            {
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{PropFolder}{prop.sprite}.png");
                if (sprite == null)
                {
                    Debug.LogWarning($"{PropFolder}{prop.sprite}.png 가 없다 — art/props/export_props.py 확인");
                }
                var root = new GameObject(prop.sprite).transform;
                root.SetParent(props, false);
                root.position = new Vector3(prop.x, 0f, prop.z);
                CreateSprite("Sprite", root, sprite, spriteMaterial);
                if (Footprints.TryGetValue(prop.sprite, out Vector2 footprint))
                {
                    var obstacleSo = new SerializedObject(root.gameObject.AddComponent<Obstacle>());
                    obstacleSo.FindProperty("_size").vector2Value = footprint;
                    obstacleSo.ApplyModifiedPropertiesWithoutUndo();
                }
                if (prop.shadow > 0)
                {
                    CreateShadow(root, prop.shadow, spriteMaterial);
                }
            }
            return floor;
        }

        internal static GameObject CreateSprite(string name, Transform parent, Sprite sprite, Material material)
        {
            var go = new GameObject(name, typeof(SpriteRenderer));
            go.transform.SetParent(parent, false);
            go.transform.localRotation = StandUp;
            var renderer = go.GetComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sharedMaterial = material;
            return go;
        }

        // 바닥에 눕힌 타원 그림자. 스프라이트 pivot 이 아래 가운데라 반 높이만큼 당겨 발밑 중앙에 맞춘다
        private static void CreateShadow(Transform parent, int scale, Material material)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{PropFolder}blob_shadow.png");
            var go = new GameObject("Shadow", typeof(SpriteRenderer));
            go.transform.SetParent(parent, false);
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            go.transform.localScale = new Vector3(scale, scale, 1f);
            float halfDepth = sprite != null ? sprite.bounds.size.y * scale / 2f : 0f;
            go.transform.localPosition = new Vector3(0f, 0.01f, -halfDepth);
            var renderer = go.GetComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sharedMaterial = material;
            // 투명 스프라이트끼리는 거리순으로 그리는데, 그림자는 항상 먼저 (= 캐릭터 · 소품 아래)
            renderer.sortingOrder = -1;
        }

        private static Material LoadOrCreateUnlitMaterial(string materialPath, string texturePath)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material != null)
            {
                return material;
            }
            // 캐릭터 스프라이트도 Unlit 이라 바닥도 Unlit: 조명 없이 텍스처 색 그대로
            material = new Material(Shader.Find("Universal Render Pipeline/Unlit"))
            {
                mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath),
            };
            AssetDatabase.CreateAsset(material, materialPath);
            return material;
        }
    }
}

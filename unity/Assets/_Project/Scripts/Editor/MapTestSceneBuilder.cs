using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DgVillage.Editor
{
    /// <summary>
    /// 메뉴 "DgVillage → Create Map Test Scene".
    /// 맵 아트 실험용 씬 Assets/Scenes/MapTest.unity 를 만든다 (네트워크 없음, 걸어다니며 보기만).
    /// 첫 맵 = 마법사 길드 사무소 한 칸: 왼쪽 자리(나무 바닥 · 책상 4개) | 가운데 복도(돌) | 오른쪽 휴게실(카펫 · 벽난로):
    ///   Main Camera   마을과 같은 각도 · Size 5.625 + CameraFollow
    ///   Floor         FloorMesh (재질별 타일 바닥 + 가장자리 벽) + Floor_Office · Wall_Office 머티리얼 (URP Unlit)
    ///   Player        PlayerMovement + Visual (girl01)
    ///   Boy01         서 있는 비교용 캐릭터 (책상 뒤)
    ///   Props         가구 (35° 세운 스프라이트 + 바닥에 눕힌 그림자 + Obstacle = 못 지나감)
    /// 바닥 모양 · 가구 위치는 서버와 같이 쓰는 python/src/dg_village_server/maps/office.json 에서 읽는다
    /// (Floor 의 Layout 을 씬에서 직접 고치면 서버의 채팅 구역과 어긋난다 — JSON 을 고치고 메뉴를 다시 실행).
    /// </summary>
    public static class MapTestSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/MapTest.unity";
        private const string FloorTexturePath = "Assets/_Project/Sprites/Environment/Ground/office_floor.png";
        private const string FloorMaterialPath = "Assets/_Project/Materials/Floor_Office.mat";
        private const string WallTexturePath = "Assets/_Project/Sprites/Environment/Ground/office_wall.png";
        private const string WallMaterialPath = "Assets/_Project/Materials/Wall_Office.mat";
        private const string PropFolder = "Assets/_Project/Sprites/Environment/Props/";
        private const string SpriteMaterialPath = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat";
        private const string Title = "Create Map Test Scene";

        // 캐릭터 Visual 과 같은 기울기: 카메라(X 35°)를 정면으로 본다
        private static readonly Quaternion StandUp = Quaternion.Euler(35f, 0f, 0f);

        // 맵 원본 = 서버와 같이 쓰는 JSON (바닥 Layout · 가구 위치 · 좌석). 고칠 땐 이 파일을 고치고 메뉴를 다시 실행
        private const string MapJsonPath = "../python/src/dg_village_server/maps/office.json";

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

        [MenuItem("DgVillage/Create Map Test Scene")]
        private static void Create()
        {
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog(Title, "Play 모드를 끄고 실행할 것.", "OK");
                return;
            }
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(FloorTexturePath) == null)
            {
                EditorUtility.DisplayDialog(Title, $"{FloorTexturePath} 가 없다. art/tilesets/make_floor_atlas.py 먼저.", "OK");
                return;
            }
            // Application.dataPath = unity/Assets → 저장소 루트 기준 경로로
            string mapPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", MapJsonPath));
            if (!File.Exists(mapPath))
            {
                EditorUtility.DisplayDialog(Title, $"{mapPath} 가 없다.", "OK");
                return;
            }
            var map = JsonUtility.FromJson<GameMapData>(File.ReadAllText(mapPath));
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null
                && !EditorUtility.DisplayDialog(Title, $"{ScenePath} 가 이미 있다. 덮어쓸까?", "덮어쓰기", "취소"))
            {
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }
            Build(map);
        }

        /// <summary>
        /// 확인 창 없이 씬을 만들어 저장한다 (열린 씬의 저장 안 한 변경은 버려진다).
        /// 메뉴 대신 코드로 다시 만들 때 (Unity MCP 등): DgVillage.Editor.MapTestSceneBuilder.Rebuild()
        /// </summary>
        public static void Rebuild()
        {
            string mapPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", MapJsonPath));
            Build(JsonUtility.FromJson<GameMapData>(File.ReadAllText(mapPath)));
        }

        private static void Build(GameMapData map)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            // 에셋은 NewScene 다음에 불러온다 (그 전에 불러오면 "가짜 null" 이 된다)
            Material floorMaterial = LoadOrCreateUnlitMaterial(FloorMaterialPath, FloorTexturePath);
            Material wallMaterial = LoadOrCreateUnlitMaterial(WallMaterialPath, WallTexturePath);
            var spriteMaterial = AssetDatabase.LoadAssetAtPath<Material>(SpriteMaterialPath);
            var girl = AssetDatabase.LoadAssetAtPath<CharacterDefinition>("Assets/_Project/Characters/girl01.asset");
            var boy = AssetDatabase.LoadAssetAtPath<CharacterDefinition>("Assets/_Project/Characters/boy01.asset");

            var floor = new GameObject("Floor", typeof(MeshFilter), typeof(MeshRenderer), typeof(FloorMesh));
            // submesh 0 = 바닥, 1 = 벽 (FloorMesh 가 바닥 끝에 자동으로 만든다)
            floor.GetComponent<MeshRenderer>().sharedMaterials = new[] { floorMaterial, wallMaterial };
            var floorSo = new SerializedObject(floor.GetComponent<FloorMesh>());
            floorSo.FindProperty("_layout").stringValue = string.Join("\n", map.layout);
            floorSo.ApplyModifiedPropertiesWithoutUndo();

            var player = new GameObject("Player", typeof(PlayerMovement));
            player.transform.position = new Vector3(-0.5f, 0f, 0f);
            GameObject visual = CreateSprite("Visual", player.transform, girl != null ? girl.Preview : null, spriteMaterial);
            var animator = visual.AddComponent<DirectionalSpriteAnimator>();
            var animatorSo = new SerializedObject(animator);
            animatorSo.FindProperty("_character").objectReferenceValue = girl;
            animatorSo.ApplyModifiedPropertiesWithoutUndo();

            var boyRoot = new GameObject("Boy01");
            boyRoot.transform.position = new Vector3(-4.5f, 0f, 3.5f);
            CreateSprite("Visual", boyRoot.transform, boy != null ? boy.Preview : null, spriteMaterial);

            var props = new GameObject("Props").transform;
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

            var cameraGo = new GameObject("Main Camera", typeof(Camera));
            cameraGo.tag = "MainCamera";
            cameraGo.transform.SetPositionAndRotation(new Vector3(0f, 8f, -11.5f), Quaternion.Euler(35f, 0f, 0f));
            var camera = cameraGo.GetComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 5.625f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.106f, 0.129f, 0.118f);
            var follow = cameraGo.AddComponent<CameraFollow>();
            var followSo = new SerializedObject(follow);
            followSo.FindProperty("_target").objectReferenceValue = player.transform;
            followSo.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, ScenePath);
            Selection.activeGameObject = floor;
            Debug.Log($"{ScenePath} 생성 완료. Play 로 걸어다니며 확인 (네트워크 없음).", floor);
        }

        private static GameObject CreateSprite(string name, Transform parent, Sprite sprite, Material material)
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

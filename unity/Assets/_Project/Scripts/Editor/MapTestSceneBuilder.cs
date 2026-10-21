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
    ///   Floor         FloorMesh (재질별 타일 바닥) + Floor_Office 머티리얼 (URP Unlit)
    ///   Player        PlayerMovement + Visual (girl01)
    ///   Boy01         서 있는 비교용 캐릭터 (책상 뒤)
    ///   Props         가구 (35° 세운 스프라이트 + 바닥에 눕힌 그림자)
    /// 바닥 모양은 Floor 의 Layout 글자를 고치면 바로 바뀐다.
    /// </summary>
    public static class MapTestSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/MapTest.unity";
        private const string FloorTexturePath = "Assets/_Project/Sprites/Environment/Ground/office_floor.png";
        private const string FloorMaterialPath = "Assets/_Project/Materials/Floor_Office.mat";
        private const string PropFolder = "Assets/_Project/Sprites/Environment/Props/";
        private const string SpriteMaterialPath = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat";
        private const string Title = "Create Map Test Scene";

        // 캐릭터 Visual 과 같은 기울기: 카메라(X 35°)를 정면으로 본다
        private static readonly Quaternion StandUp = Quaternion.Euler(35f, 0f, 0f);

        // 타일 19 × 12. w = 자리(나무) · s = 복도(돌) · c = 휴게실(카펫)
        private const string Layout =
            "wwwwwwssssscccccccc\n" +
            "wwwwwwssssscccccccc\n" +
            "wwwwwwssssscccccccc\n" +
            "wwwwwwssssscccccccc\n" +
            "wwwwwwssssscccccccc\n" +
            "wwwwwwssssscccccccc\n" +
            "wwwwwwssssscccccccc\n" +
            "wwwwwwssssscccccccc\n" +
            "sssssssssssssssssss\n" +
            "sssssssssssssssssss\n" +
            "......sssss........\n" +
            "......sssss........";

        // (스프라이트 이름, 위치 x/z, 그림자 배율 — 0 = 그림자 없음). 원점 = 바닥 가운데
        private static readonly (string Sprite, float X, float Z, int ShadowScale)[] Props =
        {
            // 자리: 책상 뒤 의자 (앉으면 카메라 쪽을 본다)
            ("chair_cushion", -7.5f, 3.6f, 1), ("desk_quill", -7.5f, 3f, 2),
            ("chair_high", -4.5f, 3.6f, 1), ("desk_spellbook", -4.5f, 3f, 2),
            ("chair_cushion", -7.5f, 0.1f, 1), ("desk_crystal", -7.5f, -0.5f, 2),
            ("chair_cushion", -4.5f, 0.1f, 1), ("desk_quill", -4.5f, -0.5f, 2),
            // 북쪽 벽을 따라
            ("bookshelf", -8.2f, 5.6f, 0), ("bookshelf_potion", -6.6f, 5.6f, 0),
            ("notice_board", -1f, 5.7f, 0), ("globe", 0.8f, 5.2f, 1),
            ("fireplace", 6f, 5.6f, 0), ("plant_pot", 8.8f, 5.4f, 1),
            // 복도
            ("candelabra", -2.6f, -3.6f, 1), ("candelabra", 1.6f, -3.6f, 1), ("owl_perch", 0.9f, 2.5f, 1),
            // 휴게실
            ("sofa", 5.5f, 3.4f, 2), ("tea_table", 5.5f, 2f, 1), ("armchair", 7.8f, 2f, 1),
            ("armchair", 3.4f, 1.2f, 1), ("plant_pot", 8.8f, -2f, 1), ("plant_pot", -8.6f, -3.6f, 1),
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
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null
                && !EditorUtility.DisplayDialog(Title, $"{ScenePath} 가 이미 있다. 덮어쓸까?", "덮어쓰기", "취소"))
            {
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            // 에셋은 NewScene 다음에 불러온다 (그 전에 불러오면 "가짜 null" 이 된다)
            Material floorMaterial = LoadOrCreateUnlitMaterial(FloorMaterialPath, FloorTexturePath);
            var spriteMaterial = AssetDatabase.LoadAssetAtPath<Material>(SpriteMaterialPath);
            var girl = AssetDatabase.LoadAssetAtPath<CharacterDefinition>("Assets/_Project/Characters/girl01.asset");
            var boy = AssetDatabase.LoadAssetAtPath<CharacterDefinition>("Assets/_Project/Characters/boy01.asset");

            var floor = new GameObject("Floor", typeof(MeshFilter), typeof(MeshRenderer), typeof(FloorMesh));
            floor.GetComponent<MeshRenderer>().sharedMaterial = floorMaterial;
            var floorSo = new SerializedObject(floor.GetComponent<FloorMesh>());
            floorSo.FindProperty("_layout").stringValue = Layout;
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
            foreach ((string spriteName, float x, float z, int shadowScale) in Props)
            {
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{PropFolder}{spriteName}.png");
                var root = new GameObject(spriteName).transform;
                root.SetParent(props, false);
                root.position = new Vector3(x, 0f, z);
                CreateSprite("Sprite", root, sprite, spriteMaterial);
                if (shadowScale > 0)
                {
                    CreateShadow(root, shadowScale, spriteMaterial);
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

using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DgVillage.Editor
{
    /// <summary>
    /// 메뉴 "DgVillage → Create Map Test Scene".
    /// 맵 아트 실험용 씬 Assets/Scenes/MapTest.unity 를 만든다 (네트워크 없음, 걸어다니며 보기만):
    ///   Main Camera   마을과 같은 각도 · Size 5.625 + CameraFollow
    ///   Ground        GroundMesh (Wang 타일 바닥) + Ground_Plaza 머티리얼 (URP Unlit)
    ///   Player        PlayerMovement + Visual (girl01)
    ///   Boy01         서 있는 비교용 캐릭터
    ///   Props         벤치 · 나무 (35° 세운 스프라이트 + 바닥에 눕힌 그림자)
    /// 바닥 모양은 Ground 의 Layout 글자를 고치면 바로 바뀐다.
    /// </summary>
    public static class MapTestSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/MapTest.unity";
        private const string GroundTexturePath = "Assets/_Project/Sprites/Environment/Ground/plaza_ground.png";
        private const string GroundMaterialPath = "Assets/_Project/Materials/Ground_Plaza.mat";
        private const string PropFolder = "Assets/_Project/Sprites/Environment/Props/";
        private const string SpriteMaterialPath = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat";
        private const string Title = "Create Map Test Scene";

        // 캐릭터 Visual 과 같은 기울기: 카메라(X 35°)를 정면으로 본다
        private static readonly Quaternion StandUp = Quaternion.Euler(35f, 0f, 0f);

        // 꼭짓점 격자 (25 × 19 → 타일 24 × 18). '#' = 돌길
        private const string Layout =
            ".........................\n" +
            ".........................\n" +
            ".........................\n" +
            ".........................\n" +
            "......#############......\n" +
            "......#############......\n" +
            "......##..#########......\n" +
            "########..#########......\n" +
            "#########################\n" +
            "......###################\n" +
            "......###################\n" +
            "......#########..##......\n" +
            "......#########..##......\n" +
            "......#############......\n" +
            "......#############......\n" +
            "...........###...........\n" +
            "...........###...........\n" +
            "...........###...........\n" +
            "...........###...........";

        // (스프라이트 이름, 위치 x/z, 그림자 배율)
        private static readonly (string Sprite, float X, float Z, int ShadowScale)[] Props =
        {
            ("bench_plain", -3f, 3.5f, 1),
            ("bench_double", 2.5f, 3.5f, 2),
            ("bench_iron", -4.5f, -2.5f, 1),
            ("bench_arch", 4f, -2.5f, 1),
            ("tree_round", -9f, 5f, 2),
            ("tree_blossom", 8.5f, 6f, 2),
            ("tree_round_b", -8.5f, -5.5f, 2),
            ("tree_twin", 9.5f, -5f, 2),
            ("tree_round", -2f, 7f, 2),
            ("tree_blossom", 2f, -7.5f, 2),
        };

        [MenuItem("DgVillage/Create Map Test Scene")]
        private static void Create()
        {
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog(Title, "Play 모드를 끄고 실행할 것.", "OK");
                return;
            }
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(GroundTexturePath) == null)
            {
                EditorUtility.DisplayDialog(Title, $"{GroundTexturePath} 가 없다. art/tilesets/make_ground_atlas.py 먼저.", "OK");
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
            Material groundMaterial = LoadOrCreateGroundMaterial();
            var spriteMaterial = AssetDatabase.LoadAssetAtPath<Material>(SpriteMaterialPath);
            var girl = AssetDatabase.LoadAssetAtPath<CharacterDefinition>("Assets/_Project/Characters/girl01.asset");
            var boy = AssetDatabase.LoadAssetAtPath<CharacterDefinition>("Assets/_Project/Characters/boy01.asset");

            var ground = new GameObject("Ground", typeof(MeshFilter), typeof(MeshRenderer), typeof(GroundMesh));
            ground.GetComponent<MeshRenderer>().sharedMaterial = groundMaterial;
            var groundSo = new SerializedObject(ground.GetComponent<GroundMesh>());
            groundSo.FindProperty("_layout").stringValue = Layout;
            groundSo.ApplyModifiedPropertiesWithoutUndo();

            var player = new GameObject("Player", typeof(PlayerMovement));
            GameObject visual = CreateSprite("Visual", player.transform, girl != null ? girl.Preview : null, spriteMaterial);
            var animator = visual.AddComponent<DirectionalSpriteAnimator>();
            var animatorSo = new SerializedObject(animator);
            animatorSo.FindProperty("_character").objectReferenceValue = girl;
            animatorSo.ApplyModifiedPropertiesWithoutUndo();

            var boyRoot = new GameObject("Boy01");
            boyRoot.transform.position = new Vector3(1.5f, 0f, 0.5f);
            CreateSprite("Visual", boyRoot.transform, boy != null ? boy.Preview : null, spriteMaterial);

            var props = new GameObject("Props").transform;
            foreach ((string spriteName, float x, float z, int shadowScale) in Props)
            {
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{PropFolder}{spriteName}.png");
                var root = new GameObject(spriteName).transform;
                root.SetParent(props, false);
                root.position = new Vector3(x, 0f, z);
                CreateSprite("Sprite", root, sprite, spriteMaterial);
                CreateShadow(root, shadowScale, spriteMaterial);
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
            Selection.activeGameObject = ground;
            Debug.Log($"{ScenePath} 생성 완료. Play 로 걸어다니며 확인 (네트워크 없음).", ground);
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

        private static Material LoadOrCreateGroundMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(GroundMaterialPath);
            if (material != null)
            {
                return material;
            }
            // 캐릭터 스프라이트도 Unlit 이라 바닥도 Unlit: 조명 없이 텍스처 색 그대로
            material = new Material(Shader.Find("Universal Render Pipeline/Unlit"))
            {
                mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(GroundTexturePath),
            };
            AssetDatabase.CreateAsset(material, GroundMaterialPath);
            return material;
        }
    }
}

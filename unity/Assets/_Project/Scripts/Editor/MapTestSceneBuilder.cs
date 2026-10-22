using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DgVillage.Editor
{
    /// <summary>
    /// 메뉴 "DgVillage → Create Map Test Scene".
    /// 맵 아트 실험용 씬 Assets/Scenes/MapTest.unity 를 만든다 (네트워크 없음, 걸어다니며 보기만):
    ///   View          ViewAngleOverride — 쿼터뷰 실험 (카메라 X 30° · Y 45°, 2026-10-22)
    ///   Main Camera   그 각도 · Size 5.625 + CameraFollow
    ///   Floor · Props OfficeRoomBuilder 가 서버의 maps/office.json (마을과 같은 사무실) 으로 깐 방
    ///   Player        PlayerMovement + Visual (girl01)
    ///   Boy01         서 있는 비교용 캐릭터 (책상 뒤)
    /// </summary>
    public static class MapTestSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/MapTest.unity";
        private const string MapId = "office";
        private const string Title = "Create Map Test Scene";

        [MenuItem("DgVillage/Create Map Test Scene")]
        private static void Create()
        {
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog(Title, "Play 모드를 끄고 실행할 것.", "OK");
                return;
            }
            if (!OfficeRoomBuilder.AssetsReady(out string missing))
            {
                EditorUtility.DisplayDialog(Title, $"{missing} 가 없다. art/tilesets/make_floor_atlas.py 먼저.", "OK");
                return;
            }
            GameMapData map = OfficeRoomBuilder.LoadMap(MapId);
            if (map == null)
            {
                EditorUtility.DisplayDialog(Title, $"{OfficeRoomBuilder.MapPath(MapId)} 가 없다.", "OK");
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
            Build(map);
        }

        /// <summary>
        /// 확인 창 없이 씬을 만들어 저장한다 (열린 씬의 저장 안 한 변경은 버려진다).
        /// 메뉴 대신 코드로 다시 만들 때 (Unity MCP 등): DgVillage.Editor.MapTestSceneBuilder.Rebuild()
        /// </summary>
        public static void Rebuild() => Build(OfficeRoomBuilder.LoadMap(MapId));

        private static void Build(GameMapData map)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            // 가장 먼저: 켜지면서 ViewAngle 을 바꿔야 아래에서 만드는 스프라이트 · 벽이 이 각도를 본다
            new GameObject("View", typeof(ViewAngleOverride));
            // 에셋은 NewScene 다음에 불러온다 (그 전에 불러오면 "가짜 null" 이 된다)
            GameObject floor = OfficeRoomBuilder.CreateRoom(map);
            Material spriteMaterial = OfficeRoomBuilder.SpriteMaterial;
            var girl = AssetDatabase.LoadAssetAtPath<CharacterDefinition>("Assets/_Project/Characters/girl01.asset");
            var boy = AssetDatabase.LoadAssetAtPath<CharacterDefinition>("Assets/_Project/Characters/boy01.asset");

            var player = new GameObject("Player", typeof(PlayerMovement));
            player.transform.position = new Vector3(0f, 0f, -1f);
            GameObject visual = MapBuilder.CreateSprite("Visual", player.transform, girl != null ? girl.Preview : null, spriteMaterial);
            var animator = visual.AddComponent<DirectionalSpriteAnimator>();
            var animatorSo = new SerializedObject(animator);
            animatorSo.FindProperty("_character").objectReferenceValue = girl;
            animatorSo.ApplyModifiedPropertiesWithoutUndo();

            var boyRoot = new GameObject("Boy01");
            boyRoot.transform.position = new Vector3(-1.6f, 0f, 5.6f);
            MapBuilder.CreateSprite("Visual", boyRoot.transform, boy != null ? boy.Preview : null, spriteMaterial);

            var cameraGo = new GameObject("Main Camera", typeof(Camera));
            cameraGo.tag = "MainCamera";
            // 플레이어에서 카메라 반대 방향으로 물러난 자리 (정면 카메라 때 거리 ≈ 14)
            Quaternion view = Quaternion.Euler(ViewAngle.Pitch, ViewAngle.Yaw, 0f);
            cameraGo.transform.SetPositionAndRotation(player.transform.position - view * Vector3.forward * 14f, view);
            var camera = cameraGo.GetComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 5.625f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = OfficeRoomBuilder.BackgroundColor;
            var follow = cameraGo.AddComponent<CameraFollow>();
            var followSo = new SerializedObject(follow);
            followSo.FindProperty("_target").objectReferenceValue = player.transform;
            followSo.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, ScenePath);
            Selection.activeGameObject = floor;
            Debug.Log($"{ScenePath} 생성 완료. Play 로 걸어다니며 확인 (네트워크 없음).", floor);
        }
    }
}

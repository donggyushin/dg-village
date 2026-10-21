using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DgVillage.Editor
{
    /// <summary>
    /// 메뉴 "DgVillage → Set Up Village Scene".
    /// 온라인 마을 씬(SampleScene) 하나가 모든 맵을 쓴다: 접속하면 MapLoader 가 서버 welcome 의 맵으로 방을 깔고,
    /// 포탈을 지나면 같은 씬에서 방만 바꿔 깐다. 이 메뉴는 그 준비를 한다:
    ///   - PropCatalog 갱신 + 미리보기용으로 maps/office.json 방(루트 "Map")을 깐다 (에디터에서 보이게; 접속하면 바뀐다)
    ///     지우는 것: 옛 평지 Ground, 이전에 깐 Map (그리고 그 전 방식의 Floor · Props · Seats · Portals)
    ///   - Network 에 MapLoader (맵 깔기) · SeatInteraction (앉기) · PortalTraveler (맵 이동),
    ///     ChatCanvas 에 안내 문구 SeatHint 를 (없으면) 붙이고 연결한다.
    /// 소품 그림을 추가했거나 이 씬의 미리보기를 새로 보고 싶을 때 다시 실행한다. 맵 배치만 바꿨으면 서버 배포로 충분.
    /// </summary>
    public static class VillageMapBuilder
    {
        private const string ScenePath = "Assets/Scenes/SampleScene.unity";
        private const string PreviewMapId = "office";
        private const string Title = "Set Up Village Scene";
        private static readonly string[] OldRoots = { "Ground", MapBuilder.RootName, "Floor", "Props", "Seats", "Portals" };

        [MenuItem("DgVillage/Set Up Village Scene")]
        private static void Apply()
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
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }
            string error = ApplyToVillage();
            if (error != null)
            {
                EditorUtility.DisplayDialog(Title, error, "OK");
            }
        }

        /// <summary>
        /// 확인 창 없이 실행한다 (Unity MCP 등). 실패하면 이유, 성공하면 null.
        /// 열린 씬의 저장 안 한 변경은 버려진다.
        /// </summary>
        public static string ApplyToVillage()
        {
            GameMapData map = OfficeRoomBuilder.LoadMap(PreviewMapId);
            if (map == null)
            {
                return $"{OfficeRoomBuilder.MapPath(PreviewMapId)} 가 없다.";
            }
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (System.Array.IndexOf(OldRoots, root.name) >= 0)
                {
                    Object.DestroyImmediate(root);
                }
            }
            GameObject room = OfficeRoomBuilder.CreateRoom(map);

            string error = SetUpComponents(room);
            if (error != null)
            {
                return error;
            }

            // 하늘색 skybox 대신 방 밖은 어두운 단색
            Camera camera = Camera.main;
            if (camera != null)
            {
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = OfficeRoomBuilder.BackgroundColor;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Selection.activeGameObject = room;
            Debug.Log($"{ScenePath} 준비 완료 (미리보기 = {map.id}).", room);
            return null;
        }

        // Network 오브젝트에 MapLoader · SeatInteraction · PortalTraveler, ChatCanvas 에 화면 아래 안내 문구 (입력창 바로 위)
        private static string SetUpComponents(GameObject room)
        {
            var client = Object.FindAnyObjectByType<NetworkClient>();
            var player = Object.FindAnyObjectByType<PlayerMovement>();
            var chatInput = Object.FindAnyObjectByType<ChatInput>();
            if (client == null || player == null || chatInput == null)
            {
                return "씬에 NetworkClient · PlayerMovement · ChatInput 이 있어야 한다.";
            }
            Transform canvas = chatInput.transform.parent;
            Transform hintTransform = canvas.Find("SeatHint");
            TMP_Text hint;
            if (hintTransform == null)
            {
                var go = new GameObject("SeatHint", typeof(RectTransform), typeof(TextMeshProUGUI));
                var rect = (RectTransform)go.transform;
                rect.SetParent(canvas, false);
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
                rect.pivot = new Vector2(0.5f, 0f);
                rect.sizeDelta = new Vector2(900f, 40f);
                rect.anchoredPosition = new Vector2(0f, 84f);
                hint = go.GetComponent<TextMeshProUGUI>();
                hint.fontSize = 22f;
                hint.alignment = TextAlignmentOptions.Center;
                hint.color = new Color(1f, 0.92f, 0.75f);
                hint.raycastTarget = false;
                hint.text = "";
            }
            else
            {
                hint = hintTransform.GetComponent<TMP_Text>();
            }

            var seating = client.GetComponent<SeatInteraction>();
            if (seating == null)
            {
                seating = client.gameObject.AddComponent<SeatInteraction>();
            }
            var so = new SerializedObject(seating);
            so.FindProperty("_client").objectReferenceValue = client;
            so.FindProperty("_localPlayer").objectReferenceValue = player;
            so.FindProperty("_hint").objectReferenceValue = hint;
            so.ApplyModifiedPropertiesWithoutUndo();

            var traveler = client.GetComponent<PortalTraveler>();
            if (traveler == null)
            {
                traveler = client.gameObject.AddComponent<PortalTraveler>();
            }
            var loader = client.GetComponent<MapLoader>();
            if (loader == null)
            {
                loader = client.gameObject.AddComponent<MapLoader>();
            }
            var travelerSo = new SerializedObject(traveler);
            travelerSo.FindProperty("_client").objectReferenceValue = client;
            travelerSo.FindProperty("_localPlayer").objectReferenceValue = player;
            travelerSo.FindProperty("_mapLoader").objectReferenceValue = loader;
            travelerSo.ApplyModifiedPropertiesWithoutUndo();

            var loaderSo = new SerializedObject(loader);
            loaderSo.FindProperty("_client").objectReferenceValue = client;
            loaderSo.FindProperty("_catalog").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<PropCatalog>(OfficeRoomBuilder.CatalogPath);
            loaderSo.FindProperty("_localPlayer").objectReferenceValue = player;
            loaderSo.FindProperty("_camera").objectReferenceValue = Object.FindAnyObjectByType<CameraFollow>();
            loaderSo.FindProperty("_room").objectReferenceValue = room;
            loaderSo.ApplyModifiedPropertiesWithoutUndo();
            return null;
        }
    }
}

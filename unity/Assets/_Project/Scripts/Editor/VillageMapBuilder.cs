using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DgVillage.Editor
{
    /// <summary>
    /// 메뉴 "DgVillage → Apply Office Map To Village".
    /// 온라인 마을 씬(SampleScene)의 바닥을 서버의 maps/office.json 사무실 방으로 바꾼다.
    /// 지우는 것: 옛 평지 Ground, 이전에 깐 Floor · Props · Seats. 그대로 두는 것: Player · Network · ChatCanvas 등 나머지 전부.
    /// 앉기용으로 Network 에 SeatInteraction 을, ChatCanvas 에 안내 문구 SeatHint 를 (없으면) 붙인다.
    /// 맵 JSON 을 고친 뒤 다시 실행하면 방만 새로 깐다 (서버도 같은 JSON 으로 채팅 구역을 정한다).
    /// </summary>
    public static class VillageMapBuilder
    {
        private const string ScenePath = "Assets/Scenes/SampleScene.unity";
        private const string MapId = "office";
        private const string Title = "Apply Office Map To Village";
        private static readonly string[] OldRoots =
        {
            "Ground", OfficeRoomBuilder.FloorName, OfficeRoomBuilder.PropsName, OfficeRoomBuilder.SeatsName,
        };

        [MenuItem("DgVillage/Apply Office Map To Village")]
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
            GameMapData map = OfficeRoomBuilder.LoadMap(MapId);
            if (map == null)
            {
                return $"{OfficeRoomBuilder.MapPath(MapId)} 가 없다.";
            }
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (System.Array.IndexOf(OldRoots, root.name) >= 0)
                {
                    Object.DestroyImmediate(root);
                }
            }
            GameObject floor = OfficeRoomBuilder.CreateRoom(map);

            string seatError = SetUpSeating();
            if (seatError != null)
            {
                return seatError;
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
            Selection.activeGameObject = floor;
            Debug.Log($"{ScenePath} 에 사무실 방을 깔았다 ({map.id}).", floor);
            return null;
        }

        // Network 오브젝트에 SeatInteraction, ChatCanvas 에 화면 아래 안내 문구 (입력창 바로 위)
        private static string SetUpSeating()
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
            return null;
        }
    }
}

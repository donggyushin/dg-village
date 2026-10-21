using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DgVillage.Editor
{
    /// <summary>
    /// 메뉴 "DgVillage → Apply Maps To Village Scenes".
    /// 맵 하나 = 씬 하나 (MapClient.SceneOf). 서버의 maps/{id}.json 으로 마을 씬들의 바닥을 깐다:
    ///   office  → SampleScene (Player · Network · ChatCanvas 등 손으로 만든 씬)
    ///   outdoor → Outdoor     (SampleScene 을 통째로 복사한 뒤 바닥만 바꾼다 — 매번 새로 복사하니 Outdoor 를 손으로 고치지 말 것)
    /// 지우는 것: 옛 평지 Ground, 이전에 깐 Floor · Props · Seats · Portals. 그대로 두는 것: 나머지 전부.
    /// Network 에 SeatInteraction (앉기) · PortalTraveler (맵 이동) 를, ChatCanvas 에 안내 문구 SeatHint 를 (없으면) 붙이고,
    /// NetworkClient 의 Map Id 를 그 씬의 맵으로 채운다.
    /// 맵 JSON 을 고친 뒤 다시 실행하면 방만 새로 깐다 (서버도 같은 JSON 으로 채팅 구역을 정한다).
    /// Outdoor 를 처음 만든 뒤엔 Build Profiles 의 Scene List 에 넣어야 LoadScene 으로 열린다.
    /// </summary>
    public static class VillageMapBuilder
    {
        private const string SceneFolder = "Assets/Scenes/";
        private const string Title = "Apply Maps To Village Scenes";
        // 첫 번째 = 손으로 만든 원본 씬, 나머지는 그 복사본
        private static readonly string[] MapIds = { MapClient.DefaultMapId, "outdoor" };
        private static readonly string[] OldRoots =
        {
            "Ground", OfficeRoomBuilder.FloorName, OfficeRoomBuilder.PropsName, OfficeRoomBuilder.SeatsName,
            OfficeRoomBuilder.PortalsName,
        };

        private static string ScenePath(string mapId) => $"{SceneFolder}{MapClient.SceneOf(mapId)}.unity";

        [MenuItem("DgVillage/Apply Maps To Village Scenes")]
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
            for (int i = 0; i < MapIds.Length; i++)
            {
                if (i > 0)
                {
                    string copy = ScenePath(MapIds[i]);
                    AssetDatabase.DeleteAsset(copy);
                    if (!AssetDatabase.CopyAsset(ScenePath(MapIds[0]), copy))
                    {
                        return $"{copy} 로 복사 실패.";
                    }
                }
                string error = ApplyMap(MapIds[i]);
                if (error != null)
                {
                    return error;
                }
            }
            EditorSceneManager.OpenScene(ScenePath(MapIds[0]), OpenSceneMode.Single);
            return null;
        }

        private static string ApplyMap(string mapId)
        {
            GameMapData map = OfficeRoomBuilder.LoadMap(mapId);
            if (map == null)
            {
                return $"{OfficeRoomBuilder.MapPath(mapId)} 가 없다.";
            }
            string scenePath = ScenePath(mapId);
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (System.Array.IndexOf(OldRoots, root.name) >= 0)
                {
                    Object.DestroyImmediate(root);
                }
            }
            GameObject floor = OfficeRoomBuilder.CreateRoom(map);

            string seatError = SetUpSeating(mapId);
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
            Debug.Log($"{scenePath} 에 맵을 깔았다 ({map.id}).", floor);
            return null;
        }

        // Network 오브젝트에 SeatInteraction · PortalTraveler + 맵 id, ChatCanvas 에 화면 아래 안내 문구 (입력창 바로 위)
        private static string SetUpSeating(string mapId)
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
            var travelerSo = new SerializedObject(traveler);
            travelerSo.FindProperty("_localPlayer").objectReferenceValue = player;
            travelerSo.ApplyModifiedPropertiesWithoutUndo();

            var clientSo = new SerializedObject(client);
            clientSo.FindProperty("_mapId").stringValue = mapId;
            clientSo.ApplyModifiedPropertiesWithoutUndo();
            return null;
        }
    }
}

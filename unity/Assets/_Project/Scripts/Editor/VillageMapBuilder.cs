using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DgVillage.Editor
{
    /// <summary>
    /// 메뉴 "DgVillage → Apply Office Map To Village".
    /// 온라인 마을 씬(SampleScene)의 바닥을 서버의 maps/office.json 사무실 방으로 바꾼다.
    /// 지우는 것: 옛 평지 Ground, 이전에 깐 Floor · Props. 그대로 두는 것: Player · Network · ChatCanvas 등 나머지 전부.
    /// 맵 JSON 을 고친 뒤 다시 실행하면 방만 새로 깐다 (서버도 같은 JSON 으로 채팅 구역을 정한다).
    /// </summary>
    public static class VillageMapBuilder
    {
        private const string ScenePath = "Assets/Scenes/SampleScene.unity";
        private const string MapId = "office";
        private const string Title = "Apply Office Map To Village";
        private static readonly string[] OldRoots = { "Ground", OfficeRoomBuilder.FloorName, OfficeRoomBuilder.PropsName };

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
    }
}

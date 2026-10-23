using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DgVillage.Editor
{
    /// <summary>
    /// 메뉴 "DgVillage → Map Editor". 서버 맵 JSON(python/src/dg_village_server/maps/{id}.json)을 Unity 씬에서 고친다.
    ///   열기  저장 안 하는 새 씬에 런타임과 같은 MapBuilder 로 맵을 깐다 + 루트에 MapEditSession
    ///   편집  소품 · 좌석 · 포탈 = 보통 오브젝트 (Move 툴로 끌기, Ctrl+D 복제, Delete 삭제, 인스펙터로 값)
    ///         점광원 · 창문 = Scene 뷰의 원 핸들로 끌기, 값 · 삭제는 루트(MapEditSession) 인스펙터
    ///         바닥 layout · 분위기(lighting) = MapEditSession 인스펙터
    ///   저장  씬 → GameMapData → MapJsonWriter → JSON 파일. 씬 자체는 저장하지 않는다 (JSON 이 원본)
    /// 저장한 JSON 은 로컬 서버(--reload-include '*.json')가 다시 읽는다 → Play 로 바로 확인. 운영 서버는 커밋 · 푸시 = 자동 배포.
    /// </summary>
    public class MapEditorWindow : EditorWindow
    {
        private const float HandleSnap = 0.05f;
        private const float WindowSnap = 0.5f;

        private string[] _mapIds = Array.Empty<string>();
        private int _mapIndex;
        private string _propFilter = "";
        private Vector2 _scroll;
        private string _status = "";

        [MenuItem("DgVillage/Map Editor")]
        private static void Open() => GetWindow<MapEditorWindow>("Map Editor");

        private void OnEnable()
        {
            SceneView.duringSceneGui += OnSceneGUI;
            _mapIds = Directory.GetFiles(Path.GetDirectoryName(OfficeRoomBuilder.MapPath("x")), "*.json")
                .Select(Path.GetFileNameWithoutExtension).OrderBy(id => id).ToArray();
        }

        private void OnDisable() => SceneView.duringSceneGui -= OnSceneGUI;

        private static MapEditSession Session => FindAnyObjectByType<MapEditSession>();

        private static PropCatalog Catalog => AssetDatabase.LoadAssetAtPath<PropCatalog>(OfficeRoomBuilder.CatalogPath);

        // 초당 10번: 인스펙터에서 바뀐 소품을 다시 입힌다
        private void OnInspectorUpdate()
        {
            if (EditorApplication.isPlaying || Session == null)
            {
                return;
            }
            PropCatalog catalog = Catalog;
            foreach (MapProp prop in FindObjectsByType<MapProp>())
            {
                if (!prop.NeedsRebuild)
                {
                    continue;
                }
                GameMapData.Prop data = prop.ToData();
                if (catalog.Find(data.sprite) == null)
                {
                    _status = $"'{data.sprite}' 는 PropCatalog 에 없는 소품";
                    Repaint();
                }
                for (int i = prop.transform.childCount - 1; i >= 0; i--)
                {
                    DestroyImmediate(prop.transform.GetChild(i).gameObject);
                }
                if (prop.TryGetComponent(out Obstacle obstacle))
                {
                    DestroyImmediate(obstacle);
                }
                prop.name = data.sprite;
                MapBuilder.DressProp(prop.transform, data, catalog);
                prop.MarkBuilt();
            }
        }

        private void OnGUI()
        {
            if (EditorApplication.isPlaying)
            {
                EditorGUILayout.HelpBox("Play 모드에선 편집하지 않는다 (Play 중 바꾼 건 사라진다).", MessageType.Info);
                return;
            }

            EditorGUILayout.BeginHorizontal();
            _mapIndex = EditorGUILayout.Popup(_mapIndex, _mapIds);
            if (GUILayout.Button("열기", GUILayout.Width(60)) && _mapIds.Length > 0)
            {
                OpenMap(_mapIds[_mapIndex]);
            }
            EditorGUILayout.EndHorizontal();

            MapEditSession session = Session;
            if (session == null)
            {
                EditorGUILayout.HelpBox("맵을 골라 '열기'. 지금 씬은 닫힌다 (저장할지 먼저 묻는다).", MessageType.Info);
                return;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField($"편집 중: {session.Id}", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("JSON 저장", GUILayout.Height(28)))
            {
                Save(session);
            }
            if (GUILayout.Button("되돌리기 (다시 열기)", GUILayout.Height(28))
                && EditorUtility.DisplayDialog("Map Editor", "저장 안 한 변경을 버리고 JSON 에서 다시 열까?", "다시 열기", "취소"))
            {
                OpenMap(session.Id);
                return;
            }
            EditorGUILayout.EndHorizontal();
            if (_status != "")
            {
                EditorGUILayout.HelpBox(_status, MessageType.None);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("추가 (Scene 뷰 가운데에 생긴다)", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("좌석"))
            {
                AddSeat(session);
            }
            if (GUILayout.Button("포탈"))
            {
                AddPortal(session);
            }
            if (GUILayout.Button("점광원"))
            {
                Undo.RecordObject(session, "Add Light");
                Vector3 p = PlacePoint();
                session.AddLight(new GameMapData.Light { x = p.x, z = p.z, radius = 3f, intensity = 1f, color = "#ffc878" });
                session.Apply();
                Selection.activeGameObject = session.gameObject;
            }
            if (GUILayout.Button("창문"))
            {
                Undo.RecordObject(session, "Add Window");
                Vector3 p = PlacePoint();
                session.AddWindow(new GameMapData.Window { x = Snap(p.x, WindowSnap), z = Snap(p.z, WindowSnap) });
                session.Apply();
                Selection.activeGameObject = session.gameObject;
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("소품 추가", EditorStyles.boldLabel);
            _propFilter = EditorGUILayout.TextField("검색", _propFilter);
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            PropCatalog catalog = Catalog;
            foreach (PropCatalog.Entry entry in catalog.Entries)
            {
                if (_propFilter != "" && !entry.Id.Contains(_propFilter, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                var content = new GUIContent(entry.Id, AssetPreview.GetAssetPreview(entry.Sprite));
                if (GUILayout.Button(content, GUILayout.Height(36)))
                {
                    AddProp(session, entry.Id, catalog);
                }
            }
            EditorGUILayout.EndScrollView();
        }

        private void OpenMap(string mapId)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }
            GameMapData map = OfficeRoomBuilder.LoadMap(mapId);
            if (map == null)
            {
                _status = $"{OfficeRoomBuilder.MapPath(mapId)} 를 못 읽었다";
                return;
            }
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 가장 먼저: 켜지면서 ViewAngle 을 바꿔야 아래에서 만드는 스프라이트 · 벽이 이 각도를 본다 (MapTest 와 같다)
            bool quarter = map.view == "quarter";
            var view = new GameObject("View");
            view.SetActive(false);
            var viewSo = new SerializedObject(view.AddComponent<ViewAngleOverride>());
            viewSo.FindProperty("_pitch").floatValue = quarter ? ViewAngle.QuarterPitch : ViewAngle.DefaultPitch;
            viewSo.FindProperty("_yaw").floatValue = quarter ? ViewAngle.QuarterYaw : ViewAngle.DefaultYaw;
            viewSo.ApplyModifiedPropertiesWithoutUndo();
            view.SetActive(true);

            // 에셋은 NewScene 다음에 불러온다 (그 전에 불러오면 "가짜 null" 이 된다)
            GameObject root = OfficeRoomBuilder.CreateRoom(map);
            var session = root.AddComponent<MapEditSession>();
            session.Init(map);
            session.SetCatalog(Catalog);

            // Game 뷰도 게임과 같은 각도로 (Play 는 네트워크 없이 그냥 보기만)
            Quaternion rotation = ViewAngle.Billboard;
            var cameraGo = new GameObject("Main Camera", typeof(Camera));
            cameraGo.tag = "MainCamera";
            cameraGo.transform.SetPositionAndRotation(-(rotation * Vector3.forward) * 14f, rotation);
            var camera = cameraGo.GetComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 5.625f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = OfficeRoomBuilder.BackgroundColor;

            SceneView sceneView = SceneView.lastActiveSceneView;
            if (sceneView != null)
            {
                sceneView.orthographic = true;
                sceneView.isRotationLocked = true; // 실수로 돌려서 각도가 어긋나지 않게 (Scene 뷰 오른쪽 위 자물쇠로 푼다)
                sceneView.LookAtDirect(Vector3.zero, rotation, 9f);
                sceneView.Repaint();
            }
            // 바닥 메시는 FloorMesh.Update 에서 만든다 — 에디터는 뭔가 바뀌어야 Update 를 부르므로 한 번 돌려 둔다
            EditorApplication.QueuePlayerLoopUpdate();
            Selection.activeGameObject = root;
            _status = $"{mapId} 열림. 씬은 저장할 필요 없음 — 'JSON 저장' 만.";
        }

        private void Save(MapEditSession session)
        {
            GameMapData map = session.ToData();
            Transform root = session.transform;
            map.props = root.GetComponentsInChildren<MapProp>(true).Select(p => p.ToData()).ToArray();
            map.seats = root.GetComponentsInChildren<Seat>(true).Select(s => new GameMapData.Seat
            {
                id = s.Id, kind = s.Kind, x = s.transform.position.x, z = s.transform.position.z, facing = s.Facing,
            }).ToArray();
            map.portals = root.GetComponentsInChildren<Portal>(true).Select(p => new GameMapData.Portal
            {
                x = p.transform.position.x, z = p.transform.position.z, w = p.Size.x, d = p.Size.y, to = p.To,
                spawnX = p.Spawn.x, spawnZ = p.Spawn.z,
            }).ToArray();

            // 서버가 id 로 자리를 찾는다: 비었거나 겹치면 저장하지 않는다 (Ctrl+D 로 복제한 좌석은 id 가 같다)
            var ids = new HashSet<string>();
            foreach (GameMapData.Seat seat in map.seats)
            {
                if (string.IsNullOrEmpty(seat.id) || !ids.Add(seat.id))
                {
                    _status = $"좌석 id '{seat.id}' 가 비었거나 겹친다 — Seats 아래 좌석 인스펙터에서 고치고 다시 저장";
                    return;
                }
            }
            if (map.portals.Any(p => string.IsNullOrEmpty(p.to)))
            {
                _status = "갈 맵(To)이 빈 포탈이 있다 — 저장 안 함";
                return;
            }

            string path = OfficeRoomBuilder.MapPath(session.Id);
            File.WriteAllText(path, MapJsonWriter.Write(map));
            _status = $"저장 {DateTime.Now:HH:mm:ss} → {path}";
        }

        private void AddProp(MapEditSession session, string spriteId, PropCatalog catalog)
        {
            Vector3 p = PlacePoint();
            var data = new GameMapData.Prop { sprite = spriteId, x = p.x, z = p.z, shadow = 1 };
            GameObject go = MapBuilder.BuildProp(data, catalog, Child(session, "Props"));
            Undo.RegisterCreatedObjectUndo(go, "Add Prop");
            Selection.activeGameObject = go;
        }

        private void AddSeat(MapEditSession session)
        {
            var existing = new HashSet<string>(FindObjectsByType<Seat>().Select(s => s.Id));
            int n = existing.Count + 1;
            while (existing.Contains($"seat-{n}"))
            {
                n++;
            }
            var go = new GameObject($"seat-{n}");
            go.transform.SetParent(Child(session, "Seats"), false);
            go.transform.position = PlacePoint();
            go.AddComponent<Seat>().Init($"seat-{n}", "lounge", "down");
            Undo.RegisterCreatedObjectUndo(go, "Add Seat");
            Selection.activeGameObject = go;
        }

        private void AddPortal(MapEditSession session)
        {
            string to = _mapIds.FirstOrDefault(id => id != session.Id) ?? session.Id;
            var go = new GameObject($"to {to}");
            go.transform.SetParent(Child(session, "Portals"), false);
            go.transform.position = PlacePoint();
            go.AddComponent<Portal>().Init(to, new Vector2(2f, 1f), Vector2.zero);
            Undo.RegisterCreatedObjectUndo(go, "Add Portal");
            Selection.activeGameObject = go;
        }

        private static Transform Child(MapEditSession session, string name)
        {
            Transform child = session.transform.Find(name);
            if (child == null)
            {
                child = new GameObject(name).transform;
                child.SetParent(session.transform, false);
            }
            return child;
        }

        // Scene 뷰 가운데 바닥 점 (0.1 단위)
        private static Vector3 PlacePoint()
        {
            Vector3 pivot = SceneView.lastActiveSceneView != null ? SceneView.lastActiveSceneView.pivot : Vector3.zero;
            return new Vector3(Snap(pivot.x, 0.1f), 0f, Snap(pivot.z, 0.1f));
        }

        private static float Snap(float value, float step) => Mathf.Round(value / step) * step;

        // 점광원 · 창문은 오브젝트가 아니라 MapEditSession 의 값이라 Scene 뷰에 직접 핸들을 그린다
        private void OnSceneGUI(SceneView sceneView)
        {
            if (EditorApplication.isPlaying)
            {
                return;
            }
            MapEditSession session = Session;
            if (session == null)
            {
                return;
            }

            GameMapData.Light[] lights = session.Lights;
            for (int i = 0; i < lights.Length; i++)
            {
                GameMapData.Light light = lights[i];
                var position = new Vector3(light.x, 0f, light.z);
                Color color = ColorUtility.TryParseHtmlString(light.color, out Color c) ? c : Color.yellow;
                Handles.color = new Color(color.r, color.g, color.b, 0.6f);
                Handles.DrawWireDisc(position, Vector3.up, light.radius);
                Handles.color = color;
                if (MoveOnGround(ref position, HandleSnap, Handles.CircleHandleCap))
                {
                    Undo.RecordObject(session, "Move Light");
                    light.x = position.x;
                    light.z = position.z;
                    session.Apply();
                }
                Handles.Label(position, $"light {i}");
            }

            GameMapData.Window[] windows = session.Windows;
            for (int i = 0; i < windows.Length; i++)
            {
                GameMapData.Window window = windows[i];
                var position = new Vector3(window.x, 0f, window.z);
                Handles.color = new Color(0.6f, 0.85f, 1f);
                if (MoveOnGround(ref position, WindowSnap, Handles.RectangleHandleCap))
                {
                    Undo.RecordObject(session, "Move Window");
                    window.x = position.x;
                    window.z = position.z;
                    session.Apply();
                }
                Handles.Label(position, $"window {i}");
            }
        }

        // 바닥(XZ) 위에서만 끄는 핸들. 바뀌었으면 true
        private static bool MoveOnGround(ref Vector3 position, float snap, Handles.CapFunction cap)
        {
            EditorGUI.BeginChangeCheck();
            float size = HandleUtility.GetHandleSize(position) * 0.12f;
            Vector3 moved = Handles.Slider2D(position, Vector3.up, Vector3.right, Vector3.forward, size, cap, Vector2.zero);
            if (!EditorGUI.EndChangeCheck())
            {
                return false;
            }
            position = new Vector3(Snap(moved.x, snap), 0f, Snap(moved.z, snap));
            return true;
        }
    }
}

using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace DgVillage.Editor
{
    /// <summary>
    /// 메뉴 "DgVillage → Create Chat UI".
    /// 열려 있는 씬에 채팅 UI 를 한 번에 만들고 연결까지 한다:
    ///   EventSystem (없을 때만) — UI 가 키보드/마우스 입력을 받는 데 필요
    ///   ChatCanvas (Screen Space Overlay) + ChatBubbleSpawner
    ///   ├── Bubbles     말풍선이 생기는 곳 (입력창보다 먼저 그려짐 = 뒤에 깔림)
    ///   └── ChatInput   화면 아래 입력창 + ChatInput
    ///   Prefabs/ChatBubble.prefab (없을 때만)
    /// </summary>
    public static class ChatUIBuilder
    {
        private const string BubblePrefabPath = "Assets/_Project/Prefabs/ChatBubble.prefab";
        private const string UndoName = "Create Chat UI";

        [MenuItem("DgVillage/Create Chat UI")]
        private static void Create()
        {
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog(UndoName, "Play 모드를 끄고 실행할 것.", "OK");
                return;
            }

            var client = Object.FindAnyObjectByType<NetworkClient>();
            var remotePlayers = Object.FindAnyObjectByType<RemotePlayerSpawner>();
            var localPlayer = Object.FindAnyObjectByType<PlayerMovement>();
            if (client == null || remotePlayers == null || localPlayer == null)
            {
                EditorUtility.DisplayDialog(UndoName,
                    "씬에 NetworkClient, RemotePlayerSpawner, PlayerMovement 가 모두 있어야 한다.", "OK");
                return;
            }
            if (Object.FindAnyObjectByType<ChatInput>() != null)
            {
                EditorUtility.DisplayDialog(UndoName, "이미 채팅 UI 가 있다. 다시 만들려면 ChatCanvas 를 지우고 실행할 것.", "OK");
                return;
            }

            var resources = new TMP_DefaultControls.Resources
            {
                standard = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd"),
                background = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd"),
                inputField = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/InputFieldBackground.psd"),
            };

            EnsureEventSystem();

            // Canvas: 화면 크기가 달라도 UI 비율이 유지되도록 1280x720 기준으로 스케일
            var canvasGo = new GameObject("ChatCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Undo.RegisterCreatedObjectUndo(canvasGo, UndoName);
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = 0.5f;

            // Bubbles: 캔버스 전체를 덮는 빈 컨테이너
            var bubbleRoot = new GameObject("Bubbles", typeof(RectTransform)).GetComponent<RectTransform>();
            bubbleRoot.SetParent(canvasGo.transform, false);
            bubbleRoot.anchorMin = Vector2.zero;
            bubbleRoot.anchorMax = Vector2.one;
            bubbleRoot.offsetMin = Vector2.zero;
            bubbleRoot.offsetMax = Vector2.zero;

            // ChatInput: 화면 아래 가운데
            GameObject inputGo = TMP_DefaultControls.CreateInputField(resources);
            inputGo.name = "ChatInput";
            var inputRect = inputGo.GetComponent<RectTransform>();
            inputRect.SetParent(canvasGo.transform, false);
            inputRect.anchorMin = inputRect.anchorMax = new Vector2(0.5f, 0f);
            inputRect.pivot = new Vector2(0.5f, 0f);
            inputRect.anchoredPosition = new Vector2(0f, 16f);
            inputRect.sizeDelta = new Vector2(600f, 40f);

            var field = inputGo.GetComponent<TMP_InputField>();
            field.textComponent.fontSize = 18f;
            var placeholder = (TMP_Text)field.placeholder;
            placeholder.text = "Press Enter to chat";
            placeholder.fontSize = 18f;

            var chatInput = inputGo.AddComponent<ChatInput>();
            SetReferences(chatInput, ("_client", client), ("_localPlayer", localPlayer));

            var spawner = canvasGo.AddComponent<ChatBubbleSpawner>();
            SetReferences(spawner,
                ("_client", client),
                ("_remotePlayers", remotePlayers),
                ("_localPlayer", localPlayer),
                ("_bubblePrefab", LoadOrCreateBubblePrefab(resources.standard)),
                ("_bubbleRoot", bubbleRoot));

            EditorSceneManager.MarkSceneDirty(canvasGo.scene);
            Selection.activeGameObject = canvasGo;
            Debug.Log("Chat UI 생성 완료. Cmd+S 로 씬을 저장할 것.", canvasGo);
        }

        private static void EnsureEventSystem()
        {
            if (Object.FindAnyObjectByType<EventSystem>() != null)
            {
                return;
            }
            // 새 Input System 을 쓰므로 StandaloneInputModule 대신 InputSystemUIInputModule
            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            Undo.RegisterCreatedObjectUndo(go, UndoName);
        }

        private static ChatBubble LoadOrCreateBubblePrefab(Sprite backgroundSprite)
        {
            var existing = AssetDatabase.LoadAssetAtPath<ChatBubble>(BubblePrefabPath);
            if (existing != null)
            {
                return existing;
            }

            // 배경: 둥근 모서리 기본 UI 스프라이트를 9-slice 로 늘려 쓴다. 아래 가운데가 기준점
            var root = new GameObject("ChatBubble", typeof(RectTransform), typeof(Image), typeof(ChatBubble));
            var rootRect = root.GetComponent<RectTransform>();
            rootRect.pivot = new Vector2(0.5f, 0f);
            var image = root.GetComponent<Image>();
            image.sprite = backgroundSprite;
            image.type = Image.Type.Sliced;
            image.color = new Color(1f, 1f, 1f, 0.92f);
            image.raycastTarget = false;

            var textGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            var textRect = textGo.GetComponent<RectTransform>();
            textRect.SetParent(rootRect, false);
            textRect.anchorMin = textRect.anchorMax = new Vector2(0.5f, 0.5f);
            textRect.pivot = new Vector2(0.5f, 0.5f);
            var text = textGo.GetComponent<TextMeshProUGUI>();
            text.text = "";
            text.fontSize = 18f;
            text.color = new Color(0.15f, 0.12f, 0.15f);
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.richText = false;
            text.raycastTarget = false;

            SetReferences(root.GetComponent<ChatBubble>(), ("_text", text));

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, BubblePrefabPath);
            Object.DestroyImmediate(root);
            return prefab.GetComponent<ChatBubble>();
        }

        // private [SerializeField] 필드는 코드에서 바로 못 넣으므로 Inspector 와 같은 경로(SerializedObject)로 넣는다
        private static void SetReferences(Object target, params (string field, Object value)[] references)
        {
            var so = new SerializedObject(target);
            foreach ((string field, Object value) in references)
            {
                SerializedProperty property = so.FindProperty(field);
                if (property == null)
                {
                    Debug.LogError($"{target.GetType().Name} 에 {field} 필드가 없다.", target);
                    continue;
                }
                property.objectReferenceValue = value;
            }
            so.ApplyModifiedProperties();
        }
    }
}

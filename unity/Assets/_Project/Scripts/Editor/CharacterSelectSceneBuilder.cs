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
    /// 메뉴 "DgVillage → Create Character Select Scene".
    /// 새 씬 Assets/Scenes/CharacterSelect.unity 를 만들고 UI 를 깔고 연결까지 한 뒤 저장한다:
    ///   Main Camera     단색 배경
    ///   EventSystem     UI 입력
    ///   SelectCanvas    Screen Space Overlay + CharacterSelect
    ///   ├── Title / Preview / Previous · Next / CharacterName / NameInput / EnterButton
    /// Build Profiles 의 Scene List 에 넣는 건 직접 한다 (이 씬이 0번 = 첫 화면).
    /// </summary>
    public static class CharacterSelectSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/CharacterSelect.unity";
        private const string RegistryPath = "Assets/_Project/Characters/CharacterRegistry.asset";
        private const string Title = "Create Character Select Scene";

        private static readonly Color BackgroundColor = new(0.13f, 0.11f, 0.19f);

        [MenuItem("DgVillage/Create Character Select Scene")]
        private static void Create()
        {
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog(Title, "Play 모드를 끄고 실행할 것.", "OK");
                return;
            }
            var registry = AssetDatabase.LoadAssetAtPath<CharacterRegistry>(RegistryPath);
            if (registry == null)
            {
                EditorUtility.DisplayDialog(Title, $"{RegistryPath} 가 없다. 캐릭터 폴더 우클릭 → DgVillage → Create Character Definition 먼저.", "OK");
                return;
            }
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null
                && !EditorUtility.DisplayDialog(Title, $"{ScenePath} 가 이미 있다. 덮어쓸까?", "덮어쓰기", "취소"))
            {
                return;
            }
            // 열려 있는 씬에 저장 안 한 변경이 있으면 먼저 물어본다
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cameraGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraGo.tag = "MainCamera";
            var camera = cameraGo.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = BackgroundColor;
            camera.orthographic = true;

            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            var resources = new TMP_DefaultControls.Resources
            {
                standard = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd"),
                background = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd"),
                inputField = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/InputFieldBackground.psd"),
            };

            // 채팅 UI 와 같은 1280x720 기준 스케일. 아래 좌표는 모두 화면 가운데 기준
            var canvasGo = new GameObject("SelectCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = 0.5f;
            var root = (RectTransform)canvasGo.transform;

            TMP_Text title = CreateText(root, "Title", "캐릭터 선택", 40f, new Vector2(0f, 280f), new Vector2(600f, 60f));
            title.fontStyle = FontStyles.Bold;

            // 픽셀 스프라이트를 크게: 64px 프레임을 256px 로 (4배, 정수 배율이라 픽셀이 고르게 보인다)
            var previewGo = new GameObject("Preview", typeof(RectTransform), typeof(Image));
            var preview = previewGo.GetComponent<Image>();
            Place(preview.rectTransform, root, new Vector2(0f, 90f), new Vector2(256f, 256f));
            preview.preserveAspect = true;
            preview.raycastTarget = false;
            preview.sprite = registry.Default != null ? registry.Default.Preview : null;

            Button previous = CreateButton(root, resources, "Previous", "<", new Vector2(-210f, 90f), new Vector2(64f, 64f), 32f);
            Button next = CreateButton(root, resources, "Next", ">", new Vector2(210f, 90f), new Vector2(64f, 64f), 32f);

            TMP_Text characterName = CreateText(root, "CharacterName", "", 26f, new Vector2(0f, -65f), new Vector2(400f, 40f));

            GameObject inputGo = TMP_DefaultControls.CreateInputField(resources);
            inputGo.name = "NameInput";
            Place((RectTransform)inputGo.transform, root, new Vector2(0f, -135f), new Vector2(320f, 48f));
            var nameField = inputGo.GetComponent<TMP_InputField>();
            nameField.textComponent.fontSize = 22f;
            nameField.textComponent.alignment = TextAlignmentOptions.Center;
            var placeholder = (TMP_Text)nameField.placeholder;
            placeholder.text = "닉네임 (비우면 guest)";
            placeholder.fontSize = 22f;
            placeholder.alignment = TextAlignmentOptions.Center;

            Button enter = CreateButton(root, resources, "EnterButton", "입장", new Vector2(0f, -220f), new Vector2(200f, 56f), 26f);

            var select = canvasGo.AddComponent<CharacterSelect>();
            var so = new SerializedObject(select);
            so.FindProperty("_characters").objectReferenceValue = registry;
            so.FindProperty("_preview").objectReferenceValue = preview;
            so.FindProperty("_characterName").objectReferenceValue = characterName;
            so.FindProperty("_nameField").objectReferenceValue = nameField;
            so.FindProperty("_previousButton").objectReferenceValue = previous;
            so.FindProperty("_nextButton").objectReferenceValue = next;
            so.FindProperty("_enterButton").objectReferenceValue = enter;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, ScenePath);
            Selection.activeGameObject = canvasGo;
            Debug.Log($"{ScenePath} 생성 완료. File → Build Profiles → Scene List 에 이 씬을 0번으로 넣을 것.", canvasGo);
        }

        private static void Place(RectTransform rect, RectTransform parent, Vector2 position, Vector2 size)
        {
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static TMP_Text CreateText(RectTransform parent, string name, string text, float fontSize, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            var tmp = go.GetComponent<TextMeshProUGUI>();
            Place(tmp.rectTransform, parent, position, size);
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.richText = false;
            tmp.raycastTarget = false;
            return tmp;
        }

        private static Button CreateButton(RectTransform parent, TMP_DefaultControls.Resources resources,
            string name, string label, Vector2 position, Vector2 size, float fontSize)
        {
            GameObject go = TMP_DefaultControls.CreateButton(resources);
            go.name = name;
            Place((RectTransform)go.transform, parent, position, size);
            var text = go.GetComponentInChildren<TMP_Text>();
            text.text = label;
            text.fontSize = fontSize;
            text.color = new Color(0.15f, 0.12f, 0.15f);
            return go.GetComponent<Button>();
        }
    }
}

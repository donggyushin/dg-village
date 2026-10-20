using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using static DgVillage.Editor.SceneUIBuilder;

namespace DgVillage.Editor
{
    /// <summary>
    /// 메뉴 "DgVillage → Create Character Select Scene".
    /// 새 씬 Assets/Scenes/CharacterSelect.unity 를 만들고 UI 를 깔고 연결까지 한 뒤 저장한다:
    ///   Main Camera     단색 배경
    ///   EventSystem     UI 입력
    ///   SelectCanvas    Screen Space Overlay + CharacterSelect
    ///   ├── Title / Preview / Previous · Next / CharacterName / NameInput / EnterButton / StatusText
    /// Build Profiles 의 Scene List 에 넣는 건 직접 한다 (이 씬이 0번 = 첫 화면).
    /// </summary>
    public static class CharacterSelectSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/CharacterSelect.unity";
        private const string RegistryPath = "Assets/_Project/Characters/CharacterRegistry.asset";
        private const string Title = "Create Character Select Scene";

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
            // NewScene 이 이전 씬을 닫으며 참조 없는 에셋을 내려버리므로 (registry 가 "가짜 null" 이 된다) 다시 불러온다
            registry = AssetDatabase.LoadAssetAtPath<CharacterRegistry>(RegistryPath);

            CreateCameraAndEventSystem();
            TMP_DefaultControls.Resources resources = DefaultResources();
            GameObject canvasGo = CreateCanvas("SelectCanvas");
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

            // 서버 접속 중 · 에러 안내 (두 줄까지)
            TMP_Text status = CreateText(root, "StatusText", "", 18f, new Vector2(0f, -295f), new Vector2(600f, 56f));
            status.color = new Color(1f, 0.85f, 0.6f);

            var select = canvasGo.AddComponent<CharacterSelect>();
            var so = new SerializedObject(select);
            so.FindProperty("_characters").objectReferenceValue = registry;
            so.FindProperty("_preview").objectReferenceValue = preview;
            so.FindProperty("_characterName").objectReferenceValue = characterName;
            so.FindProperty("_nameField").objectReferenceValue = nameField;
            so.FindProperty("_previousButton").objectReferenceValue = previous;
            so.FindProperty("_nextButton").objectReferenceValue = next;
            so.FindProperty("_enterButton").objectReferenceValue = enter;
            so.FindProperty("_statusText").objectReferenceValue = status;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, ScenePath);
            Selection.activeGameObject = canvasGo;
            Debug.Log($"{ScenePath} 생성 완료. File → Build Profiles → Scene List 에 이 씬을 0번으로 넣을 것.", canvasGo);
        }
    }
}

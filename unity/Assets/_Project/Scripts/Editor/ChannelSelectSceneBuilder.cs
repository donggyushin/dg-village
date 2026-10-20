using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using static DgVillage.Editor.SceneUIBuilder;

namespace DgVillage.Editor
{
    /// <summary>
    /// 메뉴 "DgVillage → Create Channel Select Scene".
    /// 새 씬 Assets/Scenes/ChannelSelect.unity 를 만들고 UI 를 깔고 연결까지 한 뒤 저장한다:
    ///   Main Camera     단색 배경
    ///   EventSystem     UI 입력
    ///   ChannelCanvas   Screen Space Overlay + ChannelSelect
    ///   ├── Title / BackButton / RefreshButton
    ///   ├── ChannelList   ScrollView. Viewport/Content 에 VerticalLayoutGroup (줄을 위에서부터 쌓는다)
    ///   │   └── ...Content/ChannelItemTemplate   꺼둔 한 줄 원본 (ChannelListItem)
    ///   └── NameInput / CreateButton / StatusText
    /// Build Profiles 의 Scene List 에 넣는 건 직접 한다 (CharacterSelect 다음 = 1번).
    /// </summary>
    public static class ChannelSelectSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/ChannelSelect.unity";
        private const string Title = "Create Channel Select Scene";
        private const float ItemHeight = 56f;

        [MenuItem("DgVillage/Create Channel Select Scene")]
        private static void Create()
        {
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog(Title, "Play 모드를 끄고 실행할 것.", "OK");
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

            CreateCameraAndEventSystem();
            TMP_DefaultControls.Resources resources = DefaultResources();
            GameObject canvasGo = CreateCanvas("ChannelCanvas");
            var root = (RectTransform)canvasGo.transform;

            TMP_Text title = CreateText(root, "Title", "채널 선택", 40f, new Vector2(0f, 300f), new Vector2(600f, 60f));
            title.fontStyle = FontStyles.Bold;

            Button back = CreateButton(root, resources, "BackButton", "< 캐릭터", new Vector2(-460f, 300f), new Vector2(160f, 48f), 20f);
            Button refresh = CreateButton(root, resources, "RefreshButton", "새로고침", new Vector2(460f, 300f), new Vector2(160f, 48f), 20f);

            RectTransform content = CreateScrollList(root, new Vector2(0f, 30f), new Vector2(640f, 420f));
            ChannelListItem template = CreateItemTemplate(content, resources);

            GameObject inputGo = TMP_DefaultControls.CreateInputField(resources);
            inputGo.name = "NameInput";
            Place((RectTransform)inputGo.transform, root, new Vector2(-80f, -230f), new Vector2(480f, 52f));
            var nameField = inputGo.GetComponent<TMP_InputField>();
            nameField.textComponent.fontSize = 22f;
            var placeholder = (TMP_Text)nameField.placeholder;
            placeholder.text = "새 채널 이름";
            placeholder.fontSize = 22f;

            Button create = CreateButton(root, resources, "CreateButton", "만들기", new Vector2(250f, -230f), new Vector2(140f, 52f), 22f);

            TMP_Text status = CreateText(root, "StatusText", "", 18f, new Vector2(0f, -300f), new Vector2(640f, 56f));
            status.color = new Color(1f, 0.85f, 0.6f);

            var select = canvasGo.AddComponent<ChannelSelect>();
            var so = new SerializedObject(select);
            so.FindProperty("_itemTemplate").objectReferenceValue = template;
            so.FindProperty("_listContent").objectReferenceValue = content;
            so.FindProperty("_nameField").objectReferenceValue = nameField;
            so.FindProperty("_createButton").objectReferenceValue = create;
            so.FindProperty("_refreshButton").objectReferenceValue = refresh;
            so.FindProperty("_backButton").objectReferenceValue = back;
            so.FindProperty("_statusText").objectReferenceValue = status;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, ScenePath);
            Selection.activeGameObject = canvasGo;
            Debug.Log($"{ScenePath} 생성 완료. File → Build Profiles → Scene List 에서 CharacterSelect 다음(1번)에 넣을 것.", canvasGo);
        }

        // 세로로만 스크롤되는 목록. 돌려주는 Content 에 줄을 넣으면 VerticalLayoutGroup 이 위에서부터 쌓고,
        // ContentSizeFitter 가 Content 높이를 줄 수에 맞춰 늘려서 넘치면 스크롤된다.
        private static RectTransform CreateScrollList(RectTransform parent, Vector2 position, Vector2 size)
        {
            GameObject scrollGo = DefaultControls.CreateScrollView(new DefaultControls.Resources
            {
                standard = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd"),
                background = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd"),
                mask = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UIMask.psd"),
            });
            scrollGo.name = "ChannelList";
            Place((RectTransform)scrollGo.transform, parent, position, size);
            scrollGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.25f);

            var scroll = scrollGo.GetComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.scrollSensitivity = 30f;
            Object.DestroyImmediate(scroll.horizontalScrollbar.gameObject);
            scroll.horizontalScrollbar = null;

            RectTransform content = scroll.content;
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(8, 8, 8, 8);
            layout.spacing = 8f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return content;
        }

        // 버튼 하나 = 한 줄: 왼쪽에 채널 이름, 오른쪽에 인원
        private static ChannelListItem CreateItemTemplate(RectTransform content, TMP_DefaultControls.Resources resources)
        {
            GameObject go = TMP_DefaultControls.CreateButton(resources);
            go.name = "ChannelItemTemplate";
            go.transform.SetParent(content, false);
            go.AddComponent<LayoutElement>().preferredHeight = ItemHeight;

            var nameText = go.GetComponentInChildren<TMP_Text>();
            nameText.name = "Name";
            nameText.fontSize = 22f;
            nameText.color = new Color(0.15f, 0.12f, 0.15f);
            nameText.alignment = TextAlignmentOptions.MidlineLeft;
            nameText.margin = new Vector4(20f, 0f, 100f, 0f);
            nameText.richText = false;

            var countGo = new GameObject("PlayerCount", typeof(RectTransform), typeof(TextMeshProUGUI));
            var countText = countGo.GetComponent<TextMeshProUGUI>();
            var countRect = countText.rectTransform;
            countRect.SetParent(go.transform, false);
            countRect.anchorMin = Vector2.zero;
            countRect.anchorMax = Vector2.one;
            countRect.offsetMin = countRect.offsetMax = Vector2.zero;
            countText.fontSize = 20f;
            countText.color = new Color(0.35f, 0.3f, 0.4f);
            countText.alignment = TextAlignmentOptions.MidlineRight;
            countText.margin = new Vector4(0f, 0f, 20f, 0f);
            countText.raycastTarget = false;

            var item = go.AddComponent<ChannelListItem>();
            var so = new SerializedObject(item);
            so.FindProperty("_button").objectReferenceValue = go.GetComponent<Button>();
            so.FindProperty("_name").objectReferenceValue = nameText;
            so.FindProperty("_playerCount").objectReferenceValue = countText;
            so.ApplyModifiedPropertiesWithoutUndo();

            // 원본은 꺼둔다. 꺼진 오브젝트는 VerticalLayoutGroup 이 무시하므로 목록 칸을 차지하지 않는다
            go.SetActive(false);
            return item;
        }
    }
}

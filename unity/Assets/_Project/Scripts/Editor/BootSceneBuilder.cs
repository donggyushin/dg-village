using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using static DgVillage.Editor.SceneUIBuilder;

namespace DgVillage.Editor
{
    /// <summary>
    /// 메뉴 "DgVillage → Create Boot Scene".
    /// 새 씬 Assets/Scenes/Boot.unity (게임을 켜면 처음 뜨는 로딩 화면) 를 만들고 저장한 뒤,
    /// Build Profiles 의 Scene List 맨 앞(0번)에 넣는다:
    ///   Main Camera     단색 배경
    ///   EventSystem     UI 입력
    ///   LoadingCanvas   Screen Space Overlay (sortingOrder 100 → 페이드 중 다음 씬 UI 위에 그려진다) + CanvasGroup + LoadingScreen
    ///   ├── Illustration  일러스트, 화면을 꽉 채운다 (비율 유지, 넘치는 쪽은 잘림)
    ///   ├── BottomShade   글자가 읽히도록 아래쪽을 어둡게
    ///   └── StatusText
    /// </summary>
    public static class BootSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/Boot.unity";
        private const string IllustrationPath = "Assets/_Project/Sprites/UI/loading_guild_study.png";
        private const string Title = "Create Boot Scene";

        [MenuItem("DgVillage/Create Boot Scene")]
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
            // 열려 있는 씬에 저장 안 한 변경이 있으면 먼저 물어본다
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }
            Build();
        }

        /// <summary>대화상자 없이 씬을 만들고 Scene List 에 넣는다 (Unity MCP 에서 호출용). 열린 씬의 저장 안 한 변경은 버려진다.</summary>
        public static void Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            // NewScene 이 참조 없는 에셋을 내려버리므로 씬을 만든 뒤에 불러온다
            var illustration = AssetDatabase.LoadAssetAtPath<Sprite>(IllustrationPath);
            if (illustration == null)
            {
                Debug.LogWarning($"BootSceneBuilder: {IllustrationPath} 가 없다. 일러스트 없이 만든다.");
            }

            CreateCameraAndEventSystem();
            GameObject canvasGo = CreateCanvas("LoadingCanvas");
            canvasGo.GetComponent<Canvas>().sortingOrder = 100;
            canvasGo.AddComponent<CanvasGroup>();
            var root = (RectTransform)canvasGo.transform;

            var imageGo = new GameObject("Illustration", typeof(RectTransform), typeof(Image), typeof(AspectRatioFitter));
            var image = imageGo.GetComponent<Image>();
            Place(image.rectTransform, root, Vector2.zero, Vector2.zero);
            image.sprite = illustration;
            image.color = illustration != null ? Color.white : BackgroundColor;
            image.raycastTarget = false;
            var fitter = imageGo.GetComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = illustration != null ? illustration.rect.width / illustration.rect.height : 16f / 9f;

            var shadeGo = new GameObject("BottomShade", typeof(RectTransform), typeof(Image));
            var shade = shadeGo.GetComponent<Image>();
            var shadeRect = shade.rectTransform;
            shadeRect.SetParent(root, false);
            shadeRect.anchorMin = new Vector2(0f, 0f);
            shadeRect.anchorMax = new Vector2(1f, 0f);
            shadeRect.pivot = new Vector2(0.5f, 0f);
            shadeRect.anchoredPosition = Vector2.zero;
            shadeRect.sizeDelta = new Vector2(0f, 110f);
            shade.color = new Color(0f, 0f, 0f, 0.55f);
            shade.raycastTarget = false;

            TMP_Text status = CreateText(root, "StatusText", "", 22f, new Vector2(0f, -305f), new Vector2(900f, 70f));
            status.color = new Color(1f, 0.92f, 0.75f);

            var loading = canvasGo.AddComponent<LoadingScreen>();
            var so = new SerializedObject(loading);
            so.FindProperty("_statusText").objectReferenceValue = status;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, ScenePath);
            AddToSceneListFirst();
            Selection.activeGameObject = canvasGo;
            Debug.Log($"{ScenePath} 생성 완료 · Scene List 0번으로 등록.", canvasGo);
        }

        // Scene List 맨 앞에 Boot 를 둔다 (이미 있으면 자리만 옮긴다). Save Project 전엔 메모리에만 있으므로 바로 저장한다
        private static void AddToSceneListFirst()
        {
            var scenes = new List<EditorBuildSettingsScene> { new(ScenePath, true) };
            scenes.AddRange(EditorBuildSettings.scenes.Where(s => s.path != ScenePath));
            EditorBuildSettings.scenes = scenes.ToArray();
            AssetDatabase.SaveAssets();
        }
    }
}

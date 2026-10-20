using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace DgVillage.Editor
{
    /// <summary>씬 빌더 메뉴들(캐릭터 선택 · 채널 선택)이 같이 쓰는 UI 생성 도우미. 좌표는 모두 부모 가운데 기준.</summary>
    internal static class SceneUIBuilder
    {
        internal static readonly Color BackgroundColor = new(0.13f, 0.11f, 0.19f);

        /// <summary>단색 배경 Main Camera + UI 입력용 EventSystem (새 Input System 모듈)</summary>
        internal static void CreateCameraAndEventSystem()
        {
            var cameraGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraGo.tag = "MainCamera";
            var camera = cameraGo.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = BackgroundColor;
            camera.orthographic = true;

            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        }

        /// <summary>Unity 기본 UI 스프라이트 (Create → UI 메뉴로 만들 때와 같은 모양)</summary>
        internal static TMP_DefaultControls.Resources DefaultResources() => new()
        {
            standard = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd"),
            background = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd"),
            inputField = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/InputFieldBackground.psd"),
        };

        /// <summary>채팅 UI 와 같은 1280x720 기준 스케일의 Screen Space Overlay 캔버스</summary>
        internal static GameObject CreateCanvas(string name)
        {
            var canvasGo = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = 0.5f;
            return canvasGo;
        }

        internal static void Place(RectTransform rect, RectTransform parent, Vector2 position, Vector2 size)
        {
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        internal static TMP_Text CreateText(RectTransform parent, string name, string text, float fontSize, Vector2 position, Vector2 size)
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

        internal static Button CreateButton(RectTransform parent, TMP_DefaultControls.Resources resources,
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

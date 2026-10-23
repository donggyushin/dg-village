using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DgVillage
{
    /// <summary>
    /// 화면 오른쪽 위의 전체화면/창모드 전환 버튼과 게임 닫기(X) 버튼. 모든 씬 공통.
    /// 씬에 배치하지 않는다: Play(실행) 시작 때 스스로 캔버스를 만들고 DontDestroyOnLoad 로 씬이 바뀌어도 남는다.
    /// 클릭은 각 씬의 EventSystem 이 받는다. 아이콘은 코드로 그린 픽셀 그림 (폰트에 없는 기호라서).
    /// 에디터의 Game 뷰에선 전체화면 전환이 동작하지 않는다 (빌드에서 확인). X 는 에디터에선 Play 를 끈다.
    /// </summary>
    public class WindowControls : MonoBehaviour
    {
        private const float ButtonSize = 36f;
        private const float Margin = 10f;
        private const int IconPixels = 9;
        // 로딩 화면(100)보다 아래: 로딩 일러스트를 가리지 않게
        private const int SortingOrder = 90;
        private static readonly Vector2Int WindowedSize = new(1280, 720);
        private static readonly Color ButtonColor = new(1f, 1f, 1f, 0.9f);
        private static readonly Color IconColor = new(0.15f, 0.12f, 0.15f);

        private static WindowControls _instance;

        private Image _fullscreenIcon;
        private Sprite _enterFullscreen;
        private Sprite _exitFullscreen;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _instance = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Create()
        {
            if (_instance != null)
            {
                return;
            }
            var go = new GameObject("WindowControls", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<WindowControls>();
        }

        private void Awake()
        {
            var canvas = GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortingOrder;
            // 다른 UI 와 같은 1280x720 기준 스케일
            var scaler = GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = 0.5f;

            _enterFullscreen = CreateIcon(EnterFullscreenPixel);
            _exitFullscreen = CreateIcon(ExitFullscreenPixel);

            float x = -Margin;
#if !UNITY_WEBGL
            // 웹 빌드는 브라우저 탭을 닫아야 해서 X 가 없다
            CreateButton("QuitButton", CreateIcon(CrossPixel), x, Quit);
            x -= ButtonSize + Margin * 0.5f;
#endif
            _fullscreenIcon = CreateButton("FullscreenButton", IconFor(Screen.fullScreen), x, ToggleFullscreen);
        }

        // Cmd+F · Alt+Enter 처럼 다른 방법으로 바뀌어도 아이콘이 따라가게 매 프레임 확인
        private void Update() => _fullscreenIcon.sprite = IconFor(Screen.fullScreen);

        private Sprite IconFor(bool fullscreen) => fullscreen ? _exitFullscreen : _enterFullscreen;

        private static void ToggleFullscreen()
        {
            if (Screen.fullScreen)
            {
                Screen.SetResolution(WindowedSize.x, WindowedSize.y, FullScreenMode.Windowed);
            }
            else
            {
                Screen.SetResolution(Display.main.systemWidth, Display.main.systemHeight, FullScreenMode.FullScreenWindow);
            }
        }

        private static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.ExitPlaymode();
#else
            Application.Quit();
#endif
        }

        /// <summary>오른쪽 위 기준 x 위치에 버튼을 만들고 아이콘 Image 를 돌려준다</summary>
        private Image CreateButton(string name, Sprite icon, float x, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            var rect = (RectTransform)go.transform;
            rect.SetParent(transform, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one;
            rect.anchoredPosition = new Vector2(x, -Margin);
            rect.sizeDelta = new Vector2(ButtonSize, ButtonSize);
            go.GetComponent<Image>().color = ButtonColor;

            var button = go.GetComponent<Button>();
            // 키보드로 버튼이 선택되지 않게: 선택된 채로 남으면 채팅 Enter 가 이 버튼을 누른다
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.onClick.AddListener(onClick);
            button.onClick.AddListener(() => EventSystem.current?.SetSelectedGameObject(null));

            var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            var iconRect = (RectTransform)iconGo.transform;
            iconRect.SetParent(rect, false);
            // 9px 아이콘을 정수 배(×2)로: 픽셀이 뭉개지지 않게
            iconRect.sizeDelta = new Vector2(IconPixels * 2f, IconPixels * 2f);
            var image = iconGo.GetComponent<Image>();
            image.sprite = icon;
            image.color = IconColor;
            image.raycastTarget = false;
            return image;
        }

        private static Sprite CreateIcon(System.Func<int, int, bool> pixel)
        {
            var texture = new Texture2D(IconPixels, IconPixels, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            for (int y = 0; y < IconPixels; y++)
            {
                for (int x = 0; x < IconPixels; x++)
                {
                    texture.SetPixel(x, y, pixel(x, y) ? Color.white : Color.clear);
                }
            }
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, IconPixels, IconPixels), new Vector2(0.5f, 0.5f), 32f);
        }

        // 네 모서리의 바깥쪽 꺾쇠 = 전체화면으로
        private static bool EnterFullscreenPixel(int x, int y)
        {
            int last = IconPixels - 1;
            bool edgeX = x == 0 || x == last;
            bool edgeY = y == 0 || y == last;
            bool nearX = x <= 2 || x >= last - 2;
            bool nearY = y <= 2 || y >= last - 2;
            return (edgeX && nearY) || (edgeY && nearX);
        }

        // 가운데를 향한 꺾쇠 = 창모드로
        private static bool ExitFullscreenPixel(int x, int y)
        {
            int last = IconPixels - 1;
            bool cornerX = x <= 3 || x >= last - 3;
            bool cornerY = y <= 3 || y >= last - 3;
            if (!cornerX || !cornerY)
            {
                return false;
            }
            bool innerX = x == 3 || x == last - 3;
            bool innerY = y == 3 || y == last - 3;
            return innerX || innerY;
        }

        private static bool CrossPixel(int x, int y)
        {
            int last = IconPixels - 1;
            return (x == y || x == last - y) && x >= 1 && x <= last - 1;
        }
    }
}

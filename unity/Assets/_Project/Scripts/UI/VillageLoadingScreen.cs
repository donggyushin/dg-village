using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace DgVillage
{
    /// <summary>
    /// 마을 씬(SampleScene)에 들어올 때의 로딩 화면 (Boot 와 같은 일러스트). 서버에 들어가 첫 welcome 을 받고
    /// MapLoader 가 방을 깔 때까지 덮어 두었다가 서서히 걷어낸다 → 미리보기 방이 서버 맵으로 바뀌는 순간이 안 보인다.
    /// 들어가기 전에 연결이 끊기면 안내를 띄우고, 아무 키(또는 클릭)나 누르면 채널 목록으로 돌아간다.
    /// 마을 맵 메뉴(DgVillage → Set Up Village Scene)가 VillageLoadingCanvas 로 만들어 붙인다.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class VillageLoadingScreen : MonoBehaviour
    {
        [SerializeField] private NetworkClient _client;
        [SerializeField] private TMP_Text _statusText;

        [Tooltip("입장이 빨라도 로딩 화면을 이만큼은 보여준다 (초)")]
        [SerializeField] private float _minimumSeconds = 1f;

        [Tooltip("로딩 화면이 사라지는 시간 (초)")]
        [SerializeField] private float _fadeSeconds = 0.6f;

        [Tooltip("입장에 실패했을 때 돌아갈 씬")]
        [SerializeField] private string _channelSelectScene = "ChannelSelect";

        private const string Entering = "마을에 들어가는 중";

        private CanvasGroup _group;
        private float _shownAt;
        private bool _entered;
        private bool _failed;

        private void Awake()
        {
            if (_client == null || _statusText == null)
            {
                Debug.LogError("VillageLoadingScreen: 비어 있는 칸이 있다. DgVillage → Set Up Village Scene 을 다시 실행할 것.", this);
                gameObject.SetActive(false);
                return;
            }
            _group = GetComponent<CanvasGroup>();
            _group.alpha = 1f;
            _group.blocksRaycasts = true;
            _shownAt = Time.realtimeSinceStartup;
            _client.Welcomed += OnWelcomed;
            _client.Disconnected += OnDisconnected;
        }

        private void OnDestroy()
        {
            if (_client != null)
            {
                _client.Welcomed -= OnWelcomed;
                _client.Disconnected -= OnDisconnected;
            }
        }

        private async void OnWelcomed(WelcomeMessage welcome)
        {
            if (_entered)
            {
                return; // 포탈 이동의 welcome 은 상관없다
            }
            _entered = true;
            _client.Disconnected -= OnDisconnected;
            _statusText.text = "";

            float remaining = _minimumSeconds - (Time.realtimeSinceStartup - _shownAt);
            if (remaining > 0f)
            {
                await Awaitable.WaitForSecondsAsync(remaining, destroyCancellationToken);
            }
            float elapsed = 0f;
            while (elapsed < _fadeSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                // smoothstep: 처음과 끝이 느려서 툭 끊기지 않는다
                _group.alpha = 1f - Mathf.SmoothStep(0f, 1f, elapsed / _fadeSeconds);
                await Awaitable.NextFrameAsync(destroyCancellationToken);
            }
            gameObject.SetActive(false);
        }

        private void OnDisconnected()
        {
            _failed = true;
            _statusText.text = "마을에 들어가지 못했어요\n아무 키나 누르면 채널 목록으로 돌아가요";
        }

        private void Update()
        {
            if (_failed)
            {
                if ((Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame)
                    || (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame))
                {
                    SceneManager.LoadScene(_channelSelectScene);
                }
                return;
            }
            if (!_entered)
            {
                int dots = (int)(Time.realtimeSinceStartup * 2f) % 4;
                _statusText.text = Entering + new string('.', dots);
            }
        }
    }
}

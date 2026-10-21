using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace DgVillage
{
    /// <summary>
    /// Boot 씬(게임을 켜면 처음 뜨는 씬)의 로딩 화면. 일러스트를 띄워 둔 채 서버에 로그인하고,
    /// 로그인이 빨리 끝나도 최소 _minimumSeconds 동안은 보여준 뒤 다음 씬(캐릭터 선택)을 연다.
    /// 다음 씬이 뜨면 이 캔버스를 DontDestroyOnLoad 로 살려 둔 채 서서히 투명하게 만들어 (페이드) 자연스럽게 넘어간다.
    /// 접속에 실패하면 안내를 띄우고, 아무 키(또는 클릭)나 누르면 다시 시도한다.
    /// 씬은 메뉴 DgVillage → Create Boot Scene 으로 만든다.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class LoadingScreen : MonoBehaviour
    {
        [Tooltip("로그인 후 열 씬 이름 (Build Profiles 의 Scene List 에 들어 있어야 한다)")]
        [SerializeField] private string _nextScene = "CharacterSelect";

        [Tooltip("접속이 빨라도 로딩 화면을 이만큼은 보여준다 (초)")]
        [SerializeField] private float _minimumSeconds = 2.5f;

        [Tooltip("다음 씬으로 넘어갈 때 로딩 화면이 사라지는 시간 (초)")]
        [SerializeField] private float _fadeSeconds = 0.8f;

        [SerializeField] private TMP_Text _statusText;

        private CanvasGroup _group;
        private float _shownAt;
        private string _status;
        // 접속 중이면 상태 문구 뒤에 점(...)이 움직인다
        private bool _connecting;
        private bool _failed;

        private void Awake()
        {
            if (_statusText == null)
            {
                Debug.LogError("LoadingScreen: 비어 있는 칸이 있다. DgVillage → Create Boot Scene 으로 다시 만들 것.", this);
                enabled = false;
                return;
            }
            _group = GetComponent<CanvasGroup>();
            _group.alpha = 1f;
            _shownAt = Time.realtimeSinceStartup;
        }

        private void Start() => Connect();

        private async void Connect()
        {
            _failed = false;
            SetStatus("서버에 접속하는 중", connecting: true);
            try
            {
                await AccountClient.LoginAsync();
            }
            catch (AccountException e)
            {
                SetStatus($"{e.Message}\n아무 키나 누르면 다시 시도해요", connecting: false);
                _failed = true;
                return;
            }

            // 처음 화면이 뜬 때부터 센다 (재시도까지 합쳐 이미 오래 봤으면 바로 넘어간다)
            float remaining = _minimumSeconds - (Time.realtimeSinceStartup - _shownAt);
            if (remaining > 0f)
            {
                await Awaitable.WaitForSecondsAsync(remaining, destroyCancellationToken);
            }

            await FadeIntoNextScene();
        }

        // 다음 씬을 연 뒤 그 위에서 로딩 화면을 서서히 걷어낸다
        private async Awaitable FadeIntoNextScene()
        {
            SetStatus("", connecting: false);
            DontDestroyOnLoad(gameObject);
            await SceneManager.LoadSceneAsync(_nextScene);

            float elapsed = 0f;
            while (elapsed < _fadeSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                // smoothstep: 처음과 끝이 느려서 툭 끊기지 않는다
                _group.alpha = 1f - Mathf.SmoothStep(0f, 1f, elapsed / _fadeSeconds);
                await Awaitable.NextFrameAsync(destroyCancellationToken);
            }
            Destroy(gameObject);
        }

        private void Update()
        {
            if (_failed && AnyInputPressed())
            {
                Connect();
            }

            if (_connecting)
            {
                int dots = (int)(Time.realtimeSinceStartup * 2f) % 4;
                _statusText.text = _status + new string('.', dots);
            }
        }

        private void SetStatus(string status, bool connecting)
        {
            _status = status;
            _connecting = connecting;
            _statusText.text = status;
        }

        private static bool AnyInputPressed()
        {
            return (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame)
                || (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame);
        }
    }
}

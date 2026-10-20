using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DgVillage
{
    /// <summary>
    /// CharacterSelect 씬(첫 화면)의 캐릭터 선택. 뜨자마자 서버에 로그인(없으면 익명 계정 생성)하고
    /// 계정 프로필의 닉네임 · 캐릭터로 미리 채운다. ◀ ▶ (← → / A D) 로 캐릭터를 고르고 닉네임을 고친 뒤
    /// 입장 버튼(또는 Enter)을 누르면 프로필을 서버에 저장(PATCH /me)하고 채널 목록 씬을 연다.
    /// 씬은 메뉴 DgVillage → Create Character Select Scene 으로 만든다.
    /// </summary>
    public class CharacterSelect : MonoBehaviour
    {
        [SerializeField] private CharacterRegistry _characters;

        [Tooltip("입장하면 열 씬 이름 (Build Profiles 의 Scene List 에 들어 있어야 한다)")]
        [SerializeField] private string _channelSelectScene = "ChannelSelect";

        [Header("UI")]
        [SerializeField] private Image _preview;
        [SerializeField] private TMP_Text _characterName;
        [SerializeField] private TMP_InputField _nameField;
        [SerializeField] private Button _previousButton;
        [SerializeField] private Button _nextButton;
        [SerializeField] private Button _enterButton;
        [SerializeField] private TMP_Text _statusText;

        [Tooltip("미리보기 걷기 애니메이션 속도")]
        [SerializeField] private float _previewFramesPerSecond = 8f;

        private int _index;
        private float _previewTimer;
        private int _previewFrame;
        // 서버와 통신 중 (로그인 · 저장). 이때는 입장을 다시 누를 수 없다
        private bool _busy;

        private CharacterDefinition Current => _characters.Characters[_index];

        private void Awake()
        {
            if (_characters == null || _characters.Characters.Count == 0
                || _preview == null || _characterName == null || _nameField == null
                || _previousButton == null || _nextButton == null || _enterButton == null || _statusText == null)
            {
                Debug.LogError("CharacterSelect: 비어 있는 칸이 있다. DgVillage → Create Character Select Scene 으로 다시 만들 것.", this);
                enabled = false;
                return;
            }

            _nameField.characterLimit = AccountClient.DisplayNameMaxLength;
            _nameField.lineType = TMP_InputField.LineType.SingleLine;
            _nameField.richText = false;
            _nameField.onSubmit.AddListener(_ => Enter());

            _previousButton.onClick.AddListener(() => Select(_index - 1));
            _nextButton.onClick.AddListener(() => Select(_index + 1));
            _enterButton.onClick.AddListener(Enter);

            Select(0);
        }

        private void Start() => Login();

        // 저장된 계정으로 로그인하고 프로필로 화면을 채운다. 실패하면 입장 버튼이 재시도가 된다
        private async void Login()
        {
            SetBusy(true, "서버에 접속하는 중...");
            try
            {
                await AccountClient.LoginAsync();
            }
            catch (AccountException e)
            {
                SetBusy(false, $"{e.Message}\n입장을 누르면 다시 시도해요");
                return;
            }

            Profile profile = AccountClient.Profile;
            _nameField.text = profile.displayName;
            int savedIndex = FindIndex(profile.characterId);
            Select(savedIndex >= 0 ? savedIndex : 0);
            SetBusy(false, "");
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && !_nameField.isFocused)
            {
                if (keyboard.leftArrowKey.wasPressedThisFrame || keyboard.aKey.wasPressedThisFrame)
                {
                    Select(_index - 1);
                }
                else if (keyboard.rightArrowKey.wasPressedThisFrame || keyboard.dKey.wasPressedThisFrame)
                {
                    Select(_index + 1);
                }
                else if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame)
                {
                    Enter();
                }
            }

            AnimatePreview();
        }

        private void Select(int index)
        {
            int count = _characters.Characters.Count;
            _index = (index % count + count) % count;
            _characterName.text = Current.DisplayName;
            _previewTimer = 0f;
            _previewFrame = 0;
            _preview.sprite = Current.Preview;
        }

        // 정면(아래) 걷기 프레임을 돌려가며 보여준다. 걷기가 없으면 서 있는 모습 그대로
        private void AnimatePreview()
        {
            Sprite[] walk = Current.Get(Facing.Down).Walk;
            if (walk == null || walk.Length == 0)
            {
                return;
            }

            _previewTimer += Time.deltaTime;
            float frameDuration = 1f / _previewFramesPerSecond;
            while (_previewTimer >= frameDuration)
            {
                _previewTimer -= frameDuration;
                _previewFrame++;
            }
            _preview.sprite = walk[_previewFrame % walk.Length];
        }

        private async void Enter()
        {
            if (_busy)
            {
                return;
            }
            if (!AccountClient.IsLoggedIn)
            {
                Login();
                return;
            }

            // 닉네임을 비우면 지금 이름(처음엔 서버가 지어준 guest1234)을 그대로 쓴다
            string input = _nameField.text;
            string displayName = string.IsNullOrWhiteSpace(input)
                ? AccountClient.Profile.displayName
                : AccountClient.NormalizeDisplayName(input);
            if (displayName == null)
            {
                _statusText.text = $"닉네임은 {AccountClient.DisplayNameMaxLength}자까지 쓸 수 있어요";
                return;
            }

            SetBusy(true, "저장하는 중...");
            try
            {
                await AccountClient.UpdateProfileAsync(displayName, Current.Id);
            }
            catch (AccountException e)
            {
                SetBusy(false, e.Message);
                return;
            }
            SceneManager.LoadScene(_channelSelectScene);
        }

        private void SetBusy(bool busy, string status)
        {
            _busy = busy;
            _enterButton.interactable = !busy;
            _statusText.text = status;
        }

        private int FindIndex(string id)
        {
            for (int i = 0; i < _characters.Characters.Count; i++)
            {
                if (_characters.Characters[i] != null && _characters.Characters[i].Id == id)
                {
                    return i;
                }
            }
            return -1;
        }
    }
}

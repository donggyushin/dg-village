using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DgVillage
{
    /// <summary>
    /// CharacterSelect 씬(첫 화면)의 캐릭터 선택. ◀ ▶ (← → / A D) 로 캐릭터를 고르고 닉네임을 넣은 뒤
    /// 입장 버튼(또는 Enter)을 누르면 PlayerSession 에 저장하고 마을 씬을 연다.
    /// 마을 씬의 NetworkClient 가 PlayerSession 을 읽어 그 캐릭터 · 이름으로 서버에 들어간다.
    /// 씬은 메뉴 DgVillage → Create Character Select Scene 으로 만든다.
    /// </summary>
    public class CharacterSelect : MonoBehaviour
    {
        [SerializeField] private CharacterRegistry _characters;

        [Tooltip("입장하면 열 씬 이름 (Build Profiles 의 Scene List 에 들어 있어야 한다)")]
        [SerializeField] private string _villageScene = "SampleScene";

        [Header("UI")]
        [SerializeField] private Image _preview;
        [SerializeField] private TMP_Text _characterName;
        [SerializeField] private TMP_InputField _nameField;
        [SerializeField] private Button _previousButton;
        [SerializeField] private Button _nextButton;
        [SerializeField] private Button _enterButton;

        [Tooltip("미리보기 걷기 애니메이션 속도")]
        [SerializeField] private float _previewFramesPerSecond = 8f;

        private int _index;
        private float _previewTimer;
        private int _previewFrame;
        private bool _entering;

        private CharacterDefinition Current => _characters.Characters[_index];

        private void Awake()
        {
            if (_characters == null || _characters.Characters.Count == 0
                || _preview == null || _characterName == null || _nameField == null
                || _previousButton == null || _nextButton == null || _enterButton == null)
            {
                Debug.LogError("CharacterSelect: 비어 있는 칸이 있다. DgVillage → Create Character Select Scene 으로 다시 만들 것.", this);
                enabled = false;
                return;
            }

            _nameField.characterLimit = NetworkClient.NameMaxLength;
            _nameField.lineType = TMP_InputField.LineType.SingleLine;
            _nameField.richText = false;
            _nameField.text = PlayerSession.PlayerName;
            _nameField.onSubmit.AddListener(_ => Enter());

            _previousButton.onClick.AddListener(() => Select(_index - 1));
            _nextButton.onClick.AddListener(() => Select(_index + 1));
            _enterButton.onClick.AddListener(Enter);

            int savedIndex = FindIndex(PlayerSession.CharacterId);
            Select(savedIndex >= 0 ? savedIndex : 0);
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

        private void Enter()
        {
            // 씬 로딩이 끝나기 전 Enter 가 한 번 더 들어와도 무시
            if (_entering)
            {
                return;
            }
            _entering = true;

            PlayerSession.Save(_nameField.text, Current.Id);
            SceneManager.LoadScene(_villageScene);
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

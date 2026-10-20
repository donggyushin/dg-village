using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DgVillage
{
    /// <summary>
    /// ChannelSelect 씬(캐릭터 선택 다음 화면)의 채널 목록. 뜨면 GET /channels 로 목록을 받아
    /// 채널마다 ChannelListItem 을 만든다. 하나를 누르면 ChannelClient 에 기억하고 마을 씬을 연다.
    /// 아래 입력칸에 이름을 쓰고 만들기(또는 Enter)를 누르면 새 채널을 만들고 바로 들어간다.
    /// 씬은 메뉴 DgVillage → Create Channel Select Scene 으로 만든다.
    /// </summary>
    public class ChannelSelect : MonoBehaviour
    {
        [Tooltip("채널을 고르면 열 씬 이름 (Build Profiles 의 Scene List 에 들어 있어야 한다)")]
        [SerializeField] private string _villageScene = "SampleScene";

        [Tooltip("뒤로 버튼(또는 Esc)으로 돌아갈 씬 이름")]
        [SerializeField] private string _characterSelectScene = "CharacterSelect";

        [Header("UI")]
        [Tooltip("목록 한 줄의 원본. 씬에서는 꺼두고, 채널마다 복제해서 켠다")]
        [SerializeField] private ChannelListItem _itemTemplate;
        [Tooltip("복제한 줄을 넣을 곳 (ScrollView 의 Content)")]
        [SerializeField] private RectTransform _listContent;
        [SerializeField] private TMP_InputField _nameField;
        [SerializeField] private Button _createButton;
        [SerializeField] private Button _refreshButton;
        [SerializeField] private Button _backButton;
        [SerializeField] private TMP_Text _statusText;

        private readonly List<ChannelListItem> _items = new();
        // 서버와 통신 중. 이때는 버튼을 다시 누를 수 없다
        private bool _busy;

        private void Awake()
        {
            if (_itemTemplate == null || _listContent == null || _nameField == null
                || _createButton == null || _refreshButton == null || _backButton == null || _statusText == null)
            {
                Debug.LogError("ChannelSelect: 비어 있는 칸이 있다. DgVillage → Create Channel Select Scene 으로 다시 만들 것.", this);
                enabled = false;
                return;
            }

            _itemTemplate.gameObject.SetActive(false);
            _nameField.characterLimit = ChannelClient.NameMaxLength;
            _nameField.lineType = TMP_InputField.LineType.SingleLine;
            _nameField.richText = false;
            _nameField.onSubmit.AddListener(_ => Create());

            _createButton.onClick.AddListener(Create);
            _refreshButton.onClick.AddListener(Refresh);
            _backButton.onClick.AddListener(Back);
        }

        private void Start() => Refresh();

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame && !_nameField.isFocused)
            {
                Back();
            }
        }

        private async void Refresh()
        {
            if (_busy)
            {
                return;
            }
            SetBusy(true, "채널 목록을 받는 중...");
            ChannelInfo[] channels;
            try
            {
                // 이 씬을 에디터에서 바로 Play 한 경우만 여기서 로그인
                if (!AccountClient.IsLoggedIn)
                {
                    await AccountClient.LoginAsync();
                }
                channels = await ChannelClient.ListAsync();
            }
            catch (AccountException e)
            {
                SetBusy(false, $"{e.Message}\n새로고침을 누르면 다시 시도해요");
                return;
            }

            ShowChannels(channels);
            SetBusy(false, "");
        }

        private void ShowChannels(ChannelInfo[] channels)
        {
            foreach (ChannelListItem item in _items)
            {
                Destroy(item.gameObject);
            }
            _items.Clear();

            foreach (ChannelInfo channel in channels)
            {
                ChannelListItem item = Instantiate(_itemTemplate, _listContent);
                item.gameObject.SetActive(true);
                item.Bind(channel, Join);
                _items.Add(item);
            }
        }

        private void Join(ChannelInfo channel)
        {
            if (_busy)
            {
                return;
            }
            ChannelClient.Select(channel);
            SceneManager.LoadScene(_villageScene);
        }

        private async void Create()
        {
            if (_busy)
            {
                return;
            }
            string channelName = ChannelClient.NormalizeName(_nameField.text);
            if (channelName == null)
            {
                _statusText.text = $"채널 이름은 1~{ChannelClient.NameMaxLength}자로 써 주세요";
                return;
            }

            SetBusy(true, "채널을 만드는 중...");
            ChannelInfo channel;
            try
            {
                channel = await ChannelClient.CreateAsync(channelName);
            }
            catch (AccountException e)
            {
                SetBusy(false, e.Status == 409
                    ? $"채널은 한 사람당 {ChannelClient.MaxChannelsPerUser}개까지 만들 수 있어요"
                    : e.Message);
                return;
            }
            SetBusy(false, "");
            Join(channel);
        }

        private void Back()
        {
            if (!_busy)
            {
                SceneManager.LoadScene(_characterSelectScene);
            }
        }

        private void SetBusy(bool busy, string status)
        {
            _busy = busy;
            _createButton.interactable = !busy;
            _refreshButton.interactable = !busy;
            foreach (ChannelListItem item in _items)
            {
                item.Interactable = !busy;
            }
            _statusText.text = status;
        }
    }
}

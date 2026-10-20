using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DgVillage
{
    /// <summary>
    /// 마을 씬 화면 구석의 채널 표시: 지금 채널 이름 + "채널 나가기" 버튼.
    /// 나가기를 누르면 채널 목록 씬으로 돌아간다. 씬이 바뀌며 NetworkClient 가 파괴될 때(OnDestroy) 연결을 닫으므로
    /// 서버는 그걸 보고 다른 사람들에게 player_left 를 보낸다.
    /// </summary>
    public class ChannelHud : MonoBehaviour
    {
        [Tooltip("나가기를 누르면 열 씬 이름 (Build Profiles 의 Scene List 에 들어 있어야 한다)")]
        [SerializeField] private string _channelSelectScene = "ChannelSelect";

        [SerializeField] private TMP_Text _channelName;
        [SerializeField] private Button _leaveButton;

        private void Awake()
        {
            if (_channelName == null || _leaveButton == null)
            {
                Debug.LogError("ChannelHud: Channel Name 또는 Leave Button 이 비어 있다. Inspector 에서 연결할 것.", this);
                enabled = false;
                return;
            }
            _leaveButton.onClick.AddListener(Leave);
        }

        private void Start()
        {
            // 마을 씬을 바로 Play 하면 고른 채널이 없다 = 광장
            ChannelInfo channel = ChannelClient.Current;
            _channelName.text = channel != null ? channel.name : "광장";
        }

        private void Leave() => SceneManager.LoadScene(_channelSelectScene);
    }
}

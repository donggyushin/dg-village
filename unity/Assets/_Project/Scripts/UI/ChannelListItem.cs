using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DgVillage
{
    /// <summary>
    /// 채널 목록의 한 줄 (버튼). ChannelSelect 가 씬에 꺼둔 템플릿을 복제(Instantiate)해서 채널마다 하나씩 만든다.
    /// </summary>
    public class ChannelListItem : MonoBehaviour
    {
        [SerializeField] private Button _button;
        [SerializeField] private TMP_Text _name;
        [SerializeField] private TMP_Text _playerCount;

        public void Bind(ChannelInfo channel, Action<ChannelInfo> onClick)
        {
            _name.text = channel.name;
            _playerCount.text = $"{channel.playerCount}명";
            _button.onClick.RemoveAllListeners();
            _button.onClick.AddListener(() => onClick(channel));
        }

        public bool Interactable
        {
            set => _button.interactable = value;
        }
    }
}

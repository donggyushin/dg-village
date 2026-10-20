using System.Collections.Generic;
using UnityEngine;

namespace DgVillage
{
    /// <summary>
    /// 서버에서 채팅이 오면 말한 사람(나 또는 RemotePlayer)의 이름표 위에 말풍선을 띄운다.
    /// 같은 사람이 새로 말하면 이전 말풍선은 바로 지운다.
    /// </summary>
    public class ChatBubbleSpawner : MonoBehaviour
    {
        [SerializeField] private NetworkClient _client;
        [SerializeField] private RemotePlayerSpawner _remotePlayers;
        [SerializeField] private PlayerMovement _localPlayer;
        [SerializeField] private ChatBubble _bubblePrefab;

        [Tooltip("말풍선을 담을 부모 (입력창보다 뒤에 그려지도록 Canvas 의 첫 번째 자식)")]
        [SerializeField] private RectTransform _bubbleRoot;

        private readonly Dictionary<string, ChatBubble> _bubbles = new();

        private void Awake()
        {
            if (_client == null || _remotePlayers == null || _localPlayer == null
                || _bubblePrefab == null || _bubbleRoot == null)
            {
                Debug.LogError("ChatBubbleSpawner: Inspector 에 비어 있는 칸이 있다.", this);
                enabled = false;
            }
        }

        private void OnEnable()
        {
            if (_client != null)
            {
                _client.ChatReceived += OnChatReceived;
            }
        }

        private void OnDisable()
        {
            if (_client != null)
            {
                _client.ChatReceived -= OnChatReceived;
            }
        }

        private void OnChatReceived(ChatBroadcastMessage chat)
        {
            Transform anchor = FindAnchor(chat.id);
            if (anchor == null)
            {
                return;
            }

            if (_bubbles.TryGetValue(chat.id, out ChatBubble previous) && previous != null)
            {
                Destroy(previous.gameObject);
            }

            ChatBubble bubble = Instantiate(_bubblePrefab, _bubbleRoot);
            bubble.Show(anchor, chat.text, DurationFor(chat.text));
            _bubbles[chat.id] = bubble;
        }

        // 이름표가 있으면 그 위에, 없으면 캐릭터 발밑 기준
        private Transform FindAnchor(string playerId)
        {
            Transform root = null;
            if (playerId == _client.LocalPlayerId)
            {
                root = _localPlayer.transform;
            }
            else if (_remotePlayers.TryGetPlayer(playerId, out RemotePlayer remote))
            {
                root = remote.transform;
            }

            if (root == null)
            {
                return null;
            }
            var nameTag = root.GetComponentInChildren<NameTag>();
            return nameTag != null ? nameTag.transform : root;
        }

        // 긴 말일수록 오래 보여준다 (3~8초)
        private static float DurationFor(string text) => Mathf.Clamp(3f + text.Length * 0.1f, 3f, 8f);
    }
}

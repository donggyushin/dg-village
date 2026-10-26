using UnityEngine;
using TMPro;
using System.Collections;
using UnityEngine.Networking;
using Newtonsoft.Json;
using UnityEngine.UI;

public class ChannelSelect : MonoBehaviour
{
    [SerializeField] private Transform _content;
    [SerializeField] private GameObject _channelItem;

    private static string ServersUrl => ServerConfig.BaseUrl + "/servers";

    private class ServerInfo
    {
        public int id;
        public string name;
    }

    private void Start()
    {
        StartCoroutine(FetchServerList());
    }

    private IEnumerator FetchServerList()
    {
        using (UnityWebRequest request = UnityWebRequest.Get(ServersUrl))
        {
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"서버 목록 요청 실패: {request.error}");
                yield break;
            }

            ServerInfo[] serverInfos = JsonConvert.DeserializeObject<ServerInfo[]>(request.downloadHandler.text);
            ShowServers(serverInfos);
        }
    }

    private void ShowServers(ServerInfo[] servers)
    {
        foreach (var server in servers)
        {
            GameObject channel = Instantiate(_channelItem, _content);
            TMP_Text text = channel.GetComponentInChildren<TMP_Text>();
            text.text = server.name;

            Button button = channel.GetComponent<Button>();
            button.onClick.AddListener(() => OnChannelSelected(server));
        }
    }

    private void OnChannelSelected(ServerInfo server)
    {
        Debug.Log($"{server.id} {server.name} 채널 선택");
    }

}

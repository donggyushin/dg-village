using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{
    public void GameStart()
    {
        // TODO: - Loading 화면 필요
        SceneManager.LoadSceneAsync("ChannelSelect");
    }
}

using System.Collections;
using Newtonsoft.Json;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenu : MonoBehaviour
{
    private class LoginRequestBody { public string username; public string password; }
    private class AccountResponse { public string account_id; }

    [SerializeField] private TMP_InputField _usernameInput;
    [SerializeField] private TMP_InputField _passwordInput;
    [SerializeField] private Button _loginButton;
    [SerializeField] private Button _signUpButton;
    [SerializeField] private Toast _toast;

    void Awake()
    {
        _loginButton.onClick.AddListener(Login);
        _signUpButton.onClick.AddListener(MakeNewAccount);
    }

    private void MakeNewAccount()
    {
        Debug.Log("회원가입 화면으로 이동");
    }

    private void Login()
    {
        _loginButton.interactable = false;
        string username = _usernameInput.text;
        string password = _passwordInput.text;

        if (username == "" || password == "")
        {
            _toast.Show("아이디와 비밀번호를 입력해 주세요");
            return;
        }
        StartCoroutine(LoginRequest(username, password));
    }

    private IEnumerator LoginRequest(string username, string password)
    {
        string url = ServerConfig.BaseUrl + "/login";
        string json = JsonConvert.SerializeObject(new LoginRequestBody { username = username, password = password });

        using (UnityWebRequest request = UnityWebRequest.Post(url, json, "application/json"))
        {
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.ConnectionError)
            {
                _toast.Show("서버에 연결할 수 없습니다");
            }
            else if (request.responseCode == 401)
            {
                _toast.Show("아이디 또는 비밀번호가 틀렸습니다");
            }
            else if (request.responseCode == 422)
            {
                _toast.Show("아이디/비밀번호 형식을 확인해 주세요");
            }
            else if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"로그인 실패 {request.responseCode}: {request.downloadHandler.text}");
                _toast.Show("로그인에 실패했습니다");
            }
            else
            {
                var response = JsonConvert.DeserializeObject<AccountResponse>(request.downloadHandler.text);
                Session.SetAccountId(response.account_id);
                SceneManager.LoadSceneAsync("ChannelSelect");
                yield break;              // 성공이면 버튼을 다시 풀 필요 없음
            }

            _loginButton.interactable = true;   // 실패했으니 다시 시도할 수 있게
        }
    }
}

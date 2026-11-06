using System.Collections;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class NewAccount : MonoBehaviour
{
    private class SignupRequestBody { public string username; public string password; }
    private class AccountResponse { public string account_id; }

    [SerializeField] private TMP_InputField _usernameInput;
    [SerializeField] private TMP_InputField _password1Input;
    [SerializeField] private TMP_InputField _password2Input;
    [SerializeField] private Button _signUpButton;
    [SerializeField] private Button _backButton;
    [SerializeField] private Toast _toast;

    void Awake()
    {
        _signUpButton.onClick.AddListener(MakeNewAccount);
        _backButton.onClick.AddListener(BackButton);
    }

    private void BackButton()
    {
        SceneManager.LoadSceneAsync("MainMenu");
    }

    private void MakeNewAccount()
    {
        string username = _usernameInput.text.Trim();
        string password = _password1Input.text;

        // 서버도 검사하지만(422), 먼저 걸러 내면 무엇이 틀렸는지 정확히 알려 줄 수 있다
        if (username == "" || password == "")
        {
            _toast.Show("아이디와 비밀번호를 입력해 주세요");
            return;
        }
        if (!Regex.IsMatch(username, "^[A-Za-z0-9_]{3,20}$"))
        {
            _toast.Show("아이디는 영문·숫자·_ 3~20자입니다");
            return;
        }
        if (password.Length < 4)
        {
            _toast.Show("비밀번호는 4자 이상입니다");
            return;
        }
        if (password != _password2Input.text)
        {
            _toast.Show("비밀번호가 서로 다릅니다");
            return;
        }

        _signUpButton.interactable = false;   // 요청 중 중복 클릭 방지
        StartCoroutine(SignupRequest(username, password));
    }

    private IEnumerator SignupRequest(string username, string password)
    {
        string url = ServerConfig.BaseUrl + "/signup";
        string json = JsonConvert.SerializeObject(new SignupRequestBody { username = username, password = password });

        using (UnityWebRequest request = UnityWebRequest.Post(url, json, "application/json"))
        {
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.ConnectionError)
            {
                _toast.Show("서버에 연결할 수 없습니다");
            }
            else if (request.responseCode == 409)
            {
                _toast.Show("이미 사용 중인 아이디입니다");
            }
            else if (request.responseCode == 422)
            {
                _toast.Show("아이디/비밀번호 형식을 확인해 주세요");
            }
            else if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"회원가입 실패 {request.responseCode}: {request.downloadHandler.text}");
                _toast.Show("회원가입에 실패했습니다");
            }
            else
            {
                // 서버는 가입과 동시에 로그인된 것으로 보고 account_id 를 준다
                var response = JsonConvert.DeserializeObject<AccountResponse>(request.downloadHandler.text);
                Session.SetAccountId(response.account_id);
                SceneManager.LoadSceneAsync("ChannelSelect");
                yield break;
            }

            _signUpButton.interactable = true;   // 실패했으니 다시 시도할 수 있게
        }
    }
}

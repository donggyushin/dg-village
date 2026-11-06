using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class NewAccount : MonoBehaviour
{
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

    }
}

using TMPro;
using UnityEngine;

public class NicknameLabel : MonoBehaviour
{
    [SerializeField] private TMP_Text label;

    public void SetName(string nickname)
    {
        label.text = nickname;
    }
}

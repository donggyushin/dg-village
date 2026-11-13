using UnityEngine;

// 플레이어가 올라탈 수 있는 움직이는 발판(배, 엘리베이터 등)
// id는 서버 platform.py 의 PLATFORMS 와 같아야 한다.
public class Platform : MonoBehaviour
{
    [SerializeField] private string _platformId;
    public string PlatformId => _platformId;
}

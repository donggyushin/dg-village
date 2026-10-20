using System;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace DgVillage
{
    /// <summary>서버가 요청을 거절했거나 서버에 닿지 못했다. Message 는 화면에 보여줄 수 있는 짧은 설명.</summary>
    public class AccountException : Exception
    {
        /// <summary>HTTP 상태 코드. 서버에 닿지 못했으면 0.</summary>
        public long Status { get; }

        public AccountException(long status, string message) : base(message) => Status = status;
    }

    /// <summary>
    /// 계정 (서버 api.py 의 HTTP 엔드포인트). 회원가입 화면 없이 익명 계정을 쓴다:
    /// 처음 실행하면 서버가 계정 + credential(기기 비밀값)을 만들어주고, PlayerPrefs 에 보관해 다음부터는 그걸로 로그인한다.
    /// 로그인 결과인 AccessToken 으로 /me 와 WebSocket join 을 한다.
    /// 나중에 Steam 로그인 등이 생기면 LoginRequest.provider 만 다른 로그인 경로가 추가된다.
    /// static 이라 씬이 바뀌어도 로그인 상태가 남는다. UnityWebRequest + Awaitable 이라 WebGL 에서도 동작한다.
    /// </summary>
    public static class AccountClient
    {
        /// <summary>서버 DISPLAY_NAME_MAX_LENGTH 와 같게 유지</summary>
        public const int DisplayNameMaxLength = 16;

        // 이 값을 잃으면 그 익명 계정으로 다시 들어갈 방법이 없다 (소셜 로그인 연결 전까지)
        private const string CredentialPrefsKey = "dgvillage.anonymousCredential";
        private const string AnonymousProvider = "anonymous";

        public static string AccessToken { get; private set; }

        /// <summary>로그인 후 서버에서 받은 내 프로필. UpdateProfileAsync 하면 갱신된다.</summary>
        public static Profile Profile { get; private set; }

        public static bool IsLoggedIn => AccessToken != null && Profile != null;

        /// <summary>
        /// 저장된 credential 로 로그인하고, 없거나 서버가 모르면(DB 초기화 등) 새 익명 계정을 만든다. 끝나면 Profile 이 채워진다.
        /// </summary>
        public static async Awaitable LoginAsync()
        {
            AccessToken = null;
            Profile = null;

            string credential = PlayerPrefs.GetString(CredentialPrefsKey, "");
            if (credential != "")
            {
                try
                {
                    var login = await SendAsync<LoginResponse>("POST", "/auth/login",
                        new LoginRequest { provider = AnonymousProvider, credential = credential });
                    AccessToken = login.accessToken;
                }
                catch (AccountException e) when (e.Status == 401)
                {
                    Debug.LogWarning($"AccountClient: 저장된 계정으로 로그인 실패 ({e.Message}). 새 익명 계정을 만든다.");
                }
            }

            if (AccessToken == null)
            {
                var signup = await SendAsync<SignupResponse>("POST", "/auth/anonymous", null);
                PlayerPrefs.SetString(CredentialPrefsKey, signup.credential);
                PlayerPrefs.Save();
                AccessToken = signup.accessToken;
                Debug.Log($"AccountClient: 새 익명 계정 {signup.userId}");
            }

            Profile = await SendAsync<Profile>("GET", "/me", null);
        }

        public static async Awaitable UpdateProfileAsync(string displayName, string characterId)
        {
            Profile = await SendAsync<Profile>("PATCH", "/me",
                new ProfileUpdate { displayName = displayName, characterId = characterId });
        }

        /// <summary>서버 normalize_display_name 과 같은 규칙: 공백을 하나로 줄이고 1~16자. 안 맞으면 null.</summary>
        public static string NormalizeDisplayName(string value) => NormalizeName(value, DisplayNameMaxLength);

        /// <summary>서버 _normalize_name 과 같은 규칙: 공백을 하나로 줄이고 1~maxLength 자. 안 맞으면 null.</summary>
        public static string NormalizeName(string value, int maxLength)
        {
            string normalized = string.Join(" ",
                (value ?? "").Split((char[])null, StringSplitOptions.RemoveEmptyEntries));
            return normalized.Length >= 1 && normalized.Length <= maxLength ? normalized : null;
        }

        /// <summary>서버 HTTP API 호출. 로그인했으면 AccessToken 을 붙인다. ChannelClient 도 이걸 쓴다.</summary>
        internal static async Awaitable<T> SendAsync<T>(string method, string path, object body)
        {
            using var request = new UnityWebRequest(ServerConfig.HttpBaseUrl + path, method);
            request.downloadHandler = new DownloadHandlerBuffer();
            if (body != null)
            {
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(JsonUtility.ToJson(body)));
                request.SetRequestHeader("Content-Type", "application/json");
            }
            if (AccessToken != null)
            {
                request.SetRequestHeader("Authorization", $"Bearer {AccessToken}");
            }

            await request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.ConnectionError)
            {
                throw new AccountException(0, "서버에 연결할 수 없어요");
            }
            if (request.result != UnityWebRequest.Result.Success)
            {
                throw new AccountException(request.responseCode, ErrorDetail(request));
            }
            return JsonUtility.FromJson<T>(request.downloadHandler.text);
        }

        private static string ErrorDetail(UnityWebRequest request)
        {
            try
            {
                string detail = JsonUtility.FromJson<HttpError>(request.downloadHandler.text)?.detail;
                if (!string.IsNullOrEmpty(detail))
                {
                    return detail;
                }
            }
            catch (ArgumentException)
            {
                // JSON 이 아닌 응답 (프록시 에러 페이지 등)
            }
            return $"{request.responseCode} {request.error}";
        }
    }
}

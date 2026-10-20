namespace DgVillage
{
    /// <summary>
    /// 서버 주소. 지금은 로컬 개발 서버 하나뿐이라 상수로 둔다.
    /// 배포(서울 VPS)할 때 https:// · wss:// 주소로 바꾸거나 빌드별 설정으로 뺀다.
    /// </summary>
    public static class ServerConfig
    {
        public const string HttpBaseUrl = "http://localhost:8000";
        public const string WebSocketUrl = "ws://localhost:8000/ws";
    }
}

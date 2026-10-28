using UnityEngine;

public static class ServerConfig
{
    private const string LocalUrl = "http://127.0.0.1:8000";
    private const string ProductionUrl = "https://13-54-176-157.sslip.io";
    public static string BaseUrl => Debug.isDebugBuild ? LocalUrl : ProductionUrl;
    public static string WebSocketUrl => BaseUrl.Replace("http", "ws") + "/ws";
}

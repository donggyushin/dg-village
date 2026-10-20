using System;
using UnityEngine;

namespace DgVillage
{
    /// <summary>
    /// 지금 접속할 서버 주소. 정하는 순서:
    ///   1. 실행 인자 -server &lt;url&gt;  (예: open DgVillage.app --args -server http://localhost:8000)
    ///   2. ServerSettings 에셋: 에디터면 EditorUsesProduction, 빌드면 BuildsUseProduction 에 따라 Production 또는 Local
    ///   3. 에셋이 없거나 Production 주소가 비었으면 Local (http://localhost:8000)
    /// 처음 쓸 때 한 번 정하고 끝까지 같은 값을 쓴다.
    /// </summary>
    public static class ServerConfig
    {
        private const string DefaultUrl = "http://localhost:8000";
        private const string CommandLineFlag = "-server";

        private static string _httpBaseUrl;

        // 이 프로젝트는 Domain Reload 를 꺼둬서 static 값이 다음 Play 까지 남는다. Play 마다 다시 정하게 비운다
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay() => _httpBaseUrl = null;

        /// <summary>예: https://13-125-1-2.sslip.io (끝에 / 없음)</summary>
        public static string HttpBaseUrl => _httpBaseUrl ??= Resolve();

        /// <summary>HttpBaseUrl 의 http → ws, https → wss 에 /ws 를 붙인 것</summary>
        public static string WebSocketUrl
        {
            get
            {
                string url = HttpBaseUrl;
                if (url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    return "wss://" + url.Substring("https://".Length) + "/ws";
                }
                if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
                {
                    return "ws://" + url.Substring("http://".Length) + "/ws";
                }
                return url + "/ws";
            }
        }

        private static string Resolve()
        {
            string url = FromCommandLine();
            string source = "실행 인자 -server";
            if (url == null)
            {
                var settings = Resources.Load<ServerSettings>(ServerSettings.ResourcePath);
                if (settings == null)
                {
                    Debug.LogWarning("ServerConfig: ServerSettings 에셋이 없다 (DgVillage → Server Settings). 로컬 서버를 쓴다.");
                    url = DefaultUrl;
                    source = "기본값";
                }
                else
                {
                    bool production = Application.isEditor ? settings.EditorUsesProduction : settings.BuildsUseProduction;
                    if (production && string.IsNullOrWhiteSpace(settings.ProductionUrl))
                    {
                        Debug.LogWarning("ServerConfig: Production 을 쓰도록 되어 있지만 주소가 비어 있다. 로컬 서버를 쓴다.");
                        production = false;
                    }
                    url = production ? settings.ProductionUrl : settings.LocalUrl;
                    source = production ? "Production" : "Local";
                }
            }

            url = url.Trim().TrimEnd('/');
            Debug.Log($"ServerConfig: {url} ({source})");
            return url;
        }

        private static string FromCommandLine()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == CommandLineFlag && !string.IsNullOrWhiteSpace(args[i + 1]))
                {
                    return args[i + 1];
                }
            }
            return null;
        }
    }
}

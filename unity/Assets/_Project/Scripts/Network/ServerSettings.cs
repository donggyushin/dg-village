using UnityEngine;

namespace DgVillage
{
    /// <summary>
    /// 접속할 서버 주소 (에셋 Assets/_Project/Resources/ServerSettings.asset).
    /// 주소는 http(s) 하나만 적는다. WebSocket 주소는 ServerConfig 가 거기서 만든다 (https → wss, + "/ws").
    /// 메뉴 DgVillage → Server Settings 로 에셋을 열거나 만든다.
    /// Resources 폴더에 있어야 실행 중에 Resources.Load 로 찾을 수 있고 빌드에도 들어간다.
    /// </summary>
    public class ServerSettings : ScriptableObject
    {
        public const string ResourcePath = "ServerSettings";

        [Tooltip("내 맥에서 uv run uvicorn 으로 띄운 개발 서버")]
        [SerializeField] private string _localUrl = "http://localhost:8000";

        [Tooltip("운영 서버 (VPS). 예: https://13-125-1-2.sslip.io  — deploy/.env 의 DG_DOMAIN 에 https:// 를 붙인 것")]
        [SerializeField] private string _productionUrl = "";

        [Tooltip("에디터 Play 에서 운영 서버에 붙을지 (보통 꺼둔다)")]
        [SerializeField] private bool _editorUsesProduction;

        [Tooltip("빌드한 앱이 운영 서버에 붙을지 (친구들에게 줄 빌드는 켠다). 켰는데 주소가 비어 있으면 빌드가 실패한다")]
        [SerializeField] private bool _buildsUseProduction = true;

        public string LocalUrl => _localUrl;
        public string ProductionUrl => _productionUrl;
        public bool EditorUsesProduction => _editorUsesProduction;
        public bool BuildsUseProduction => _buildsUseProduction;
    }
}

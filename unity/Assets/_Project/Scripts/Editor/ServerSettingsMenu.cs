using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace DgVillage.Editor
{
    /// <summary>
    /// 메뉴 "DgVillage → Server Settings": ServerSettings 에셋을 Inspector 에 띄운다 (없으면 기본값으로 만든다).
    /// 빌드 직전 검사도 여기 있다: 빌드가 운영 서버를 쓰도록 되어 있는데 주소가 비었으면 빌드를 멈춘다.
    /// </summary>
    public class ServerSettingsMenu : IPreprocessBuildWithReport
    {
        private const string AssetPath = "Assets/_Project/Resources/ServerSettings.asset";

        public int callbackOrder => 0;

        [MenuItem("DgVillage/Server Settings")]
        private static void Open()
        {
            var settings = AssetDatabase.LoadAssetAtPath<ServerSettings>(AssetPath);
            if (settings == null)
            {
                if (!AssetDatabase.IsValidFolder("Assets/_Project/Resources"))
                {
                    AssetDatabase.CreateFolder("Assets/_Project", "Resources");
                }
                settings = ScriptableObject.CreateInstance<ServerSettings>();
                AssetDatabase.CreateAsset(settings, AssetPath);
                AssetDatabase.SaveAssets();
                Debug.Log($"{AssetPath} 생성", settings);
            }
            Selection.activeObject = settings;
            EditorGUIUtility.PingObject(settings);
        }

        public void OnPreprocessBuild(BuildReport report)
        {
            var settings = AssetDatabase.LoadAssetAtPath<ServerSettings>(AssetPath);
            if (settings == null)
            {
                throw new BuildFailedException($"{AssetPath} 가 없다. DgVillage → Server Settings 로 만들 것.");
            }
            if (settings.BuildsUseProduction && string.IsNullOrWhiteSpace(settings.ProductionUrl))
            {
                throw new BuildFailedException(
                    "ServerSettings: Builds Use Production 이 켜져 있는데 Production Url 이 비어 있다. " +
                    "주소를 넣거나, 로컬 테스트용 빌드면 Builds Use Production 을 끌 것.");
            }
            Debug.Log($"빌드 서버: {(settings.BuildsUseProduction ? settings.ProductionUrl : settings.LocalUrl)}");
        }
    }
}

using TMPro;
using UnityEditor;

namespace DgVillage.Editor
{
    /// <summary>
    /// Play 모드가 끝나면 _Project/Fonts 의 Dynamic 폰트 에셋(Galmuri11 SDF)을 비운다.
    /// Dynamic 폰트는 Play 중 화면에 나온 글자(한글 채팅 · 닉네임)를 아틀라스에 그려 넣는데,
    /// 에디터에서는 그 결과가 에셋 파일에 그대로 남아 git 에 1MB 짜리 변경이 생긴다.
    /// 글자는 다음에 필요할 때 다시 그려지므로 비워도 문제없다. 빌드는 Clear Dynamic Data On Build 가 맡는다.
    /// </summary>
    [InitializeOnLoad]
    public static class DynamicFontCleaner
    {
        private const string FontsFolder = "Assets/_Project/Fonts";

        static DynamicFontCleaner()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        private static void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.EnteredEditMode)
            {
                ClearAll();
            }
        }

        [MenuItem("DgVillage/Clear Dynamic Font Data")]
        private static void ClearAll()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:TMP_FontAsset", new[] { FontsFolder }))
            {
                var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(guid));
                if (font == null || font.atlasPopulationMode != AtlasPopulationMode.Dynamic)
                {
                    continue;
                }
                font.ClearFontAssetData(setAtlasSizeToZero: true);
                AssetDatabase.SaveAssetIfDirty(font);
            }
        }
    }
}

using System.Reflection;
using TMPro;
using UnityEditor;

// 동적 TMP 폰트(Galmuri 등)는 에디터에서 글자를 그릴 때마다 atlas 에 글자를 채워 넣고, 그 상태로 .asset 에 저장된다.
// TMP 는 빌드 직전·에디터 종료 시 이 데이터를 비우기 때문에(Clear Dynamic Data On Build), 저장 시점에 따라
// 폰트 파일이 "채워짐 ↔ 비워짐" 으로 계속 바뀌어 git diff 가 생긴다.
// 저장할 때도 TMP 와 같은 방식으로 비워서, 디스크(=git)에는 항상 비워진 상태만 남게 한다.
// 실행 중에는 TMP 가 필요한 글자를 다시 채우므로 화면 표시는 그대로다.
public class TmpDynamicFontCleaner : AssetModificationProcessor
{
    private static readonly MethodInfo ClearMethod = typeof(TMP_FontAsset).GetMethod(
        "ClearCharacterAndGlyphTablesInternal", BindingFlags.Instance | BindingFlags.NonPublic);

    private static string[] OnWillSaveAssets(string[] paths)
    {
        if (ClearMethod == null || EditorApplication.isPlayingOrWillChangePlaymode) return paths;

        foreach (string path in paths)
        {
            if (!path.EndsWith(".asset")) continue;

            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (font == null || !ShouldClear(font)) continue;

            ClearMethod.Invoke(font, null);
        }

        return paths;
    }

    // TMP_PreBuildProcessor 와 같은 조건.
    private static bool ShouldClear(TMP_FontAsset font)
    {
        bool dynamic = font.atlasPopulationMode == AtlasPopulationMode.Dynamic
                       || font.atlasPopulationMode == AtlasPopulationMode.DynamicOS;
        var so = new SerializedObject(font);
        bool clearOnBuild = so.FindProperty("m_ClearDynamicDataOnBuild").boolValue;
        return dynamic && clearOnBuild && font.atlasTexture != null && font.atlasTexture.width != 0;
    }
}

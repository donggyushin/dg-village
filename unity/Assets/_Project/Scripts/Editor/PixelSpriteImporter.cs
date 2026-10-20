using UnityEditor;
using UnityEngine;

namespace DgVillage.Editor
{
    /// <summary>
    /// Assets/_Project/Sprites 아래에 들어오는 이미지를 도트 스프라이트 설정으로 자동 import 한다.
    /// (Point 필터 · 압축 없음 · 밉맵 끔 · 발밑 기준 pivot)
    /// </summary>
    public class PixelSpriteImporter : AssetPostprocessor
    {
        private const string SpriteFolder = "Assets/_Project/Sprites/";
        private const float PixelsPerUnit = 32f;

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(SpriteFolder))
            {
                return;
            }

            var importer = (TextureImporter)assetImporter;

            // 최초 import 때만 기본값을 잡는다. 이후 Inspector 에서 바꾼 값(예: Multiple 로 시트 자르기)은 존중.
            if (!importer.importSettingsMissing)
            {
                return;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = PixelsPerUnit;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.BottomCenter;
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
        }
    }
}

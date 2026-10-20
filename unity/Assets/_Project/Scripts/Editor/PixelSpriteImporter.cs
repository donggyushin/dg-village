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

        // PixelLab 캐릭터 프레임은 64x64 캔버스에 발밑 아래 10px 여백이 있다 → pivot 을 발 위치로
        private const string CharacterFolder = "Assets/_Project/Sprites/Characters/";
        private static readonly Vector2 CharacterFootPivot = new Vector2(0.5f, 10f / 64f);

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
            if (assetPath.StartsWith(CharacterFolder))
            {
                settings.spriteAlignment = (int)SpriteAlignment.Custom;
                settings.spritePivot = CharacterFootPivot;
            }
            else
            {
                settings.spriteAlignment = (int)SpriteAlignment.BottomCenter;
            }
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
        }
    }
}

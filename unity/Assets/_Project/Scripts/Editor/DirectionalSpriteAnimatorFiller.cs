using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace DgVillage.Editor
{
    /// <summary>
    /// DirectionalSpriteAnimator 컴포넌트 우클릭(⋮) 메뉴 → "Fill From Character Folder".
    /// 캐릭터 폴더를 고르면 파일명 규칙에 따라 Idle/Walk 스프라이트를 자동으로 채운다.
    ///   {name}_idle_{south|north|east}.png
    ///   {name}_walk_{south|north|east}_{0..n}.png
    /// </summary>
    public static class DirectionalSpriteAnimatorFiller
    {
        private const string MenuPath = "CONTEXT/DirectionalSpriteAnimator/Fill From Character Folder";
        private const string DefaultFolder = "Assets/_Project/Sprites/Characters";

        [MenuItem(MenuPath)]
        private static void Fill(MenuCommand command)
        {
            var animator = (DirectionalSpriteAnimator)command.context;

            string absolute = EditorUtility.OpenFolderPanel("캐릭터 스프라이트 폴더 선택", DefaultFolder, "");
            if (string.IsNullOrEmpty(absolute))
            {
                return;
            }

            string folder = ToAssetPath(absolute);
            if (folder == null)
            {
                Debug.LogError($"프로젝트 Assets 폴더 안을 선택해야 한다: {absolute}");
                return;
            }

            var so = new SerializedObject(animator);
            FillDirection(so, "_down", folder, "south");
            FillDirection(so, "_up", folder, "north");
            FillDirection(so, "_right", folder, "east");
            so.ApplyModifiedProperties();

            // 에디터에서도 바로 정면 서기 모습이 보이도록
            var renderer = animator.GetComponent<SpriteRenderer>();
            Sprite idleDown = so.FindProperty("_down.Idle").objectReferenceValue as Sprite;
            if (renderer != null && idleDown != null)
            {
                Undo.RecordObject(renderer, "Set idle sprite");
                renderer.sprite = idleDown;
            }

            Debug.Log($"DirectionalSpriteAnimator 채우기 완료: {folder}", animator);
        }

        private static void FillDirection(SerializedObject so, string field, string folder, string direction)
        {
            Sprite[] sprites = AssetDatabase.FindAssets("t:Sprite", new[] { folder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Distinct()
                .Select(AssetDatabase.LoadAssetAtPath<Sprite>)
                .Where(s => s != null)
                .ToArray();

            Sprite idle = sprites.FirstOrDefault(s => s.name.EndsWith($"_idle_{direction}"));
            Sprite[] walk = sprites
                .Where(s => s.name.Contains($"_walk_{direction}_"))
                .OrderBy(s => FrameIndex(s.name))
                .ToArray();

            so.FindProperty($"{field}.Idle").objectReferenceValue = idle;

            SerializedProperty walkProp = so.FindProperty($"{field}.Walk");
            walkProp.arraySize = walk.Length;
            for (int i = 0; i < walk.Length; i++)
            {
                walkProp.GetArrayElementAtIndex(i).objectReferenceValue = walk[i];
            }

            if (idle == null || walk.Length == 0)
            {
                Debug.LogWarning($"{direction}: idle={(idle != null)} walk={walk.Length}장 — 파일명 규칙 확인 필요");
            }
        }

        private static int FrameIndex(string spriteName)
        {
            string last = spriteName.Substring(spriteName.LastIndexOf('_') + 1);
            return int.TryParse(last, out int index) ? index : 0;
        }

        private static string ToAssetPath(string absolute)
        {
            string dataPath = Path.GetFullPath(Application.dataPath).Replace('\\', '/');
            string full = Path.GetFullPath(absolute).Replace('\\', '/');
            if (!full.StartsWith(dataPath))
            {
                return null;
            }
            return "Assets" + full.Substring(dataPath.Length);
        }
    }
}

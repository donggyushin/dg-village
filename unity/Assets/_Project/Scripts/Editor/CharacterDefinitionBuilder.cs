using System.Linq;
using UnityEditor;
using UnityEngine;

namespace DgVillage.Editor
{
    /// <summary>
    /// Project 창에서 캐릭터 스프라이트 폴더 우클릭 → DgVillage → Create Character Definition.
    /// 폴더 이름을 id 로 CharacterDefinition 에셋을 만들고(이미 있으면 갱신) 파일명 규칙으로 스프라이트를 채운 뒤
    /// CharacterRegistry 에 등록한다.
    ///   {name}_idle_{south|north|east|southeast|northeast}.png   (대각선은 선택)
    ///   {name}_walk_{south|north|east|southeast|northeast}_{0..n}.png
    ///   {name}_sit_{south|east|north}.png   (선택 — 앉은 모습, 의자 방향별)
    /// </summary>
    public static class CharacterDefinitionBuilder
    {
        private const string MenuPath = "Assets/DgVillage/Create Character Definition";
        private const string CharactersFolder = "Assets/_Project/Characters";
        private const string RegistryPath = CharactersFolder + "/CharacterRegistry.asset";

        [MenuItem(MenuPath, true)]
        private static bool Validate()
        {
            return Selection.assetGUIDs
                .Select(AssetDatabase.GUIDToAssetPath)
                .Any(AssetDatabase.IsValidFolder);
        }

        [MenuItem(MenuPath)]
        private static void Build()
        {
            string[] folders = Selection.assetGUIDs
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(AssetDatabase.IsValidFolder)
                .ToArray();

            EnsureFolder(CharactersFolder);
            CharacterRegistry registry = LoadOrCreate<CharacterRegistry>(RegistryPath);

            foreach (string folder in folders)
            {
                string id = System.IO.Path.GetFileName(folder);
                CharacterDefinition character = LoadOrCreate<CharacterDefinition>($"{CharactersFolder}/{id}.asset");

                var so = new SerializedObject(character);
                so.FindProperty("_id").stringValue = id;
                FillDirection(so, "_down", folder, "south");
                FillDirection(so, "_up", folder, "north");
                FillDirection(so, "_right", folder, "east");
                FillDirection(so, "_downRight", folder, "southeast", optional: true);
                FillDirection(so, "_upRight", folder, "northeast", optional: true);
                so.FindProperty("_sitDown").objectReferenceValue = FindSprite(folder, "_sit_south");
                so.FindProperty("_sitRight").objectReferenceValue = FindSprite(folder, "_sit_east");
                so.FindProperty("_sitUp").objectReferenceValue = FindSprite(folder, "_sit_north");
                so.ApplyModifiedProperties();

                Register(registry, character);
                Debug.Log($"CharacterDefinition '{id}' 생성/갱신: {folder}", character);
            }

            AssetDatabase.SaveAssets();
            Selection.activeObject = registry;
        }

        private static Sprite FindSprite(string folder, string suffix) =>
            AssetDatabase.FindAssets("t:Sprite", new[] { folder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<Sprite>)
                .FirstOrDefault(s => s != null && s.name.EndsWith(suffix));

        private static void Register(CharacterRegistry registry, CharacterDefinition character)
        {
            var so = new SerializedObject(registry);
            SerializedProperty list = so.FindProperty("_characters");
            for (int i = 0; i < list.arraySize; i++)
            {
                if (list.GetArrayElementAtIndex(i).objectReferenceValue == character)
                {
                    return;
                }
            }
            list.arraySize++;
            list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = character;
            so.ApplyModifiedProperties();
        }

        private static void FillDirection(SerializedObject so, string field, string folder, string direction, bool optional = false)
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

            bool missing = idle == null || walk.Length == 0;
            bool absent = idle == null && walk.Length == 0;
            if (missing && !(optional && absent))
            {
                Debug.LogWarning($"{folder} {direction}: idle={(idle != null)} walk={walk.Length}장 — 파일명 규칙 확인 필요");
            }
        }

        private static int FrameIndex(string spriteName)
        {
            string last = spriteName.Substring(spriteName.LastIndexOf('_') + 1);
            return int.TryParse(last, out int index) ? index : 0;
        }

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<T>();
                AssetDatabase.CreateAsset(asset, path);
            }
            return asset;
        }

        private static void EnsureFolder(string path)
        {
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(System.IO.Path.GetDirectoryName(path), System.IO.Path.GetFileName(path));
            }
        }
    }
}

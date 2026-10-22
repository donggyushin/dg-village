using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace DgVillage.Editor
{
    /// <summary>
    /// 에디터에서 맵을 까는 데 필요한 것들:
    ///   - 서버와 같이 쓰는 맵 JSON(python/src/dg_village_server/maps/{id}.json) 읽기
    ///   - PropCatalog (런타임에 맵을 깔 재료 목록) 만들기 · 갱신 — 메뉴 "DgVillage → Update Prop Catalog"
    ///   - 열린 씬에 방 깔기 (= 런타임과 같은 MapBuilder, 미리보기 · MapTest 용)
    /// 방을 고칠 땐 JSON 을 고친다 (씬에 깔린 방은 미리보기일 뿐, 마을은 접속하면 서버가 보낸 맵으로 다시 깐다).
    /// </summary>
    internal static class OfficeRoomBuilder
    {
        private const string FloorTexturePath = "Assets/_Project/Sprites/Environment/Ground/office_floor.png";
        private const string FloorMaterialPath = "Assets/_Project/Materials/Floor_Office.mat";
        private const string WallTexturePath = "Assets/_Project/Sprites/Environment/Ground/office_wall.png";
        private const string WallMaterialPath = "Assets/_Project/Materials/Wall_Office.mat";
        private const string PropFolder = "Assets/_Project/Sprites/Environment/Props";
        private const string ShadowPath = PropFolder + "/blob_shadow.png";
        // 소품 · 캐릭터 공용 (DgVillage/Sprite = URP Sprite-Unlit-Default + 맵 조명)
        private const string SpriteMaterialPath = "Assets/_Project/Materials/Sprite_Lit.mat";
        internal const string CatalogPath = "Assets/_Project/Maps/PropCatalog.asset";
        // Application.dataPath(= unity/Assets) 기준
        private const string MapFolder = "../../python/src/dg_village_server/maps/";

        internal static readonly Color BackgroundColor = new(0.106f, 0.129f, 0.118f);

        // 가구가 바닥에서 차지하는 크기 (가로, 깊이) unit → Obstacle. 가로 ≈ 스프라이트에 그려진 폭, 깊이는 눈대중.
        // 여기 없는 소품은 막지 않는다 (벤치 등 아직 안 정한 것)
        private static readonly Dictionary<string, Vector2> Footprints = new()
        {
            ["desk_quill"] = new(1.3f, 0.6f), ["desk_spellbook"] = new(1.5f, 0.6f), ["desk_crystal"] = new(1.5f, 0.6f),
            ["chair_cushion"] = new(0.7f, 0.4f), ["chair_high"] = new(1.0f, 0.4f),
            ["bookshelf"] = new(1.1f, 0.4f), ["bookshelf_potion"] = new(1.1f, 0.4f), ["fireplace"] = new(1.4f, 0.4f),
            ["notice_board"] = new(1.1f, 0.3f), ["globe"] = new(0.8f, 0.5f),
            ["sofa"] = new(1.7f, 0.6f), ["armchair"] = new(1.2f, 0.6f), ["tea_table"] = new(1.0f, 0.6f),
            ["plant_pot"] = new(0.7f, 0.4f), ["candelabra"] = new(0.4f, 0.3f), ["owl_perch"] = new(0.4f, 0.3f),
            // 나무는 줄기만 막는다 (잎 아래로는 지나가도 자연스럽다)
            ["tree_round"] = new(0.6f, 0.3f), ["tree_blossom"] = new(0.5f, 0.3f), ["tree_round_b"] = new(0.6f, 0.3f),
            ["tree_twin"] = new(1.0f, 0.3f),
        };

        // 돌린 가구({id}_east 등, export_props.py)는 정면 크기에서 계산한다.
        // 옆(east) = 가로 · 깊이 맞바꿈, 뒤(north) = 그대로, 대각선 = 둘 다 걸치는 중간 크기
        private static Vector2 FootprintOf(string id)
        {
            if (Footprints.TryGetValue(id, out Vector2 footprint))
            {
                return footprint;
            }
            foreach ((string suffix, Func<Vector2, Vector2> rotate) in Rotations)
            {
                if (id.EndsWith(suffix) && Footprints.TryGetValue(id[..^suffix.Length], out Vector2 front))
                {
                    return rotate(front);
                }
            }
            return Vector2.zero;
        }

        private static readonly (string Suffix, Func<Vector2, Vector2> Rotate)[] Rotations =
        {
            ("_southeast", f => new Vector2((f.x + f.y) * 0.7f, (f.x + f.y) * 0.5f)),
            ("_northeast", f => new Vector2((f.x + f.y) * 0.7f, (f.x + f.y) * 0.5f)),
            ("_east", f => new Vector2(f.y, f.x)),
            ("_north", f => f),
        };

        internal static string MapPath(string mapId) =>
            Path.GetFullPath(Path.Combine(Application.dataPath, MapFolder, $"{mapId}.json"));

        /// <summary>맵 JSON 을 읽는다. 없으면 null.</summary>
        internal static GameMapData LoadMap(string mapId)
        {
            string path = MapPath(mapId);
            return File.Exists(path) ? JsonUtility.FromJson<GameMapData>(File.ReadAllText(path)) : null;
        }

        internal static bool AssetsReady(out string missing)
        {
            missing = AssetDatabase.LoadAssetAtPath<Texture2D>(FloorTexturePath) == null ? FloorTexturePath
                : AssetDatabase.LoadAssetAtPath<Texture2D>(WallTexturePath) == null ? WallTexturePath
                : null;
            return missing == null;
        }

        internal static Material SpriteMaterial => LoadOrCreateMaterial(SpriteMaterialPath, null, "DgVillage/Sprite");

        /// <summary>
        /// 열린 씬에 방(루트 "Map")을 만든다. (NewScene 다음에 불러야 한다 — 에셋을 여기서 불러오므로)
        /// </summary>
        internal static GameObject CreateRoom(GameMapData map) => MapBuilder.Build(map, UpdateCatalog());

        [MenuItem("DgVillage/Update Prop Catalog")]
        private static void UpdateCatalogMenu()
        {
            PropCatalog catalog = UpdateCatalog();
            Selection.activeObject = catalog;
            Debug.Log($"{CatalogPath} 갱신 완료.", catalog);
        }

        /// <summary>
        /// Props 폴더의 스프라이트 전부(그림자 제외) + 위 Footprints + 바닥 · 벽 · 스프라이트 머티리얼로
        /// PropCatalog 를 만들거나 갱신해서 돌려준다. 소품을 export_props.py 로 추가했으면 다시 부를 것.
        /// </summary>
        internal static PropCatalog UpdateCatalog()
        {
            var entries = new List<PropCatalog.Entry>();
            foreach (string guid in AssetDatabase.FindAssets("t:Sprite", new[] { PropFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path == ShadowPath)
                {
                    continue;
                }
                string id = Path.GetFileNameWithoutExtension(path);
                entries.Add(new PropCatalog.Entry
                {
                    Id = id,
                    Sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path),
                    Footprint = FootprintOf(id),
                });
            }
            entries.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));

            var catalog = AssetDatabase.LoadAssetAtPath<PropCatalog>(CatalogPath);
            if (catalog == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(CatalogPath));
                catalog = ScriptableObject.CreateInstance<PropCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }
            catalog.Set(
                LoadOrCreateMaterial(FloorMaterialPath, FloorTexturePath, "DgVillage/Ground"),
                WallMaterial(),
                SpriteMaterial,
                AssetDatabase.LoadAssetAtPath<Sprite>(ShadowPath),
                entries);
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssetIfDirty(catalog);
            return catalog;
        }

        // 벽도 맵 조명을 받는다 (촛불 옆 벽이 밝게). 세운 벽면에 바닥 구름은 줄무늬가 되므로 끈다
        private static Material WallMaterial()
        {
            Material material = LoadOrCreateMaterial(WallMaterialPath, WallTexturePath, "DgVillage/Ground");
            if (material.GetFloat("_Clouds") != 0f)
            {
                material.SetFloat("_Clouds", 0f);
                EditorUtility.SetDirty(material);
            }
            return material;
        }

        private static Material LoadOrCreateMaterial(string materialPath, string texturePath, string shaderName)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            Shader shader = Shader.Find(shaderName);
            if (material != null)
            {
                // 예전에 URP Unlit 으로 만든 바닥 · 벽도 Ground 로 바꾼다 (_BaseMap 이름이 같아 텍스처는 그대로)
                if (material.shader != shader)
                {
                    material.shader = shader;
                    EditorUtility.SetDirty(material);
                }
                return material;
            }
            // 전부 Unlit 계열: URP 조명 없이 텍스처 색 그대로 × 맵 조명(MapLighting). 스프라이트는 텍스처를 SpriteRenderer 가 준다
            material = new Material(shader);
            if (texturePath != null)
            {
                material.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            }
            AssetDatabase.CreateAsset(material, materialPath);
            return material;
        }
    }
}

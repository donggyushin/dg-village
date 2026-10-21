using System;
using System.Collections.Generic;
using UnityEngine;

namespace DgVillage
{
    /// <summary>
    /// 실내 바닥 메시. 1 타일 = 1 unit = 32 texel. GroundMesh(야외, 꼭짓점 Wang)와 달리 타일 하나가 재질 하나다.
    /// Layout 한 글자 = 타일 하나: 's' 돌(복도) · 'w' 나무(자리) · 'c' 카펫(휴게실), 그 외 = 바닥 없음.
    /// 아틀라스(art/tilesets/make_floor_atlas.py): 행 = 재질(Materials 순서), 열 = 테두리 마스크
    /// N·1 + E·2 + S·4 + W·8 (그 방향 이웃이 다른 재질이거나 비어 있으면 그쪽에 몰딩을 그린 타일).
    /// 메시는 저장하지 않고 켜질 때마다 만든다 (Layout 을 고치면 바로 다시 그린다).
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class FloorMesh : MonoBehaviour
    {
        private const string Materials = "swc";
        private const int AtlasColumns = 16;
        // 칸 경계에서 이웃 칸 픽셀을 집어오지 않도록 UV 를 아주 조금 안쪽으로
        private const float UvInset = 0.0002f;

        [Tooltip("타일 격자. 첫 줄이 북쪽(화면 위). s = 돌, w = 나무, c = 카펫, 그 외 = 바닥 없음")]
        [SerializeField, TextArea(5, 30)] private string _layout = "ss\nss";

        private Mesh _mesh;
        private bool _dirty = true;

        private void OnEnable() => _dirty = true;

        private void OnValidate() => _dirty = true;

        // OnValidate 안에서는 MeshFilter 를 바꿀 수 없어서 다음 Update 에서 다시 만든다
        private void Update()
        {
            if (_dirty)
            {
                Rebuild();
            }
        }

        private void OnDestroy()
        {
            if (_mesh != null)
            {
                DestroyImmediate(_mesh);
            }
        }

        private void Rebuild()
        {
            _dirty = false;
            string[] rows = _layout.Replace("\r", "").Split('\n', StringSplitOptions.RemoveEmptyEntries);
            if (rows.Length == 0)
            {
                return;
            }
            int width = 0;
            foreach (string row in rows)
            {
                width = Mathf.Max(width, row.Length);
            }
            int height = rows.Length;
            // 가운데가 원점이 되도록
            float left = -width / 2f;
            float top = height / 2f;
            int atlasRows = Materials.Length;

            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();
            for (int ty = 0; ty < height; ty++)
            {
                for (int tx = 0; tx < width; tx++)
                {
                    char c = At(rows, tx, ty);
                    int material = Materials.IndexOf(c);
                    if (material < 0)
                    {
                        continue;
                    }
                    int mask = (At(rows, tx, ty - 1) != c ? 1 : 0)
                        | (At(rows, tx + 1, ty) != c ? 2 : 0)
                        | (At(rows, tx, ty + 1) != c ? 4 : 0)
                        | (At(rows, tx - 1, ty) != c ? 8 : 0);

                    int v = vertices.Count;
                    float x = left + tx;
                    float z = top - ty;
                    vertices.Add(new Vector3(x, 0f, z - 1f));      // SW
                    vertices.Add(new Vector3(x, 0f, z));           // NW
                    vertices.Add(new Vector3(x + 1f, 0f, z));      // NE
                    vertices.Add(new Vector3(x + 1f, 0f, z - 1f)); // SE

                    // 아틀라스 이미지는 위에서부터 행을 세지만 UV 의 v 는 아래가 0
                    float u0 = (float)mask / AtlasColumns + UvInset;
                    float u1 = (float)(mask + 1) / AtlasColumns - UvInset;
                    float v1 = 1f - (float)material / atlasRows - UvInset;
                    float v0 = 1f - (float)(material + 1) / atlasRows + UvInset;
                    uvs.Add(new Vector2(u0, v0));
                    uvs.Add(new Vector2(u0, v1));
                    uvs.Add(new Vector2(u1, v1));
                    uvs.Add(new Vector2(u1, v0));

                    triangles.AddRange(new[] { v, v + 1, v + 2, v, v + 2, v + 3 });
                }
            }

            if (_mesh == null)
            {
                _mesh = new Mesh { name = "FloorMesh", hideFlags = HideFlags.DontSave };
            }
            _mesh.Clear();
            _mesh.SetVertices(vertices);
            _mesh.SetUVs(0, uvs);
            _mesh.SetTriangles(triangles, 0);
            _mesh.RecalculateNormals();
            _mesh.RecalculateBounds();
            GetComponent<MeshFilter>().sharedMesh = _mesh;
        }

        // 격자 밖이나 짧은 줄의 빈 칸은 ' ' (= 바닥 없음)
        private static char At(string[] rows, int x, int y) =>
            y >= 0 && y < rows.Length && x >= 0 && x < rows[y].Length ? rows[y][x] : ' ';
    }
}

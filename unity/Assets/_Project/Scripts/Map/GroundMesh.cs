using System;
using UnityEngine;

namespace DgVillage
{
    /// <summary>
    /// 바닥 메시를 Wang 타일로 만든다. 1 타일 = 1 unit = 32 texel (캐릭터와 같은 픽셀 밀도).
    /// Layout 은 타일이 아니라 "꼭짓점" 격자다: 한 글자 = 꼭짓점 하나, '#' = 돌길, 그 외 = 잔디.
    /// 타일은 네 꼭짓점의 조합(16가지)으로 아틀라스 칸을 고른다 — 경계 모양은 아틀라스가 알아서 그린다.
    /// 아틀라스 칸 번호 = NE·1 + SE·2 + SW·4 + NW·8 (art/tilesets/make_ground_atlas.py 와 같은 규칙).
    /// 메시는 저장하지 않고 켜질 때마다 만든다 (Layout 을 고치면 바로 다시 그린다).
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class GroundMesh : MonoBehaviour
    {
        private const int AtlasColumns = 4;
        private const int AtlasRows = 4;
        // 칸 경계에서 이웃 칸 픽셀을 집어오지 않도록 UV 를 아주 조금 안쪽으로
        private const float UvInset = 0.0005f;

        [Tooltip("꼭짓점 격자. 줄 수 = 세로 타일 + 1, 글자 수 = 가로 타일 + 1. 첫 줄이 북쪽(화면 위)")]
        [SerializeField, TextArea(5, 30)] private string _layout = "##\n##";

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
            if (rows.Length < 2 || rows[0].Length < 2)
            {
                Debug.LogError("GroundMesh: Layout 은 최소 2줄 × 2글자 (타일 1개).", this);
                return;
            }
            int width = rows[0].Length - 1;
            int height = rows.Length - 1;
            foreach (string row in rows)
            {
                if (row.Length != width + 1)
                {
                    Debug.LogError("GroundMesh: Layout 의 줄 길이가 모두 같아야 한다.", this);
                    return;
                }
            }

            var vertices = new Vector3[width * height * 4];
            var uvs = new Vector2[vertices.Length];
            var triangles = new int[width * height * 6];
            // 가운데가 원점이 되도록
            float left = -width / 2f;
            float top = height / 2f;

            for (int ty = 0; ty < height; ty++)
            {
                for (int tx = 0; tx < width; tx++)
                {
                    int index = (IsStone(rows, tx + 1, ty) ? 1 : 0)
                        | (IsStone(rows, tx + 1, ty + 1) ? 2 : 0)
                        | (IsStone(rows, tx, ty + 1) ? 4 : 0)
                        | (IsStone(rows, tx, ty) ? 8 : 0);

                    int v = (ty * width + tx) * 4;
                    float x = left + tx;
                    float z = top - ty;
                    vertices[v] = new Vector3(x, 0f, z - 1f);     // SW
                    vertices[v + 1] = new Vector3(x, 0f, z);      // NW
                    vertices[v + 2] = new Vector3(x + 1f, 0f, z); // NE
                    vertices[v + 3] = new Vector3(x + 1f, 0f, z - 1f); // SE

                    // 아틀라스 이미지는 위에서부터 칸을 세지만 UV 의 v 는 아래가 0
                    float u0 = (float)(index % AtlasColumns) / AtlasColumns + UvInset;
                    float u1 = (float)(index % AtlasColumns + 1) / AtlasColumns - UvInset;
                    float v1 = 1f - (float)(index / AtlasColumns) / AtlasRows - UvInset;
                    float v0 = 1f - (float)(index / AtlasColumns + 1) / AtlasRows + UvInset;
                    uvs[v] = new Vector2(u0, v0);
                    uvs[v + 1] = new Vector2(u0, v1);
                    uvs[v + 2] = new Vector2(u1, v1);
                    uvs[v + 3] = new Vector2(u1, v0);

                    int t = (ty * width + tx) * 6;
                    triangles[t] = v;
                    triangles[t + 1] = v + 1;
                    triangles[t + 2] = v + 2;
                    triangles[t + 3] = v;
                    triangles[t + 4] = v + 2;
                    triangles[t + 5] = v + 3;
                }
            }

            if (_mesh == null)
            {
                _mesh = new Mesh { name = "GroundMesh", hideFlags = HideFlags.DontSave };
            }
            _mesh.Clear();
            _mesh.vertices = vertices;
            _mesh.uv = uvs;
            _mesh.triangles = triangles;
            _mesh.RecalculateNormals();
            _mesh.RecalculateBounds();
            GetComponent<MeshFilter>().sharedMesh = _mesh;
        }

        private static bool IsStone(string[] rows, int x, int y) => rows[y][x] == '#';
    }
}

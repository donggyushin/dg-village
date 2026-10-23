using System.Collections.Generic;
using UnityEngine;

namespace DgVillage
{
    /// <summary>
    /// 야외 건물 하나 (맵 JSON buildings): 돌벽 상자 + 박공 지붕 메시. 1 unit = 32 texel, 벽 높이 4 unit.
    /// 루트 위치 = 앞(남쪽) 벽의 가운데 바닥, 건물은 거기서 뒤(+Z)로 깊이만큼 (Obstacle 기본 모양과 같다).
    /// 세로선은 FloorMesh 벽처럼 카메라 위 방향(ViewAngle.Billboard)으로 세운다 → 화면에서 똑바르고 텍셀도 1:1,
    /// 진짜 메시라 캐릭터가 벽 앞 · 뒤를 지나가도 가림이 맞다 (큰 스프라이트 한 장은 이게 틀린다).
    /// 아틀라스(art/tilesets/make_building_atlas.py, 32px 열 8개): 0 벽 · 1–2 창문 · 3–4 문 · 5 지붕 · 6 박공.
    /// 앞벽 = 문(_door) + 양옆으로 한 칸 띄우고 창문, 옆벽 = 창문. submesh 0 = 햇빛 쪽(서 · 북 · 지붕), 1 = 그늘 쪽(남 · 동).
    /// 메시는 저장하지 않고 켜질 때마다 만든다.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class BuildingMesh : MonoBehaviour
    {
        private const float WallHeight = 4f;
        private const float Columns = 8f;
        private const int WallColumn = 0;
        private const int WindowColumn = 1;
        private const int DoorColumn = 3;
        private const int RoofColumn = 5;
        private const int GableColumn = 6;
        private const float Overhang = 0.3f;
        private const float UvInset = 0.0002f;
        // 지붕 칸(32px) 세 종류의 v 범위 (아틀라스 위에서부터 0 · 1 · 2 번째 32px 줄)
        private const float RoofTileV = 0.75f;
        private const float RoofEaveV = 0.5f;
        private const float RoofRidgeV = 0.25f;

        [Tooltip("가로(X) × 깊이(Z) unit. 가로는 정수")]
        [SerializeField] private Vector2 _size = new(8f, 5f);
        [Tooltip("지붕 높이 (벽 위로) unit")]
        [SerializeField] private float _roof = 2.5f;
        [Tooltip("문 가운데의 X (건물 가운데 기준). 문 폭 = 2 칸")]
        [SerializeField] private float _door;

        private Mesh _mesh;
        private bool _dirty = true;

        public Vector2 Size => _size;

        /// <summary>런타임에 만들 때 (MapBuilder)</summary>
        public void Init(GameMapData.Building building)
        {
            _size = new Vector2(building.w, building.d);
            _roof = building.roof > 0f ? building.roof : building.d / 2f;
            _door = building.door;
            _dirty = true;
        }

        private void OnEnable() => _dirty = true;

        private void OnValidate() => _dirty = true;

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
            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var lit = new List<int>();
            var shade = new List<int>();

            int width = Mathf.Max(1, Mathf.RoundToInt(_size.x));
            float depth = Mathf.Max(1f, _size.y);
            float x0 = -width / 2f;
            float x1 = width / 2f;
            float z1 = depth;
            float ridgeZ = depth / 2f;
            float ridgeH = WallHeight + Mathf.Max(0.5f, _roof);
            Vector3 up = ViewAngle.Billboard * Vector3.up;
            Vector3 P(float x, float z, float h) => new Vector3(x, 0f, z) + up * h;

            // 보는 쪽에서 왼아래 · 왼위 · 오른위 · 오른아래 (시계 방향 = 앞면)
            void Quad(List<int> triangles, Vector3 bl, Vector3 tl, Vector3 tr, Vector3 br, Vector2 uvBl, Vector2 uvTl, Vector2 uvTr, Vector2 uvBr)
            {
                int v = vertices.Count;
                vertices.Add(bl);
                vertices.Add(tl);
                vertices.Add(tr);
                vertices.Add(br);
                uvs.Add(uvBl);
                uvs.Add(uvTl);
                uvs.Add(uvTr);
                uvs.Add(uvBr);
                triangles.AddRange(new[] { v, v + 1, v + 2, v, v + 2, v + 3 });
            }

            float U(int column, float t) => (column + Mathf.Lerp(UvInset, 1f - UvInset, t)) / Columns;
            float V(float t) => Mathf.Lerp(UvInset, 1f - UvInset, t);

            // 벽 한 줄: start → end (보는 쪽에서 왼쪽 → 오른쪽) 를 1 unit 칸으로, 칸마다 아틀라스 열 하나
            void Wall(List<int> triangles, Vector2 start, Vector2 end, int[] columns)
            {
                for (int i = 0; i < columns.Length; i++)
                {
                    Vector2 a = Vector2.Lerp(start, end, (float)i / columns.Length);
                    Vector2 b = Vector2.Lerp(start, end, (float)(i + 1) / columns.Length);
                    int c = columns[i];
                    Quad(triangles, P(a.x, a.y, 0f), P(a.x, a.y, WallHeight), P(b.x, b.y, WallHeight), P(b.x, b.y, 0f),
                        new Vector2(U(c, 0f), V(0f)), new Vector2(U(c, 0f), V(1f)), new Vector2(U(c, 1f), V(1f)), new Vector2(U(c, 1f), V(0f)));
                }
            }

            // 박공(옆벽 위 삼각형): start → end 를 1 unit 띠로, 띠 윗변은 지붕선을 따라간다
            void Gable(List<int> triangles, Vector2 start, Vector2 end)
            {
                float length = Vector2.Distance(start, end);
                int strips = Mathf.CeilToInt(length - 0.001f);
                float Height(float z) => WallHeight + (ridgeH - WallHeight) * (1f - Mathf.Abs(z - ridgeZ) / ridgeZ);
                for (int i = 0; i < strips; i++)
                {
                    float ta = i / length;
                    float tb = Mathf.Min(i + 1, length) / length;
                    Vector2 a = Vector2.Lerp(start, end, ta);
                    Vector2 b = Vector2.Lerp(start, end, tb);
                    float ha = Height(a.y) - WallHeight;
                    float hb = Height(b.y) - WallHeight;
                    float ub = tb * length - i;
                    Quad(triangles, P(a.x, a.y, WallHeight), P(a.x, a.y, WallHeight + ha), P(b.x, b.y, WallHeight + hb), P(b.x, b.y, WallHeight),
                        new Vector2(U(GableColumn, 0f), V(0f)), new Vector2(U(GableColumn, 0f), V(ha / WallHeight)),
                        new Vector2(U(GableColumn, ub), V(hb / WallHeight)), new Vector2(U(GableColumn, ub), V(0f)));
                }
            }

            // 지붕 한 쪽 면: 처마(eave) → 용마루(ridge). 가로 1 unit 칸 × 경사 1 줄씩, 첫 줄 = 처마 그림, 마지막 줄 = 용마루 그림
            void RoofSide(List<int> triangles, float eaveZ, float sign)
            {
                float run = Mathf.Abs(ridgeZ - eaveZ);
                float eaveH = WallHeight - Overhang * (ridgeH - WallHeight) / ridgeZ;
                int rows = Mathf.CeilToInt(run - 0.001f);
                float left = sign > 0f ? x0 - Overhang : x1 + Overhang;
                float right = sign > 0f ? x1 + Overhang : x0 - Overhang;
                float span = Mathf.Abs(right - left);
                int cells = Mathf.CeilToInt(span - 0.001f);
                for (int r = 0; r < rows; r++)
                {
                    float sa = r;
                    float sb = Mathf.Min(r + 1, run);
                    Vector2 BandPoint(float s, float x) => new(x, eaveZ + (ridgeZ - eaveZ) * s / run);
                    float Hs(float s) => eaveH + (ridgeH - eaveH) * s / run;
                    float vTop = r == rows - 1 ? RoofRidgeV + 0.25f : r == 0 ? RoofEaveV + 0.25f : RoofTileV + 0.25f;
                    // 용마루 줄이 짧으면 그림 윗부분(용마루)만 쓴다. 처마 줄은 그림 아랫부분(처마 판)이 처마에 오도록
                    float rowLength = sb - sa;
                    float vBottom = vTop - 0.25f * rowLength;
                    if (r == 0 && rows > 1)
                    {
                        vBottom = vTop - 0.25f;
                        vTop = vBottom + 0.25f * rowLength;
                    }
                    // 32px 줄 경계에서 옆 줄 픽셀을 집어오지 않도록
                    vTop -= 0.001f;
                    vBottom += 0.001f;
                    for (int c = 0; c < cells; c++)
                    {
                        float xa = Mathf.Lerp(left, right, c / span);
                        float xb = Mathf.Lerp(left, right, Mathf.Min(c + 1, span) / span);
                        float ub = Mathf.Min(c + 1, span) - c;
                        Vector2 bl = BandPoint(sa, xa), tl = BandPoint(sb, xa), tr = BandPoint(sb, xb), br = BandPoint(sa, xb);
                        Quad(triangles, P(bl.x, bl.y, Hs(sa)), P(tl.x, tl.y, Hs(sb)), P(tr.x, tr.y, Hs(sb)), P(br.x, br.y, Hs(sa)),
                            new Vector2(U(RoofColumn, 0f), vBottom), new Vector2(U(RoofColumn, 0f), vTop),
                            new Vector2(U(RoofColumn, ub), vTop), new Vector2(U(RoofColumn, ub), vBottom));
                    }
                }
            }

            int depthColumns = Mathf.Max(1, Mathf.RoundToInt(depth));
            Wall(shade, new Vector2(x0, 0f), new Vector2(x1, 0f), FrontColumns(width, _door - x0));
            Wall(lit, new Vector2(x0, z1), new Vector2(x0, 0f), SideColumns(depthColumns));
            Wall(shade, new Vector2(x1, 0f), new Vector2(x1, z1), SideColumns(depthColumns));
            Wall(lit, new Vector2(x1, z1), new Vector2(x0, z1), SideColumns(width));
            Gable(lit, new Vector2(x0, z1), new Vector2(x0, 0f));
            Gable(shade, new Vector2(x1, 0f), new Vector2(x1, z1));
            RoofSide(lit, -Overhang, 1f);
            RoofSide(lit, z1 + Overhang, -1f);

            if (_mesh == null)
            {
                _mesh = new Mesh { name = "BuildingMesh", hideFlags = HideFlags.DontSave };
            }
            _mesh.Clear();
            _mesh.SetVertices(vertices);
            _mesh.SetUVs(0, uvs);
            _mesh.subMeshCount = 2;
            _mesh.SetTriangles(lit, 0);
            _mesh.SetTriangles(shade, 1);
            _mesh.RecalculateNormals();
            _mesh.RecalculateBounds();
            GetComponent<MeshFilter>().sharedMesh = _mesh;
        }

        // 앞벽: 문 두 칸, 그 양옆으로 벽 한 칸 띄우고 창문 두 칸씩 반복 (모서리 칸은 벽)
        private static int[] FrontColumns(int width, float doorFromLeft)
        {
            var columns = new int[width];
            int door = Mathf.RoundToInt(doorFromLeft) - 1; // 문 왼쪽 칸
            if (door < 0 || door + 1 >= width)
            {
                return SideColumns(width);
            }
            columns[door] = DoorColumn;
            columns[door + 1] = DoorColumn + 1;
            for (int c = door + 3; c + 1 <= width - 2; c += 3)
            {
                columns[c] = WindowColumn;
                columns[c + 1] = WindowColumn + 1;
            }
            for (int c = door - 3; c >= 1; c -= 3)
            {
                columns[c] = WindowColumn;
                columns[c + 1] = WindowColumn + 1;
            }
            return columns;
        }

        // 옆 · 뒷벽: 모서리에서 한 칸 띄우고 창문 두 칸, 벽 한 칸 … (모서리 칸은 벽)
        private static int[] SideColumns(int length)
        {
            var columns = new int[length];
            for (int c = 1; c + 1 <= length - 2; c += 3)
            {
                columns[c] = WindowColumn;
                columns[c + 1] = WindowColumn + 1;
            }
            return columns;
        }
    }
}

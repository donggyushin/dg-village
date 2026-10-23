using System;
using System.Collections.Generic;
using UnityEngine;

namespace DgVillage
{
    /// <summary>
    /// 바닥 + 벽 메시. 1 타일 = 1 unit = 32 texel. GroundMesh(꼭짓점 Wang)와 달리 타일 하나가 재질 하나다.
    /// Layout 한 글자 = 타일 하나: 's' 돌(복도) · 'w' 나무(자리) · 'c' 카펫(휴게실) · 'g' 잔디 · 'p' 정원 길, 그 외 = 바닥 없음.
    /// 바닥 아틀라스(art/tilesets/make_floor_atlas.py): 행 = 재질(Materials 순서), 열 = 테두리 마스크
    /// N·1 + E·2 + S·4 + W·8 (그 방향 이웃이 다른 재질이거나 비어 있으면 그쪽에 몰딩을 그린 타일).
    /// 벽은 바닥 끝(이웃이 비어 있는 변)에 자동으로 생긴다 — submesh 1, 두 번째 머티리얼(벽 아틀라스):
    ///   북쪽 변 = 4 unit 높이 벽면. 캐릭터 스프라이트처럼 35° 뒤로 눕혀 카메라를 정면으로 본다
    ///             → 화면에선 똑바로 선 벽이고 텍셀도 1:1 (수직으로 세우면 세로가 찌그러진다)
    ///   나머지 변 = 바닥 밖으로 0.5 unit 폭의 벽 윗면 띠 (위에서 본 벽 두께)
    /// 바닥이 있는 칸만 걸을 수 있다: PlayerMovement 가 Current.IsWalkable 로 확인한다.
    /// 메시는 저장하지 않고 켜질 때마다 만든다 (Layout 을 고치면 바로 다시 그린다).
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class FloorMesh : MonoBehaviour
    {
        private const string Materials = "swcgp";
        public static int MaterialCount => Materials.Length;
        private const int AtlasColumns = 16;
        // 칸 경계에서 이웃 칸 픽셀을 집어오지 않도록 UV 를 아주 조금 안쪽으로
        private const float UvInset = 0.0002f;

        // 벽 아틀라스 64×128: 왼쪽 절반 = 벽면 (128px = 4 unit), 오른쪽 절반 위 16px = 윗면
        private const float WallHeight = 4f;
        private const float CapWidth = 0.5f;
        private const float CapV = 1f - 16f / 128f;

        // 쿼터뷰 바닥 아틀라스(make_iso_floor.py): 재질마다 화면 픽셀 기준 256×128 반복 패턴. 1 unit = 32 화면 픽셀
        private const float TexelsPerUnit = 32f;
        private const float IsoCellWidth = 256f;
        private const float IsoCellHeight = 128f;

        [Tooltip("타일 격자. 첫 줄이 북쪽(화면 위). s = 돌, w = 나무, c = 카펫, g = 잔디, p = 길, 그 외 = 바닥 없음")]
        [SerializeField, TextArea(5, 30)] private string _layout = "ss\nss";

        /// <summary>씬에 켜져 있는 바닥 (없으면 null = 어디든 걸을 수 있음)</summary>
        public static FloorMesh Current { get; private set; }

        private string[] _rows = Array.Empty<string>();
        private float _left;
        private float _top;
        private Mesh _mesh;
        private bool _dirty = true;

        private void OnEnable()
        {
            Current = this;
            Parse();
        }

        private void OnDisable()
        {
            if (Current == this)
            {
                Current = null;
            }
        }

        private void OnValidate() => Parse();

        /// <summary>런타임에 만들 때 (MapBuilder). 메시는 다음 Update 에서 다시 만든다</summary>
        public void SetLayout(string layout)
        {
            _layout = layout;
            Parse();
        }

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

        /// <summary>
        /// 발 위치(월드)를 중심으로 반지름 radius 의 네 모서리가 모두 바닥 위면 true.
        /// </summary>
        public bool IsWalkable(Vector3 position, float radius)
        {
            Vector3 local = position - transform.position;
            return IsFloor(local.x - radius, local.z - radius) && IsFloor(local.x + radius, local.z - radius)
                && IsFloor(local.x - radius, local.z + radius) && IsFloor(local.x + radius, local.z + radius);
        }

        private bool IsFloor(float x, float z) =>
            Materials.IndexOf(At(_rows, Mathf.FloorToInt(x - _left), Mathf.FloorToInt(_top - z))) >= 0;

        private void Parse()
        {
            _rows = _layout.Replace("\r", "").Split('\n', StringSplitOptions.RemoveEmptyEntries);
            int width = 0;
            foreach (string row in _rows)
            {
                width = Mathf.Max(width, row.Length);
            }
            // 가운데가 원점이 되도록
            _left = -width / 2f;
            _top = _rows.Length / 2f;
            _dirty = true;
        }

        private void Rebuild()
        {
            _dirty = false;
            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var rows = new List<Vector2>(); // uv1.x = 쿼터뷰 아틀라스 행
            var floorTriangles = new List<int>();
            var wallTriangles = new List<int>();
            int atlasRows = Materials.Length;
            // 벽면은 스프라이트처럼 카메라 쪽 위 방향으로 세운다. 쿼터뷰면 뒤쪽 두 변(북 · 동)이 벽면
            Vector3 wallUp = ViewAngle.Billboard * Vector3.up;
            bool quarterView = ViewAngle.IsQuarterView;

            // 꼭짓점 네 개를 SW · NW · NE · SE (보는 쪽에서 왼아래부터 시계 방향) 순서로 받는다
            void AddQuad(List<int> triangles, Vector3 sw, Vector3 nw, Vector3 ne, Vector3 se, Vector2 uvMin, Vector2 uvMax, bool rotateUv = false)
            {
                int v = vertices.Count;
                vertices.Add(sw);
                vertices.Add(nw);
                vertices.Add(ne);
                vertices.Add(se);
                if (rotateUv)
                {
                    // 동·서쪽 띠: 텍스처의 가로가 띠의 길이(남북) 방향으로 가도록 90° 돌린다
                    uvs.Add(new Vector2(uvMin.x, uvMax.y));
                    uvs.Add(new Vector2(uvMax.x, uvMax.y));
                    uvs.Add(new Vector2(uvMax.x, uvMin.y));
                    uvs.Add(new Vector2(uvMin.x, uvMin.y));
                }
                else
                {
                    uvs.Add(new Vector2(uvMin.x, uvMin.y));
                    uvs.Add(new Vector2(uvMin.x, uvMax.y));
                    uvs.Add(new Vector2(uvMax.x, uvMax.y));
                    uvs.Add(new Vector2(uvMax.x, uvMin.y));
                }
                triangles.AddRange(new[] { v, v + 1, v + 2, v, v + 2, v + 3 });
                rows.AddRange(new[] { Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero });
            }

            // 쿼터뷰: 꼭짓점이 화면에 찍히는 픽셀 위치 / 셀 크기 = UV. 바닥 위 점이 카메라 앞쪽으로 d 가면 화면에선 d·sin(pitch) 위로
            Quaternion yaw = Quaternion.Euler(0f, ViewAngle.Yaw, 0f);
            Vector3 screenRight = yaw * Vector3.right;
            Vector3 screenUp = yaw * Vector3.forward * Mathf.Sin(ViewAngle.Pitch * Mathf.Deg2Rad);
            void UseScreenUv(int material)
            {
                for (int i = vertices.Count - 4; i < vertices.Count; i++)
                {
                    Vector3 p = vertices[i];
                    uvs[i] = new Vector2(Vector3.Dot(p, screenRight) * TexelsPerUnit / IsoCellWidth,
                        Vector3.Dot(p, screenUp) * TexelsPerUnit / IsoCellHeight);
                    rows[i] = new Vector2(material, 0f);
                }
            }

            var faceMin = new Vector2(UvInset, UvInset);
            var faceMax = new Vector2(0.5f - UvInset, 1f - UvInset);
            var capMin = new Vector2(0.5f + UvInset, CapV + UvInset);
            var capMax = new Vector2(1f - UvInset, 1f - UvInset);
            const float capY = 0.002f; // 바닥과 겹치는 곳에서 깜빡이지 않도록 살짝 위

            for (int ty = 0; ty < _rows.Length; ty++)
            {
                for (int tx = 0; tx < _rows[ty].Length; tx++)
                {
                    char c = At(_rows, tx, ty);
                    int material = Materials.IndexOf(c);
                    if (material < 0)
                    {
                        continue;
                    }
                    int mask = (At(_rows, tx, ty - 1) != c ? 1 : 0)
                        | (At(_rows, tx + 1, ty) != c ? 2 : 0)
                        | (At(_rows, tx, ty + 1) != c ? 4 : 0)
                        | (At(_rows, tx - 1, ty) != c ? 8 : 0);
                    float x = _left + tx;
                    float z = _top - ty;

                    // 아틀라스 이미지는 위에서부터 행을 세지만 UV 의 v 는 아래가 0
                    AddQuad(floorTriangles,
                        new Vector3(x, 0f, z - 1f), new Vector3(x, 0f, z), new Vector3(x + 1f, 0f, z), new Vector3(x + 1f, 0f, z - 1f),
                        new Vector2((float)mask / AtlasColumns + UvInset, 1f - (float)(material + 1) / atlasRows + UvInset),
                        new Vector2((float)(mask + 1) / AtlasColumns - UvInset, 1f - (float)material / atlasRows - UvInset));
                    if (quarterView)
                    {
                        UseScreenUv(material);
                    }

                    bool openN = !IsFloorTile(tx, ty - 1);
                    bool openE = !IsFloorTile(tx + 1, ty);
                    bool openS = !IsFloorTile(tx, ty + 1);
                    bool openW = !IsFloorTile(tx - 1, ty);
                    if (openN)
                    {
                        var baseL = new Vector3(x, 0f, z);
                        var baseR = new Vector3(x + 1f, 0f, z);
                        AddQuad(wallTriangles, baseL, baseL + wallUp * WallHeight, baseR + wallUp * WallHeight, baseR, faceMin, faceMax);
                    }
                    if (openE && quarterView)
                    {
                        // 서쪽(-X)에서 보므로 왼쪽 = 북쪽 끝
                        var baseL = new Vector3(x + 1f, 0f, z);
                        var baseR = new Vector3(x + 1f, 0f, z - 1f);
                        AddQuad(wallTriangles, baseL, baseL + wallUp * WallHeight, baseR + wallUp * WallHeight, baseR, faceMin, faceMax);
                    }
                    if (openS)
                    {
                        AddQuad(wallTriangles,
                            new Vector3(x, capY, z - 1f - CapWidth), new Vector3(x, capY, z - 1f),
                            new Vector3(x + 1f, capY, z - 1f), new Vector3(x + 1f, capY, z - 1f - CapWidth), capMin, capMax);
                    }
                    if (openE && !quarterView)
                    {
                        AddQuad(wallTriangles,
                            new Vector3(x + 1f, capY, z - 1f), new Vector3(x + 1f, capY, z),
                            new Vector3(x + 1f + CapWidth, capY, z), new Vector3(x + 1f + CapWidth, capY, z - 1f), capMin, capMax, true);
                    }
                    if (openW)
                    {
                        AddQuad(wallTriangles,
                            new Vector3(x - CapWidth, capY, z - 1f), new Vector3(x - CapWidth, capY, z),
                            new Vector3(x, capY, z), new Vector3(x, capY, z - 1f), capMin, capMax, true);
                    }
                    // 바깥 모서리: 남쪽 띠와 옆 띠 사이의 빈 사각형을 메운다
                    if (openS && openE && !quarterView && !IsFloorTile(tx + 1, ty + 1))
                    {
                        AddQuad(wallTriangles,
                            new Vector3(x + 1f, capY, z - 1f - CapWidth), new Vector3(x + 1f, capY, z - 1f),
                            new Vector3(x + 1f + CapWidth, capY, z - 1f), new Vector3(x + 1f + CapWidth, capY, z - 1f - CapWidth), capMin, capMax);
                    }
                    if (openS && openW && !IsFloorTile(tx - 1, ty + 1))
                    {
                        AddQuad(wallTriangles,
                            new Vector3(x - CapWidth, capY, z - 1f - CapWidth), new Vector3(x - CapWidth, capY, z - 1f),
                            new Vector3(x, capY, z - 1f), new Vector3(x, capY, z - 1f - CapWidth), capMin, capMax);
                    }
                }
            }

            if (_mesh == null)
            {
                _mesh = new Mesh { name = "FloorMesh", hideFlags = HideFlags.DontSave };
            }
            _mesh.Clear();
            _mesh.SetVertices(vertices);
            _mesh.SetUVs(0, uvs);
            _mesh.SetUVs(1, rows);
            _mesh.subMeshCount = 2;
            _mesh.SetTriangles(floorTriangles, 0);
            _mesh.SetTriangles(wallTriangles, 1);
            _mesh.RecalculateNormals();
            _mesh.RecalculateBounds();
            GetComponent<MeshFilter>().sharedMesh = _mesh;
        }

        private bool IsFloorTile(int x, int y) => Materials.IndexOf(At(_rows, x, y)) >= 0;

        // 격자 밖이나 짧은 줄의 빈 칸은 ' ' (= 바닥 없음)
        private static char At(string[] rows, int x, int y) =>
            y >= 0 && y < rows.Length && x >= 0 && x < rows[y].Length ? rows[y][x] : ' ';
    }
}

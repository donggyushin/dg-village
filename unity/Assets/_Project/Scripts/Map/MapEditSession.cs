using System;
using UnityEngine;

namespace DgVillage
{
    /// <summary>
    /// 맵 에디터(DgVillage → Map Editor)로 연 맵의 루트에 붙는다. 오브젝트로 나타나지 않는 맵 데이터를 들고 있다:
    /// id · view · 바닥 layout · 분위기(lighting) · 점광원(lights) · 창문(windows).
    /// 인스펙터에서 고치면 바로 바닥 · 조명에 반영한다 (점광원 · 창문 위치는 Scene 뷰 핸들로도 옮긴다).
    /// 소품 · 좌석 · 포탈은 오브젝트(MapProp · Seat · Portal) 쪽이 원본이다.
    /// </summary>
    [ExecuteAlways]
    public class MapEditSession : MonoBehaviour
    {
        [SerializeField] private string _id;
        [Tooltip("\"quarter\" = 쿼터뷰, \"\" = 정면. 바꾸면 맵을 다시 열어야 보인다")]
        [SerializeField] private string _view;
        [Tooltip("한 글자 = 타일 하나, 첫 줄 = 북쪽. s 돌 · w 나무 · c 카펫 · g 잔디 · p 길, 그 외 = 바닥 없음")]
        [SerializeField, TextArea(5, 40)] private string _layout;
        [SerializeField] private GameMapData.Lighting _lighting = new();
        [SerializeField] private GameMapData.Light[] _lights = Array.Empty<GameMapData.Light>();
        [Tooltip("창문 가운데 (x, z). 북쪽 벽 = 벽 선의 z · 정수 x, 동쪽 벽 = 벽 선의 x · 정수 z. 폭 2 칸")]
        [SerializeField] private GameMapData.Window[] _windows = Array.Empty<GameMapData.Window>();

        private bool _dirty;

        public string Id => _id;
        public GameMapData.Light[] Lights => _lights;
        public GameMapData.Window[] Windows => _windows;

        public void Init(GameMapData map)
        {
            _id = map.id;
            _view = map.view;
            _layout = string.Join("\n", map.layout ?? Array.Empty<string>());
            _lighting = map.lighting ?? new GameMapData.Lighting();
            _lights = map.lights ?? Array.Empty<GameMapData.Light>();
            _windows = map.windows ?? Array.Empty<GameMapData.Window>();
        }

        public void AddLight(GameMapData.Light light) => _lights = Append(_lights, light);

        public void AddWindow(GameMapData.Window window) => _windows = Append(_windows, window);

        /// <summary>바닥 · 창문 · 조명을 지금 값으로 다시 깐다</summary>
        public void Apply()
        {
            var floor = GetComponentInChildren<FloorMesh>();
            if (floor != null)
            {
                floor.SetLayout(_layout);
                floor.SetWindows(Array.ConvertAll(_windows, w => new Vector2(w.x, w.z)));
            }
            var lighting = GetComponentInChildren<MapLighting>();
            if (lighting != null)
            {
                lighting.Init(_lighting, _lights);
            }
        }

        /// <summary>오브젝트로 나타나지 않는 부분만 채운 맵 데이터 (소품 · 좌석 · 포탈은 맵 에디터가 채운다)</summary>
        public GameMapData ToData() => new()
        {
            id = _id,
            view = _view,
            layout = _layout.Replace("\r", "").Split('\n', StringSplitOptions.RemoveEmptyEntries),
            lighting = _lighting,
            lights = _lights,
            windows = _windows,
        };

        // OnValidate 안에서는 메시 · 빛무리를 못 바꿔서 다음 Update 에서
        private void OnValidate() => _dirty = true;

        private void Update()
        {
            if (_dirty)
            {
                _dirty = false;
                Apply();
            }
        }

        private static T[] Append<T>(T[] items, T item)
        {
            var result = new T[items.Length + 1];
            items.CopyTo(result, 0);
            result[^1] = item;
            return result;
        }
    }
}

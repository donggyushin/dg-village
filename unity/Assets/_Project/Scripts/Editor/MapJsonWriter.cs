using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace DgVillage.Editor
{
    /// <summary>
    /// GameMapData → 서버 맵 JSON (python/src/dg_village_server/maps/{id}.json). 사람이 읽고 git diff 를 보기 좋게
    /// 손으로 쓰던 모양 그대로: 목록 항목은 한 줄에 하나, 기본값(0 · "" · false)인 선택 필드는 뺀다.
    /// JsonUtility.ToJson 은 줄을 못 고르고 모든 필드를 써서 직접 쓴다.
    /// </summary>
    internal static class MapJsonWriter
    {
        public static string Write(GameMapData map)
        {
            var sections = new List<string> { $"  \"id\": {Str(map.id)}" };
            if (!string.IsNullOrEmpty(map.view))
            {
                sections.Add($"  \"view\": {Str(map.view)}");
            }
            string lighting = Obj(LightingFields(map.lighting ?? new GameMapData.Lighting()));
            if (lighting != "{}")
            {
                sections.Add($"  \"lighting\": {lighting}");
            }
            string night = Obj(LightingFields(map.night ?? new GameMapData.Lighting()));
            if (night != "{}")
            {
                sections.Add($"  \"night\": {night}");
            }
            string dusk = Obj(LightingFields(map.dusk ?? new GameMapData.Lighting()));
            if (dusk != "{}")
            {
                sections.Add($"  \"dusk\": {dusk}");
            }
            if (map.lights is { Length: > 0 })
            {
                sections.Add(List("lights", map.lights.Select(l => Obj(LightFields(l)))));
            }
            if (map.windows is { Length: > 0 })
            {
                sections.Add(List("windows", map.windows.Select(w => Obj(new() { ("x", Num(w.x)), ("z", Num(w.z)) }))));
            }
            if (map.noWalls)
            {
                sections.Add("  \"noWalls\": true");
            }
            sections.Add(List("layout", (map.layout ?? Array.Empty<string>()).Select(Str)));
            sections.Add(List("seats", (map.seats ?? Array.Empty<GameMapData.Seat>()).Select(s => Obj(SeatFields(s)))));
            sections.Add(List("portals", (map.portals ?? Array.Empty<GameMapData.Portal>()).Select(p => Obj(PortalFields(p)))));
            if (map.pets is { Length: > 0 })
            {
                sections.Add(List("pets", map.pets.Select(p => Obj(new()
                {
                    ("id", Str(p.id)), ("character", Str(p.character)), ("x", Num(p.x)), ("z", Num(p.z)), ("roam", Num(p.roam)),
                }))));
            }
            sections.Add(List("props", (map.props ?? Array.Empty<GameMapData.Prop>()).Select(p => Obj(PropFields(p)))));
            return "{\n" + string.Join(",\n", sections) + "\n}\n";
        }

        private static List<(string, string)> LightingFields(GameMapData.Lighting l)
        {
            var fields = new List<(string, string)>();
            void AddNum(string key, float value)
            {
                if (value != 0f)
                {
                    fields.Add((key, Num(value)));
                }
            }
            void AddStr(string key, string value)
            {
                if (!string.IsNullOrEmpty(value))
                {
                    fields.Add((key, Str(value)));
                }
            }
            AddNum("shadowX", l.shadowX);
            AddNum("shadowZ", l.shadowZ);
            AddNum("shadowStrength", l.shadowStrength);
            AddStr("shadowColor", l.shadowColor);
            AddNum("clouds", l.clouds);
            AddNum("exposure", l.exposure);
            AddNum("contrast", l.contrast);
            AddNum("saturation", l.saturation);
            AddNum("temperature", l.temperature);
            AddStr("colorFilter", l.colorFilter);
            AddNum("vignette", l.vignette);
            AddStr("ambient", l.ambient);
            AddStr("sky", l.sky);
            AddNum("shafts", l.shafts);
            AddStr("shaftColor", l.shaftColor);
            AddNum("shaftReach", l.shaftReach);
            AddNum("shaftSlant", l.shaftSlant);
            return fields;
        }

        private static List<(string, string)> LightFields(GameMapData.Light l)
        {
            var fields = new List<(string, string)>
            {
                ("x", Num(l.x)), ("z", Num(l.z)), ("radius", Num(l.radius)), ("intensity", Num(l.intensity)), ("color", Str(l.color)),
            };
            if (l.flicker != 0f)
            {
                fields.Add(("flicker", Num(l.flicker)));
            }
            if (l.glow != 0f)
            {
                fields.Add(("glow", Num(l.glow)));
                fields.Add(("glowY", Num(l.glowY)));
            }
            if (!string.IsNullOrEmpty(l.when))
            {
                fields.Add(("when", Str(l.when)));
            }
            return fields;
        }

        private static List<(string, string)> SeatFields(GameMapData.Seat s)
        {
            var fields = new List<(string, string)> { ("id", Str(s.id)), ("kind", Str(s.kind)), ("x", Num(s.x)), ("z", Num(s.z)) };
            if (!string.IsNullOrEmpty(s.facing))
            {
                fields.Add(("facing", Str(s.facing)));
            }
            return fields;
        }

        private static List<(string, string)> PortalFields(GameMapData.Portal p) => new()
        {
            ("x", Num(p.x)), ("z", Num(p.z)), ("w", Num(p.w)), ("d", Num(p.d)), ("to", Str(p.to)),
            ("spawnX", Num(p.spawnX)), ("spawnZ", Num(p.spawnZ)),
        };

        private static List<(string, string)> PropFields(GameMapData.Prop p)
        {
            var fields = new List<(string, string)> { ("sprite", Str(p.sprite)), ("x", Num(p.x)), ("z", Num(p.z)), ("shadow", p.shadow.ToString(CultureInfo.InvariantCulture)) };
            if (p.flip)
            {
                fields.Add(("flip", "true"));
            }
            return fields;
        }

        private static string List(string key, IEnumerable<string> items)
        {
            string[] lines = items.ToArray();
            return lines.Length == 0 ? $"  \"{key}\": []" : $"  \"{key}\": [\n    {string.Join(",\n    ", lines)}\n  ]";
        }

        private static string Obj(List<(string Key, string Value)> fields) =>
            fields.Count == 0 ? "{}" : "{" + string.Join(", ", fields.Select(f => $"\"{f.Key}\": {f.Value}")) + "}";

        // 소수 셋째 자리까지 (에디터에서 끌면 생기는 0.30000001 같은 꼬리를 자른다). 정수도 4.0 처럼 소수점을 남긴다
        private static string Num(float value)
        {
            double rounded = Math.Round(value, 3);
            if (rounded == 0d)
            {
                return "0.0";
            }
            return rounded == Math.Floor(rounded)
                ? rounded.ToString("0.0", CultureInfo.InvariantCulture)
                : rounded.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private static string Str(string value) =>
            "\"" + (value ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n") + "\"";
    }
}

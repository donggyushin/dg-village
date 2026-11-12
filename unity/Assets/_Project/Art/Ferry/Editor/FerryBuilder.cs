using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

/// <summary>
/// Builds the passenger ship prefab (Art/Ferry/Ferry.prefab): a 20 m two-masted schooner with a walkable main deck,
/// a raised stern castle with a glowing gallery, a gilded figurehead, portholes and festoon lights.
/// Local frame: +Z = bow, +X = starboard, y 0 = main deck surface (DeckAbovePier above the pier deck).
/// The boarding opening is on the port side (-X) at z GapZ0..GapZ1; "Gangway" and "Boarding gate" toggle it.
/// </summary>
public static class FerryBuilder
{
    private const string Art = "Assets/_Project/Art/Ferry/";
    private const string Project = "Assets/_Project/";
    public const string PrefabPath = Art + "Ferry.prefab";

    public const float Stern = -9.5f, Bow = 10.5f, MaxHalfBeam = 3.2f, Keel = -3.0f, Bulwark = .15f;
    /// <summary>Main deck height above the pier deck; the gangway ramps down by this much.</summary>
    public const float DeckAbovePier = .65f;
    /// <summary>Waterline in local space (world water y -1.25, pier deck y -0.3).</summary>
    public const float Waterline = -1.25f - (-.3f + DeckAbovePier);
    /// <summary>Distance from the pier's centre line to the ship's centre line when berthed.</summary>
    public const float BerthOffset = 1.1f + .3f + MaxHalfBeam;
    public const float HalfLength = 10f;
    public const float GapZ0 = -1.2f, GapZ1 = 1.2f;
    private const float Step = .25f;
    private const float CastleZ0 = -8.8f, CastleZ1 = -5.6f, CastleHalf = 1.75f, CastleH = 2.4f;
    private const float ForeZ = 5.2f, ForeTop = 12.8f, MainZ = -3.0f, MainTop = 14.6f;

    private static Material _hull, _deck, _oak, _darkOak, _honey, _teal, _rope, _iron, _glass, _sail, _pennant, _gilt, _gallery, _figure;

    [MenuItem("DG Village/Waterfront/Ferry prefab")]
    public static void Menu() => Debug.Log("Ferry prefab: " + AssetDatabase.GetAssetPath(BuildPrefab()));

    // ---------------- hull shape ----------------
    private static float T(float z) => z >= 0 ? z / Bow : z / -Stern;
    /// <summary>Half-beam at the sheer.</summary>
    public static float HalfBeam(float z)
    {
        float t = T(z);
        return t >= 0 ? MaxHalfBeam * Mathf.Pow(Mathf.Max(0, 1 - t * t), .6f) : MaxHalfBeam * (1 - .32f * t * t);
    }
    public static float KeelY(float z) { float t = T(z); return t > .55f ? Keel + Mathf.Pow((t - .55f) / .45f, 2) * (-Keel + .6f) : Keel; }
    public static float Sheer(float z) { float t = T(z); return t >= 0 ? .95f + 1.3f * t * t * t : .95f + .55f * t * t; }
    private static float KDeck(float z) => Mathf.Clamp01((0 - KeelY(z)) / (Sheer(z) - KeelY(z)));
    private static Vector3 HullPoint(float z, float k, int side)
    {
        float y = Mathf.Lerp(KeelY(z), Sheer(z), k);
        return new Vector3(side * HalfBeam(z) * Mathf.Pow(Mathf.Max(k, 0), .45f), y, z);
    }
    private static Vector3 HullNormal(float z, float k, int side)
    {
        var dk = HullPoint(z, Mathf.Min(k + .01f, 1), side) - HullPoint(z, Mathf.Max(k - .01f, 0), side);
        var dz = HullPoint(z + .05f, k, side) - HullPoint(z - .05f, k, side);
        var n = Vector3.Cross(dk, dz).normalized;
        return n.x * side < 0 ? -n : n;
    }
    /// <summary>Half-width of the walkable deck (inside the bulwark).</summary>
    public static float DeckHalf(float z) => Mathf.Max(0, HalfBeam(z) * Mathf.Pow(KDeck(z), .45f) - Bulwark);
    private static float DeckOuter(float z) => HalfBeam(z) * Mathf.Pow(KDeck(z), .45f);

    // ---------------- mesh helper ----------------
    private sealed class MeshBuilder
    {
        public readonly List<Vector3> V = new(); public readonly List<Vector2> UV = new(); public readonly List<int>[] Sub;
        public MeshBuilder(int subs) { Sub = new List<int>[subs]; for (int i = 0; i < subs; i++) Sub[i] = new List<int>(); }
        public int Add(Vector3 p, Vector2 uv) { V.Add(p); UV.Add(uv); return V.Count - 1; }
        /// <summary>Triangle oriented so its normal faces along hint.</summary>
        public void Tri(int sub, int a, int b, int c, Vector3 hint)
        {
            var n = Vector3.Cross(V[b] - V[a], V[c] - V[a]);
            if (n.sqrMagnitude < 1e-10f) return;
            if (Vector3.Dot(n, hint) >= 0) Sub[sub].AddRange(new[] { a, b, c }); else Sub[sub].AddRange(new[] { a, c, b });
        }
        public void Quad(int sub, int a, int b, int c, int d, Vector3 hint) { Tri(sub, a, b, c, hint); Tri(sub, a, c, d, hint); }
        public Mesh Save(string name)
        {
            string path = Art + "Meshes/" + name + ".asset";
            var m = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (m) m.Clear(); else { m = new Mesh { name = name }; AssetDatabase.CreateAsset(m, path); }
            m.indexFormat = IndexFormat.UInt32;
            m.SetVertices(V); m.SetUVs(0, UV); m.subMeshCount = Sub.Length;
            for (int i = 0; i < Sub.Length; i++) m.SetTriangles(Sub[i], i);
            m.RecalculateNormals(); m.RecalculateBounds(); m.RecalculateTangents();
            EditorUtility.SetDirty(m); return m;
        }
    }

    private static List<float> Stations()
    {
        var zs = new List<float>();
        for (float z = Stern; z <= Bow + .001f; z += Step) zs.Add(Mathf.Round(z * 100) / 100);
        foreach (var g in new[] { GapZ0, GapZ1 }) if (!zs.Exists(z => Mathf.Abs(z - g) < .01f)) zs.Add(g);
        zs.Sort();
        return zs;
    }
    private static bool InGap(float z0, float z1, int side) => side < 0 && z0 >= GapZ0 - .001f && z1 <= GapZ1 + .001f;

    /// <summary>Hull: sub 0 = painted outer hull, 1 = bulwark inside + transom, 2 = rail cap.</summary>
    private static Mesh HullMesh()
    {
        var mb = new MeshBuilder(3); var zs = Stations();
        const int below = 10, above = 4;
        float uScale = 1 / ((Sheer(0) - Keel) * 4);
        foreach (int side in new[] { -1, 1 })
        {
            var grid = new int[zs.Count, below + above + 1];
            for (int i = 0; i < zs.Count; i++)
            {
                float z = zs[i], kd = KDeck(z), kw = Mathf.Clamp01((Waterline - KeelY(z)) / (Sheer(z) - KeelY(z)));
                for (int j = 0; j <= below + above; j++)
                {
                    float k = j <= below ? kd * j / below : Mathf.Lerp(kd, 1, (j - below) / (float)above);
                    float v = k < kw ? k / Mathf.Max(kw, .001f) * .17f : .17f + (k - kw) / Mathf.Max(1 - kw, .001f) * .83f;
                    grid[i, j] = mb.Add(HullPoint(z, k, side), new Vector2(side * z * uScale, v));
                }
            }
            for (int i = 0; i + 1 < zs.Count; i++)
                for (int j = 0; j < below + above; j++)
                {
                    if (j >= below && InGap(zs[i], zs[i + 1], side)) continue;
                    mb.Quad(0, grid[i, j], grid[i + 1, j], grid[i + 1, j + 1], grid[i, j + 1], new Vector3(side, 0, 0));
                }
            for (int i = 0; i + 1 < zs.Count; i++)
            {
                if (InGap(zs[i], zs[i + 1], side)) continue;
                float za = zs[i], zb = zs[i + 1];
                if (DeckHalf(za) <= 0 && DeckHalf(zb) <= 0) continue;
                Vector3 Ia(float z, float y) => new(side * DeckHalf(z), y, z);
                int a0 = mb.Add(Ia(za, 0), new Vector2(za / 2, 0)), b0 = mb.Add(Ia(zb, 0), new Vector2(zb / 2, 0));
                int a1 = mb.Add(Ia(za, Sheer(za)), new Vector2(za / 2, Sheer(za) / 2)), b1 = mb.Add(Ia(zb, Sheer(zb)), new Vector2(zb / 2, Sheer(zb) / 2));
                mb.Quad(1, a0, b0, b1, a1, new Vector3(-side, 0, 0));
                int o0 = mb.Add(HullPoint(za, 1, side) + Vector3.up * .001f, new Vector2(za / 2, 0)), o1 = mb.Add(HullPoint(zb, 1, side) + Vector3.up * .001f, new Vector2(zb / 2, 0));
                int c0 = mb.Add(Ia(za, Sheer(za)), new Vector2(za / 2, .3f)), c1 = mb.Add(Ia(zb, Sheer(zb)), new Vector2(zb / 2, .3f));
                mb.Quad(2, o0, o1, c1, c0, Vector3.up);
            }
        }
        // boarding opening end caps (port side)
        foreach (float z in new[] { GapZ0, GapZ1 })
        {
            var outer = HullPoint(z, 1, -1); var deckOuter = HullPoint(z, KDeck(z), -1); float ix = -DeckHalf(z);
            int a = mb.Add(deckOuter, new Vector2(0, 0)), b = mb.Add(new Vector3(ix, 0, z), new Vector2(.12f, 0));
            int c = mb.Add(new Vector3(ix, outer.y, z), new Vector2(.12f, .4f)), d = mb.Add(outer, new Vector2(0, .4f));
            mb.Quad(1, a, b, c, d, new Vector3(0, 0, z < 0 ? 1 : -1));
        }
        // transom: outer face (double sided) + inside face above the deck + rail cap
        {
            float z = Stern; var ring = new List<int>(); var ringBack = new List<int>(); var pts = new List<Vector3>();
            for (int j = 0; j <= 14; j++) pts.Add(HullPoint(z, 1 - j / 14f, -1));
            for (int j = 1; j <= 14; j++) pts.Add(HullPoint(z, j / 14f, 1));
            var centre = new Vector3(0, (KeelY(z) + Sheer(z)) / 2, z);
            int ci = mb.Add(centre, new Vector2(0, .5f)), cb = mb.Add(centre, new Vector2(0, .5f));
            foreach (var p in pts) { ring.Add(mb.Add(p, new Vector2(p.x / 2, p.y / 2))); ringBack.Add(mb.Add(p, new Vector2(p.x / 2, p.y / 2))); }
            for (int i = 0; i + 1 < pts.Count; i++)
            {
                mb.Tri(1, ci, ring[i], ring[i + 1], Vector3.back);
                mb.Tri(1, cb, ringBack[i], ringBack[i + 1], Vector3.forward);
            }
            float zi = z + Bulwark, h = DeckHalf(zi);
            int a = mb.Add(new Vector3(-h, 0, zi), new Vector2(-h / 2, 0)), b = mb.Add(new Vector3(h, 0, zi), new Vector2(h / 2, 0));
            int c = mb.Add(new Vector3(h, Sheer(z), zi), new Vector2(h / 2, Sheer(z) / 2)), d = mb.Add(new Vector3(-h, Sheer(z), zi), new Vector2(-h / 2, Sheer(z) / 2));
            mb.Quad(1, a, b, c, d, Vector3.forward);
            int e = mb.Add(new Vector3(-HalfBeam(z), Sheer(z) + .001f, z), Vector2.zero), f = mb.Add(new Vector3(HalfBeam(z), Sheer(z) + .001f, z), new Vector2(1, 0));
            int g = mb.Add(new Vector3(h, Sheer(z) + .001f, zi), new Vector2(1, .3f)), k2 = mb.Add(new Vector3(-h, Sheer(z) + .001f, zi), new Vector2(0, .3f));
            mb.Quad(2, e, f, g, k2, Vector3.up);
        }
        return mb.Save("Ferry hull");
    }

    private static Mesh DeckMesh()
    {
        var mb = new MeshBuilder(1); var zs = Stations();
        int pa = -1, pb = -1;
        foreach (float z0 in zs)
        {
            float z = Mathf.Max(z0, Stern + Bulwark), h = DeckHalf(z);
            int a = mb.Add(new Vector3(-h, 0, z), new Vector2(-h / 4.8f, z / 4.8f)), b = mb.Add(new Vector3(h, 0, z), new Vector2(h / 4.8f, z / 4.8f));
            if (pa >= 0) mb.Quad(0, pa, pb, b, a, Vector3.up);
            pa = a; pb = b;
            if (h <= 0) break;
        }
        // deck strip filling the boarding opening up to the hull's outer edge
        int s0 = mb.Add(new Vector3(-DeckHalf(GapZ0), 0, GapZ0), new Vector2(0, GapZ0 / 4.8f)), s1 = mb.Add(new Vector3(-DeckHalf(GapZ1), 0, GapZ1), new Vector2(0, GapZ1 / 4.8f));
        int s2 = mb.Add(new Vector3(-DeckOuter(GapZ1), 0, GapZ1), new Vector2(.04f, GapZ1 / 4.8f)), s3 = mb.Add(new Vector3(-DeckOuter(GapZ0), 0, GapZ0), new Vector2(.04f, GapZ0 / 4.8f));
        mb.Quad(0, s0, s1, s2, s3, Vector3.up);
        return mb.Save("Ferry deck");
    }

    /// <summary>Invisible double-sided wall along the inside of the bulwark, open at the boarding gap.</summary>
    private static Mesh WallMesh()
    {
        var mb = new MeshBuilder(1); var zs = Stations();
        void Wall(Vector3 a, Vector3 b)
        {
            int i0 = mb.Add(a + Vector3.down * .4f, Vector2.zero), i1 = mb.Add(b + Vector3.down * .4f, Vector2.zero);
            int i2 = mb.Add(b + Vector3.up * 2.6f, Vector2.zero), i3 = mb.Add(a + Vector3.up * 2.6f, Vector2.zero);
            var n = Vector3.Cross(b - a, Vector3.up);
            mb.Quad(0, i0, i1, i2, i3, n); mb.Quad(0, i0, i1, i2, i3, -n);
        }
        foreach (int side in new[] { -1, 1 })
            for (int i = 0; i + 1 < zs.Count; i++)
            {
                if (InGap(zs[i], zs[i + 1], side)) continue;
                float za = Mathf.Max(zs[i], Stern + Bulwark), zb = zs[i + 1];
                if (DeckHalf(za) <= 0 && DeckHalf(zb) <= 0) continue;
                Wall(new Vector3(side * DeckHalf(za), 0, za), new Vector3(side * DeckHalf(zb), 0, zb));
            }
        float zs0 = Stern + Bulwark;
        Wall(new Vector3(-DeckHalf(zs0), 0, zs0), new Vector3(DeckHalf(zs0), 0, zs0));
        return mb.Save("Ferry walls");
    }

    /// <summary>Sail in the centre plane (x = 0) from four corners; a triangle repeats its last corner.</summary>
    private static Mesh SailMesh(string name, Vector3 tack, Vector3 clew, Vector3 peak, Vector3 throat, Rect uv, float billow)
    {
        var mb = new MeshBuilder(1); const int n = 10; var idx = new int[n + 1, n + 1];
        for (int i = 0; i <= n; i++) for (int j = 0; j <= n; j++)
        {
            float u = i / (float)n, v = j / (float)n;
            var p = Vector3.Lerp(Vector3.Lerp(tack, clew, u), Vector3.Lerp(throat, peak, u), v);
            p.x = -billow * Mathf.Sin(Mathf.PI * u) * Mathf.Sin(Mathf.PI * Mathf.Clamp01(v * .9f + .1f));
            idx[i, j] = mb.Add(p, new Vector2(uv.xMin + u * uv.width, uv.yMin + v * uv.height));
        }
        for (int i = 0; i < n; i++) for (int j = 0; j < n; j++) mb.Quad(0, idx[i, j], idx[i + 1, j], idx[i + 1, j + 1], idx[i, j + 1], Vector3.left);
        return mb.Save(name);
    }

    /// <summary>Flat picture quad in the centre plane, u = 0 at the bow end (so it reads correctly from the port side).</summary>
    private static Mesh PlaneMesh(string name, float zFront, float zBack, float y0, float y1)
    {
        var mb = new MeshBuilder(1);
        int a = mb.Add(new Vector3(0, y0, zFront), new Vector2(0, 0)), b = mb.Add(new Vector3(0, y0, zBack), new Vector2(1, 0));
        int c = mb.Add(new Vector3(0, y1, zBack), new Vector2(1, 1)), d = mb.Add(new Vector3(0, y1, zFront), new Vector2(0, 1));
        mb.Quad(0, a, b, c, d, Vector3.left);
        return mb.Save(name);
    }

    // ---------------- materials ----------------
    private static Texture2D Tex(string file, bool alpha, bool clamp)
    {
        string path = Art + "Textures/" + file;
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        if (!importer) { Debug.LogWarning("Ferry: missing " + path); return null; }
        var wrap = clamp ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
        if (importer.alphaIsTransparency != alpha || importer.wrapMode != wrap || importer.textureCompression != TextureImporterCompression.Uncompressed)
        {
            importer.textureType = TextureImporterType.Default; importer.alphaIsTransparency = alpha; importer.mipmapEnabled = true;
            importer.wrapMode = wrap; importer.filterMode = FilterMode.Bilinear; importer.anisoLevel = 4;
            importer.maxTextureSize = 2048; importer.textureCompression = TextureImporterCompression.Uncompressed; importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }
    private static Material LoadOrCreate(string name, Material template = null)
    {
        string path = Art + "Materials/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!m)
        {
            m = template ? new Material(template) : new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.name = name; AssetDatabase.CreateAsset(m, path);
        }
        return m;
    }
    private static Material Lit(string name, string tex, Color tint, float smooth, bool clamp = false, bool twoSided = false)
    {
        var m = LoadOrCreate(name);
        if (tex != null) m.SetTexture("_BaseMap", Tex(tex, false, clamp));
        m.SetColor("_BaseColor", tint); m.SetFloat("_Smoothness", smooth);
        if (twoSided) { m.SetFloat("_Cull", 0); m.doubleSidedGI = true; }
        EditorUtility.SetDirty(m); return m;
    }
    private static Material Glow(Material m, Texture map, Color emission)
    {
        m.EnableKeyword("_EMISSION");
        if (map) m.SetTexture("_EmissionMap", map);
        m.SetColor("_EmissionColor", emission);
        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive; // keeps _EMISSION in builds
        EditorUtility.SetDirty(m); return m;
    }
    private static Material Cutout(string name, string tex)
    {
        var m = LoadOrCreate(name, AssetDatabase.LoadAssetAtPath<Material>(Project + "Materials/Garden/shrub.mat"));
        m.mainTexture = Tex(tex, true, true); m.color = Color.white; m.SetFloat("_Cutoff", .4f); m.SetFloat("_Cull", 0); m.SetFloat("_Smoothness", .15f);
        m.DisableKeyword("_EMISSION"); EditorUtility.SetDirty(m); return m;
    }
    private static void LoadMaterials()
    {
        _hull = Lit("Ferry hull", "FerryHull.png", Color.white, .18f);
        _deck = Lit("Ferry deck", "FerryDeck.png", new Color(.86f, .8f, .74f), .08f);
        _sail = Lit("Ferry sail", "FerrySail.png", Color.white, 0, true, true);
        _gallery = Lit("Ferry stern gallery", "FerryStern.png", Color.white, .2f, true);
        // only the window panes glow (FerrySternGlow.png = bright amber pixels of FerryStern.png)
        Glow(_gallery, Tex("FerrySternGlow.png", false, true), new Color(1f, .85f, .7f) * 1.3f);
        _gilt = Lit("Ferry gilt", null, new Color(.93f, .7f, .3f), .55f);
        _gilt.SetFloat("_Metallic", .65f); EditorUtility.SetDirty(_gilt);
        _glass = Lit("Ferry lantern glass", null, new Color(1f, .7f, .32f), .5f);
        Glow(_glass, null, new Color(1f, .5f, .12f) * 2.2f);
        _pennant = Cutout("Ferry pennant", "FerryPennant.png");
        _figure = Cutout("Ferry figurehead", "FerryFigurehead.png");
        _oak = AssetDatabase.LoadAssetAtPath<Material>(Project + "Art/AcademyGarden/Materials/Weathered oak.mat");
        _darkOak = AssetDatabase.LoadAssetAtPath<Material>(Project + "Art/AcademyGarden/Materials/Oak end grain.mat");
        _honey = AssetDatabase.LoadAssetAtPath<Material>(Project + "Art/CanalLanding/Materials/Honey oak.mat");
        _teal = AssetDatabase.LoadAssetAtPath<Material>(Project + "Art/CanalLanding/Materials/Painted teal hull.mat");
        _rope = AssetDatabase.LoadAssetAtPath<Material>(Project + "Art/CanalLanding/Materials/Hemp rope.mat");
        _iron = AssetDatabase.LoadAssetAtPath<Material>(Project + "Art/CanalLanding/Materials/Dark iron.mat");
    }

    // ---------------- parts ----------------
    private static GameObject Part(Transform parent, string name, Mesh mesh, params Material[] mats)
    {
        var g = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer)); g.transform.SetParent(parent, false);
        g.GetComponent<MeshFilter>().sharedMesh = mesh; g.GetComponent<MeshRenderer>().sharedMaterials = mats; return g;
    }
    private static GameObject Prim(Transform parent, PrimitiveType type, string name, Vector3 pos, Vector3 scale, Material mat, Quaternion? rot = null, bool shadows = true)
    {
        var g = GameObject.CreatePrimitive(type); g.name = name; g.transform.SetParent(parent, false);
        g.transform.localPosition = pos; g.transform.localRotation = rot ?? Quaternion.identity; g.transform.localScale = scale;
        var r = g.GetComponent<Renderer>(); r.sharedMaterial = mat; if (!shadows) r.shadowCastingMode = ShadowCastingMode.Off;
        Object.DestroyImmediate(g.GetComponent<Collider>()); return g;
    }
    private static GameObject Spar(Transform parent, string name, Vector3 a, Vector3 b, float thickness, Material mat) =>
        Prim(parent, PrimitiveType.Cylinder, name, (a + b) / 2, new Vector3(thickness, Vector3.Distance(a, b) / 2, thickness), mat,
            Quaternion.FromToRotation(Vector3.up, (b - a).normalized));
    private static void Line(Transform parent, string name, Vector3[] pts, float width, Material mat, bool loop = false)
    {
        var g = new GameObject(name); g.transform.SetParent(parent, false);
        var l = g.AddComponent<LineRenderer>(); l.useWorldSpace = false; l.positionCount = pts.Length; l.SetPositions(pts); l.loop = loop;
        l.startWidth = l.endWidth = width; l.numCornerVertices = 2; l.numCapVertices = 2; l.sharedMaterial = mat;
        l.shadowCastingMode = ShadowCastingMode.Off;
    }
    private static void Rope(Transform parent, string name, Vector3[] pts, float width) => Line(parent, name, pts, width, _rope);
    private static Vector3[] Sag(Vector3 a, Vector3 b, float sag, int n = 8)
    {
        var p = new Vector3[n + 1];
        for (int i = 0; i <= n; i++) { float t = i / (float)n; p[i] = Vector3.Lerp(a, b, t) + Vector3.down * sag * 4 * t * (1 - t); }
        return p;
    }
    private static void Lantern(Transform parent, string name, Vector3 pos, float range, float intensity, float scale = 1)
    {
        var root = new GameObject(name).transform; root.SetParent(parent, false); root.localPosition = pos; root.localScale = Vector3.one * scale;
        Prim(root, PrimitiveType.Cube, "Lantern cap", new Vector3(0, .17f, 0), new Vector3(.26f, .06f, .26f), _gilt);
        Prim(root, PrimitiveType.Sphere, "Lantern finial", new Vector3(0, .23f, 0), Vector3.one * .07f, _gilt);
        Prim(root, PrimitiveType.Cube, "Lantern glass", Vector3.zero, new Vector3(.16f, .26f, .16f), _glass, null, false);
        Prim(root, PrimitiveType.Cube, "Lantern base", new Vector3(0, -.15f, 0), new Vector3(.22f, .05f, .22f), _iron);
        if (range <= 0) return;
        var lg = new GameObject("Lantern light"); lg.transform.SetParent(root, false);
        var l = lg.AddComponent<Light>(); l.type = LightType.Point; l.color = new Color(1f, .72f, .42f); l.range = range; l.intensity = intensity; l.shadows = LightShadows.None;
    }
    private static void Collider(Transform parent, string name, Vector3 centre, Vector3 size, Quaternion? rot = null)
    {
        var g = new GameObject(name); g.transform.SetParent(parent, false); g.transform.localPosition = centre; g.transform.localRotation = rot ?? Quaternion.identity;
        g.AddComponent<BoxCollider>().size = size;
    }
    private static void Bench(Transform t, int side, float z0, float z1)
    {
        float x = side * (DeckHalf((z0 + z1) / 2) - .26f), zc = (z0 + z1) / 2, len = z1 - z0;
        Prim(t, PrimitiveType.Cube, "Bench seat", new Vector3(x, .45f, zc), new Vector3(.46f, .07f, len), _honey);
        Prim(t, PrimitiveType.Cube, "Bench back", new Vector3(x + side * .2f, .72f, zc), new Vector3(.06f, .42f, len), _honey);
        for (float z = z0 + .2f; z < z1; z += .9f) Prim(t, PrimitiveType.Cube, "Bench leg", new Vector3(x, .21f, z), new Vector3(.38f, .42f, .07f), _darkOak);
        Collider(t, "Bench collider", new Vector3(x, .3f, zc), new Vector3(.5f, .6f, len));
    }

    public static GameObject BuildPrefab()
    {
        foreach (var f in new[] { "Meshes", "Materials", "Textures" }) Directory.CreateDirectory(Art + f);
        AssetDatabase.Refresh();
        LoadMaterials();
        var root = new GameObject("Ferry");
        try
        {
            var t = root.transform;
            var hull = Part(t, "Hull", HullMesh(), _hull, _honey, _darkOak);
            hull.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.TwoSided;
            var deck = Part(t, "Deck", DeckMesh(), _deck);
            deck.AddComponent<MeshCollider>().sharedMesh = deck.GetComponent<MeshFilter>().sharedMesh;
            var walls = new GameObject("Invisible deck walls"); walls.transform.SetParent(t, false);
            walls.AddComponent<MeshCollider>().sharedMesh = WallMesh();
            var trim = new GameObject("Gilded trim").transform; trim.SetParent(t, false);

            // gilded rub rails along both sides at deck level and just under the rail
            foreach (int side in new[] { -1, 1 })
                foreach (float kk in new[] { 0f, .93f })
                {
                    var pts = new List<Vector3>();
                    for (float z = Stern + .05f; z <= Bow - .3f; z += .25f)
                    {
                        if (side < 0 && kk > 0 && z > GapZ0 - .05f && z < GapZ1 + .05f) { if (pts.Count > 1) Line(trim, "Gilded rail", pts.ToArray(), .07f, _gilt); pts.Clear(); continue; }
                        float k = kk == 0 ? KDeck(z) : Mathf.Lerp(KDeck(z), 1, kk);
                        pts.Add(HullPoint(z, k, side) + HullNormal(z, k, side) * .03f);
                    }
                    if (pts.Count > 1) Line(trim, kk == 0 ? "Gilded wale" : "Gilded rail", pts.ToArray(), kk == 0 ? .11f : .07f, _gilt);
                }
            // portholes below the deck on both sides
            var portholes = new GameObject("Portholes").transform; portholes.SetParent(t, false);
            foreach (int side in new[] { -1, 1 })
                for (float z = -8f; z <= 7.6f; z += 1.55f)
                {
                    float y = -.72f, k = (y - KeelY(z)) / (Sheer(z) - KeelY(z));
                    var p = HullPoint(z, k, side); var n = HullNormal(z, k, side); var rot = Quaternion.FromToRotation(Vector3.up, n);
                    Prim(portholes, PrimitiveType.Cylinder, "Porthole rim", p + n * .02f, new Vector3(.36f, .025f, .36f), _gilt, rot);
                    Prim(portholes, PrimitiveType.Cylinder, "Porthole glass", p + n * .035f, new Vector3(.25f, .02f, .25f), _glass, rot, false);
                }

            // stem post, bowsprit, figurehead
            var stem = new List<Vector3>();
            for (float z = 6f; z <= Bow + .001f; z += .25f) stem.Add(new Vector3(0, KeelY(z), z));
            stem.Add(new Vector3(0, Sheer(Bow) + .25f, Bow + .05f));
            for (int i = 0; i + 1 < stem.Count; i++) Spar(t, "Stem", stem[i], stem[i + 1], .22f, _darkOak);
            var sprit0 = new Vector3(0, Sheer(Bow) - .15f, Bow - 2f); var spritTip = new Vector3(0, Sheer(Bow) + 1.25f, Bow + 3.6f);
            Spar(t, "Bowsprit", sprit0, spritTip, .2f, _oak);
            Prim(t, PrimitiveType.Sphere, "Bowsprit cap", spritTip, Vector3.one * .22f, _gilt);
            var fig = Part(t, "Figurehead", PlaneMesh("Ferry figurehead", Bow + 2.1f, Bow - .1f, Sheer(Bow) - 2.0f, Sheer(Bow) + .2f), _figure);
            fig.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.TwoSided;

            // fenders along the port side (boarding side)
            foreach (float z in new[] { -5.5f, -2.6f, 2.6f, 5.2f })
            {
                var top = HullPoint(z, 1, -1);
                Prim(t, PrimitiveType.Capsule, "Rope fender", new Vector3(top.x - .16f, -.45f, z), new Vector3(.32f, .36f, .32f), _rope);
                Rope(t, "Fender line", new[] { top + Vector3.up * .02f, new Vector3(top.x - .16f, -.1f, z) }, .03f);
            }

            // ---- rig: two masts with gaff sails, gaff topsails and two headsails ----
            var rig = new GameObject("Rig").transform; rig.SetParent(t, false);
            var fullSail = new Rect(0, 0, 1, 1); var plain = new Rect(.03f, .06f, .2f, .5f);
            foreach (var (mz, top, name) in new[] { (ForeZ, ForeTop, "Foremast"), (MainZ, MainTop, "Mainmast") })
            {
                Spar(rig, name, new Vector3(0, -.2f, mz), new Vector3(0, top, mz), .3f, _oak);
                Prim(rig, PrimitiveType.Cylinder, name + " step", new Vector3(0, .12f, mz), new Vector3(.55f, .12f, .55f), _darkOak);
                Prim(rig, PrimitiveType.Sphere, name + " truck", new Vector3(0, top + .08f, mz), Vector3.one * .26f, _gilt);
                Prim(rig, PrimitiveType.Cylinder, name + " top band", new Vector3(0, top - 2.6f, mz), new Vector3(.42f, .08f, .42f), _gilt);
                var c = new GameObject(name + " collider"); c.transform.SetParent(rig, false); c.transform.localPosition = new Vector3(0, 1.5f, mz);
                var cap = c.AddComponent<CapsuleCollider>(); cap.radius = .3f; cap.height = 3; cap.direction = 1;
                var pennant = Part(rig, name + " pennant", PlaneMesh("Ferry pennant", 0, -2.2f, -.27f, .27f), _pennant);
                pennant.transform.localPosition = new Vector3(0, top - .2f, mz - .12f);
                pennant.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
                // shrouds to the rail on both sides
                foreach (int side in new[] { -1, 1 })
                    foreach (float dz in new[] { -.3f, -1.1f, -1.9f })
                        Rope(rig, name + " shroud", new[] { new Vector3(0, top - 2.7f, mz), HullPoint(mz + dz, 1, side) }, .035f);
            }
            void Sail(string name, Vector3 tack, Vector3 clew, Vector3 peak, Vector3 throat, Rect uv, float billow)
            {
                var s = Part(rig, name, SailMesh("Ferry " + name.ToLower(), tack, clew, peak, throat, uv, billow), _sail);
                s.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.TwoSided;
                s.AddComponent<Occluder>();
            }
            // fore gaff sail + boom/gaff
            Spar(rig, "Fore boom", new Vector3(0, 2.5f, ForeZ - .1f), new Vector3(0, 2.5f, -1.95f), .16f, _oak);
            Spar(rig, "Fore gaff", new Vector3(0, 9.0f, ForeZ - .1f), new Vector3(0, 11.3f, -1.5f), .14f, _oak);
            Sail("Fore sail", new(0, 2.6f, ForeZ - .15f), new(0, 2.6f, -1.85f), new(0, 11.15f, -1.45f), new(0, 9.0f, ForeZ - .15f), fullSail, .5f);
            Sail("Fore topsail", new(0, 9.25f, ForeZ - .15f), new(0, 11.4f, -1.2f), new(0, ForeTop - .3f, ForeZ - .15f), new(0, ForeTop - .3f, ForeZ - .15f), plain, .2f);
            // main gaff sail over the stern castle
            Spar(rig, "Main boom", new Vector3(0, 3.75f, MainZ - .1f), new Vector3(0, 3.75f, -9.9f), .18f, _oak);
            Spar(rig, "Main gaff", new Vector3(0, 10.6f, MainZ - .1f), new Vector3(0, 13.4f, -8.9f), .15f, _oak);
            Sail("Main sail", new(0, 3.85f, MainZ - .15f), new(0, 3.85f, -9.6f), new(0, 13.25f, -8.8f), new(0, 10.6f, MainZ - .15f), fullSail, .6f);
            Sail("Main topsail", new(0, 10.85f, MainZ - .15f), new(0, 13.5f, -8.4f), new(0, MainTop - .3f, MainZ - .15f), new(0, MainTop - .3f, MainZ - .15f), plain, .2f);
            // headsails
            Sail("Jib", new(0, spritTip.y - .1f, spritTip.z - .2f), new(0, 3.2f, 9.3f), new(0, ForeTop - 1.2f, ForeZ + .2f), new(0, ForeTop - 1.2f, ForeZ + .2f), plain, .3f);
            Sail("Staysail", new(0, Sheer(Bow) + .1f, Bow - .6f), new(0, 2.6f, 7.1f), new(0, 9.4f, ForeZ + .2f), new(0, 9.4f, ForeZ + .2f), plain, .25f);
            Rope(rig, "Outer forestay", new[] { new Vector3(0, ForeTop - 1.1f, ForeZ), spritTip }, .04f);
            Rope(rig, "Inner forestay", new[] { new Vector3(0, 9.5f, ForeZ), new Vector3(0, Sheer(Bow), Bow - .5f) }, .04f);
            Rope(rig, "Spring stay", new[] { new Vector3(0, ForeTop - .4f, ForeZ), new Vector3(0, MainTop - 1.6f, MainZ) }, .035f);
            Rope(rig, "Backstay", new[] { new Vector3(0, MainTop - .4f, MainZ), new Vector3(0, CastleH + 1.0f, Stern + .3f) }, .035f);
            foreach (var (mz, top, gz, gy) in new[] { (ForeZ, ForeTop, -1.5f, 11.3f), (MainZ, MainTop, -8.9f, 13.4f) })
                Rope(rig, "Peak halyard", new[] { new Vector3(0, top - .3f, mz), new Vector3(0, gy, gz) }, .028f);

            // festoon lights: bowsprit -> foremast -> mainmast -> ensign staff
            var lights = new GameObject("Festoon lights").transform; lights.SetParent(rig, false);
            var ensignTop = new Vector3(0, CastleH + 2.6f, Stern + .35f);
            var chain = new[] { spritTip, new Vector3(0, ForeTop - 1.6f, ForeZ), new Vector3(0, MainTop - 2.9f, MainZ), ensignTop };
            for (int i = 0; i + 1 < chain.Length; i++)
            {
                var pts = Sag(chain[i], chain[i + 1], .5f, 14);
                Line(lights, "Festoon wire", pts, .02f, _iron);
                for (int j = 1; j < pts.Length - 1; j++) Prim(lights, PrimitiveType.Sphere, "Festoon bulb", pts[j] + Vector3.down * .09f, Vector3.one * .13f, _glass, null, false);
            }

            // ---- stern castle with a glowing gallery on the port side and a poop deck on top ----
            var castle = new GameObject("Stern castle").transform; castle.SetParent(t, false);
            float czm = (CastleZ0 + CastleZ1) / 2, cl = CastleZ1 - CastleZ0;
            Prim(castle, PrimitiveType.Cube, "Castle walls", new Vector3(0, CastleH / 2, czm), new Vector3(CastleHalf * 2, CastleH, cl), _honey);
            var gallery = Part(castle, "Gallery", PlaneMesh("Ferry stern gallery", CastleZ1 - .02f, CastleZ0 + .02f, .05f, CastleH - .05f), _gallery);
            gallery.transform.localPosition = new Vector3(-CastleHalf - .015f, 0, 0);
            var galleryStar = Part(castle, "Gallery (starboard)", gallery.GetComponent<MeshFilter>().sharedMesh, _gallery);
            galleryStar.transform.localPosition = new Vector3(CastleHalf + .015f, 0, 0); galleryStar.transform.localScale = new Vector3(-1, 1, 1);
            foreach (int side in new[] { -1, 1 })
            {
                foreach (float z in new[] { CastleZ0 + .06f, CastleZ1 - .06f })
                    Prim(castle, PrimitiveType.Cube, "Gilded corner", new Vector3(side * (CastleHalf + .03f), CastleH / 2, z), new Vector3(.14f, CastleH + .04f, .14f), _gilt);
                Prim(castle, PrimitiveType.Cube, "Cornice", new Vector3(side * (CastleHalf + .04f), CastleH + .02f, czm), new Vector3(.16f, .14f, cl + .16f), _darkOak);
            }
            Prim(castle, PrimitiveType.Cube, "Front cornice", new Vector3(0, CastleH + .02f, CastleZ1 + .04f), new Vector3(CastleHalf * 2 + .2f, .14f, .16f), _darkOak);
            // poop deck slab + gilded balustrade
            Prim(castle, PrimitiveType.Cube, "Poop deck", new Vector3(0, CastleH + .1f, czm), new Vector3(CastleHalf * 2 + .3f, .12f, cl + .3f), _deck);
            var corners = new[] { new Vector3(-CastleHalf - .08f, 0, CastleZ1 + .08f), new Vector3(CastleHalf + .08f, 0, CastleZ1 + .08f),
                                  new Vector3(CastleHalf + .08f, 0, CastleZ0 - .08f), new Vector3(-CastleHalf - .08f, 0, CastleZ0 - .08f) };
            for (int i = 0; i < 4; i++)
            {
                var a = corners[i]; var b = corners[(i + 1) % 4]; int n = Mathf.Max(2, Mathf.RoundToInt(Vector3.Distance(a, b) / .35f));
                for (int j = 0; j <= n; j++)
                {
                    var p = Vector3.Lerp(a, b, j / (float)n) + Vector3.up * (CastleH + .16f);
                    Prim(castle, PrimitiveType.Cylinder, "Baluster", p + Vector3.up * .3f, new Vector3(.07f, .3f, .07f), j == 0 ? _gilt : _honey);
                }
                var r0 = a + Vector3.up * (CastleH + .78f); var r1 = b + Vector3.up * (CastleH + .78f);
                Prim(castle, PrimitiveType.Cube, "Balustrade rail", (r0 + r1) / 2, new Vector3(.12f, .08f, Vector3.Distance(r0, r1) + .1f), _gilt, Quaternion.LookRotation(r1 - r0));
            }
            // helm on the poop deck, lanterns at the corners, ensign staff at the stern
            var helm = new GameObject("Helm").transform; helm.SetParent(castle, false); helm.localPosition = new Vector3(0, CastleH + .16f, CastleZ1 - .7f);
            Prim(helm, PrimitiveType.Cube, "Helm stand", new Vector3(0, .5f, 0), new Vector3(.22f, 1f, .22f), _darkOak);
            var wheel = new Vector3[24];
            for (int i = 0; i < 24; i++) { float a = i * Mathf.PI * 2 / 24; wheel[i] = new Vector3(Mathf.Cos(a) * .45f, 1.1f + Mathf.Sin(a) * .45f, .15f); }
            // rim from short spars: a looped, view-aligned LineRenderer smeared into a flat panel from the game camera
            for (int i = 0; i < 24; i++) Spar(helm, "Wheel rim", wheel[i], wheel[(i + 1) % 24], .06f, _gilt);
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI / 4; var dir = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0);
                Spar(helm, "Spoke", new Vector3(0, 1.1f, .15f), new Vector3(0, 1.1f, .15f) + dir * .6f, .045f, _oak);
            }
            Prim(helm, PrimitiveType.Sphere, "Wheel hub", new Vector3(0, 1.1f, .16f), Vector3.one * .12f, _gilt);
            Lantern(castle, "Poop lantern", new Vector3(-CastleHalf - .08f, CastleH + 1.25f, CastleZ1 + .08f), 7, 1.5f, 1.6f);
            Lantern(castle, "Poop lantern", new Vector3(CastleHalf + .08f, CastleH + 1.25f, CastleZ1 + .08f), 0, 0, 1.6f);
            Lantern(castle, "Stern lantern", new Vector3(-CastleHalf - .08f, CastleH + 1.25f, CastleZ0 - .08f), 6, 1.2f, 1.9f);
            Lantern(castle, "Stern lantern", new Vector3(CastleHalf + .08f, CastleH + 1.25f, CastleZ0 - .08f), 0, 0, 1.9f);
            Spar(castle, "Ensign staff", new Vector3(0, CastleH + .1f, Stern + .35f), ensignTop, .07f, _darkOak);
            var ensign = Part(castle, "Ensign", PlaneMesh("Ferry ensign", 0, -2.6f, -.42f, .42f), _pennant);
            ensign.transform.localPosition = ensignTop + new Vector3(0, -.45f, -.05f);
            ensign.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            // front of the castle: door, two gilded windows and a lantern
            Prim(castle, PrimitiveType.Cube, "Castle door", new Vector3(0, .95f, CastleZ1 + .01f), new Vector3(.8f, 1.8f, .05f), _darkOak);
            Prim(castle, PrimitiveType.Cube, "Door arch", new Vector3(0, 1.9f, CastleZ1 + .02f), new Vector3(1.0f, .12f, .05f), _gilt);
            foreach (int side in new[] { -1, 1 })
            {
                Prim(castle, PrimitiveType.Cube, "Front window", new Vector3(side * 1.05f, 1.3f, CastleZ1 + .015f), new Vector3(.5f, .62f, .03f), _glass, null, false);
                Prim(castle, PrimitiveType.Cube, "Front window frame", new Vector3(side * 1.05f, 1.3f, CastleZ1 + .01f), new Vector3(.62f, .74f, .03f), _gilt);
            }
            var glow = new GameObject("Gallery glow"); glow.transform.SetParent(castle, false); glow.transform.localPosition = new Vector3(-CastleHalf - 1.2f, 1.3f, czm);
            var gl = glow.AddComponent<Light>(); gl.type = LightType.Point; gl.color = new Color(1f, .68f, .38f); gl.range = 4.5f; gl.intensity = 1.1f; gl.shadows = LightShadows.None;
            Collider(castle, "Stern castle collider", new Vector3(0, 1.6f, czm), new Vector3(CastleHalf * 2 + .3f, 3.2f, cl + .3f));

            // benches along the bulwarks (not across the boarding opening)
            foreach (var (side, z0, z1) in new[] { (1, -5.2f, -3.7f), (1, -2.3f, 2.6f), (1, 3.6f, 6.6f), (-1, -5.2f, -1.9f), (-1, 1.9f, 4.4f) })
                Bench(t, side, z0, z1);
            // lantern posts on the rail (emissive only)
            foreach (int side in new[] { -1, 1 })
                foreach (float z in new[] { -4.3f, 3.0f, 7.0f })
                {
                    var p = new Vector3(side * DeckHalf(z), Sheer(z), z);
                    Spar(t, "Rail lantern post", p, p + Vector3.up * .6f, .07f, _darkOak);
                    Lantern(t, "Rail lantern", p + Vector3.up * .78f, 0, 0);
                }
            // bow lantern
            Spar(t, "Bow lantern post", new Vector3(0, Sheer(Bow - 1.2f), Bow - 1.2f), new Vector3(0, Sheer(Bow - 1.2f) + 1.0f, Bow - 1.2f), .09f, _darkOak);
            Lantern(t, "Bow lantern", new Vector3(0, Sheer(Bow - 1.2f) + .85f, Bow - 1.0f), 7, 1.6f, 1.3f);
            // a lantern hanging from the main boom over the waist
            Rope(t, "Deck lantern line", new[] { new Vector3(0, 3.7f, -1.4f), new Vector3(0, 2.75f, -1.4f) }, .025f);
            Lantern(t, "Deck lantern", new Vector3(0, 2.55f, -1.4f), 8, 1.3f, 1.2f);

            // boarding: gangway ramp down to the pier and a rope gate used while sailing
            var gang = new GameObject("Gangway").transform; gang.SetParent(t, false);
            var gTop = new Vector3(-DeckOuter(0) + .05f, 0, 0); var gBottom = new Vector3(-BerthOffset - .3f, -DeckAbovePier, 0);
            var along = (gBottom - gTop).normalized; float glen = Vector3.Distance(gTop, gBottom);
            var rotG = Quaternion.FromToRotation(Vector3.right, -along);
            var mid = (gTop + gBottom) / 2; var up = rotG * Vector3.up;
            Prim(gang, PrimitiveType.Cube, "Gangway plank", mid - up * .035f, new Vector3(glen + .1f, .07f, 1.2f), _honey, rotG);
            for (float s = -glen / 2 + .2f; s < glen / 2; s += .3f) Prim(gang, PrimitiveType.Cube, "Cleat", mid + rotG * new Vector3(s, .02f, 0), new Vector3(.05f, .03f, 1.1f), _darkOak, rotG);
            foreach (float z in new[] { -.62f, .62f })
            {
                var h0 = gTop + new Vector3(0, .9f, z); var h1 = gBottom + new Vector3(0, .9f, z);
                Line(gang, "Gangway hand rope", Sag(h0, h1, .08f, 6), .04f, _rope);
                Spar(gang, "Gangway stanchion", gBottom + new Vector3(.15f, 0, z), gBottom + new Vector3(.15f, .95f, z), .06f, _iron);
            }
            Collider(gang, "Gangway collider", mid - up * .15f, new Vector3(glen + .3f, .3f, 1.3f), rotG);
            var gate = new GameObject("Boarding gate").transform; gate.SetParent(t, false);
            float gxi = -DeckHalf(0);
            foreach (float z in new[] { GapZ0 + .08f, GapZ1 - .08f })
                Prim(gate, PrimitiveType.Cylinder, "Gate stanchion", new Vector3(gxi - .05f, .45f, z), new Vector3(.07f, .45f, .07f), _iron);
            Rope(gate, "Gate rope", Sag(new Vector3(gxi - .05f, .8f, GapZ0 + .08f), new Vector3(gxi - .05f, .8f, GapZ1 - .08f), .1f), .04f);
            Collider(gate, "Gate collider", new Vector3(gxi - .05f, 1.0f, 0), new Vector3(.15f, 2.6f, GapZ1 - GapZ0));
            gate.gameObject.SetActive(false);

            // cargo near the bow
            var crates = AssetDatabase.LoadAssetAtPath<GameObject>(Project + "Art/Square/Props/crates.prefab");
            if (crates)
                foreach (var (x, z) in new[] { (.9f, 7.9f), (-.8f, 8.3f) })
                {
                    var c = (GameObject)PrefabUtility.InstantiatePrefab(crates, t);
                    c.transform.localPosition = new Vector3(x, 0, z); c.transform.localScale = Vector3.one * .75f;
                    var blob = c.transform.Find("Blob"); if (blob) blob.gameObject.SetActive(false);
                    Collider(t, "Cargo collider", new Vector3(x, .4f, z), new Vector3(.85f, .8f, .85f));
                }

            return PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally { Object.DestroyImmediate(root); }
    }
}

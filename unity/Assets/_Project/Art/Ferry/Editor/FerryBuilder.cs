using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

/// <summary>
/// Builds the passenger ferry prefab (Art/Ferry/Ferry.prefab): a 12 m wooden sailing boat with a walkable deck.
/// Local frame: +Z = bow, +X = starboard, y 0 = deck surface, waterline at y -0.95.
/// The boarding opening is on the port side (-X) at z -1..1; "Gangway" and "Boarding gate" toggle it.
/// </summary>
public static class FerryBuilder
{
    private const string Art = "Assets/_Project/Art/Ferry/";
    private const string Project = "Assets/_Project/";
    public const string PrefabPath = Art + "Ferry.prefab";

    public const float Stern = -5.75f, Bow = 6.25f, MaxHalfBeam = 2.15f, Keel = -1.9f, Waterline = -.95f, Bulwark = .12f;
    public const float GapZ0 = -1f, GapZ1 = 1f;
    private const float Step = .25f;

    private static Material _hull, _deck, _oak, _darkOak, _honey, _teal, _rope, _iron, _glass, _sail, _pennant;

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
    public static float KeelY(float z) { float t = T(z); return t > .55f ? Keel + Mathf.Pow((t - .55f) / .45f, 2) * 2.2f : Keel; }
    public static float Sheer(float z) { float t = T(z); return t >= 0 ? .75f + .5f * t * t * t : .75f + .2f * t * t; }
    private static float KDeck(float z) => Mathf.Clamp01((0 - KeelY(z)) / (Sheer(z) - KeelY(z)));
    private static Vector3 HullPoint(float z, float k, int side)
    {
        float y = Mathf.Lerp(KeelY(z), Sheer(z), k);
        return new Vector3(side * HalfBeam(z) * Mathf.Pow(k, .45f), y, z);
    }
    /// <summary>Half-width of the walkable deck (inside the bulwark).</summary>
    public static float DeckHalf(float z) => Mathf.Max(0, HalfBeam(z) * Mathf.Pow(KDeck(z), .45f) - Bulwark);

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
        return zs;
    }
    private static bool InGap(float z0, float z1, int side) => side < 0 && z0 >= GapZ0 - .001f && z1 <= GapZ1 + .001f;

    /// <summary>Hull: sub 0 = painted outer hull, 1 = bulwark inside + transom, 2 = rail cap.</summary>
    private static Mesh HullMesh()
    {
        var mb = new MeshBuilder(3); var zs = Stations();
        const int below = 8, above = 4;
        foreach (int side in new[] { -1, 1 })
        {
            // outer skin: rings keel..deck (below) and deck..sheer (above)
            var grid = new int[zs.Count, below + above + 1];
            for (int i = 0; i < zs.Count; i++)
            {
                float z = zs[i], kd = KDeck(z), kw = Mathf.Clamp01((Waterline - KeelY(z)) / (Sheer(z) - KeelY(z)));
                for (int j = 0; j <= below + above; j++)
                {
                    float k = j <= below ? kd * j / below : Mathf.Lerp(kd, 1, (j - below) / (float)above);
                    float v = k < kw ? k / Mathf.Max(kw, .001f) * .17f : .17f + (k - kw) / Mathf.Max(1 - kw, .001f) * .83f;
                    grid[i, j] = mb.Add(HullPoint(z, k, side), new Vector2(side * z / 10.6f, v));
                }
            }
            for (int i = 0; i + 1 < zs.Count; i++)
                for (int j = 0; j < below + above; j++)
                {
                    if (j >= below && InGap(zs[i], zs[i + 1], side)) continue;
                    mb.Quad(0, grid[i, j], grid[i + 1, j], grid[i + 1, j + 1], grid[i, j + 1], new Vector3(side, 0, 0));
                }
            // inside of the bulwark (deck .. sheer) and rail cap
            for (int i = 0; i + 1 < zs.Count; i++)
            {
                if (InGap(zs[i], zs[i + 1], side)) continue;
                float za = zs[i], zb = zs[i + 1];
                if (DeckHalf(za) <= 0 && DeckHalf(zb) <= 0) continue;
                Vector3 Ia(float z, float y) => new(side * (DeckHalf(z)), y, z);
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
            var outer = HullPoint(z, 1, -1); var inner = new Vector3(-DeckHalf(z), 0, z);
            var deckOuter = HullPoint(z, KDeck(z), -1);
            int a = mb.Add(deckOuter, new Vector2(0, 0)), b = mb.Add(new Vector3(inner.x, 0, z), new Vector2(.12f, 0));
            int c = mb.Add(new Vector3(inner.x, outer.y, z), new Vector2(.12f, .4f)), d = mb.Add(outer, new Vector2(0, .4f));
            var hint = new Vector3(0, 0, z < 0 ? 1 : -1);
            mb.Quad(1, a, b, c, d, hint);
            // and a cap on the opening's deck edge so the hull thickness reads
            int e = mb.Add(deckOuter + Vector3.up * .001f, Vector2.zero);
        }
        // transom: outer face (double sided) + inside face above the deck
        {
            float z = Stern; var ring = new List<int>(); var ringBack = new List<int>();
            var pts = new List<Vector3>();
            for (int j = 0; j <= 12; j++) pts.Add(HullPoint(z, 1 - j / 12f, -1));
            for (int j = 1; j <= 12; j++) pts.Add(HullPoint(z, j / 12f, 1));
            var centre = new Vector3(0, (KeelY(z) + Sheer(z)) / 2, z);
            int ci = mb.Add(centre, new Vector2(0, .5f)), cb = mb.Add(centre, new Vector2(0, .5f));
            foreach (var p in pts) { ring.Add(mb.Add(p, new Vector2(p.x / 2, p.y / 2))); ringBack.Add(mb.Add(p, new Vector2(p.x / 2, p.y / 2))); }
            for (int i = 0; i + 1 < pts.Count; i++)
            {
                mb.Tri(1, ci, ring[i], ring[i + 1], Vector3.back);
                mb.Tri(1, cb, ringBack[i], ringBack[i + 1], Vector3.forward);
            }
            float h = DeckHalf(z + Bulwark), zi = z + Bulwark;
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
        int s0 = mb.Add(new Vector3(-DeckHalf(GapZ0), 0, GapZ0), new Vector2(0, 0)), s1 = mb.Add(new Vector3(-DeckHalf(GapZ1), 0, GapZ1), new Vector2(0, 1));
        int s2 = mb.Add(new Vector3(HullPoint(GapZ1, KDeck(GapZ1), -1).x, 0, GapZ1), new Vector2(.1f, 1)), s3 = mb.Add(new Vector3(HullPoint(GapZ0, KDeck(GapZ0), -1).x, 0, GapZ0), new Vector2(.1f, 0));
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

    private static Mesh SailMesh()
    {
        // gaff sail in the boat's centre plane: tack, clew, peak, throat
        Vector3 tack = new(0, 2.4f, 2.1f), clew = new(0, 2.4f, -2.35f), peak = new(0, 7.9f, -2.0f), throat = new(0, 6.55f, 2.1f);
        var mb = new MeshBuilder(1); const int n = 10; var idx = new int[n + 1, n + 1];
        for (int i = 0; i <= n; i++) for (int j = 0; j <= n; j++)
        {
            float u = i / (float)n, v = j / (float)n;
            var p = Vector3.Lerp(Vector3.Lerp(tack, clew, u), Vector3.Lerp(throat, peak, u), v);
            p.x = -.38f * Mathf.Sin(Mathf.PI * u) * Mathf.Sin(Mathf.PI * Mathf.Clamp01(v * .9f + .1f));
            idx[i, j] = mb.Add(p, new Vector2(u, v));
        }
        for (int i = 0; i < n; i++) for (int j = 0; j < n; j++) mb.Quad(0, idx[i, j], idx[i + 1, j], idx[i + 1, j + 1], idx[i, j + 1], Vector3.left);
        return mb.Save("Ferry sail");
    }

    private static Mesh PennantMesh()
    {
        var mb = new MeshBuilder(1);
        int a = mb.Add(new Vector3(0, -.2f, 0), new Vector2(0, 0)), b = mb.Add(new Vector3(0, -.2f, -1.6f), new Vector2(1, 0));
        int c = mb.Add(new Vector3(0, .2f, -1.6f), new Vector2(1, 1)), d = mb.Add(new Vector3(0, .2f, 0), new Vector2(0, 1));
        mb.Quad(0, a, b, c, d, Vector3.left);
        return mb.Save("Ferry pennant");
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
    private static Material Lit(string name, string tex, Color tint, float smooth, bool clamp = false, bool twoSided = false)
    {
        string path = Art + "Materials/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!m) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name }; AssetDatabase.CreateAsset(m, path); }
        if (tex != null) m.SetTexture("_BaseMap", Tex(tex, false, clamp));
        m.SetColor("_BaseColor", tint); m.SetFloat("_Smoothness", smooth);
        if (twoSided) { m.SetFloat("_Cull", 0); m.doubleSidedGI = true; }
        EditorUtility.SetDirty(m); return m;
    }
    private static void LoadMaterials()
    {
        _hull = Lit("Ferry hull", "FerryHull.png", Color.white, .18f);
        _deck = Lit("Ferry deck", "FerryDeck.png", new Color(.86f, .8f, .74f), .08f);
        _sail = Lit("Ferry sail", "FerrySail.png", Color.white, 0, true, true);
        _oak = AssetDatabase.LoadAssetAtPath<Material>(Project + "Art/AcademyGarden/Materials/Weathered oak.mat");
        _darkOak = AssetDatabase.LoadAssetAtPath<Material>(Project + "Art/AcademyGarden/Materials/Oak end grain.mat");
        _honey = AssetDatabase.LoadAssetAtPath<Material>(Project + "Art/CanalLanding/Materials/Honey oak.mat");
        _teal = AssetDatabase.LoadAssetAtPath<Material>(Project + "Art/CanalLanding/Materials/Painted teal hull.mat");
        _rope = AssetDatabase.LoadAssetAtPath<Material>(Project + "Art/CanalLanding/Materials/Hemp rope.mat");
        _iron = AssetDatabase.LoadAssetAtPath<Material>(Project + "Art/CanalLanding/Materials/Dark iron.mat");
        string gp = Art + "Materials/Ferry lantern glass.mat";
        _glass = AssetDatabase.LoadAssetAtPath<Material>(gp);
        if (!_glass) { _glass = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Ferry lantern glass" }; AssetDatabase.CreateAsset(_glass, gp); }
        _glass.SetColor("_BaseColor", new Color(1f, .7f, .32f)); _glass.EnableKeyword("_EMISSION");
        _glass.SetColor("_EmissionColor", new Color(1f, .5f, .12f) * 2.2f);
        _glass.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive; // keeps _EMISSION in builds
        EditorUtility.SetDirty(_glass);
        string pp = Art + "Materials/Ferry pennant.mat";
        _pennant = AssetDatabase.LoadAssetAtPath<Material>(pp);
        if (!_pennant) { _pennant = new Material(AssetDatabase.LoadAssetAtPath<Material>(Project + "Materials/Garden/shrub.mat")) { name = "Ferry pennant" }; AssetDatabase.CreateAsset(_pennant, pp); }
        _pennant.mainTexture = Tex("FerryPennant.png", true, true); _pennant.color = Color.white; _pennant.SetFloat("_Cutoff", .4f); _pennant.SetFloat("_Cull", 0);
        _pennant.DisableKeyword("_EMISSION"); EditorUtility.SetDirty(_pennant);
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
    private static GameObject Beam(Transform parent, string name, Vector3 a, Vector3 b, float thickness, Material mat)
    {
        var g = Prim(parent, PrimitiveType.Cylinder, name, (a + b) / 2, new Vector3(thickness, Vector3.Distance(a, b) / 2, thickness), mat,
            Quaternion.FromToRotation(Vector3.up, (b - a).normalized));
        return g;
    }
    private static void Rope(Transform parent, string name, Vector3[] pts, float width)
    {
        var g = new GameObject(name); g.transform.SetParent(parent, false);
        var l = g.AddComponent<LineRenderer>(); l.useWorldSpace = false; l.positionCount = pts.Length; l.SetPositions(pts);
        l.startWidth = l.endWidth = width; l.numCornerVertices = 2; l.numCapVertices = 2; l.sharedMaterial = _rope;
        l.shadowCastingMode = ShadowCastingMode.Off;
    }
    private static Vector3[] Sag(Vector3 a, Vector3 b, float sag, int n = 8)
    {
        var p = new Vector3[n + 1];
        for (int i = 0; i <= n; i++) { float t = i / (float)n; p[i] = Vector3.Lerp(a, b, t) + Vector3.down * sag * 4 * t * (1 - t); }
        return p;
    }
    private static void Lantern(Transform parent, string name, Vector3 pos, float range, float intensity)
    {
        var root = new GameObject(name).transform; root.SetParent(parent, false); root.localPosition = pos;
        Prim(root, PrimitiveType.Cube, "Lantern cap", new Vector3(0, .17f, 0), new Vector3(.24f, .06f, .24f), _iron);
        Prim(root, PrimitiveType.Cube, "Lantern glass", Vector3.zero, new Vector3(.16f, .26f, .16f), _glass, null, false);
        Prim(root, PrimitiveType.Cube, "Lantern base", new Vector3(0, -.15f, 0), new Vector3(.2f, .05f, .2f), _iron);
        var lg = new GameObject("Lantern light"); lg.transform.SetParent(root, false);
        var l = lg.AddComponent<Light>(); l.type = LightType.Point; l.color = new Color(1f, .72f, .42f); l.range = range; l.intensity = intensity; l.shadows = LightShadows.None;
    }
    private static void BoxCollider(Transform parent, string name, Vector3 centre, Vector3 size)
    {
        var g = new GameObject(name); g.transform.SetParent(parent, false); g.transform.localPosition = centre;
        g.AddComponent<BoxCollider>().size = size;
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
            var deck = Part(t, "Deck", DeckMesh(), _deck);
            deck.AddComponent<MeshCollider>().sharedMesh = deck.GetComponent<MeshFilter>().sharedMesh;
            var walls = new GameObject("Invisible deck walls"); walls.transform.SetParent(t, false);
            walls.AddComponent<MeshCollider>().sharedMesh = WallMesh();

            // keel strip + stem post
            var stem = new List<Vector3>();
            for (float z = 3.5f; z <= Bow + .001f; z += .25f) stem.Add(new Vector3(0, KeelY(z), z));
            stem.Add(new Vector3(0, Sheer(Bow) + .25f, Bow + .05f));
            for (int i = 0; i + 1 < stem.Count; i++) Beam(t, "Stem", stem[i], stem[i + 1], .16f, _darkOak);
            // bowsprit
            Beam(t, "Bowsprit", new Vector3(0, Sheer(Bow) - .1f, Bow - 1.2f), new Vector3(0, Sheer(Bow) + .55f, Bow + 1.7f), .14f, _oak);

            // fenders along the port side (boarding side)
            foreach (float z in new[] { -3.2f, -1.4f, 1.4f, 3.0f })
            {
                var top = HullPoint(z, 1, -1);
                var fender = Prim(t, PrimitiveType.Capsule, "Rope fender", new Vector3(top.x - .14f, -.35f, z), new Vector3(.26f, .3f, .26f), _rope);
                Rope(t, "Fender line", new[] { top + Vector3.up * .02f, new Vector3(top.x - .14f, -.08f, z) }, .025f);
            }

            // mast, boom, gaff, sail, pennant
            var rig = new GameObject("Rig").transform; rig.SetParent(t, false);
            const float mastZ = 2.3f, mastTop = 8.6f;
            Beam(rig, "Mast", new Vector3(0, 0, mastZ), new Vector3(0, mastTop, mastZ), .22f, _oak);
            Prim(rig, PrimitiveType.Cylinder, "Mast step", new Vector3(0, .1f, mastZ), new Vector3(.42f, .1f, .42f), _darkOak);
            Beam(rig, "Boom", new Vector3(0, 2.3f, mastZ - .05f), new Vector3(0, 2.3f, -2.55f), .13f, _oak);
            Beam(rig, "Gaff", new Vector3(0, 6.5f, mastZ - .05f), new Vector3(0, 8.0f, -2.15f), .11f, _oak);
            var sail = Part(rig, "Sail", SailMesh(), _sail);
            sail.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.TwoSided;
            sail.AddComponent<Occluder>();
            var pennant = Part(rig, "Pennant", PennantMesh(), _pennant);
            pennant.transform.localPosition = new Vector3(0, mastTop - .1f, mastZ - .1f);
            pennant.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            Prim(rig, PrimitiveType.Sphere, "Mast truck", new Vector3(0, mastTop + .05f, mastZ), Vector3.one * .2f, _darkOak);
            var bowTip = new Vector3(0, Sheer(Bow) + .55f, Bow + 1.7f);
            Rope(rig, "Forestay", new[] { new Vector3(0, mastTop - .3f, mastZ), bowTip }, .035f);
            foreach (int side in new[] { -1, 1 })
            {
                var chain = HullPoint(mastZ - .4f, 1, side);
                Rope(rig, "Shroud", new[] { new Vector3(0, mastTop - .5f, mastZ), chain }, .03f);
                Rope(rig, "Shroud", new[] { new Vector3(0, mastTop - .5f, mastZ), HullPoint(mastZ - 1.2f, 1, side) }, .03f);
            }
            Rope(rig, "Backstay", new[] { new Vector3(0, mastTop - .3f, mastZ), new Vector3(0, Sheer(Stern) + .1f, Stern + .1f) }, .03f);
            Rope(rig, "Peak halyard", new[] { new Vector3(0, mastTop - .2f, mastZ), new Vector3(0, 8.0f, -2.1f) }, .025f);
            // string of lanterns from the bowsprit to the masthead
            var lights = new GameObject("Festoon lights").transform; lights.SetParent(rig, false);
            var festoon = Sag(bowTip, new Vector3(0, mastTop - .9f, mastZ), .35f, 10);
            for (int i = 1; i < festoon.Length - 1; i += 1)
                Prim(lights, PrimitiveType.Sphere, "Festoon bulb", festoon[i] + Vector3.down * .08f, Vector3.one * .1f, _glass, null, false);

            // deckhouse at the stern
            var cabin = new GameObject("Deckhouse").transform; cabin.SetParent(t, false);
            const float cz0 = -5.35f, cz1 = -3.2f, cw = 1.08f, ch = 1.9f;
            float czm = (cz0 + cz1) / 2, cl = cz1 - cz0;
            Prim(cabin, PrimitiveType.Cube, "Cabin walls", new Vector3(0, ch / 2, czm), new Vector3(cw * 2, ch, cl), _honey);
            foreach (int side in new[] { -1, 1 })
            {
                foreach (float z in new[] { cz0 + .05f, cz1 - .05f })
                    Prim(cabin, PrimitiveType.Cube, "Corner post", new Vector3(side * (cw + .02f), ch / 2, z), new Vector3(.12f, ch + .02f, .12f), _darkOak);
                Prim(cabin, PrimitiveType.Cube, "Wall plate", new Vector3(side * (cw + .02f), ch - .05f, czm), new Vector3(.1f, .1f, cl), _darkOak);
                // gabled roof halves
                var rot = Quaternion.Euler(0, 0, side * -16f);
                Prim(cabin, PrimitiveType.Cube, "Roof", new Vector3(side * .62f, ch + .2f, czm), new Vector3(1.42f, .1f, cl + .5f), _teal, rot);
            }
            Prim(cabin, PrimitiveType.Cube, "Ridge", new Vector3(0, ch + .4f, czm), new Vector3(.14f, .1f, cl + .55f), _darkOak);
            // windows: three on the port side (facing the camera), one aft
            for (int i = 0; i < 3; i++)
            {
                float z = cz0 + .4f + i * .68f;
                Prim(cabin, PrimitiveType.Cube, "Window", new Vector3(-cw - .01f, 1.15f, z), new Vector3(.04f, .42f, .36f), _glass, null, false);
                Prim(cabin, PrimitiveType.Cube, "Window frame", new Vector3(-cw - .025f, 1.15f, z), new Vector3(.03f, .5f, .44f), _darkOak);
                Prim(cabin, PrimitiveType.Cube, "Window bar", new Vector3(-cw - .04f, 1.15f, z), new Vector3(.02f, .42f, .04f), _darkOak);
            }
            Prim(cabin, PrimitiveType.Cube, "Starboard window", new Vector3(cw + .01f, 1.15f, czm), new Vector3(.04f, .42f, .5f), _glass, null, false);
            Prim(cabin, PrimitiveType.Cube, "Door", new Vector3(.3f, .85f, cz1 + .01f), new Vector3(.62f, 1.6f, .04f), _darkOak);
            Prim(cabin, PrimitiveType.Cube, "Door window", new Vector3(.3f, 1.25f, cz1 + .035f), new Vector3(.26f, .3f, .02f), _glass, null, false);
            Lantern(cabin, "Cabin lantern", new Vector3(-.55f, 1.55f, cz1 + .14f), 5.5f, 1.4f);
            BoxCollider(cabin, "Deckhouse collider", new Vector3(0, 1.2f, czm), new Vector3(cw * 2 + .1f, 2.4f, cl + .1f));
            // ship's wheel in front of the deckhouse
            var helm = new GameObject("Helm").transform; helm.SetParent(t, false); helm.localPosition = new Vector3(-.5f, 0, cz1 - .45f);
            Prim(helm, PrimitiveType.Cube, "Helm stand", new Vector3(0, .5f, 0), new Vector3(.2f, 1f, .2f), _darkOak);
            var wheel = new Vector3[25];
            for (int i = 0; i < 25; i++) { float a = i * Mathf.PI * 2 / 24; wheel[i] = new Vector3(Mathf.Cos(a) * .38f, 1.05f + Mathf.Sin(a) * .38f, .14f); }
            var wl = new GameObject("Wheel rim"); wl.transform.SetParent(helm, false);
            var lr = wl.AddComponent<LineRenderer>(); lr.useWorldSpace = false; lr.loop = true; lr.positionCount = 24; lr.SetPositions(wheel[..24]);
            lr.startWidth = lr.endWidth = .05f; lr.sharedMaterial = _darkOak;
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI / 4; var dir = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0);
                Beam(helm, "Spoke", new Vector3(0, 1.05f, .14f), new Vector3(0, 1.05f, .14f) + dir * .5f, .04f, _oak);
            }
            BoxCollider(helm, "Helm collider", new Vector3(0, .6f, 0), new Vector3(.4f, 1.2f, .4f));

            // benches along the bulwarks (not across the boarding opening)
            foreach (var (side, z0, z1) in new[] { (1, -2.6f, .9f), (1, 1.6f, 3.6f), (-1, 1.5f, 3.4f), (-1, -2.8f, -1.35f) })
            {
                float x = side * (DeckHalf((z0 + z1) / 2) - .24f), zc = (z0 + z1) / 2, len = z1 - z0;
                Prim(t, PrimitiveType.Cube, "Bench seat", new Vector3(x, .45f, zc), new Vector3(.44f, .07f, len), _honey);
                for (float z = z0 + .2f; z < z1; z += .8f) Prim(t, PrimitiveType.Cube, "Bench leg", new Vector3(x, .21f, z), new Vector3(.36f, .42f, .07f), _darkOak);
                BoxCollider(t, "Bench collider", new Vector3(x, .25f, zc), new Vector3(.44f, .5f, len));
            }
            var mastCol = new GameObject("Mast collider"); mastCol.transform.SetParent(rig, false); mastCol.transform.localPosition = new Vector3(0, 1.5f, mastZ);
            var mc = mastCol.AddComponent<CapsuleCollider>(); mc.radius = .25f; mc.height = 3; mc.direction = 1;

            // bow lantern and stern lanterns
            Beam(t, "Bow lantern post", new Vector3(0, Sheer(Bow - .6f), Bow - .6f), new Vector3(0, Sheer(Bow - .6f) + .9f, Bow - .6f), .08f, _darkOak);
            Lantern(t, "Bow lantern", new Vector3(0, Sheer(Bow - .6f) + .75f, Bow - .45f), 6, 1.6f);
            foreach (int side in new[] { -1, 1 })
            {
                var p = new Vector3(side * (DeckHalf(Stern + .3f) + .02f), Sheer(Stern + .3f), Stern + .3f);
                Beam(t, "Stern lantern post", p, p + Vector3.up * .7f, .07f, _darkOak);
                Lantern(t, "Stern lantern", p + Vector3.up * .85f, 5, 1.1f);
            }

            // boarding: gangway plank (out to the pier) and a rope gate used while sailing
            var gang = new GameObject("Gangway").transform; gang.SetParent(t, false);
            float gx0 = HullPoint(0, KDeck(0), -1).x, gx1 = gx0 - 1.25f;
            gang.localPosition = new Vector3((gx0 + gx1) / 2, -.04f, 0);
            Prim(gang, PrimitiveType.Cube, "Gangway plank", Vector3.zero, new Vector3(gx0 - gx1 + .3f, .07f, 1.15f), _honey);
            for (float z = -.45f; z <= .46f; z += .3f) Prim(gang, PrimitiveType.Cube, "Cleat", new Vector3(0, .04f, z), new Vector3(gx0 - gx1, .03f, .05f), _darkOak);
            var gc = new GameObject("Gangway collider"); gc.transform.SetParent(gang, false); gc.transform.localPosition = new Vector3(0, -.11f, 0);
            gc.AddComponent<BoxCollider>().size = new Vector3(gx0 - gx1 + .4f, .3f, 1.3f);
            var gate = new GameObject("Boarding gate").transform; gate.SetParent(t, false);
            float gxi = -DeckHalf(0);
            foreach (float z in new[] { GapZ0 + .08f, GapZ1 - .08f })
                Prim(gate, PrimitiveType.Cylinder, "Gate stanchion", new Vector3(gxi - .05f, .45f, z), new Vector3(.07f, .45f, .07f), _iron);
            Rope(gate, "Gate rope", Sag(new Vector3(gxi - .05f, .8f, GapZ0 + .08f), new Vector3(gxi - .05f, .8f, GapZ1 - .08f), .1f), .04f);
            BoxCollider(gate, "Gate collider", new Vector3(gxi - .05f, 1.0f, 0), new Vector3(.15f, 2.6f, GapZ1 - GapZ0));
            gate.gameObject.SetActive(false);

            // a little cargo near the bow
            var crates = AssetDatabase.LoadAssetAtPath<GameObject>(Project + "Art/Square/Props/crates.prefab");
            if (crates)
            {
                var c = (GameObject)PrefabUtility.InstantiatePrefab(crates, t);
                c.transform.localPosition = new Vector3(.75f, 0, 4.3f); c.transform.localScale = Vector3.one * .7f;
                var blob = c.transform.Find("Blob"); if (blob) blob.gameObject.SetActive(false);
                BoxCollider(t, "Cargo collider", new Vector3(.75f, .4f, 4.3f), new Vector3(.8f, .8f, .8f));
            }

            hull.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.TwoSided;
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            return prefab;
        }
        finally { Object.DestroyImmediate(root); }
    }
}

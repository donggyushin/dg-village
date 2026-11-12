using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

/// <summary>
/// Sacred isle north-west of the Academy north garden, plus a ramp, curved beach and pier on the garden side.
/// Terrain = height-field meshes (grass / sand / trail / cliff sub-meshes). Re-runnable: rebuilds group 21.
/// </summary>
public static class SacredIsleBuilder
{
    private const string Art = "Assets/_Project/Art/SacredIsle/";
    private const string Project = "Assets/_Project/";
    private const string GroupName = "21 • Sacred isle";
    private const float WaterY = -1.25f, Floor = -2.8f, GardenTop = 2.74f, BeachY = -0.42f, IsleTop = 4.6f, Cell = .3f;
    private const float DeckY = -.3f;

    // Garden edge (same formula as AcademyGardenBuilder.Edge).
    private static readonly Vector2 GardenCentre = new(-1.62f, 21.055f);
    // Mainland ramp along the garden coast, parameter a of the garden edge.
    private const float ALand0 = 1.70f, ALand1 = 1.95f, ARampEnd = 2.62f, RampW = 3.4f;
    private const float GapA0 = 1.72f, GapA1 = 1.94f;
    private static readonly Vector2 BeachC = new(-21.3f, 33.2f);
    // Island.
    // 2026-11-12: moved far out (88 m from the garden beach, straight screen-left) so the ferry ride means something.
    private const float IsleDistance = 88f;
    private static readonly Vector2 IsleC = BeachC + new Vector2(-1, 1).normalized * IsleDistance;
    private static readonly Vector2 ToIsle = (IsleC - BeachC).normalized;
    private static float ThetaDock => Mathf.Atan2(-ToIsle.y, -ToIsle.x);
    private static float Theta0 => ThetaDock - .32f;      // ramp foot
    private const float RampSpan = 1.7f;                   // radians climbed clockwise
    private const float IsleBandIn = .6f, IsleBandOut = 3.6f;
    // Piers and ferry berths. The ferry lies on the north-east (starboard-of-route) side of both piers, so it sails
    // a straight line between the berths; its port side opening meets each pier at distance GangT from the pier start.
    private const float PierLen = 12f, GangT = 9f, BoatOff = 3.5f;
    private static Vector2 RouteRight => new(ToIsle.y, -ToIsle.x);
    private static Vector2[] _berths = Array.Empty<Vector2>();

    private static readonly List<string> Log = new();
    private static (Vector2 start, Vector2 dir, float length)[] _docks = Array.Empty<(Vector2, Vector2, float)>();
    private static Vector2[] _gardenTable;
    private static float[] _gardenA;

    [MenuItem("DG Village/Waterfront/Sacred isle")]
    public static void Menu() => Debug.Log(Build());

    public static string Build()
    {
        Log.Clear();
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (EditorApplication.isPlaying || scene.path != "Assets/_Project/Scenes/SquareScene.unity")
            throw new InvalidOperationException("Open SquareScene in Edit mode.");
        foreach (var f in new[] { "Meshes", "Materials", "Textures" }) Directory.CreateDirectory(Art + f);
        AssetDatabase.Refresh();

        var village = GameObject.Find("Willow Court").transform;
        var old = village.Find(GroupName);
        if (old) Object.DestroyImmediate(old.gameObject);
        var group = new GameObject(GroupName).transform;
        group.SetParent(village, false);
        group.position = Vector3.zero;

        BuildGardenTable();
        // Docks first: the terrain raises the sand under each pier's landward end.
        ComputeDocks();
        EnsureSea(village);

        var surfaces = new List<Collider>();
        var mats = Materials();
        var mainland = new Field("Garden beach & ramp", new Rect(-34, 23, 32, 27), MainlandHeight, MainlandTrail, MainlandMask);
        var isle = new Field("Sacred isle", new Rect(IsleC.x - 21, IsleC.y - 21, 42, 42), IsleHeight, IsleTrail, _ => 1);
        foreach (var field in new[] { mainland, isle })
        {
            field.Sample();
            surfaces.Add(field.BuildMesh(group, mats));
            field.BuildWalls(group);
        }

        OpenFence(village);
        Railing(group, "Ramp railing", MainlandRail(), MainlandHeight);
        Railing(group, "Isle path railing", IsleRail(), IsleHeight);
        var piers = new GameObject("Piers").transform; piers.SetParent(group, false);
        surfaces.Add(Dock(piers, "Garden pier", _docks[0], +1));
        surfaces.Add(Dock(piers, "Isle pier", _docks[1], -1));
        surfaces.Add(Ferry(group));
        Dressing(group);
        ShoreMap(village, new[] { mainland, isle });

        var placement = village.GetComponent<SquareSpawnPlacement>();
        if (placement)
        {
            var so = new SerializedObject(placement); var arr = so.FindProperty("_surfaces");
            var list = new List<Object>();
            for (int i = 0; i < arr.arraySize; i++) { var o = arr.GetArrayElementAtIndex(i).objectReferenceValue; if (o) list.Add(o); }
            list.AddRange(surfaces);
            arr.arraySize = list.Count;
            for (int i = 0; i < list.Count; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = list[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            Log.Add($"spawn surfaces: {list.Count}");
        }
        Physics.SyncTransforms();
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return "Sacred isle built.\n" + string.Join("\n", Log);
    }

    /// <summary>
    /// Walks a player-like Rigidbody capsule (r .28, h 1.6, 4 m/s, like PlayerMovement) along the routes in an
    /// isolated preview physics scene built from copies of the real colliders. Also pushes it at the walls.
    /// </summary>
    public static string Validate()
    {
        BuildGardenTable(); ComputeDocks();
        var village = GameObject.Find("Willow Court").transform;
        var sources = village.Find(GroupName).GetComponentsInChildren<Collider>().ToList();
        var garden = village.Find("18 • Academy north garden");
        sources.AddRange(garden.GetComponentsInChildren<Collider>());
        var scene = EditorSceneManager.NewPreviewScene(); var physics = scene.GetPhysicsScene();
        var report = new List<string>();
        try
        {
            foreach (var s in sources)
            {
                var g = new GameObject("copy"); UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(g, scene);
                g.transform.SetPositionAndRotation(s.transform.position, s.transform.rotation); g.transform.localScale = s.transform.lossyScale;
                switch (s)
                {
                    case MeshCollider m: g.AddComponent<MeshCollider>().sharedMesh = m.sharedMesh; break;
                    case BoxCollider b: { var c = g.AddComponent<BoxCollider>(); c.center = b.center; c.size = b.size; break; }
                    case CapsuleCollider k: { var c = g.AddComponent<CapsuleCollider>(); c.center = k.center; c.radius = k.radius; c.height = k.height; c.direction = k.direction; break; }
                }
            }
            var body = new GameObject("player"); UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(body, scene);
            var cap = body.AddComponent<CapsuleCollider>(); cap.radius = .28f; cap.height = 1.6f; cap.center = Vector3.up * .8f;
            var rb = body.AddComponent<Rigidbody>(); rb.constraints = RigidbodyConstraints.FreezeRotation; rb.interpolation = RigidbodyInterpolation.None;

            string Walk(string name, List<Vector2> route, Vector3 start)
            {
                rb.position = start + Vector3.up * .3f; rb.linearVelocity = Vector3.zero; body.transform.position = rb.position;
                int wp = 0, frames = 0; float minY = 99, maxY = -99;
                while (wp < route.Count && frames < 3000)
                {
                    var to = route[wp] - new Vector2(rb.position.x, rb.position.z);
                    if (to.magnitude < .45f) { wp++; continue; }
                    var dir = to.normalized; var v = new Vector3(dir.x, 0, dir.y) * 4f; v.y = rb.linearVelocity.y; rb.linearVelocity = v;
                    physics.Simulate(.02f); frames++;
                    minY = Mathf.Min(minY, rb.position.y); maxY = Mathf.Max(maxY, rb.position.y);
                    if (rb.position.y < WaterY - .5f) break;
                }
                return $"{name}: reached {wp}/{route.Count} waypoints in {frames * .02f:0.0}s, y {minY:0.00}..{maxY:0.00}, end {rb.position}";
            }
            Vector2 G(float a, float s) { var (e, n) = EdgeAndNormal(a); return e + n * s; }
            // Mainland: lawn -> landing -> ramp -> beach -> pier end.
            var main = new List<Vector2> { G(1.83f, .5f), G(1.83f, 1.9f) };
            for (float a = 1.95f; a <= ARampEnd; a += .05f) main.Add(G(a, 2.0f));
            main.Add(_docks[0].start - _docks[0].dir * 1.5f);
            main.Add(_docks[0].start + _docks[0].dir * (_docks[0].length - .6f));
            // back to the gangway, across onto the ferry, to the bow and back towards the deckhouse
            main.Add(_docks[0].start + _docks[0].dir * GangT);
            main.Add(_berths[0]);
            main.Add(_berths[0] + RouteRight * .9f + ToIsle * 1.2f);   // pass the mast on the starboard side
            main.Add(_berths[0] + RouteRight * .9f + ToIsle * 3.4f);
            main.Add(_berths[0] + ToIsle * 4f);
            main.Add(_berths[0] + RouteRight * .9f + ToIsle * 2.8f);
            main.Add(_berths[0] + RouteRight * .9f + ToIsle * 1.0f);
            main.Add(_berths[0] - ToIsle * 2f);
            var lawn = G(1.83f, -2.5f);
            report.Add(Walk("garden -> pier end", main, new Vector3(lawn.x, GardenTop, lawn.y)));
            // Island: pier end -> beach -> ramp -> tree.
            var isle = new List<Vector2> { _docks[1].start - _docks[1].dir * 1.2f };
            float mid = (IsleBandIn + IsleBandOut) / 2;
            for (float u = -.02f; u <= 1.06f; u += .04f) { float th = Theta0 - u * RampSpan; isle.Add(IsleC + Dir(th) * (IsleRadius(th) - mid)); }
            float headA = Theta0 - RampSpan; isle.Add(IsleC + Dir(headA) * 3.2f);
            var pierEnd = _docks[1].start + _docks[1].dir * (_docks[1].length - .6f);
            report.Add(Walk("isle pier -> tree", isle, new Vector3(pierEnd.x, DeckY + .1f, pierEnd.y)));
            // Wall pushes: from walkable points straight outward; must not end in the water or off the cliff.
            int pushes = 0, escapes = 0;
            void Push(Vector2 from, Vector2 dir, float minEndY)
            {
                rb.position = new Vector3(0, -500, 0); body.transform.position = rb.position; Physics.SyncTransforms(); rb.linearVelocity = Vector3.zero;
                var probe = new Vector3(from.x, 30, from.y);
                if (physics.Raycast(probe, Vector3.down, out var hit, 40)) rb.position = hit.point + Vector3.up * .05f;
                float startY = rb.position.y;
                for (int f = 0; f < 150; f++) { var v = new Vector3(dir.x, 0, dir.y) * 4; v.y = rb.linearVelocity.y; rb.linearVelocity = v; physics.Simulate(.02f); }
                pushes++; if (rb.position.y < minEndY) { escapes++; if (escapes <= 40) report.Add($"  escape from {from} dir {dir} start y {startY:0.00} -> {rb.position} (min {minEndY:0.00})"); }
            }
            for (float u = .1f; u <= 1f; u += .1f) { float th = Theta0 - u * RampSpan; Push(IsleC + Dir(th) * (IsleRadius(th) - mid), Dir(th), IsleRampHeight(u) - 1.0f); }
            for (float th = -3.1f; th < 3.1f; th += .4f) if (Mathf.Abs(Wrap(th - (Theta0 - RampSpan))) > .6f) Push(IsleC + Dir(th) * 5f, Dir(th), IsleTop - .5f);
            for (float a = 1.97f; a < ARampEnd - .05f; a += .06f) { var (_, n) = EdgeAndNormal(a); Push(G(a, 2f), n, GardenRampHeight(a) - 1.0f); }
            for (float t = 1; t < 6; t += 1) foreach (int side in new[] { -1, 1 })
            {
                var d = _docks[0]; var right = new Vector2(d.dir.y, -d.dir.x) * side;
                Push(d.start + d.dir * t, right, DeckY - .5f);
            }
            for (float th = 0; th < 6.28f; th += .5f) Push(BeachC, Dir(th), WaterY - .2f);
            // ferry deck: starboard, bow, stern corners, and the port side away from the opening
            foreach (float a in new[] { -2.5f, 0f, 2.5f }) Push(_berths[0] + ToIsle * a, RouteRight, DeckY - .5f);
            Push(_berths[0], ToIsle, DeckY - .5f);
            Push(_berths[0] + ToIsle * 3f, -RouteRight, DeckY - .5f);
            Push(_berths[0] - ToIsle * 2.2f, -RouteRight, DeckY - .5f);
            // pier side away from the ferry
            for (float t = 2; t < PierLen; t += 2) Push(_docks[0].start + _docks[0].dir * t, -RouteRight, DeckY - .5f);
            report.Add($"wall pushes: {pushes}, escapes: {escapes}");
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
        return string.Join("\n", report);
    }

    private static void ComputeDocks()
    {
        _docks = Array.Empty<(Vector2, Vector2, float)>(); _berths = Array.Empty<Vector2>();
        var mainStart = March(BeachC, ToIsle, MainlandHeight) - ToIsle * 1.6f;
        var isleDir = -ToIsle;
        var isleStart = March(IsleC + isleDir * 4f, isleDir, IsleHeight) - isleDir * 1.6f;
        _docks = new[] { (mainStart, ToIsle, PierLen), (isleStart, isleDir, PierLen) };
        _berths = new[] { mainStart + ToIsle * GangT + RouteRight * BoatOff, isleStart + isleDir * GangT + RouteRight * BoatOff };
    }

    // ---------------- math helpers ----------------
    private static float SS(float e0, float e1, float x) { float t = Mathf.Clamp01((x - e0) / (e1 - e0)); return t * t * (3 - 2 * t); }
    private static Vector2 XZ(Vector3 v) => new(v.x, v.z);
    private static Vector2 Dir(float a) => new(Mathf.Cos(a), Mathf.Sin(a));
    private static float Hash(float x, float y) { float h = Mathf.Sin(x * 127.1f + y * 311.7f) * 43758.5453f; return h - Mathf.Floor(h); }
    private static float Noise(float x, float y)
    {
        float ix = Mathf.Floor(x), iy = Mathf.Floor(y), fx = x - ix, fy = y - iy;
        fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy);
        return Mathf.Lerp(Mathf.Lerp(Hash(ix, iy), Hash(ix + 1, iy), fx), Mathf.Lerp(Hash(ix, iy + 1), Hash(ix + 1, iy + 1), fx), fy);
    }

    private static Vector2 GardenEdge(float a)
    {
        float ripple = Mathf.Sin(a) * (.065f * Mathf.Sin(5 * a + .6f) + .035f * Mathf.Sin(9 * a));
        return GardenCentre + new Vector2(18.63f * Mathf.Cos(a) * (1 + ripple), 22.5f * Mathf.Sin(a) * (1 + ripple));
    }
    private static void BuildGardenTable()
    {
        const int n = 1600;
        _gardenTable = new Vector2[n + 1]; _gardenA = new float[n + 1];
        for (int i = 0; i <= n; i++) { _gardenA[i] = Mathf.PI * i / n; _gardenTable[i] = GardenEdge(_gardenA[i]); }
    }
    /// <summary>Nearest garden edge parameter a and signed distance s (positive = outside the lawn).</summary>
    private static (float a, float s) Garden(Vector2 p)
    {
        int best = 0; float bd = float.MaxValue;
        for (int i = 0; i < _gardenTable.Length; i += 16) { float d = (p - _gardenTable[i]).sqrMagnitude; if (d < bd) { bd = d; best = i; } }
        int lo = Mathf.Max(0, best - 16), hi = Mathf.Min(_gardenTable.Length - 1, best + 16);
        for (int i = lo; i <= hi; i++) { float d = (p - _gardenTable[i]).sqrMagnitude; if (d < bd) { bd = d; best = i; } }
        var e = _gardenTable[best];
        var t = _gardenTable[Mathf.Min(best + 1, _gardenTable.Length - 1)] - _gardenTable[Mathf.Max(best - 1, 0)];
        var normal = new Vector2(t.y, -t.x).normalized;
        return (_gardenA[best], Vector2.Dot(p - e, normal));
    }

    /// <summary>Organic beach blob: flat sand in the middle, sloping into the water at a wavy rim.</summary>
    private static float Beach(Vector2 p, Vector2 c, float major, float minor, float axisAngle, float phase)
    {
        var v = p - c; var ax = Dir(axisAngle); var ay = new Vector2(-ax.y, ax.x);
        float x = Vector2.Dot(v, ax) / major, y = Vector2.Dot(v, ay) / minor;
        float th = Mathf.Atan2(y, x);
        float rr = 1 + .10f * Mathf.Sin(2 * th + phase) + .07f * Mathf.Sin(3 * th + 1.3f + phase) + .04f * Mathf.Sin(5 * th + 2 * phase);
        float d = Mathf.Sqrt(x * x + y * y) / rr;
        return Mathf.Lerp(BeachY + .1f * (1 - Mathf.Clamp01(d)), Floor, SS(.55f, 1.25f, d));
    }

    private static float DockRaise(Vector2 p, float h)
    {
        foreach (var (start, dir, _) in _docks)
        {
            var v = p - start; float t = Vector2.Dot(v, dir), lat = Mathf.Abs(v.x * dir.y - v.y * dir.x);
            float w = (1 - SS(1.05f, 1.7f, lat)) * SS(-1.8f, -.7f, t) * (1 - SS(1.2f, 2.2f, t));
            if (w > 0) h = Mathf.Max(h, DeckY - .03f - (1 - w) * 3f);
        }
        return h;
    }
    /// <summary>Deepens only the underwater ground under the two ferry berths so the hull never meets the sea floor.</summary>
    private static float BerthCarve(Vector2 p, float h)
    {
        if (h >= WaterY - .05f) return h;
        foreach (var c in _berths)
        {
            var v = p - c; float along = Mathf.Abs(Vector2.Dot(v, ToIsle)), lat = Mathf.Abs(Vector2.Dot(v, RouteRight));
            float w = (1 - SS(7.0f, 8.5f, along)) * (1 - SS(2.6f, 3.8f, lat));
            if (w > 0) h = Mathf.Min(h, Mathf.Lerp(h, Floor - .1f, w));
        }
        return h;
    }
    private static bool InDock(Vector2 p, float margin)
    {
        foreach (var (start, dir, len) in _docks)
        {
            var v = p - start; float t = Vector2.Dot(v, dir), lat = Mathf.Abs(v.x * dir.y - v.y * dir.x);
            if (t > -1.2f - margin && t < len + margin && lat < 1.1f + margin) return true;
        }
        return false;
    }
    private static Vector2 March(Vector2 from, Vector2 dir, Func<Vector2, float> height)
    {
        for (float d = 0; d < 40; d += .1f) if (height(from + dir * d) < WaterY + .35f) return from + dir * d;
        return from;
    }

    // ---------------- mainland ----------------
    private static float GardenRampHeight(float a)
    {
        if (a <= ALand1) return GardenTop - .012f;
        float t = Mathf.Clamp01((a - ALand1) / (ARampEnd - ALand1));
        // ease out at the bottom so the path lands softly on the sand
        t = t < .85f ? t / .85f * .93f : .93f + (t - .85f) / .15f * .07f;
        return Mathf.Lerp(GardenTop - .012f, BeachY + .05f, t);
    }
    private static float MainlandHeight(Vector2 p)
    {
        var (a, s) = Garden(p);
        float h = Floor;
        float beachAxis = Mathf.Atan2(GardenEdge(2.78f).y - GardenEdge(2.62f).y, GardenEdge(2.78f).x - GardenEdge(2.62f).x);
        h = Mathf.Max(h, Beach(p, BeachC, 8.4f, 6.2f, beachAxis, .8f));
        if (s > -1.5f && a > ALand0 - .25f && a < ARampEnd + .4f)
        {
            float rh = GardenRampHeight(a);
            float arc = 21f;
            float edge = Mathf.Max((s - RampW) / 1.5f, Mathf.Max((ALand0 - a) * arc / 1.4f, (a - ARampEnd) * arc / 2.5f));
            if (edge < 1) h = Mathf.Max(h, Mathf.Lerp(rh, Floor, SS(0, 1, edge)));
        }
        h = BerthCarve(p, DockRaise(p, h));
        // jitter only on low sand so the beach does not look machined
        if (h < 0 && h > Floor + .1f) h += (Noise(p.x * .7f, p.y * .7f) - .5f) * .06f;
        return h;
    }
    private static float MainlandTrail(Vector2 p)
    {
        var (a, s) = Garden(p);
        return SS(ALand0 - .03f, ALand0 + .02f, a) * (1 - SS(ARampEnd - .02f, ARampEnd + .08f, a)) * (1 - SS(RampW - .1f, RampW + .2f, s)) * SS(-1.6f, -1.4f, s);
    }
    /// <summary>Walkability mask: keep players off the rock skirt under the garden lawn, except at the ramp landing.</summary>
    private static float MainlandMask(Vector2 p)
    {
        var (a, s) = Garden(p);
        if (a > GapA0 - .01f && a < GapA1 && s > -1.6f) return 1;
        return s - .8f;
    }
    private static IEnumerable<Vector2> MainlandRail()
    {
        float s = RampW - .3f;
        var pts = new List<Vector2>();
        var (e0, n0) = EdgeAndNormal(ALand0 + .02f);
        for (float d = .2f; d < s; d += 1.6f) pts.Add(e0 + n0 * d);
        for (float a = ALand0 + .02f; a < ARampEnd - .12f; a += .08f) { var (e, n) = EdgeAndNormal(a); pts.Add(e + n * s); }
        return pts;
    }
    private static (Vector2 e, Vector2 n) EdgeAndNormal(float a)
    {
        var e = GardenEdge(a); var t = GardenEdge(a + .001f) - GardenEdge(a - .001f);
        return (e, new Vector2(t.y, -t.x).normalized);
    }

    // ---------------- island ----------------
    private static float IsleRadius(float th) => 12.5f + 1.1f * Mathf.Sin(3 * th + .7f) + .6f * Mathf.Sin(5 * th + 2.1f) + .35f * Mathf.Sin(8 * th + .4f);
    private static float Wrap(float a) { while (a > Mathf.PI) a -= 2 * Mathf.PI; while (a < -Mathf.PI) a += 2 * Mathf.PI; return a; }
    private static float IsleRampU(float th) => -Wrap(th - Theta0) / RampSpan;   // 0 at the foot, 1 at the top (clockwise)
    private static float IsleRampHeight(float u)
    {
        float t = Mathf.Clamp01(u);
        return Mathf.Lerp(BeachY + .05f, IsleTop + .02f, t);
    }
    private static float IsleHeight(Vector2 p)
    {
        var v = p - IsleC; float r = v.magnitude, th = Mathf.Atan2(v.y, v.x);
        float rc = IsleRadius(th);
        float cove = 3.6f * Mathf.Exp(-Mathf.Pow(Wrap(th - ThetaDock) / .36f, 2));
        float s = rc - r - cove;
        float cliff;
        if (s < 0) cliff = -1.45f + s * 1.3f;
        else if (s < 2.3f)
        {
            float t = s / 2.3f;
            cliff = Mathf.Lerp(-1.45f, IsleTop, Mathf.Pow(SS(0, 1, t), .75f));
            cliff += (Noise(p.x * 1.3f, p.y * 1.3f) - .5f) * .5f * Mathf.Sin(t * Mathf.PI);
        }
        else cliff = IsleTop + .45f * SS(2.3f, 11, s);
        float h = Mathf.Max(Floor, cliff);
        var coveCentre = IsleC + Dir(ThetaDock) * (IsleRadius(ThetaDock) - 2.2f);
        h = Mathf.Max(h, Beach(p, coveCentre, 7.2f, 5.0f, ThetaDock + Mathf.PI / 2, 2.1f));
        // path cut into / built against the cliff
        float u = IsleRampU(th), arcLen = RampSpan * (rc - 2);
        float sr = rc - r;
        float band = SS(IsleBandIn - .45f, IsleBandIn, sr) * (1 - SS(IsleBandOut, IsleBandOut + .45f, sr));
        float ends = SS(-.6f / arcLen, 0, u) * (1 - SS(1, 1 + 1.2f / arcLen, u));
        float m = band * ends;
        if (m > 0) h = Mathf.Lerp(h, IsleRampHeight(u), m);
        h = BerthCarve(p, DockRaise(p, h));
        if (h < 0 && h > Floor + .1f) h += (Noise(p.x * .7f, p.y * .7f) - .5f) * .06f;
        return h;
    }
    private static float IsleTrail(Vector2 p)
    {
        var v = p - IsleC; float r = v.magnitude, th = Mathf.Atan2(v.y, v.x), sr = IsleRadius(th) - r;
        float u = IsleRampU(th);
        float onRamp = SS(IsleBandIn - .2f, IsleBandIn + .1f, sr) * (1 - SS(IsleBandOut - .1f, IsleBandOut + .2f, sr)) * SS(-.05f, 0, u) * (1 - SS(1.02f, 1.08f, u));
        // short trail on the plateau from the ramp head towards the tree
        var head = IsleC + Dir(Theta0 - RampSpan) * (IsleRadius(Theta0 - RampSpan) - 2.1f);
        float toTree = SouthBankCoast.DistToSegment(p, head, IsleC + (head - IsleC).normalized * 4.2f);
        return Mathf.Max(onRamp, 1 - SS(.9f, 1.25f, toTree));
    }
    private static IEnumerable<Vector2> IsleRail()
    {
        var pts = new List<Vector2>();
        for (float u = .18f; u <= 1.0f; u += .045f)
        {
            float th = Theta0 - u * RampSpan;
            pts.Add(IsleC + Dir(th) * (IsleRadius(th) - IsleBandIn - .25f));
        }
        return pts;
    }

    // ---------------- height field ----------------
    private sealed class Field
    {
        private readonly string _name; private readonly Rect _rect;
        private readonly Func<Vector2, float> _height, _trail, _mask;
        private int _nx, _nz; private float[] _h, _t, _f; private Vector3[] _n;
        public float[] Heights => _h; public Rect Area => _rect; public int Nx => _nx; public int Nz => _nz;

        public Field(string name, Rect rect, Func<Vector2, float> height, Func<Vector2, float> trail, Func<Vector2, float> mask)
        { _name = name; _rect = rect; _height = height; _trail = trail; _mask = mask; }

        private Vector2 P(int x, int z) => new(_rect.xMin + x * Cell, _rect.yMin + z * Cell);
        private int I(int x, int z) => z * _nx + x;

        public void Sample()
        {
            _nx = Mathf.CeilToInt(_rect.width / Cell) + 1; _nz = Mathf.CeilToInt(_rect.height / Cell) + 1;
            _h = new float[_nx * _nz]; _t = new float[_nx * _nz]; _f = new float[_nx * _nz]; _n = new Vector3[_nx * _nz];
            for (int z = 0; z < _nz; z++) for (int x = 0; x < _nx; x++) { var p = P(x, z); _h[I(x, z)] = _height(p); _t[I(x, z)] = _trail(p); }
            for (int z = 0; z < _nz; z++) for (int x = 0; x < _nx; x++)
            {
                float hx = (_h[I(Mathf.Min(x + 1, _nx - 1), z)] - _h[I(Mathf.Max(x - 1, 0), z)]) / (2 * Cell);
                float hz = (_h[I(x, Mathf.Min(z + 1, _nz - 1))] - _h[I(x, Mathf.Max(z - 1, 0))]) / (2 * Cell);
                _n[I(x, z)] = new Vector3(-hx, 1, -hz).normalized;
                float slope = Mathf.Sqrt(hx * hx + hz * hz), h = _h[I(x, z)];
                _f[I(x, z)] = Mathf.Min(Mathf.Min(.8f - slope, h - (WaterY + .15f)), _mask(P(x, z)));
            }
        }

        public float HeightAt(Vector2 p)
        {
            float fx = (p.x - _rect.xMin) / Cell, fz = (p.y - _rect.yMin) / Cell;
            if (fx < 0 || fz < 0 || fx >= _nx - 1 || fz >= _nz - 1) return Floor;
            int x = (int)fx, z = (int)fz; fx -= x; fz -= z;
            return Mathf.Lerp(Mathf.Lerp(_h[I(x, z)], _h[I(x + 1, z)], fx), Mathf.Lerp(_h[I(x, z + 1)], _h[I(x + 1, z + 1)], fx), fz);
        }

        public MeshCollider BuildMesh(Transform group, Material[] mats)
        {
            var verts = new List<Vector3>(); var norms = new List<Vector3>(); var uvs = new List<Vector2>();
            var subs = new List<int>[4]; for (int i = 0; i < 4; i++) subs[i] = new List<int>();
            var map = new Dictionary<long, int>();
            int V(int gi, int cls, int proj)
            {
                long key = ((long)gi << 4) | ((long)cls << 2) | (long)proj;
                if (map.TryGetValue(key, out int idx)) return idx;
                int x = gi % _nx, z = gi / _nx; var p = P(x, z); float h = _h[gi];
                verts.Add(new Vector3(p.x, h, p.y)); norms.Add(_n[gi]);
                Vector2 uv = cls == 3
                    ? new Vector2((proj == 0 ? (p.x - p.y) : (p.x + p.y)) * .7071f / 3.5f, h / 3.5f)
                    : new Vector2(p.x, p.y);
                uvs.Add(uv); map[key] = verts.Count - 1; return verts.Count - 1;
            }
            void Tri(int a, int b, int c)
            {
                float ha = _h[a], hb = _h[b], hc = _h[c];
                if (ha < Floor + .35f && hb < Floor + .35f && hc < Floor + .35f) return;
                Vector3 A = verts.Count >= 0 ? new Vector3(P(a % _nx, a / _nx).x, ha, P(a % _nx, a / _nx).y) : default;
                Vector3 B = new(P(b % _nx, b / _nx).x, hb, P(b % _nx, b / _nx).y), C = new(P(c % _nx, c / _nx).x, hc, P(c % _nx, c / _nx).y);
                var n = Vector3.Cross(B - A, C - A).normalized;
                float trail = (_t[a] + _t[b] + _t[c]) / 3, hm = (ha + hb + hc) / 3;
                int cls = n.y < .72f ? 3 : trail > .5f ? 2 : hm < BeachY + .38f ? 1 : 0;
                int proj = Mathf.Abs(n.x - n.z) >= Mathf.Abs(n.x + n.z) ? 0 : 1;
                subs[cls].Add(V(a, cls, proj)); subs[cls].Add(V(b, cls, proj)); subs[cls].Add(V(c, cls, proj));
            }
            for (int z = 0; z < _nz - 1; z++) for (int x = 0; x < _nx - 1; x++)
            {
                int i00 = I(x, z), i10 = I(x + 1, z), i01 = I(x, z + 1), i11 = I(x + 1, z + 1);
                // split along the flatter diagonal
                if (Mathf.Abs(_h[i00] - _h[i11]) < Mathf.Abs(_h[i10] - _h[i01])) { Tri(i00, i01, i11); Tri(i00, i11, i10); }
                else { Tri(i00, i01, i10); Tri(i10, i01, i11); }
            }
            var mesh = MeshAsset(_name);
            mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(verts); mesh.SetNormals(norms); mesh.SetUVs(0, uvs);
            mesh.subMeshCount = 4;
            for (int i = 0; i < 4; i++) mesh.SetTriangles(subs[i], i);
            mesh.RecalculateBounds(); mesh.RecalculateTangents();
            EditorUtility.SetDirty(mesh);
            var g = new GameObject(_name + " ground", typeof(MeshFilter), typeof(MeshRenderer));
            g.transform.SetParent(group, false);
            g.GetComponent<MeshFilter>().sharedMesh = mesh;
            g.GetComponent<MeshRenderer>().sharedMaterials = mats;
            var col = g.AddComponent<MeshCollider>(); col.sharedMesh = mesh;
            Log.Add($"{_name}: {verts.Count} vertices, triangles grass/sand/trail/cliff = {subs[0].Count / 3}/{subs[1].Count / 3}/{subs[2].Count / 3}/{subs[3].Count / 3}");
            return col;
        }

        /// <summary>Invisible walls along the walkable contour (marching squares on f = 0).</summary>
        public void BuildWalls(Transform group)
        {
            var verts = new List<Vector3>(); var tris = new List<int>(); int segments = 0;
            bool mainland = _name.StartsWith("Garden");
            for (int z = 0; z < _nz - 1; z++) for (int x = 0; x < _nx - 1; x++)
            {
                int[] c = { I(x, z), I(x + 1, z), I(x + 1, z + 1), I(x, z + 1) };
                var cp = new[] { P(x, z), P(x + 1, z), P(x + 1, z + 1), P(x, z + 1) };
                var hits = new List<Vector2>();
                for (int e = 0; e < 4; e++)
                {
                    float fa = _f[c[e]], fb = _f[c[(e + 1) % 4]];
                    if ((fa >= 0) != (fb >= 0)) hits.Add(Vector2.Lerp(cp[e], cp[(e + 1) % 4], fa / (fa - fb)));
                }
                if (hits.Count < 2) continue;
                float lo = Mathf.Min(Mathf.Min(_h[c[0]], _h[c[1]]), Mathf.Min(_h[c[2]], _h[c[3]]));
                float hi = Mathf.Max(Mathf.Max(_h[c[0]], _h[c[1]]), Mathf.Max(_h[c[2]], _h[c[3]]));
                for (int k = 0; k + 1 < hits.Count; k += 2)
                {
                    var a = hits[k]; var b = hits[k + 1]; var mid = (a + b) / 2;
                    if (InDock(mid, .2f)) continue;
                    if (mainland && Garden(mid).s < -.2f) continue;
                    int s0 = verts.Count;
                    verts.Add(new Vector3(a.x, lo - .3f, a.y)); verts.Add(new Vector3(b.x, lo - .3f, b.y));
                    verts.Add(new Vector3(a.x, hi + 2.8f, a.y)); verts.Add(new Vector3(b.x, hi + 2.8f, b.y));
                    tris.AddRange(new[] { s0, s0 + 2, s0 + 1, s0 + 1, s0 + 2, s0 + 3, s0, s0 + 1, s0 + 2, s0 + 1, s0 + 3, s0 + 2 });
                    segments++;
                }
            }
            var mesh = MeshAsset(_name + " walls");
            mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(verts); mesh.SetTriangles(tris, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);
            var g = new GameObject(_name + " invisible boundary"); g.transform.SetParent(group, false);
            g.AddComponent<MeshCollider>().sharedMesh = mesh;
            Log.Add($"{_name}: {segments} boundary wall segments");
        }
    }

    // ---------------- assets ----------------
    private static Mesh MeshAsset(string name)
    {
        string path = Art + "Meshes/" + name + ".asset";
        var m = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (m) { m.Clear(); return m; }
        m = new Mesh { name = name }; AssetDatabase.CreateAsset(m, path); return m;
    }
    private static Texture2D Tex(string file, bool alpha)
    {
        string path = Art + "Textures/" + file;
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        if (!importer) return null;
        var wrap = alpha ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
        if (importer.alphaIsTransparency != alpha || importer.wrapMode != wrap || importer.textureCompression != TextureImporterCompression.Uncompressed || importer.maxTextureSize != 2048)
        {
            importer.textureType = TextureImporterType.Default; importer.alphaIsTransparency = alpha; importer.mipmapEnabled = true;
            importer.wrapMode = wrap; importer.filterMode = FilterMode.Bilinear; importer.anisoLevel = 4;
            importer.maxTextureSize = 2048; importer.textureCompression = TextureImporterCompression.Uncompressed; importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }
    private static Material GroundMaterial(string name, string tex, float tiling, Color tint)
    {
        string path = Art + "Materials/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!m) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name }; AssetDatabase.CreateAsset(m, path); }
        var t = Tex(tex, false);
        if (t) m.SetTexture("_BaseMap", t);
        m.SetTextureScale("_BaseMap", Vector2.one * tiling);
        m.SetColor("_BaseColor", tint); m.SetFloat("_Smoothness", .05f);
        EditorUtility.SetDirty(m); return m;
    }
    private static Material[] Materials() => new[]
    {
        AssetDatabase.LoadAssetAtPath<Material>(Project + "Art/WillowCourt/Materials/Moss.mat"),
        GroundMaterial("Sacred beach sand", "SandTile.png", .2f, Color.white),
        GroundMaterial("Sacred trail", "TrailTile.png", .25f, new Color(.82f, .77f, .72f)),
        AssetDatabase.LoadAssetAtPath<Material>(Project + "Art/WaterfrontUpgrade/Materials/Coast cliff.mat"),
    };
    private static Material PropMaterial(string name)
    {
        // Reuse the WaterfrontUpgrade shore props (e.g. ShoreReeds) when this folder has no such texture.
        var shared = AssetDatabase.LoadAssetAtPath<Material>(Project + "Art/WaterfrontUpgrade/Materials/" + name + ".mat");
        if (shared && !File.Exists(Art + "Textures/" + name + ".png")) return shared;
        string path = Art + "Materials/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!m)
        {
            m = new Material(AssetDatabase.LoadAssetAtPath<Material>(Project + "Materials/Garden/shrub.mat")) { name = name };
            AssetDatabase.CreateAsset(m, path);
        }
        m.mainTexture = Tex(name + ".png", true); m.color = Color.white; m.SetFloat("_Cutoff", .35f); m.SetFloat("_Smoothness", 0); m.SetFloat("_Cull", 0);
        // Glowing props: emission = the picture itself, so Bloom picks them up at night.
        float glow = name == "GlowMushrooms" ? .9f : name == "SpiritFlowers" ? .45f : 0;
        if (glow > 0)
        {
            m.EnableKeyword("_EMISSION"); m.SetTexture("_EmissionMap", m.mainTexture); m.SetColor("_EmissionColor", Color.white * glow);
            // With flags None, URP validation strips _EMISSION again during builds.
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }
        else m.DisableKeyword("_EMISSION");
        EditorUtility.SetDirty(m); return m;
    }

    // ---------------- garden fence gap ----------------
    private static void OpenFence(Transform village)
    {
        var fence = village.Find("18 • Academy north garden/Oak coast fence");
        if (!fence) { Log.Add("WARNING: oak fence not found"); return; }
        int off = 0;
        foreach (Transform t in fence)
        {
            var (a, s) = Garden(XZ(t.position));
            bool inGap = a > GapA0 && a < GapA1 && s > -1.5f;
            if (inGap && t.gameObject.activeSelf) { t.gameObject.SetActive(false); off++; }
        }
        Log.Add($"fence: {off} pieces disabled for the ramp entrance (a {GapA0}..{GapA1})");
    }

    // ---------------- railings & piers ----------------
    private static Material Oak => AssetDatabase.LoadAssetAtPath<Material>(Project + "Art/AcademyGarden/Materials/Weathered oak.mat");
    private static Material OakCap => AssetDatabase.LoadAssetAtPath<Material>(Project + "Art/AcademyGarden/Materials/Oak end grain.mat");
    private static GameObject Box(Transform parent, string name, Vector3 p, Vector3 size, Material mat, Quaternion? rot = null)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Cube); g.name = name; g.transform.SetParent(parent, false);
        g.transform.position = p; g.transform.rotation = rot ?? Quaternion.identity; g.transform.localScale = size;
        g.GetComponent<Renderer>().sharedMaterial = mat; Object.DestroyImmediate(g.GetComponent<BoxCollider>()); return g;
    }
    private static void Railing(Transform group, string name, IEnumerable<Vector2> points, Func<Vector2, float> height)
    {
        var parent = new GameObject(name).transform; parent.SetParent(group, false);
        var pts = points.ToList(); Vector3 prev = default;
        for (int i = 0; i < pts.Count; i++)
        {
            var p = new Vector3(pts[i].x, height(pts[i]), pts[i].y);
            Box(parent, "Oak post", p + Vector3.up * .5f, new Vector3(.17f, 1.0f, .17f), Oak);
            Box(parent, "Post cap", p + Vector3.up * 1.03f, new Vector3(.23f, .08f, .23f), OakCap);
            if (i > 0)
            {
                var mid = (prev + p) / 2; float len = Vector3.Distance(prev, p); var rot = Quaternion.LookRotation(p - prev);
                Box(parent, "Oak rail", mid + Vector3.up * .82f, new Vector3(.09f, .12f, len + .06f), Oak, rot);
                Box(parent, "Oak rail", mid + Vector3.up * .38f, new Vector3(.08f, .1f, len + .06f), Oak, rot);
            }
            prev = p;
        }
    }
    private static Collider Dock(Transform parent, string name, (Vector2 start, Vector2 dir, float length) d, int ferrySide)
    {
        var root = new GameObject(name).transform; root.SetParent(parent, false);
        var fwd = new Vector3(d.dir.x, 0, d.dir.y); var rot = Quaternion.LookRotation(fwd); var right = rot * Vector3.right;
        var s = new Vector3(d.start.x, DeckY, d.start.y);
        for (float t = .15f; t < d.length; t += .34f)
            Box(root, "Deck plank", s + fwd * t + Vector3.down * .04f, new Vector3(2.2f + Mathf.Sin(t * 7) * .06f, .08f, .3f), Oak, rot);
        foreach (float side in new[] { -.82f, .82f })
            Box(root, "Stringer", s + fwd * d.length / 2 + right * side + Vector3.down * .2f, new Vector3(.16f, .22f, d.length), OakCap, rot);
        for (float t = 1.4f; t <= d.length + .01f; t += 2f)
            foreach (float side in new[] { -1.06f, 1.06f })
            {
                bool end = t + 2f > d.length + .01f;
                float top = end ? .95f : .12f;
                Box(root, "Pile", s + fwd * t + right * side + Vector3.up * ((top + Floor - DeckY) / 2), new Vector3(.22f, top - (Floor - DeckY), .22f), Oak, rot);
                if (end) Box(root, "Pile cap", s + fwd * t + right * side + Vector3.up * (top + .04f), new Vector3(.28f, .08f, .28f), OakCap, rot);
            }
        // walkable deck + side / end walls
        var deck = new GameObject("Deck collider"); deck.transform.SetParent(root, false);
        deck.transform.SetPositionAndRotation(s + fwd * (d.length - 1.2f) / 2 + Vector3.down * .15f, rot);
        var box = deck.AddComponent<BoxCollider>(); box.size = new Vector3(2.2f, .3f, d.length + 1.2f);
        foreach (float side in new[] { -1.2f, 1.2f })
        {
            // the ferry side is open where the gangway meets the pier
            var spans = Mathf.Sign(side) == ferrySide
                ? new[] { (.3f, GangT - .7f), (GangT + .7f, d.length) }
                : new[] { (.3f, d.length) };
            foreach (var (t0, t1) in spans)
            {
                var w = new GameObject("Invisible pier rail"); w.transform.SetParent(root, false);
                w.transform.SetPositionAndRotation(s + fwd * (t0 + t1) / 2 + right * side + Vector3.up * 1.2f, rot);
                w.AddComponent<BoxCollider>().size = new Vector3(.2f, 3, t1 - t0);
            }
        }
        // mooring bollards either side of the gangway
        foreach (float t in new[] { GangT - 1.3f, GangT + 1.3f })
        {
            var b = s + fwd * t + right * (ferrySide * .95f);
            Box(root, "Mooring bollard", b + Vector3.up * .22f, new Vector3(.2f, .44f, .2f), OakCap, rot);
        }
        var endWall = new GameObject("Invisible pier end"); endWall.transform.SetParent(root, false);
        endWall.transform.SetPositionAndRotation(s + fwd * (d.length + .1f) + Vector3.up * 1.2f, rot);
        endWall.AddComponent<BoxCollider>().size = new Vector3(2.6f, 3, .2f);
        Log.Add($"{name}: start {d.start}, length {d.length}");
        return box;
    }

    // ---------------- ferry ----------------
    /// <summary>Places the ferry prefab at the garden berth and marks both berths for the future sailing script.</summary>
    private static Collider Ferry(Transform group)
    {
        var prefab = FerryBuilder.BuildPrefab();
        var rot = Quaternion.LookRotation(new Vector3(ToIsle.x, 0, ToIsle.y));
        var route = new GameObject("Ferry route").transform; route.SetParent(group, false);
        foreach (var (name, b) in new[] { ("Garden berth", _berths[0]), ("Isle berth", _berths[1]) })
        {
            var m = new GameObject(name).transform; m.SetParent(route, false);
            m.SetPositionAndRotation(new Vector3(b.x, DeckY, b.y), rot);
        }
        var ferry = (GameObject)PrefabUtility.InstantiatePrefab(prefab, group);
        ferry.transform.SetPositionAndRotation(new Vector3(_berths[0].x, DeckY, _berths[0].y), rot);
        Log.Add($"ferry at garden berth {_berths[0]}, isle berth {_berths[1]}, route {Vector2.Distance(_berths[0], _berths[1]):0.0} m");
        return ferry.transform.Find("Deck").GetComponent<Collider>();
    }

    /// <summary>The far island needs more sea: grow the water plane (world-space shader, so scaling is safe).</summary>
    private static void EnsureSea(Transform village)
    {
        var water = village.Find("05 • Canal & bridge/Canal water");
        const float scale = 1.8f;
        if (water.localScale.x < scale) { water.localScale = new Vector3(scale, 1, scale); Log.Add($"sea scaled to {scale}x"); }
        var mat = water.GetComponent<Renderer>().sharedMaterial;
        var rect = mat.GetVector("_MapRect");
        if (rect.z < 300) Log.Add($"WARNING shore map rect {rect} is too small for the far island");
    }

    // ---------------- props ----------------
    private static Transform SpriteProp(Transform parent, string tex, Vector3 pos, float height, bool flip = false, float bury = 0, bool shadows = false)
    {
        var mat = PropMaterial(tex); var t = (Texture2D)mat.mainTexture;
        var root = new GameObject(tex).transform; root.SetParent(parent, false); root.position = pos;
        if (!t) { Log.Add("WARNING missing texture " + tex); return root; }
        float w = height * t.width / t.height;
        var sprite = new GameObject("Sprite", typeof(MeshFilter), typeof(MeshRenderer)); sprite.transform.SetParent(root, false);
        var q = Quaternion.Euler(30, 45, 0);
        sprite.transform.localRotation = q; sprite.transform.localScale = new Vector3(flip ? -w : w, height, 1);
        sprite.transform.localPosition = -(q * Vector3.up) * bury * height;
        sprite.GetComponent<MeshFilter>().sharedMesh = SouthBankCoast.BottomQuad();
        var r = sprite.GetComponent<MeshRenderer>(); r.sharedMaterial = mat; r.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
        return root;
    }
    private static GameObject PrefabProp(Transform parent, string prefab, Vector3 pos, float scale, bool noBlob = true)
    {
        var p = AssetDatabase.LoadAssetAtPath<GameObject>(Project + "Art/Square/Props/" + prefab + ".prefab");
        var g = (GameObject)PrefabUtility.InstantiatePrefab(p, parent); g.transform.position = pos; g.transform.localScale = Vector3.one * scale;
        if (noBlob) { var b = g.transform.Find("Blob"); if (b) b.gameObject.SetActive(false); }
        return g;
    }
    private static void Light(Transform parent, string name, Vector3 pos, Color c, float range, float intensity)
    {
        var g = new GameObject(name); g.transform.SetParent(parent, false); g.transform.position = pos;
        var l = g.AddComponent<Light>(); l.type = LightType.Point; l.color = c; l.range = range; l.intensity = intensity; l.shadows = LightShadows.None;
    }

    private static void Dressing(Transform group)
    {
        var rng = new System.Random(2111);
        float R() => (float)rng.NextDouble();
        Vector3 OnIsle(Vector2 p) => new(p.x, IsleHeight(p), p.y);
        Vector3 OnMain(Vector2 p) => new(p.x, MainlandHeight(p), p.y);

        // --- sacred tree ---
        var shrine = new GameObject("Sacred tree & shrine").transform; shrine.SetParent(group, false);
        var tree = SpriteProp(shrine, "SacredTree", OnIsle(IsleC), 13.5f, false, .07f, true);
        tree.name = "Sacred tree";
        var spr = tree.Find("Sprite");
        if (spr) spr.gameObject.AddComponent<Occluder>();
        var trunk = new GameObject("Trunk collider"); trunk.transform.SetParent(tree, false);
        var cap = trunk.AddComponent<CapsuleCollider>(); cap.radius = 1.25f; cap.height = 6; cap.center = Vector3.up * 3;
        Light(shrine, "Tree glow", OnIsle(IsleC) + new Vector3(-1.2f, 2.2f, -1.2f), new Color(1f, .82f, .55f), 10, 1.6f);
        Light(shrine, "Canopy glow", OnIsle(IsleC) + new Vector3(-2f, 8f, -2f), new Color(.6f, 1f, .85f), 12, 1.0f);
        // rune stones in an open ring (gap towards the path head)
        float headAngle = Theta0 - RampSpan;
        for (int i = 0; i < 5; i++)
        {
            float a = headAngle + .75f + i * (2 * Mathf.PI - 1.5f) / 4;
            var p = IsleC + Dir(a) * 5.6f;
            var stone = SpriteProp(shrine, "RuneStone", OnIsle(p), 1.7f + R() * .4f, R() < .5f, .03f, true);
            var c = stone.gameObject.AddComponent<CapsuleCollider>(); c.radius = .35f; c.height = 2; c.center = Vector3.up;
        }
        // lanterns at the path head and on the beach
        var head = IsleC + Dir(headAngle) * (IsleRadius(headAngle) - 2.4f);
        var side = new Vector2(-(IsleC - head).normalized.y, (IsleC - head).normalized.x);
        foreach (var p in new[] { head + side * 1.6f + (IsleC - head).normalized * 1.2f, head - side * 1.6f + (IsleC - head).normalized * 1.2f })
        {
            var l = SpriteProp(shrine, "StoneLantern", OnIsle(p), 1.35f, false, .02f, true);
            var c = l.gameObject.AddComponent<CapsuleCollider>(); c.radius = .3f; c.height = 1.6f; c.center = Vector3.up * .8f;
            Light(l, "Lantern light", OnIsle(p) + Vector3.up * 1.0f, new Color(1f, .7f, .4f), 4.5f, 1.4f);
        }
        // mushrooms at the roots, spirit flowers in the grass
        for (int i = 0; i < 7; i++)
        {
            float a = R() * Mathf.PI * 2; var p = IsleC + Dir(a) * (2.0f + R() * 1.4f);
            SpriteProp(shrine, "GlowMushrooms", OnIsle(p), .45f + R() * .25f, R() < .5f);
        }
        Light(shrine, "Mushroom glow", OnIsle(IsleC) + new Vector3(-2.2f, .6f, -2.2f), new Color(.45f, .85f, 1f), 3f, .5f);
        var meadow = new GameObject("Isle planting").transform; meadow.SetParent(group, false);
        for (int i = 0; i < 110; i++)
        {
            float a = R() * Mathf.PI * 2, rr = 3.4f + R() * 7f;
            var p = IsleC + Dir(a) * rr;
            if (IsleRadius(a) - rr < 2.2f || IsleTrail(p) > .2f || Vector2.Distance(p, IsleC + Dir(a) * 5.6f) < .6f) continue;
            float pick = R();
            if (pick < .12f) SpriteProp(meadow, "SpiritFlowers", OnIsle(p), .6f + R() * .3f, R() < .5f);
            else if (pick < .2f) SpriteProp(meadow, "GlowMushrooms", OnIsle(p), .4f + R() * .2f, R() < .5f);
            else if (pick < .34f) PrefabProp(meadow, "daisies", OnIsle(p), .38f + R() * .1f);
            else PrefabProp(meadow, "grass_tall", OnIsle(p), .5f + R() * .3f);
        }
        // fireflies
        var flies = new GameObject("Fireflies", typeof(ParticleSystem)); flies.transform.SetParent(shrine, false);
        flies.transform.position = OnIsle(IsleC) + Vector3.up * 4.5f;
        var ps = flies.GetComponent<ParticleSystem>(); var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(5, 9); main.startSpeed = .12f; main.startSize = new ParticleSystem.MinMaxCurve(.05f, .12f);
        main.maxParticles = 90; main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, .9f, .55f), new Color(.65f, 1f, .85f));
        var em = ps.emission; em.rateOverTime = 12;
        var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = 7f;
        var noise = ps.noise; noise.enabled = true; noise.strength = .35f; noise.frequency = .4f;
        var col = ps.colorOverLifetime; col.enabled = true;
        var grad = new Gradient(); grad.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
            new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, .3f), new GradientAlphaKey(1, .7f), new GradientAlphaKey(0, 1) });
        col.color = grad;
        var pr = flies.GetComponent<ParticleSystemRenderer>();
        pr.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(Project + "Materials/Garden/_FX_Mote.mat");
        pr.shadowCastingMode = ShadowCastingMode.Off; pr.receiveShadows = false;

        // --- beaches ---
        var shore = new GameObject("Beach dressing").transform; shore.SetParent(group, false);
        void Beachcomb(Func<Vector2, float> h, Vector2 centre, float radius, int count)
        {
            int placed = 0;
            for (int i = 0; i < count * 6 && placed < count; i++)
            {
                var p = centre + Dir(R() * Mathf.PI * 2) * (R() * radius);
                float y = h(p);
                if (y < WaterY + .25f || y > BeachY + .25f || InDock(p, .6f)) continue;
                float pick = R();
                if (pick < .45f) SpriteProp(shore, "BeachRocks", new Vector3(p.x, y, p.y), .45f + R() * .3f, R() < .5f);
                else if (pick < .6f) SpriteProp(shore, "Driftwood", new Vector3(p.x, y, p.y), .55f + R() * .2f, R() < .5f);
                else SpriteProp(shore, "ShoreReeds", new Vector3(p.x, y, p.y), 1.1f + R() * .4f, R() < .5f);
                placed++;
            }
        }
        Beachcomb(MainlandHeight, BeachC, 8f, 9);
        Beachcomb(IsleHeight, IsleC + Dir(ThetaDock) * (IsleRadius(ThetaDock) - 2.2f), 6f, 7);
        // Lamp at each pier head on land
        foreach (var (start, dir, _) in _docks)
        {
            var right = new Vector2(dir.y, -dir.x);
            var p = start - dir * .2f + right * 1.55f;
            var h = p.x < -40 ? IsleHeight(p) : MainlandHeight(p);
            PrefabProp(shore, "lamp_iron", new Vector3(p.x, h, p.y), .85f);
        }
        // tufts along the ramp railing
        foreach (var p in MainlandRail().Where((_, i) => i % 2 == 1))
            PrefabProp(shore, "grass_tall", OnMain(p) + new Vector3(.25f, 0, .25f), .45f + R() * .2f);
        Log.Add("dressing placed");
    }

    // ---------------- water shore map ----------------
    private static void ShoreMap(Transform village, Field[] fields)
    {
        var water = village.Find("05 • Canal & bridge/Canal water").GetComponent<Renderer>().sharedMaterial;
        var texPath = AssetDatabase.GetAssetPath(water.GetTexture("_ShoreMap"));
        var rect = water.GetVector("_MapRect");
        var tex = new Texture2D(2, 2, TextureFormat.RGB24, false, true); tex.LoadImage(File.ReadAllBytes(texPath));
        int size = tex.width; var px = tex.GetPixels32(); float texel = rect.z / size;
        int changed = 0;
        foreach (var f in fields)
        {
            var area = f.Area;
            int x0 = Mathf.Max(0, Mathf.FloorToInt((area.xMin - 12 - rect.x) / texel)), x1 = Mathf.Min(size - 1, Mathf.CeilToInt((area.xMax + 12 - rect.x) / texel));
            int y0 = Mathf.Max(0, Mathf.FloorToInt((area.yMin - 12 - rect.y) / texel)), y1 = Mathf.Min(size - 1, Mathf.CeilToInt((area.yMax + 12 - rect.y) / texel));
            int w = x1 - x0 + 1, hgt = y1 - y0 + 1; var dist = new float[w * hgt];
            for (int y = 0; y < hgt; y++) for (int x = 0; x < w; x++)
            {
                var p = new Vector2(rect.x + (x0 + x + .5f) * texel, rect.y + (y0 + y + .5f) * texel);
                dist[y * w + x] = f.HeightAt(p) > WaterY || InDock(p, -.9f) ? 0 : 1e6f;
            }
            const float D1 = 1, D2 = 1.4142f;
            for (int y = 0; y < hgt; y++) for (int x = 0; x < w; x++)
            {
                float d = dist[y * w + x];
                if (x > 0) d = Mathf.Min(d, dist[y * w + x - 1] + D1);
                if (y > 0) { d = Mathf.Min(d, dist[(y - 1) * w + x] + D1); if (x > 0) d = Mathf.Min(d, dist[(y - 1) * w + x - 1] + D2); if (x < w - 1) d = Mathf.Min(d, dist[(y - 1) * w + x + 1] + D2); }
                dist[y * w + x] = d;
            }
            for (int y = hgt - 1; y >= 0; y--) for (int x = w - 1; x >= 0; x--)
            {
                float d = dist[y * w + x];
                if (x < w - 1) d = Mathf.Min(d, dist[y * w + x + 1] + D1);
                if (y < hgt - 1) { d = Mathf.Min(d, dist[(y + 1) * w + x] + D1); if (x < w - 1) d = Mathf.Min(d, dist[(y + 1) * w + x + 1] + D2); if (x > 0) d = Mathf.Min(d, dist[(y + 1) * w + x - 1] + D2); }
                dist[y * w + x] = d;
            }
            for (int y = 0; y < hgt; y++) for (int x = 0; x < w; x++)
            {
                float m = Mathf.Min(12, dist[y * w + x] * texel);
                int i = (y0 + y) * size + x0 + x;
                byte v = (byte)Mathf.Min(px[i].r, Mathf.RoundToInt(m / 12 * 255));
                if (v != px[i].r) { px[i] = new Color32(v, v, v, 255); changed++; }
            }
        }
        tex.SetPixels32(px); tex.Apply(); File.WriteAllBytes(texPath, tex.EncodeToPNG()); Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(texPath);
        Log.Add($"shore map {texPath}: {changed} pixels changed");
    }
}

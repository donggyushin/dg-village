using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Edit-time northern garden extension. Does not move the academy or rebuild existing districts.</summary>
public static class AcademyGardenBuilder
{
    private const string Art = "Assets/_Project/Art/AcademyGarden/";
    private const string Existing = "Assets/_Project/Art/";
    private const string Group = "18 • Academy north garden";
    private const float Top = 2.74f, Join = 21.055f;
    private const int Segments = 96;
    private static readonly Vector2 Centre = new(-1.62f, Join);
    private static Vector2[] _edge;
    private static Transform _root;
    private static Material _oak, _cap;

    public static Vector2 Edge(int i)
    {
        float a = Mathf.PI * i / Segments;
        float ripple = Mathf.Sin(a) * (.065f * Mathf.Sin(5*a+.6f) + .035f*Mathf.Sin(9*a));
        return Centre + new Vector2(18.63f*Mathf.Cos(a)*(1+ripple), 22.5f*Mathf.Sin(a)*(1+ripple));
    }

    [MenuItem("DG Village/Waterfront/Add academy north garden once")]
    public static void Build()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (EditorApplication.isPlaying || scene.path != "Assets/_Project/Scenes/SquareScene.unity")
            throw new InvalidOperationException("Open SquareScene in Edit mode.");
        var village = GameObject.Find("Willow Court").transform;
        if (village.Find(Group)) throw new InvalidOperationException("Garden already exists.");
        foreach (var folder in new[]{"Meshes", "Materials", "Textures"}) Directory.CreateDirectory(Art+folder);
        AssetDatabase.Refresh();
        _edge = Enumerable.Range(0, Segments+1).Select(Edge).ToArray();
        _root = new GameObject(Group).transform; _root.SetParent(village,false); _root.position=Vector3.zero;
        _oak = CopyMaterial("CanalLanding/Materials/Honey oak.mat", "Weathered oak");
        _cap = CopyMaterial("CanalLanding/Materials/Warm cut oak.mat", "Oak end grain");
        var ground = Ground();
        OpenOldBoundary(village);
        Fence();
        Planting();
        // Paths() (pink stepping stones) was removed from the scene on 2026-11-10 at the owner's request.
        ShoreMap(village);
        var placement = village.GetComponent<SquareSpawnPlacement>();
        if (placement) {
            var so=new SerializedObject(placement);var surfaces=so.FindProperty("_surfaces");
            int index=surfaces.arraySize;surfaces.InsertArrayElementAtIndex(index);surfaces.GetArrayElementAtIndex(index).objectReferenceValue=ground;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        Physics.SyncTransforms();AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        Debug.Log("Academy north garden saved: organic coast, continuous oak fence, walkable ground and spawn surface.");
    }

    private static Material CopyMaterial(string source,string name)
    {
        var m=new Material(AssetDatabase.LoadAssetAtPath<Material>(Existing+source)){name=name};
        AssetDatabase.CreateAsset(m,Art+"Materials/"+name+".mat");return m;
    }
    private static Mesh SaveMesh(string name,List<Vector3> v,List<int> triangles,List<Vector2> uv=null)
    {
        var mesh=new Mesh{name=name};mesh.SetVertices(v);mesh.SetTriangles(triangles,0);
        mesh.SetUVs(0,uv ?? v.Select(p=>new Vector2(p.x,p.z)).ToList());mesh.RecalculateNormals();mesh.RecalculateBounds();mesh.RecalculateTangents();
        AssetDatabase.CreateAsset(mesh,Art+"Meshes/"+name+".asset");return mesh;
    }
    private static GameObject MeshObject(string name,Mesh mesh,Material mat)
    {
        var g=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));g.transform.SetParent(_root,false);
        g.GetComponent<MeshFilter>().sharedMesh=mesh;g.GetComponent<MeshRenderer>().sharedMaterial=mat;return g;
    }
    private static Vector3 At(Vector2 p,float y=Top)=>new(p.x,y,p.y);
    private static MeshCollider Ground()
    {
        // The open semicircle closes along the old terrace's north edge. Flat top means no hidden steps.
        var v=new List<Vector3>{At(Centre)};v.AddRange(_edge.Select(p=>At(p)));
        var tris=new List<int>();for(int i=0;i<Segments;i++)tris.AddRange(new[]{0,i+2,i+1});
        var top=MeshObject("North garden walkable lawn",SaveMesh("Garden surface",v,tris),AssetDatabase.LoadAssetAtPath<Material>(Existing+"WillowCourt/Materials/Moss.mat"));
        var col=top.AddComponent<MeshCollider>();col.sharedMesh=top.GetComponent<MeshFilter>().sharedMesh;
        v=new List<Vector3>();tris=new List<int>();var uv=new List<Vector2>();
        float[] heights={Top,Top-.18f,1.6f,.35f,-.8f,-1.7f,-2.5f};float[] offsets={0,.12f,.32f,.65f,.95f,1.15f,1.4f};
        float arc=0;
        for(int i=0;i<Segments;i++) {
            float length=Vector2.Distance(_edge[i],_edge[i+1]);
            for(int band=0;band<heights.Length-1;band++) {
                int start=v.Count;
                foreach(var k in new[]{0,1,2,3}) {
                    int e=i+k%2,b=band+k/2;
                    var dir=(_edge[e]-Centre).normalized;
                    float jitter=b<2?0:.10f*Mathf.Sin(e*1.8f+b*2.1f);
                    v.Add(At(_edge[e]+dir*(offsets[b]+jitter),heights[b]));
                    uv.Add(new Vector2((arc+(k%2)*length)/3.5f,heights[b]/3.5f));
                }
                tris.AddRange(new[]{start,start+1,start+2,start+1,start+3,start+2});
            }
            arc+=length;
        }
        MeshObject("Natural rock coast",SaveMesh("Garden rocky shore",v,tris,uv),AssetDatabase.LoadAssetAtPath<Material>(Existing+"WaterfrontUpgrade/Materials/Coast cliff.mat"));
        return col;
    }

    private static void OpenOldBoundary(Transform village)
    {
        // Preserve original objects as disabled references instead of deleting the user's old perimeter.
        foreach(var r in village.GetComponentsInChildren<MeshRenderer>().Where(r=>r.name=="Rail plinth").ToArray()) {
            var b=r.bounds;
            if(Mathf.Abs(b.center.z-20.493f)<.12f && b.min.y>2.6f && b.size.x>2)
                r.transform.parent.gameObject.SetActive(false);
        }
        foreach(var c in village.Find("08 • Traversal boundaries").GetComponentsInChildren<BoxCollider>()) {
            var b=c.bounds;
            if(b.size.x>30 && b.min.z>20) c.gameObject.SetActive(false);
            else if(b.size.z>30) {
                // Retain protection of the old district, but do not cut across the new lawn.
                float max=20.49f;float length=max-b.min.z;var pos=c.transform.position;pos.z=(b.min.z+max)*.5f;c.transform.position=pos;
                var size=c.size;size.z=length/c.transform.lossyScale.z;c.size=size;
            }
        }
    }
    private static GameObject Box(Transform parent,string name,Vector3 p,Vector3 size,Material mat)
    {
        var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=name;g.transform.SetParent(parent,false);g.transform.position=p;g.transform.localScale=size;
        g.GetComponent<Renderer>().sharedMaterial=mat;Object.DestroyImmediate(g.GetComponent<BoxCollider>());return g;
    }
    private static void Fence()
    {
        var parent=new GameObject("Oak coast fence").transform;parent.SetParent(_root,false);
        var points=new List<Vector2>{new(16.605f,20.493f)};
        for(int i=0;i<=Segments;i+=2)points.Add(_edge[i]+(Centre-_edge[i]).normalized*.7f);
        points.Add(new Vector2(-19.683f,20.493f));
        for(int i=0;i<points.Count;i++) {
            Box(parent,"Oak post",At(points[i],Top+.65f),new Vector3(.19f,1.3f,.19f),_oak);
            Box(parent,"Post cap",At(points[i],Top+1.32f),new Vector3(.26f,.09f,.26f),_cap);
            if(i==0)continue;
            var a=At(points[i-1]);var b=At(points[i]);var mid=(a+b)*.5f;float len=Vector3.Distance(a,b);var rotation=Quaternion.LookRotation(b-a);
            foreach(float y in new[]{.38f,.94f}) {
                var rail=Box(parent,"Oak rail",mid+Vector3.up*y,new Vector3(.10f,.14f,len+.08f),_oak);rail.transform.rotation=rotation;
            }
            var wall=new GameObject("Fence collision");wall.transform.SetParent(parent,false);wall.transform.position=mid+Vector3.up*1.05f;wall.transform.rotation=rotation;
            // Continuous full panel colliders prevent slipping below or between the visible rails.
            wall.AddComponent<BoxCollider>().size=new Vector3(.24f,2.3f,len+.18f);
        }
    }
    private static void Prop(string name,Vector2 p,float scale)
    {
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Existing+"Square/Props/"+name+".prefab");
        var g=(GameObject)PrefabUtility.InstantiatePrefab(prefab,_root);g.name="Garden • "+name;g.transform.position=At(p);g.transform.localScale=Vector3.one*scale;
    }
    private static void Planting()
    {
        foreach(var p in new[]{new Vector2(-16,26),new Vector2(-16,33),new Vector2(-11,38),new Vector2(-5,40),new Vector2(4,39),new Vector2(11,34),new Vector2(13,27)})
            Prop(p.x<0?"tree_birch":"tree_maple",p,1.0f);
        Prop("tree_oak",new Vector2(-4,33),1.15f);
        Prop("bench",new Vector2(-9,32),.9f);Prop("bench",new Vector2(4,33),.9f);
        Prop("lamp_iron",new Vector2(-10.5f,31.8f),.9f);Prop("lamp_iron",new Vector2(5.5f,33),.9f);
        var rng=new System.Random(11026);
        for(int i=4;i<Segments-3;i+=3) {
            var p=_edge[i]+(Centre-_edge[i]).normalized*(1.8f+(float)rng.NextDouble()*.9f);
            Prop(i%2==0?"shrub":"grass_tall",p,.48f+(float)rng.NextDouble()*.15f);
            if(i%2==0)Prop("daisies",p+(Centre-p).normalized*.8f,.38f);
        }
        foreach(var centre in new[]{new Vector2(-12,27),new Vector2(7,28),new Vector2(-9,36),new Vector2(3,37)})
            for(int j=0;j<5;j++) {
                var p=centre+new Vector2(Mathf.Sin(j*2.4f),Mathf.Cos(j*2.4f))*(.4f+j*.2f);
                Prop(j%2==0?"marigolds":"daisies",p,.45f);
            }
    }
    private static void Paths()
    {
        var mat=CopyMaterial("WillowCourt/Materials/Paving.mat","Garden stepping stone");
        // Low oval stones define two approaches around the academy, joining a loose garden loop.
        var points=new List<Vector2>();
        for(int i=0;i<50;i++) {float a=i*Mathf.PI*2/50;points.Add(new Vector2(-2+11*Mathf.Cos(a),31+6*Mathf.Sin(a)));}
        for(int i=0;i<7;i++) {points.Add(new Vector2(-13+.35f*Mathf.Sin(i*.6f),21.3f+i*.75f));points.Add(new Vector2(7+.6f*Mathf.Sin(i*.4f),21.3f+i*.75f));}
        foreach(var p in points) {
            var stone=GameObject.CreatePrimitive(PrimitiveType.Cylinder);stone.name="Garden stepping stone";stone.transform.SetParent(_root,false);stone.transform.position=At(p,Top+.016f);stone.transform.localScale=new Vector3(.82f,.018f,.58f);stone.transform.rotation=Quaternion.Euler(0,p.x*29,0);
            stone.GetComponent<Renderer>().sharedMaterial=mat;Object.DestroyImmediate(stone.GetComponent<Collider>());
        }
    }
    private static void ShoreMap(Transform village)
    {
        var renderer=village.Find("05 • Canal & bridge/Canal water").GetComponent<Renderer>();
        var water=new Material(renderer.sharedMaterial){name="Garden water with north shore"};
        var map=(Texture2D)water.GetTexture("_ShoreMap");var rect=water.GetVector("_MapRect");
        var texture=new Texture2D(2,2,TextureFormat.RGB24,false,true);texture.LoadImage(File.ReadAllBytes(AssetDatabase.GetAssetPath(map)));
        int size=texture.width;var pixels=texture.GetPixels32();
        for(int y=0;y<size;y++)for(int x=0;x<size;x++) {
            var p=new Vector2(rect.x+(x+.5f)/size*rect.z,rect.y+(y+.5f)/size*rect.w);
            if(p.y<Join-1.4f || p.y>60 || p.x< -35 || p.x>32)continue;
            bool inside=false;float distance=12;
            for(int i=0,j=_edge.Length-1;i<_edge.Length;j=i++) {
                var a=_edge[i];var b=_edge[j];
                if((a.y>p.y)!=(b.y>p.y)&&p.x<(b.x-a.x)*(p.y-a.y)/(b.y-a.y)+a.x)inside=!inside;
                distance=Mathf.Min(distance,SouthBankCoast.DistToSegment(p,a,b));
            }
            float d=inside?0:Mathf.Max(0,distance-.95f);byte value=(byte)Mathf.Min(pixels[y*size+x].r,Mathf.RoundToInt(d/12*255));pixels[y*size+x]=new Color32(value,value,value,255);
        }
        texture.SetPixels32(pixels);texture.Apply();string path=Art+"Textures/NorthShoreDistance.png";File.WriteAllBytes(path,texture.EncodeToPNG());Object.DestroyImmediate(texture);AssetDatabase.ImportAsset(path);
        var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.sRGBTexture=false;importer.mipmapEnabled=false;importer.wrapMode=TextureWrapMode.Clamp;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();
        water.SetTexture("_ShoreMap",AssetDatabase.LoadAssetAtPath<Texture2D>(path));AssetDatabase.CreateAsset(water,Art+"Materials/Garden water with north shore.mat");renderer.sharedMaterial=water;
    }
}

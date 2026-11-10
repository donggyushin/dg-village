using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

// Edit-time mesh authoring; the saved scene needs no runtime generator.
public static class CanalLandingBuilder
{
    private const string Folder = "Assets/_Project/Art/CanalLanding/";
    private static Transform _root;
    private static Material _wood, _edge, _teal, _rope, _metal;

    [MenuItem("DG Village/Waterfront/Add decorative canal landing once")]
    public static void Create()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (EditorApplication.isPlaying || scene.path != "Assets/_Project/Scenes/SquareScene.unity" || scene.isDirty)
            throw new InvalidOperationException("Open saved SquareScene in Edit mode first.");
        if (GameObject.Find("Canal Landing")) throw new InvalidOperationException("Landing already exists.");
        System.IO.Directory.CreateDirectory(Folder + "Materials");
        System.IO.Directory.CreateDirectory(Folder + "Meshes");
        AssetDatabase.Refresh();
        _wood = Material("Honey oak", new Color(.45f,.25f,.11f));
        _edge = Material("Warm cut oak", new Color(.65f,.43f,.23f));
        _teal = Material("Painted teal hull", new Color(.10f,.28f,.27f));
        _rope = Material("Hemp rope", new Color(.65f,.56f,.37f));
        _metal = Material("Dark iron", new Color(.10f,.13f,.13f));
        _root = new GameObject("Canal Landing").transform;
        _root.SetParent(GameObject.Find("Willow Court").transform, false);
        _root.position = new Vector3(-8.8f, -.65f, -19.3f);
        var pier = new GameObject("Simple wooden landing").transform; pier.SetParent(_root, false);
        for (int i=0;i<15;i++) Box(pier,"Individual oak deck plank",new Vector3(0,0,1.95f-i*.27f),new Vector3(1.65f,.14f,.25f),i%3==0?_edge:_wood);
        foreach(float x in new[]{-.65f,.65f}) {
            Box(pier,"Under-deck bearer",new Vector3(x,-.18f,.05f),new Vector3(.14f,.22f,4.3f),_wood);
            foreach(float z in new[]{-1.85f,.05f,1.9f}) {
                Box(pier,"Timber pile",new Vector3(x,-.38f,z),new Vector3(.22f,1.95f,.22f),_wood);
                Box(pier,"Pile cap",new Vector3(x,.62f,z),new Vector3(.29f,.09f,.29f),_edge);
                for(int j=0;j<3;j++) Rope(pier,"Pile lashing",new[]{new Vector3(x-.12f,.38f+j*.035f,z-.12f),new Vector3(x+.12f,.38f+j*.035f,z-.12f),new Vector3(x+.12f,.38f+j*.035f,z+.12f),new Vector3(x-.12f,.38f+j*.035f,z+.12f),new Vector3(x-.12f,.38f+j*.035f,z-.12f)},.025f,_rope);
            }
        }
        // A small service ladder descends from the stone quay. Decorative, no new walking route.
        foreach(float x in new[]{-.3f,.3f}) Box(pier,"Quay ladder rail",new Vector3(x,.9f,2.04f),new Vector3(.07f,2.3f,.09f),_wood);
        for(int i=0;i<8;i++) Box(pier,"Quay ladder rung",new Vector3(0,-.05f+i*.28f,1.99f),new Vector3(.64f,.065f,.09f),_edge);
        var boat = new GameObject("Moored rowboat").transform; boat.SetParent(_root,false);
        boat.localPosition=new Vector3(-2.0f,-.57f,-.5f); boat.localRotation=Quaternion.Euler(0,-8,0);
        Hull(boat);
        foreach(float z in new[]{-.7f,.55f}) Box(boat,"Oak thwart",new Vector3(0,.48f,z),new Vector3(1.1f,.09f,.32f),_edge);
        for(int i=0;i<5;i++) Box(boat,"Floorboard",new Vector3((i-2)*.16f,.12f,-.12f),new Vector3(.145f,.055f,2.1f),_wood);
        for(int i=0;i<2;i++) {
            var oar=new GameObject("Resting oar").transform;oar.SetParent(boat,false);oar.localPosition=new Vector3(i==0?-.28f:.28f,.59f,-.1f);oar.localRotation=Quaternion.Euler(0,i==0?-24:19,0);
            Box(oar,"Oar shaft",Vector3.zero,new Vector3(.055f,.055f,2.4f),_edge);
            Box(oar,"Oar blade",new Vector3(0,0,-1.2f),new Vector3(.22f,.045f,.6f),_wood);
        }
        Rope(_root,"Bow mooring line",new[]{new Vector3(-.65f,.4f,1.9f),new Vector3(-1.1f,-.03f,1.48f),boat.localPosition+boat.localRotation*new Vector3(0,.6f,1.88f)},.035f,_rope);
        Rope(_root,"Stern mooring line",new[]{new Vector3(-.65f,.4f,-1.85f),new Vector3(-1.15f,-.1f,-2f),boat.localPosition+boat.localRotation*new Vector3(0,.5f,-1.6f)},.035f,_rope);
        var glow=Material("Lantern warm glass",new Color(1f,.66f,.23f));glow.EnableKeyword("_EMISSION");glow.SetColor("_EmissionColor",new Color(1f,.45f,.08f)*1.5f);
        Box(pier,"Lantern foot",new Vector3(.65f,.71f,-1.85f),new Vector3(.26f,.07f,.26f),_metal);
        Box(pier,"Lantern glass",new Vector3(.65f,.9f,-1.85f),new Vector3(.17f,.3f,.17f),glow);
        Box(pier,"Lantern roof",new Vector3(.65f,1.08f,-1.85f),new Vector3(.29f,.07f,.29f),_metal);
        foreach(float dx in new[]{-.09f,.09f}) foreach(float dz in new[]{-.09f,.09f}) Box(pier,"Lantern frame",new Vector3(.65f+dx,.9f,-1.85f+dz),new Vector3(.025f,.33f,.025f),_metal);
        AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
    }

    private static Material Material(string name, Color color) {
        var m=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name,color=color};m.SetFloat("_Smoothness",.22f);
        AssetDatabase.CreateAsset(m,Folder+"Materials/"+name+".mat");return m;
    }
    private static void Box(Transform parent,string name,Vector3 p,Vector3 scale,Material material) {
        var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=name;g.transform.SetParent(parent,false);g.transform.localPosition=p;g.transform.localScale=scale;
        g.GetComponent<Renderer>().sharedMaterial=material;Object.DestroyImmediate(g.GetComponent<Collider>());
    }
    private static void Rope(Transform parent,string name,Vector3[] points,float width,Material material) {
        var g=new GameObject(name);g.transform.SetParent(parent,false);var line=g.AddComponent<LineRenderer>();line.useWorldSpace=false;line.positionCount=points.Length;line.SetPositions(points);line.startWidth=line.endWidth=width;line.numCornerVertices=3;line.numCapVertices=3;line.sharedMaterial=material;
    }
    private static void Hull(Transform boat) {
        const int n=24;var v=new List<Vector3>();var triangles=new List<int>();
        // Outer keel, outer gunwale, inner gunwale, inner floor: an open, thick-walled hull.
        for(int ring=0;ring<4;ring++) for(int i=0;i<n;i++) {
            float a=i*Mathf.PI*2/n;float width=ring==0?.36f:ring==1?.7f:ring==2?.60f:.32f;
            float length=ring==0?1.4f:ring==1?1.95f:ring==2?1.82f:1.32f;
            float y=ring==0?-.19f:ring==3?.10f:.52f+.13f*Mathf.Pow(Mathf.Abs(Mathf.Cos(a)),4);
            v.Add(new Vector3(Mathf.Sin(a)*width,y,Mathf.Cos(a)*length));
        }
        for(int ring=0;ring<3;ring++) for(int i=0;i<n;i++) {int a=ring*n+i,b=ring*n+(i+1)%n,c=b+n,d=a+n;triangles.AddRange(new[]{a,b,c,a,c,d});}
        for(int i=1;i<n-1;i++) triangles.AddRange(new[]{3*n,3*n+i+1,3*n+i});
        var mesh=new Mesh{name="Open clinker rowboat hull"};mesh.SetVertices(v);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
        AssetDatabase.CreateAsset(mesh,Folder+"Meshes/RowboatHull.asset");
        var g=new GameObject("Teal hull",typeof(MeshFilter),typeof(MeshRenderer));g.transform.SetParent(boat,false);g.GetComponent<MeshFilter>().sharedMesh=mesh;g.GetComponent<Renderer>().sharedMaterial=_teal;
        var rim=new Vector3[n+1];for(int i=0;i<=n;i++)rim[i]=v[n+i%n];Rope(boat,"Oak gunwale",rim,.085f,_edge);
        for(int band=0;band<2;band++) {var p=new Vector3[n+1];for(int i=0;i<=n;i++)p[i]=Vector3.Lerp(v[i%n],v[n+i%n],.3f+band*.32f);Rope(boat,"Clinker seam",p,.022f,_wood);}
    }
}

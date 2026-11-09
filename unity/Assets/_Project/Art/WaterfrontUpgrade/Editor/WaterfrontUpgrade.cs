using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

// Edit-time authoring only. Existing art and character/network assets are left intact.
public static class WaterfrontUpgrade
{
    private const string Art = "Assets/_Project/Art/WaterfrontUpgrade/";
    private static float Expansion = 1.35f;
    private static string _meshPrefix = "Expanded_";
    private static Transform _detail;
    private static readonly Dictionary<string, Mesh> Meshes = new();

    [MenuItem("DG Village/Waterfront/Upgrade SquareScene once")]
    public static void Apply()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (EditorApplication.isPlaying || scene.path != "Assets/_Project/Scenes/SquareScene.unity")
            throw new InvalidOperationException("Open SquareScene in Edit mode.");
        if (scene.isDirty) throw new InvalidOperationException("Save your edits first.");
        var root = GameObject.Find("Willow Court").transform;
        if (root.Find("14 • Waterfront atelier")) throw new InvalidOperationException("Upgrade already applied.");
        const string backup = Art + "Backups/SquareScene_BeforeWaterfront.unity";
        if (!AssetDatabase.LoadAssetAtPath<SceneAsset>(backup) && !AssetDatabase.CopyAsset(scene.path, backup))
            throw new InvalidOperationException("Could not preserve original scene.");
        Expand(root);
        _detail = new GameObject("14 • Waterfront atelier").transform;
        _detail.SetParent(root, false);
        Water(root);
        WaterfrontPlanting();
        CourtDetails();
        var camera = Camera.main;
        camera.orthographicSize = 10.4f;
        var target = new Vector3(-1.08f, 1.1f, -.9f);
        camera.transform.position = target - camera.transform.forward * 35;
        camera.farClipPlane = Mathf.Max(camera.farClipPlane, 300);
        var vcam = Object.FindAnyObjectByType<Unity.Cinemachine.CinemachineCamera>(FindObjectsInactive.Include);
        if (vcam) { vcam.Lens.OrthographicSize = 10.4f; vcam.transform.SetPositionAndRotation(camera.transform.position, camera.transform.rotation); }
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("Waterfront upgrade saved: 35% wider paths, preserved character scale, new water and bank planting.");
    }

    private static void Expand(Transform root)
    {
        // Expand architectural meshes, not the root Transform: billboard aspect ratios,
        // character scale, elevation, and stair risers remain unchanged.
        var decorations = new HashSet<Transform>();
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
        {
            if (PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject)) decorations.Add(t);
            if (t.name == "Painted canopy" && t.parent) decorations.Add(t.parent);
            if (t.name == "Waterworn bank stone" || t.name == "Notched water lily") decorations.Add(t);
        }
        bool Decorative(Transform t) { for (var p=t;p && p!=root;p=p.parent) if(decorations.Contains(p)) return true; return false; }
        var transforms=root.GetComponentsInChildren<Transform>(true);
        var positions=transforms.ToDictionary(t=>t,t=>t.position);
        foreach (var t in transforms)
        {
            if(t==root) continue;
            if(Decorative(t) && !decorations.Contains(t)) continue;
            var p=positions[t]; t.position=new Vector3(p.x*Expansion,p.y,p.z*Expansion);
        }
        foreach(var filter in root.GetComponentsInChildren<MeshFilter>(true))
        {
            if(Decorative(filter.transform) || !filter.sharedMesh) continue;
            filter.sharedMesh=ExpandedMesh(filter.sharedMesh,filter.transform);
        }
        foreach(var collider in root.GetComponentsInChildren<Collider>(true))
        {
            if(Decorative(collider.transform)) continue;
            if(collider is BoxCollider box) {
                box.size=Vector3.Scale(box.size,new Vector3(Expansion,1,Expansion));
                box.center=Vector3.Scale(box.center,new Vector3(Expansion,1,Expansion));
            }
            else if(collider is MeshCollider mesh && mesh.sharedMesh)
                mesh.sharedMesh=ExpandedMesh(mesh.sharedMesh,mesh.transform);
        }
        // Buildings gain stature while seats and small garden props retain human scale.
        foreach(var t in decorations)
            if(_meshPrefix=="Expanded_" && (t.name=="The waterside academy" || t.name=="Garden keeper cottage" || t.name=="Rotunda" || t.name=="Windmill"))
                t.localScale*=1.16f;
    }

    private static Mesh ExpandedMesh(Mesh original,Transform t)
    {
        var matrix=t.worldToLocalMatrix*Matrix4x4.Scale(new Vector3(Expansion,1,Expansion))*t.localToWorldMatrix;
        matrix.SetColumn(3,new Vector4(0,0,0,1));
        string key=original.GetEntityId()+"_"+t.eulerAngles.ToString()+"_"+t.lossyScale.ToString();
        if(Meshes.TryGetValue(key,out var existing)) return existing;
        var mesh=Object.Instantiate(original);mesh.name=original.name+" Spacious";
        mesh.vertices=original.vertices.Select(matrix.MultiplyVector).ToArray();
        // Architectural UVs are authored in metres. Retain horizontal texel density.
        var uv=mesh.uv;var normals=original.normals;
        for(int i=0;i<uv.Length;i++) {
            bool floor=i<normals.Length && Mathf.Abs(t.TransformDirection(normals[i]).y)>.7f;
            uv[i]=Vector2.Scale(uv[i],new Vector2(Expansion,floor?Expansion:1));
        }
        mesh.uv=uv;mesh.RecalculateNormals();mesh.RecalculateBounds();
        AssetDatabase.CreateAsset(mesh,Art+"Meshes/"+_meshPrefix+Meshes.Count.ToString("D4")+".asset");
        Meshes[key]=mesh;return mesh;
    }

    private static void Water(Transform root)
    {
        var water=root.GetComponentsInChildren<Renderer>().First(r=>r.name=="Canal water");
        // A baked distance field follows the actual masonry footprints, including bridges.
        var banks=root.GetComponentsInChildren<Renderer>()
            .Where(r=>r!=water && r.bounds.min.y<-.65f && r.bounds.max.y> -1.2f && r.bounds.size.x>.6f && r.bounds.size.z>.6f)
            .Select(r=>r.bounds).ToArray();
        const int size=1024;var texture=new Texture2D(size,size,TextureFormat.RGB24,false,true);
        var pixels=new Color32[size*size];
        for(int y=0;y<size;y++) for(int x=0;x<size;x++) {
            float wx=-110+(x+.5f)/size*230,wz=-115+(y+.5f)/size*230,distance=12;
            foreach(var b in banks) {
                float dx=Mathf.Max(b.min.x-wx,0,wx-b.max.x),dz=Mathf.Max(b.min.z-wz,0,wz-b.max.z);
                distance=Mathf.Min(distance,Mathf.Sqrt(dx*dx+dz*dz));
            }
            byte value=(byte)Mathf.RoundToInt(distance/12*255);pixels[y*size+x]=new Color32(value,value,value,255);
        }
        texture.SetPixels32(pixels);texture.Apply();
        string path=Art+"Textures/BankDistance.png";File.WriteAllBytes(path,texture.EncodeToPNG());Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(path);
        var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.sRGBTexture=false;importer.mipmapEnabled=false;
        importer.wrapMode=TextureWrapMode.Clamp;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();
        var material=AssetDatabase.LoadAssetAtPath<Material>(Art+"Materials/GardenWater.mat");
        if(!material) {
            material=new Material(Shader.Find("DG Village/Waterfront/Garden Water"));material.name="Deep garden canal";
            AssetDatabase.CreateAsset(material,Art+"Materials/GardenWater.mat");
        }
        material.SetTexture("_ShoreMap",AssetDatabase.LoadAssetAtPath<Texture2D>(path));
        EditorUtility.SetDirty(material);water.sharedMaterial=material;
    }

    [MenuItem("DG Village/Waterfront/Raise land and enlarge once")]
    public static void RaiseAndEnlarge()
    {
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if(EditorApplication.isPlaying || scene.path!="Assets/_Project/Scenes/SquareScene.unity" || scene.isDirty)
            throw new InvalidOperationException("Save SquareScene in Edit mode first.");
        var root=GameObject.Find("Willow Court").transform;
        if(!root.Find("14 • Waterfront atelier") || root.Find("15 • Higher terraces"))
            throw new InvalidOperationException("Needs the first upgrade, and can only run once.");
        if(!AssetDatabase.CopyAsset(scene.path,Art+"Backups/SquareScene_BeforeRaisedTerraces.unity"))
            throw new InvalidOperationException("Could not back up the current scene.");
        Expansion=1.2f;_meshPrefix="Raised_";Meshes.Clear();Expand(root);
        const float rise=1.2f;
        var aquatic=new HashSet<Transform>();
        foreach(var t in root.GetComponentsInChildren<Transform>(true)) {
            if(t.name=="Canal water" || t.name=="Waterworn bank stone" || t.name=="Notched water lily" || t.name=="Water lily leaf") aquatic.Add(t);
            if(t.position.y<-.8f && PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject)) aquatic.Add(t);
            if(t.name=="Painted canopy" && t.parent.position.y<-.8f) aquatic.Add(t.parent);
        }
        var waterPositions=aquatic.Where(t=>!aquatic.Any(a=>a!=t && t.IsChildOf(a))).ToDictionary(t=>t,t=>t.position);
        var foundations=root.GetComponentsInChildren<MeshRenderer>().Where(r=>r.name!="Canal water" && r.bounds.min.y<-.4f &&
            (r.name.Contains("foundation") || r.name.Contains("Heights block") || r.name=="Bridge body" || r.name=="Bridge arch body" || r.name=="Buttress")).ToArray();
        root.position+=Vector3.up*rise;
        foreach(var pair in waterPositions)pair.Key.position=pair.Value;
        // Extend submerged masonry down, retaining its old bottom while lifting the top.
        int index=0;
        foreach(var r in foundations) {
            var filter=r.GetComponent<MeshFilter>();if(!filter || !filter.sharedMesh)continue;
            var mesh=Object.Instantiate(filter.sharedMesh);var b=mesh.bounds;
            float localRise=rise/r.transform.lossyScale.y;
            var vertices=mesh.vertices;var uv=mesh.uv;var normals=mesh.normals;
            for(int i=0;i<vertices.Length;i++) {
                float amount=r.name=="Bridge body"
                    ? Mathf.Clamp01(-vertices[i].y/.4f)
                    : 1-Mathf.InverseLerp(b.min.y,b.max.y,vertices[i].y);
                vertices[i].y-=localRise*amount;
                if(i<uv.Length && i<normals.Length && Mathf.Abs(normals[i].y)<.5f)
                    uv[i].y-=localRise*amount;
            }
            mesh.vertices=vertices;mesh.uv=uv;mesh.RecalculateNormals();mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh,Art+"Meshes/TallFoundation_"+(index++).ToString("D3")+".asset");filter.sharedMesh=mesh;
            if(r.TryGetComponent<BoxCollider>(out var box)) {
                box.center-=Vector3.up*localRise*.5f;box.size+=Vector3.up*localRise;
            }
            if(r.TryGetComponent<MeshCollider>(out var collider))collider.sharedMesh=mesh;
        }
        new GameObject("15 • Higher terraces").transform.SetParent(root,false);
        Water(root);
        var placement=root.gameObject.AddComponent<SquareSpawnPlacement>();
        var surfaces=root.GetComponentsInChildren<Collider>().Where(c=>
            c.name=="Lower court" || c.name=="Academy terrace" || c.name=="Terrace paving" || c.name=="East garden walk" ||
            c.name=="Bridge walk" || c.name=="Bridge body" || c.name=="South bank" || c.name=="South garden path" ||
            c.name=="Stair walking ramp" || c.name=="Stair landing" || c.name=="East forecourt paving" || c.name.StartsWith("Heights paving")).ToArray();
        var so=new SerializedObject(placement);var array=so.FindProperty("_surfaces");array.arraySize=surfaces.Length;
        for(int i=0;i<surfaces.Length;i++)array.GetArrayElementAtIndex(i).objectReferenceValue=surfaces[i];
        so.FindProperty("_fallback").vector3Value=new Vector3(-1.62f,1.33f,-6);
        so.ApplyModifiedPropertiesWithoutUndo();
        var camera=Camera.main;camera.orthographicSize=8.3f;
        camera.transform.position=new Vector3(-1.3f,2.3f,-1.1f)-camera.transform.forward*40;
        var vcam=Object.FindAnyObjectByType<Unity.Cinemachine.CinemachineCamera>(FindObjectsInactive.Include);
        if(vcam){vcam.Lens.OrthographicSize=8.3f;vcam.transform.SetPositionAndRotation(camera.transform.position,camera.transform.rotation);}
        Physics.SyncTransforms();AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
    }

    private static GameObject Prop(string name,Vector3 position,float scale,bool flip=false)
    {
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Art/Square/Props/"+name+".prefab");
        var g=(GameObject)PrefabUtility.InstantiatePrefab(prefab,_detail);g.name="Atelier • "+name;
        g.transform.position=position;g.transform.localScale=Vector3.one*scale;
        if(flip) {var sprite=g.transform.Find("Sprite");if(sprite) {var s=sprite.localScale;s.x*=-1;sprite.localScale=s;}}
        return g;
    }

    private static Material NewLit(string name,Color color)
    {
        var m=new Material(Shader.Find("Universal Render Pipeline/Lit"));m.name=name;m.color=color;m.SetFloat("_Smoothness",.18f);
        AssetDatabase.CreateAsset(m,Art+"Materials/"+name+".mat");return m;
    }

    private static void WaterfrontPlanting()
    {
        var stone=NewLit("River limestone",new Color(.36f,.40f,.34f));
        var moss=NewLit("Lily jade",new Color(.21f,.34f,.17f));
        var rng=new System.Random(20261110);
        // Sparse asymmetrical islands at the banks, clear of the playable bridges.
        var patches=new[]{new Vector2(-16.6f,-9),new Vector2(-12,-15.4f),new Vector2(10.5f,-15.5f),
            new Vector2(-20.9f,-31),new Vector2(19.5f,-37),new Vector2(25.8f,-52),new Vector2(65,-40)};
        foreach(var center in patches) {
            for(int i=0;i<7;i++) {
                float a=(float)rng.NextDouble()*Mathf.PI*2,r=(float)rng.NextDouble()*1.4f;
                var p=new Vector3(center.x+Mathf.Cos(a)*r,-1.28f,center.y+Mathf.Sin(a)*r*.6f);
                var rock=GameObject.CreatePrimitive(PrimitiveType.Sphere);rock.name="Waterworn bank stone";
                rock.transform.SetParent(_detail,false);rock.transform.position=p;
                rock.transform.localScale=new Vector3(.4f+(float)rng.NextDouble()*.6f,.22f+(float)rng.NextDouble()*.35f,.4f+(float)rng.NextDouble()*.45f);
                rock.GetComponent<Renderer>().sharedMaterial=stone;Object.DestroyImmediate(rock.GetComponent<Collider>());
                if(i%2==0)Prop("grass_tall",p+Vector3.up*.15f,.38f+(float)rng.NextDouble()*.2f,i%3==0);
            }
            for(int i=0;i<9;i++) {
                float a=(float)rng.NextDouble()*6.28f,r=1.1f+(float)rng.NextDouble()*1.5f;
                var leaf=new GameObject("Notched water lily",typeof(MeshFilter),typeof(MeshRenderer));leaf.transform.SetParent(_detail,false);
                leaf.transform.position=new Vector3(center.x+Mathf.Cos(a)*r,-1.229f,center.y+Mathf.Sin(a)*r);
                leaf.transform.localRotation=Quaternion.Euler(0,(float)rng.NextDouble()*360,0);
                leaf.transform.localScale=Vector3.one*(.15f+(float)rng.NextDouble()*.14f);
                leaf.GetComponent<MeshFilter>().sharedMesh=LilyMesh();leaf.GetComponent<Renderer>().sharedMaterial=moss;
                leaf.GetComponent<Renderer>().shadowCastingMode=ShadowCastingMode.Off;
            }
        }
    }

    private static Mesh LilyMesh()
    {
        if(Meshes.TryGetValue("Lily",out var m))return m;
        var vertices=new List<Vector3>{Vector3.zero};var triangles=new List<int>();
        for(int i=0;i<=24;i++) {
            float a=(20+i*320f/24)*Mathf.Deg2Rad;vertices.Add(new Vector3(Mathf.Cos(a),0,Mathf.Sin(a)*.82f));
            if(i>0)triangles.AddRange(new[]{0,i+1,i});
        }
        m=new Mesh{name="Notched lily leaf"};m.SetVertices(vertices);m.SetTriangles(triangles,0);m.RecalculateNormals();
        AssetDatabase.CreateAsset(m,Art+"Meshes/Lily.asset");Meshes["Lily"]=m;return m;
    }

    private static void CourtDetails()
    {
        // Human-scale punctuation along the widened walks; the centre remains traversable.
        foreach(var p in new[]{new Vector3(-12.7f,.05f,-10),new Vector3(4.7f,.05f,-11.8f),new Vector3(-13.6f,1.56f,6.7f),new Vector3(9.0f,1.56f,10)})
            Prop("lamp_iron",p,.95f);
        foreach(var p in new[]{new Vector3(-8.3f,1.56f,8.2f),new Vector3(6.8f,1.56f,9.5f),new Vector3(-11,.05f,-7.4f)}) {
            Prop("planter",p,.72f);Prop("daisies",p+new Vector3(.65f,.02f,.35f),.36f);
        }
        Prop("bench",new Vector3(7.0f,1.56f,7.1f),.95f,true);
        Prop("table",new Vector3(8.6f,1.56f,7.0f),1.2f);
        // Fill the newly widened planted beds using their actual surface bounds.
        var root=_detail.parent;var rng=new System.Random(722);
        foreach(var r in root.GetComponentsInChildren<MeshRenderer>().Where(r=>r.name=="Bed soil" || r.name=="Planter soil").ToArray()) {
            var b=r.bounds;
            if(b.size.x<1 || b.size.z<1)continue;
            int count=Mathf.Clamp(Mathf.RoundToInt(b.size.x*b.size.z*.18f),2,10);
            for(int i=0;i<count;i++) {
                var p=new Vector3(Mathf.Lerp(b.min.x+.3f,b.max.x-.3f,(float)rng.NextDouble()),b.max.y+.02f,Mathf.Lerp(b.min.z+.3f,b.max.z-.3f,(float)rng.NextDouble()));
                Prop(i%3==0?"marigolds":"shrub",p,i%3==0?.35f:.42f,i%2==0);
            }
        }
    }
}

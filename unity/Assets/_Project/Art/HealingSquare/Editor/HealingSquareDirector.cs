using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

// Editor-only scene authoring. All geometry, materials and prefabs are saved assets;
// the finished scene does not depend on this generator at runtime.
public static class HealingSquareDirector
{
    private const string Project = "Assets/_Project/";
    private const string Art = Project + "Art/HealingSquare/";
    private static Transform _root;
    private static readonly Dictionary<string, Material> Materials = new();
    private static readonly Dictionary<string, Mesh> Meshes = new();

    [MenuItem("DG Village/Square/Create healing garden copy")]
    public static void Build()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        const string output = Project + "Scenes/SquareScene_Healing.unity";
        if (EditorApplication.isPlaying || scene.path != Project + "Scenes/SquareScene.unity")
            throw new InvalidOperationException("Open original SquareScene in Edit mode.");
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(output))
            throw new InvalidOperationException("Healing scene exists. Edit it directly to preserve your work.");
        if (!EditorSceneManager.SaveScene(scene, output, true)) throw new Exception("Scene copy failed.");
        scene = EditorSceneManager.OpenScene(output);
        EnsureFolder(Art.TrimEnd('/')); EnsureFolder(Art+"Meshes");
        Materials.Clear(); Meshes.Clear();
        var village=GameObject.Find("VillageSquare").transform;
        foreach(Transform group in village)
            if(group.name.StartsWith("03") || group.name.StartsWith("04")) group.gameObject.SetActive(false);
        var paving=village.Cast<Transform>().First(t=>t.name.StartsWith("01"));
        foreach(Transform t in paving)
            if(t.name.Contains("medallion"))t.gameObject.SetActive(false);
        _root=new GameObject("Healing Garden • authored layout").transform;
        var ground=Group("01 • Circular promenade");
        var flowers=Group("02 • Cottage flower gardens");
        var seats=Group("03 • Reading & tea corners");
        var trees=Group("04 • Canopy frame");
        var details=Group("05 • Shop life");
        // All paving is visual over the original continuous collision floor.
        Box(ground,"Garden lawn",0,.033f,0,31.4f,.018f,29.4f,"Meadow",false);
        Box(ground,"South walk border",0,.048f,-8,3.7f,.02f,14,"Limestone",false);
        Box(ground,"South walk",0,.062f,-8,3.35f,.02f,14,"Road",false);
        Box(ground,"Academy walk border",0,.048f,10,4.2f,.02f,10,"Limestone",false);
        Box(ground,"Academy walk",0,.062f,10,3.85f,.02f,10,"Road",false);
        Box(ground,"Cross walk border",0,.05f,2.4f,31.4f,.02f,3.2f,"Limestone",false);
        Box(ground,"Cross walk",0,.066f,2.4f,31.4f,.02f,2.85f,"Road",false);
        Disc(ground,"Promenade outer limestone",new Vector3(-.6f,.083f,2.4f),5.8f,5.8f,"Limestone");
        Disc(ground,"Quiet stone court",new Vector3(-.6f,.09f,2.4f),5.57f,5.57f,"Road");
        Disc(ground,"Fountain warm inset",new Vector3(-.6f,.097f,2.4f),3.05f,3.05f,"Plaza");
        Disc(ground,"Basin stone edging",new Vector3(-.6f,.104f,2.4f),1.9f,1.9f,"Limestone");
        Crescent(flowers,new Vector3(-.6f,0,2.4f),5.95f,7.65f,18,70,0);
        Crescent(flowers,new Vector3(-.6f,0,2.4f),5.95f,7.65f,111,160,1);
        Crescent(flowers,new Vector3(-.6f,0,2.4f),5.95f,7.5f,201,250,2);
        Crescent(flowers,new Vector3(-.6f,0,2.4f),5.95f,7.5f,290,339,3);
        Prop(seats,"bench",-4.4f,.1f,.2f,.85f);
        Prop(seats,"bench",3.8f,.1f,1,.85f,true);
        Prop(seats,"bench",-2.8f,.1f,6.7f,.85f);
        Prop(seats,"fern_pot",-5.2f,.1f,1,.65f);
        Prop(seats,"planter",4.8f,.1f,.4f,.62f);
        foreach(var p in new[]{new Vector2(-3.5f,-2.5f),new Vector2(3,-2.5f),new Vector2(-3.6f,7.4f),new Vector2(3,7.4f)})
            Prop(seats,"lamp_vine",p.x,.08f,p.y,.75f);
        Island(trees,8.8f,7.4f,3.2f,2.5f);
        Prop(trees,"tree_oak",9,.15f,8,.67f);
        Island(trees,-8.6f,7.2f,2.5f,2.3f);
        Prop(trees,"tree_birch",-8.6f,.15f,7.7f,.85f);
        Prop(trees,"tree_birch",-10.2f,.15f,9.5f,.65f,true);
        Island(trees,9.2f,-7.9f,2.8f,2.1f);
        Prop(trees,"tree_maple",11.2f,.12f,-7,.48f);
        Island(trees,-9.3f,-8.9f,2.8f,2.1f);
        Prop(trees,"tree_birch",-11.2f,.12f,-8,.65f);
        Disc(details,"Tea patio border",new Vector3(-10,.075f,-4.3f),3.1f,2.4f,"Limestone");
        Disc(details,"Tea patio brick",new Vector3(-10,.081f,-4.3f),2.9f,2.2f,"Plaza");
        Prop(details,"table",-10,.1f,-4,1.5f);
        Prop(details,"bench",-11.4f,.1f,-4,.72f);
        Prop(details,"bench",-8.8f,.1f,-3.6f,.72f,true);
        Prop(details,"trellis_rose",-12.3f,.1f,-3,.9f);
        Prop(details,"planter",-10.8f,.1f,-6,.65f);
        Prop(details,"lantern_low",-9.2f,.1f,-5.8f,.6f);
        for(int i=0;i<5;i++){
            Prop(details,i%2==0?"fern_pot":"planter",-13+i*.65f,.08f,4.4f,.6f);
            Prop(details,"hollyhock",-14+i*.62f,.08f,8.8f,.55f);
        }
        Prop(details,"trellis_rose",-10,.08f,6,.85f);
        Prop(details,"crates",11,.08f,5.2f,.65f);
        Prop(details,"fern_pot",10.1f,.08f,5,.65f);
        Prop(details,"lantern_low",11.7f,.08f,4.8f,.65f);
        for(int side=-1;side<=1;side+=2){
            for(int i=0;i<3;i++){
                Island(flowers,side*(4.2f+i*.35f),-10.5f+i*2.2f,1.15f,1.05f);
                Prop(flowers,"hollyhock",side*(4.3f+i*.35f),.12f,-10.4f+i*2.2f,.5f);
            }
            Prop(details,"lamp_iron",side*2.5f,.08f,-10.8f,.8f);
            Prop(details,"bench",side*7,.95f,18,.8f,side>0);
            Prop(details,"lantern_low",side*3.35f,1.52f,13.25f,.65f);
            for(int i=0;i<3;i++)Prop(details,"planter",side*(6+i*3.4f),1.58f,15.1f,.65f);
        }
        for(int i=0;i<5;i++)
            Disc(details,"Tea stepping stone",new Vector3(-5.8f-i*.65f,.082f,-1.5f-i*.48f),.43f,.33f,"Limestone");
        var sun=GameObject.Find("Directional Light").GetComponent<Light>();
        sun.transform.rotation=Quaternion.Euler(48,-28,0);sun.color=new Color(1,.95f,.86f);sun.intensity=1.12f;sun.shadowStrength=.5f;
        RenderSettings.ambientMode=AmbientMode.Trilight;
        RenderSettings.ambientSkyColor=new Color(.72f,.78f,.83f);
        RenderSettings.ambientEquatorColor=new Color(.69f,.73f,.63f);
        RenderSettings.ambientGroundColor=new Color(.44f,.45f,.37f);
        var volume=Object.FindAnyObjectByType<Volume>();
        var profile=Object.Instantiate(volume.sharedProfile);profile.name="Healing daylight";
        profile.components=new List<VolumeComponent>();
        AssetDatabase.CreateAsset(profile,Art+"HealingDaylight.asset");
        foreach(var c in volume.sharedProfile.components){var clone=Object.Instantiate(c);profile.components.Add(clone);AssetDatabase.AddObjectToAsset(clone,profile);}
        if(profile.TryGet<ColorAdjustments>(out var grade)){grade.saturation.value=-1;grade.contrast.value=3;grade.postExposure.value=.08f;}
        if(profile.TryGet<Vignette>(out var vignette))vignette.intensity.value=.1f;
        volume.sharedProfile=profile;
        FinishLayout();
        AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        Debug.Log("Healing square created; source scene and shared assets preserved.");
    }

    public static void FinishLayout()
    {
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if(EditorApplication.isPlaying || scene.path!=Project+"Scenes/SquareScene_Healing.unity")
            throw new InvalidOperationException("Open healing square in Edit mode.");
        _root=GameObject.Find("Healing Garden • authored layout").transform;
        if(_root.Find("06 • Garden finishing"))
            throw new InvalidOperationException("Finishing is already applied; edit scene objects directly.");
        var village=GameObject.Find("VillageSquare").transform;
        RestoreApproach(village);
        var buildings=village.Cast<Transform>().First(t=>t.name.StartsWith("02"));
        // City art uses folded meshes: the lower portion lies on the ground.
        // Lift that painted footprint above the new paving, rather than hide it.
        foreach(Transform t in buildings){
            var p=t.position;if(p.y<.2f){p.y=t.name=="Meeting fountain"?.13f:.085f;t.position=p;}
        }
        var details=Group("06 • Garden finishing");
        Island(details,-8.5f,12.1f,3.4f,1.45f);
        Island(details,8.4f,12.5f,3.5f,1.3f);
        for(int side=-1;side<=1;side+=2){
            for(int i=0;i<8;i++){
                Prop(details,i%3==0?"hollyhock":"daisies",side*(5.8f+i*.7f),.12f,12.2f+(i%2)*.35f,i%3==0?.5f:.6f);
            }
            for(int i=0;i<8;i++){
                Prop(details,i%3==0?"shrub":"grass_tall",side*(12.5f+(i%2)*.6f),.08f,-9.5f+i*1.6f,i%3==0?.45f:.28f,i%2==0);
            }
        }
        // A few relaxed clusters break up the lawn without filling walkways.
        var rng=new System.Random(928);
        foreach(var p in new[]{new Vector2(-6.2f,-7),new Vector2(6.2f,-7),new Vector2(10.7f,-1.7f),new Vector2(-9.7f,1),new Vector2(4.8f,10.6f)}){
            for(int i=0;i<7;i++){
                float a=i*2.4f,r=.35f+(float)rng.NextDouble()*.8f;
                Prop(details,i%3==0?"daisies":"grass_tall",p.x+Mathf.Cos(a)*r,.08f,p.y+Mathf.Sin(a)*r,.23f+(float)rng.NextDouble()*.12f,i%2==0);
            }
        }
        // Unobstructed visual stepping stones to each shop entrance.
        for(int side=-1;side<=1;side+=2)
            for(int i=0;i<3;i++)
                Disc(details,"Shop stepping stone",new Vector3(side*11.5f,.09f,4.1f+i*.58f),.48f,.36f,"Limestone");
        AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
    }

    private static void RestoreApproach(Transform village)
    {
        foreach(Transform group in village){
            if(group.name.StartsWith("03")){
                group.gameObject.SetActive(true);
                foreach(Transform bed in group){
                    var tree=bed.Cast<Transform>().FirstOrDefault(t=>t.name.StartsWith("tree_"));
                    bed.gameObject.SetActive(tree && tree.position.z < -16);
                }
            }
            if(group.name.StartsWith("04")){
                group.gameObject.SetActive(true);
                foreach(Transform prop in group)prop.gameObject.SetActive(prop.position.z < -16);
            }
            if(group.name.StartsWith("01"))
                foreach(Transform t in group)
                    if(t.name=="East lane" || t.name=="West lane"){var p=t.position;p.z=2.4f;t.position=p;}
        }
    }

    private static void Island(Transform parent,float x,float z,float rx,float rz)
    {
        Disc(parent,"Soft garden edging",new Vector3(x,.08f,z),rx,rz,"Limestone");
        Disc(parent,"Garden moss",new Vector3(x,.09f,z),rx-.16f,rz-.16f,"Meadow");
        for(int i=0;i<9;i++){
            float a=i*2.39996f,r=.35f+.6f*(i%3)/2;
            Prop(parent,i%3==0?"shrub":i%3==1?"daisies":"marigolds",x+Mathf.Cos(a)*rx*r,.1f,z+Mathf.Sin(a)*rz*r,i%3==0?.45f:.55f,i%2==0);
        }
    }

    private static void Crescent(Transform parent,Vector3 center,float inner,float outer,float start,float end,int seed)
    {
        var group=new GameObject("Crescent garden "+seed).transform;group.SetParent(parent,false);
        Arc(group,"Limestone crescent rim",center+Vector3.up*.085f,inner,outer,start,end,"Limestone");
        Arc(group,"Moss and flower bed",center+Vector3.up*.095f,inner+.15f,outer-.15f,start+1,end-1,"Meadow");
        var rng=new System.Random(831+seed);
        for(int row=0;row<3;row++)for(int i=0;i<12;i++){
            float a=Mathf.Lerp(start+3,end-3,(i+.35f*(row%2))/12f)*Mathf.Deg2Rad;
            float r=inner+.35f+row*.48f+(float)rng.NextDouble()*.16f;
            var p=center+new Vector3(Mathf.Cos(a)*r,.11f,Mathf.Sin(a)*r);
            string prop=row==2 && i%3==0?"shrub":(i+seed)%5==0?"hollyhock":(i+seed)%3==0?"marigolds":"daisies";
            Prop(group,prop,p.x,p.y,p.z,prop=="hollyhock"?.37f:prop=="shrub"?.36f:.45f+(float)rng.NextDouble()*.15f,i%2==0);
        }
    }

    private static void Arc(Transform parent,string name,Vector3 position,float inner,float outer,float start,float end,string material)
    {
        string key=FormattableString.Invariant($"Arc_{inner}_{outer}_{start}_{end}");
        var mesh=SavedMesh(key,()=>{
            const int count=32;var v=new Vector3[(count+1)*2];var uv=new Vector2[v.Length];var tri=new int[count*6];
            for(int i=0;i<=count;i++){
                float a=Mathf.Lerp(start,end,(float)i/count)*Mathf.Deg2Rad;
                for(int j=0;j<2;j++){float r=j==0?inner:outer;v[i*2+j]=new Vector3(Mathf.Cos(a)*r,0,Mathf.Sin(a)*r);uv[i*2+j]=new Vector2(v[i*2+j].x,v[i*2+j].z);}
                if(i<count){int t=i*6,k=i*2;tri[t]=k;tri[t+1]=k+2;tri[t+2]=k+1;tri[t+3]=k+1;tri[t+4]=k+2;tri[t+5]=k+3;}
            }
            var m=new Mesh{vertices=v,uv=uv,triangles=tri};m.RecalculateNormals();m.RecalculateBounds();return m;
        });
        var g=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));g.transform.SetParent(parent,false);g.transform.position=position;
        g.GetComponent<MeshFilter>().sharedMesh=mesh;g.GetComponent<Renderer>().sharedMaterial=Mat(material);g.isStatic=true;
    }
    private static void EnsureFolder(string path)
    {
        if(AssetDatabase.IsValidFolder(path)) return;
        int slash=path.LastIndexOf('/');
        EnsureFolder(path[..slash]);
        AssetDatabase.CreateFolder(path[..slash],path[(slash+1)..]);
    }

    private static Transform Group(string name)
    {
        var t=new GameObject(name).transform;
        t.SetParent(_root,false);
        return t;
    }

    private static Material Mat(string name,string source=null,Color? tint=null,float tile=1)
    {
        if(Materials.TryGetValue(name,out var cached)) return cached;
        string path=Art+name+".mat";
        var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(!mat)
        {
            if(source==null)
                source=name switch {"Plaza"=>"City/Floor_Plaza","Road"=>"City/Floor_Road","Wall"=>"City/Wall_Stone","Limestone"=>"City/Cap_Stone","Soil"=>"Garden/_ForestFloor",_=>"Garden/_Meadow"};
            mat=new Material(AssetDatabase.LoadAssetAtPath<Material>(Project+"Materials/"+source+".mat"));
            mat.name=name;
            mat.mainTextureScale=Vector2.one*(tile==1 ? name switch {"Plaza"=>.35f,"Road"=>.35f,"Wall"=>.6f,"Limestone"=>.6f,"Soil"=>.4f,_=>.2f} : tile);
            if(tint.HasValue) mat.color=tint.Value;
            mat.SetFloat("_Smoothness",.12f);
            AssetDatabase.CreateAsset(mat,path);
        }
        if(name=="Meadow")
        {
            mat.mainTexture=MeadowTexture();
            mat.mainTextureScale=Vector2.one*.125f;
            mat.color=Color.white;
            mat.SetFloat("_AlphaClip",0);mat.DisableKeyword("_ALPHATEST_ON");mat.renderQueue=2000;
            EditorUtility.SetDirty(mat);
        }
        Materials[name]=mat;
        return mat;
    }

    private static Texture2D MeadowTexture()
    {
        string path=Art+"MeadowSurface.asset";
        var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if(texture)return texture;
        const int size=512;
        texture=new Texture2D(size,size,TextureFormat.RGB24,true){name="MeadowSurface",wrapMode=TextureWrapMode.Repeat,filterMode=FilterMode.Bilinear,anisoLevel=4};
        var pixels=new Color[size*size];
        for(int y=0;y<size;y++)for(int x=0;x<size;x++)
        {
            float u=(float)x/size,v=(float)y/size;
            float n=TileNoise(u,v,4)*.62f+TileNoise(u,v,16)*.28f+TileNoise(u,v,64)*.1f;
            n=Mathf.Round(n*10)/10;
            pixels[y*size+x]=Color.Lerp(new Color(.39f,.47f,.25f),new Color(.49f,.56f,.32f),n);
        }
        var rng=new System.Random(211);
        for(int i=0;i<950;i++)
        {
            int x=rng.Next(size),y=rng.Next(size);
            Color grass=Color.Lerp(pixels[y*size+x],new Color(.65f,.67f,.40f),.2f);
            for(int j=0;j<3;j++)pixels[((y+j)%size)*size+(x+j/2)%size]=grass;
        }
        texture.SetPixels(pixels);texture.Apply(true,false);AssetDatabase.CreateAsset(texture,path);return texture;
    }

    private static float TileNoise(float u,float v,int cells)
    {
        float x=u*cells,y=v*cells;int ix=Mathf.FloorToInt(x),iy=Mathf.FloorToInt(y);
        float tx=Mathf.SmoothStep(0,1,x-ix),ty=Mathf.SmoothStep(0,1,y-iy);
        float Hash(int a,int b){uint h=(uint)((a%cells)*374761393+(b%cells)*668265263);h=(h^(h>>13))*1274126177;return (h&65535)/65535f;}
        return Mathf.Lerp(Mathf.Lerp(Hash(ix,iy),Hash(ix+1,iy),tx),Mathf.Lerp(Hash(ix,iy+1),Hash(ix+1,iy+1),tx),ty);
    }

    private static Mesh SavedMesh(string key,Func<Mesh> create)
    {
        if(Meshes.TryGetValue(key,out var mesh))return mesh;
        string path=Art+"Meshes/"+key+".asset";
        mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if(!mesh){mesh=create();mesh.name=key;AssetDatabase.CreateAsset(mesh,path);}
        Meshes[key]=mesh;
        return mesh;
    }

    private static GameObject Box(Transform parent,string name,float x,float y,float z,float w,float h,float d,string material,bool collide=true)
    {
        string key=FormattableString.Invariant($"Box_{w:0.###}_{h:0.###}_{d:0.###}");
        var mesh=SavedMesh(key,()=>
        {
            // Face-local metre UVs preserve texture density on every modular piece.
            var temp=GameObject.CreatePrimitive(PrimitiveType.Cube);
            var m=Object.Instantiate(temp.GetComponent<MeshFilter>().sharedMesh);
            Object.DestroyImmediate(temp);
            var v=m.vertices;var n=m.normals;var uv=new Vector2[v.Length];
            for(int i=0;i<v.Length;i++)
            {
                v[i]=Vector3.Scale(v[i],new Vector3(w,h,d));
                uv[i]=Mathf.Abs(n[i].y)>.5f?new Vector2(v[i].x,v[i].z):Mathf.Abs(n[i].x)>.5f?new Vector2(v[i].z,v[i].y):new Vector2(v[i].x,v[i].y);
            }
            m.vertices=v;m.uv=uv;m.RecalculateBounds();return m;
        });
        var g=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));
        g.transform.SetParent(parent,false);g.transform.position=new Vector3(x,y,z);
        g.GetComponent<MeshFilter>().sharedMesh=mesh;
        g.GetComponent<Renderer>().sharedMaterial=Mat(material);
        if(collide)g.AddComponent<BoxCollider>().size=new Vector3(w,h,d);
        g.isStatic=true;
        return g;
    }

    private static void Disc(Transform parent,string name,Vector3 position,float rx,float rz,string material)
    {
        string key=FormattableString.Invariant($"Disc_{rx:0.###}_{rz:0.###}");
        var mesh=SavedMesh(key,()=>
        {
            var v=new Vector3[65];var uv=new Vector2[65];var tris=new int[64*3];
            for(int i=0;i<64;i++)
            {
                float a=i*Mathf.PI*2/64;v[i+1]=new Vector3(Mathf.Cos(a)*rx,0,Mathf.Sin(a)*rz);
                uv[i+1]=new Vector2(v[i+1].x,v[i+1].z);
                tris[i*3]=0;tris[i*3+1]=(i+1)%64+1;tris[i*3+2]=i+1;
            }
            var m=new Mesh{vertices=v,uv=uv,triangles=tris};m.RecalculateNormals();m.RecalculateBounds();return m;
        });
        var g=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));g.transform.SetParent(parent,false);g.transform.position=position;
        g.GetComponent<MeshFilter>().sharedMesh=mesh;g.GetComponent<Renderer>().sharedMaterial=Mat(material);g.isStatic=true;
    }

    private static GameObject Prop(Transform parent,string name,float x,float y,float z,float scale=1,bool flip=false)
    {
        var asset=AssetDatabase.LoadAssetAtPath<GameObject>(Project+"Art/Square/Props/"+name+".prefab");
        var g=(GameObject)PrefabUtility.InstantiatePrefab(asset,parent);
        g.transform.position=new Vector3(x,y,z);g.transform.localScale=Vector3.one*scale;g.SetActive(true);
        if(name.StartsWith("tree_") || name=="bench" || name=="crates" || name=="table" || name=="planter" || name=="fern_pot")
            foreach(var previous in g.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(previous);
        if(flip)
        {
            var sprite=g.transform.Find("Sprite");
            if(sprite){var s=sprite.localScale;s.x=-s.x;sprite.localScale=s;}
        }
        // Collision follows the footprint, never the full billboard rectangle.
        if(name.StartsWith("tree_"))
        {
            var trunk=g.AddComponent<CapsuleCollider>();trunk.radius=.42f;trunk.height=2;trunk.center=Vector3.up;
            if(!g.transform.Find("Blob"))
            {
                var shadow=GameObject.CreatePrimitive(PrimitiveType.Quad);shadow.name="Canopy contact shadow";
                Object.DestroyImmediate(shadow.GetComponent<Collider>());
                shadow.transform.SetParent(g.transform,false);shadow.transform.localPosition=new Vector3(0,.035f,0);shadow.transform.localRotation=Quaternion.Euler(90,0,0);shadow.transform.localScale=new Vector3(4.2f,3,1);
                var renderer=shadow.GetComponent<Renderer>();renderer.sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>(Project+"Materials/BlobShadow.mat");renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            }
        }
        else if(name=="bench"||name=="crates"||name=="table"||name=="planter"||name=="fern_pot")
        {
            var collider=g.AddComponent<BoxCollider>();float width=name=="bench"?1.7f:name=="table"?.65f:.75f;
            collider.size=new Vector3(width,.8f,.65f);collider.center=new Vector3(0,.4f,0);
            if(name=="bench")
            {
                // Orient a child collider to the same diagonal as the painted bench.
                Object.DestroyImmediate(collider);
                var foot=new GameObject("Seat footprint",typeof(BoxCollider));foot.transform.SetParent(g.transform,false);foot.transform.localRotation=Quaternion.Euler(0,flip?-45:45,0);
                var seat=foot.GetComponent<BoxCollider>();seat.center=new Vector3(0,.4f,0);seat.size=new Vector3(width,.8f,.65f);
            }
        }
        return g;
    }

}

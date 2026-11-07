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
public static class SquareArtDirector
{
    private const string Project = "Assets/_Project/";
    private const string Art = Project + "Art/Square/";
    private const string Archive = Project + "Editor/Reference/OriginalCity.prefab";
    private static Transform _root;
    private static readonly Dictionary<string, Material> Materials = new();
    private static readonly Dictionary<string, Mesh> Meshes = new();

    [MenuItem("DG Village/Square/Rebuild art layout")]
    public static void Build()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (EditorApplication.isPlaying || scene.path != Project + "Scenes/SquareScene.unity")
            throw new InvalidOperationException("Open SquareScene in Edit mode first.");

        EnsureFolder(Art.TrimEnd('/'));
        EnsureFolder(Art + "Props");
        EnsureFolder(Art + "Meshes");
        EnsureFolder(Project + "Editor/Reference");
        Materials.Clear();
        Meshes.Clear();
        var city = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "City");
        if(city && !AssetDatabase.LoadAssetAtPath<GameObject>(Archive))
            PrefabUtility.SaveAsPrefabAsset(city,Archive);
        if(!city)city=AssetDatabase.LoadAssetAtPath<GameObject>(Archive);
        if(!city)throw new InvalidOperationException("Original City source is missing.");
        var templates = city.transform.Find("_Templates");
        foreach (Transform t in templates)
        {
            string path = Art + "Props/" + t.name + ".prefab";
            if (!AssetDatabase.LoadAssetAtPath<GameObject>(path))
                PrefabUtility.SaveAsPrefabAsset(t.gameObject, path);
        }

        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Art direct village square");
        var old = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "VillageSquare");
        if (old) Undo.DestroyObjectImmediate(old);
        if(!EditorUtility.IsPersistent(city))
        {
            Undo.RecordObject(city, "Archive previous city layout");
            city.SetActive(false);
        }
        var ground = scene.GetRootGameObjects().First(g => g.name == "Ground");
        Undo.RecordObject(ground.GetComponent<Renderer>(), "Meadow surround");
        ground.GetComponent<Renderer>().enabled = false;

        _root = new GameObject("VillageSquare").transform;
        Undo.RegisterCreatedObjectUndo(_root.gameObject, "Create village square");
        Transform paving = Group("01 • Paving & terraces");
        Transform buildings = Group("02 • Shops & academy");
        Transform gardens = Group("03 • Walled gardens");
        Transform street = Group("04 • Street furniture");
        Transform woodland = Group("05 • Woodland edge");
        Transform effects = Group("06 • Atmosphere");
        var bounds=Object.Instantiate(city.transform.Find("Bounds").gameObject,_root);
        bounds.name="World boundaries";bounds.SetActive(true);

        // The main crossroads stays at y=0 so saved multiplayer spawn positions still work.
        Box(paving,"Meadow surround",0,-.07f,0,200,.1f,200,"Meadow",false);
        Box(paving,"Square foundation",0,-.22f,0,32,.46f,30,"Plaza");
        Box(paving,"South approach",0,-.055f,-25,6,.14f,22,"Road");
        Box(paving,"East lane",24,-.055f,0,16,.14f,4,"Road");
        Box(paving,"West lane",-24,-.055f,0,16,.14f,4,"Road");
        // Pale borders keep the muted brick from becoming an uninterrupted texture.
        foreach (float x in new[]{-15.8f,15.8f}) Box(paving,"Square edging",x,.015f,0,.26f,.05f,30,"Limestone",false);
        foreach (float z in new[]{-14.8f,14.8f}) Box(paving,"Square edging",0,.015f,z,32,.05f,.26f,"Limestone",false);
        foreach (float x in new[]{-3.15f,3.15f}) Box(paving,"Approach edging",x,.04f,-25,.25f,.12f,22,"Limestone");
        Box(paving,"Academy terrace",0,.46f,19.6f,32,.9f,9.2f,"Wall");
        Box(paving,"Terrace paving",0,.925f,19.6f,32,.04f,9.2f,"Road",false);
        for(int i=0;i<6;i++)
            Box(paving,"Broad stair "+(i+1),0,.075f*(i+1),13.25f+i*.32f,5.8f,.15f*(i+1),.34f,"Limestone",false);
        // A continuous hidden ramp carries the capsule over small visual stair risers.
        Ramp(paving, new Vector3(0,.01f,13.08f),5.8f,1.94f,.935f);
        foreach(float x in new[]{-9.45f,9.45f})
        {
            Box(paving,"Retaining wall",x,.73f,15.1f,12.9f,1.46f,.48f,"Wall");
            Box(paving,"Retaining coping",x,1.49f,15.1f,13.1f,.15f,.64f,"Limestone");
            for(int i=0;i<3;i++)
                Prop(street,"planter",x-4+i*4,1.58f,15.1f,.65f);
        }
        foreach(float x in new[]{-3.35f,3.35f})
        {
            Box(paving,"Stair gatepost",x,.69f,13.25f,.72f,1.38f,.72f,"Wall");
            Box(paving,"Gatepost cap",x,1.44f,13.25f,.86f,.16f,.86f,"Limestone");
            Prop(street,"lantern_low",x,1.52f,13.25f,.75f);
        }

        Building(buildings,"Img_GuildHall","Academy",0,.95f,21,.75f);
        Building(buildings,"Img_Tavern","The Copper Kettle",-11,.95f,21.5f,.64f);
        Building(buildings,"Img_RowHouses","Terrace residences",11,.95f,22,.60f);
        Building(buildings,"Img_FlowerShop","Rose & Fern",-12,0,7,.67f);
        Building(buildings,"Img_TallBakery","Honeycrust Bakery",12,0,8,.62f);
        Building(buildings,"Img_Tavern","Corner tearoom",-16,0,-7,.57f);
        Building(buildings,"Img_FlowerShop","South florist",14,0,-9,.57f);

        // The fountain is a small meeting point, with circulation on all four sides.
        var fountain = Building(buildings,"Img_Fountain_Angel","Meeting fountain",0,.08f,3,.29f);
        var basin = new GameObject("Basin collision");
        basin.transform.SetParent(fountain.transform,false);
        var basinCol = basin.AddComponent<BoxCollider>();
        basinCol.center = new Vector3(0,1f,-.5f);
        basinCol.size = new Vector3(9,2,9);
        Disc(paving,"Fountain stone medallion",new Vector3(-.6f,.035f,2.4f),3.45f,3.45f,"Limestone");
        Disc(paving,"Inner fountain medallion",new Vector3(-.6f,.041f,2.4f),3.12f,3.12f,"Road");

        Bed(gardens,-8,-3,4.5f,5.6f,true);
        Bed(gardens,8,-3,4.5f,5.6f,true);
        Bed(gardens,-8,10,4.5f,3.3f,false);
        Bed(gardens,8,11,4.5f,2.8f,false);
        Bed(gardens,-9,-12,6.4f,2.3f,false);
        Bed(gardens,8,-12,5.8f,2.3f,false);
        for(int side=-1;side<=1;side+=2)
        {
            for(int i=0;i<4;i++)
            {
                float z=-18-i*5.5f;
                Bed(gardens,side*5.9f,z,3.6f,3.5f,true);
                Prop(street,"lamp_iron",side*3.9f,0,z+1.5f,.85f);
            }
            Prop(street,"bench",side*4.7f,0,-6.6f,.8f,side>0);
            Prop(street,"bench",side*5.1f,0,8,.8f,side>0);
            Prop(street,"lamp_vine",side*5.6f,0,11.7f,.9f);
            Prop(street,"lamp_iron",side*4.8f,0,-10.2f,.9f);
            Prop(street,"planter",side*4.6f,0,-7.9f,.6f);
            Prop(street,"fern_pot",side*11.1f,0,4.8f,.8f);
            Prop(street,"trellis_rose",side*14.1f,0,3,.8f);
            for(int j=0;j<3;j++) Prop(street,"planter",side*(11+j*.8f),0,-11.4f,.6f);
        }
        // Tiny shop stories: pots, packed crates, café tables, roses against the wall.
        for(int i=0;i<4;i++) Prop(street,"fern_pot",-13.4f+i*.85f,0,4,.65f);
        Prop(street,"crates",10.2f,0,5,.8f);
        Prop(street,"crates",10.8f,0,5.3f,.55f,true);
        Prop(street,"lantern_low",11.5f,0,4.7f,.7f);
        for(int i=0;i<3;i++)
        {
            Prop(street,"table",-12+i*1.7f,0,-7.4f,1.35f);
            Prop(street,"planter",-12+i*1.7f,0,-9,.65f);
        }
        Prop(street,"trellis_rose",-14,0,-3.9f,.9f);
        Prop(street,"bench",-7,.95f,18,.8f);
        Prop(street,"bench",7,.95f,18,.8f,true);
        for(int i=0;i<5;i++)
        {
            Prop(street,"lamp_iron",-14+i*7,.95f,16.6f,.85f);
            if(i!=2) Prop(street,"fern_pot",-14+i*7,.95f,18.6f,.8f);
        }
        // Woodland wraps the built area; seeded offsets avoid mechanically repeated rows.
        var random = new System.Random(713);
        for(int i=0;i<16;i++)
        {
            float x=-29+i*3.9f;
            Prop(woodland,i%5==0?"tree_birch":i%3==0?"tree_maple":"tree_oak",x,0,28+(float)random.NextDouble()*6,.65f+(float)random.NextDouble()*.25f,i%2==0);
        }
        for(int side=-1;side<=1;side+=2)
            for(int i=0;i<12;i++)
            {
                float z=-35+i*5.4f;
                Prop(woodland,i%5==0?"tree_birch":i%3==0?"tree_maple":"tree_oak",side*(22+(float)random.NextDouble()*4),0,z,.65f+(float)random.NextDouble()*.22f,i%2==0);
                Prop(woodland,"shrub",side*19.4f,0,z+1,.9f);
            }
        Atmosphere(effects);
        FountainMist(effects);
        SetLighting();
        if(!EditorUtility.IsPersistent(city))Undo.DestroyObjectImmediate(city);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Undo.CollapseUndoOperations(undoGroup);
        Debug.Log("Square art layout saved. Previous City archived under Editor/Reference.");
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

    private static void Ramp(Transform parent,Vector3 p,float w,float d,float h)
    {
        var mesh=SavedMesh(FormattableString.Invariant($"StairRamp_{w}_{d}_{h}"),()=>
        {
            var m=new Mesh {vertices=new[]{new Vector3(-w/2,0,0),new Vector3(-w/2,h,d),new Vector3(w/2,h,d),new Vector3(w/2,0,0)},triangles=new[]{0,1,2,0,2,3}};
            m.RecalculateNormals();return m;
        });
        var g=new GameObject("Stair walking ramp",typeof(MeshCollider));g.transform.SetParent(parent,false);g.transform.position=p;g.GetComponent<MeshCollider>().sharedMesh=mesh;
    }

    private static GameObject Building(Transform parent,string prefab,string name,float x,float y,float z,float scale)
    {
        var asset=AssetDatabase.LoadAssetAtPath<GameObject>(Project+"Prefabs/City/"+prefab+".prefab");
        var g=(GameObject)PrefabUtility.InstantiatePrefab(asset,parent);
        g.name=name;g.transform.position=new Vector3(x,y,z);g.transform.localScale=Vector3.one*scale;
        return g;
    }

    private static GameObject Prop(Transform parent,string name,float x,float y,float z,float scale=1,bool flip=false)
    {
        var asset=AssetDatabase.LoadAssetAtPath<GameObject>(Art+"Props/"+name+".prefab");
        var g=(GameObject)PrefabUtility.InstantiatePrefab(asset,parent);
        g.transform.position=new Vector3(x,y,z);g.transform.localScale=Vector3.one*scale;g.SetActive(true);
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

    private static void Bed(Transform parent,float x,float z,float w,float d,bool tree)
    {
        var bed=new GameObject(tree?"Tree garden":"Flower garden").transform;bed.SetParent(parent,false);
        Box(bed,"Raised soil",x,.16f,z,w,.32f,d,"Soil");
        foreach(float sign in new[]{-1f,1f})
        {
            Box(bed,"Stone border",x+sign*w/2,.23f,z,.22f,.46f,d+.22f,"Wall");
            Box(bed,"Border coping",x+sign*w/2,.49f,z,.3f,.08f,d+.3f,"Limestone");
            Box(bed,"Stone border",x,.23f,z+sign*d/2,w,.46f,.22f,"Wall");
            Box(bed,"Border coping",x,.49f,z+sign*d/2,w,.08f,.3f,"Limestone");
        }
        if(tree)Prop(bed,"tree_oak",x,.33f,z+.6f,.65f);
        for(int i=0;i<5;i++)
        {
            float px=x-w*.34f+w*.17f*i;
            Prop(bed,i%2==0?"marigolds":"daisies",px,.34f,z-d*.31f,.52f,i%2==0);
            if(!tree)Prop(bed,i%2==0?"hollyhock":"shrub",px,.34f,z+.15f,.53f);
        }
        if(tree){Prop(bed,"shrub",x-w*.28f,.33f,z,.55f);Prop(bed,"grass_tall",x+w*.28f,.33f,z,.5f);}
    }

    private static void Atmosphere(Transform parent)
    {
        var g=new GameObject("Sunlit pollen",typeof(ParticleSystem));g.transform.SetParent(parent,false);g.transform.position=new Vector3(0,1.8f,0);
        var ps=g.GetComponent<ParticleSystem>();var main=ps.main;main.startLifetime=9;main.startSpeed=.07f;main.startSize=new ParticleSystem.MinMaxCurve(.015f,.04f);main.maxParticles=100;main.simulationSpace=ParticleSystemSimulationSpace.World;
        main.startColor=new Color(1,.89f,.6f,.34f);main.loop=true;main.prewarm=true;
        var emission=ps.emission;emission.rateOverTime=7;
        var shape=ps.shape;shape.shapeType=ParticleSystemShapeType.Box;shape.scale=new Vector3(28,3,30);
        var noise=ps.noise;noise.enabled=true;noise.strength=.12f;noise.frequency=.25f;
        var col=ps.colorOverLifetime;col.enabled=true;
        var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(.45f,.25f),new GradientAlphaKey(.3f,.75f),new GradientAlphaKey(0,1)});col.color=gradient;
        var renderer=g.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>(Project+"Materials/Garden/_FX_Mote.mat");renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
    }

    private static void FountainMist(Transform parent)
    {
        var g=new GameObject("Fountain droplets",typeof(ParticleSystem));g.transform.SetParent(parent,false);g.transform.position=new Vector3(-.6f,1.5f,2.4f);
        var ps=g.GetComponent<ParticleSystem>();var main=ps.main;main.loop=true;main.prewarm=true;main.startLifetime=.9f;main.startSpeed=.12f;main.gravityModifier=.18f;main.startSize=new ParticleSystem.MinMaxCurve(.015f,.035f);main.startColor=new Color(.65f,.9f,1,.5f);main.maxParticles=32;
        var emission=ps.emission;emission.rateOverTime=14;
        var shape=ps.shape;shape.shapeType=ParticleSystemShapeType.Circle;shape.radius=.65f;shape.rotation=new Vector3(90,0,0);
        var renderer=g.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>(Project+"Materials/Garden/_FX_Mote.mat");renderer.shadowCastingMode=ShadowCastingMode.Off;
    }

    private static void SetLighting()
    {
        var sun=Object.FindAnyObjectByType<Light>();Undo.RecordObject(sun,"Square sunlight");Undo.RecordObject(sun.transform,"Square sunlight angle");
        sun.transform.rotation=Quaternion.Euler(48,-28,0);sun.color=new Color(1,.93f,.82f);sun.intensity=1.15f;sun.shadowStrength=.65f;sun.shadows=LightShadows.Soft;
        RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.66f,.75f,.81f);RenderSettings.ambientEquatorColor=new Color(.62f,.68f,.58f);RenderSettings.ambientGroundColor=new Color(.39f,.40f,.34f);
        RenderSettings.fog=false;
        string path=Art+"SquareGrade.asset";
        var profile=AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
        if(!profile)
        {
            profile=ScriptableObject.CreateInstance<VolumeProfile>();AssetDatabase.CreateAsset(profile,path);
            var grade=profile.Add<ColorAdjustments>(true);grade.postExposure.value=.1f;grade.contrast.value=5;grade.saturation.value=-7;
            var white=profile.Add<WhiteBalance>(true);white.temperature.value=3;
            var bloom=profile.Add<Bloom>(true);bloom.threshold.value=1.2f;bloom.intensity.value=.14f;bloom.scatter.value=.55f;
            var vignette=profile.Add<Vignette>(true);vignette.intensity.value=.12f;vignette.smoothness.value=.7f;
            var tone=profile.Add<Tonemapping>(true);tone.mode.value=TonemappingMode.Neutral;
            foreach(var component in profile.components)AssetDatabase.AddObjectToAsset(component,profile);
        }
        var volume=Object.FindAnyObjectByType<Volume>();Undo.RecordObject(volume,"Square color grade");volume.sharedProfile=profile;volume.isGlobal=true;
        var camera=Camera.main;Undo.RecordObject(camera,"Square camera");
        camera.backgroundColor=new Color(.47f,.57f,.45f);camera.farClipPlane=300;
        var data=camera.GetComponent<UniversalAdditionalCameraData>();if(data){Undo.RecordObject(data,"Square post processing");data.renderPostProcessing=true;}
    }
}

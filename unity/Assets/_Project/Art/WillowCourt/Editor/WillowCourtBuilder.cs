using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

// Authored meshes and materials persist as assets; no generator runs in a build.
public static class WillowCourtBuilder
{
    private const string Project="Assets/_Project/";
    private const string Art=Project+"Art/WillowCourt/";
    private const string ScenePath=Project+"Scenes/SquareScene_WillowCourt.unity";
    private static Transform _root;
    private static readonly Dictionary<string,Material> Materials=new();
    private static readonly Dictionary<string,Mesh> Meshes=new();

    [MenuItem("DG Village/Willow Court/Create new scene")]
    public static void Build()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Exit Play mode.");
        if(UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)
            throw new InvalidOperationException("Save current edits before opening another scene.");
        if(AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath))
            throw new InvalidOperationException("Willow Court exists. Edit the saved scene directly.");
        EnsureFolder(Art+"Meshes");EnsureFolder(Art+"Materials");
        ConfigureTexture("Willow.png",true);
        ConfigureTexture("Hydrangea.png",true);
        ConfigureTexture("RosePaving.png",false);
        Materials.Clear();Meshes.Clear();
        if(!AssetDatabase.CopyAsset(Project+"Scenes/SquareScene.unity",ScenePath))
            throw new InvalidOperationException("Could not copy source scene.");
        var scene=EditorSceneManager.OpenScene(ScenePath);
        foreach(var g in scene.GetRootGameObjects()){
            if(g.name=="VillageSquare" || g.name=="Ground")Object.DestroyImmediate(g);
            else if(g.name!="Main Camera" && g.name!="Directional Light" && g.name!="Garden Volume" && g.name!="EventSystem" && g.name!="CinemachineCamera")
                g.SetActive(false);
        }
        _root=new GameObject("Willow Court").transform;
        var stone=Group("01 • Terraces, stairs & foundations");
        var architecture=Group("02 • Academy & garden walls");
        var gardens=Group("03 • Layered planting");
        var furniture=Group("04 • Places to linger");
        var canal=Group("05 • Canal & bridge");
        var atmosphere=Group("06 • Light, water & pollen");
        var life=Group("07 • Village life");

        // Compact, stepped architecture. Every visible walkable surface is physical.
        Box(stone,"Lower court",-1,-.24f,-2.8f,21,.5f,15.6f,"Paving");
        Box(stone,"Academy terrace",-1,.74f,8,23,1.5f,10,"Wall");
        Box(stone,"Terrace paving",-1,1.515f,8,23,.05f,10,"Paving");
        Box(stone,"East garden walk",9.3f,-.24f,5,4.4f,.5f,14,"Paving");
        Box(stone,"Court canal foundation",-1,-1.47f,-2.8f,21,1.96f,15.6f,"Wall");
        Box(stone,"East promenade foundation",9.3f,-1.47f,5,4.4f,1.96f,14,"Wall");
        Box(stone,"Academy submerged foundation",-1,-1.23f,8,23,2.44f,10,"Wall");
        // Eight shallow visual treads sit over a continuous capsule-friendly ramp.
        for(int i=0;i<8;i++){
            float h=(i+1)*1.5f/8;
            Box(stone,"Stair tread "+(i+1),-1,h/2,.15f+i*.37f,7.2f,h,.39f,"Cream",false);
            Box(stone,"Stair nosing "+(i+1),-1,h+.012f,-.015f+i*.37f,7.3f,.035f,.09f,"Cream",false);
        }
        Ramp(stone,new Vector3(-1,.025f,-.04f),7.25f,3.04f,1.52f);
        // Stone borders and inset carpets break up the floor without covering it.
        Box(stone,"Court inlay border",-1,.031f,-4,9.3f,.012f,5.5f,"Cream",false);
        Box(stone,"Court inset",-1,.04f,-4,9.08f,.014f,5.28f,"Paving",false);
        Mosaic(stone,new Vector3(-1,.052f,-4.0f),1.35f);
        Box(stone,"Threshold inlay",-1,1.55f,5.5f,6.7f,.018f,2.1f,"Cream",false);
        Box(stone,"Threshold inset",-1,1.564f,5.5f,6.46f,.018f,1.86f,"Paving",false);
        // Hero building is deliberately large and partially cropped at gameplay framing.
        Building(architecture,"Img_GuildHall","The waterside academy",-3,1.58f,10.4f,1.18f);
        Building(architecture,"Img_FlowerShop","Garden keeper cottage",7.8f,1.58f,10.5f,.62f);

        // The stair is held by planted masonry, not floating beside empty grass.
        RaisedBed(gardens,-7.2f,1.4f,4.7f,3.3f,0,1.45f,12);
        RaisedBed(gardens,5.1f,1.5f,4.7f,3.5f,0,1.45f,21);
        RaisedBed(gardens,-8.3f,7.2f,2.5f,4.3f,1.54f,.65f,31);
        RaisedBed(gardens,4.4f,8.8f,3.2f,3.8f,1.54f,.65f,41);
        // Quiet elevated willow garden, accessible by the side promenade.
        Box(gardens,"Willow garden soil",7.9f,.08f,-3.7f,3.6f,.12f,7.4f,"Moss");
        Wall(architecture,new Vector3(6.05f,0,-4.0f),new Vector3(6.05f,0,-7.4f),.58f);
        Wall(architecture,new Vector3(9.9f,0,-3.7f),new Vector3(9.9f,0,-7.4f),.7f);
        SpriteAsset(gardens,"Willow",new Vector3(8.25f,.16f,-2.0f),7.5f,7.5f,true);
        PlantRibbon(gardens,new Vector3(6.3f,.17f,-7.1f),new Vector3(9.6f,.17f,-7.1f),11,51);
        PlantRibbon(gardens,new Vector3(9.5f,.17f,-6.7f),new Vector3(9.5f,.17f,-.4f),18,61);
        PlantRibbon(gardens,new Vector3(6.4f,.17f,-6.5f),new Vector3(6.4f,.17f,-4.9f),6,71);
        Prop(furniture,"bench",6.75f,.18f,-4.15f,.9f,true);
        Prop(furniture,"fern_pot",6.35f,.18f,-3.0f,.62f);
        Prop(furniture,"lamp_iron",6.0f,.05f,-7.3f,.92f);
        // Foreground canal terrace: ornament is concentrated on its edges.
        Rail(architecture,new Vector3(-11.15f,0,-9.8f),new Vector3(-3.1f,0,-9.8f),.76f);
        Rail(architecture,new Vector3(2.0f,0,-9.8f),new Vector3(9.4f,0,-9.8f),.76f);
        Rail(architecture,new Vector3(-11.15f,0,-9.8f),new Vector3(-11.15f,0,2.8f),.76f);
        Rail(architecture,new Vector3(10.9f,0,2.8f),new Vector3(10.9f,0,11.8f),.76f);
        // Foreground water has visible retaining walls, a bridge and a landing.
        Box(canal,"Canal water",-2,-1.25f,-4,160,.02f,160,"Water",false);
        Box(canal,"Bridge arch body",-.55f,-.4f,-13,4.5f,.85f,6.4f,"Wall");
        Box(canal,"Bridge walk",-.55f,.045f,-13,4.5f,.08f,6.4f,"Paving");
        Rail(canal,new Vector3(-2.8f,.08f,-10.2f),new Vector3(-2.8f,.08f,-16.1f),.78f);
        Rail(canal,new Vector3(1.7f,.08f,-10.2f),new Vector3(1.7f,.08f,-16.1f),.78f);
        Box(canal,"South bank",-1,-.25f,-22,28,.5f,12,"Moss");
        Box(canal,"South bank submerged foundation",-1,-1.47f,-22,28,1.94f,12,"Wall");
        Box(canal,"South garden path",-.55f,.02f,-22,4.5f,.06f,12,"Paving");
        for(int s=-1;s<=1;s+=2){
            Prop(gardens,"tree_birch",s*5.5f,.02f,-18.5f,.85f,s>0);
            Prop(gardens,"tree_oak",s*9,.02f,-22,.75f,s>0);
            PlantRibbon(gardens,new Vector3(s*3.4f,.04f,-18),new Vector3(s*3.4f,.04f,-25),18,80+s);
        }
        // Two distinct rest areas, rather than evenly spaced props around a circle.
        Prop(furniture,"bench",-8,.04f,-5.3f,.9f);
        Prop(furniture,"lamp_vine",-9.9f,.05f,-6.9f,.9f);
        Prop(furniture,"table",-7.4f,.05f,-6.7f,1.4f);
        Prop(furniture,"fern_pot",-9.2f,.05f,-4.9f,.75f);
        RaisedBed(gardens,-8.4f,-2.3f,3.5f,1.6f,0,.48f,95);
        Prop(furniture,"bench",3.9f,1.56f,5.9f,.85f,true);
        Prop(furniture,"table",5.15f,1.56f,6.5f,1.25f);
        Prop(furniture,"lantern_low",5.7f,1.56f,5.8f,.62f);
        Prop(furniture,"trellis_rose",5.5f,1.56f,10.3f,.95f);
        Prop(furniture,"planter",2.7f,1.56f,7.3f,.65f);
        foreach(float x in new[]{-4.9f,2.9f}){
            Pier(architecture,new Vector3(x,.03f,-.13f),1.1f);
            Prop(furniture,"lantern_low",x,1.2f,-.13f,.8f);
        }
        foreach(float x in new[]{-10.8f,9.2f}){
            Pier(architecture,new Vector3(x,0,-9.8f),1.1f);
            Prop(furniture,"lantern_low",x,1.22f,-9.8f,.7f);
        }
        // Trailing leaves tie planting, stone coping and paving together.
        EdgeGrowth(gardens);
        Atmosphere(atmosphere);
        Lighting();
        AddResidents(life);
        AddLocalPlayer();
        SetupPreviewEntry();
        Boundaries();
        Polish();
        AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        Debug.Log("Willow Court authored and saved.");
    }

    [MenuItem("DG Village/Willow Court/Apply finishing once")]
    public static void Polish()
    {
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if(EditorApplication.isPlaying || scene.path!=ScenePath)throw new InvalidOperationException("Open Willow Court in Edit mode.");
        _root=GameObject.Find("Willow Court").transform;
        if(_root.Find("09 • Hand finished details"))throw new InvalidOperationException("Finishing already applied.");
        ConfigureTexture("Ivy.png",true);ConfigureTexture("Mosaic.png",true);
        var finish=Group("09 • Hand finished details");
        // Remove visible pots from planted soil, and vary the tall flowering stalks.
        int index=0;
        foreach(var t in _root.GetComponentsInChildren<Transform>().ToArray()){
            if(t.name=="fern_pot" && t.parent.name.StartsWith("Planted terrace"))t.gameObject.SetActive(false);
            if(t.name=="hollyhock" && t.parent.name.StartsWith("Planted terrace")){
                index++;
                if(index%3==0)t.gameObject.SetActive(false);
                else t.localScale*=index%2==0?.83f:1.12f;
            }
            if(t.name=="Invisible safe water boundary"){
                var plinth=t.parent.Find("Rail plinth");if(plinth)t.rotation=plinth.rotation;
            }
            if(t.name.StartsWith("Mosaic ") || t.name=="Compass petal" || t.name=="Compass heart")
                t.gameObject.SetActive(false);
        }
        foreach(var p in new[]{
            new Vector3(-5.5f,.3f,-.65f),new Vector3(-9.3f,.4f,.0f),
            new Vector3(3.1f,.35f,-.55f),new Vector3(6.1f,.4f,-.55f),
            new Vector3(-9.2f,1.7f,4.75f),new Vector3(3.0f,1.8f,6.5f),
            new Vector3(-9.7f,-.28f,-10.1f),new Vector3(-5.5f,-.32f,-10.1f),
            new Vector3(4.3f,-.25f,-10.1f),new Vector3(7.9f,-.28f,-10.1f)}){
            SpriteAsset(finish,"Ivy",p,1.5f,1.65f,false);
        }
        FloorDecal(finish,"Mosaic",new Vector3(-1,.068f,-4),3.5f);
        // The terrace's rear edge is framed by planting instead of a bare cut edge.
        PlantRibbon(finish,new Vector3(5.8f,1.56f,11.9f),new Vector3(9.4f,1.56f,11.9f),14,211);
        Rail(finish,new Vector3(7.2f,1.54f,12.65f),new Vector3(10.25f,1.54f,12.65f),.7f);
        // Low irregular grass patches connect the bench niche and paving.
        for(int i=0;i<7;i++){
            var p=new Vector3(5.75f+i*.16f,.035f,-6.3f+i*.3f);
            Disc(finish,"Grass tongue",p,.27f+i*.025f,.35f,"Moss");
            if(i%2==0)Prop(finish,"daisies",p.x,p.y+.02f,p.z,.3f,i%3==0);
        }
        // A small lily cluster and submerged stones give the water a readable scale.
        for(int i=0;i<12;i++){
            float a=i*2.4f,r=.4f+(i%4)*.29f;
            var p=new Vector3(-12.9f+Mathf.Cos(a)*r,-1.226f,-7.9f+Mathf.Sin(a)*r);
            Disc(finish,"Water lily leaf",p,.17f+(i%3)*.035f,.16f,"Moss");
            if(i%4==0)SpriteAsset(finish,"Hydrangea",p+Vector3.up*.02f,.18f,.14f,false);
        }
        // Soften the harsh full-length surface streaks.
        var water=Mat("Water");water.SetColor("_GlintColor",new Color(.44f,.64f,.59f));EditorUtility.SetDirty(water);
        var player=scene.GetRootGameObjects().First(g=>g.name=="Local exploration player");
        var follow=Camera.main.GetComponent<WillowCourtCamera>();
        if(!follow)follow=Camera.main.gameObject.AddComponent<WillowCourtCamera>();
        var so=new SerializedObject(follow);so.FindProperty("_target").objectReferenceValue=player.transform;so.ApplyModifiedPropertiesWithoutUndo();
        foreach(var light in _root.GetComponentsInChildren<Light>())if(light.type==LightType.Point)light.shadows=LightShadows.None;
        SecureEdges();
        AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
    }

    [MenuItem("DG Village/Willow Court/Finish perimeter once")]
    public static void SecureEdges()
    {
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if(EditorApplication.isPlaying || scene.path!=ScenePath)throw new InvalidOperationException("Open Willow Court in Edit mode.");
        _root=GameObject.Find("Willow Court").transform;
        if(_root.Find("10 • Safe waterfront perimeter"))throw new InvalidOperationException("Perimeter already finished.");
        var edge=Group("10 • Safe waterfront perimeter");
        Rail(edge,new Vector3(9.35f,0,-9.8f),new Vector3(9.35f,0,-2.1f),.65f);
        Rail(edge,new Vector3(11.15f,0,-1.9f),new Vector3(11.15f,0,2.8f),.72f);
        Rail(edge,new Vector3(10.25f,1.54f,3.3f),new Vector3(10.25f,1.54f,12.65f),.72f);
        Rail(edge,new Vector3(-12.15f,1.54f,3.4f),new Vector3(-12.15f,1.54f,12.65f),.72f);
        Rail(edge,new Vector3(-12.15f,1.54f,12.65f),new Vector3(7.2f,1.54f,12.65f),.72f);
        Rail(edge,new Vector3(-12,0,-16.3f),new Vector3(-2.85f,0,-16.3f),.7f);
        Rail(edge,new Vector3(1.75f,0,-16.3f),new Vector3(12,0,-16.3f),.7f);
        // Close the two short joins between the lower court and side promenade.
        Wall(edge,new Vector3(9.4f,0,-2.05f),new Vector3(11.15f,0,-2.05f),.65f);
        Wall(edge,new Vector3(-11.2f,0,2.8f),new Vector3(-12.15f,0,3.4f),1.1f);
        var wall=Mat("Wall");wall.color=new Color(1,.88f,.8f);EditorUtility.SetDirty(wall);
        // Animated map elements are sparse and share their materials.
        foreach(var r in _root.GetComponentsInChildren<MeshRenderer>())
            r.gameObject.isStatic=!r.GetComponent<Occluder>();
        AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
    }

    private static void FloorDecal(Transform parent,string name,Vector3 position,float width)
    {
        var m=new Material(AssetDatabase.LoadAssetAtPath<Material>(Project+"Materials/Garden/shrub.mat"));
        m.name=name;m.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(Art+"Textures/"+name+".png");
        m.color=Color.white;m.SetFloat("_Cutoff",.4f);m.SetFloat("_Cull",0);
        AssetDatabase.CreateAsset(m,Art+"Materials/"+name+".mat");
        var g=GameObject.CreatePrimitive(PrimitiveType.Quad);g.name="Botanical stone mosaic";Object.DestroyImmediate(g.GetComponent<Collider>());
        g.transform.SetParent(parent,false);g.transform.position=position;g.transform.rotation=Quaternion.Euler(90,0,0);g.transform.localScale=new Vector3(width,width,1);
        var r=g.GetComponent<Renderer>();r.sharedMaterial=m;r.shadowCastingMode=ShadowCastingMode.Off;
    }

    [MenuItem("DG Village/Willow Court/Configure local entry")]
    public static void SetupPreviewEntry()
    {
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if(EditorApplication.isPlaying || scene.path!=ScenePath)throw new InvalidOperationException("Open Willow Court in Edit mode.");
        if(Object.FindAnyObjectByType<WillowCourtEntry>())return;
        var player=scene.GetRootGameObjects().First(g=>g.name=="Local exploration player");
        var source=player.GetComponentInChildren<SpriteRenderer>();
        var preview=new GameObject("Editor-only player preview",typeof(SpriteRenderer));preview.tag="EditorOnly";
        preview.transform.SetPositionAndRotation(source.transform.position,source.transform.rotation);
        preview.transform.localScale=source.transform.lossyScale;
        var renderer=preview.GetComponent<SpriteRenderer>();renderer.sprite=source.sprite;renderer.sharedMaterial=source.sharedMaterial;
        player.SetActive(false);
        var entry=new GameObject("Local map entry",typeof(WillowCourtEntry)).GetComponent<WillowCourtEntry>();
        var so=new SerializedObject(entry);so.FindProperty("_player").objectReferenceValue=player;so.FindProperty("_editorPreview").objectReferenceValue=preview;so.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
    }

    private static void ConfigureTexture(string file,bool alpha)
    {
        var path=Art+"Textures/"+file;AssetDatabase.ImportAsset(path);
        var importer=AssetImporter.GetAtPath(path) as TextureImporter;
        if(!importer)throw new InvalidOperationException("Missing generated texture "+path);
        importer.textureType=TextureImporterType.Default;
        importer.alphaIsTransparency=alpha;importer.mipmapEnabled=true;
        importer.wrapMode=alpha?TextureWrapMode.Clamp:TextureWrapMode.Repeat;
        importer.filterMode=FilterMode.Bilinear;importer.anisoLevel=4;
        importer.maxTextureSize=2048;importer.textureCompression=TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
    }

    private static Material Mat(string name)
    {
        if(Materials.TryGetValue(name,out var existing))return existing;
        string path=Art+"Materials/"+name+".mat";
        var m=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(!m){
            string source=name=="Wall"?"City/Wall_Stone":name=="Moss"?"Garden/_Meadow":"City/Cap_Stone";
            m=new Material(AssetDatabase.LoadAssetAtPath<Material>(Project+"Materials/"+source+".mat"));
            m.name=name;m.SetFloat("_Smoothness",.08f);
            m.mainTextureScale=Vector2.one*(name=="Wall"?.62f:.48f);
            if(name=="Paving"){
                m.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(Art+"Textures/RosePaving.png");
                m.mainTextureScale=Vector2.one*.2f;m.color=new Color(.92f,.89f,.9f);
            }
            if(name=="Wall")m.color=new Color(.86f,.72f,.65f);
            if(name=="Cream")m.color=new Color(1,.96f,.86f);
            if(name=="Moss"){
                m.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(Project+"Art/Square/MeadowSurface.asset");
                m.mainTextureScale=Vector2.one*.18f;m.color=new Color(.92f,1,.86f);
                m.SetFloat("_AlphaClip",0);m.DisableKeyword("_ALPHATEST_ON");m.renderQueue=2000;
            }
            if(name=="Water"){
                m.shader=Shader.Find("DG Village/Willow Court/Canal Water");
            }
            if(name=="Inlay"){m.mainTexture=null;m.color=new Color(.42f,.43f,.4f);}
            AssetDatabase.CreateAsset(m,path);
        }
        Materials[name]=m;return m;
    }

    private static void RaisedBed(Transform parent,float x,float z,float w,float d,float ground,float height,int seed)
    {
        var g=new GameObject("Planted terrace "+seed).transform;g.SetParent(parent,false);
        Box(g,"Masonry planter",x,ground+height/2,z,w,height,d,"Wall");
        Box(g,"Dark garden soil",x,ground+height+.018f,z,w-.25f,.04f,d-.25f,"Moss",false);
        foreach(float side in new[]{-1f,1f}){
            Box(g,"Limestone coping",x+side*w/2,ground+height+.06f,z,.24f,.13f,d+.24f,"Cream",false);
            Box(g,"Limestone coping",x,ground+height+.06f,z+side*d/2,w,.13f,.24f,"Cream",false);
            Box(g,"Planter foot",x,ground+.09f,z+side*d/2,w+.12f,.18f,.15f,"Cream",false);
        }
        var rng=new System.Random(seed);
        for(float u=-w*.38f;u<=w*.4f;u+=.6f)for(float v=-d*.32f;v<=d*.34f;v+=.65f){
            float px=x+u+(float)rng.NextDouble()*.14f,pz=z+v+(float)rng.NextDouble()*.18f;
            if(rng.NextDouble()<.5)
                SpriteAsset(g,"Hydrangea",new Vector3(px,ground+height+.06f,pz),1.05f,.85f,false);
            else
                Prop(g,rng.NextDouble()<.65?"hollyhock":"fern_pot",px,ground+height+.05f,pz,.45f+(float)rng.NextDouble()*.12f,rng.Next(2)==0);
        }
    }

    private static void PlantRibbon(Transform parent,Vector3 a,Vector3 b,int count,int seed)
    {
        var rng=new System.Random(seed);
        for(int i=0;i<count;i++){
            var p=Vector3.Lerp(a,b,(i+.25f)/count);p.x+=(float)rng.NextDouble()*.35f-.175f;p.z+=(float)rng.NextDouble()*.35f-.175f;
            if(i%3==0)SpriteAsset(parent,"Hydrangea",p,1.15f,.92f,false);
            else Prop(parent,i%4==0?"hollyhock":i%2==0?"daisies":"shrub",p.x,p.y,p.z,.35f+(float)rng.NextDouble()*.19f,i%2==0);
        }
    }

    private static void SpriteAsset(Transform parent,string name,Vector3 position,float width,float height,bool collide)
    {
        string path=Art+"Materials/"+name+".mat";
        var m=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(!m){
            m=new Material(AssetDatabase.LoadAssetAtPath<Material>(Project+"Materials/Garden/shrub.mat"));
            m.name=name;m.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(Art+"Textures/"+name+".png");
            m.color=Color.white;m.SetFloat("_Cutoff",.35f);m.SetFloat("_Smoothness",0);
            m.SetFloat("_Cull",0);AssetDatabase.CreateAsset(m,path);
        }
        var root=new GameObject(name).transform;root.SetParent(parent,false);root.position=position;
        var mesh=SavedMesh("Bottom quad",()=>{
            var q=new Mesh{vertices=new[]{new Vector3(-.5f,0,0),new Vector3(.5f,0,0),new Vector3(-.5f,1,0),new Vector3(.5f,1,0)},uv=new[]{new Vector2(0,0),new Vector2(1,0),new Vector2(0,1),new Vector2(1,1)},triangles=new[]{0,2,1,2,3,1}};
            q.RecalculateNormals();q.RecalculateBounds();return q;
        });
        var sprite=new GameObject("Painted canopy",typeof(MeshFilter),typeof(MeshRenderer));sprite.transform.SetParent(root,false);
        sprite.transform.localRotation=Quaternion.Euler(30,45,0);sprite.transform.localScale=new Vector3(width,height,1);
        sprite.GetComponent<MeshFilter>().sharedMesh=mesh;
        var renderer=sprite.GetComponent<MeshRenderer>();renderer.sharedMaterial=m;renderer.shadowCastingMode=ShadowCastingMode.Off;
        if(collide){
            var c=root.gameObject.AddComponent<CapsuleCollider>();c.center=Vector3.up;c.radius=.42f;c.height=2;
            sprite.AddComponent<Occluder>();
        }
        Shadow(root,width*.6f,width*.38f,collide?.22f:.12f);
    }

    private static void Shadow(Transform parent,float width,float depth,float alpha)
    {
        var g=GameObject.CreatePrimitive(PrimitiveType.Quad);Object.DestroyImmediate(g.GetComponent<Collider>());
        g.name="Contact shadow";g.transform.SetParent(parent,false);g.transform.localPosition=new Vector3(0,.018f,0);
        g.transform.localRotation=Quaternion.Euler(90,0,0);g.transform.localScale=new Vector3(width,depth,1);
        var renderer=g.GetComponent<Renderer>();renderer.sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>(Project+"Materials/BlobShadow.mat");
        renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
    }

    private static void Wall(Transform parent,Vector3 a,Vector3 b,float height)
    {
        var d=b-a;float length=d.magnitude;var center=(a+b)*.5f;
        var root=new GameObject("Garden wall").transform;root.SetParent(parent,false);
        var body=Box(root,"Rose stone",center.x,center.y+height/2,center.z,.28f,height,length,"Wall");
        var cap=Box(root,"Cream coping",center.x,center.y+height+.04f,center.z,.4f,.12f,length+.14f,"Cream",false);
        var rot=Quaternion.LookRotation(d);body.transform.rotation=rot;cap.transform.rotation=rot;
    }

    private static void Pier(Transform parent,Vector3 position,float height)
    {
        Box(parent,"Pier",position.x,position.y+height/2,position.z,.5f,height,.5f,"Wall");
        Box(parent,"Pier foot",position.x,position.y+.09f,position.z,.65f,.18f,.65f,"Cream",false);
        Box(parent,"Pier cap",position.x,position.y+height+.06f,position.z,.69f,.14f,.69f,"Cream",false);
    }

    private static void Rail(Transform parent,Vector3 a,Vector3 b,float height)
    {
        var root=new GameObject("Carved stone balustrade").transform;root.SetParent(parent,false);
        var delta=b-a;var center=(a+b)*.5f;var rot=Quaternion.LookRotation(delta);
        float length=delta.magnitude;
        var bottom=Box(root,"Rail plinth",center.x,center.y+.12f,center.z,.36f,.24f,length,"Cream");
        bottom.transform.rotation=rot;
        var top=Box(root,"Rail handstone",center.x,center.y+height,center.z,.4f,.14f,length+.1f,"Cream",false);
        top.transform.rotation=rot;
        var fence=Box(root,"Invisible safe water boundary",center.x,center.y+.8f,center.z,.3f,1.6f,length,"Cream");
        fence.transform.rotation=rot;
        fence.GetComponent<Renderer>().enabled=false;
        int count=Mathf.CeilToInt(length/.55f);
        for(int i=0;i<=count;i++){
            var p=Vector3.Lerp(a,b,(float)i/count);p.y+=.23f;
            Baluster(root,p,height-.28f);
        }
        int piers=Mathf.Max(1,Mathf.RoundToInt(length/3.4f));
        for(int i=0;i<=piers;i++)Pier(root,Vector3.Lerp(a,b,(float)i/piers),height+.08f);
    }

    private static void Baluster(Transform parent,Vector3 p,float h)
    {
        var mesh=SavedMesh("Carved baluster",()=>{
            float[] radii={.11f,.11f,.065f,.075f,.13f,.10f,.06f,.06f,.11f,.11f};
            const int sides=8;var v=new Vector3[radii.Length*sides];var uv=new Vector2[v.Length];var tris=new List<int>();
            for(int j=0;j<radii.Length;j++)for(int i=0;i<sides;i++){
                float a=i*Mathf.PI*2/sides;int n=j*sides+i;
                v[n]=new Vector3(Mathf.Cos(a)*radii[j],(float)j/(radii.Length-1),Mathf.Sin(a)*radii[j]);
                uv[n]=new Vector2((float)i/sides,(float)j/(radii.Length-1));
                if(j<radii.Length-1){int k=j*sides+(i+1)%sides;tris.AddRange(new[]{n,n+sides,k,k,n+sides,k+sides});}
            }
            var m=new Mesh{vertices=v,uv=uv,triangles=tris.ToArray()};m.RecalculateNormals();m.RecalculateBounds();return m;
        });
        var g=new GameObject("Carved spindle",typeof(MeshFilter),typeof(MeshRenderer));g.transform.SetParent(parent,false);g.transform.position=p;g.transform.localScale=new Vector3(1,h,1);
        g.GetComponent<MeshFilter>().sharedMesh=mesh;g.GetComponent<Renderer>().sharedMaterial=Mat("Cream");
    }

    private static void Mosaic(Transform parent,Vector3 center,float radius)
    {
        Disc(parent,"Mosaic outer ring",center,radius,radius,"Cream");
        Disc(parent,"Mosaic inner field",center+Vector3.up*.004f,radius-.1f,radius-.1f,"Paving");
        for(int i=0;i<8;i++){
            float a=i*45*Mathf.Deg2Rad;
            var p=center+new Vector3(Mathf.Cos(a)*radius*.54f,.012f,Mathf.Sin(a)*radius*.54f);
            var diamond=Box(parent,"Compass petal",p.x,p.y,p.z,.2f,.012f,.65f,"Inlay",false);
            diamond.transform.rotation=Quaternion.Euler(0,-i*45+90,0);
        }
        Disc(parent,"Compass heart",center+Vector3.up*.02f,.16f,.16f,"Cream");
    }

    private static void EdgeGrowth(Transform parent)
    {
        PlantRibbon(parent,new Vector3(-10.7f,.04f,-8.9f),new Vector3(-4.6f,.04f,-8.9f),18,111);
        PlantRibbon(parent,new Vector3(3.5f,.04f,-8.9f),new Vector3(8.8f,.04f,-8.9f),16,121);
        PlantRibbon(parent,new Vector3(-10.7f,.04f,-8.2f),new Vector3(-10.7f,.04f,-.8f),20,131);
        var rng=new System.Random(882);
        for(int i=0;i<45;i++){
            float x=i<22?-10.2f+(float)rng.NextDouble()*6:3.7f+(float)rng.NextDouble()*5;
            float z=-8.6f+(float)rng.NextDouble()*1.15f;
            // Small irregular moss stones suggest growth at the pavement joints.
            Disc(parent,"Moss at paving joint",new Vector3(x,.045f,z),.06f+(float)rng.NextDouble()*.12f,.06f+(float)rng.NextDouble()*.09f,"Moss");
        }
    }

    private static void Lighting()
    {
        var sun=GameObject.Find("Directional Light").GetComponent<Light>();
        sun.transform.rotation=Quaternion.Euler(48,-30,0);sun.color=new Color(1,.94f,.83f);sun.intensity=1.05f;sun.shadowStrength=.55f;sun.shadows=LightShadows.Soft;
        RenderSettings.ambientMode=AmbientMode.Trilight;
        RenderSettings.ambientSkyColor=new Color(.72f,.79f,.85f);
        RenderSettings.ambientEquatorColor=new Color(.7f,.73f,.66f);
        RenderSettings.ambientGroundColor=new Color(.46f,.43f,.4f);
        RenderSettings.fog=false;
        var volume=Object.FindAnyObjectByType<Volume>();
        var profile=ScriptableObject.CreateInstance<VolumeProfile>();AssetDatabase.CreateAsset(profile,Art+"WillowCourtGrade.asset");
        var grade=profile.Add<ColorAdjustments>(true);grade.postExposure.value=.05f;grade.contrast.value=4;grade.saturation.value=-5;
        var bloom=profile.Add<Bloom>(true);bloom.threshold.value=1.15f;bloom.intensity.value=.12f;
        var white=profile.Add<WhiteBalance>(true);white.temperature.value=2;
        var vignette=profile.Add<Vignette>(true);vignette.intensity.value=.09f;vignette.smoothness.value=.7f;
        var tone=profile.Add<Tonemapping>(true);tone.mode.value=TonemappingMode.Neutral;
        foreach(var c in profile.components)AssetDatabase.AddObjectToAsset(c,profile);
        volume.sharedProfile=profile;
        var cam=Camera.main;cam.orthographicSize=8.3f;cam.farClipPlane=250;
        cam.transform.rotation=Quaternion.Euler(30,45,0);
        var target=new Vector3(-.8f,1.2f,1.1f);cam.transform.position=target-cam.transform.forward*25;
        cam.backgroundColor=new Color(.22f,.39f,.38f);
        cam.GetComponent<UniversalAdditionalCameraData>().renderPostProcessing=true;
        var vcam=Object.FindAnyObjectByType<Unity.Cinemachine.CinemachineCamera>();
        vcam.transform.SetPositionAndRotation(cam.transform.position,cam.transform.rotation);vcam.Lens.OrthographicSize=8.3f;
    }

    private static void AddLocalPlayer()
    {
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Project+"Prefabs/Characters/Player.prefab");
        var g=(GameObject)PrefabUtility.InstantiatePrefab(prefab);
        g.name="Local exploration player";g.transform.position=new Vector3(-1,.12f,-3.2f);
        var set=AssetDatabase.LoadAssetAtPath<CharacterSpriteSet>(Project+"Data/Character/LavenderGirlSpriteSet.asset");
        var view=g.GetComponentInChildren<CharacterSpriteView>();
        var so=new SerializedObject(view);so.FindProperty("_spriteSet").objectReferenceValue=set;so.ApplyModifiedPropertiesWithoutUndo();
        var renderer=view.GetComponent<SpriteRenderer>();renderer.sprite=set.Idle.Get(Direction.South);
        view.transform.rotation=Quaternion.Euler(30,45,0);
        foreach(var canvas in g.GetComponentsInChildren<Canvas>())canvas.gameObject.SetActive(false);
        var vcam=Object.FindAnyObjectByType<Unity.Cinemachine.CinemachineCamera>();
        // Keep the composed view fixed while walking this small self-contained district.
        vcam.gameObject.SetActive(false);
        var brain=Camera.main.GetComponent<Unity.Cinemachine.CinemachineBrain>();if(brain)brain.enabled=false;
    }

    private static void AddResidents(Transform parent)
    {
        var boy=AssetDatabase.LoadAssetAtPath<CharacterSpriteSet>(Project+"Data/Character/WandererBoySpriteSet.asset");
        var girl=AssetDatabase.LoadAssetAtPath<CharacterSpriteSet>(Project+"Data/Character/LavenderGirlSpriteSet.asset");
        var positions=new[]{new Vector3(1.6f,1.56f,5.4f),new Vector3(-6.6f,.04f,-4.4f),new Vector3(5.2f,.04f,-6.1f)};
        for(int i=0;i<positions.Length;i++){
            var g=new GameObject("Garden visitor "+(i+1),typeof(SpriteRenderer));g.transform.SetParent(parent,false);g.transform.position=positions[i];g.transform.rotation=Quaternion.Euler(30,45,0);
            var set=i%2==0?boy:girl;var r=g.GetComponent<SpriteRenderer>();r.sprite=set.Idle.Get(i==0?Direction.South:Direction.East);
            var player=AssetDatabase.LoadAssetAtPath<GameObject>(Project+"Prefabs/Characters/Player.prefab");
            r.sharedMaterial=player.GetComponentInChildren<SpriteRenderer>().sharedMaterial;
            var foot=new GameObject("Visitor footprint").transform;foot.SetParent(parent,false);foot.position=positions[i];
            Shadow(foot,.75f,.5f,.2f);
        }
    }

    private static void Boundaries()
    {
        var root=Group("08 • Traversal boundaries");
        foreach(var p in new[]{new Vector4(-12.4f,4,1,30),new Vector4(12.2f,4,1,30),new Vector4(0,13.5f,26,1),new Vector4(0,-27.5f,30,1)}){
            var wall=Box(root,"Invisible district boundary",p.x,2,p.y,p.z,5,p.w,"Cream");wall.GetComponent<Renderer>().enabled=false;
        }
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
        var asset=AssetDatabase.LoadAssetAtPath<GameObject>(Project+"Art/Square/Props/"+name+".prefab");
        var g=(GameObject)PrefabUtility.InstantiatePrefab(asset,parent);
        g.transform.position=new Vector3(x,y,z);g.transform.localScale=Vector3.one*scale;g.SetActive(true);
        if(name.StartsWith("tree_") || name=="bench" || name=="crates" || name=="table" || name=="planter" || name=="fern_pot")
            foreach(var old in g.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(old);
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

}

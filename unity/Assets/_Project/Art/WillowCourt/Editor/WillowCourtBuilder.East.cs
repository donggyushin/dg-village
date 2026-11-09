using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

// 2026-11-10: east extension of the Willow Court map in SquareScene.
// South bank → arched bridge (east) → East forecourt (weathered stone, y 0) → grand stairs → East heights (stone, y 4.54).
// Re-running deletes groups 12 and 13 and builds them again.
public static partial class WillowCourtBuilder
{
    private const string MeadowGroup="12 • East meadow";
    private const string HeightsGroup="13 • East heights";

    // Forecourt: x 21..47, z -46..-20, top y 0.
    // Heights: x 21..47, z -20..2, top y 4.54, with a notch x 29..39, z -20..-14 that holds the upper stairs.
    private const float East0=21,East1=47,MeadowZ0=-46,MeadowZ1=-20,HeightsZ1=2;
    private const float BridgeZ=-33.4f,StairX=34;
    private const float Rise=4.5f,Top=4.54f;
    // Grand stairs: 3 flights of 8 steps (0.1875 m rise, 0.375 m tread) with 1.5 m landings.
    private const float StairW=10,StairZ0=-26,NotchZ=-14,FlightRise=1.5f,FlightRun=3,LandingRun=1.5f;

    [MenuItem("DG Village/Willow Court/Build east extension")]
    public static void BuildEast()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Exit Play mode.");
        var root=GameObject.Find("Willow Court");
        if(!root)throw new InvalidOperationException("Open SquareScene first (no 'Willow Court' root).");
        _root=root.transform;
        Materials.Clear();Meshes.Clear();
        foreach(var n in new[]{MeadowGroup,HeightsGroup}){var old=_root.Find(n);if(old)Object.DestroyImmediate(old.gameObject);}
        foreach(var f in new[]{"Gazebo.png","Lavender.png","Windmill.png","Rotunda.png"})
            if(HasArt(f))ConfigureTexture(f,true);

        EnsureEastPaving();
        BuildMeadow(Group(MeadowGroup));
        BuildHeights(Group(HeightsGroup));
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(root.scene);
    }

    // Weathered warm-grey version of RosePaving (2026-11-10 owner: "less pink, duller stone" for the east side).
    private static void EnsureEastPaving()
    {
        ConfigureTexture("WeatheredPaving.png",false);
        string path=Art+"Materials/EastPaving.mat";
        var m=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(!m){m=new Material(Mat("Paving"));m.name="EastPaving";AssetDatabase.CreateAsset(m,path);}
        m.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(Art+"Textures/WeatheredPaving.png");
        m.color=new Color(.97f,.96f,.94f);
        EditorUtility.SetDirty(m);
        Materials["EastPaving"]=m;
    }

    private static bool HasArt(string file)=>File.Exists(Art+"Textures/"+file);

    private static void BuildMeadow(Transform g)
    {
        float cx=(East0+East1)/2,cz=(MeadowZ0+MeadowZ1)/2,w=East1-East0,d=MeadowZ1-MeadowZ0;
        var ground=Sub(g,"Ground");
        Box(ground,"East forecourt paving",cx,-.25f,cz,w,.5f,d,"EastPaving");
        Box(ground,"East forecourt submerged foundation",cx,-1.47f,cz,w,1.94f,d,"Wall");
        // Cream kerb on the water edges (west edge leaves room for the bridge).
        Box(ground,"Kerb south",cx,.03f,MeadowZ0+.15f,w,.06f,.3f,"Cream",false);
        Box(ground,"Kerb east",East1-.15f,.03f,cz,.3f,.06f,d,"Cream",false);
        Box(ground,"Kerb west",East0+.15f,.03f,(MeadowZ0+BridgeZ-1.6f)/2,.3f,.06f,BridgeZ-1.6f-MeadowZ0,"Cream",false);
        Box(ground,"Kerb west",East0+.15f,.03f,(BridgeZ+1.6f+MeadowZ1)/2,.3f,.06f,MeadowZ1-BridgeZ-1.6f,"Cream",false);

        // Bridge: copy the south arched bridge and turn it so it spans x 13..21.
        var bridge=Sub(g,"East arched bridge");
        bridge.position=new Vector3(13,0,BridgeZ);bridge.rotation=Quaternion.Euler(0,270,0);
        var source=_root.Find("11 • South grove/Arched stone bridge");
        if(source){
            Selection.objects=new Object[]{source.gameObject};
            Unsupported.DuplicateGameObjectsUsingPasteboard();
            var copy=Selection.activeGameObject.transform;
            copy.name="Bridge (copy of south bridge)";
            copy.SetParent(bridge,false);copy.localPosition=new Vector3(.55f,0,44);copy.localRotation=Quaternion.identity;
            Selection.activeObject=null;
        }

        // Cream walks on the grey paving: bridge → round court → grand stairs, and east to the windmill.
        var paths=Sub(g,"Court & walks");
        var court=new Vector3(StairX,.03f,BridgeZ);
        Box(paths,"Bridge walk",(East0+court.x-3)/2,.02f,BridgeZ,court.x-3-East0+.4f,.06f,2.8f,"Cream",false);
        Box(paths,"Stair apron",StairX,.02f,(court.z+3.2f+StairZ0)/2,StairW+1.6f,.06f,StairZ0-court.z-3.2f+.1f,"Cream",false);
        Box(paths,"Windmill walk",(court.x+3+East1-1)/2,.02f,BridgeZ,East1-1-court.x-3+.4f,.06f,1.8f,"Cream",false);
        Disc(paths,"Court border",court,3.9f,3.9f,"Cream");
        Disc(paths,"Court paving",court+Vector3.up*.01f,3.7f,3.7f,"EastPaving");
        Mosaic(paths,court+Vector3.up*.02f,1.35f);
        foreach(Transform t in paths)if(t.name=="Mosaic inner field")t.GetComponent<Renderer>().sharedMaterial=Mat("EastPaving");

        // Seating around the court.
        var seat=Sub(g,"Seating & light");
        Prop(seat,"bench",StairX-2.2f,.04f,BridgeZ+3.1f,.9f);
        Prop(seat,"bench",StairX+2.6f,.04f,BridgeZ-2.6f,.9f,true);
        foreach(var p in new[]{new Vector2(-3.2f,3.2f),new Vector2(3.2f,3.2f),new Vector2(-3.2f,-3.2f),new Vector2(3.2f,-3.2f)})
            Prop(seat,"lamp_iron",StairX+p.x,.02f,BridgeZ+p.y,.92f);
        Prop(seat,"lantern_low",East0+1.2f,.04f,BridgeZ+2.1f,.62f);
        Prop(seat,"lantern_low",East0+1.2f,.04f,BridgeZ-2.1f,.62f);
        Prop(seat,"table",StairX-2.8f,.04f,BridgeZ+2.2f,1.2f);

        // Raised planters along the bridge walk.
        var planting=Sub(g,"Planting");
        RaisedBed(planting,26.8f,BridgeZ+4.0f,4.2f,2.8f,0,.45f,1201);
        RaisedBed(planting,26.8f,BridgeZ-4.0f,4.2f,2.8f,0,.45f,1202);
        // Lawn strips with flowers along the west water edge.
        foreach(var (z0,z1) in new[]{(MeadowZ0+1,BridgeZ-6.2f)}){
            Box(planting,"Lawn strip",East0+1.0f,.025f,(z0+z1)/2,1.2f,.04f,z1-z0,"Moss",false);
            PlantRibbon(planting,new Vector3(East0+1.0f,.05f,z0+.3f),new Vector3(East0+1.0f,.05f,z1-.3f),Mathf.RoundToInt((z1-z0)*1.6f),1220+(int)z0);
        }

        // Lavender in a raised masonry bed (south-east).
        var field=Sub(g,"Lavender bed");
        float fx0=StairX+5.5f,fx1=East1-.8f,fz0=MeadowZ0+.9f,fz1=BridgeZ-3.4f;
        Box(field,"Bed masonry",(fx0+fx1)/2,.225f,(fz0+fz1)/2,fx1-fx0,.45f,fz1-fz0,"Wall");
        var soil=Box(field,"Bed soil",(fx0+fx1)/2,.465f,(fz0+fz1)/2,fx1-fx0-.3f,.04f,fz1-fz0-.3f,"Cream",false);
        soil.GetComponent<Renderer>().sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>(Project+"Art/Square/Soil.mat");
        foreach(float s in new[]{-1f,1f}){
            Box(field,"Bed coping",(fx0+fx1)/2,.5f,(fz0+fz1)/2+s*(fz1-fz0)/2,fx1-fx0+.24f,.12f,.24f,"Cream",false);
            Box(field,"Bed coping",(fx0+fx1)/2+s*(fx1-fx0)/2,.5f,(fz0+fz1)/2,.24f,.12f,fz1-fz0,"Cream",false);
        }
        var rng=new System.Random(1210);
        for(float z=fz0+.75f;z<=fz1-.5f;z+=1.05f)
            for(float x=fx0+.6f;x<=fx1-.5f;x+=.8f){
                var p=new Vector3(x+(float)rng.NextDouble()*.1f,.48f,z+(float)rng.NextDouble()*.1f);
                if(HasArt("Lavender.png"))SpriteAsset(field,"Lavender",p,1.05f,1.05f,false);
                else Prop(field,"hollyhock",p.x,p.y,p.z,.42f);
            }

        // Landmarks and trees stand in grass pits; big ones only where they hide nothing important.
        var trees=Sub(g,"Trees & landmarks");
        if(HasArt("Windmill.png")){
            TreePit(trees,new Vector3(44.4f,0,-29.4f),2.3f,1240);
            SpriteAsset(trees,"Windmill",new Vector3(44.4f,.05f,-29.4f),6.6f,6.6f,true);
        }
        if(HasArt("Gazebo.png")){
            var gz=new Vector3(25.4f,.03f,-24.2f);
            Disc(trees,"Gazebo border",gz,3.1f,3.1f,"Cream");
            SpriteAsset(trees,"Gazebo",gz+Vector3.up*.02f,6.0f,6.0f,false);
            var gazebo=trees.GetChild(trees.childCount-1);
            var col=gazebo.gameObject.AddComponent<CapsuleCollider>();col.center=Vector3.up;col.radius=1.6f;col.height=2;
            gazebo.Find("Painted canopy").gameObject.AddComponent<Occluder>();
        }
        foreach(var t in new (string n,float x,float z,float s)[]{
            ("tree_maple",26.0f,-43.4f,.6f),("tree_birch",22.9f,-30.6f,.62f),
            ("tree_oak",45.0f,-23.2f,.78f),("tree_maple",41.0f,-22.4f,.6f)}){
            TreePit(trees,new Vector3(t.x,0,t.z),1.35f,(int)(t.x*10));
            Prop(trees,t.n,t.x,.05f,t.z,t.s,t.x>StairX);
        }

        var water=Sub(g,"Water life");
        for(int i=0;i<14;i++){
            var p=new Vector3(East0-1.2f-(float)rng.NextDouble()*2.4f,-1.23f,BridgeZ-6-(float)rng.NextDouble()*6);
            Disc(water,"Water lily leaf",p,.17f+(i%3)*.035f,.16f,"Moss");
        }

        // Invisible walls: forecourt edges, with a gap for the bridge on the west side.
        var bounds=Sub(g,"Boundaries");
        float gap0=BridgeZ-1.5f,gap1=BridgeZ+1.5f;
        Wall(bounds,East0-.5f,(MeadowZ0+gap0)/2,1,gap0-MeadowZ0+1);
        Wall(bounds,East0-.5f,(gap1+MeadowZ1)/2,1,MeadowZ1-gap1);
        Wall(bounds,East1+.5f,cz,1,d+1);
        Wall(bounds,cx,MeadowZ0-.5f,w+2,1);
        // South bank's east wall (x 13.5) gets the same gap; the original one is switched off.
        var southEast=FindBoundary("11 • South grove/Boundaries",13.5f);
        if(southEast)southEast.SetActive(false);
        Wall(bounds,13.5f,(-45+gap0)/2,1,gap0+45);
        Wall(bounds,13.5f,(gap1-15)/2,1,-15-gap1);
    }

    private static void BuildHeights(Transform g)
    {
        float wx0=East0,wx1=StairX-StairW/2,ex0=StairX+StairW/2,ex1=East1;   // west block, east block
        float bottom=-2.45f,h=Rise-bottom,cy=(bottom+Rise)/2;
        var ground=Sub(g,"Ground");
        // Three masonry blocks leave a notch in the middle for the upper stair flights.
        Box(ground,"Heights block west",(wx0+wx1)/2,cy,(MeadowZ1+HeightsZ1)/2,wx1-wx0,h,HeightsZ1-MeadowZ1,"Wall");
        Box(ground,"Heights block east",(ex0+ex1)/2,cy,(MeadowZ1+HeightsZ1)/2,ex1-ex0,h,HeightsZ1-MeadowZ1,"Wall");
        Box(ground,"Heights block middle",StairX,cy,(NotchZ+HeightsZ1)/2,StairW,h,HeightsZ1-NotchZ,"Wall");
        Box(ground,"Heights paving west",(wx0+wx1)/2,Rise+.02f,(MeadowZ1+HeightsZ1)/2,wx1-wx0,.04f,HeightsZ1-MeadowZ1,"EastPaving");
        Box(ground,"Heights paving east",(ex0+ex1)/2,Rise+.02f,(MeadowZ1+HeightsZ1)/2,ex1-ex0,.04f,HeightsZ1-MeadowZ1,"EastPaving");
        Box(ground,"Heights paving middle",StairX,Rise+.02f,(NotchZ+HeightsZ1)/2,StairW,.04f,HeightsZ1-NotchZ,"EastPaving");

        // Facade: cornice, base plinth and buttresses on the faces the camera sees.
        var facade=Sub(g,"Facade");
        foreach(var (x0,x1) in new[]{(wx0,wx1),(ex0,ex1)}){
            Box(facade,"Cornice",(x0+x1)/2,Rise-.17f,MeadowZ1-.12f,x1-x0+.3f,.32f,.5f,"Cream",false);
            Box(facade,"Plinth",(x0+x1)/2,.2f,MeadowZ1-.1f,x1-x0+.2f,.4f,.4f,"Cream",false);
            for(float x=x0+1.1f;x<=x1-1.0f;x+=2.9f){
                Box(facade,"Buttress",x,(Rise-.36f)/2,MeadowZ1-.3f,.9f,Rise-.36f,.6f,"Wall");
                Box(facade,"Buttress cap",x,Rise-.42f,MeadowZ1-.36f,1.08f,.14f,.78f,"Cream",false);
                Box(facade,"Buttress foot",x,.12f,MeadowZ1-.36f,1.1f,.24f,.8f,"Cream",false);
            }
        }
        Box(facade,"Cornice west",East0-.12f,Rise-.17f,(MeadowZ1+HeightsZ1)/2,.5f,.32f,HeightsZ1-MeadowZ1+.3f,"Cream",false);
        for(float z=MeadowZ1+2.4f;z<=HeightsZ1-1.5f;z+=4.2f){
            Box(facade,"Buttress",East0-.3f,(bottom+Rise-.36f)/2,z,.6f,Rise-.36f-bottom,.9f,"Wall");
            Box(facade,"Buttress cap",East0-.36f,Rise-.42f,z,.78f,.14f,1.08f,"Cream",false);
        }

        BuildGrandStairs(Sub(g,"Grand stairs"));

        // Top: stair head → promenade → rotunda.
        var paths=Sub(g,"Promenade");
        var rotunda=new Vector3(StairX,Top,HeightsZ1-6.0f);
        Box(paths,"Stair head",StairX,Top+.005f,NotchZ+.8f,StairW,.05f,1.6f,"Cream",false);
        Box(paths,"Promenade",StairX,Top+.005f,(NotchZ+1.6f+rotunda.z-4.4f)/2,3.4f,.05f,rotunda.z-4.4f-NotchZ-1.6f+.3f,"Cream",false);
        Box(paths,"Lookout walk",(East0+3+wx1)/2,Top+.005f,NotchZ-2.5f,wx1-East0-3+.2f,.05f,2.2f,"Cream",false);
        Disc(paths,"Rotunda border",rotunda+Vector3.up*.012f,4.8f,4.8f,"Cream");
        Disc(paths,"Rotunda field",rotunda+Vector3.up*.02f,4.55f,4.55f,"EastPaving");

        var feature=Sub(g,"Rotunda");
        string art=HasArt("Rotunda.png")?"Rotunda":HasArt("Gazebo.png")?"Gazebo":null;
        if(art!=null){
            float size=art=="Rotunda"?9.5f:6.0f;
            SpriteAsset(feature,art,rotunda+Vector3.up*.03f,size,size,false);
            var r=feature.GetChild(feature.childCount-1);
            var col=r.gameObject.AddComponent<CapsuleCollider>();col.center=Vector3.up;col.radius=art=="Rotunda"?3.0f:1.6f;col.height=2;
            r.Find("Painted canopy").gameObject.AddComponent<Occluder>();
        }

        var beds=Sub(g,"Planting");
        RaisedBed(beds,StairX-5.0f,rotunda.z-6.0f,3.2f,4.0f,Top,.45f,1301);
        RaisedBed(beds,StairX+5.0f,rotunda.z-6.0f,3.2f,4.0f,Top,.45f,1302);
        foreach(var (x,z0,z1) in new[]{(East0+1.0f,NotchZ+2.5f,HeightsZ1-1.0f),(East1-1.0f,MeadowZ1+1.0f,HeightsZ1-1.0f)}){
            Box(beds,"Lawn strip",x,Top+.005f,(z0+z1)/2,1.2f,.04f,z1-z0,"Moss",false);
            PlantRibbon(beds,new Vector3(x,Top+.02f,z0+.3f),new Vector3(x,Top+.02f,z1-.3f),Mathf.RoundToInt((z1-z0)*1.5f),1310+(int)x);
        }

        var trees=Sub(g,"Trees");
        TreePit(trees,new Vector3(East1-3.5f,Top,MeadowZ1+5.0f),2.0f,1350);
        SpriteAsset(trees,"Willow",new Vector3(East1-3.5f,Top+.03f,MeadowZ1+5.0f),7.0f,7.0f,true);
        foreach(var t in new (string n,float x,float z,float s)[]{
            ("tree_oak",East0+3.6f,HeightsZ1-2.0f,.72f),("tree_birch",East0+8.0f,HeightsZ1-1.6f,.7f),
            ("tree_maple",East1-3.2f,HeightsZ1-2.2f,.62f),("tree_birch",East1-3.4f,MeadowZ1+11.0f,.66f)}){
            TreePit(trees,new Vector3(t.x,Top,t.z),1.35f,(int)(t.x*7));
            Prop(trees,t.n,t.x,Top+.03f,t.z,t.s,t.x>StairX);
        }

        // Lookout over the water and the forecourt.
        var seat=Sub(g,"Lookout seating");
        Prop(seat,"bench",East0+2.0f,Top+.03f,MeadowZ1+2.0f,.9f);
        Prop(seat,"bench",East0+4.6f,Top+.03f,MeadowZ1+1.4f,.9f);
        Prop(seat,"table",East0+3.0f,Top+.03f,MeadowZ1+3.4f,1.2f);
        Prop(seat,"lamp_iron",East0+1.2f,Top,NotchZ-1.0f,.92f);
        foreach(float s in new[]{-1f,1f}){
            Prop(seat,"lamp_iron",StairX+s*(StairW/2-.6f),Top,NotchZ+.6f,.92f);
            Prop(seat,"lantern_low",StairX+s*2.1f,Top+.03f,rotunda.z-5.4f,.62f);
        }
        Prop(seat,"bench",StairX+6.8f,Top+.03f,rotunda.z,.9f,true);
        Prop(seat,"trellis_rose",StairX-6.8f,Top+.03f,rotunda.z,.95f);

        // Balustrades on every edge (they hold their own invisible fence); open only at the stair head.
        var edge=Sub(g,"Balustrades");
        float e0=East0+.25f,e1=East1-.25f,s0=MeadowZ1+.25f,n1=HeightsZ1-.25f;
        Rail(edge,new Vector3(e0,Top,s0),new Vector3(wx1-.25f,Top,s0),.72f);
        Rail(edge,new Vector3(ex0+.25f,Top,s0),new Vector3(e1,Top,s0),.72f);
        Rail(edge,new Vector3(wx1-.25f,Top,s0),new Vector3(wx1-.25f,Top,NotchZ),.72f);
        Rail(edge,new Vector3(ex0+.25f,Top,s0),new Vector3(ex0+.25f,Top,NotchZ),.72f);
        Rail(edge,new Vector3(e0,Top,s0),new Vector3(e0,Top,n1),.72f);
        Rail(edge,new Vector3(e0,Top,n1),new Vector3(e1,Top,n1),.72f);
        Rail(edge,new Vector3(e1,Top,s0),new Vector3(e1,Top,n1),.72f);
    }

    private static void BuildGrandStairs(Transform g)
    {
        const int steps=8;float rise=FlightRise/steps,run=FlightRun/steps;
        for(int f=0;f<3;f++){
            float z0=StairZ0+f*(FlightRun+LandingRun),y0=f*FlightRise;
            for(int j=0;j<steps;j++){
                float top=y0+(j+1)*rise,zs=z0+j*run;
                Box(g,"Stair tread",StairX,top/2,zs+run/2,StairW,top,run,"Cream",false);
                Box(g,"Stair nosing",StairX,top,zs+.02f,StairW+.1f,.035f,.09f,"Cream",false);
            }
            Ramp(g,new Vector3(StairX,y0+.025f,z0),StairW+.05f,FlightRun,FlightRise);
            float cheekEnd=f<2?z0+FlightRun+LandingRun:NotchZ;
            if(f<2)Box(g,"Stair landing",StairX,(y0+FlightRise)/2,z0+FlightRun+LandingRun/2,StairW,y0+FlightRise,LandingRun,"EastPaving");
            // Stepped cheek walls with cream coping; a pier and lantern where each flight starts.
            foreach(float s in new[]{-1f,1f}){
                float x=StairX+s*(StairW/2+.3f),ch=y0+FlightRise+.45f;
                Box(g,"Stair cheek",x,ch/2,(z0+cheekEnd)/2,.6f,ch,cheekEnd-z0,"Wall");
                Box(g,"Cheek coping",x,ch+.05f,(z0+cheekEnd)/2,.74f,.1f,cheekEnd-z0+.1f,"Cream",false);
                Pier(g,new Vector3(x,0,z0-.05f),y0+FlightRise+.75f);
                Prop(g,"lantern_low",x,y0+FlightRise+.95f,z0-.05f,.55f);
            }
        }
        foreach(float s in new[]{-1f,1f})Prop(g,"lamp_iron",StairX+s*(StairW/2+1.6f),.02f,StairZ0-1.2f,.92f);
    }

    private static void TreePit(Transform parent,Vector3 p,float r,int seed)
    {
        Disc(parent,"Pit kerb",p+Vector3.up*.015f,r+.15f,r+.15f,"Cream");
        Disc(parent,"Pit grass",p+Vector3.up*.03f,r,r,"Moss");
        var rng=new System.Random(seed);string[] small={"daisies","grass_tall","marigolds","shrub"};
        for(int i=0;i<5;i++){
            float a=(float)rng.NextDouble()*Mathf.PI*2,d=r*(.55f+(float)rng.NextDouble()*.3f);
            Prop(parent,small[rng.Next(small.Length)],p.x+Mathf.Cos(a)*d,p.y+.04f,p.z+Mathf.Sin(a)*d,.3f+(float)rng.NextDouble()*.12f,rng.Next(2)==0);
        }
    }

    private static Transform Sub(Transform parent,string name)
    {
        var t=new GameObject(name).transform;t.SetParent(parent,false);return t;
    }

    private static void Wall(Transform parent,float x,float z,float w,float d)
    {
        var wall=Box(parent,"Invisible boundary",x,2,z,w,5,d,"Cream");
        wall.GetComponent<Renderer>().enabled=false;
    }

    private static GameObject FindBoundary(string path,float x)
    {
        var group=_root.Find(path);if(!group)return null;
        foreach(Transform t in group)if(Mathf.Abs(t.position.x-x)<.01f)return t.gameObject;
        return null;
    }
}

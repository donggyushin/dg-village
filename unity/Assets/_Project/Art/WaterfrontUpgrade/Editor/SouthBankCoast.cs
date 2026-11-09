using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

// 2026-11-10: the South bank (45 m square island) gets a natural coastline.
// The outline is a radius per angle around the island centre: a rounded square + a few sine "waves"
// (bays and headlands), pushed out where trees, benches and paths must stay on land,
// and kept straight where the three bridges (north, east, south) land.
// Re-running rebuilds the mesh, collider, boundary walls, shore planting and the water's shore map.
public static class SouthBankCoast
{
    private const string Art="Assets/_Project/Art/WaterfrontUpgrade/";
    private const string Project="Assets/_Project/";
    private const string GroupName="16 • South bank coast";
    private static readonly Vector2 Centre=new Vector2(-1.62f,-48.6f);
    private const float Half=22.68f,X0=-24.3f,X1=21.06f,Z0=-71.28f,Z1=-25.92f;
    private const float Top=1.2f,WaterY=-1.25f,Toe=1.05f;
    private const int N=240;
    private static float[] _r;
    private static Vector2[] _coast;
    private static readonly List<string> Log=new List<string>();

    // Bridge landings: the coast stays on the old straight edge between these points.
    private static readonly Vector2[][] Anchors={
        new[]{new Vector2(-6.4f,Z1),new Vector2(4.6f,Z1)},
        new[]{new Vector2(-5.4f,Z0),new Vector2(3.6f,Z0)},
        new[]{new Vector2(X1,-58.8f),new Vector2(X1,-49.4f)},
    };

    [MenuItem("DG Village/Waterfront/South bank coastline")]
    public static void Menu()=>Debug.Log(Build());

    public static string Build()
    {
        Log.Clear();_keepCache=null;
        var root=GameObject.Find("Willow Court")?.transform;
        if(!root)throw new InvalidOperationException("Open SquareScene first (Willow Court root missing).");
        var bank=root.Find("05 • Canal & bridge/South bank");
        var old=root.Find(GroupName);if(old)Object.DestroyImmediate(old.gameObject);
        var group=new GameObject(GroupName).transform;group.SetParent(root,false);

        Shape(root);
        BankMesh(bank);
        RemoveOldEdges(root);
        Prune(root);
        Boundaries(group);
        ShorePlanting(group);
        BakeShore(root,group,bank);
        EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
        AssetDatabase.SaveAssets();
        return "South bank coastline\n"+string.Join("\n",Log);
    }

    // ---------- outline ----------
    private static float Angle(int i)=>i*Mathf.PI*2/N;
    private static Vector2 Dir(float a)=>new Vector2(Mathf.Cos(a),Mathf.Sin(a));
    private static float SquareR(float a){float c=Mathf.Abs(Mathf.Cos(a)),s=Mathf.Abs(Mathf.Sin(a));return Half/Mathf.Max(c,s);}
    private static float SuperR(float a,float p){float c=Mathf.Abs(Mathf.Cos(a)),s=Mathf.Abs(Mathf.Sin(a));return Half/Mathf.Pow(Mathf.Pow(c,p)+Mathf.Pow(s,p),1/p);}
    private static float AngleOf(Vector2 p){var d=p-Centre;float a=Mathf.Atan2(d.y,d.x);return a<0?a+Mathf.PI*2:a;}

    public static float CoastRadius(float a)
    {
        float f=a/(Mathf.PI*2)*N;int i=Mathf.FloorToInt(f);float t=f-i;
        return Mathf.Lerp(_r[((i%N)+N)%N],_r[(i+1)%N],t);
    }

    private static void Shape(Transform root)
    {
        var rng=new System.Random(20261110);
        int[] k={2,3,4,5,7,9,13,19,29};float[] amp={.035f,.05f,.03f,.045f,.03f,.02f,.014f,.009f,.006f};
        var phase=k.Select(_=>(float)rng.NextDouble()*Mathf.PI*2).ToArray();
        var r=new float[N];
        for(int i=0;i<N;i++){
            float a=Angle(i),f=1-.02f;
            for(int j=0;j<k.Length;j++)f+=amp[j]*Mathf.Sin(k[j]*a+phase[j]);
            r[i]=SuperR(a,3)*f;
        }
        // Discs that must stay on land (centre, radius incl. margin).
        var keep=new float[N];
        foreach(var (p,radius) in KeepDiscs(root)){
            var d=p-Centre;float rp=d.magnitude,ap=AngleOf(p);
            for(int i=0;i<N;i++){
                float delta=Mathf.DeltaAngle(ap*Mathf.Rad2Deg,Angle(i)*Mathf.Rad2Deg)*Mathf.Deg2Rad;
                if(Mathf.Abs(delta)>Mathf.PI/2)continue;
                float off=rp*Mathf.Sin(delta);if(Mathf.Abs(off)>=radius)continue;
                keep[i]=Mathf.Max(keep[i],rp*Mathf.Cos(delta)+Mathf.Sqrt(radius*radius-off*off));
            }
        }
        for(int i=0;i<N;i++)r[i]=Mathf.Max(r[i],keep[i]);
        for(int pass=0;pass<2;pass++){
            var s=new float[N];
            for(int i=0;i<N;i++){float sum=0;for(int o=-2;o<=2;o++)sum+=r[(i+o+N)%N];s[i]=sum/5;}
            r=s;
        }
        for(int i=0;i<N;i++)r[i]=Mathf.Max(r[i],keep[i]);
        // Bridge landings: blend to the old straight edge.
        foreach(var an in Anchors){
            float a0=AngleOf(an[0])*Mathf.Rad2Deg,a1=AngleOf(an[1])*Mathf.Rad2Deg;
            float mid=a0+Mathf.DeltaAngle(a0,a1)/2,half=Mathf.Abs(Mathf.DeltaAngle(a0,a1))/2;
            for(int i=0;i<N;i++){
                float d=Mathf.Abs(Mathf.DeltaAngle(mid,Angle(i)*Mathf.Rad2Deg));
                float w=d<=half?1:Mathf.SmoothStep(1,0,(d-half)/7f);
                if(w>0)r[i]=Mathf.Lerp(r[i],SquareR(Angle(i)),w);
            }
        }
        // The canal north of the bank is narrow: barely grow there.
        for(int i=0;i<N;i++){
            float a=Angle(i);var dir=Dir(a);
            bool north=dir.y>Mathf.Abs(dir.x);
            r[i]=Mathf.Min(r[i],SquareR(a)+(north?.4f:2.4f));
        }
        _r=r;_coast=new Vector2[N];
        for(int i=0;i<N;i++)_coast[i]=Centre+Dir(Angle(i))*r[i];
        float minIn=float.MaxValue,maxOut=float.MinValue;
        for(int i=0;i<N;i++){float d=r[i]-SquareR(Angle(i));minIn=Mathf.Min(minIn,d);maxOut=Mathf.Max(maxOut,d);}
        Log.Add($"outline: deepest bay {-minIn:F1} m inside the old edge, furthest headland {maxOut:F1} m outside");
    }

    private static IEnumerable<(Vector2,float)> KeepDiscs(Transform root)
    {
        var grove=root.Find("11 • South grove");
        foreach(Transform t in grove.Find("Trees"))yield return (XZ(t.position),1.9f);
        foreach(Transform t in grove.Find("Seating & light"))yield return (XZ(t.position),1.4f);
        foreach(Transform t in grove.Find("Clearing & paths")){
            var r=t.GetComponent<Renderer>();if(!r)continue;var b=r.bounds;
            if(t.name.StartsWith("Dock path")){
                for(float z=b.min.z;z<=b.max.z;z+=1)yield return (new Vector2(b.center.x,z),b.extents.x+.9f);
            }else yield return (XZ(b.center),Mathf.Max(b.extents.x,b.extents.z)+1.0f);
        }
        // Main path from the north bridge to the clearing.
        var path=root.Find("05 • Canal & bridge/South garden path").GetComponent<Renderer>().bounds;
        for(float z=path.min.z;z<=path.max.z;z+=1)yield return (new Vector2(path.center.x,z),path.extents.x+1.0f);
        // Anything else with a collider standing on the bank top (trees from other groups etc.).
        foreach(var c in root.GetComponentsInChildren<CapsuleCollider>()){
            var p=c.transform.position;
            if(Mathf.Abs(p.y-Top)>.3f||!InOldSquare(p,0)||c.transform.IsChildOf(grove))continue;
            yield return (XZ(p),1.5f);
        }
    }

    private static Vector2 XZ(Vector3 p)=>new Vector2(p.x,p.z);
    private static bool InOldSquare(Vector3 p,float pad)=>p.x>X0-pad&&p.x<X1+pad&&p.z>Z0-pad&&p.z<Z1+pad;
    private static float AnchorWeight(Vector2 p)
    {
        float w=0;
        foreach(var an in Anchors){
            var a=an[0];var b=an[1];var ab=b-a;float t=Mathf.Clamp01(Vector2.Dot(p-a,ab)/ab.sqrMagnitude);
            float d=Vector2.Distance(p,a+ab*t);w=Mathf.Max(w,Mathf.SmoothStep(1,0,d/4f));
        }
        return w;
    }

    // ---------- island mesh ----------
    private static void BankMesh(Transform bank)
    {
        var rng=new System.Random(11);
        var verts=new List<Vector3>();var uvs=new List<Vector2>();var topTris=new List<int>();var cliffTris=new List<int>();
        // Top: centre + rings, uv = local xz like the old box.
        float[] rings={.2f,.4f,.6f,.8f,.92f,1};
        verts.Add(L(bank,new Vector3(Centre.x,Top,Centre.y)));
        foreach(var f in rings)for(int i=0;i<N;i++){var p=Centre+Dir(Angle(i))*_r[i]*f;verts.Add(L(bank,new Vector3(p.x,Top,p.y)));}
        // Lip: the moss rolls over the edge.
        for(int i=0;i<N;i++){var p=_coast[i]+Dir(Angle(i))*.14f;verts.Add(L(bank,new Vector3(p.x,Top-.1f,p.y)));}
        int ringCount=rings.Length+1;
        for(int i=0;i<N;i++){int j=(i+1)%N;topTris.AddRange(new[]{0,1+j,1+i});}
        for(int r=0;r<ringCount-1;r++)for(int i=0;i<N;i++){
            int j=(i+1)%N,a=1+r*N+i,b=1+r*N+j,c=1+(r+1)*N+i,d=1+(r+1)*N+j;
            topTris.AddRange(new[]{a,b,c,b,d,c});
        }
        foreach(var v in verts)uvs.Add(new Vector2(v.x,v.z));

        // Cliff: rocky skirt from the lip down below the water, flat shaded facets.
        (float off,float y)[] bands={(.14f,Top-.1f),(.34f,.62f),(.55f,.05f),(.8f,-.55f),(Toe,-1.15f),(1.35f,-1.75f),(1.6f,-2.7f)};
        var grid=new Vector3[bands.Length,N];var arc=new float[N+1];
        for(int i=0;i<N;i++)arc[i+1]=arc[i]+Vector2.Distance(_coast[i],_coast[(i+1)%N]);
        for(int i=0;i<N;i++){
            var dir=Dir(Angle(i));float steep=Mathf.Lerp(1,.35f,AnchorWeight(_coast[i]));
            for(int b=0;b<bands.Length;b++){
                float jitter=b==0||b==bands.Length-1?0:(float)(rng.NextDouble()-.5)*.28f+.12f*Mathf.Sin(i*.9f+b*1.7f);
                float yj=b==0||b==bands.Length-1?0:(float)(rng.NextDouble()-.5)*.16f;
                var p=_coast[i]+dir*((bands[b].off+jitter)*steep);
                grid[b,i]=new Vector3(p.x,bands[b].y+yj,p.y);
            }
        }
        const float tile=3.5f;
        for(int b=0;b<bands.Length-1;b++)for(int i=0;i<N;i++){
            int j=(i+1)%N;float u0=arc[i]/tile,u1=arc[i+1]/tile;
            var q=new[]{grid[b,i],grid[b,j],grid[b+1,i],grid[b+1,j]};
            var quv=new[]{new Vector2(u0,q[0].y/tile),new Vector2(u1,q[1].y/tile),new Vector2(u0,q[2].y/tile),new Vector2(u1,q[3].y/tile)};
            foreach(var tri in new[]{new[]{0,1,2},new[]{1,3,2}})foreach(var k in tri){
                cliffTris.Add(verts.Count);verts.Add(L(bank,q[k]));uvs.Add(quv[k]);
            }
        }

        var mesh=MeshAsset("SouthBankCoast");
        mesh.indexFormat=IndexFormat.UInt32;mesh.SetVertices(verts);mesh.SetUVs(0,uvs);
        mesh.subMeshCount=2;mesh.SetTriangles(topTris,0);mesh.SetTriangles(cliffTris,1);
        mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();EditorUtility.SetDirty(mesh);
        // Collider = walkable top only.
        var col=MeshAsset("SouthBankCoast_Collider");
        int topCount=1+ringCount*N;
        col.SetVertices(verts.Take(topCount).ToList());col.SetTriangles(topTris,0);col.RecalculateNormals();col.RecalculateBounds();EditorUtility.SetDirty(col);

        bank.GetComponent<MeshFilter>().sharedMesh=mesh;
        var moss=AssetDatabase.LoadAssetAtPath<Material>(Project+"Art/WillowCourt/Materials/Moss.mat");
        bank.GetComponent<MeshRenderer>().sharedMaterials=new[]{moss,CliffMaterial()};

        var box=bank.GetComponent<BoxCollider>();var meshCol=bank.GetComponent<MeshCollider>();
        if(!meshCol)meshCol=bank.gameObject.AddComponent<MeshCollider>();
        meshCol.sharedMesh=null;meshCol.sharedMesh=col;
        foreach(var spawn in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include)){
            if(spawn.GetType().Name!="SquareSpawnPlacement")continue;
            var so=new SerializedObject(spawn);var arr=so.FindProperty("_surfaces");
            for(int i=0;i<arr.arraySize;i++){
                var e=arr.GetArrayElementAtIndex(i);
                if(e.objectReferenceValue==box||e.objectReferenceValue==null)e.objectReferenceValue=meshCol;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        if(box)Object.DestroyImmediate(box);
        Log.Add($"mesh: {verts.Count} vertices, collider = top surface ({topCount} vertices)");
    }

    private static Vector3 L(Transform t,Vector3 world)=>t.InverseTransformPoint(world);

    private static Mesh MeshAsset(string name)
    {
        string path=Art+"Meshes/"+name+".asset";
        var m=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if(m){m.Clear();return m;}
        m=new Mesh{name=name};AssetDatabase.CreateAsset(m,path);return m;
    }

    private static Material CliffMaterial()
    {
        string path=Art+"Materials/Coast cliff.mat";
        var m=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(m)return m;
        var tex=Texture("CoastCliff.png",false);
        m=new Material(Shader.Find("Universal Render Pipeline/Lit")){name="Coast cliff"};
        m.SetTexture("_BaseMap",tex);m.SetColor("_BaseColor",new Color(.93f,.9f,.88f));m.SetFloat("_Smoothness",.06f);
        AssetDatabase.CreateAsset(m,path);return m;
    }

    private static Texture2D Texture(string file,bool alpha)
    {
        string path=Art+"Textures/"+file;
        var importer=(TextureImporter)AssetImporter.GetAtPath(path);
        if(importer.alphaIsTransparency!=alpha||importer.wrapMode!=(alpha?TextureWrapMode.Clamp:TextureWrapMode.Repeat)||importer.textureCompression!=TextureImporterCompression.Uncompressed){
            importer.textureType=TextureImporterType.Default;importer.alphaIsTransparency=alpha;importer.mipmapEnabled=true;
            importer.wrapMode=alpha?TextureWrapMode.Clamp:TextureWrapMode.Repeat;importer.filterMode=FilterMode.Bilinear;importer.anisoLevel=4;
            importer.maxTextureSize=2048;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    // ---------- old square edge ----------
    private static void RemoveOldEdges(Transform root)
    {
        int n=0;
        var foundation=root.Find("05 • Canal & bridge/South bank submerged foundation");
        if(foundation){Object.DestroyImmediate(foundation.gameObject);n++;}
        var gb=root.Find("11 • South grove/Boundaries");if(gb){Object.DestroyImmediate(gb.gameObject);n++;}
        var eb=root.Find("12 • East meadow/Boundaries");
        if(eb)foreach(Transform w in eb.Cast<Transform>().ToList())
            if(Mathf.Abs(w.position.x-21.9f)<.3f){Object.DestroyImmediate(w.gameObject);n++;}
        var perimeter=root.Find("10 • Safe waterfront perimeter");
        foreach(Transform b in perimeter.Cast<Transform>().ToList()){
            if(b.name!="Carved stone balustrade")continue;
            var bounds=new Bounds(b.GetChild(0).position,Vector3.zero);
            foreach(var r in b.GetComponentsInChildren<Renderer>())bounds.Encapsulate(r.bounds);
            if(bounds.max.z<-25.5f&&bounds.min.z>-27.5f){Object.DestroyImmediate(b.gameObject);n++;}
        }
        if(n>0)Log.Add($"removed {n} old square-edge objects (foundation, straight walls, north balustrades)");
    }

    // Delete small planting that now hangs over the water, and water props now under the land.
    private static void Prune(Transform root)
    {
        string[] containers={"11 • South grove/Planting","11 • South grove/Water life","11 • South grove/Clearing & paths",
            "03 • Layered planting","09 • Hand finished details","14 • Waterfront atelier"};
        int land=0,water=0;var kept=new List<string>();
        foreach(var path in containers){
            var c=root.Find(path);if(!c)continue;
            foreach(Transform t in c.Cast<Transform>().ToList()){
                var p=t.position;if(!InOldSquare(p,6))continue;
                float rp=Vector2.Distance(XZ(p),Centre),rc=CoastRadius(AngleOf(XZ(p)));
                if(p.y>Top-.3f&&p.y<Top+.5f&&InOldSquare(p,.5f)){
                    if(rp>rc-.35f){
                        if(t.GetComponent<Collider>()&&!t.name.StartsWith("shrub")){kept.Add(t.name);continue;}
                        Object.DestroyImmediate(t.gameObject);land++;
                    }
                }else if(p.y<0&&rp<rc+Toe+.6f){Object.DestroyImmediate(t.gameObject);water++;}
            }
        }
        Log.Add($"pruned {land} plants off the new edge, {water} water props now under land");
        if(kept.Count>0)Log.Add("WARNING outside the coast but kept: "+string.Join(", ",kept));
    }

    // ---------- invisible walls ----------
    private static bool InGap(Vector2 p)=>
        (p.x>-4.0f&&p.x<2.2f&&p.y>Z1-1.2f)||(p.x>-3.2f&&p.x<1.5f&&p.y<Z0+1.2f)||(p.y>-56.5f&&p.y<-51.7f&&p.x>X1-1.2f);

    private static void Boundaries(Transform group)
    {
        var parent=new GameObject("Coast boundary").transform;parent.SetParent(group,false);
        var pts=new List<Vector2>();
        for(int i=0;i<N;i++){
            var a=Centre+Dir(Angle(i))*(_r[i]-.45f);var b=Centre+Dir(Angle((i+1)%N))*(_r[(i+1)%N]-.45f);
            int steps=Mathf.CeilToInt(Vector2.Distance(a,b)/.1f);
            for(int s=0;s<steps;s++)pts.Add(Vector2.Lerp(a,b,(float)s/steps));
        }
        // Rotate so the list starts inside a gap (runs never wrap).
        int start=pts.FindIndex(InGap);pts=pts.Skip(start).Concat(pts.Take(start)).ToList();
        int count=0;
        for(int i=0;i<pts.Count;){
            int s=i,e=i+1;
            if(InGap(pts[s])||e>=pts.Count||InGap(pts[e])){i++;continue;}
            while(e+1<pts.Count&&!InGap(pts[e+1])&&Chord(pts,s,e+1))e++;
            var a=pts[s];var b=pts[e];var mid=(a+b)/2;float l=Vector2.Distance(a,b);
            var w=new GameObject("Invisible coast boundary").transform;w.SetParent(parent,false);
            w.position=new Vector3(mid.x,Top+2,mid.y);w.rotation=Quaternion.LookRotation(new Vector3(b.x-a.x,0,b.y-a.y));
            w.gameObject.AddComponent<BoxCollider>().size=new Vector3(.3f,5,l+.06f);
            count++;i=e;
        }
        Log.Add($"boundary: {count} invisible wall pieces along the coast, gaps at the 3 bridges");
    }

    private static bool Chord(List<Vector2> pts,int s,int e)
    {
        if(Vector2.Distance(pts[s],pts[e])>2.5f)return false;
        for(int k=s+1;k<e;k++)if(DistToSegment(pts[k],pts[s],pts[e])>.08f)return false;
        return true;
    }

    private static float DistToSegment(Vector2 p,Vector2 a,Vector2 b)
    {
        var ab=b-a;float t=ab.sqrMagnitude<1e-6f?0:Mathf.Clamp01(Vector2.Dot(p-a,ab)/ab.sqrMagnitude);
        return Vector2.Distance(p,a+ab*t);
    }

    // ---------- shore planting (ChatGPT painted props) ----------
    private static readonly (string name,float height)[] Props={
        ("ShoreBoulders",.95f),("ShoreFlatRock",.8f),("ShorePebbles",.32f),("ShoreReeds",1.5f),("ShoreIris",.8f),("ShoreFernRock",.85f)};

    private static void ShorePlanting(Transform group)
    {
        var rng=new System.Random(7);
        float R()=>(float)rng.NextDouble();
        var waterline=new GameObject("Waterline rocks & reeds").transform;waterline.SetParent(group,false);
        var rim=new GameObject("Rim planting").transform;rim.SetParent(group,false);
        var lilies=new GameObject("Lily pads").transform;lilies.SetParent(group,false);
        var grass=AssetDatabase.LoadAssetAtPath<GameObject>(Project+"Art/Square/Props/grass_tall.prefab");
        var lilyMesh=AssetDatabase.LoadAssetAtPath<Mesh>(Art+"Meshes/Lily.asset");
        var lilyMat=AssetDatabase.LoadAssetAtPath<Material>(Art+"Materials/Lily jade.mat");
        int nWater=0,nRim=0,nLily=0;
        float nextWater=0,nextRim=0,nextLily=8,arc=0;
        var toCamera=new Vector2(-.7071f,-.7071f);
        for(int i=0;i<N;i++){
            var a=Angle(i);var dir=Dir(a);var c=_coast[i];
            arc+=Vector2.Distance(c,_coast[(i+1)%N]);
            bool nearBridge=AnchorWeight(c)>.15f;
            bool facesCamera=Vector2.Dot(dir,toCamera)>-.35f;
            if(!nearBridge&&facesCamera&&arc>=nextWater){
                int items=1+rng.Next(3);
                for(int k=0;k<items;k++){
                    int pick=rng.Next(10);int idx=pick<3?0:pick<5?1:pick<6?2:pick<8?3:5;
                    var tangent=new Vector2(-dir.y,dir.x);
                    var p=c+dir*(Toe+.35f+R()*.6f)+tangent*((R()-.5f)*2.4f);
                    Prop(waterline,idx,new Vector3(p.x,WaterY-.06f,p.y),1.25f+R()*.6f,rng.Next(2)==0);nWater++;
                }
                nextWater=arc+4.5f+R()*5;
            }
            if(!nearBridge&&arc>=nextRim){
                var p=c-dir*(.35f+R()*.5f);
                if(!NearKeep(p)){
                    if(R()<.35f){Prop(rim,4,new Vector3(p.x,Top,p.y),.75f+R()*.35f,rng.Next(2)==0);}
                    else{
                        var g=(GameObject)PrefabUtility.InstantiatePrefab(grass,rim);
                        g.transform.position=new Vector3(p.x,Top,p.y);g.transform.localScale=Vector3.one*(.5f+R()*.3f);
                        var blob=g.transform.Find("Blob");if(blob)blob.gameObject.SetActive(false);
                        if(R()<.5f){var s=g.transform.Find("Sprite");if(s)s.localScale=new Vector3(-s.localScale.x,s.localScale.y,s.localScale.z);}
                    }
                    nRim++;
                }
                nextRim=arc+1.6f+R()*2.2f;
            }
            // Lilies float in the bays (coast pulled in) on the open-water sides.
            bool bay=_r[i]<SquareR(a)-1.5f;bool north=dir.y>Mathf.Abs(dir.x);
            if(bay&&!north&&!nearBridge&&arc>=nextLily&&lilyMesh&&lilyMat){
                var centre=c+dir*(Toe+1.6f+R()*1.4f);int leaves=5+rng.Next(4);
                for(int k=0;k<leaves;k++){
                    float la=R()*Mathf.PI*2,lr=R()*1.3f;
                    var leaf=new GameObject("Notched water lily",typeof(MeshFilter),typeof(MeshRenderer));leaf.transform.SetParent(lilies,false);
                    leaf.transform.position=new Vector3(centre.x+Mathf.Cos(la)*lr,WaterY+.021f,centre.y+Mathf.Sin(la)*lr);
                    leaf.transform.rotation=Quaternion.Euler(0,R()*360,0);leaf.transform.localScale=Vector3.one*(.16f+R()*.14f);
                    leaf.GetComponent<MeshFilter>().sharedMesh=lilyMesh;var mr=leaf.GetComponent<MeshRenderer>();mr.sharedMaterial=lilyMat;mr.shadowCastingMode=ShadowCastingMode.Off;
                    nLily++;
                }
                nextLily=arc+9+R()*8;
            }
        }
        Log.Add($"planting: {nWater} waterline rocks/reeds, {nRim} rim tufts/irises, {nLily} lily leaves");
    }

    private static List<(Vector2,float)> _keepCache;
    private static bool NearKeep(Vector2 p)
    {
        if(_keepCache==null)_keepCache=KeepDiscs(GameObject.Find("Willow Court").transform).ToList();
        foreach(var (c,r) in _keepCache)if(Vector2.Distance(p,c)<r-.6f)return true;
        return false;
    }

    private static Mesh BottomQuad()
    {
        string path=Art+"Meshes/BottomQuad.asset";
        var q=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(q)return q;
        q=new Mesh{name="Bottom quad",vertices=new[]{new Vector3(-.5f,0,0),new Vector3(.5f,0,0),new Vector3(-.5f,1,0),new Vector3(.5f,1,0)},
            uv=new[]{new Vector2(0,0),new Vector2(1,0),new Vector2(0,1),new Vector2(1,1)},triangles=new[]{0,2,1,2,3,1}};
        q.RecalculateNormals();q.RecalculateBounds();AssetDatabase.CreateAsset(q,path);return q;
    }

    private static Material PropMaterial(string name)
    {
        string path=Art+"Materials/"+name+".mat";
        var m=AssetDatabase.LoadAssetAtPath<Material>(path);if(m)return m;
        m=new Material(AssetDatabase.LoadAssetAtPath<Material>(Project+"Materials/Garden/shrub.mat")){name=name};
        m.mainTexture=Texture(name+".png",true);m.color=Color.white;m.SetFloat("_Cutoff",.35f);m.SetFloat("_Smoothness",0);m.SetFloat("_Cull",0);
        AssetDatabase.CreateAsset(m,path);return m;
    }

    private static void Prop(Transform parent,int index,Vector3 position,float scale,bool flip)
    {
        var (name,height)=Props[index];var mat=PropMaterial(name);var tex=(Texture2D)mat.mainTexture;
        float h=height*scale,w=h*tex.width/tex.height;
        var root=new GameObject(name).transform;root.SetParent(parent,false);root.position=position;
        var sprite=new GameObject("Sprite",typeof(MeshFilter),typeof(MeshRenderer));sprite.transform.SetParent(root,false);
        sprite.transform.localRotation=Quaternion.Euler(30,45,0);sprite.transform.localScale=new Vector3(flip?-w:w,h,1);
        sprite.GetComponent<MeshFilter>().sharedMesh=BottomQuad();
        var r=sprite.GetComponent<MeshRenderer>();r.sharedMaterial=mat;r.shadowCastingMode=ShadowCastingMode.Off;
    }

    // ---------- water shore map ----------
    private static void BakeShore(Transform root,Transform group,Transform bank)
    {
        var water=root.Find("05 • Canal & bridge/Canal water").GetComponent<Renderer>();
        var mat=water.sharedMaterial;string texPath=Art+"Textures/BankDistance.png";
        if(!mat.HasProperty("_ShoreMap")){Log.Add("WARNING water material has no _ShoreMap; shore map not baked");return;}
        var rect=mat.GetVector("_MapRect");
        var tex=new Texture2D(2,2,TextureFormat.RGB24,false,true);tex.LoadImage(File.ReadAllBytes(texPath));
        int size=tex.width;var px=tex.GetPixels32();
        var others=root.GetComponentsInChildren<Renderer>()
            .Where(r=>r!=water&&r.transform!=bank&&!r.transform.IsChildOf(group)&&r.bounds.min.y<WaterY-.05f&&r.bounds.max.y>WaterY+.05f&&r.bounds.size.x>.6f&&r.bounds.size.z>.6f)
            .Select(r=>r.bounds).ToArray();
        float BoxDist(float wx,float wz){
            float d=12;
            foreach(var b in others){float dx=Mathf.Max(b.min.x-wx,0,wx-b.max.x),dz=Mathf.Max(b.min.z-wz,0,wz-b.max.z);d=Mathf.Min(d,Mathf.Sqrt(dx*dx+dz*dz));}
            return d;
        }
        int changed=0;
        for(int y=0;y<size;y++)for(int x=0;x<size;x++){
            float wx=rect.x+(x+.5f)/size*rect.z,wz=rect.y+(y+.5f)/size*rect.w;
            if(wx<X0-14||wx>X1+14||wz<Z0-14||wz>Z1+14)continue;
            var p=new Vector2(wx,wz);float rp=Vector2.Distance(p,Centre),rc=CoastRadius(AngleOf(p));
            float dPoly;
            if(rp<rc+Toe)dPoly=0;
            else{dPoly=float.MaxValue;for(int i=0;i<N;i++)dPoly=Mathf.Min(dPoly,DistToSegment(p,_coast[i],_coast[(i+1)%N]));dPoly=Mathf.Max(0,dPoly-Toe);}
            float oldVal=px[y*size+x].r/255f*12;
            float dxs=Mathf.Max(X0-wx,0,wx-X1),dzs=Mathf.Max(Z0-wz,0,wz-Z1),dSquare=Mathf.Sqrt(dxs*dxs+dzs*dzs);
            // The old map = min(square, other banks). Where the square did not decide it, keep the old value;
            // elsewhere the other banks are at least as far as the old value.
            float dOther=dSquare>oldVal+.3f?oldVal:Mathf.Max(BoxDist(wx,wz),oldVal);
            float d=Mathf.Min(12,Mathf.Min(dPoly,dOther));
            byte v=(byte)Mathf.RoundToInt(d/12*255);
            if(v!=px[y*size+x].r)changed++;
            px[y*size+x]=new Color32(v,v,v,255);
        }
        tex.SetPixels32(px);tex.Apply();File.WriteAllBytes(texPath,tex.EncodeToPNG());Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(texPath);
        Log.Add($"shore map: {changed} pixels changed around the South bank");
    }
}

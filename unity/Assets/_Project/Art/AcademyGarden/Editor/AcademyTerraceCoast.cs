using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

// 2026-11-10: the Academy terrace becomes natural ground like the north garden lawn.
// - The pink terrace paving turns into lawn (Moss, world-space UVs so it lines up with the north garden).
//   (A paved apron and stepping stones were tried and removed on the owner's request: plain lawn looks better.)
// - The straight west wall + stone balustrade become a grassy rim with a natural rock cliff (like
//   "17 • East heights cliffs"). The rim tapers to nothing at both ends: south where the lower court
//   wall continues, north where the north garden's rock coast starts.
// Re-running rebuilds "19 • Academy terrace cliffs" and re-bakes the north shore map (min with the current map).
public static class AcademyTerraceCoast
{
    private const string Art="Assets/_Project/Art/AcademyGarden/";
    private const string Project="Assets/_Project/";
    private const string GroupName="19 • Academy terrace cliffs";
    private const float XW=-20.25f,ZS=4.86f,ZN=21.055f,Top=2.74f,Taper=4f;
    private static readonly Vector2 Out=Vector2.left;
    private static readonly List<string> Log=new List<string>();
    private static List<Vector2> _p,_c;private static List<float> _w,_s,_steep;

    [MenuItem("DG Village/Waterfront/Academy terrace cliffs")]
    public static void Menu()=>Debug.Log(Build());

    public static string Build()
    {
        Log.Clear();
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Leave Play mode first.");
        var root=GameObject.Find("Willow Court")?.transform;
        if(!root)throw new InvalidOperationException("Open SquareScene first (Willow Court root missing).");
        var old=root.Find(GroupName);if(old)Object.DestroyImmediate(old.gameObject);
        // Willow Court sits at y 1.2: put the group at the world origin so the world-space meshes line up.
        var group=new GameObject(GroupName).transform;group.SetParent(root,false);group.position=Vector3.zero;
        Outline();
        RemoveOldEdge(root);
        Lawn(root);
        var cliff=new GameObject("Cliff rim",typeof(MeshFilter),typeof(MeshRenderer),typeof(MeshCollider));
        cliff.transform.SetParent(group,false);GameObjectUtility.SetStaticEditorFlags(cliff,(StaticEditorFlags)~0);
        BuildMesh(cliff);
        Prune(root,group);
        Boundaries(group);
        Planting(group);
        SpawnSurface(cliff.GetComponent<MeshCollider>());
        BakeShore(root);
        EditorSceneManager.MarkSceneDirty(root.gameObject.scene);AssetDatabase.SaveAssets();
        return "Academy terrace cliffs\n"+string.Join("\n",Log);
    }

    // ---------- outline (old west edge, south → north) ----------
    private static void Outline()
    {
        _p=new List<Vector2>();_s=new List<float>();
        int k=Mathf.CeilToInt((ZN-ZS)/.5f);
        for(int i=0;i<=k;i++){_p.Add(new Vector2(XW,Mathf.Lerp(ZS,ZN,(float)i/k)));_s.Add(i*(ZN-ZS)/k);}
        float total=_s[_s.Count-1];
        var rng=new System.Random(1910);
        float[] wave={23,13,7.3f,4.1f},amp={1.0f,.6f,.3f,.15f};
        var phase=wave.Select(_=>(float)rng.NextDouble()*Mathf.PI*2).ToArray();
        _w=new List<float>();_steep=new List<float>();_c=new List<Vector2>();
        for(int i=0;i<_p.Count;i++){
            float w=2.6f;for(int j=0;j<wave.Length;j++)w+=amp[j]*Mathf.Sin(_s[i]/wave[j]*Mathf.PI*2+phase[j]);
            w=Mathf.Clamp(w,.8f,4.2f);
            float end=Mathf.Min(_s[i],total-_s[i]);float t=Mathf.SmoothStep(0,1,end/Taper);
            _w.Add(w*t);_steep.Add(Mathf.Lerp(.35f,1,t));_c.Add(_p[i]+Out*_w[i]);
        }
        Log.Add($"outline: {_p.Count} samples, rim width up to {_w.Max():F1} m");
    }

    private static bool InsideLand(Vector2 q)
    {
        // New coast south → north, closed along the old wall line.
        bool inside=false;int n=_c.Count;
        for(int i=0,j=n-1;i<n;j=i++){
            var a=_c[i];var b=_c[j];
            if((a.y>q.y)!=(b.y>q.y)&&q.x<(b.x-a.x)*(q.y-a.y)/(b.y-a.y)+a.x)inside=!inside;
        }
        return inside;
    }

    private static float CoastDistance(Vector2 q)
    {
        float d=float.MaxValue;for(int i=0;i<_c.Count-1;i++)d=Mathf.Min(d,SouthBankCoast.DistToSegment(q,_c[i],_c[i+1]));return d;
    }

    // ---------- old edge ----------
    private static void RemoveOldEdge(Transform root)
    {
        int n=0;
        foreach(Transform b in root.Find("10 • Safe waterfront perimeter").Cast<Transform>().ToList()){
            if(!b.gameObject.activeSelf||b.name!="Carved stone balustrade")continue;
            var bounds=Bounds(b);
            if(bounds.max.x<XW+1.2f&&bounds.min.z>ZS-.2f&&bounds.size.z>10){Object.DestroyImmediate(b.gameObject);n++;}
        }
        if(n>0)Log.Add($"removed {n} west terrace balustrade");
        // The west district wall stops at the terrace's south corner; the cliff boundary takes over from there.
        foreach(var c in root.Find("08 • Traversal boundaries").GetComponentsInChildren<BoxCollider>()){
            var b=c.bounds;if(b.max.x>XW+1.5f||b.size.z<20)continue;
            const float max=ZS+.75f;var pos=c.transform.position;pos.z=(b.min.z+max)*.5f;c.transform.position=pos;
            var size=c.size;size.z=(max-b.min.z)/c.transform.lossyScale.z;c.size=size;
            Log.Add($"west district boundary now ends at z {max:F2}");
        }
    }

    private static Bounds Bounds(Transform t)
    {
        var b=new Bounds(t.position,Vector3.zero);bool any=false;
        foreach(var r in t.GetComponentsInChildren<Renderer>(true)){if(!any){b=r.bounds;any=true;}else b.Encapsulate(r.bounds);}
        return b;
    }

    // ---------- lawn ----------
    private static Mesh MeshAsset(string name)
    {
        string path=Art+"Meshes/"+name+".asset";
        var m=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if(m){m.Clear();return m;}
        m=new Mesh{name=name};AssetDatabase.CreateAsset(m,path);return m;
    }

    private static void Lawn(Transform root)
    {
        var moss=AssetDatabase.LoadAssetAtPath<Material>(Project+"Art/WillowCourt/Materials/Moss.mat");
        var terraces=root.Find("01 • Terraces, stairs & foundations");
        var paving=terraces.Find("Terrace paving")??terraces.Find("Terrace lawn");
        var mf=paving.GetComponent<MeshFilter>();
        // Keep the box shape, but shift the 1 uv = 1 m local mapping to world space like the north garden lawn.
        var source=mf.sharedMesh.name=="Academy terrace lawn"?null:mf.sharedMesh;
        if(source){
            var lawn=MeshAsset("Academy terrace lawn");
            lawn.SetVertices(source.vertices);lawn.SetNormals(source.normals);lawn.SetTangents(source.tangents);
            var shift=new Vector2(paving.position.x,paving.position.z);
            lawn.SetUVs(0,source.uv.Select(u=>u+shift).ToList());
            lawn.subMeshCount=source.subMeshCount;for(int i=0;i<source.subMeshCount;i++)lawn.SetTriangles(source.GetTriangles(i),i);
            lawn.RecalculateBounds();EditorUtility.SetDirty(lawn);mf.sharedMesh=lawn;
        }
        paving.GetComponent<MeshRenderer>().sharedMaterial=moss;
        paving.name="Terrace lawn";
        Log.Add("terrace paving → lawn");
    }

    // ---------- cliff mesh ----------
    private static void BuildMesh(GameObject go)
    {
        var rng=new System.Random(41);
        var verts=new List<Vector3>();var uvs=new List<Vector2>();var top=new List<int>();var rock=new List<int>();
        int n=_p.Count;var nOut=Out;
        float[] rows={0,.35f,.7f,1};
        foreach(var f in rows)for(int i=0;i<n;i++){var q=Vector2.Lerp(_p[i]-nOut*.3f,_c[i],f);verts.Add(new Vector3(q.x,Top,q.y));}
        for(int i=0;i<n;i++){var q=_c[i]+nOut*.14f*_steep[i];verts.Add(new Vector3(q.x,Top-.1f,q.y));}
        int rings=rows.Length+1;
        // Same winding as EastHeightsCoast (whose west edge also runs south → north).
        for(int r=0;r<rings-1;r++)for(int i=0;i<n-1;i++){int a=r*n+i,b=a+1,c=a+n,d=c+1;top.AddRange(new[]{a,c,b,b,c,d});}
        foreach(var v in verts)uvs.Add(new Vector2(v.x,v.z));
        int topCount=verts.Count;

        (float off,float y)[] bands={(.14f,Top-.1f),(.4f,2.05f),(.7f,1.35f),(1.0f,.6f),(1.3f,-.15f),(1.6f,-.85f),(1.9f,-1.5f),(2.2f,-2.1f),(2.5f,-2.9f)};
        var grid=new Vector3[bands.Length,n];
        for(int i=0;i<n;i++)for(int b=0;b<bands.Length;b++){
            bool edge=b==0||b==bands.Length-1;
            float j=edge?0:(float)(rng.NextDouble()-.5)*.4f+.28f*Mathf.Sin(_s[i]*.5f+b*1.3f)+.22f*Mathf.Sin(_s[i]*.15f+b*.7f);
            float yj=edge?0:(float)(rng.NextDouble()-.5)*.22f;
            var q=_c[i]+nOut*Mathf.Max(.05f,bands[b].off+j)*_steep[i];
            grid[b,i]=new Vector3(q.x,bands[b].y+yj,q.y);
        }
        const float tile=3.5f;
        void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,float ua,float ub){
            var q=new[]{a,b,c,d};var uv=new[]{new Vector2(ua,a.y/tile),new Vector2(ub,b.y/tile),new Vector2(ua,c.y/tile),new Vector2(ub,d.y/tile)};
            foreach(var k in new[]{0,2,1,1,2,3}){rock.Add(verts.Count);verts.Add(q[k]);uvs.Add(uv[k]);}
        }
        float arc=0;
        for(int i=0;i<n-1;i++){
            float da=Vector2.Distance(_c[i],_c[i+1]);
            for(int b=0;b<bands.Length-1;b++)Quad(grid[b,i],grid[b,i+1],grid[b+1,i],grid[b+1,i+1],arc/tile,(arc+da)/tile);
            arc+=da;
        }
        foreach(int i in new[]{0,n-1}){
            for(int b=0;b<bands.Length-1;b++){
                var wa=new Vector3(_p[i].x,grid[b,i].y,_p[i].y);var wb=new Vector3(_p[i].x,grid[b+1,i].y,_p[i].y);
                Quad(wa,grid[b,i],wb,grid[b+1,i],0,1);Quad(grid[b,i],wa,grid[b+1,i],wb,0,1);
            }
        }
        var mesh=MeshAsset("AcademyTerraceCliff");
        mesh.indexFormat=IndexFormat.UInt32;mesh.SetVertices(verts);mesh.SetUVs(0,uvs);mesh.subMeshCount=2;
        mesh.SetTriangles(top,0);mesh.SetTriangles(rock,1);mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();EditorUtility.SetDirty(mesh);
        var col=MeshAsset("AcademyTerraceCliff_Collider");
        col.SetVertices(verts.Take(topCount).ToList());col.SetTriangles(top,0);col.RecalculateNormals();col.RecalculateBounds();EditorUtility.SetDirty(col);
        go.GetComponent<MeshFilter>().sharedMesh=mesh;
        go.GetComponent<MeshRenderer>().sharedMaterials=new[]{AssetDatabase.LoadAssetAtPath<Material>(Project+"Art/WillowCourt/Materials/Moss.mat"),SouthBankCoast.CliffMaterial()};
        var mc=go.GetComponent<MeshCollider>();mc.sharedMesh=null;mc.sharedMesh=col;
        // Sanity: the top must face up and the cliff face west (towards the water).
        Log.Add($"mesh: {verts.Count} vertices, top normal y {col.normals[n+n/2].y:F2}");
    }

    // ---------- water props now inside the cliff ----------
    private static void Prune(Transform root,Transform group)
    {
        int n=0;
        foreach(var r in root.GetComponentsInChildren<Transform>(true).ToList()){
            if(!r||r.IsChildOf(group))continue;
            var p=r.position;if(p.y>0||p.y<-2)continue;
            if(r.childCount>0||!(r.name.Contains("lily")||r.name.Contains("stone")||r.name.Contains("grass")))continue;
            var q=new Vector2(p.x,p.z);
            if(q.x<XW&&q.y>ZS-.5f&&q.y<ZN+.5f&&(InsideLand(q)||CoastDistance(q)<2.4f)){
                var target=r.parent!=null&&r.parent.name.Contains("grass")?r.parent:r;Object.DestroyImmediate(target.gameObject);n++;
            }
        }
        if(n>0)Log.Add($"pruned {n} water props now under the cliff");
    }

    // ---------- invisible walls along the rim ----------
    private static void Boundaries(Transform group)
    {
        var parent=new GameObject("Cliff boundary").transform;parent.SetParent(group,false);
        var pts=new List<Vector2>();
        for(int i=0;i<_c.Count-1;i++){
            var a=_c[i]-Out*.45f;var b=_c[i+1]-Out*.45f;int k=Mathf.Max(1,Mathf.CeilToInt(Vector2.Distance(a,b)/.1f));
            for(int s=0;s<k;s++)pts.Add(Vector2.Lerp(a,b,(float)s/k));
        }
        pts.Add(_c[_c.Count-1]-Out*.45f);
        int count=0;
        for(int i=0;i<pts.Count-1;){
            int e=i+1;
            while(e+1<pts.Count&&Chord(pts,i,e+1))e++;
            var a=pts[i];var b=pts[e];var mid=(a+b)/2;
            var w=new GameObject("Invisible cliff boundary").transform;w.SetParent(parent,false);
            w.position=new Vector3(mid.x,Top+2,mid.y);w.rotation=Quaternion.LookRotation(new Vector3(b.x-a.x,0,b.y-a.y));
            w.gameObject.AddComponent<BoxCollider>().size=new Vector3(.3f,5,Vector2.Distance(a,b)+.06f);
            count++;i=e;
        }
        Log.Add($"boundary: {count} invisible wall pieces along the rim");
    }

    private static bool Chord(List<Vector2> pts,int s,int e)
    {
        if(Vector2.Distance(pts[s],pts[e])>2.5f)return false;
        for(int k=s+1;k<e;k++)if(SouthBankCoast.DistToSegment(pts[k],pts[s],pts[e])>.08f)return false;
        return true;
    }

    // ---------- planting ----------
    private static void Planting(Transform group)
    {
        var rng=new System.Random(19);float R()=>(float)rng.NextDouble();
        var rim=new GameObject("Rim planting").transform;rim.SetParent(group,false);
        var ivy=new GameObject("Hanging ivy").transform;ivy.SetParent(group,false);
        var grass=AssetDatabase.LoadAssetAtPath<GameObject>(Project+"Art/Square/Props/grass_tall.prefab");
        int nRim=0,nIvy=0;float nextRim=.8f,nextIvy=1.5f;float last=_s[_s.Count-1];
        for(int i=0;i<_p.Count;i++){
            float s=_s[i],w=_w[i];var along=Vector2.up;
            if(w>=.6f&&s>=nextRim){
                var q=Vector2.Lerp(_p[i],_c[i],.3f+R()*.45f)+along*(R()-.5f);
                float pick=R();
                if(pick<.4f){
                    var g=(GameObject)PrefabUtility.InstantiatePrefab(grass,rim);g.transform.position=new Vector3(q.x,Top,q.y);
                    g.transform.localScale=Vector3.one*(.5f+R()*.35f);var blob=g.transform.Find("Blob");if(blob)blob.gameObject.SetActive(false);
                }else Sprite(rim,pick<.75f?"CliffWildflowers":pick<.9f?"CliffFerns":"CliffJuniper",new Vector3(q.x,Top,q.y),(pick<.75f?.65f:pick<.9f?.8f:.9f)*(R()*.3f+.85f),R()<.5f);
                nRim++;nextRim=s+1.1f+R()*1.6f;
            }
            // The west cliff faces the camera: ivy curtains hang over it (shorter than on the taller East heights).
            if(s>=nextIvy&&s>2&&s<last-2){
                string[] names={"CliffIvyLong","CliffIvyFlower","CliffIvyWide"};float[] hs={3.0f,2.0f,2.2f};int k=rng.Next(3);
                float h=hs[k]*(.8f+R()*.35f);
                var lip=_c[i]+Out*(.45f+R()*.2f);var topPoint=new Vector3(lip.x,Top+.05f,lip.y);
                var down=Quaternion.Euler(30,45,0)*Vector3.up;
                Sprite(ivy,names[k],topPoint-down*h,h,R()<.5f);nIvy++;nextIvy=s+2.4f+R()*3f;
            }
        }
        Log.Add($"planting: {nRim} rim plants, {nIvy} hanging ivy curtains");
    }

    private static void Sprite(Transform parent,string name,Vector3 position,float height,bool flip)
    {
        var mat=SouthBankCoast.PropMaterial(name);var tex=(Texture2D)mat.mainTexture;
        float w=height*tex.width/tex.height;
        var root=new GameObject(name).transform;root.SetParent(parent,false);root.position=position;
        var sprite=new GameObject("Sprite",typeof(MeshFilter),typeof(MeshRenderer));sprite.transform.SetParent(root,false);
        sprite.transform.localRotation=Quaternion.Euler(30,45,0);sprite.transform.localScale=new Vector3(flip?-w:w,height,1);
        sprite.GetComponent<MeshFilter>().sharedMesh=SouthBankCoast.BottomQuad();
        var r=sprite.GetComponent<MeshRenderer>();r.sharedMaterial=mat;r.shadowCastingMode=ShadowCastingMode.Off;
    }

    // ---------- spawn surface & water ----------
    private static void SpawnSurface(MeshCollider collider)
    {
        foreach(var spawn in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include)){
            if(spawn.GetType().Name!="SquareSpawnPlacement")continue;
            var so=new SerializedObject(spawn);var arr=so.FindProperty("_surfaces");
            for(int i=arr.arraySize-1;i>=0;i--)if(arr.GetArrayElementAtIndex(i).objectReferenceValue==null)arr.DeleteArrayElementAtIndex(i);
            bool has=false;for(int i=0;i<arr.arraySize;i++)if(arr.GetArrayElementAtIndex(i).objectReferenceValue==collider)has=true;
            if(!has){arr.InsertArrayElementAtIndex(arr.arraySize);arr.GetArrayElementAtIndex(arr.arraySize-1).objectReferenceValue=collider;}
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private static void BakeShore(Transform root)
    {
        var mat=root.Find("05 • Canal & bridge/Canal water").GetComponent<Renderer>().sharedMaterial;
        if(!mat.HasProperty("_MapRect")||!mat.GetTexture("_ShoreMap")){Log.Add("WARNING water has no shore map");return;}
        var rect=mat.GetVector("_MapRect");string path=AssetDatabase.GetAssetPath(mat.GetTexture("_ShoreMap"));
        if(!path.StartsWith(Art)){Log.Add("WARNING shore map is not the academy garden copy; skipped "+path);return;}
        var tex=new Texture2D(2,2,TextureFormat.RGB24,false,true);tex.LoadImage(File.ReadAllBytes(path));
        int size=tex.width;var px=tex.GetPixels32();int changed=0;
        for(int y=0;y<size;y++)for(int x=0;x<size;x++){
            float wx=rect.x+(x+.5f)/size*rect.z,wz=rect.y+(y+.5f)/size*rect.w;
            if(wx<XW-18||wx>XW||wz<ZS-12||wz>ZN+12)continue;
            var q=new Vector2(wx,wz);
            float d=InsideLand(q)?0:Mathf.Max(0,CoastDistance(q)-1.9f);
            float old=px[y*size+x].r/255f*12;if(d>=old)continue;
            byte v=(byte)Mathf.RoundToInt(Mathf.Min(12,d)/12*255);px[y*size+x]=new Color32(v,v,v,255);changed++;
        }
        tex.SetPixels32(px);tex.Apply();File.WriteAllBytes(path,tex.EncodeToPNG());Object.DestroyImmediate(tex);AssetDatabase.ImportAsset(path);
        Log.Add($"shore map: {changed} pixels moved closer to the new cliff foot");
    }
}

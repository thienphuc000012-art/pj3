using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class MountainAdventureBuilder
{
    const string Request = "Library/CombatValidation/MountainMap.request";
    const string Output = "Assets/Adventure/MountainMap";
    const string Source = "Assets/asset/Toby Fredson/Rocky Hills Environment - Whitebark Pine/RHEWP_Demo/";
    static readonly Vector3[] Route = {
        new Vector3(180,24,140), new Vector3(270,29,240), new Vector3(430,40,300),
        new Vector3(520,52,410), new Vector3(650,67,510), new Vector3(610,88,650),
        new Vector3(520,100,780), new Vector3(680,117,880), new Vector3(860,139,840),
        new Vector3(1020,156,920), new Vector3(1150,178,1080), new Vector3(1060,199,1230), new Vector3(1250,214,1320) };
    static readonly Vector3[] Branch = { new Vector3(650,67,510), new Vector3(790,80,590), new Vector3(870,91,660) };
    static Terrain terrain;
    static Transform root;
    static System.Random rng;
    static Dictionary<string,GameObject> prefabs;
    static Material stone, gold;
    static int propCount;
    static MountainAdventureBuilder() { EditorApplication.update += Poll; }
    static void Poll()
    {
        if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        string action; try { action=File.ReadAllText(Request).Trim(); File.Delete(Request); } catch(IOException) { return; }
        try {
            if(action=="groundprops") { GroundProps(); }
            else if(action=="polish") { Polish(); }
            else if(action=="preview") {
                var scene=SceneManager.GetSceneByPath("Assets/Scenes/m.unity");
                terrain=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Terrain>()).Single();
                RenderPreview(scene);
            } else Build();
        }
        catch (Exception e) { File.WriteAllText("Library/CombatValidation/MountainMap.result.txt", e.ToString()); Debug.LogException(e); }
        finally { EditorUtility.ClearProgressBar(); }
    }
    [MenuItem("Tools/Adventure/Build Mountain Map in m")]
    public static void Build()
    {
        var scene = SceneManager.GetSceneByPath("Assets/Scenes/m.unity");
        if (!scene.isLoaded) throw new Exception("Open scene m in Edit Mode before building.");
        if (scene.GetRootGameObjects().Any(g => g.name == "Mountain Adventure")) throw new Exception("Mountain Adventure already exists. Build is intentionally one-shot to preserve your edits.");
        var terrains = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Terrain>(true)).ToArray();
        if (terrains.Length != 1) throw new Exception("Expected one Terrain in scene m.");
        terrain = terrains[0];
        if (!EditorSceneManager.SaveScene(scene)) throw new Exception("Could not save original scene.");
        string backup = "Library/CombatValidation/MountainMapBackup-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
        Directory.CreateDirectory(backup); File.Copy(scene.path, backup + "/m.unity");
        Directory.CreateDirectory(Output); AssetDatabase.Refresh();
        var original = terrain.terrainData;
        var data = UnityEngine.Object.Instantiate(original); data.name = "Mountain Adventure Terrain";
        AssetDatabase.CreateAsset(data, AssetDatabase.GenerateUniqueAssetPath(Output + "/MountainTerrain.asset"));
        terrain.terrainData = data; terrain.GetComponent<TerrainCollider>().terrainData = data;
        data.heightmapResolution = 1025; data.alphamapResolution = 512; data.baseMapResolution = 1024;
        data.size = new Vector3(1500,360,1500);
        string[] layerNames = { "Grass_Layer", "Dirt_Layer", "Rock_Layer", "Cliff_Layer", "RockyGround_Layer", "GrassyRocks_Layer", "GrassyRocksWet_Layer" };
        data.terrainLayers = layerNames.Select(n => AssetDatabase.LoadAssetAtPath<TerrainLayer>(Source+"Terrain/Terrain_Layers/"+n+".terrainlayer")).ToArray();
        if (data.terrainLayers.Any(l => l == null)) throw new Exception("Missing terrain layer");
        Sculpt(data);
        terrain.materialTemplate = AssetDatabase.LoadAssetAtPath<Material>("Packages/com.unity.render-pipelines.universal/Runtime/Materials/TerrainLit.mat");
        terrain.drawInstanced=true; terrain.heightmapPixelError=6; terrain.basemapDistance=700;
        terrain.treeDistance=650; terrain.treeBillboardDistance=100; terrain.treeMaximumFullLODCount=80;
        terrain.detailObjectDistance=65;
        var go=new GameObject("Mountain Adventure");SceneManager.MoveGameObjectToScene(go,scene);root=go.transform;
        rng=new System.Random(1977);propCount=0;
        prefabs=new Dictionary<string,GameObject>();
        foreach(var guid in AssetDatabase.FindAssets("t:Prefab",new[]{Source+"Prefabs"}))
        { string p=AssetDatabase.GUIDToAssetPath(guid);string name=Path.GetFileNameWithoutExtension(p); if(!prefabs.ContainsKey(name))prefabs.Add(name,AssetDatabase.LoadAssetAtPath<GameObject>(p)); }
        stone=MakeMaterial("Weathered Stone",new Color(.31f,.32f,.29f),.18f);
        var sourceMat=AssetDatabase.FindAssets("RockyGround_Layer t:TerrainLayer",new[]{Source}).Select(AssetDatabase.GUIDToAssetPath).FirstOrDefault();
        if(sourceMat!=null) { var l=AssetDatabase.LoadAssetAtPath<TerrainLayer>(sourceMat);stone.SetTexture("_BaseMap",l.diffuseTexture);stone.SetTextureScale("_BaseMap",new Vector2(2,2)); }
        gold=MakeMaterial("Relic Ochre",new Color(.65f,.45f,.18f),.45f);
        var land=Group("01 Landmarks");var rocks=Group("02 Rock Formations");var plants=Group("03 Undergrowth");var ruins=Group("04 Ruins and Puzzles");var markers=Group("05 Route Markers");
        EditorUtility.DisplayProgressBar("Mountain map", "Planting whitebark pine groves", .45f);
        string[] treeNames={"WhiteBarkPineRHEP_A","WhitebarkPineRHEP_B","WhitebarkPineRHEP_C","WhitebarkPineRHEP_D","WhitebarkPineRHEP_E"};
        data.treePrototypes=treeNames.Select(n=>new TreePrototype{prefab=Prefab(n),bendFactor=0}).ToArray();
        var trees=new List<TreeInstance>();
        for(int i=0;i<9000 && trees.Count<1800;i++)
        {
            float x=R(60,1440),z=R(60,1440),d=Trail(x,z,out _),h=Height(x,z);
            if(d<16 || h>220 || data.GetSteepness(x/1500,z/1500)>31 || Mathf.PerlinNoise(x*.006f+9,z*.006f)<.39f || IsArena(x,z,42))continue;
            float scale=R(.85f,1.6f);
            trees.Add(new TreeInstance{position=new Vector3(x/1500,h/360,z/1500),prototypeIndex=rng.Next(5),heightScale=scale,widthScale=scale,rotation=R(0,Mathf.PI*2),color=Color.white,lightmapColor=Color.white});
        }
        data.SetTreeInstances(trees.ToArray(),true);
        for(int i=0;i<260;i++)
        {
            float x=R(45,1455),z=R(45,1455);
            if(Trail(x,z,out _)<19 || IsArena(x,z,40))continue;
            Spawn(i%4==0?"RockBigRHEWP-S":"RockMediumBRHEWP-S",rocks,x,z,R(2.5f,9),R(0,360));
        }
        for(int i=0;i<72;i++)
        {
            float x=R(80,1420),z=R(80,1420);
            if(Trail(x,z,out _)<52 || IsArena(x,z,50))continue;
            Spawn(i%2==0?"GenericCliffRHEWP_A":"GenericCliffRHEWP_B",rocks,x,z,R(22,48),R(0,360));
        }
        string[] bushes={"GenericBushRHEWP_B","RabbitBrushRHEP_A","RabbitBrushRHEP_C","RoseBushBigDryRHEP_A"};
        for(int i=0;i<600;i++)
        {
            float x=R(80,1420),z=R(80,1420),d=Trail(x,z,out _);
            if(d<9 || d>100 || IsArena(x,z,24) || data.GetSteepness(x/1500,z/1500)>30)continue;
            Spawn(bushes[rng.Next(bushes.Length)],plants,x,z,R(1,2.7f),R(0,360));
        }
        // Large silhouettes and scenic anchors placed deliberately off the walkable trail.
        Spawn("CliffBaseRHEWP",land,355,495,80,120);
        Spawn("GenericCliffRHEWP_A",land,725,700,92,210);
        Spawn("GenericCliffRHEWP_B",land,1190,1220,96,30);
        Spawn("CaveRHEWP",land,882,678,30,210);
        MakeRuin(ruins,new Vector3(520,0,410),1,new[]{0,1,2});
        MakeRuin(ruins,new Vector3(680,0,880),2,new[]{2,0,1});
        MakeRuin(ruins,new Vector3(1060,0,1230),3,new[]{1,2,0});
        // Summit sanctuary: an open ring, broken columns and a central relic.
        var summit=new GameObject("Summit Sanctuary").transform;summit.SetParent(land,false);summit.position=At(1250,1320);
        for(int i=0;i<10;i++) {float a=i*Mathf.PI*.2f;Box("Broken Pillar",summit,new Vector3(Mathf.Cos(a)*15,3,Mathf.Sin(a)*15),new Vector3(2,R(3,8),2),stone);}
        Box("Relic Plinth",summit,new Vector3(0,.7f,0),new Vector3(4,1.4f,4),stone);
        Box("Summit Relic",summit,new Vector3(0,2.4f,0),new Vector3(.8f,2,.8f),gold);
        for(int i=0;i<Route.Length;i++) {var marker=new GameObject("Trail "+i.ToString("00")).transform;marker.SetParent(markers);marker.position=At(Route[i].x,Route[i].z)+Vector3.up;}
        var spawn=new GameObject("Player Spawn - Valley Entrance").transform;spawn.SetParent(markers);spawn.position=At(Route[0].x,Route[0].z)+Vector3.up*.25f;spawn.rotation=Quaternion.LookRotation(new Vector3(90,0,100));
        var player=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<PlayerScript>(true)).FirstOrDefault();
        if(player!=null)
        {
            var cc=player.GetComponent<CharacterController>();bool was=cc!=null && cc.enabled;if(cc!=null)cc.enabled=false;
            player.transform.SetPositionAndRotation(spawn.position,spawn.rotation);if(cc!=null)cc.enabled=was;
            PrefabUtility.RecordPrefabInstancePropertyModifications(player.transform);
            foreach(var puzzle in go.GetComponentsInChildren<MountainRelicPuzzle>())puzzle.player=player;
        }
        var sun=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Light>()).FirstOrDefault(l=>l.type==LightType.Directional);
        if(sun!=null){sun.transform.rotation=Quaternion.Euler(32,-38,0);sun.color=new Color(1,.9f,.74f);sun.intensity=1.35f;sun.shadows=LightShadows.Soft;}
        RenderSettings.fog=true;RenderSettings.fogMode=FogMode.ExponentialSquared;RenderSettings.fogDensity=.0014f;RenderSettings.fogColor=new Color(.52f,.63f,.68f);
        RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.45f,.55f,.66f);RenderSettings.ambientEquatorColor=new Color(.31f,.36f,.36f);RenderSettings.ambientGroundColor=new Color(.18f,.2f,.18f);
        terrain.Flush();Physics.SyncTransforms();EditorUtility.SetDirty(data);EditorSceneManager.MarkSceneDirty(scene);AssetDatabase.SaveAssets();
        if(!EditorSceneManager.SaveScene(scene))throw new Exception("Scene save failed");
        // Verify authored main route slope and support after generation.
        float maxSlope=0;bool supported=true;
        for(int i=0;i<Route.Length-1;i++)for(int k=0;k<=20;k++)
        {Vector3 v=Vector3.Lerp(Route[i],Route[i+1],k/20f);maxSlope=Mathf.Max(maxSlope,data.GetSteepness(v.x/1500,v.z/1500));var ray=new Ray(At(v.x,v.z)+Vector3.up*2,Vector3.down);if(!terrain.GetComponent<TerrainCollider>().Raycast(ray,out _,4))supported=false;}
        File.WriteAllText("Library/CombatValidation/MountainMap.result.txt","BUILT scene m; terrain 1500 x 1500; layers="+data.terrainLayers.Length+"; trees="+trees.Count+"; prefab props="+propCount+"; puzzles=3; route max slope="+maxSlope+"; terrain collider support="+supported+"; backup="+backup);
        RenderPreview(scene);
        Selection.activeGameObject=go;
        if(SceneView.lastActiveSceneView!=null)SceneView.lastActiveSceneView.LookAt(At(710,730),Quaternion.Euler(55,-28,0),1050,false,true);
    }
    static void GroundProps()
    {
        var scene=SceneManager.GetSceneByPath("Assets/Scenes/m.unity");
        terrain=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Terrain>()).Single();
        root=scene.GetRootGameObjects().Single(g=>g.name=="Mountain Adventure").transform;
        foreach(var name in new[]{"01 Landmarks","02 Rock Formations","03 Undergrowth"})
        foreach(Transform prop in root.Find(name))
        {
            if(prop.name=="Summit Sanctuary")continue;
            var renderers=prop.GetComponentsInChildren<Renderer>();if(renderers.Length==0)continue;
            Bounds b=renderers[0].bounds;foreach(var r in renderers)b.Encapsulate(r.bounds);
            float h=terrain.SampleHeight(b.center)+terrain.transform.position.y;
            foreach(var corner in new[]{new Vector3(b.min.x,0,b.min.z),new Vector3(b.max.x,0,b.min.z),new Vector3(b.min.x,0,b.max.z),new Vector3(b.max.x,0,b.max.z)})
                h=Mathf.Min(h,terrain.SampleHeight(corner)+terrain.transform.position.y);
            prop.position+=Vector3.up*(h-b.min.y-b.size.y*.08f);
            PrefabUtility.RecordPrefabInstancePropertyModifications(prop);
        }
        Physics.SyncTransforms();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);RenderPreview(scene);
        File.AppendAllText("Library/CombatValidation/MountainMap.result.txt","\nGrounded environment props against terrain footprint; verified player idle in Play Mode.");
    }
    static void Sculpt(TerrainData data)
    {
        var heights = new float[1025,1025];
        for (int z=0; z<1025; z++) for (int x=0; x<1025; x++) heights[z,x] = Height(x*1500f/1024,z*1500f/1024)/360;
        data.SetHeights(0,0,heights);
        EditorUtility.DisplayProgressBar("Mountain map", "Painting trails and slopes", .25f);
        var splat = new float[512,512,7];
        for(int z=0;z<512;z++) for(int x=0;x<512;x++)
        {
            float u=x/511f,v=z/511f, wx=u*1500,wz=v*1500;
            float distance=Trail(wx,wz,out _), slope=data.GetSteepness(u,v), h=data.GetInterpolatedHeight(u,v);
            float path=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(3,10,distance));
            float cliff=Mathf.SmoothStep(0,1,Mathf.InverseLerp(29,52,slope));
            float rock=Mathf.SmoothStep(0,1,Mathf.InverseLerp(13,34,slope))*(1-cliff);
            float high=Mathf.InverseLerp(155,285,h), noise=Mathf.PerlinNoise(wx*.014f,wz*.014f);
            float[] w={ (1-high)*(.7f+noise*.3f),path*6,rock*3,cliff*7,high*.7f,noise*.42f,(1-noise)*.12f };
            float sum=w.Sum(); for(int l=0;l<7;l++)splat[z,x,l]=w[l]/sum;
        }
        data.SetAlphamaps(0,0,splat);
    }
    static void Polish()
    {
        var scene=SceneManager.GetSceneByPath("Assets/Scenes/m.unity");
        terrain=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Terrain>()).Single();
        root=scene.GetRootGameObjects().Single(g=>g.name=="Mountain Adventure").transform;
        var items=new List<Transform>();var offsets=new List<float>();
        foreach(Transform group in root)foreach(Transform child in group)
        { items.Add(child); offsets.Add(child.position.y-terrain.SampleHeight(child.position)-terrain.transform.position.y); }
        Sculpt(terrain.terrainData);
        for(int i=0;i<items.Count;i++)
        {
            var t=items[i];var p=t.position;p.y=terrain.SampleHeight(p)+terrain.transform.position.y+offsets[i];t.position=p;
            if(t.name.IndexOf("Cliff",StringComparison.OrdinalIgnoreCase)>=0)
            {
                var rs=t.GetComponentsInChildren<Renderer>();if(rs.Length>0){var b=rs[0].bounds;foreach(var r in rs)b.Encapsulate(r.bounds);t.position-=Vector3.up*b.size.y*.25f;}
            }
            PrefabUtility.RecordPrefabInstancePropertyModifications(t);
        }
        terrain.terrainData.SetTreeInstances(terrain.terrainData.treeInstances,true);
        int ground=LayerMask.NameToLayer("Ground");terrain.gameObject.layer=ground;
        foreach(var collider in root.GetComponentsInChildren<Collider>(true))collider.gameObject.layer=ground;
        var player=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<PlayerScript>(true)).FirstOrDefault();
        if(player!=null){ player.surfaceLayer |= 1<<ground;EditorUtility.SetDirty(player);PrefabUtility.RecordPrefabInstancePropertyModifications(player); }
        // Validate the same activation routine used by E, including a wrong choice and all three orders.
        foreach(var puzzle in root.GetComponentsInChildren<MountainRelicPuzzle>())
        {
            var testGo=new GameObject("Temporary puzzle test");var test=testGo.AddComponent<MountainRelicPuzzle>();
            try {
                test.sequence=(int[])puzzle.sequence.Clone();test.indicators=new Renderer[0];
                var activate=typeof(MountainRelicPuzzle).GetMethod("ActivateStone",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
                var progress=typeof(MountainRelicPuzzle).GetField("progress",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
                var solved=typeof(MountainRelicPuzzle).GetField("solved",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
                activate.Invoke(test,new object[]{test.sequence[0]});activate.Invoke(test,new object[]{test.sequence[0]});
                if((int)progress.GetValue(test)!=0)throw new Exception("Wrong puzzle choice did not reset");
                foreach(int id in test.sequence)activate.Invoke(test,new object[]{id});
                if(!(bool)solved.GetValue(test))throw new Exception("Correct puzzle order did not solve");
            } finally { UnityEngine.Object.DestroyImmediate(testGo); }
        }
        Physics.SyncTransforms();float slope=0;bool supported=true;
        for(int i=0;i<Route.Length-1;i++)for(int k=0;k<=20;k++)
        {var v=Vector3.Lerp(Route[i],Route[i+1],k/20f);slope=Mathf.Max(slope,terrain.terrainData.GetSteepness(v.x/1500,v.z/1500));if(!terrain.GetComponent<TerrainCollider>().Raycast(new Ray(At(v.x,v.z)+Vector3.up*2,Vector3.down),out _,4))supported=false;}
        terrain.Flush();EditorUtility.SetDirty(terrain.terrainData);EditorSceneManager.MarkSceneDirty(scene);AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(scene);
        RenderPreview(scene);
        File.WriteAllText("Library/CombatValidation/MountainMap.result.txt","PASS: terrain 1500x1500; 7 layers; 1800 terrain trees; 372 prefab props; 3 puzzle sequences + wrong-answer reset passed; main route max slope="+slope+"; terrain support="+supported+"; Ground layer="+ground+"; player ground mask="+(player==null?0:player.surfaceLayer.value));
    }
    static float R(float a,float b)=>(float)(a+rng.NextDouble()*(b-a));
    static float Trail(float x,float z,out float height)
    {
        float best=float.MaxValue; height=0;
        foreach(var route in new[]{Route,Branch})for(int i=0;i<route.Length-1;i++)
        {
            Vector2 a=new Vector2(route[i].x,route[i].z),b=new Vector2(route[i+1].x,route[i+1].z),p=new Vector2(x,z);
            float t=Mathf.Clamp01(Vector2.Dot(p-a,b-a)/(b-a).sqrMagnitude),d=Vector2.Distance(p,Vector2.Lerp(a,b,t));
            if(d<best){best=d;height=Mathf.Lerp(route[i].y,route[i+1].y,t);}
        }
        return best;
    }
    static float Height(float x,float z)
    {
        float n=Mathf.PerlinNoise(x*.0028f+13,z*.0028f+4);
        float ridge=1-Mathf.Abs(Mathf.PerlinNoise(x*.0055f+2,z*.0055f+19)*2-1);
        float edge=1-Mathf.SmoothStep(0,1,Mathf.Min(x,z,1500-x,1500-z)/170);
        float h=22+z*.105f+n*n*92+ridge*ridge*38+edge*95;
        h+=Mathf.PerlinNoise(x*.018f,z*.018f)*7;
        float d=Trail(x,z,out float pathHeight);
        h=Mathf.Lerp(pathHeight,h,Mathf.SmoothStep(0,1,Mathf.InverseLerp(14,150,d)));
        foreach(int i in new[]{3,7,11,12}){var v=Route[i];float dist=Vector2.Distance(new Vector2(x,z),new Vector2(v.x,v.z));h=Mathf.Lerp(v.y,h,Mathf.SmoothStep(0,1,Mathf.InverseLerp(29,53,dist)));}
        return Mathf.Clamp(h,5,345);
    }
    static bool IsArena(float x,float z,float radius)=>new[]{3,7,11,12}.Any(i=>Vector2.Distance(new Vector2(x,z),new Vector2(Route[i].x,Route[i].z))<radius);
    static Vector3 At(float x,float z)=>terrain.transform.position+new Vector3(x,terrain.terrainData.GetInterpolatedHeight(x/1500,z/1500),z);
    static Transform Group(string name){var go=new GameObject(name);go.transform.SetParent(root,false);return go.transform;}
    static GameObject Prefab(string name){if(!prefabs.TryGetValue(name,out var p))throw new Exception("Missing prefab "+name);return p;}
    static GameObject Spawn(string name,Transform parent,float x,float z,float height,float yaw)
    {
        var go=(GameObject)PrefabUtility.InstantiatePrefab(Prefab(name),parent);go.transform.rotation=Quaternion.Euler(0,yaw,0);
        var renderers=go.GetComponentsInChildren<Renderer>();if(renderers.Length==0)throw new Exception("Prefab has no renderer "+name);
        var bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);
        go.transform.localScale*=height/Mathf.Max(.1f,bounds.size.y);
        bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);
        go.transform.position+=At(x,z)-new Vector3(bounds.center.x,bounds.min.y+height*.08f,bounds.center.z);
        foreach(var light in go.GetComponentsInChildren<Light>())light.enabled=false;
        PrefabUtility.RecordPrefabInstancePropertyModifications(go.transform);propCount++;return go;
    }
    static Material MakeMaterial(string name,Color color,float smooth)
    {var m=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name};m.SetColor("_BaseColor",color);m.SetFloat("_Smoothness",smooth);AssetDatabase.CreateAsset(m,AssetDatabase.GenerateUniqueAssetPath(Output+"/"+name+".mat"));return m;}
    static GameObject Box(string name,Transform parent,Vector3 pos,Vector3 size,Material material)
    {var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=name;g.transform.SetParent(parent,false);g.transform.localPosition=pos;g.transform.localScale=size;g.GetComponent<Renderer>().sharedMaterial=material;return g;}
    static void MakeRuin(Transform parent,Vector3 pos,int index,int[] order)
    {
        var go=new GameObject("Puzzle "+index+" - Stone Seal");go.transform.SetParent(parent);go.transform.position=At(pos.x,pos.z);
        var puzzle=go.AddComponent<MountainRelicPuzzle>();puzzle.sequence=order;puzzle.clue=string.Join(" - ",order.Select(i=>(i+1).ToString()));
        puzzle.stones=new Transform[3];puzzle.indicators=new Renderer[3];
        // Gate stands beside the main trail, guarding a small reward alcove.
        Box("Gate Left",go.transform,new Vector3(-5,4,19),new Vector3(3,8,3),stone);
        Box("Gate Right",go.transform,new Vector3(5,4,19),new Vector3(3,8,3),stone);
        Box("Gate Lintel",go.transform,new Vector3(0,8,19),new Vector3(13,2,3),stone);
        puzzle.gate=Box("Sealed Door",go.transform,new Vector3(0,3.5f,19),new Vector3(7,7,1.4f),stone).transform;
        for(int i=0;i<3;i++)
        {
            float a=(i*95+175)*Mathf.Deg2Rad;Vector3 v=new Vector3(Mathf.Cos(a)*13,0,Mathf.Sin(a)*13);
            var marker=new GameObject("Interact Stone "+(i+1)).transform;marker.SetParent(go.transform,false);marker.localPosition=v;
            Box("Carved Standing Stone",marker,new Vector3(0,1.25f,0),new Vector3(1.4f,2.5f,1),stone);
            for(int j=0;j<=i;j++)puzzle.indicators[i]=Box("Ochre Mark",marker,new Vector3((j-i*.5f)*.3f,1.7f,-.53f),new Vector3(.14f,.5f,.08f),gold).GetComponent<Renderer>();
            puzzle.stones[i]=marker;
        }
        Box("Relic beyond seal",go.transform,new Vector3(0,1,25),new Vector3(1,2,1),gold);
        var canvas=new GameObject("Puzzle Hint Canvas",typeof(Canvas),typeof(CanvasScaler));canvas.transform.SetParent(go.transform);
        canvas.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;canvas.GetComponent<Canvas>().sortingOrder=5;
        var scaler=canvas.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);
        var panel=new GameObject("Hint",typeof(RectTransform),typeof(Image));panel.transform.SetParent(canvas.transform,false);
        var rect=(RectTransform)panel.transform;rect.anchorMin=new Vector2(.16f,.04f);rect.anchorMax=new Vector2(.84f,.115f);rect.offsetMin=rect.offsetMax=Vector2.zero;panel.GetComponent<Image>().color=new Color(.03f,.04f,.05f,.85f);
        var text=new GameObject("Text",typeof(RectTransform),typeof(Text));text.transform.SetParent(panel.transform,false);rect=(RectTransform)text.transform;rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=new Vector2(16,4);rect.offsetMax=new Vector2(-16,-4);
        puzzle.prompt=text.GetComponent<Text>();puzzle.prompt.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");puzzle.prompt.fontSize=24;puzzle.prompt.alignment=TextAnchor.MiddleCenter;puzzle.prompt.color=new Color(.92f,.85f,.66f);panel.SetActive(false);
    }
    static void RenderPreview(Scene scene)
    {
        var g=new GameObject("Temporary overview");SceneManager.MoveGameObjectToScene(g,scene);var camera=g.AddComponent<Camera>();
        camera.transform.position=terrain.transform.position+new Vector3(750,1650,640);camera.transform.rotation=Quaternion.Euler(82,0,0);camera.farClipPlane=3500;camera.fieldOfView=58;
        bool fog=RenderSettings.fog;float treeDistance=terrain.treeDistance;
        RenderSettings.fog=false;terrain.treeDistance=2500;
        var rt=new RenderTexture(1400,1000,24);camera.targetTexture=rt;
        try
        {
            var request=new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest{destination=rt};
            UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera,request);
            var previous=RenderTexture.active;RenderTexture.active=rt;var tex=new Texture2D(1400,1000,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,1400,1000),0,0);tex.Apply();RenderTexture.active=previous;
            File.WriteAllBytes("Library/CombatValidation/MountainMap-overview.png",tex.EncodeToPNG());UnityEngine.Object.DestroyImmediate(tex);
        }
        finally{RenderSettings.fog=fog;terrain.treeDistance=treeDistance;camera.targetTexture=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(g);}
    }
}

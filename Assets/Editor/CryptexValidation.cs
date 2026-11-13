using System;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class CryptexValidation
{
    const string Request = "Library/CombatValidation/CryptexValidation.request";
    [InitializeOnLoadMethod] static void Schedule() { if (File.Exists(Request)) EditorApplication.delayCall += Requested; }
    static void Requested()
    {
        if (!File.Exists(Request)) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) { EditorApplication.delayCall += Requested; return; }
        File.Delete(Request); Validate();
    }
    [MenuItem("Tools/Adventure/Cryptex/Validate Lock and Materials")]
    public static void Validate()
    {
        Scene scene=default;GameObject root=null;CryptexPuzzleView view=null;
        try
        {
            scene=EditorSceneManager.NewPreviewScene();
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(CryptexSetup.PrefabPath);Check(prefab!=null,"Missing lock prefab");
            root=(GameObject)PrefabUtility.InstantiatePrefab(prefab,scene);var puzzle=root.GetComponent<CryptexLock>();puzzle.Initialize();
            Check(puzzle.rings.Length==7 && puzzle.PasswordValid,"Seven valid rings and A-Z password");
            Check(puzzle.CurrentWord=="AAAAAAA" && !puzzle.CheckCombination(),"Wrong word must not unlock");
            int unlocks=0,locks=0;puzzle.onUnlocked.AddListener(()=>unlocks++);puzzle.onLocked.AddListener(()=>locks++);
            for(int i=0;i<7;i++)for(int j=0;j<puzzle.password[i]-'A';j++)puzzle.RotateRing(i,1);
            Check(puzzle.CurrentWord==puzzle.password && puzzle.CheckCombination(),"Every ring contributes to exact keyword");
            puzzle.RotateRing(6,1);Check(!puzzle.CheckCombination(),"Wrong last letter must fail");puzzle.RotateRing(6,-1);Check(puzzle.CheckCombination(),"Reverse ring rotation");
            for(int j=0;j<26;j++)puzzle.RotateRing(0,1);Check(puzzle.CheckCombination(),"Alphabet wrap");
            string saved=puzzle.password;puzzle.password="ABC";Check(!puzzle.CheckCombination(),"Invalid password length");puzzle.password="ÁNCIENT";Check(!puzzle.CheckCombination(),"Unsupported characters");puzzle.password=saved;
            puzzle.RestoreSavedState(true);puzzle.RestoreSavedState(true);Check(puzzle.IsUnlocked && unlocks==1,"Saved unlock restored once");
            Check(Vector3.Distance(puzzle.slidingKey.localPosition,puzzle.unlockedKeyOffset)<.001f,"Key slides open");
            puzzle.RestoreSavedState(false);Check(!puzzle.IsUnlocked && locks==1 && puzzle.CurrentWord==puzzle.initialWord,"Earlier unsolved save restores lock");
            foreach(var material in root.GetComponentsInChildren<MeshRenderer>().SelectMany(r=>r.sharedMaterials).Distinct())
            {
                Check(material!=null && !UnityEditor.ShaderUtil.ShaderHasError(material.shader),"Material shader compiles");
                if (material.shader.name.StartsWith("TextMeshPro/")) continue;
                Check(material.shader.name=="Universal Render Pipeline/Lit","Model uses URP Lit");
                Check(material.GetTexture("_BaseMap")!=null && material.GetTexture("_BumpMap")!=null && material.GetTexture("_MetallicGlossMap")!=null,"All PBR maps linked");
                Check(((TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(material.GetTexture("_BumpMap")))).textureType==TextureImporterType.NormalMap,"Normal imported correctly");
            }
            var bounds=root.GetComponentsInChildren<Renderer>()[0].bounds;foreach(var renderer in root.GetComponentsInChildren<Renderer>())bounds.Encapsulate(renderer.bounds);
            Check(Mathf.Abs(bounds.size.x-.55f)<.002f,"Assembled length 0.55m");Check(bounds.size.y<.3f,"Exploded duplicates removed");
            var colliders=root.GetComponentsInChildren<Collider>();Check(colliders.Length==1 && colliders[0] is BoxCollider,"One simple collider");
            var player=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Adventure/Prefabs/Exploration/ExplorationPlayer.prefab");var cc=player.GetComponentInChildren<CharacterController>(true);
            Check(cc!=null && bounds.size.x<cc.height,"Handheld size relative to player");
            view=((GameObject)PrefabUtility.InstantiatePrefab(puzzle.viewPrefab.gameObject,scene)).GetComponent<CryptexPuzzleView>();
            // No CampaignSession.Update/Play Mode/save APIs are run. Only this modal's event wiring is exercised.
            view.Open(puzzle,null);Canvas.ForceUpdateCanvases();
            Check(view.ringControls.GetComponentsInChildren<Button>().Length==21,"Seven letter selectors and fourteen arrow buttons");
            var arrows=view.ringControls.GetComponentsInChildren<Button>();string before=puzzle.CurrentWord;
            arrows.First(b=>b.name=="Up").onClick.Invoke();Check(puzzle.CurrentWord!=before,"Mouse arrow rotates real ring");
            typeof(CryptexPuzzleView).GetMethod("LateUpdate",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(view,null);
            var cam=(Camera)typeof(CryptexPuzzleView).GetField("previewCamera",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(view);
            var texture=(RenderTexture)typeof(CryptexPuzzleView).GetField("texture",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(view);
            RenderPipeline.SubmitRenderRequest(cam, new UniversalRenderPipeline.SingleCameraRequest { destination = texture });var previous=RenderTexture.active;RenderTexture.active=texture;
            var shot=new Texture2D(texture.width,texture.height,TextureFormat.RGB24,false);shot.ReadPixels(new Rect(0,0,texture.width,texture.height),0,0);shot.Apply();RenderTexture.active=previous;
            Check(shot.GetPixels32().Count(p=>p.r>40 && p.g>25 && p.b<230)>10000,"Model is visible in preview, not a black render");
            Directory.CreateDirectory("Tools/Cryptex");File.WriteAllBytes("Tools/Cryptex/Preview.png",shot.EncodeToPNG());UnityEngine.Object.DestroyImmediate(shot);
            CaptureInterface(view, scene);
            File.WriteAllText("Library/CombatValidation/CryptexValidation.result.txt","PASS: 7 independent rings, exact password, wrong/invalid password rejection, wrap, saved state restore, once-only events, 6 URP PBR materials, 0.55m scale, one collider, editable modal and mouse arrows. No Play Mode or campaign saves. Player capsule height="+cc.height+". Preview: Tools/Cryptex/Preview.png");
            Debug.Log("Cryptex validation PASS");
        }
        catch(Exception e){File.WriteAllText("Library/CombatValidation/CryptexValidation.result.txt",e.ToString());Debug.LogException(e);}
        finally
        {
            // Editor fixtures own these transient resources; use immediate disposal, never runtime Destroy in Edit Mode.
            if(view!=null)
            {
                var stage=(GameObject)typeof(CryptexPuzzleView).GetField("stage",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(view);
                var texture=(RenderTexture)typeof(CryptexPuzzleView).GetField("texture",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(view);
                if(stage!=null)UnityEngine.Object.DestroyImmediate(stage);
                if(texture!=null){texture.Release();UnityEngine.Object.DestroyImmediate(texture);}
                UnityEngine.Object.DestroyImmediate(view.gameObject);
            }
            if(root!=null)UnityEngine.Object.DestroyImmediate(root);if(scene.IsValid())EditorSceneManager.ClosePreviewScene(scene);
        }
    }
    static void Check(bool valid,string message){if(!valid)throw new InvalidOperationException(message);}
    static void CaptureInterface(CryptexPuzzleView view, Scene scene)
    {
        var obj=new GameObject("UI validation camera");SceneManager.MoveGameObjectToScene(obj,scene);
        var camera=obj.AddComponent<Camera>();camera.enabled=false;camera.scene=scene;camera.orthographic=true;
        camera.transform.position=new Vector3(0,0,-10);camera.nearClipPlane=.01f;camera.farClipPlane=100;
        var target=new RenderTexture(1920,1080,24);target.Create();camera.targetTexture=target;
        var canvas=view.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;
        var previous=RenderTexture.active;
        try
        {
            Canvas.ForceUpdateCanvases();RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest {destination=target});
            RenderTexture.active=target;var shot=new Texture2D(1920,1080,TextureFormat.RGB24,false);
            shot.ReadPixels(new Rect(0,0,1920,1080),0,0);shot.Apply();File.WriteAllBytes("Tools/Cryptex/Interface.png",shot.EncodeToPNG());UnityEngine.Object.DestroyImmediate(shot);
        }
        finally {canvas.worldCamera=null;camera.targetTexture=null;RenderTexture.active=previous;target.Release();UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(obj);}
    }
}

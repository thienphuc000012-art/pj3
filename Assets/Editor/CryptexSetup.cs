using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class CryptexSetup
{
    public const string Root = "Assets/asset/model item/cryptex-engine";
    public const string PrefabPath = Root + "/Prefabs/Cryptex Lock.prefab";
    public const string ModelPath = Root + "/source/criptexDesarmado/cryptex.obj";
    public const string ViewPath = "Assets/Resources/Adventure/UI/CryptexCanvas.prefab";
    const string Request = "Library/CombatValidation/CryptexSetup.request";
    static string backup;
    [InitializeOnLoadMethod] static void Schedule() { if(File.Exists(Request)) EditorApplication.delayCall += Requested; }
    static void Requested()
    {
        if(!File.Exists(Request))return;
        if(EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) { EditorApplication.delayCall += Requested;return; }
        File.Delete(Request);
        try { Build(); CryptexValidation.Validate(); }
        catch(Exception e) { File.WriteAllText("Library/CombatValidation/CryptexSetup.result.txt",e.ToString());Debug.LogException(e); }
    }
    [MenuItem("Tools/Adventure/Cryptex/Build URP Lock Prefab")]
    public static void Build()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play Mode first.");
        backup="Tools/Cryptex/BeforeSetup-"+DateTime.Now.ToString("yyyyMMdd-HHmmss-fff");
        Directory.CreateDirectory(backup); Directory.CreateDirectory(Root+"/Materials"); Directory.CreateDirectory(Root+"/Prefabs");
        AssetDatabase.Refresh();
        var materials=BuildMaterials();
        var importer=(ModelImporter)AssetImporter.GetAtPath(ModelPath);
        Backup(ModelPath+".meta");
        string[] oldNames={"blinn1SG","blinn2SG","blinn3SG","blinn4SG","blinn5SG","initialShadingGroup"};
        for(int i=0;i<oldNames.Length;i++) importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material),oldNames[i]),materials[i]);
        importer.SaveAndReimport();
        var view=BuildView();
        var source=AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        var scene=EditorSceneManager.NewPreviewScene(); GameObject obj=null;
        try
        {
            obj=new GameObject("Cryptex Lock");UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(obj,scene);
            var interaction=obj.AddComponent<WorldInteraction>();interaction.kind=WorldInteractionKind.Cryptex;
            interaction.displayName="Ổ khóa mật mã";interaction.range=2.5f;interaction.experience=0;
            interaction.persistentId=Guid.NewGuid().ToString("N"); interaction.savedWorldId="";
            var puzzle=obj.AddComponent<CryptexLock>();puzzle.viewPrefab=view;
            var model=new GameObject("Model").transform; model.SetParent(obj.transform,false);puzzle.modelRoot=model;
            var filters=source.GetComponentsInChildren<MeshFilter>(true).Where(f=>f.sharedMesh.bounds.center.y<3).ToArray();
            var bounds=filters[0].sharedMesh.bounds;foreach(var f in filters)bounds.Encapsulate(f.sharedMesh.bounds);
            float scale=.55f/bounds.size.z;
            // Keep all vertices and UVs. The asset includes an assembled first row and exploded duplicates above it.
            var recenter=new GameObject("Assembly").transform;recenter.SetParent(model,false);recenter.localPosition=-bounds.center;
            var ringFilters=filters.Where(f=>f.GetComponent<Renderer>().sharedMaterial==materials[2]).OrderBy(f=>f.sharedMesh.bounds.center.z).ToArray();
            if(ringFilters.Length!=7)throw new InvalidOperationException("Expected seven original letter rings.");
            puzzle.rings=new CryptexLetterRing[ringFilters.Length];
            foreach(var f in filters)
            {
                var item=new GameObject(f.name); var mf=item.AddComponent<MeshFilter>(); mf.sharedMesh=f.sharedMesh;
                item.AddComponent<MeshRenderer>().sharedMaterials=f.GetComponent<Renderer>().sharedMaterials;
                int ring=Array.IndexOf(ringFilters,f);
                if(ring>=0)
                {
                    var pivot=new GameObject("Ring "+(ring+1)).transform;pivot.SetParent(recenter,false);
                    pivot.localPosition=new Vector3(bounds.center.x,bounds.center.y,f.sharedMesh.bounds.center.z);
                    item.transform.SetParent(pivot,false);item.transform.localPosition=-pivot.localPosition;
                    puzzle.rings[ring]=new CryptexLetterRing {pivot=pivot,letterAEuler=Vector3.zero};
                    pivot.localRotation=Quaternion.Euler(puzzle.rings[ring].letterAEuler);
                    AddAlphabet(pivot, f.sharedMesh, ring);
                }
                else
                {
                    item.transform.SetParent(recenter,false);
                    if(f.GetComponent<Renderer>().sharedMaterial==materials[0])
                    {
                        var key=new GameObject("Sliding Key").transform;key.SetParent(recenter,false);
                        item.transform.SetParent(key,false);puzzle.slidingKey=key;
                    }
                }
            }
            model.localRotation=Quaternion.Euler(0,90,0);model.localScale=Vector3.one*scale;
            model.localPosition=Vector3.up*(bounds.size.y*scale*.5f);
            puzzle.unlockedKeyOffset=Vector3.forward*(.13f/scale);
            var box=obj.AddComponent<BoxCollider>();box.center=model.localPosition;box.size=new Vector3(.55f,bounds.size.y*scale,bounds.size.x*scale);
            Backup(PrefabPath);PrefabUtility.SaveAsPrefabAsset(obj,PrefabPath);
            File.WriteAllText("Library/CombatValidation/CryptexSetup.result.txt","PASS: 6 URP materials, 7 assembled rings, length 0.55 m, separate sliding key, interaction prefab and editable Canvas. Backup: "+backup);
        }
        finally {if(obj!=null)UnityEngine.Object.DestroyImmediate(obj);EditorSceneManager.ClosePreviewScene(scene);}
        AssetDatabase.SaveAssets(); Debug.Log("Cryptex ready: "+PrefabPath);
    }
    static string CleanLetterBand(string original, bool normal = false)
    {
        // Keep the supplied worn brass; replace only the old Spanish alphabet strip.
        // Clear glyph pixels with neighbouring brass texels. The original PNG is never overwritten.
        var texture=ReadPixels(original);var pixels=texture.GetPixels32();var source=(Color32[])pixels.Clone();
        CleanBrassBand(pixels,source,texture.width,texture.height);
        texture.SetPixels32(pixels);texture.Apply();
        string path=Root+"/Materials/C_Letras_AlphabetAZ_"+(normal?"Normal":"BaseColor")+".png";Backup(path);File.WriteAllBytes(path,texture.EncodeToPNG());UnityEngine.Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(path);SetTextureImport(path,!normal,normal);return path;
    }
    static void CleanBrassBand(Color32[] pixels,Color32[] source,int width,int height)
    {
        for(int y=0;y<height;y++)
        {
            float fromTop=1f-(float)y/height;
            if(fromTop<.449f || fromTop>.499f)continue;
            int sampleY=Mathf.Clamp(Mathf.RoundToInt(height*(1f-.445f))+(y%5),0,height-1);
            for(int x=0;x<width;x++)pixels[y*width+x]=source[sampleY*width+x];
        }
    }
    static void AddAlphabet(Transform pivot,Mesh ringMesh,int ring)
    {
        var font=TMP_Settings.defaultFontAsset;
        string materialPath=Root+"/Materials/Alphabet_Ink.mat";var ink=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if(ink==null){ink=new Material(font.material);AssetDatabase.CreateAsset(ink,materialPath);}
        ink.SetColor("_FaceColor",new Color(.02f,.014f,.008f,1));EditorUtility.SetDirty(ink);
        var temp=new GameObject("Letter bake");temp.transform.SetParent(pivot,false);
        var text=temp.AddComponent<TextMeshPro>();text.font=font;text.fontSize=36;text.fontStyle=FontStyles.Bold;text.alignment=TextAlignmentOptions.Center;
        text.rectTransform.sizeDelta=new Vector2(10,10);text.textWrappingMode=TextWrappingModes.NoWrap;
        var combines=new List<CombineInstance>();var glyphs=new List<Mesh>();
        try
        {
            for(int letter=0;letter<26;letter++)
            {
                text.text=((char)('A'+letter)).ToString();text.ForceMeshUpdate(true,true);
                var glyph=UnityEngine.Object.Instantiate(text.mesh);glyphs.Add(glyph);
                float scale=.42f/Mathf.Max(.01f,glyph.bounds.size.y);
                var rotation=Quaternion.AngleAxis(-letter*360f/26f,Vector3.forward);
                var position=rotation*Vector3.right*(ringMesh.bounds.extents.x+.012f);
                var facing=rotation*Quaternion.Euler(0,-90,0);
                combines.Add(new CombineInstance{mesh=glyph,transform=Matrix4x4.TRS(position,facing,Vector3.one*scale)*Matrix4x4.Translate(-glyph.bounds.center)});
            }
            var combined=new Mesh {name="Cryptex Alphabet Ring "+(ring+1)};combined.CombineMeshes(combines.ToArray(),true,true);
            string path=Root+"/Prefabs/Alphabet_Ring_"+(ring+1)+".asset";var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(existing==null)AssetDatabase.CreateAsset(combined,path);else{Backup(path);EditorUtility.CopySerialized(combined,existing);UnityEngine.Object.DestroyImmediate(combined);combined=existing;EditorUtility.SetDirty(existing);}
            var letters=new GameObject("Alphabet A-Z");letters.transform.SetParent(pivot,false);
            letters.AddComponent<MeshFilter>().sharedMesh=combined;letters.AddComponent<MeshRenderer>().sharedMaterial=ink;
        }
        finally {foreach(var glyph in glyphs)UnityEngine.Object.DestroyImmediate(glyph);UnityEngine.Object.DestroyImmediate(temp);}
    }
    static Material[] BuildMaterials()
    {
        var prefixes=new[]{"A_CilindroLlave","B_TapaEnranajes","C_Letras","D_Engranajes","E_CilindroBase","F_Tornillo"};
        var shader=Shader.Find("Universal Render Pipeline/Lit"); if(shader==null)throw new InvalidOperationException("Missing URP Lit.");
        var result=new Material[6];
        for(int i=0;i<6;i++)
        {
            string prefix=prefixes[i],path=Root+"/Materials/"+prefix+"_URP.mat";
            Backup(path);var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(material==null){material=new Material(shader);AssetDatabase.CreateAsset(material,path);}else material.shader=shader;
            string basePath=TexturePath(prefix,"Base_Color"), normalPath=TexturePath(prefix,"Normal_OpenGL");
            SetTextureImport(basePath,true,false);SetTextureImport(normalPath,false,true);
            material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(i == 2 ? CleanLetterBand(basePath) : basePath));material.SetColor("_BaseColor",Color.white);
            material.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(i == 2 ? CleanLetterBand(normalPath,true) : normalPath));material.SetFloat("_BumpScale",.75f);material.EnableKeyword("_NORMALMAP");
            var metallic=ReadPixels(TexturePath(prefix,"Metallic"));var rough=ReadPixels(TexturePath(prefix,"Roughness"));
            var pixels=metallic.GetPixels32();var roughPixels=rough.GetPixels32();
            if(pixels.Length!=roughPixels.Length)throw new InvalidOperationException("PBR texture sizes differ: "+prefix);
            for(int p=0;p<pixels.Length;p++)pixels[p]=new Color32(pixels[p].r,0,0,(byte)(255-roughPixels[p].r));
            if(i==2)CleanBrassBand(pixels,(Color32[])pixels.Clone(),metallic.width,metallic.height);
            metallic.SetPixels32(pixels);metallic.Apply();
            string packed=Root+"/Materials/"+prefix+"_MetallicSmoothness.png";Backup(packed);File.WriteAllBytes(packed,metallic.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(metallic);UnityEngine.Object.DestroyImmediate(rough);
            AssetDatabase.ImportAsset(packed);SetTextureImport(packed,false,false);
            material.SetTexture("_MetallicGlossMap",AssetDatabase.LoadAssetAtPath<Texture2D>(packed));material.SetFloat("_Metallic",1);material.SetFloat("_Smoothness",.75f);material.EnableKeyword("_METALLICSPECGLOSSMAP");
            EditorUtility.SetDirty(material);result[i]=material;
        }
        return result;
    }
    static string TexturePath(string prefix,string suffix)
    {
        string path=Root+"/textures/"+prefix+"_"+suffix+".png";
        if(File.Exists(path))return path;
        path=Root+"/source/criptexDesarmado/"+prefix+"_"+suffix+".png";
        if(!File.Exists(path))throw new FileNotFoundException("Missing PBR texture",path);
        return path;
    }
    static Texture2D ReadPixels(string path)
    { var texture=new Texture2D(2,2,TextureFormat.RGBA32,false,true);if(!texture.LoadImage(File.ReadAllBytes(path)))throw new InvalidOperationException(path);return texture; }
    static void SetTextureImport(string path,bool srgb,bool normal)
    {
        var importer=(TextureImporter)AssetImporter.GetAtPath(path);
        if(importer.sRGBTexture==srgb && importer.textureType==(normal?TextureImporterType.NormalMap:TextureImporterType.Default))return;
        Backup(path+".meta");importer.sRGBTexture=srgb;importer.textureType=normal?TextureImporterType.NormalMap:TextureImporterType.Default;importer.SaveAndReimport();
    }
    static void Backup(string path)
    {if(!File.Exists(path))return;string dest=backup+"/"+path;Directory.CreateDirectory(Path.GetDirectoryName(dest));File.Copy(path,dest,true);}
    static CryptexPuzzleView BuildView()
    {
        var scene=EditorSceneManager.NewPreviewScene();GameObject root=null;
        try
        {
            root=new GameObject("CryptexCanvas",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root,scene);
            var canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=220;
            var scaler=root.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
            var view=root.AddComponent<CryptexPuzzleView>();
            var backdrop=Rect(root.transform,"Backdrop",Vector2.zero,Vector2.one);backdrop.gameObject.AddComponent<Image>().color=new Color(.005f,.005f,.008f,.96f);
            view.title=Label(backdrop,"Title","Ổ KHÓA MẬT MÃ",new Vector2(.15f,.83f),new Vector2(.85f,.92f),40);
            var image=Rect(backdrop,"Model Preview",new Vector2(.15f,.36f),new Vector2(.85f,.81f));view.preview=image.gameObject.AddComponent<RawImage>();view.preview.raycastTarget=false;
            var controls=Rect(backdrop,"Rings",new Vector2(.24f,.21f),new Vector2(.76f,.36f));view.ringControls=controls;
            var layout=controls.gameObject.AddComponent<HorizontalLayoutGroup>();layout.spacing=10;layout.childControlHeight=true;layout.childControlWidth=true;layout.childForceExpandWidth=true;
            var template=Rect(controls,"Ring Template",Vector2.zero,Vector2.one);var bg=template.gameObject.AddComponent<Image>();bg.color=view.normalColor;
            view.ringTemplate=template.gameObject.AddComponent<Button>();view.ringTemplate.transition=Selectable.Transition.None;
            template.gameObject.AddComponent<LayoutElement>().preferredWidth=100;
            Label(template,"Letter","A",new Vector2(0,.27f),new Vector2(1,.73f),44);
            Button(template,"Up","▲",new Vector2(0,.73f),Vector2.one,22);
            Button(template,"Down","▼",Vector2.zero,new Vector2(1,.27f),22);
            template.gameObject.SetActive(false);
            view.message=Label(backdrop,"Message","",new Vector2(.2f,.13f),new Vector2(.8f,.20f),26);
            view.unlockButton=Button(backdrop,"Unlock","Mở khóa",new Vector2(.42f,.05f),new Vector2(.58f,.12f),28);
            view.closeButton=Button(backdrop,"Back","Trở lại",new Vector2(.76f,.05f),new Vector2(.9f,.12f),28);
            view.unlockLabel=view.unlockButton.GetComponentInChildren<TMP_Text>();view.closeLabel=view.closeButton.GetComponentInChildren<TMP_Text>();
            Backup(ViewPath);PrefabUtility.SaveAsPrefabAsset(root,ViewPath);
            return AssetDatabase.LoadAssetAtPath<GameObject>(ViewPath).GetComponent<CryptexPuzzleView>();
        }
        finally{if(root!=null)UnityEngine.Object.DestroyImmediate(root);EditorSceneManager.ClosePreviewScene(scene);}
    }
    static RectTransform Rect(Transform parent,string name,Vector2 min,Vector2 max)
    {var r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();r.SetParent(parent,false);r.anchorMin=min;r.anchorMax=max;r.offsetMin=r.offsetMax=Vector2.zero;return r;}
    static TMP_Text Label(Transform parent,string name,string value,Vector2 min,Vector2 max,float size)
    {var r=Rect(parent,name,min,max);var t=r.gameObject.AddComponent<TextMeshProUGUI>();t.font=TMP_Settings.defaultFontAsset;t.text=value;t.fontSize=size;t.alignment=TextAlignmentOptions.Center;t.color=Color.white;t.raycastTarget=false;t.textWrappingMode=TextWrappingModes.NoWrap;return t;}
    static Button Button(Transform parent,string name,string value,Vector2 min,Vector2 max,float size)
    {
        var r=Rect(parent,name,min,max);var image=r.gameObject.AddComponent<Image>();image.color=new Color(.12f,.12f,.13f);
        var b=r.gameObject.AddComponent<Button>();b.targetGraphic=image;
        var colors=b.colors;colors.highlightedColor=new Color(.8f,.75f,.65f);colors.selectedColor=colors.normalColor; b.colors=colors;
        Label(r,"Label",value,Vector2.zero,Vector2.one,size);return b;
    }
    [MenuItem("GameObject/Adventure/Cryptex Lock",false,16)]
    static void CreatePoint()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)return;
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);if(prefab==null){Build();prefab=AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);}
        var obj=(GameObject)PrefabUtility.InstantiatePrefab(prefab);Undo.RegisterCreatedObjectUndo(obj,"Create Cryptex Lock");
        var selected=Selection.activeTransform;
        if(selected!=null&&!EditorUtility.IsPersistent(selected))obj.transform.position=selected.position;
        else if(SceneView.lastActiveSceneView!=null)obj.transform.position=SceneView.lastActiveSceneView.pivot;
        var interaction=obj.GetComponent<WorldInteraction>();interaction.persistentId=Guid.NewGuid().ToString("N");interaction.savedWorldId="world:"+interaction.persistentId;
        Selection.activeGameObject=obj;EditorSceneManager.MarkSceneDirty(obj.scene);
    }
}

[CustomEditor(typeof(CryptexLock))]
public sealed class CryptexLockEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();var puzzle=(CryptexLock)target;
        if(!puzzle.PasswordValid)EditorGUILayout.HelpBox("Password phải có đúng "+puzzle.rings.Length+" chữ A-Z, không dấu. Ví dụ ANCIENT cho 7 vòng.",MessageType.Error);
        EditorGUILayout.HelpBox("E để tương tác. Chọn vòng, bấm ▲/▼ hoặc cuộn chuột. A/D đổi vòng, W/S xoay, Enter mở khóa. Esc/chuột phải đóng. On Unlocked dùng để nối với cửa; Rewards/EXP đặt trong World Interaction.",MessageType.Info);
        if(Application.isPlaying)EditorGUILayout.LabelField("Current Word",puzzle.CurrentWord);
    }
}

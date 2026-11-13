using System;
using System.IO;
using System.Linq;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>Explicit setup and isolated tests. Does not start a campaign or read/write saves.</summary>
public static class CameraOcclusionSetup
{
    const string Request = "Library/CombatValidation/CameraOcclusion.request";
    const string Result = "Library/CombatValidation/CameraOcclusion.result.txt";
    const string Template = "Assets/Resources/CameraOcclusion/FadeTemplate.mat";
    static string report;

    [InitializeOnLoadMethod]
    static void Schedule() { if (File.Exists(Request)) EditorApplication.delayCall += RunRequested; }
    static void RunRequested()
    {
        if (!File.Exists(Request)) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
        { EditorApplication.delayCall += RunRequested; return; }
        File.Delete(Request); Setup();
    }

    [MenuItem("Tools/Adventure/Camera/Setup and Validate Obstruction Fade")]
    public static void Setup()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Result));
        report = "";
        try
        {
            Check(!EditorApplication.isPlayingOrWillChangePlaymode, "Exit Play Mode before setup.");
            Material template = EnsureTemplate();
            Validate(template);
            string backup = "Tools/CameraOcclusion/BeforeSetup-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff");
            Directory.CreateDirectory(backup);
            var config = Resources.Load<CampaignConfig>("Adventure/CampaignConfig");
            var scenes = new[] { config.mapScene, config.battleScene }.Concat(config.additionalMapScenes ?? new System.Collections.Generic.List<string>()).Distinct();
            foreach (string name in scenes)
            {
                string path = EditorBuildSettings.scenes.Select(x => x.path).FirstOrDefault(x => Path.GetFileNameWithoutExtension(x) == name);
                Check(!string.IsNullOrEmpty(path), "Scene not in Build Settings: " + name);
                var scene = SceneManager.GetSceneByPath(path);
                bool open = scene.IsValid() && scene.isLoaded;
                var active = SceneManager.GetActiveScene();
                if (open && scene.isDirty) EditorSceneManager.SaveScene(scene, backup + "/" + name + ".unity", true);
                else File.Copy(path, backup + "/" + name + ".unity");
                if (!open) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                try
                {
                    var cameras = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Camera>(true))
                        .Where(camera => camera.CompareTag("MainCamera") && camera.GetComponent<CinemachineBrain>() != null).ToArray();
                    Check(cameras.Length > 0, name + " requires a Main Camera with Cinemachine Brain.");
                    foreach (var camera in cameras)
                    {
                        var fade = CameraOcclusionFade.Ensure(camera);
                        fade.fallbackTemplate = template;
                        EditorUtility.SetDirty(fade);
                        if (PrefabUtility.IsPartOfPrefabInstance(fade)) PrefabUtility.RecordPrefabInstancePropertyModifications(fade);
                    }
                    EditorSceneManager.MarkSceneDirty(scene);
                    Check(EditorSceneManager.SaveScene(scene), "Could not save " + name);
                    report += "Configured " + name + ": " + string.Join(", ", cameras.Select(x => x.name)) + "\n";
                }
                finally
                {
                    if (active.IsValid() && active.isLoaded) SceneManager.SetActiveScene(active);
                    if (!open) EditorSceneManager.CloseScene(scene, true);
                }
            }
            File.WriteAllText(Result, "PASS\n" + report + "Backup: " + backup);
            Debug.Log(File.ReadAllText(Result));
        }
        catch (Exception error) { File.WriteAllText(Result, "FAIL\n" + report + error); Debug.LogException(error); }
    }

    static Material EnsureTemplate()
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(Template);
        if (material != null) return material;
        Directory.CreateDirectory(Path.GetDirectoryName(Template));
        AssetDatabase.Refresh();
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        Check(shader != null, "URP Lit shader missing.");
        material = new Material(shader) { name = "Camera Occlusion Transparent Template" };
        material.SetFloat("_Surface", 1); material.SetFloat("_Blend", 0);
        material.SetFloat("_BlendModePreserveSpecular", 0);
        material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
        material.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
        material.SetFloat("_ZWrite", 0);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.SetOverrideTag("RenderType", "Transparent");
        material.renderQueue = 3000;
        foreach (string pass in new[] { "ShadowCaster", "DepthOnly", "DepthNormals", "DepthNormalsOnly" }) material.SetShaderPassEnabled(pass, false);
        AssetDatabase.CreateAsset(material, Template);
        AssetDatabase.SaveAssets();
        return material;
    }

    static void Validate(Material template)
    {
        var originalActive = SceneManager.GetActiveScene();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        scene.name = "OcclusionValidation-" + Guid.NewGuid().ToString("N");
        Material source = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        Material custom = null;
        Texture2D texture = null;
        CameraOcclusionFade fade = null;
        try
        {
            SceneManager.SetActiveScene(scene);
            var cameraObject = new GameObject("Isolated Output Camera");
            var camera = cameraObject.AddComponent<Camera>(); camera.enabled = true;
            camera.transform.position = new Vector3(0, 1, -5); camera.transform.rotation = Quaternion.identity;
            fade = cameraObject.AddComponent<CameraOcclusionFade>();
            fade.fallbackTemplate = template; fade.verticalSampleOffset = 0;
            source.SetColor("_BaseColor", new Color(.35f, .45f, .55f, 1));
            var subject = new GameObject("Subject"); subject.transform.position = new Vector3(0, 1, 5);
            fade.SetFocus(subject.transform, subject.transform.position);
            var wall = Cube("Blocker", new Vector3(0, 1, 0), source);
            wall.transform.localScale = new Vector3(3, 3, .3f);
            var clear = Cube("Off Camera Ray", new Vector3(5, 1, 0), source);
            var behind = Cube("Behind Target", new Vector3(0, 1, 8), source);
            Sync(); Step(1);
            Check(fade.FadingRendererCount == 1 && Alpha(wall) < .2f, "Blocker must fade; off-ray and behind-target meshes must stay opaque.");
            Check(wall.sharedMaterial != source && clear.sharedMaterial == source && behind.sharedMaterial == source, "Original/shared materials changed.");
            Check(source.GetFloat("_Surface") == 0 && source.GetColor("_BaseColor").a == 1, "Source material was mutated.");
            Check(wall.GetComponent<Collider>().enabled, "Fade changed collider.");
            report += "PASS: ray obstruction, shared material isolation, collider preserved.\n";
            // Existing global and per-submesh property blocks survive fading and restoration.
            fade.RestoreAll();
            var global = new MaterialPropertyBlock(); global.SetFloat("_OcclusionValidation", 42); wall.SetPropertyBlock(global);
            var block = new MaterialPropertyBlock(); block.SetColor("_BaseColor", Color.green); wall.SetPropertyBlock(block, 0);
            Step(.5f); Check(Alpha(wall) < .2f, "Per-submesh property block stopped fade.");
            subject.transform.position = new Vector3(6, 1, 5); fade.SetFocus(subject.transform, subject.transform.position); Sync(); Step(1);
            Check(wall.sharedMaterial == source && fade.FadingRendererCount == 0, "Moving target did not restore original materials.");
            wall.GetPropertyBlock(block, 0); wall.GetPropertyBlock(global);
            Check(block.GetColor("_BaseColor") == Color.green && global.GetFloat("_OcclusionValidation") == 42, "Property blocks were not restored.");
            wall.SetPropertyBlock(null); wall.SetPropertyBlock(null, 0);
            subject.transform.position = new Vector3(0, 1, 5); fade.SetFocus(subject.transform, subject.transform.position);
            var second = Cube("Second Blocker", new Vector3(0, 1, 2), source); Sync(); Step(.5f);
            Check(fade.FadingRendererCount == 2 && Alpha(second) < .2f, "Multiple blockers did not fade together.");
            fade.enabled = false;
            // Non-ExecuteAlways camera scripts do not receive Play Mode lifecycle callbacks in this Edit Mode fixture.
            fade.SendMessage("OnDisable");
            Check(wall.sharedMaterial == source && second.sharedMaterial == source && fade.FadingRendererCount == 0, "Disabling camera effect leaked materials.");
            fade.enabled = true; second.gameObject.SetActive(false);
            report += "PASS: target switch, multiple blockers, MPB restore, disable cleanup.\n";
            // A collider containing the camera origin must still be detected.
            wall.transform.position = camera.transform.position; Sync(); Step(.5f);
            Check(Alpha(wall) < .2f, "Camera-inside-wall overlap was not detected.");
            fade.RestoreAll(); wall.transform.position = new Vector3(0, 1, 0);
            wall.GetComponent<Collider>().isTrigger = true; Sync(); Step(.5f);
            Check(fade.FadingRendererCount == 0, "Trigger volumes should not fade.");
            wall.GetComponent<Collider>().isTrigger = false;
            var marker = wall.gameObject.AddComponent<CameraOcclusionObject>(); marker.ignoreOcclusion = true; Sync(); Step(.5f);
            Check(fade.FadingRendererCount == 0, "Ignore Occlusion was not respected.");
            marker.ignoreOcclusion = false; wall.GetComponent<Collider>().enabled = false; Sync(); Step(.5f);
            Check(Alpha(wall) < .2f, "Explicit no-collider object marker failed.");
            fade.RestoreAll(); marker.ignoreOcclusion = true;
            report += "PASS: camera inside mesh, trigger exclusion, opt-out and no-collider marker.\n";
            // Environment with fixed opaque custom shader uses a temporary URP fallback.
            var customShader = Shader.Find("Toby Fredson/The Toby Foliage Engine/(TTFE) Tree Foliage");
            Check(customShader != null, "TTFE shader missing from validation fixture.");
            custom = new Material(customShader); texture = new Texture2D(2, 2);
            custom.SetTexture("_AlbedoMap", texture);
            marker.ignoreOcclusion = false; wall.sharedMaterial = custom; Sync(); Step(.5f);
            Check(wall.sharedMaterial.shader == template.shader && wall.sharedMaterial.GetTexture("_BaseMap") == texture && Alpha(wall) < .2f,
                "Opaque custom material fallback failed.");
            fade.RestoreAll(); Check(wall.sharedMaterial == custom, "TTFE material was not restored.");
            report += "PASS: TTFE fallback texture, transparency and exact material restoration.\n";
            // Produce images of the same isolated scene before/after fading for visual inspection.
            wall.sharedMaterial = source;
            subject.transform.position = new Vector3(0, 1, 5);
            var visibleSubject = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            visibleSubject.name = "Visible Subject"; visibleSubject.transform.SetParent(subject.transform, false);
            var subjectMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            try
            {
                subjectMaterial.SetColor("_BaseColor", new Color(.03f, .85f, 1));
                visibleSubject.GetComponent<Renderer>().sharedMaterial = subjectMaterial;
                var light = new GameObject("Fixture Light").AddComponent<Light>(); light.type = LightType.Directional;
                light.transform.rotation = Quaternion.Euler(45, 35, 0);
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.06f, .08f, .12f);
                camera.fieldOfView = 45;
                Capture(camera, "Library/CombatValidation/CameraOcclusion-before.png");
                fade.SetFocus(subject.transform, subject.transform.position); Sync(); Step(.5f);
                Capture(camera, "Library/CombatValidation/CameraOcclusion-after.png");
            }
            finally { UnityEngine.Object.DestroyImmediate(subjectMaterial); }
            fade.RestoreAll();
            Check(fade.FadingRendererCount == 0, "Final cleanup failed.");

            void Sync() { Physics.SyncTransforms(); }
            void Step(float seconds) { for (int i = 0; i < Mathf.CeilToInt(seconds / .05f); i++) fade.Tick(.05f); }
        }
        finally
        {
            if (fade != null) fade.RestoreAll();
            if (originalActive.IsValid() && originalActive.isLoaded) SceneManager.SetActiveScene(originalActive);
            EditorSceneManager.CloseScene(scene, true);
            UnityEngine.Object.DestroyImmediate(source);
            if (custom != null) UnityEngine.Object.DestroyImmediate(custom);
            if (texture != null) UnityEngine.Object.DestroyImmediate(texture);
        }
    }

    static Renderer Cube(string name, Vector3 position, Material material)
    {
        var obj = GameObject.CreatePrimitive(PrimitiveType.Cube); obj.name = name; obj.transform.position = position;
        var renderer = obj.GetComponent<Renderer>(); renderer.sharedMaterial = material; return renderer;
    }
    static float Alpha(Renderer renderer)
    { var block = new MaterialPropertyBlock(); renderer.GetPropertyBlock(block, 0); return block.GetColor("_BaseColor").a; }
    static void Capture(Camera camera, string path)
    {
        var rt = new RenderTexture(640, 360, 24);
        var texture = new Texture2D(640, 360, TextureFormat.RGB24, false);
        var old = RenderTexture.active;
        try
        {
            camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt;
            texture.ReadPixels(new Rect(0, 0, 640, 360), 0, 0); texture.Apply(); File.WriteAllBytes(path, texture.EncodeToPNG());
        }
        finally
        { camera.targetTexture = null; RenderTexture.active = old; rt.Release(); UnityEngine.Object.DestroyImmediate(rt); UnityEngine.Object.DestroyImmediate(texture); }
    }
    static void Check(bool valid, string message) { if (!valid) throw new InvalidOperationException(message); }
}

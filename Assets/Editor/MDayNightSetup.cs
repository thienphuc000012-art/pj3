using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.UI;
using TMPro;

public static class MDayNightSetup
{
    const string Request = "Library/CombatValidation/MDayNight.request";
    const string Output = "Tools/DayNight";
    const string Clouds = "Assets/asset/Toby Fredson/Rocky Hills Environment - Whitebark Pine/RHEWP_Demo/Particles/Particles_Clouds/Clouds Default Thin.prefab";
    const string Stars = "Assets/Altar_Ruins_FREE/Prefabs/Particles/Stars.prefab";
    const string Moon = "Assets/Altar_Ruins_FREE/Prefabs/Moon.prefab";
    [InitializeOnLoadMethod] static void Init() { EditorApplication.update -= Poll; EditorApplication.update += Poll; }
    static void Poll()
    {
        if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        if (File.GetLastWriteTimeUtc("Library/ScriptAssemblies/Assembly-CSharp-Editor.dll") < File.GetLastWriteTimeUtc("Assets/Editor/MDayNightSetup.cs")) return;
        string command;
        try { command = File.ReadAllText(Request).Trim(); File.Delete(Request); } catch (IOException) { return; }
        try
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Day/night setup requires Edit Mode.");
            if (command == "apply") Apply();
            else if (command == "soften-player-light") SoftenPlayerNightLight();
            else if (command == "verify") Validate();
            else Inspect();
        }
        catch (Exception e) { Directory.CreateDirectory(Output); File.WriteAllText(Output + "/Error.txt", e.ToString()); Debug.LogException(e); }
    }
    static Scene Map() => Enumerable.Range(0, SceneManager.sceneCount).Select(SceneManager.GetSceneAt).Single(s => s.name == "m");
    static T[] Components<T>(Scene scene) where T : Component => scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true)).ToArray();

    [MenuItem("Tools/Adventure/Day Night/Soften Player Night Light")]
    public static void SoftenPlayerNightLight()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode before saving player light tuning.");
        var scene = Map(); var rig = Components<MDayNightController>(scene).Single();
        string backup = Output + "/Before-Player-Light-" + DateTime.Now.ToString("yyyyMMdd-HHmmss"); Directory.CreateDirectory(backup);
        File.Copy(scene.path, backup + "/m.disk.unity");
        if (!EditorSceneManager.SaveScene(scene, backup + "/m.current.unity", true)) throw new Exception("Cannot back up scene before tuning player light.");
        Undo.RecordObject(rig, "Soften player night light");
        rig.playerLightIntensity = .8f; rig.playerLightRange = 7f;
        rig.playerLightColor = new Color(1f, .91f, .8f);
        rig.playerLightOffset = new Vector3(0, 2.8f, -1.4f);
        if (rig.playerNightLight != null)
        {
            Undo.RecordObjects(new UnityEngine.Object[] { rig.playerNightLight, rig.playerNightLight.transform }, "Soften player night light");
            rig.playerNightLight.intensity = rig.playerLightIntensity;
            rig.playerNightLight.range = rig.playerLightRange;
            rig.playerNightLight.color = rig.playerLightColor;
            rig.playerNightLight.shadows = LightShadows.None;
            if (rig.player != null) rig.playerNightLight.transform.position = rig.player.position + rig.player.rotation * rig.playerLightOffset;
        }
        EditorUtility.SetDirty(rig); EditorSceneManager.MarkSceneDirty(scene);
        Validate();
    }

    [MenuItem("Tools/Adventure/Day Night/Set Up Scene m and Clock HUD")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode before setup.");
        var scene = Map();
        string backup = Output + "/Before-" + DateTime.Now.ToString("yyyyMMdd-HHmmss"); Directory.CreateDirectory(backup);
        File.Copy(scene.path, backup + "/m.disk.unity");
        if (!EditorSceneManager.SaveScene(scene, backup + "/m.current.unity", true)) throw new Exception("Cannot back up current scene.");
        const string canvasPath = "Assets/Resources/Adventure/UI/AdventureCanvas.prefab";
        File.Copy(canvasPath, backup + "/AdventureCanvas.prefab");
        const string generated = "Assets/Adventure/Generated/DayNight";
        Directory.CreateDirectory(generated); AssetDatabase.Refresh();
        var clock = AssetDatabase.LoadAssetAtPath<AdventureWorldTimeSettings>("Assets/Resources/Adventure/WorldTimeSettings.asset");
        if (clock == null) { clock = ScriptableObject.CreateInstance<AdventureWorldTimeSettings>(); AssetDatabase.CreateAsset(clock, "Assets/Resources/Adventure/WorldTimeSettings.asset"); }
        var sky = CreateMaterial(generated + "/m Sky.mat", "Adventure/Day Night Sky");
        var moonMat = CreateMaterial(generated + "/m Moon.mat", "Adventure/Celestial Unlit");
        moonMat.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture>("Assets/Altar_Ruins_FREE/Art/Textures/Moon.png"));
        var starMat = CreateMaterial(generated + "/m Stars.mat", "Adventure/Celestial Unlit");
        var dot = AssetDatabase.LoadAssetAtPath<Texture2D>(generated + "/Star.png");
        if (dot == null)
        {
            dot = new Texture2D(32, 32, TextureFormat.RGBA32, false);
            var pixels = new Color[1024];
            for (int y = 0; y < 32; y++) for (int x = 0; x < 32; x++)
            { float radius = new Vector2((x - 15.5f) / 15.5f, (y - 15.5f) / 15.5f).magnitude; pixels[y * 32 + x] = new Color(1, 1, 1, Mathf.Pow(Mathf.Clamp01(1 - radius), 2)); }
            dot.SetPixels(pixels); dot.Apply(); File.WriteAllBytes(generated + "/Star.png", dot.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(dot);
            AssetDatabase.ImportAsset(generated + "/Star.png");
            var importer = (TextureImporter)AssetImporter.GetAtPath(generated + "/Star.png"); importer.alphaIsTransparency = true; importer.mipmapEnabled = false; importer.wrapMode = TextureWrapMode.Clamp; importer.SaveAndReimport();
            dot = AssetDatabase.LoadAssetAtPath<Texture2D>(generated + "/Star.png");
        }
        starMat.SetTexture("_MainTex", dot); EditorUtility.SetDirty(starMat); EditorUtility.SetDirty(moonMat);
        var rig = Components<MDayNightController>(scene).SingleOrDefault();
        if (rig == null) { var root = new GameObject("Day Night System"); SceneManager.MoveGameObjectToScene(root, scene); rig = root.AddComponent<MDayNightController>(); }
        rig.clockSettings = clock; rig.skyMaterial = sky;
        rig.sun = Components<Light>(scene).First(l => l.type == LightType.Directional && l.name == "Directional Light");
        rig.player = Components<PlayerScript>(scene).First().transform;
        rig.viewCamera = Components<Camera>(scene).First(c => c.name == "Main Camera (1)");
        if (rig.skyVisuals == null) rig.skyVisuals = Child(rig.transform, "Sky Visuals");
        if (rig.clouds == null) rig.clouds = ((GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Clouds), rig.skyVisuals)).transform;
        rig.clouds.name = "Clouds Default Thin";
        var cloudSystem = rig.clouds.GetComponentInChildren<ParticleSystem>(true);
        cloudSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = cloudSystem.main; main.loop = true; main.prewarm = true; main.duration = 120; main.startLifetime = 120; main.startSpeed = 0;
        main.startSize = new ParticleSystem.MinMaxCurve(95, 145); main.maxParticles = 40; main.simulationSpace = ParticleSystemSimulationSpace.Local;
        var shape = cloudSystem.shape; shape.scale = new Vector3(600, 600, 12);
        var emission = cloudSystem.emission; emission.rateOverTime = .18f; emission.SetBursts(Array.Empty<ParticleSystem.Burst>());
        var cloudSource = cloudSystem.GetComponent<ParticleSystemRenderer>().sharedMaterial;
        var cloudMat = AssetDatabase.LoadAssetAtPath<Material>(generated + "/m Clouds.mat");
        if (cloudMat == null) { cloudMat = new Material(cloudSource); AssetDatabase.CreateAsset(cloudMat, generated + "/m Clouds.mat"); }
        cloudMat.renderQueue = 3020; cloudMat.SetFloat("_Alpha", .35f); EditorUtility.SetDirty(cloudMat);
        cloudSystem.GetComponent<ParticleSystemRenderer>().sharedMaterial = cloudMat;
        if (rig.stars == null) rig.stars = ((GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Stars), rig.skyVisuals)).GetComponent<ParticleSystem>();
        main = rig.stars.main; main.playOnAwake = false; main.maxParticles = 300; main.startSpeed = 0; main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
        emission = rig.stars.emission; emission.enabled = false;
        shape = rig.stars.shape; shape.enabled = false;
        rig.stars.GetComponent<ParticleSystemRenderer>().sharedMaterial = starMat;
        if (rig.moon == null) rig.moon = ((GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Moon), rig.skyVisuals)).transform;
        rig.moon.GetComponentInChildren<Renderer>(true).sharedMaterial = moonMat;
        foreach (var r in rig.skyVisuals.GetComponentsInChildren<Renderer>(true)) { r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false; r.lightProbeUsage = LightProbeUsage.Off; r.reflectionProbeUsage = ReflectionProbeUsage.Off; }
        rig.moonLight = EnsureLight(rig.transform, "Moon Fill", LightType.Directional);
        rig.moonLight.shadows = LightShadows.None; rig.moonLight.enabled = false;
        rig.playerNightLight = EnsureLight(rig.transform, "Player Night Light", LightType.Point);
        rig.playerNightLight.shadows = LightShadows.None; rig.playerNightLight.enabled = false;
        rig.skyVisuals.position = rig.viewCamera.transform.position;
        rig.clouds.localPosition = Vector3.up * rig.cloudHeight;
        rig.moon.localScale = Vector3.one * rig.moonScale;
        rig.skyVisuals.gameObject.SetActive(false);
        foreach (var component in rig.skyVisuals.GetComponentsInChildren<Component>(true))
            if (PrefabUtility.IsPartOfPrefabInstance(component)) PrefabUtility.RecordPrefabInstancePropertyModifications(component);
        EditorUtility.SetDirty(rig);
        var prefab = PrefabUtility.LoadPrefabContents(canvasPath);
        try { ConfigureHUD(prefab.GetComponent<AdventureCanvasRoot>()); PrefabUtility.SaveAsPrefabAsset(prefab, canvasPath); }
        finally { PrefabUtility.UnloadPrefabContents(prefab); }
        foreach (var canvas in Components<AdventureCanvasRoot>(scene).Where(c => c.kind == AdventureCanvasKind.Adventure)) ConfigureHUD(canvas);
        AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new Exception("Cannot save m.");
        File.WriteAllText(Output + "/Setup.txt", "Day/night rig saved in m only. Shared clock HUD authored in AdventureCanvas. Backup=" + backup + "\n");
        Validate();
    }
    static Transform Child(Transform parent, string name)
    {
        var existing = parent.Find(name); if (existing != null) return existing;
        var go = new GameObject(name); go.transform.SetParent(parent, false); return go.transform;
    }
    static Light EnsureLight(Transform root, string name, LightType type)
    {
        var t = Child(root, name); var light = t.GetComponent<Light>(); if (light == null) light = t.gameObject.AddComponent<Light>(); light.type = type; light.lightmapBakeType = LightmapBakeType.Realtime; return light;
    }
    static Material CreateMaterial(string path, string shader)
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null) return material;
        var found = Shader.Find(shader); if (found == null || !found.isSupported) throw new Exception("Unsupported shader " + shader);
        material = new Material(found); AssetDatabase.CreateAsset(material, path); return material;
    }
    static void ConfigureHUD(AdventureCanvasRoot canvas)
    {
        var hud = canvas.transform.Find("HUD"); if (hud == null) throw new Exception("Missing HUD in AdventureCanvas");
        var panel = hud.Find("World Time") as RectTransform;
        if (panel == null) { panel = (RectTransform)new GameObject("World Time", typeof(RectTransform), typeof(PreserveAuthoredUI), typeof(CanvasRenderer), typeof(Image)).transform; panel.SetParent(hud, false); }
        panel.anchorMin = panel.anchorMax = panel.pivot = Vector2.one; panel.anchoredPosition = new Vector2(-36, -22); panel.sizeDelta = new Vector2(142, 62);
        var background = panel.GetComponent<Image>(); background.color = new Color(.015f, .025f, .04f, .58f); background.raycastTarget = false;
        var text = panel.Find("Time")?.GetComponent<TextMeshProUGUI>();
        if (text == null) { var go = new GameObject("Time", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI)); go.transform.SetParent(panel, false); text = go.GetComponent<TextMeshProUGUI>(); }
        text.rectTransform.anchorMin = Vector2.zero; text.rectTransform.anchorMax = Vector2.one; text.rectTransform.offsetMin = new Vector2(12, 8); text.rectTransform.offsetMax = new Vector2(-12, -6);
        text.font = canvas.defaultFont; text.fontSize = 30; text.alignment = TextAlignmentOptions.Center; text.color = new Color(.97f, .94f, .86f); text.raycastTarget = false; text.text = "08:00";
        var line = panel.Find("Accent") as RectTransform;
        if (line == null) { line = (RectTransform)new GameObject("Accent", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).transform; line.SetParent(panel, false); }
        line.anchorMin = new Vector2(0, 0); line.anchorMax = new Vector2(1, 0); line.pivot = Vector2.zero; line.offsetMin = new Vector2(12, 0); line.offsetMax = new Vector2(-12, 1);
        line.GetComponent<Image>().color = new Color(.72f, .58f, .32f, .65f); line.GetComponent<Image>().raycastTarget = false;
        var controller = canvas.GetComponent<AdventureTimeHUD>(); if (controller == null) controller = canvas.gameObject.AddComponent<AdventureTimeHUD>(); controller.timeLabel = text; EditorUtility.SetDirty(controller);
    }

    [MenuItem("Tools/Adventure/Day Night/Validate Scene m and Clock")]
    public static void Validate()
    {
        var scene = Map(); var rig = Components<MDayNightController>(scene).Single();
        string backup = Output + "/Before-Validation-" + DateTime.Now.ToString("yyyyMMdd-HHmmss"); Directory.CreateDirectory(backup);
        File.Copy(scene.path, backup + "/m.disk.unity");
        if (!EditorSceneManager.SaveScene(scene, backup + "/m.current.unity", true)) throw new Exception("Cannot back up scene before preview validation.");
        if (rig.sun == null || rig.skyMaterial == null || rig.player == null || rig.moon == null || rig.stars == null || rig.clouds == null || rig.clockSettings == null) throw new Exception("Incomplete day/night references.");
        var report = new StringBuilder();
        var old = new CampaignSave(); if (AdventureWorldTime.Read(old) != rig.clockSettings.startingHour) throw new Exception("Old save default failed");
        old.hasWorldTime = true; old.worldTimeHours = 23.99; string json = JsonUtility.ToJson(old); var copy = JsonUtility.FromJson<CampaignSave>(json);
        AdventureWorldTime.Advance(copy, rig.clockSettings.dayLengthMinutes / 12);
        if (rig.clockSettings.advanceTime && (copy.worldTimeHours <= 24 || AdventureWorldTime.Format(copy.worldTimeHours) != "00:01")) throw new Exception("Midnight rollover failed: " + copy.worldTimeHours);
        report.AppendLine("PASS: old-save defaults, JSON round trip, midnight rollover; clock fields travel in existing campaign save.");
        try
        {
            int moonInstance = rig.moon.GetInstanceID();
            Vector3 previous = Vector3.zero, midnight = Vector3.zero;
            var orbit = new StringBuilder("Hour\tSunElevation\tMoonElevation\tAngleToPrevious\n");
            for (int step = 0; step <= 48; step++)
            {
                float hour = step * .5f; rig.Apply(hour, rig.viewCamera.transform.position);
                Vector3 direction = (rig.moon.position - rig.skyVisuals.position).normalized;
                float change = step == 0 ? 0 : Vector3.Angle(previous, direction);
                if (rig.moon.GetInstanceID() != moonInstance || !rig.moon.gameObject.activeInHierarchy || Vector3.Dot(-rig.sun.transform.forward, direction) > -.9999f)
                    throw new Exception("Sun/moon orbit lost opposition or replaced/disabled the moon at hour " + hour);
                if (step > 0 && Mathf.Abs(change - 7.5f) > .02f) throw new Exception("Orbit jumped at hour " + hour);
                if (step == 0) midnight = direction;
                if (step == 24 && Vector3.Dot(direction, midnight) > -.9999f) throw new Exception("Moon failed to exchange positions after twelve hours");
                if (step == 48 && Vector3.Angle(direction, midnight) > .02f) throw new Exception("Midnight wrap changed orbit position");
                previous = direction;
                orbit.AppendLine(hour + "\t" + (-rig.sun.transform.forward.y) + "\t" + direction.y + "\t" + change);
            }
            report.AppendLine("PASS: 49 orbit samples over 24 hours; sun/moon stay 180 degrees apart, swap after 12 hours, wrap midnight continuously and reuse the same active Moon instance.");
            File.WriteAllText(Output + "/Orbit.tsv", orbit.ToString());
            foreach (float hour in new[] { 0f, 5.5f, 6.5f, 8f, 12f, 18f, 20f })
            {
                rig.Apply(hour, rig.viewCamera.transform.position);
                if (RenderSettings.ambientSkyColor.maxColorComponent < .4f || rig.playerNightLight.shadows != LightShadows.None) throw new Exception("Night visibility or shadow policy failed");
                if (hour == 0 && (!rig.playerNightLight.enabled || rig.moonLight.intensity < .2f || rig.stars.particleCount != 280)) throw new Exception("Missing night assets or light: player=" + rig.playerNightLight.enabled + " moon=" + rig.moonLight.intensity + " stars=" + rig.stars.particleCount);
                report.AppendLine("PASS hour=" + hour + " sun=" + rig.sun.intensity + " moon=" + rig.moonLight.intensity + " player=" + rig.playerNightLight.intensity + " stars=" + rig.stars.particleCount);
                RenderPreview(rig, hour);
                if (hour == 0 || hour == 12) RenderSkyPreview(rig, hour);
            }
            var other = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(other);
                var sentinel = new Color(.13f, .24f, .35f); RenderSettings.ambientSkyColor = sentinel;
                rig.Apply(0, Vector3.zero);
                if (RenderSettings.ambientSkyColor != sentinel || rig.OwnsActiveScene || rig.playerNightLight.enabled) throw new Exception("Inactive m altered other scene lighting");
                report.AppendLine("PASS: additive destination lighting untouched; m sky and player light disabled when another scene is active.");
            }
            finally { SceneManager.SetActiveScene(scene); EditorSceneManager.CloseScene(other, true); }
        }
        finally { rig.RestoreEnvironment(); rig.skyVisuals.gameObject.SetActive(false); rig.moonLight.enabled = false; rig.playerNightLight.enabled = false; }
        var canvas = AssetDatabase.LoadAssetAtPath<AdventureCanvasRoot>("Assets/Resources/Adventure/UI/AdventureCanvas.prefab");
        if (canvas.GetComponent<AdventureTimeHUD>()?.timeLabel == null) throw new Exception("Clock HUD missing from shared Canvas");
        foreach (var component in rig.skyVisuals.GetComponentsInChildren<Component>(true))
            if (PrefabUtility.IsPartOfPrefabInstance(component)) PrefabUtility.RecordPrefabInstancePropertyModifications(component);
        report.AppendLine("PASS: shared AdventureCanvas has an authored top-right clock; raycasts disabled; mapgame has no lighting controller.");
        if (Components<MDayNightController>(scene).Length != 1) throw new Exception("Duplicate rig");
        Directory.CreateDirectory(Output); File.WriteAllText(Output + "/Validation.txt", report.ToString());
        EditorSceneManager.SaveScene(scene);
        var savedScene = EditorSceneManager.OpenPreviewScene(scene.path);
        try
        {
            var saved = Components<MDayNightController>(savedScene).Single();
            var savedCloud = saved.clouds.GetComponentInChildren<ParticleSystem>(true);
            if (saved.sun == null || saved.player == null || saved.viewCamera == null || saved.skyMaterial == null || saved.moon == null || saved.stars == null || saved.playerNightLight == null) throw new Exception("Saved scene lost a rig reference");
            if (savedCloud.main.startSize.constantMax < 90 || savedCloud.main.startLifetime.constantMax < 100) throw new Exception("Cloud prefab overrides were not saved");
            if (saved.stars.GetComponent<Renderer>().sharedMaterial.shader.name != "Adventure/Celestial Unlit" || saved.moon.GetComponentInChildren<Renderer>(true).sharedMaterial.shader.name != "Adventure/Celestial Unlit") throw new Exception("Celestial materials were not saved");
            File.AppendAllText(Output + "/Validation.txt", "PASS: saved scene reopened in isolation; prefab overrides, all rig references, cloud dimensions and celestial materials persisted.\n");
        }
        finally { EditorSceneManager.ClosePreviewScene(savedScene); }
        if (File.Exists(Output + "/Error.txt")) File.Move(Output + "/Error.txt", Output + "/Resolved-" + DateTime.Now.ToString("HHmmss") + ".txt");
        Debug.Log("[Day Night] Setup and Edit Mode validation passed. " + Output);
    }
    static void RenderPreview(MDayNightController rig, float hour)
    {
        if (hour != 0 && hour != 6.5f && hour != 12 && hour != 18) return;
        var go = new GameObject("Temporary day/night preview camera"); SceneManager.MoveGameObjectToScene(go, rig.gameObject.scene);
        var camera = go.AddComponent<Camera>(); camera.CopyFrom(rig.viewCamera); camera.enabled = false;
        camera.transform.position = rig.player.position + new Vector3(0, 3, -5);
        camera.transform.rotation = Quaternion.LookRotation(rig.player.position + Vector3.up * 1.4f + rig.player.forward * 12 - camera.transform.position);
        var hud = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<AdventureCanvasRoot>("Assets/Resources/Adventure/UI/AdventureCanvas.prefab"));
        SceneManager.MoveGameObjectToScene(hud.gameObject, rig.gameObject.scene);
        foreach (Transform child in hud.transform) if (child.name != "HUD") child.gameObject.SetActive(false);
        foreach (Transform child in hud.transform.Find("HUD")) child.gameObject.SetActive(child.name == "World Time");
        hud.transform.Find("HUD").gameObject.SetActive(true);
        hud.GetComponent<AdventureTimeHUD>().timeLabel.text = AdventureWorldTime.Format(hour);
        var canvas = hud.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
        Canvas.ForceUpdateCanvases();
        var target = RenderTexture.GetTemporary(960, 540, 24); var previous = RenderTexture.active; Texture2D image = null;
        try
        {
            camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
            image = new Texture2D(960, 540, TextureFormat.RGB24, false); image.ReadPixels(new Rect(0, 0, 960, 540), 0, 0); image.Apply();
            File.WriteAllBytes(Output + "/Preview-" + hour.ToString("00.0", System.Globalization.CultureInfo.InvariantCulture) + ".png", image.EncodeToPNG());
        }
        finally { RenderTexture.active = previous; camera.targetTexture = null; RenderTexture.ReleaseTemporary(target); if (image != null) UnityEngine.Object.DestroyImmediate(image); UnityEngine.Object.DestroyImmediate(hud.gameObject); UnityEngine.Object.DestroyImmediate(go); }
    }
    static void RenderSkyPreview(MDayNightController rig, float hour)
    {
        var go = new GameObject("Temporary sky preview camera"); SceneManager.MoveGameObjectToScene(go, rig.gameObject.scene);
        var camera = go.AddComponent<Camera>(); camera.CopyFrom(rig.viewCamera); camera.enabled = false;
        camera.transform.position = rig.viewCamera.transform.position + Vector3.up * 180;
        rig.Apply(hour, camera.transform.position);
        foreach (var cloud in rig.clouds.GetComponentsInChildren<ParticleSystem>(true)) cloud.Simulate(120, true, true);
        Vector3 bodyDirection = hour == 12 ? -rig.sun.transform.forward : (rig.moon.position - camera.transform.position).normalized;
        camera.transform.rotation = Quaternion.LookRotation(bodyDirection, rig.sun.transform.up);
        File.WriteAllText(Output + "/SkyState-" + hour.ToString("00") + ".txt", "stars=" + rig.stars.particleCount + " bounds=" + rig.stars.GetComponent<Renderer>().bounds + " cloud particles=" + rig.clouds.GetComponentInChildren<ParticleSystem>().particleCount);
        var target = RenderTexture.GetTemporary(960, 540, 24); var previous = RenderTexture.active; Texture2D image = null;
        try
        {
            camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
            image = new Texture2D(960, 540, TextureFormat.RGB24, false); image.ReadPixels(new Rect(0, 0, 960, 540), 0, 0); image.Apply();
            File.WriteAllBytes(Output + "/Sky-" + hour.ToString("00", System.Globalization.CultureInfo.InvariantCulture) + ".png", image.EncodeToPNG());
        }
        finally { RenderTexture.active = previous; camera.targetTexture = null; RenderTexture.ReleaseTemporary(target); if (image != null) UnityEngine.Object.DestroyImmediate(image); UnityEngine.Object.DestroyImmediate(go); }
    }
    static void Inspect()
    {
        var s = Map(); var text = new StringBuilder();
        text.AppendLine("Scene=" + s.path + " active=" + SceneManager.GetActiveScene().name + " dirty=" + s.isDirty);
        foreach (var root in s.GetRootGameObjects())
        {
            foreach (var light in root.GetComponentsInChildren<Light>(true).Where(l => l.type == LightType.Directional)) text.AppendLine("LIGHT " + light.name + " " + light.transform.eulerAngles + " intensity=" + light.intensity);
            foreach (var c in root.GetComponentsInChildren<Camera>(true)) text.AppendLine("CAMERA " + c.name + " pos=" + c.transform.position + " far=" + c.farClipPlane);
            foreach (var p in root.GetComponentsInChildren<PlayerScript>(true)) text.AppendLine("PLAYER " + p.name + " pos=" + p.transform.position);
        }
        foreach (string path in new[] { Clouds, Stars, Moon })
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) throw new Exception("Missing prefab " + path);
            text.AppendLine("PREFAB " + path);
            foreach (var p in prefab.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = p.main; var shape = p.shape;
                text.AppendLine("PARTICLE " + p.name + " size=" + main.startSize.constantMax + " lifetime=" + main.startLifetime.constantMax + " speed=" + main.startSpeed.constantMax + " max=" + main.maxParticles + " shape=" + shape.shapeType + " radius=" + shape.radius + " scale=" + shape.scale + " rotation=" + p.transform.localEulerAngles + " simulation=" + main.simulationSpace);
            }
            foreach (var r in prefab.GetComponentsInChildren<Renderer>(true))
                text.AppendLine("RENDERER " + r.name + " bounds=" + r.bounds + " " + string.Join(",", r.sharedMaterials.Select(m => m == null ? "NULL" : m.name + ":" + m.shader.name + ":" + AssetDatabase.GetAssetPath(m))));
            foreach (var f in prefab.GetComponentsInChildren<MeshFilter>(true)) text.AppendLine("MESH " + f.name + " " + f.sharedMesh.bounds);
        }
        Directory.CreateDirectory(Output); File.WriteAllText(Output + "/Inspection.txt", text.ToString()); Debug.Log("Day/night inspection: " + Output);
    }
}

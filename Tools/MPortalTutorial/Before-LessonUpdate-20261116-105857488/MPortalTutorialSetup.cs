using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using TMPro;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class MPortalTutorialSetup
{
    public const string ModelPath = "Assets/asset/model item/european-and-american-game-scencemagic-portal/source/low textures/low.fbx";
    const string Request = "Library/CombatValidation/MPortalTutorial.request";
    const string Output = "Tools/MPortalTutorial";
    [InitializeOnLoadMethod] static void Init() { EditorApplication.update -= Poll; EditorApplication.update += Poll; }
    static void Poll()
    {
        if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (new[] { "Assets/Editor/MPortalTutorialSetup.cs", "Assets/Editor/MPortalTutorialValidation.cs" }
            .Any(path => File.GetLastWriteTimeUtc("Library/ScriptAssemblies/Assembly-CSharp-Editor.dll") < File.GetLastWriteTimeUtc(path))) return;
        if (File.GetLastWriteTimeUtc("Library/ScriptAssemblies/Assembly-CSharp.dll") < File.GetLastWriteTimeUtc("Assets/Scripts/Adventure/MExplorationTutorial.cs")) return;
        string command = File.ReadAllText(Request).Trim(); File.Delete(Request);
        try { if (command == "setup") Setup(); else if (command == "finish") Finish(); else if (command == "validate") Validate(); else Inspect(); }
        catch (Exception error) { Directory.CreateDirectory(Output); File.WriteAllText(Output + "/Error-" + DateTime.Now.ToString("yyyyMMdd-HHmmssfff") + ".txt", error.ToString()); Debug.LogException(error); }
    }
    [MenuItem("Tools/Adventure/Tutorial/Inspect m Portal and Route")]
    public static void Inspect()
    {
        Directory.CreateDirectory(Output); var report = new StringBuilder();
        var scene = SceneManager.GetSceneByPath("Assets/Scenes/m.unity");
        if (!scene.IsValid() || !scene.isLoaded) throw new InvalidOperationException("Open scene m in Edit Mode first.");
        var roots = scene.GetRootGameObjects();
        var player = roots.SelectMany(r => r.GetComponentsInChildren<PlayerScript>(true)).First();
        report.AppendLine("Scene dirty=" + scene.isDirty + "; player=" + player.transform.position.ToString("F5") + " yaw=" + player.transform.eulerAngles);
        var start = new Vector3(-87.2855682f, -91.1699982f, -7.56377697f); var forward = Quaternion.Euler(0, 124.340195f, 0) * Vector3.forward;
        Physics.SyncTransforms();
        for (int i = -2; i <= 10; i++)
        {
            Vector3 position = start + forward * i;
            if (Physics.Raycast(position + Vector3.up * 5, Vector3.down, out var hit, 15, player.surfaceLayer, QueryTriggerInteraction.Ignore))
                report.AppendLine("Walk " + i + "m floor=" + hit.point.ToString("F3") + " normal=" + hit.normal.ToString("F3") + " " + Hierarchy(hit.transform));
        }
        foreach (var point in roots.SelectMany(r => r.GetComponentsInChildren<WorldInteraction>(true)).OrderBy(p => (p.transform.position - start).sqrMagnitude).Take(18))
            report.AppendLine("Interaction " + point.kind + " " + Hierarchy(point.transform) + " at=" + point.transform.position.ToString("F3") + " range=" + point.range + " id=" + point.Id);
        foreach (var c in roots.SelectMany(r => r.GetComponentsInChildren<BoxCollider>(true)).Where(c => c.name.StartsWith("climb", StringComparison.OrdinalIgnoreCase)))
            report.AppendLine("Parkour " + Hierarchy(c.transform) + " bounds=" + c.bounds);
        foreach (var c in roots.SelectMany(r => r.GetComponentsInChildren<Collider>(true)).Where(c => c.gameObject.layer == LayerMask.NameToLayer("Obstacle") &&
            (c.bounds.center - start).sqrMagnitude < 1600).OrderBy(c => (c.bounds.center - start).sqrMagnitude).Take(20))
            report.AppendLine("Nearby obstacle " + Hierarchy(c.transform) + " tag=" + c.tag + " bounds=" + c.bounds);
        foreach (var go in roots.SelectMany(r => r.GetComponentsInChildren<Transform>(true)).Where(t => PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject)))
        {
            string path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go.gameObject);
            if (path == ModelPath) report.AppendLine("Existing portal " + Hierarchy(go) + " at=" + go.position + " rot=" + go.eulerAngles + " scale=" + go.lossyScale);
        }
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        if (model == null) throw new InvalidOperationException("Missing requested low.fbx portal model.");
        var preview = EditorSceneManager.NewPreviewScene(); GameObject instance = null;
        try
        {
            instance = UnityEngine.Object.Instantiate(model); SceneManager.MoveGameObjectToScene(instance, preview);
            foreach (var r in instance.GetComponentsInChildren<Renderer>(true))
            {
                var mesh = r.GetComponent<MeshFilter>()?.sharedMesh;
                report.AppendLine("Mesh " + Hierarchy(r.transform) + " bounds=" + r.bounds + " local=" + mesh?.bounds + " vertices=" + mesh?.vertexCount + " submeshes=" + mesh?.subMeshCount);
                foreach (var material in r.sharedMaterials) report.AppendLine("  Material=" + material?.name + "; shader=" + material?.shader?.name + "; texture=" + material?.mainTexture?.name);
            }
            RenderModel(instance, preview, report);
        }
        finally { if (instance != null) UnityEngine.Object.DestroyImmediate(instance); EditorSceneManager.ClosePreviewScene(preview); }
        File.WriteAllText(Output + "/Inspection.txt", report.ToString()); Debug.Log("[m Portal Tutorial] Inspection complete: " + Output + "/Inspection.txt");
    }
    static void RenderModel(GameObject instance, Scene scene, StringBuilder report)
    {
        var renderers = instance.GetComponentsInChildren<Renderer>(true); var bounds = renderers[0].bounds;
        foreach (var r in renderers) bounds.Encapsulate(r.bounds);
        var material = new Material(Shader.Find("Universal Render Pipeline/Unlit")); material.color = new Color(.72f, .75f, .78f);
        foreach (var r in renderers) r.sharedMaterials = Enumerable.Repeat(material, r.sharedMaterials.Length).ToArray();
        var cameraObject = new GameObject("Portal inspection camera", typeof(Camera)); SceneManager.MoveGameObjectToScene(cameraObject, scene);
        var camera = cameraObject.GetComponent<Camera>(); camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.07f, .08f, .1f);
        camera.enabled = false; camera.scene = scene; camera.overrideSceneCullingMask = EditorSceneManager.GetSceneCullingMask(scene);
        camera.nearClipPlane = .01f; camera.farClipPlane = 100000; camera.fieldOfView = 40;
        var rt = new RenderTexture(640, 640, 24); var texture = new Texture2D(640, 640, TextureFormat.RGB24, false); var old = RenderTexture.active;
        try
        {
            var directions = new[] { new Vector3(0, .15f, -1), new Vector3(1, .15f, 0), new Vector3(.7f, .3f, -.7f) };
            for (int i = 0; i < directions.Length; i++)
            {
                cameraObject.transform.position = bounds.center + directions[i].normalized * bounds.size.magnitude * 1.6f;
                cameraObject.transform.LookAt(bounds.center); camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt;
                texture.ReadPixels(new Rect(0, 0, 640, 640), 0, 0); texture.Apply();
                string file = Output + "/Model-" + i + "-" + DateTime.Now.ToString("yyyyMMdd-HHmmssfff") + ".png";
                File.WriteAllBytes(file, texture.EncodeToPNG()); report.AppendLine("Preview=" + file);
            }
        }
        finally { camera.targetTexture = null; RenderTexture.active = old; rt.Release(); UnityEngine.Object.DestroyImmediate(rt); UnityEngine.Object.DestroyImmediate(texture); UnityEngine.Object.DestroyImmediate(cameraObject); UnityEngine.Object.DestroyImmediate(material); }
    }
    static string Hierarchy(Transform t) => t.parent != null ? Hierarchy(t.parent) + "/" + t.name : t.name;

    const string Generated = "Assets/Adventure/Generated/MPortalTutorial";
    static readonly Vector3 Spawn = new Vector3(-87.2855682f, -91.1699982f, -7.56377697f);
    static readonly Quaternion SpawnRotation = Quaternion.Euler(0, 124.340195f, 0);
    [MenuItem("Tools/Adventure/Tutorial/Setup m Portal Arrival and Lessons")]
    public static void Setup()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode before saving the tutorial.");
        var scene = SceneManager.GetSceneByPath("Assets/Scenes/m.unity");
        if (!scene.IsValid() || !scene.isLoaded) throw new InvalidOperationException("Open scene m first.");
        string backup = Output + "/Before-Scene-" + DateTime.Now.ToString("yyyyMMdd-HHmmssfff"); Directory.CreateDirectory(backup);
        File.Copy(scene.path, backup + "/m.disk.unity");
        if (!EditorSceneManager.SaveScene(scene, backup + "/m.current.unity", true)) throw new IOException("Could not back up unsaved scene changes.");
        Directory.CreateDirectory(Generated); AssetDatabase.Refresh();
        var player = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<PlayerScript>(true)).First();
        var root = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "M Arrival Tutorial");
        if (root == null) { root = new GameObject("M Arrival Tutorial"); SceneManager.MoveGameObjectToScene(root, scene); Undo.RegisterCreatedObjectUndo(root, "Create m tutorial"); }
        var tutorial = Component<MExplorationTutorial>(root);
        tutorial.player = player; tutorial.mapSetup = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MapGameSetup>(true)).First();
        Undo.RecordObject(player.transform, "Set portal arrival spawn"); player.transform.SetPositionAndRotation(Spawn, SpawnRotation);
        tutorial.walkStart = Pose(root.transform, "Arrival Poses/Player Start", Spawn, SpawnRotation);
        Vector3 forward = SpawnRotation * Vector3.forward, right = SpawnRotation * Vector3.right;
        Vector3 exit = Spawn + forward * 3.4f;
        if (Physics.Raycast(exit + Vector3.up * 3, Vector3.down, out var floor, 8, player.surfaceLayer, QueryTriggerInteraction.Ignore)) exit.y = floor.point.y + .03f;
        tutorial.walkExit = Pose(root.transform, "Arrival Poses/Player Exit", exit, SpawnRotation);
        tutorial.portal = BuildPortal(root.transform, scene, backup);
        var obstacle = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Collider>(true))
            .Where(c => c.gameObject.layer == LayerMask.NameToLayer("Obstacle") && c.enabled && (c.bounds.center - Spawn).sqrMagnitude < 1600)
            .OrderBy(c => (c.bounds.center - Spawn).sqrMagnitude).FirstOrDefault();
        if (obstacle == null) throw new InvalidOperationException("Missing the existing nearby parkour obstacle.");
        tutorial.parkourObstacle = obstacle;
        tutorial.campfire = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<WorldInteraction>(true))
            .Where(p => p.kind == WorldInteractionKind.RestPoint).OrderBy(p => (p.transform.position - Spawn).sqrMagnitude).FirstOrDefault();
        if (tutorial.campfire == null) throw new InvalidOperationException("Missing existing campfire/rest point.");
        var playerData = new SerializedObject(player.MCC);
        tutorial.gameplayCamera = playerData.FindProperty("vcam").objectReferenceValue as CinemachineCamera;
        SetWalkCameras(tutorial, root.transform);
        var panA = LookPose(root.transform, "Arrival Poses/Map Pan 1", Spawn + new Vector3(-9, 13, -8), Vector3.Lerp(obstacle.bounds.center, tutorial.campfire.transform.position, .5f));
        var panB = LookPose(root.transform, "Arrival Poses/Map Pan 2", Spawn + new Vector3(10, 27, -37), Spawn + new Vector3(4, 9, -72));
        tutorial.panorama = new[] { panA, panB }; tutorial.panoramaShotSeconds = 3.5f; tutorial.returnCameraSeconds = 1.8f;
        var cameraTransform = Child(root.transform, "Arrival Camera");
        tutorial.cinematicCamera = Component<CinemachineCamera>(cameraTransform.gameObject);
        tutorial.cinematicCamera.enabled = false; tutorial.cinematicCamera.Priority = -100;
        BuildCanvas(tutorial, root.transform);
        EditorUtility.SetDirty(tutorial); EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save scene m.");
        AssetDatabase.SaveAssets();
        Directory.CreateDirectory(Output); File.WriteAllText(Output + "/Setup.txt", "Scene m saved; in-memory and disk backup: " + backup + "\nPlayer start=" + Spawn.ToString("F6") + ", yaw=124.340195\nExisting parkour=" + Hierarchy(obstacle.transform) + "\nExisting campfire=" + Hierarchy(tutorial.campfire.transform));
        Validate();
    }
    static PortalArrivalVisual BuildPortal(Transform parent, Scene scene, string backup)
    {
        var anchor = Child(parent, "Arrival Portal"); anchor.SetPositionAndRotation(Spawn, SpawnRotation);
        var gate = Component<PortalArrivalVisual>(anchor.gameObject);
        var model = anchor.Find("Portal Model")?.gameObject;
        if (model == null)
            model = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true))
                .Where(t => PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject) && PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t.gameObject) == ModelPath)
                .OrderBy(t => (t.position - Spawn).sqrMagnitude).FirstOrDefault()?.gameObject;
        if (model == null) model = PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath), scene) as GameObject;
        if (model == null) throw new InvalidOperationException("Cannot instantiate the requested portal model.");
        var energy = model.GetComponentsInChildren<Renderer>(true).Single(r => r.name == "Plane001");
        Vector3 opening = model.transform.InverseTransformPoint(energy.bounds.center);
        Undo.SetTransformParent(model.transform, anchor, "Place existing portal at arrival"); model.name = "Portal Model";
        model.transform.localRotation = Quaternion.Euler(0, -90, 0); model.transform.localScale = Vector3.one * 3f;
        model.transform.localPosition = Vector3.up * 1.56f - model.transform.localRotation * (opening * 3f);
        Material stone = Lit("Portal Stone", "dizuo", false, backup), rune = Lit("Portal Rune Stone", "ji tan", true, backup);
        var gem = MaterialAt("Portal Gem", Shader.Find("Universal Render Pipeline/Lit"));
        gem.SetColor("_BaseColor", new Color(.035f, .12f, .17f)); gem.EnableKeyword("_EMISSION"); gem.SetColor("_EmissionColor", new Color(.04f, .45f, .8f) * 2f);
        var portalShader = Shader.Find("Adventure/Arrival Portal Energy"); if (portalShader == null) throw new InvalidOperationException("Portal shader has not imported yet.");
        var energyMaterial = MaterialAt("Portal Energy", portalShader);
        energyMaterial.SetTexture("_BaseMap", Texture("chuan song men_Base_Color")); energyMaterial.SetTexture("_OpacityMap", Texture("chuan song men_Opacity"));
        energyMaterial.SetColor("_Tint", new Color(.2f, .7f, 1.4f)); energyMaterial.SetFloat("_Charge", 1f);
        var glowing = new List<Renderer>();
        foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
        {
            renderer.gameObject.layer = LayerMask.NameToLayer("Surface");
            string original = renderer.sharedMaterial != null ? renderer.sharedMaterial.name : "";
            bool isEnergy = renderer == energy;
            Material chosen = isEnergy ? energyMaterial : renderer.name == "Sphere001" ? gem : original.Contains("Surface4") || original == "Portal Rune Stone" ? rune : stone;
            Undo.RecordObject(renderer, "Repair portal materials"); renderer.sharedMaterials = Enumerable.Repeat(chosen, renderer.sharedMaterials.Length).ToArray();
            foreach (var collider in renderer.GetComponents<Collider>()) Undo.DestroyObjectImmediate(collider);
            if (!isEnergy && renderer.TryGetComponent<MeshFilter>(out var filter) && filter.sharedMesh != null)
            {
                var collider = Undo.AddComponent<MeshCollider>(renderer.gameObject); collider.sharedMesh = filter.sharedMesh; collider.convex = false;
            }
            if (isEnergy) { renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; renderer.receiveShadows = false; }
            else if (chosen == rune || chosen == gem) glowing.Add(renderer);
        }
        var lightTransform = Child(anchor, "Portal Glow"); lightTransform.localPosition = new Vector3(0, 1.6f, .6f);
        var glow = Component<Light>(lightTransform.gameObject);
        glow.type = LightType.Point; glow.color = new Color(.2f, .65f, 1f); glow.intensity = 2f; glow.range = 5f; glow.shadows = LightShadows.None;
        gate.energySurfaces = new[] { energy }; gate.runeSurfaces = glowing.ToArray(); gate.glowLights = new[] { glow }; gate.SetCharge(1);
        EditorUtility.SetDirty(gate); return gate;
    }
    static Material Lit(string name, string prefix, bool emission, string backup)
    {
        var material = MaterialAt(name, Shader.Find("Universal Render Pipeline/Lit"));
        material.SetTexture("_BaseMap", Texture(prefix + "_Base_Color")); material.SetColor("_BaseColor", Color.white);
        material.SetFloat("_Smoothness", .18f); material.SetFloat("_Metallic", .06f);
        var normal = Texture(prefix + "_Normal_OpenGL");
        if (normal != null)
        {
            string path = AssetDatabase.GetAssetPath(normal); var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null && importer.textureType != TextureImporterType.NormalMap)
            {
                File.Copy(path + ".meta", backup + "/" + prefix.Replace(" ", "_") + "-normal.meta", true);
                importer.textureType = TextureImporterType.NormalMap; importer.SaveAndReimport();
            }
            material.SetTexture("_BumpMap", normal); material.EnableKeyword("_NORMALMAP");
        }
        material.SetTexture("_OcclusionMap", Texture(prefix + "_Mixed_AO"));
        if (emission) { material.SetTexture("_EmissionMap", Texture(prefix + "_Emissive")); material.SetColor("_EmissionColor", new Color(.1f, .7f, 1.3f) * 1.4f); material.EnableKeyword("_EMISSION"); }
        EditorUtility.SetDirty(material); return material;
    }
    static Texture2D Texture(string name) => AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/asset/model item/european-and-american-game-scencemagic-portal/source/low textures/" + name + ".png");
    static Material MaterialAt(string name, Shader shader)
    {
        string path = Generated + "/" + name + ".mat"; var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null) { material = new Material(shader) { name = name }; AssetDatabase.CreateAsset(material, path); }
        else material.shader = shader;
        return material;
    }
    static void BuildCanvas(MExplorationTutorial tutorial, Transform root)
    {
        var canvasObject = Rect(root, "Exploration Tutorial Canvas", new Vector2(.5f, .5f), Vector2.zero, new Vector2(1920, 1080)).gameObject;
        var canvas = Component<Canvas>(canvasObject); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 2600;
        var scaler = Component<CanvasScaler>(canvasObject); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
        if (canvasObject.GetComponent<GraphicRaycaster>() == null) canvasObject.AddComponent<GraphicRaycaster>();
        tutorial.tutorialCanvas = canvas;
        var font = TMP_Settings.defaultFontAsset;
        var existing = canvas.GetComponentInChildren<GuidedCombatTutorialView>(true);
        if (existing == null)
        {
            var template = Resources.Load<GuidedCombatTutorialView>("Combat/GuidedCombatTutorial");
            if (template == null) throw new InvalidOperationException("Missing existing guided combat tutorial prefab.");
            existing = UnityEngine.Object.Instantiate(template, canvas.transform, false); existing.name = "Guided Exploration Tutorial";
        }
        existing.Close(); tutorial.lessons = existing;
        tutorial.keyHints = new RectTransform[5];
        string[] keys = { "W A S D", "SPACE", "Q", "P", "E" };
        for (int i = 0; i < keys.Length; i++)
        {
            var rect = Rect(canvas.transform, "Controls/" + keys[i], new Vector2(0, 0), new Vector2(i == 0 ? 115 : 260 + (i - 1) * 112, 70), new Vector2(i == 0 ? 150 : 92, 52));
            var image = Component<Image>(rect.gameObject); image.color = new Color(.018f, .02f, .025f, .9f); image.raycastTarget = false;
            var text = Label(rect, "Key", keys[i], font, 25); Stretch(text.rectTransform); tutorial.keyHints[i] = rect; rect.gameObject.SetActive(false);
        }
        var focus = Rect(canvas.transform, "World Focus", new Vector2(.5f, .5f), Vector2.zero, new Vector2(245, 105));
        tutorial.worldFocus = focus; tutorial.worldFocusLabel = Label(focus, "Target", "Parkour", font, 25); Stretch(tutorial.worldFocusLabel.rectTransform); focus.gameObject.SetActive(false);
        var bars = Rect(canvas.transform, "Cinematic Bars", new Vector2(.5f, .5f), Vector2.zero, Vector2.zero); Stretch(bars);
        foreach (bool top in new[] { true, false })
        {
            var rect = Rect(bars, top ? "Top" : "Bottom", top ? new Vector2(.5f, 1) : new Vector2(.5f, 0), Vector2.zero, new Vector2(1920, 100));
            rect.pivot = top ? new Vector2(.5f, 1) : new Vector2(.5f, 0);
            var image = Component<Image>(rect.gameObject); image.color = Color.black; image.raycastTarget = false;
        }
        tutorial.cinematicBars = bars.gameObject; bars.gameObject.SetActive(false);
    }
    static Transform Child(Transform parent, string path)
    {
        foreach (string name in path.Split('/'))
        {
            var found = parent.Find(name); if (found == null) { found = new GameObject(name).transform; found.SetParent(parent, false); }
            parent = found;
        }
        return parent;
    }
    static Transform Pose(Transform parent, string name, Vector3 position, Quaternion rotation) { var t = Child(parent, name); t.SetPositionAndRotation(position, rotation); return t; }
    static Transform LookPose(Transform parent, string name, Vector3 position, Vector3 target) => Pose(parent, name, position, Quaternion.LookRotation(target - position));
    static void SetWalkCameras(MExplorationTutorial tutorial, Transform root)
    {
        Vector3 start = tutorial.walkStart.position, exit = tutorial.walkExit.position;
        Vector3 forward = tutorial.walkStart.forward, right = tutorial.walkStart.right;
        tutorial.walkCameraStart = LookPose(root, "Arrival Poses/Walk Camera Start", start + forward * 7.8f - right * 2.8f + Vector3.up * 2.4f, start + Vector3.up * 1.2f);
        tutorial.walkCameraEnd = LookPose(root, "Arrival Poses/Walk Camera End", exit + forward * 6.5f + right * 1.6f + Vector3.up * 2f, exit + Vector3.up * 1.25f);
    }
    static RectTransform Rect(Transform parent, string path, Vector2 anchor, Vector2 position, Vector2 size)
    {
        string[] pieces = path.Split('/');
        for (int i = 0; i < pieces.Length - 1; i++)
        { var container = Rect(parent, pieces[i], new Vector2(.5f, .5f), Vector2.zero, Vector2.zero); Stretch(container); parent = container; }
        string name = pieces[pieces.Length - 1]; var found = parent.Find(name);
        if (found != null && found is not RectTransform) { UnityEngine.Object.DestroyImmediate(found.gameObject); found = null; }
        var rect = found as RectTransform;
        if (rect == null) { rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); rect.SetParent(parent, false); }
        rect.anchorMin = rect.anchorMax = anchor; rect.pivot = new Vector2(.5f, .5f); rect.anchoredPosition = position; rect.sizeDelta = size; return rect;
    }
    static TMP_Text Label(Transform parent, string name, string text, TMP_FontAsset font, int size)
    {
        var rect = Rect(parent, name, new Vector2(.5f, .5f), Vector2.zero, Vector2.zero);
        TMP_Text label = Component<TextMeshProUGUI>(rect.gameObject);
        label.font = font; label.text = text; label.fontSize = size; label.color = new Color(.93f, .83f, .6f); label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false; return label;
    }
    static void Stretch(RectTransform rect) { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; }
    static T Component<T>(GameObject go) where T : Component => go.TryGetComponent<T>(out var component) ? component : go.AddComponent<T>();
    [MenuItem("Tools/Adventure/Tutorial/Validate m Arrival Tutorial")]
    public static void Validate()
    {
        var scene = SceneManager.GetSceneByPath("Assets/Scenes/m.unity");
        var tutorial = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MExplorationTutorial>(true)).Single();
        if (tutorial.player == null || tutorial.portal == null || tutorial.lessons == null || tutorial.campfire == null || tutorial.parkourObstacle == null)
            throw new InvalidOperationException("Tutorial references are incomplete.");
        MPortalTutorialValidation.Run(tutorial);
    }
    static void Finish()
    {
        var scene = SceneManager.GetSceneByPath("Assets/Scenes/m.unity");
        var tutorial = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MExplorationTutorial>(true)).Single();
        foreach (var hint in tutorial.keyHints) hint.gameObject.SetActive(false);
        SetWalkCameras(tutorial, tutorial.transform);
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); Validate();
    }
}

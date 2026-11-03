using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using LineworkLite.FreeOutline;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

// Explicit scene authoring. No Play Mode, encounter, campaign session or save files are used.
public static class CombatTargetSelectionSetup
{
    const string ScenePath = "Assets/Scenes/combattest.unity";
    const string RendererPath = "Assets/Settings/PC_Renderer.asset";
    const string ResultPath = "Library/CombatValidation/CombatTargetSelectionSetup.result.txt";
    const string PreviewPath = "Tools/CanvasUI/TargetSelectionPreview.png";
    const string RequestPath = "Library/CombatValidation/CombatTargetSelectionSetup.request";
    const string ValidateRequestPath = "Library/CombatValidation/CombatTargetSelectionValidate.request";

    [InitializeOnLoadMethod]
    static void Schedule()
    {
        if (File.Exists(RequestPath)) EditorApplication.delayCall += SetupRequested;
        if (File.Exists(ValidateRequestPath)) EditorApplication.delayCall += ValidateRequested;
    }

    static bool Busy => EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode;

    static void SetupRequested()
    {
        if (Busy) { EditorApplication.delayCall += SetupRequested; return; }
        if (!File.Exists(RequestPath)) return;
        File.Delete(RequestPath);
        Setup();
    }

    static void ValidateRequested()
    {
        if (Busy) { EditorApplication.delayCall += ValidateRequested; return; }
        if (!File.Exists(ValidateRequestPath)) return;
        File.Delete(ValidateRequestPath);
        ValidateOnly();
    }

    [MenuItem("Tools/Adventure/UI/Setup Target Selection")]
    public static void Setup() => Run(true);

    [MenuItem("Tools/Adventure/UI/Validate Target Selection")]
    public static void ValidateOnly() => Run(false);

    static void Run(bool setup)
    {
        Scene scene = default;
        bool opened = false;
        try
        {
            Check(!EditorApplication.isPlayingOrWillChangePlaymode, "Exit Play Mode before changing target selection.");
            scene = SceneManager.GetSceneByPath(ScenePath);
            if (setup) Check(!scene.isLoaded || !scene.isDirty, "Save the open combattest scene first; unsaved changes will not be overwritten.");
            string sceneHash = Hash(ScenePath);
            string rendererHash = Hash(RendererPath);
            string outlineHash = Hash("Assets/Free Outline Settings.asset");
            string backup = null;
            if (setup)
            {
                backup = "Tools/CanvasUI/BeforeTargetSelection-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff");
                Directory.CreateDirectory(backup);
                File.Copy(ScenePath, backup + "/combattest.unity");
                File.Copy(ScenePath + ".meta", backup + "/combattest.unity.meta");
            }
            if (!scene.isLoaded) { scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive); opened = true; }
            var combat = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<CombatManager>(true)).Single();
            var ui = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<AdvancedUIManager>(true)).Single();
            var canvas = ui.GetComponentInParent<Canvas>();
            Check(canvas != null, "Combat UI must have a Canvas.");
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            Check(renderer != null, "PC_Renderer is missing.");
            var feature = renderer.rendererFeatures.OfType<FreeOutline>().SingleOrDefault();
            Check(feature != null && feature.isActive, "Enable the existing Free Outline renderer feature first.");
            var enemySprite = LoadSprite("Assets/asset/Ui/target enemy.png");
            var playerSprite = LoadSprite("Assets/asset/Ui/target player.png");
            var target = combat.GetComponent<CombatTargetSelection>();
            if (setup)
            {
                if (target == null) target = combat.gameObject.AddComponent<CombatTargetSelection>();
                var serialized = new SerializedObject(target);
                Assign(serialized, "combat", combat);
                Assign(serialized, "canvas", canvas);
                Assign(serialized, "outlineFeature", feature);
                Assign(serialized, "enemyTargetSprite", enemySprite);
                Assign(serialized, "playerTargetSprite", playerSprite);
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            Check(target != null, "Run Setup Target Selection first.");
            ValidateReferences(target, combat, canvas, feature, enemySprite, playerSprite);
            ValidateRuntime(target);
            Check(rendererHash == Hash(RendererPath), "Target selection must preserve the renderer asset.");
            Check(outlineHash == Hash("Assets/Free Outline Settings.asset"), "Target selection must preserve authored Free Outline settings.");
            if (setup)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                Check(EditorSceneManager.SaveScene(scene), "Could not save target selection in combattest.");
            }
            else Check(sceneHash == Hash(ScenePath), "Validation must not write combattest.");
            WriteResult("PASS: target selection references; isolated enemy/player selection, simultaneous switch, fade/cleanup and reticle checks; actual URP red/blue silhouette pixels. Preview: " + PreviewPath + ". " +
                (setup ? "Scene updated. Backup: " + backup + ". " : "Validation only. ") + "No live battle or saves used; renderer/settings assets preserved.");
        }
        catch (Exception exception) { WriteResult("FAIL: " + exception, true); }
        finally { if (opened && scene.isLoaded) EditorSceneManager.CloseScene(scene, true); }
    }

    static Sprite LoadSprite(string path)
    {
        var sprite = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().FirstOrDefault();
        Check(sprite != null, "Import a Sprite at " + path);
        return sprite;
    }

    static void Assign(SerializedObject serialized, string name, UnityEngine.Object value)
    {
        var property = serialized.FindProperty(name);
        Check(property != null, "Missing target selection reference: " + name);
        property.objectReferenceValue = value;
    }

    static void ValidateReferences(CombatTargetSelection target, CombatManager combat, Canvas canvas, FreeOutline feature, Sprite enemy, Sprite player)
    {
        var serialized = new SerializedObject(target);
        Check(serialized.FindProperty("combat").objectReferenceValue == combat, "Wrong CombatManager reference.");
        Check(serialized.FindProperty("canvas").objectReferenceValue == canvas, "Wrong Canvas reference.");
        Check(serialized.FindProperty("outlineFeature").objectReferenceValue == feature, "Wrong Free Outline feature reference.");
        Check(serialized.FindProperty("enemyTargetSprite").objectReferenceValue == enemy, "Wrong enemy target sprite.");
        Check(serialized.FindProperty("playerTargetSprite").objectReferenceValue == player, "Wrong player target sprite.");
    }

    static void ValidateRuntime(CombatTargetSelection source)
    {
        Scene preview = EditorSceneManager.NewPreviewScene();
        FreeOutline feature = null;
        FreeOutlineSettings settings = null;
        CombatTargetSelection view = null;
        try
        {
            var root = new GameObject("Isolated target selection");
            SceneManager.MoveGameObjectToScene(root, preview);
            view = root.AddComponent<CombatTargetSelection>();
            EditorUtility.CopySerialized(source, view);
            view.enabled = false;
            view.combat = null;
            view.enemyStyle = new CombatTargetStyle { outlineColor = Color.red };
            view.playerStyle = new CombatTargetStyle { outlineColor = Color.blue };
            view.enterDuration = .1f;
            view.exitDuration = .16f;
            view.markerEnterDuration = .28f;
            view.enterScalePeak = 1.45f;
            view.rotationSpeed = 45f;
            view.showMarker = true;
            var uiRoot = new GameObject("Isolated Canvas", typeof(RectTransform), typeof(Canvas));
            uiRoot.transform.SetParent(root.transform, false);
            view.canvas = uiRoot.GetComponent<Canvas>();
            view.canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var cameraObject = new GameObject("Isolated target camera", typeof(Camera));
            cameraObject.transform.SetParent(root.transform, false);
            cameraObject.transform.position = new Vector3(0f, 2f, -10f);
            cameraObject.transform.LookAt(Vector3.zero);
            view.targetCamera = cameraObject.GetComponent<Camera>();
            view.targetCamera.enabled = false;
            settings = ScriptableObject.CreateInstance<FreeOutlineSettings>();
            settings.hideFlags = HideFlags.HideAndDontSave;
            feature = ScriptableObject.CreateInstance<FreeOutline>();
            feature.hideFlags = HideFlags.HideAndDontSave;
            feature.SetSettings(settings);
            view.outlineFeature = feature;
            Check(view.Initialize(), "Could not initialize target selection with the installed Free Outline package.");
            Check(feature.Settings != settings, "Runtime settings must be isolated from the authored outline asset.");
            Check(settings.Outlines.Count == 0, "Initializing selection must not add profiles to authored settings.");

            var enemyA = MakeUnit(root.transform, "Enemy A", false, new Vector3(-2f, 0f, 0f));
            var enemyB = MakeUnit(root.transform, "Enemy B", false, new Vector3(2f, 0f, 0f));
            var player = MakeUnit(root.transform, "Player", true, new Vector3(0f, 0f, 2f));
            var rendererA = enemyA.GetComponent<Renderer>();
            var rendererB = enemyB.GetComponent<Renderer>();
            var rendererPlayer = player.GetComponent<Renderer>();
            rendererA.renderingLayerMask = 1u;
            rendererB.renderingLayerMask = 2u;
            rendererPlayer.renderingLayerMask = 4u;
            ValidateSelectionPredicate(root.transform, player, enemyA);
            var effect = new GameObject("Particle visual", typeof(ParticleSystem));
            effect.transform.SetParent(enemyA.transform, false);
            var effectRenderer = effect.GetComponent<ParticleSystemRenderer>();
            uint originalEffectMask = effectRenderer.renderingLayerMask;
            var nestedUnit = MakeUnit(enemyA.transform, "Separate attached unit", false, Vector3.zero);
            var nestedRenderer = nestedUnit.GetComponent<Renderer>();
            uint nestedMask = nestedRenderer.renderingLayerMask;

            view.SelectTarget(enemyA);
            Check(view.CurrentTarget == enemyA && view.ActiveVisualCount == 1, "Selecting an enemy must open one target visual.");
            Check(view.GetOutline(enemyA) != null && view.GetMarker(enemyA) != null, "Selection requires outline and center marker.");
            Check(rendererA.renderingLayerMask != 1u && rendererB.renderingLayerMask == 2u, "Only the selected character receives an outline layer.");
            Check(effectRenderer.renderingLayerMask == originalEffectMask, "Particle effects must not become part of the character outline.");
            Check(nestedRenderer.renderingLayerMask == nestedMask, "A separate nested BattleUnit must not inherit the parent's selected outline.");
            var marker = view.GetMarker(enemyA);
            view.Advance(.10f);
            float earlyScale = marker.localScale.x;
            view.Advance(.50f);
            float settledScale = marker.localScale.x;
            Check(marker.gameObject.activeSelf && earlyScale > settledScale && Mathf.Abs(settledScale - 1f) < .001f,
                "Target marker must grow and settle back to its original size.");
            float rotation = marker.localEulerAngles.z;
            view.Advance(.1f);
            Check(Mathf.Abs(Mathf.DeltaAngle(rotation, marker.localEulerAngles.z)) > .01f, "Selected target marker must rotate.");
            var enemyColor = view.GetOutline(enemyA).color;
            Check(enemyColor.r > enemyColor.b, "Enemy outline must use the red style.");

            view.SelectTarget(enemyB);
            Check(view.CurrentTarget == enemyB && view.ActiveVisualCount == 2, "Switching target must overlap the old fade and new entrance.");
            Check(view.GetOutline(enemyA) != null && view.GetOutline(enemyB) != null, "Old and new enemies need independent outline profiles.");
            view.Advance(Mathf.Max(.001f, view.exitDuration * .5f));
            Check(view.GetOutline(enemyA).color.a < .99f && view.GetOutline(enemyA).color.a > .01f,
                "Outgoing outline must fade rather than remain fully bright until removal.");
            Check(view.GetOutline(enemyB).color.a > .99f, "Entering target must stay visible while the previous target fades.");
            view.Advance(1f);
            Check(view.ActiveVisualCount == 1 && rendererA.renderingLayerMask == 1u, "After fade, the previous enemy must regain its original layer mask.");

            view.SelectTarget(player);
            view.Advance(.5f);
            Check(view.CurrentTarget == player && view.ActiveVisualCount == 1, "Friendly selection must replace enemy selection.");
            var playerColor = view.GetOutline(player).color;
            Check(playerColor.b > playerColor.r, "Player outline must use the blue style.");
            Check(rendererB.renderingLayerMask == 2u, "Previous enemy layer must be restored when switching to a player.");
            SetHealth(player, 0);
            view.Advance(1f);
            view.Advance(1f);
            Check(view.CurrentTarget == null && view.ActiveVisualCount == 0, "A dead target must release its marker and outline.");
            Check(rendererPlayer.renderingLayerMask == 4u, "Dead target layer must be restored.");

            SetHealth(player, 100);
            view.SelectTarget(enemyA);
            view.SelectTarget(enemyB);
            view.SelectTarget(player);
            view.SelectTarget(enemyA);
            view.ClearSelection(true);
            Check(view.CurrentTarget == null && view.ActiveVisualCount == 0, "Cancel must clear every visual even after rapid switches.");
            Check(rendererA.renderingLayerMask == 1u && rendererB.renderingLayerMask == 2u && rendererPlayer.renderingLayerMask == 4u,
                "Cancel must restore all original rendering layers.");
            view.SelectTarget(enemyA);
            rendererA.renderingLayerMask |= 1u << 10;
            view.Shutdown();
            Check(rendererA.renderingLayerMask == (1u | (1u << 10)), "Cleanup must preserve unrelated rendering-layer changes.");
            Check(feature.Settings == settings && settings.Outlines.Count == 0, "Shutdown must restore the original feature settings.");
            ValidateRender(source, view, enemyA, enemyB, player, nestedUnit, view.targetCamera);
        }
        finally
        {
            if (view != null) view.Shutdown();
            EditorSceneManager.ClosePreviewScene(preview);
            if (feature != null) UnityEngine.Object.DestroyImmediate(feature);
            if (settings != null) UnityEngine.Object.DestroyImmediate(settings);
        }
    }

    static BattleUnit MakeUnit(Transform parent, string name, bool isPlayer, Vector3 position)
    {
        var root = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        root.name = name;
        root.transform.SetParent(parent, false);
        root.transform.localPosition = position;
        var unit = root.AddComponent<BattleUnit>();
        unit.isPlayer = isPlayer;
        unit.maxHP = 100;
        SetHealth(unit, 100);
        return unit;
    }

    static void SetHealth(BattleUnit unit, int health) => typeof(BattleUnit).GetProperty("currentHP", BindingFlags.Instance | BindingFlags.Public)
        .SetValue(unit, health);

    static void ValidateSelectionPredicate(Transform parent, BattleUnit player, BattleUnit enemy)
    {
        var holder = new GameObject("Isolated combat selection state");
        holder.transform.SetParent(parent, false);
        CombatManager original = CombatManager.Instance;
        var combat = holder.AddComponent<CombatManager>();
        ActionData action = ScriptableObject.CreateInstance<ActionData>();
        try
        {
            combat.state = CombatState.PlayerTurn;
            combat.currentActiveUnit = player;
            combat.currentTarget = enemy;
            Check(!combat.IsChoosingTarget, "Idle player turns must not highlight a remembered target.");
            var selection = typeof(CombatManager).GetField("selectedAction", BindingFlags.Instance | BindingFlags.NonPublic);
            var confirmed = typeof(CombatManager).GetField("targetSelectionConfirmed", BindingFlags.Instance | BindingFlags.NonPublic);
            Check(selection != null && confirmed != null, "Combat selection state fields are missing.");
            selection.SetValue(combat, action);
            Check(combat.IsChoosingTarget, "A pending player action must expose the target selection state.");
            confirmed.SetValue(combat, true);
            Check(!combat.IsChoosingTarget, "Confirming a target must immediately end its selection highlight.");
            confirmed.SetValue(combat, false);
            combat.state = CombatState.Executing;
            Check(!combat.IsChoosingTarget, "Executing an attack must not keep selection active.");
            combat.state = CombatState.EnemyTurn;
            Check(!combat.IsChoosingTarget, "Enemy turns must not activate the player's targeting UI.");
            combat.state = CombatState.PlayerTurn;
            combat.enabled = false;
            Check(!combat.IsChoosingTarget, "A disabled combat manager must release target selection.");
        }
        finally
        {
            if (CombatManager.Instance == combat) CombatManager.Instance = original;
            UnityEngine.Object.DestroyImmediate(action);
            UnityEngine.Object.DestroyImmediate(holder);
        }
    }

    static void ValidateRender(CombatTargetSelection source, CombatTargetSelection view, BattleUnit enemy, BattleUnit unusedEnemy,
        BattleUnit player, BattleUnit nestedUnit, Camera camera)
    {
        FreeOutlineSettings original = source.outlineFeature.Settings;
        Material body = null;
        RenderTexture texture = null;
        Texture2D pixels = null;
        RenderTexture prior = RenderTexture.active;
        try
        {
            unusedEnemy.gameObject.SetActive(false);
            nestedUnit.gameObject.SetActive(false);
            foreach (var particles in enemy.GetComponentsInChildren<ParticleSystem>(true)) particles.gameObject.SetActive(false);
            enemy.transform.position = new Vector3(-1.6f, 0f, 0f);
            player.transform.position = new Vector3(1.6f, 0f, 0f);
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            Check(shader != null, "URP Unlit is required for an isolated silhouette render.");
            body = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            body.SetColor("_BaseColor", new Color(.32f, .32f, .32f));
            enemy.GetComponent<Renderer>().sharedMaterial = body;
            player.GetComponent<Renderer>().sharedMaterial = body;
            camera.transform.position = new Vector3(0f, 0f, -10f);
            camera.transform.rotation = Quaternion.identity;
            camera.orthographic = true;
            camera.cameraType = CameraType.Game;
            camera.orthographicSize = 2.2f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.scene = enemy.gameObject.scene;
            camera.overrideSceneCullingMask = EditorSceneManager.GetSceneCullingMask(enemy.gameObject.scene);
            camera.cullingMask = ~0;
            texture = new RenderTexture(900, 600, 24, RenderTextureFormat.ARGB32) { hideFlags = HideFlags.HideAndDontSave };
            texture.Create();
            camera.targetTexture = texture;
            var cameraData = camera.GetUniversalAdditionalCameraData();
            cameraData.SetRenderer(0);
            view.canvas.renderMode = RenderMode.ScreenSpaceCamera;
            view.canvas.worldCamera = camera;
            view.canvas.planeDistance = 1f;
            view.outlineFeature = source.outlineFeature;
            view.showMarker = false;
            Check(view.Initialize(), "Could not bind the active renderer feature for the render check.");
            view.SelectTarget(enemy);
            view.Advance(.3f);
            view.SelectTarget(player);
            view.Advance(.025f);
            Canvas.ForceUpdateCanvases();
            RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = texture });
            RenderTexture.active = texture;
            pixels = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
            pixels.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
            pixels.Apply();
            Directory.CreateDirectory(Path.GetDirectoryName(PreviewPath));
            File.WriteAllBytes(PreviewPath, pixels.EncodeToPNG());
            File.WriteAllText("Library/CombatValidation/CombatTargetRenderDiagnostics.txt", RenderDiagnostics(view, camera, cameraData));
            int red = 0, blue = 0, gray = 0;
            foreach (Color32 pixel in pixels.GetPixels32())
            {
                if (pixel.r > 45 && pixel.r > pixel.g * 1.5f && pixel.r > pixel.b * 1.5f) red++;
                if (pixel.b > 45 && pixel.b > pixel.r * 1.5f && pixel.b > pixel.g * 1.2f) blue++;
                if (pixel.r > 40 && Math.Abs(pixel.r - pixel.g) < 5 && Math.Abs(pixel.r - pixel.b) < 5) gray++;
            }
            Check(gray > 1000, "Isolated URP preview is blank: grayscale character meshes were not rendered.");
            Check(red > 20 && blue > 20, "Actual Free Outline pass did not render both target colors (red=" + red + ", blue=" + blue + ").");
            view.showMarker = true;
            view.Advance(0f);
            Canvas.ForceUpdateCanvases();
            foreach (var graphic in view.canvas.GetComponentsInChildren<UnityEngine.UI.Graphic>(true))
                graphic.Rebuild(UnityEngine.UI.CanvasUpdate.PreRender);
            RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = texture });
            RenderTexture.active = texture;
            pixels.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
            pixels.Apply();
            Directory.CreateDirectory(Path.GetDirectoryName(PreviewPath));
            File.WriteAllBytes(PreviewPath, pixels.EncodeToPNG());
        }
        finally
        {
            view.Shutdown();
            Check(source.outlineFeature.Settings == original, "Render validation must restore the real renderer feature settings.");
            RenderTexture.active = prior;
            camera.targetTexture = null;
            if (texture != null) { texture.Release(); UnityEngine.Object.DestroyImmediate(texture); }
            if (pixels != null) UnityEngine.Object.DestroyImmediate(pixels);
            if (body != null) UnityEngine.Object.DestroyImmediate(body);
        }
    }

    static string RenderDiagnostics(CombatTargetSelection view, Camera camera, UniversalAdditionalCameraData cameraData)
    {
        var text = new StringBuilder();
        text.AppendLine("Active pipeline: " + GraphicsSettings.currentRenderPipeline);
        text.AppendLine("Quality pipeline: " + QualitySettings.renderPipeline);
        text.AppendLine("Camera type: " + camera.cameraType + "; renderer: " + cameraData.scriptableRenderer);
        text.AppendLine("Compatibility mode: " + GraphicsSettings.GetRenderPipelineSettings<RenderGraphSettings>().enableRenderCompatibilityMode);
        text.AppendLine("Feature: " + view.outlineFeature + "; active=" + view.outlineFeature.isActive + "; settings=" + view.outlineFeature.Settings);
        var features = typeof(ScriptableRenderer).GetField("m_RendererFeatures", BindingFlags.Instance | BindingFlags.NonPublic)
            .GetValue(cameraData.scriptableRenderer) as System.Collections.IEnumerable;
        foreach (var feature in features) text.AppendLine("Renderer feature: " + feature + "; same=" + ReferenceEquals(feature, view.outlineFeature));
        foreach (var outline in view.outlineFeature.Settings.Outlines)
            text.AppendLine("Outline: " + outline.name + "; active=" + outline.IsActive() + "; layer=" + ((uint)outline.RenderingLayer) +
                "; width=" + outline.width + "; color=" + outline.color + "; material=" + outline.material + "; shader=" + outline.material?.shader);
        foreach (var renderer in view.targetCamera.scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Renderer>(true)))
            text.AppendLine("Renderer " + renderer.name + "; active=" + renderer.gameObject.activeInHierarchy + "; enabled=" + renderer.enabled + "; layer=" + renderer.renderingLayerMask);
        return text.ToString();
    }

    static string Hash(string path)
    {
        using (var hash = SHA256.Create()) return Convert.ToBase64String(hash.ComputeHash(File.ReadAllBytes(path)));
    }

    static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }

    static void WriteResult(string text, bool error = false)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ResultPath));
        File.WriteAllText(ResultPath, text);
        if (error) Debug.LogError(text); else Debug.Log(text);
    }
}

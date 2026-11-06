using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Isolated preview only: no game saves, scene writes, map loads or live session required.
[InitializeOnLoad]
public static class FastTravelPresentationValidation
{
    const string Request = "Library/CombatValidation/FastTravelPresentation.request";
    const string Result = "Library/CombatValidation/FastTravelPresentation.result.txt";
    static double nextCheck;
    static FastTravelPresentationValidation() { EditorApplication.update += CheckRequest; }

    static void CheckRequest()
    {
        if (EditorApplication.timeSinceStartup < nextCheck) return;
        nextCheck = EditorApplication.timeSinceStartup + 1;
        if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Delete(Request); Validate();
    }

    [MenuItem("Tools/Adventure/UI/Validate Fast Travel Presentation")]
    public static void Validate()
    {
        var preview = EditorSceneManager.NewPreviewScene();
        float previousScale = Time.timeScale;
        Camera camera = null;
        RenderTexture render = null;
        Texture2D image = null;
        try
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Adventure/UI/AdventureCanvas.prefab");
            Check(prefab != null, "AdventureCanvas prefab missing.");
            var canvasGo = (GameObject)PrefabUtility.InstantiatePrefab(prefab, preview);
            var root = canvasGo.GetComponent<AdventureCanvasRoot>();
            Check(root != null, "AdventureCanvasRoot missing.");
            canvasGo.SetActive(true);
            root.transform.Find("HUD").gameObject.SetActive(false);
            root.transform.Find("Toast")?.gameObject.SetActive(false);
            var menu = root.transform.Find("Menu"); menu.gameObject.SetActive(true);
            foreach (Transform child in menu)
                if (child.name != "Rest") child.gameObject.SetActive(false);
            menu.Find("Rest").gameObject.SetActive(true);
            GameUITheme.Apply(canvasGo);

            var controllerGo = new GameObject("Isolated Fast Travel Controller", typeof(AdventureUI));
            SceneManager.MoveGameObjectToScene(controllerGo, preview);
            var controller = controllerGo.GetComponent<AdventureUI>();
            Set(controller, "view", root);
            Call(controller, "BuildFastTravelUI");
            var points = new List<UnlockedRestPoint>
            {
                new UnlockedRestPoint { id = "world:current", displayName = "Điểm nghỉ hiện tại", sceneName = "mapgame", position = new Vector3(10, 2, 10) },
                new UnlockedRestPoint { id = "world:forest", displayName = "Trại rừng đã mở khóa", sceneName = "mapgame", position = new Vector3(50, 2, 10) },
                new UnlockedRestPoint { id = "world:mountain", displayName = "Trại núi đã mở khóa", sceneName = "m", position = new Vector3(10, 4, 80) }
            };
            Time.timeScale = 0;
            Call(controller, "PopulateTravelDestinations", points, "world:current");
            var panel = Get<GameObject>(controller, "travelPanel");
            var dialog = Get<GameObject>(controller, "travelDialog");
            var content = Get<RectTransform>(controller, "travelContent");
            panel.SetActive(true); dialog.SetActive(true);
            Canvas.ForceUpdateCanvases(); LayoutRebuilder.ForceRebuildLayoutImmediate(content); Canvas.ForceUpdateCanvases();
            var rows = content.GetComponentsInChildren<Button>(true).Where(b => b.transform.parent == content && b.gameObject.activeSelf).ToArray();
            Check(rows.Length == 3, "All unlocked points, including another map, must have a destination row.");
            Check(rows.Count(b => b.interactable) == 2, "Only the current camp must be disabled.");
            Check(content.rect.height >= 3 * 48, "Fast travel list content must have visible row height.");
            foreach (var point in points)
                Check(rows.Any(row => row.GetComponentsInChildren<TMP_Text>(true).Any(t => t.text.Contains(point.displayName))), "Missing destination label: " + point.displayName);

            var buttons = new List<Button> { menu.Find("Rest/FastTravel").GetComponent<Button>() };
            buttons.AddRange(panel.GetComponentsInChildren<Button>(true));
            buttons.AddRange(dialog.GetComponentsInChildren<Button>(true));
            foreach (var button in buttons.Distinct())
            {
                Check(button.GetComponent<SmoothButtonHoverScale>() != null && button.GetComponent<SmoothButtonHoverScale>().enabled,
                    button.name + " needs SmoothButtonHoverScale.");
                int originalScaleId = button.GetComponent<SmoothButtonHoverScale>().GetInstanceID();
                for (int pass = 0; pass < 3; pass++) GameUITheme.Apply(button.gameObject);
                Check(button.GetComponent<SmoothButtonHoverScale>() != null && button.GetComponent<SmoothButtonHoverScale>().GetInstanceID() == originalScaleId,
                    "Repeated theme application must preserve the hover scaler: " + button.name);
                Check(!button.GetComponents<DynamicButtonUI>().Any(motion => motion.enabled), "Fast travel must not use scaled-time entrance animation: " + button.name);
                foreach (var group in button.GetComponentsInParent<CanvasGroup>(true))
                    Check(group.alpha > .99f, "Fast travel button must remain visible at TimeScale 0: " + button.name);
                foreach (var group in button.GetComponentsInChildren<CanvasGroup>(true))
                    Check(group.alpha > .99f, "Fast travel button content must remain visible at TimeScale 0: " + button.name);
            }
            Canvas.ForceUpdateCanvases(); LayoutRebuilder.ForceRebuildLayoutImmediate(content); Canvas.ForceUpdateCanvases();
            foreach (var row in rows)
            {
                var label = row.GetComponentInChildren<TMP_Text>(true);
                var background = row.transform.Find("Skill Background Plate") as RectTransform;
                Check(label != null && background != null, "Destination needs text inside the skill plate.");
                Bounds labelBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(background, label.rectTransform);
                Rect plateRect = background.rect;
                Check(labelBounds.min.x >= plateRect.xMin - 1 && labelBounds.max.x <= plateRect.xMax + 1 &&
                    labelBounds.min.y >= plateRect.yMin - 1 && labelBounds.max.y <= plateRect.yMax + 1,
                    "Destination label must stay inside its background.");
            }

            dialog.SetActive(false);
            var cameraGo = new GameObject("Fast Travel Preview Camera", typeof(Camera));
            SceneManager.MoveGameObjectToScene(cameraGo, preview); camera = cameraGo.GetComponent<Camera>();
            camera.scene = preview; camera.overrideSceneCullingMask = EditorSceneManager.GetSceneCullingMask(preview);
            camera.transform.position = new Vector3(0, 0, -10); camera.orthographic = true; camera.orthographicSize = 360;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.1f, .12f, .14f);
            var canvas = canvasGo.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
            render = new RenderTexture(1280, 720, 24); render.Create(); camera.targetTexture = render;
            Canvas.ForceUpdateCanvases(); LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            foreach (var graphic in canvasGo.GetComponentsInChildren<Graphic>(true)) graphic.SetAllDirty();
            foreach (var text in canvasGo.GetComponentsInChildren<TMP_Text>(true)) if (text.gameObject.activeInHierarchy) text.ForceMeshUpdate(true);
            Canvas.ForceUpdateCanvases(); camera.Render();
            RenderTexture previous = RenderTexture.active;
            string previewPath = "Library/CombatValidation/FastTravel-" + DateTime.Now.ToString("yyyyMMdd-HHmmssfff") + ".png";
            try
            {
                RenderTexture.active = render; image = new Texture2D(render.width, render.height, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, render.width, render.height), 0, 0); image.Apply();
                Directory.CreateDirectory(Path.GetDirectoryName(previewPath)); File.WriteAllBytes(previewPath, image.EncodeToPNG());
            }
            finally { RenderTexture.active = previous; }
            File.WriteAllText(Result, "PASS: three unlocked camp rows (including other map), current-only disabled, visible content at TimeScale 0, persistent SmoothButtonHoverScale on entry/destinations/back/yes/no, no DynamicButtonUI, labels contained. Preview: " + previewPath);
        }
        catch (Exception error) { Directory.CreateDirectory(Path.GetDirectoryName(Result)); File.WriteAllText(Result, "FAIL: " + error); Debug.LogException(error); }
        finally
        {
            Time.timeScale = previousScale;
            if (camera != null) camera.targetTexture = null;
            if (image != null) UnityEngine.Object.DestroyImmediate(image);
            if (render != null) { render.Release(); UnityEngine.Object.DestroyImmediate(render); }
            EditorSceneManager.ClosePreviewScene(preview);
        }
    }
    static T Get<T>(object source, string name) => (T)source.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(source);
    static void Set(object source, string name, object value) => source.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(source, value);
    static void Call(object source, string name, params object[] args)
    {
        var method = source.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);
        Check(method != null, "Missing method: " + name); method.Invoke(source, args);
    }
    static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}

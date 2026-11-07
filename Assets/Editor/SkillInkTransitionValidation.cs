using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if DOTWEEN
using DG.Tweening;
#endif

/// <summary>Isolated checks only: no live scenes, combat state, prefabs or saves are modified.</summary>
public static class SkillInkTransitionValidation
{
    const string ResultPath = "Library/CombatValidation/SkillInkTransition.result.txt";
    const string PreviewFolder = "Library/CombatValidation/SkillInkPreviews";
    const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    [MenuItem("Tools/Adventure/UI/Validate Skill Ink Transition")]
    public static void Run() => Validate(false);
    public static void RunBatch() => Validate(true);

    [InitializeOnLoadMethod]
    static void RunExplicitRequestAfterCompilation()
    {
        const string request = "Library/CombatValidation/SkillInkTransition.request";
        if (!File.Exists(request)) return;
        EditorApplication.delayCall += () =>
        {
            if (!File.Exists(request)) return;
            File.Delete(request);
            Run();
        };
    }

    static void Validate(bool batch)
    {
        Scene preview = default;
        RenderTexture target = null;
        SkillPanelTransitionController controller = null;
        var results = new List<string>();
        try
        {
            Check(!EditorApplication.isPlayingOrWillChangePlaymode, "Run validation in Edit Mode.");
            Directory.CreateDirectory(Path.GetDirectoryName(ResultPath));
            Directory.CreateDirectory(PreviewFolder);
            preview = EditorSceneManager.NewPreviewScene();
            var canvasObject = new GameObject("Skill Ink Isolated Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            SceneManager.MoveGameObjectToScene(canvasObject, preview);
            canvasObject.layer = 5;
            var canvas = canvasObject.GetComponent<Canvas>();
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600f, 900f);
            scaler.matchWidthOrHeight = .5f;
            var cameraObject = new GameObject("Skill Ink Isolated Camera", typeof(Camera));
            SceneManager.MoveGameObjectToScene(cameraObject, preview);
            var camera = cameraObject.GetComponent<Camera>();
            camera.scene = preview;
            camera.overrideSceneCullingMask = EditorSceneManager.GetSceneCullingMask(preview);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.23f, .21f, .19f);
            camera.cullingMask = 1 << 5;
            camera.orthographic = true;
            camera.orthographicSize = 450f;
            camera.transform.position = new Vector3(0f, 0f, -20f);
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 10f;

            var commands = Rect("Commands", canvas.transform, new Vector2(.16f, .5f), new Vector2(340f, 100f));
            var source = Rect("Skills Button", commands, new Vector2(.5f, .5f), new Vector2(320f, 80f));
            var sourceImage = source.gameObject.AddComponent<Image>();
            sourceImage.color = new Color(.035f, .035f, .035f);
            var sourceButton = source.gameObject.AddComponent<Button>();
            sourceButton.targetGraphic = sourceImage;
            Label("Skills", source, "SKILLS", Vector2.zero, Vector2.one, 32f);
            source.localRotation = Quaternion.Euler(0f, 0f, -6f);
            var panel = Rect("Authored Skills Panel", canvas.transform, new Vector2(.66f, .5f), new Vector2(760f, 610f));
            panel.localRotation = Quaternion.Euler(0f, 0f, -4f);
            // A muted blue diagnostic surface makes hidden/revealed pixels measurable.
            // It is created only in this preview scene, never in the game Canvas.
            var panelImage = panel.gameObject.AddComponent<Image>();
            panelImage.color = new Color(.035f, .16f, .19f, 1f);
            panelImage.raycastTarget = false;
            var title = Label("Title", panel, "SKILLS / INK REVEAL CHECK", new Vector2(.05f, .90f), new Vector2(.95f, .98f), 26f);
            var rows = Stretch("Rows", panel, new Vector2(.035f, .28f), new Vector2(.965f, .88f));
            var rowRects = new List<RectTransform>();
            for (int i = 0; i < 5; i++)
            {
                var row = Stretch("Skill " + (i + 1), rows, new Vector2(0f, 1f - (i + 1) / 5f), new Vector2(1f, 1f - i / 5f));
                row.offsetMin = new Vector2(0f, 4f);
                row.offsetMax = new Vector2(0f, -4f);
                var background = row.gameObject.AddComponent<Image>();
                background.color = new Color(.015f, .02f, .025f, .8f);
                var button = row.gameObject.AddComponent<Button>();
                button.targetGraphic = background;
                // An unavailable row must not become usable during/after the reveal.
                button.interactable = i != 2;
                Label("Name", row, i == 4 ? "ULTIMATE" : "Skill item " + (i + 1), new Vector2(.16f, 0f), new Vector2(.76f, 1f), 25f);
                Label("Cost", row, (i + 2).ToString(), new Vector2(.8f, 0f), new Vector2(.95f, 1f), 26f);
                var icon = Stretch("Icon", row, new Vector2(.025f, .20f), new Vector2(.12f, .80f));
                icon.gameObject.AddComponent<Image>().color = new Color(.75f, .65f, .45f);
                rowRects.Add(row);
            }
            Label("Description", panel, "A cumulative brush edge reveals every icon and glyph together.\nThe layout and text scale stay unchanged.",
                new Vector2(.06f, .035f), new Vector2(.94f, .23f), 23f);

            var host = Rect("Skill Transition", canvas.transform, Vector2.zero, Vector2.zero);
            controller = host.gameObject.AddComponent<SkillPanelTransitionController>();
            controller.skillsButton = sourceButton;
            controller.skillsPanel = panel;
            controller.skillItemsRoot = rows;
            controller.commandMenu = commands;
            controller.brushReveal = host.gameObject.AddComponent<BrushRevealController>();
            target = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32);
            camera.targetTexture = target;
            Layout(canvas, scaler);
            var titleBefore = WorldCorners(title.rectTransform);
            var panelBefore = WorldCorners(panel);
            Check(controller.Initialize(), "Controller failed to initialize with existing Canvas references.");
            Check(controller.brushReveal.MaskRoot.GetComponent<CanvasRenderer>() != null &&
                controller.brushReveal.MaskRoot.GetComponent<InkRevealGraphic>().canvasRenderer != null,
                "The generated BrushMask needs a live CanvasRenderer before SoftMask can be enabled.");
            Layout(canvas, scaler);
            Check(SameCorners(titleBefore, WorldCorners(title.rectTransform)) && SameCorners(panelBefore, WorldCorners(panel)),
                "Mask initialization moved/scaled the authored panel or text.");
            results.Add("PASS: initialization preserves authored panel/text world corners, angle and layout.");

#if DOTWEEN
            CheckTimeline(controller, commands, panel, rowRects, results);
            commands.gameObject.SetActive(true);
            controller.OpenSkillPanel();
            foreach (float time in new[] { .0f, .16f, .30f, .48f })
            {
                if (time < .48f) DOTween.Goto(controller, time, false);
                else DOTween.Complete(controller);
                Capture(camera, canvas, scaler, Path.Combine(PreviewFolder, "SkillInkTimeline-" + Mathf.RoundToInt(time * 1000f) + "ms.png"));
            }
            controller.ResetTransition();
            CheckItems(controller, commands, camera, canvas, scaler, results);
#else
            results.Add("SKIP: Editor timeline sampling requires DOTween; the coroutine fallback must be checked in Play Mode.");
#endif
            panel.gameObject.SetActive(true);
            commands.gameObject.SetActive(false);
            foreach (var animator in panel.GetComponentsInChildren<SkillItemAnimator>(true)) animator.Restore();
            var brush = controller.brushReveal;
            foreach (var resolution in new[] { new Vector2Int(1280, 720), new Vector2Int(960, 720), new Vector2Int(1680, 720) })
            {
                camera.targetTexture = null;
                target.Release();
                UnityEngine.Object.DestroyImmediate(target);
                target = new RenderTexture(resolution.x, resolution.y, 24, RenderTextureFormat.ARGB32);
                camera.targetTexture = target;
                Layout(canvas, scaler);
                var fixedTitle = WorldCorners(title.rectTransform);
                bool[] previousCoverage = null;
                int previousCount = -1;
                foreach (float progress in new[] { 0f, .25f, .5f, 1f })
                {
                    brush.SetProgress(progress);
                    brush.SetBrushOpacity(0f);
                    string path = Path.Combine(PreviewFolder, "SkillInkMask-" + resolution.x + "x" + resolution.y + "-" + Mathf.RoundToInt(progress * 100f) + ".png");
                    var pixels = Capture(camera, canvas, scaler, path);
                    bool[] coverage = pixels.Select(IsDiagnosticSurface).ToArray();
                    int count = coverage.Count(value => value);
                    Check(SameCorners(fixedTitle, WorldCorners(title.rectTransform)), "Revealing the mask altered a child's authored world-space geometry.");
                    if (progress == 0f) Check(count < 10, "Panel surface leaks through a fully closed mask at " + resolution + ": " + count + " pixels.");
                    else Check(count > previousCount + 150, "The reveal did not increase visible panel coverage at " + progress + " / " + resolution + ". Counts " + previousCount + " → " + count);
                    if (previousCoverage != null)
                    {
                        int lost = 0;
                        for (int i = 0; i < coverage.Length; i++) if (previousCoverage[i] && !coverage[i]) lost++;
                        Check(lost < 20, "Already revealed content became hidden again (" + lost + " pixels), so the wipe is not cumulative.");
                    }
                    if (progress == 1f)
                    {
                        int bright = pixels.Count(p => p.r > 190 && p.g > 190 && p.b > 180);
                        Check(bright > 100, "Full reveal rendered no visible text/icons.");
                    }
                    previousCoverage = coverage;
                    previousCount = count;
                }
                results.Add("PASS: cumulative 0/25/50/100% coverage, no re-hiding, visible TMP/icons, unchanged child geometry at " + resolution.x + "×" + resolution.y + ".");
            }
            // The combat panel is mostly transparent. Measure the visible ink by itself,
            // so a tiny mark cannot pass on the strength of the mask's diagnostic fill.
            panel.gameObject.SetActive(false);
            CheckFullHeightInk(brush, camera, canvas, scaler);
            results.Add("PASS: the visible ink spans the panel height and sweeps right to left at 20/50/80% on a transparent panel.");
            CheckInkHeadOrientation(brush, camera, canvas, scaler);
            results.Add("PASS: the tilted and flipped ink remains full height and travels right to left.");
            results.Add("Previews: " + PreviewFolder + "/SkillInk*.png. Synthetic diagnostic UI only; no scene/assets/save data changed.");
            File.WriteAllLines(ResultPath, results);
            Debug.Log(string.Join("\n", results));
        }
        catch (Exception exception)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ResultPath));
            results.Add("FAIL: " + exception);
            File.WriteAllLines(ResultPath, results);
            Debug.LogException(exception);
            if (batch) throw;
        }
        finally
        {
            if (controller != null) controller.ResetTransition();
#if DOTWEEN
            if (controller != null) DOTween.Kill(controller);
#endif
            if (preview.IsValid()) EditorSceneManager.ClosePreviewScene(preview);
            if (target != null) { target.Release(); UnityEngine.Object.DestroyImmediate(target); }
        }
    }

#if DOTWEEN
    static void CheckTimeline(SkillPanelTransitionController controller, RectTransform commands, RectTransform panel,
        List<RectTransform> rows, List<string> results)
    {
        var positions = rows.Select(row => row.anchoredPosition).ToArray();
        commands.gameObject.SetActive(true);
        controller.OpenSkillPanel();
        Check(controller.IsTransitioning && !controller.IsOpen, "Opening must lock selection.");
        Check(rows.All(row => !row.GetComponent<Button>().IsInteractable()), "A skill can be submitted before reveal completes.");
        DOTween.Goto(controller, .3f, false);
        Check(rows[0].GetComponent<SkillItemAnimator>().Progress > rows[4].GetComponent<SkillItemAnimator>().Progress,
            "Skill items must enter in staggered order, not all at once.");
        controller.OpenSkillPanel();
        DOTween.Complete(controller);
        Check(controller.IsOpen && panel.gameObject.activeSelf, "Opening did not finish in a selectable state.");
        for (int i = 0; i < rows.Count; i++)
        {
            Check(Vector2.Distance(positions[i], rows[i].anchoredPosition) < .001f, "Item did not settle on its authored position.");
            Check(rows[i].GetComponent<Button>().IsInteractable() == (i != 2), "The transition changed per-skill availability.");
        }
        bool cancelDelivered = ExecuteEvents.Execute(rows[0].gameObject, new BaseEventData(null), ExecuteEvents.cancelHandler);
        Check(cancelDelivered && !controller.IsVisible && !panel.gameObject.activeSelf && commands.gameObject.activeSelf &&
            !DOTween.IsTweening(controller) && !controller.brushReveal.Mask.enabled,
            "Cancel on a selected skill must close immediately and remove the mask.");
        for (int i = 0; i < 12; i++)
        {
            commands.gameObject.SetActive(true);
            controller.OpenSkillPanel();
            DOTween.Goto(controller, .22f, false);
            controller.CloseSkillPanel();
            Check(!DOTween.IsTweening(controller) && !controller.IsVisible && !panel.gameObject.activeSelf &&
                commands.gameObject.activeSelf && !controller.brushReveal.Mask.enabled,
                $"Cancelled transition did not clean up at iteration {i}. Tweens={DOTween.TweensByTarget(controller)?.Count ?? 0}, state={controller.State}, panel={panel.gameObject.activeSelf}.");
        }
        commands.gameObject.SetActive(true);
        controller.OpenSkillPanel();
        DOTween.Goto(controller, .2f, false);
        // Ordinary MonoBehaviour lifecycle is not driven in an Edit Mode preview.
        typeof(SkillPanelTransitionController).GetMethod("OnDisable", PrivateInstance).Invoke(controller, null);
        Check(!DOTween.IsTweening(controller) && !controller.IsVisible && !panel.gameObject.activeSelf,
            "Disabling the controller leaves an active transition.");
        results.Add("PASS: skill input gate, stagger order, exact settle, availability preserved, immediate selected-row Cancel, 12 mid-wipe cancellations and disable cleanup.");
    }

    static void CheckItems(SkillPanelTransitionController controller, RectTransform commands, Camera camera,
        Canvas canvas, CanvasScaler scaler, List<string> results)
    {
        var scrollRoot = Rect("Item Scroll", canvas.transform, new Vector2(.66f, .5f), new Vector2(580f, 660f));
        var scroll = scrollRoot.gameObject.AddComponent<ScrollRect>();
        var viewport = Stretch("Viewport", scrollRoot, Vector2.zero, Vector2.one);
        viewport.gameObject.AddComponent<Image>();
        viewport.gameObject.AddComponent<Mask>().showMaskGraphic = false;
        var content = Stretch("Content", viewport, new Vector2(0f, 1f), Vector2.one);
        content.pivot = new Vector2(.5f, 1f);
        content.sizeDelta = new Vector2(0f, 900f);
        var row = Stretch("Item", content, Vector2.zero, Vector2.one);
        row.gameObject.AddComponent<Image>().color = new Color(.035f, .16f, .19f);
        var button = row.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        scroll.viewport = viewport;
        scroll.content = content;
        controller.itemsButton = commands.GetComponentInChildren<Button>(true);
        controller.itemsScroll = scroll;
        controller.itemsViewport = viewport;
        controller.itemRowsRoot = content;
        commands.gameObject.SetActive(true);
        Layout(canvas, scaler);
        var viewportBefore = WorldCorners(viewport);
        Check(controller.TryOpenPanel(true) && !scroll.enabled && !button.IsInteractable(), "Item opening did not lock scroll and buttons.");
        DOTween.Goto(controller, .25f, false);
        var pixels = Capture(camera, canvas, scaler, Path.Combine(PreviewFolder, "ItemInkHalf.png"));
        var bounds = viewport.rect;
        Color32 Sample(float x, float y)
        {
            var point = camera.WorldToScreenPoint(viewport.TransformPoint(new Vector2(x, y)));
            return pixels[(int)point.y * camera.targetTexture.width + (int)point.x];
        }
        Check(SameCorners(viewportBefore, WorldCorners(viewport)) && scroll.viewport == viewport && scroll.content == content,
            "Item reveal moved the viewport or changed the ScrollRect references.");
        Check(IsDiagnosticSurface(Sample(bounds.xMax - 35f, bounds.center.y)) &&
            !IsDiagnosticSurface(Sample(bounds.xMin + 35f, bounds.center.y)) &&
            !IsDiagnosticSurface(Sample(bounds.center.x, bounds.yMin - 20f)),
            "Item wipe did not reveal from the right or failed to clip the oversized list.");
        controller.CloseSkillPanel();
        Check(!DOTween.IsTweening(controller) && !viewport.gameObject.activeSelf && scroll.enabled && commands.gameObject.activeSelf,
            "Cancel during Items wipe must restore scrolling and commands immediately.");
        Check(controller.TryOpenPanel(true), "Items did not reopen after cancellation.");
        DOTween.Complete(controller);
        Check(scroll.enabled && button.IsInteractable(), "Item buttons and scrolling did not recover after the wipe.");
        float initialY = content.anchoredPosition.y;
        scroll.verticalNormalizedPosition = 0f;
        Check(content.anchoredPosition.y > initialY + 100f, "The revealed list cannot scroll inside the original viewport.");
        bool cancelDelivered = ExecuteEvents.Execute(row.gameObject, new BaseEventData(null), ExecuteEvents.cancelHandler);
        Check(cancelDelivered && !viewport.gameObject.activeSelf && commands.gameObject.activeSelf && !DOTween.IsTweening(controller),
            "Items Cancel left the panel or wipe active.");
        results.Add("PASS: Items right-to-left reveal, nested viewport clipping, input lock, scrolling after reveal, immediate Cancel both mid-wipe and fully open.");
    }
#endif

    static void CheckFullHeightInk(BrushRevealController brush, Camera camera, Canvas canvas, CanvasScaler scaler)
    {
        var corners = WorldCorners(brush.MaskRoot).Select(camera.WorldToScreenPoint).ToArray();
        float panelHeight = corners.Max(point => point.y) - corners.Min(point => point.y);
        float previousCenter = float.PositiveInfinity;
        foreach (float progress in new[] { .2f, .5f, .8f })
        {
            brush.SetProgress(progress);
            brush.SetBrushOpacity(1f);
            var pixels = Capture(camera, canvas, scaler,
                Path.Combine(PreviewFolder, $"SkillInkFront-{Mathf.RoundToInt(progress * 100f)}.png"));
            int width = camera.targetTexture.width;
            int minY = int.MaxValue, maxY = int.MinValue, count = 0;
            long totalX = 0;
            int maxRed = pixels[0].r - 12;
            for (int i = 0; i < pixels.Length; i++)
            {
                if (pixels[i].r > maxRed) continue;
                int y = i / width;
                minY = Mathf.Min(minY, y);
                maxY = Mathf.Max(maxY, y);
                totalX += i % width;
                count++;
            }
            Check(count > panelHeight * 8f && maxY - minY > panelHeight * .90f,
                $"Visible ink is too small at {progress:P0}. Span={maxY - minY}px, panel={panelHeight:0}px, ink pixels={count}.");
            float center = totalX / (float)count;
            Check(center < previousCenter - 20f, "The visible front did not advance from right to left.");
            previousCenter = center;
        }
    }

    static void CheckInkHeadOrientation(BrushRevealController brush, Camera camera, Canvas canvas, CanvasScaler scaler)
    {
        brush.inkHeadAngle = 25f;
        brush.flipInkHead = true;
        var corners = WorldCorners(brush.MaskRoot).Select(camera.WorldToScreenPoint).ToArray();
        float bottom = corners.Min(p => p.y), top = corners.Max(p => p.y);
        float previousCenter = float.PositiveInfinity;
        foreach (float progress in new[] { .35f, .5f, .65f })
        {
            brush.SetProgress(progress);
            brush.SetBrushOpacity(1f);
            var pixels = Capture(camera, canvas, scaler,
                Path.Combine(PreviewFolder, $"SkillInkTilt-{Mathf.RoundToInt(progress * 100f)}.png"));
            int width = camera.targetTexture.width;
            int maxRed = pixels[0].r - 12;
            float CenterAt(float height)
            {
                int y = Mathf.RoundToInt(Mathf.Lerp(bottom, top, height));
                var columns = Enumerable.Range(0, width).Where(x => pixels[y * width + x].r < maxRed).ToArray();
                Check(columns.Length > 8, "Angled ink failed to cover a row of the panel.");
                return (float)columns.Average();
            }
            Check(CenterAt(.90f) - CenterAt(.10f) > (top - bottom) * .24f, "Positive ink angle did not tilt the head to the right.");
            float center = CenterAt(.50f);
            Check(center < previousCenter - 20f, "Tilting or flipping the ink changed its travel direction.");
            previousCenter = center;
        }
    }

    static Color32[] Capture(Camera camera, Canvas canvas, CanvasScaler scaler, string path)
    {
        Layout(canvas, scaler);
        foreach (var graphic in canvas.GetComponentsInChildren<Graphic>(true))
            if (graphic.isActiveAndEnabled) graphic.SetAllDirty();
        foreach (var text in canvas.GetComponentsInChildren<TMP_Text>(true)) if (text.isActiveAndEnabled) text.ForceMeshUpdate(true);
        Canvas.ForceUpdateCanvases();
        camera.Render();
        var previous = RenderTexture.active;
        Texture2D image = null;
        try
        {
            RenderTexture.active = camera.targetTexture;
            image = new Texture2D(camera.targetTexture.width, camera.targetTexture.height, TextureFormat.RGBA32, false);
            image.ReadPixels(new Rect(0, 0, image.width, image.height), 0, 0);
            image.Apply();
            File.WriteAllBytes(path, image.EncodeToPNG());
            return image.GetPixels32();
        }
        finally
        {
            RenderTexture.active = previous;
            if (image != null) UnityEngine.Object.DestroyImmediate(image);
        }
    }

    static void Layout(Canvas canvas, CanvasScaler scaler)
    {
        typeof(CanvasScaler).GetMethod("Handle", PrivateInstance)?.Invoke(scaler, null);
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)canvas.transform);
        Canvas.ForceUpdateCanvases();
    }
    static bool IsDiagnosticSurface(Color32 pixel) => pixel.g > pixel.r + 20 && pixel.b > pixel.r + 20;
    static RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 size)
    {
        var obj = new GameObject(name, typeof(RectTransform));
        obj.layer = 5;
        obj.transform.SetParent(parent, false);
        var rect = (RectTransform)obj.transform;
        rect.anchorMin = rect.anchorMax = anchor;
        rect.sizeDelta = size;
        rect.anchoredPosition = Vector2.zero;
        return rect;
    }
    static RectTransform Stretch(string name, Transform parent, Vector2 min, Vector2 max)
    {
        var rect = Rect(name, parent, min, Vector2.zero);
        rect.anchorMax = max;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        return rect;
    }
    static TMP_Text Label(string name, Transform parent, string value, Vector2 min, Vector2 max, float size)
    {
        var rect = Stretch(name, parent, min, max);
        var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.font = TMP_Settings.defaultFontAsset;
        text.text = value;
        text.fontSize = size;
        text.color = new Color(.95f, .93f, .88f);
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.raycastTarget = false;
        return text;
    }
    static Vector3[] WorldCorners(RectTransform rect) { var corners = new Vector3[4]; rect.GetWorldCorners(corners); return corners; }
    static bool SameCorners(Vector3[] a, Vector3[] b) => a.Zip(b, (left, right) => Vector3.Distance(left, right)).All(distance => distance < .01f);
    static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
}

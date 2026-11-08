#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.Timeline;
using UnityEngine.UI;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

/// <summary>Opt-in checks of the saved scene, native Timeline bindings, scrubbing and actual UI renders.</summary>
[InitializeOnLoad]
public static class MangaIntroValidation
{
    const string ScenePath = "Assets/Scenes/MangaIntro.unity";
    const string AssetFolder = "Assets/MangaIntro/Timelines/";
    const string RequestPath = "Library/MangaIntroValidation.request";
    const string ResultPath = "Library/MangaIntroValidation.result.txt";
    const string CaptureFolder = "Library/MangaIntroValidation";
    static readonly double[] PanelStarts = { .5, 6.5, 12.5, 18.5, 24.5 };
    static bool running;

    static MangaIntroValidation() { EditorApplication.update += Poll; }

    // A domain reload alone never runs validation, creates a scene, or changes an open scene.
    static void Poll()
    {
        if (running || !File.Exists(RequestPath) || EditorApplication.isCompiling ||
            EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Delete(RequestPath);
        Validate();
    }

    [MenuItem("Tools/Manga Intro/Validate Saved Intro")]
    public static void Validate()
    {
        if (running || EditorApplication.isPlayingOrWillChangePlaymode) return;
        running = true;
        var timer = Stopwatch.StartNew();
        var report = new Report();
        Scene preview = default;
        PlayableDirector master = null;
        var openScenes = Enumerable.Range(0, SceneManager.sceneCount)
            .Select(i => SceneManager.GetSceneAt(i)).ToArray();
        var dirtyStates = openScenes.Select(s => s.isDirty).ToArray();
        Scene activeScene = SceneManager.GetActiveScene();
        try
        {
            Require(File.Exists(ScenePath), "Saved intro scene is missing: " + ScenePath);
            preview = EditorSceneManager.OpenPreviewScene(ScenePath);
            Require(preview.IsValid(), "Could not open the saved intro in an isolated preview scene.");
            GameObject[] roots = preview.GetRootGameObjects();
            var all = roots.SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
            report.Check(all.All(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) == 0), "No missing scripts in the saved scene");
            master = all.Single(t => t.name == "MangaIntro_Master").GetComponent<PlayableDirector>();
            Require(master != null, "MangaIntro_Master must have a PlayableDirector.");
            var canvas = all.Single(t => t.name == "MangaCanvas").GetComponent<Canvas>();
            Require(canvas != null, "MangaCanvas must have a native Canvas.");
            report.Check(canvas.GetComponent<PreserveAuthoredUI>() != null, "MangaCanvas preserves authored colors under the global UI theme");
            report.Check(canvas.GetComponentsInChildren<Button>(true).Length == 0, "Intro has no Next, Close or Replay buttons");
            report.Check(master.playOnAwake && master.extrapolationMode == DirectorWrapMode.None, "Only the master autoplays and ends with Wrap Mode None");
            var masterTimeline = master.playableAsset as TimelineAsset;
            Require(masterTimeline != null, "Master director does not reference a TimelineAsset.");
            report.Check(AssetDatabase.GetAssetPath(masterTimeline) == AssetFolder + "MangaIntro_Master.playable", "Master Timeline asset path resolves");
            var controls = Tracks(masterTimeline).OfType<ControlTrack>().ToArray();
            Require(controls.Length == 1, "Master must contain exactly one native Control Track.");
            TimelineClip[] controlClips = controls[0].GetClips().OrderBy(c => c.start).ToArray();
            Require(controlClips.Length == 3, "Master Control Track must have exactly three clips.");
            var chapters = new Chapter[3];
            for (int i = 0; i < chapters.Length; i++)
            {
                string name = "Chapter_" + (i + 1).ToString("00");
                Transform root = canvas.transform.Find(name);
                Require(root != null, "Missing Canvas child " + name);
                var director = root.GetComponent<PlayableDirector>();
                Require(director != null, name + " needs its own PlayableDirector.");
                var timeline = director.playableAsset as TimelineAsset;
                Require(timeline != null, name + " needs its own TimelineAsset.");
                var chapter = new Chapter(root.gameObject, director, timeline, controlClips[i]);
                chapters[i] = chapter;
                ValidateChapter(chapter, i, report);
                var asset = controlClips[i].asset as ControlPlayableAsset;
                Require(asset != null, name + " must use a native ControlPlayableAsset.");
                report.Check(asset.sourceGameObject.Resolve(master) == root.gameObject, name + " Control source resolves to its actual chapter root");
                report.Check(asset.active && asset.updateDirector && !asset.updateParticle && !asset.updateITimeControl && !asset.searchHierarchy && asset.prefabGameObject == null,
                    name + " Control only controls chapter activation and its own director");
                report.Check(asset.postPlayback == ActivationControlPlayable.PostPlaybackState.Inactive &&
                    asset.directorOnClipEnd == DirectorControlPlayable.PauseAction.PauseDirector, name + " Control hides and pauses the child on exit");
                report.Check(Near(controlClips[i].start, i * 33.7) && Near(controlClips[i].duration, 33.5) &&
                    Near(controlClips[i].clipIn, 0) && Near(controlClips[i].timeScale, 1), name + " Control range is untrimmed, speed 1, duration 33.5 seconds");
                if (i > 0) report.Check(Near(controlClips[i].start - controlClips[i - 1].end, .2), "Black interchapter gap " + i + " is 0.2 seconds with no overlapping Control clips");
            }
            report.Check(Near(masterTimeline.duration, controlClips[2].end), "Master duration ends with Chapter_03");
            var overlay = canvas.GetComponentsInChildren<Transform>(true).SingleOrDefault(t => t.name == "FadeOverlay");
            Require(overlay != null, "Canvas needs a FadeOverlay.");
            ValidateOverlay(master, masterTimeline, controlClips, overlay, report);
            ValidateAuthoredColors(canvas.gameObject, report);
            report.Step("Structure and asset bindings", timer.Elapsed);

            // Evaluate the real saved director graph. No validator activation or time forwarding substitutes for Timeline.
            master.RebuildGraph();
            int samples = 0;
            foreach (double time in SampleTimes(controlClips))
            {
                master.time = time;
                master.Evaluate();
                ValidateSample(master, chapters, overlay.GetComponent<CanvasGroup>(), time, report);
                samples++;
            }
            report.Line("Evaluated " + samples + " single-evaluation samples: forward, backward, boundaries, black gaps and discontinuous seeks.");
            report.Step("Native Timeline scrubbing", timer.Elapsed);

            Camera camera = roots.SelectMany(r => r.GetComponentsInChildren<Camera>(true)).FirstOrDefault();
            Require(camera != null, "The saved intro scene needs a camera for visual validation.");
            camera.scene = preview;
            camera.overrideSceneCullingMask = EditorSceneManager.GetSceneCullingMask(preview);
            camera.cullingMask = ~0;
            camera.cameraType = CameraType.Game;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.enabled = false;
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1;
            Directory.CreateDirectory(CaptureFolder);
            for (int i = 0; i < chapters.Length; i++)
                Capture(master, canvas, camera, chapters[i].Control.start + 30, "Chapter_" + (i + 1).ToString("00") + "_FullPage.png", report);
            for (int panel = 0; panel < 5; panel++)
                Capture(master, canvas, camera, controlClips[0].start + PanelStarts[panel] + 1.5,
                    "Chapter_01_Reveal_" + (panel + 1).ToString("00") + ".png", report);
            Capture(master, canvas, camera, controlClips[1].start - .1, "Black_ChapterGap.png", report, true);
            Capture(master, canvas, camera, controlClips[0].start + 30, "Chapter_01_FullPage_1280x1024.png", report, false, 1280, 1024);
            Capture(master, canvas, camera, controlClips[0].start + 30, "Chapter_01_FullPage_1080x1920.png", report, false, 1080, 1920);
            report.Step("1920 x 1080 rendered evidence", timer.Elapsed);

            master.time = controlClips[2].end;
            master.Evaluate();
            master.Stop();
            report.Check(chapters.All(c => !c.Root.activeSelf), "Stopping after the final chapter leaves every chapter inactive");
            report.Check(chapters.All(c => c.Director.state != PlayState.Playing), "Child directors are stopped or paused after master Stop");
            report.Line("All scene/camera/Canvas changes existed only in the unsaved preview scene.");
        }
        catch (Exception exception) { report.Fail(exception.ToString()); }
        finally
        {
            if (master != null) master.Stop();
            if (preview.IsValid()) EditorSceneManager.ClosePreviewScene(preview);
            report.Check(SceneManager.GetActiveScene() == activeScene, "User's active scene was preserved");
            for (int i = 0; i < openScenes.Length; i++)
                report.Check(openScenes[i].IsValid() && openScenes[i].isLoaded && openScenes[i].isDirty == dirtyStates[i], "User scene and dirty state preserved: " + openScenes[i].path);
            report.Line("Elapsed: " + timer.Elapsed.TotalSeconds.ToString("0.000", CultureInfo.InvariantCulture) + " seconds.");
            Directory.CreateDirectory("Library");
            File.WriteAllText(ResultPath, report.ToString(), new UTF8Encoding(false));
            running = false;
        }
        if (report.Failures == 0) Debug.Log("Manga intro validation PASS. " + ResultPath);
        else Debug.LogError("Manga intro validation FAIL (" + report.Failures + "). " + ResultPath);
    }

    static void ValidateChapter(Chapter chapter, int index, Report report)
    {
        string name = chapter.Root.name;
        report.Check(!chapter.Root.activeSelf && !chapter.Director.playOnAwake && chapter.Director.extrapolationMode == DirectorWrapMode.Hold,
            name + " is saved inactive, child autoplay off, Wrap Mode Hold");
        report.Check(AssetDatabase.GetAssetPath(chapter.Timeline) == AssetFolder + name + ".playable", name + " Timeline asset path resolves");
        report.Check(Near(chapter.Timeline.duration, 33.5), name + " child duration is 33.5 seconds");
        report.Check(chapter.Root.GetComponent<CanvasGroup>() != null && chapter.Root.GetComponent<Animator>() != null, name + " has editable page CanvasGroup and Animator");
        Transform page = chapter.Root.transform.Find("PageSafeArea/Page");
        Require(page != null, name + " needs PageSafeArea/Page.");
        report.Check(page.GetComponent<AspectRatioFitter>() != null, name + " keeps the authored page aspect ratio");
        chapter.Panels = Enumerable.Range(1, 5).Select(n => page.Find("Panel_" + n.ToString("00"))).ToArray();
        Require(chapter.Panels.All(p => p != null), name + " needs five named panel roots.");
        report.Check(Enumerable.Range(0, page.childCount).Count(n => page.GetChild(n).name.StartsWith("Panel_", StringComparison.Ordinal)) == 5,
            name + " has exactly five panel roots");
        var activationTracks = Tracks(chapter.Timeline).OfType<ActivationTrack>().ToArray();
        report.Check(activationTracks.Length == 5, name + " has exactly five native Activation Tracks");
        var animationTracks = Tracks(chapter.Timeline).OfType<AnimationTrack>().ToArray();
        for (int p = 0; p < chapter.Panels.Length; p++)
        {
            Transform panel = chapter.Panels[p];
            string label = name + "/" + panel.name;
            report.Check(!panel.gameObject.activeSelf, label + " is saved inactive");
            report.Check(panel.GetComponent<MangaPolygonGraphic>() != null && panel.GetComponent<Mask>() != null &&
                panel.GetComponent<CanvasGroup>() != null && panel.GetComponent<Animator>() != null, label + " has polygon stencil mask, CanvasGroup and Animator");
            foreach (string layer in new[] { "Background", "Character", "Relic", "Effects", "Dialogue" })
                report.Check(panel.Find(layer) != null, label + " contains editable " + layer + " layer");
            Transform dialogue = panel.Find("Dialogue");
            if (dialogue != null)
            {
                report.Check(dialogue.GetComponent<CanvasGroup>() != null && dialogue.GetComponent<Animator>() != null, label + " dialogue has an independent CanvasGroup and Animator");
                var text = dialogue.GetComponentsInChildren<TMP_Text>(true);
                bool intentionalSilentPanel = index == 2 && p == 3;
                report.Check((intentionalSilentPanel ? text.Length == 0 : text.Length > 0) &&
                    text.All(t => t.font != null && t.color.r < .08f && t.color.g < .08f && t.color.b < .08f),
                    label + (intentionalSilentPanel ? " preserves the intentionally silent portal-walk panel" : " has readable black TMP captions with a valid font"));
            }
            var bound = activationTracks.Where(t => BoundObject(chapter.Director, t) == panel.gameObject).ToArray();
            Require(bound.Length == 1, label + " must have one distinct Activation Track binding.");
            var clips = bound[0].GetClips().ToArray();
            report.Check(clips.Length == 1 && Near(clips[0].start, PanelStarts[p]) && Near(clips[0].end, 33.5),
                label + " activates at " + Number(PanelStarts[p]) + " and remains present until chapter end");
            report.Check(bound[0].postPlaybackState == ActivationTrack.PostPlaybackState.Inactive, label + " activation resets inactive on Stop");
            report.Check(animationTracks.Any(t => BoundObject(chapter.Director, t) == panel.gameObject && HasAlphaCurve(t)), label + " reveal is an editable CanvasGroup Animation Track");
            if (dialogue != null) report.Check(animationTracks.Any(t => BoundObject(chapter.Director, t) == dialogue.gameObject && HasAlphaCurve(t)), label + " caption fade is an independent editable Animation Track");
        }
        var pageTracks = animationTracks.Where(t => BoundObject(chapter.Director, t) == chapter.Root).ToArray();
        var pageClips = pageTracks.SelectMany(t => t.GetClips()).ToArray();
        report.Check(pageTracks.Any(HasAlphaCurve), name + " page opacity uses a native Animation Track");
        foreach (string label in new[] { "PageIn", "HoldFullPage", "PageFadeOut" })
            report.Check(pageClips.Any(c => c.displayName.IndexOf(label, StringComparison.OrdinalIgnoreCase) >= 0), name + " exposes an editable " + label + " clip");
        report.Line(name + ": Control " + Number(chapter.Control.start) + "–" + Number(chapter.Control.end) + "s; child " + Number(chapter.Timeline.duration) + "s; panels " + string.Join(", ", PanelStarts.Select(Number)) + "s.");
    }

    static void ValidateOverlay(PlayableDirector master, TimelineAsset timeline, TimelineClip[] controls, Transform overlay, Report report)
    {
        var image = overlay.GetComponent<Image>();
        var group = overlay.GetComponent<CanvasGroup>();
        Require(image != null && group != null && overlay.GetComponent<Animator>() != null, "FadeOverlay needs black Image, CanvasGroup and Animator.");
        report.Check(image.color.r < .001f && image.color.g < .001f && image.color.b < .001f && image.color.a > .99f && group.alpha > .99f,
            "Overlay is saved opaque black");
        report.Check(overlay.GetSiblingIndex() == overlay.parent.childCount - 1, "FadeOverlay draws over all chapters");
        var tracks = Tracks(timeline).OfType<AnimationTrack>().Where(t => BoundObject(master, t) == overlay.gameObject).ToArray();
        Require(tracks.Length == 1, "Master needs one native Animation Track bound to FadeOverlay Animator.");
        var fades = tracks[0].GetClips().OrderBy(c => c.start).ToArray();
        report.Check(fades.Length == controls.Length && tracks.Any(HasAlphaCurve), "Overlay has three editable native alpha Animation clips");
        for (int i = 0; i < Math.Min(fades.Length, controls.Length); i++)
        {
            report.Check(Near(fades[i].start, controls[i].start) && Near(fades[i].end, controls[i].end), "Fade clip " + (i + 1) + " matches its Control range");
            var animation = fades[i].asset as AnimationPlayableAsset;
            var curve = AlphaCurve(animation != null ? animation.clip : null);
            Require(curve != null, "Overlay fade clip " + (i + 1) + " has no CanvasGroup alpha curve.");
            float end = (float)fades[i].duration;
            report.Check(curve.Evaluate(0) > .99f && curve.Evaluate(.6f) < .01f &&
                curve.Evaluate(end - .8f) < .01f && curve.Evaluate(end) > .99f, "Fade clip " + (i + 1) + " is black→clear in 0.6s and clear→black in the final 0.8s");
        }
    }

    static void ValidateAuthoredColors(GameObject root, Report report)
    {
        var graphics = root.GetComponentsInChildren<Graphic>(true);
        Color[] colors = graphics.Select(g => g.color).ToArray();
        GameUITheme.Apply(root);
        report.Check(graphics.Select((g, i) => g != null && g.color == colors[i]).All(s => s), "Global GameUITheme.Apply leaves every authored manga Graphic color unchanged");
    }

    static void ValidateSample(PlayableDirector master, Chapter[] chapters, CanvasGroup overlay, double time, Report report)
    {
        int expected = Array.FindIndex(chapters, c => time >= c.Control.start && time < c.Control.end);
        var active = chapters.Where(c => c.Root.activeSelf).ToArray();
        string label = "t=" + Number(time) + "s";
        report.Check(active.Length <= 1, label + " never activates overlapping chapters");
        // The final endpoint is handled by explicit Stop below: Timeline may sample its last tick there.
        if (!Near(time, chapters[2].Control.end, .0000001))
            report.Check(expected < 0 ? active.Length == 0 : active.Length == 1 && active[0] == chapters[expected], label + " activates only the chapter selected by actual Control ranges");
        if (expected < 0)
        {
            if (time > chapters[0].Control.end && time < chapters[2].Control.start)
                report.Check(overlay.alpha > .98f, label + " chapter gap is fully black");
            return;
        }
        Chapter chapter = chapters[expected];
        double local = time - chapter.Control.start;
        report.Check(Math.Abs(chapter.Director.time - local) < .04, label + " native Control synchronizes child time");
        for (int p = 0; p < chapter.Panels.Length; p++)
        {
            bool visible = local >= PanelStarts[p];
            report.Check(chapter.Panels[p].gameObject.activeSelf == visible, label + " " + chapter.Root.name + "/Panel_" + (p + 1).ToString("00") + " preserves cumulative reveal state");
            if (local > PanelStarts[p] + .8 && local < 32.5)
                report.Check(chapter.Panels[p].GetComponent<CanvasGroup>().alpha > .98f, label + " revealed panel " + (p + 1) + " stays opaque");
        }
        if (local >= 27 && local <= 31.5)
        {
            report.Check(chapter.Panels.All(p => p.gameObject.activeInHierarchy), label + " full-page hold keeps all five panels visible");
            report.Check(chapter.Root.GetComponent<CanvasGroup>().alpha > .98f && overlay.alpha < .02f, label + " full-page hold has no page fade or black overlay");
        }
    }

    static IEnumerable<double> SampleTimes(TimelineClip[] clips)
    {
        double end = clips[clips.Length - 1].end;
        var forward = new SortedSet<double>();
        for (double t = 0; t < end; t += .25) forward.Add(t);
        forward.Add(end);
        foreach (TimelineClip clip in clips)
        {
            foreach (double boundary in new[] { clip.start, clip.end }.Concat(PanelStarts.Select(p => clip.start + p)))
                foreach (double offset in new[] { -.02, -.001, 0, .001, .02 })
                    if (boundary + offset >= 0 && boundary + offset <= end) forward.Add(boundary + offset);
            foreach (double hold in new[] { 27.0, 29.0, 31.5 }) forward.Add(clip.start + hold);
        }
        foreach (double time in forward) yield return time;
        foreach (double time in forward.Reverse()) yield return time;
        double[] discontinuous = { 97.4, .25, 64.7, 33.6, 91.9, 6.4, 40.3, 67.3, 30.0, 80.0, 0, 100.89, 33.7, 24.49, 67.4, 58.2 };
        for (int repeat = 0; repeat < 3; repeat++)
            foreach (double time in discontinuous) yield return time;
    }

    static void Capture(PlayableDirector master, Canvas canvas, Camera camera, double time, string file, Report report,
        bool expectBlack = false, int width = 1920, int height = 1080)
    {
        var renderTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { hideFlags = HideFlags.HideAndDontSave, antiAliasing = 1 };
        Texture2D pixels = null;
        RenderTexture previous = RenderTexture.active;
        RenderTexture previousTarget = camera.targetTexture;
        try
        {
            renderTexture.Create();
            camera.targetTexture = renderTexture;
            master.time = time;
            master.Evaluate();
            Canvas.ForceUpdateCanvases();
            foreach (var text in canvas.GetComponentsInChildren<TMP_Text>(false)) text.ForceMeshUpdate();
            foreach (var graphic in canvas.GetComponentsInChildren<Graphic>(false)) graphic.Rebuild(CanvasUpdate.PreRender);
            Canvas.ForceUpdateCanvases();
            if (GraphicsSettings.currentRenderPipeline != null)
                RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = renderTexture });
            else camera.Render();
            // A camera render applies the final Canvas and AspectRatioFitter layout, especially on its first target size.
            if (!expectBlack)
            {
                var page = canvas.GetComponentsInChildren<AspectRatioFitter>(false).Single(fitter => fitter.name == "Page");
                var pageRect = page.GetComponent<RectTransform>();
                float actualRatio = pageRect.rect.width / pageRect.rect.height;
                report.Check(Math.Abs(actualRatio - page.aspectRatio) < .002f,
                    file + " preserves the authored page aspect ratio (actual=" + Number(actualRatio) + ", expected=" + Number(page.aspectRatio) + ")");
                var corners = new Vector3[4];
                pageRect.GetWorldCorners(corners);
                Vector3[] viewport = corners.Select(camera.WorldToViewportPoint).ToArray();
                report.Check(viewport.All(point => point.x >= .045f && point.x <= .955f && point.y >= .03f && point.y <= .97f),
                    file + " keeps every page corner within the Canvas safe area");
            }
            RenderTexture.active = renderTexture;
            pixels = new Texture2D(width, height, TextureFormat.RGB24, false) { hideFlags = HideFlags.HideAndDontSave };
            pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            pixels.Apply();
            File.WriteAllBytes(Path.Combine(CaptureFolder, file), pixels.EncodeToPNG());
            int nonblack = pixels.GetPixels32().Count(p => p.r > 24 || p.g > 24 || p.b > 24);
            report.Check(expectBlack ? nonblack < 100 : nonblack > 20000, file + (expectBlack ? " renders a black chapter gap" : " contains visible UI imagery") + " (nonblack pixels: " + nonblack + ")");
            report.Line("Capture: " + Path.Combine(CaptureFolder, file) + "; master t=" + Number(time) + "s; " + width + "x" + height + ".");
        }
        finally
        {
            RenderTexture.active = previous;
            camera.targetTexture = previousTarget;
            renderTexture.Release();
            Object.DestroyImmediate(renderTexture);
            if (pixels != null) Object.DestroyImmediate(pixels);
        }
    }

    static IEnumerable<TrackAsset> Tracks(TimelineAsset timeline)
    {
        foreach (var root in timeline.GetRootTracks())
            foreach (var track in Walk(root)) yield return track;
    }
    static IEnumerable<TrackAsset> Walk(TrackAsset track)
    {
        yield return track;
        foreach (var child in track.GetChildTracks())
            foreach (var nested in Walk(child)) yield return nested;
    }
    static GameObject BoundObject(PlayableDirector director, TrackAsset track)
    {
        Object binding = director.GetGenericBinding(track);
        return binding is GameObject go ? go : binding is Component component ? component.gameObject : null;
    }
    static bool HasAlphaCurve(AnimationTrack track)
    {
        return AlphaCurve(track.infiniteClip) != null || track.GetClips().Any(c => c.asset is AnimationPlayableAsset asset && AlphaCurve(asset.clip) != null);
    }
    static AnimationCurve AlphaCurve(AnimationClip clip)
    {
        if (clip == null) return null;
        var bindings = AnimationUtility.GetCurveBindings(clip);
        foreach (var binding in bindings)
            if (binding.type == typeof(CanvasGroup) && binding.propertyName == "m_Alpha") return AnimationUtility.GetEditorCurve(clip, binding);
        return null;
    }
    static bool Near(double a, double b, double epsilon = .002) { return Math.Abs(a - b) <= epsilon; }
    static string Number(double number) { return number.ToString("0.000", CultureInfo.InvariantCulture); }
    static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }

    sealed class Chapter
    {
        public readonly GameObject Root;
        public readonly PlayableDirector Director;
        public readonly TimelineAsset Timeline;
        public readonly TimelineClip Control;
        public Transform[] Panels;
        public Chapter(GameObject root, PlayableDirector director, TimelineAsset timeline, TimelineClip control)
        { Root = root; Director = director; Timeline = timeline; Control = control; }
    }
    sealed class Report
    {
        readonly StringBuilder lines = new StringBuilder();
        int checks;
        public int Failures { get; private set; }
        public void Check(bool condition, string message)
        {
            checks++;
            if (!condition) Fail(message);
        }
        public void Fail(string message)
        {
            Failures++;
            // Preserve useful evidence without producing megabytes if one binding affects every scrub sample.
            if (Failures <= 160) lines.AppendLine("FAIL: " + message);
        }
        public void Line(string message) { lines.AppendLine(message); }
        public void Step(string name, TimeSpan elapsed) { Line(name + ": " + elapsed.TotalSeconds.ToString("0.000", CultureInfo.InvariantCulture) + "s elapsed."); }
        public override string ToString()
        {
            return (Failures == 0 ? "PASS" : "FAIL") + ": Manga intro; " + checks + " checks; " + Failures + " failures.\n" +
                "Saved scene: " + ScenePath + "\nUTC: " + DateTime.UtcNow.ToString("O") + "\n" + lines +
                (Failures > 160 ? "Further repeated failures omitted.\n" : "");
        }
    }
}
#endif

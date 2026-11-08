using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.Timeline;
using UnityEngine.UI;

/// <summary>Authors native Timeline assets and a standalone scene; no runtime sequence manager.</summary>
[InitializeOnLoad]
public static class MangaIntroBuilder
{
    public const string ScenePath = "Assets/Scenes/MangaIntro.unity";
    public const string Output = "Assets/MangaIntro";
    public const double ChapterLength = 33.5;
    public static readonly double[] PanelStarts = { .5, 6.5, 12.5, 18.5, 24.5 };
    const string Request = "Library/MangaIntroBuild.request";
    static MangaIntroBuilder() { EditorApplication.update += CheckRequest; }

    static void CheckRequest()
    {
        if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating ||
            EditorApplication.isPlayingOrWillChangePlaymode) return;
        try { File.Delete(Request); } catch (IOException) { return; }
        Build();
    }

    [MenuItem("Tools/Manga Intro/Build Manga Intro Scene")]
    public static void Build()
    {
        Scene scene = default;
        Scene previous = SceneManager.GetActiveScene();
        try
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode before building the intro.");
            if (File.Exists(ScenePath) || AssetDatabase.LoadAssetAtPath<TimelineAsset>(Output + "/Timelines/MangaIntro_Master.playable") != null)
                throw new InvalidOperationException("Manga Intro already exists. Edit its native Timelines directly; this command never overwrites authored work.");
            Folder(Output); Folder(Output + "/Timelines"); Folder(Output + "/Animations"); Folder(Output + "/Fonts");
            var data = MangaIntroLayout.Create();
            foreach (var chapter in data)
            {
                RequireTexture(chapter.background);
                foreach (var layer in chapter.panels.SelectMany(panel => panel.layers)) RequireTexture(layer.path);
            }
            var font = PrepareFont(data);
            scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            var masterObject = new GameObject("MangaIntro_Master", typeof(PlayableDirector));
            var masterDirector = masterObject.GetComponent<PlayableDirector>();
            masterDirector.playOnAwake = true;
            masterDirector.extrapolationMode = DirectorWrapMode.None;
            masterDirector.timeUpdateMode = DirectorUpdateMode.UnscaledGameTime;
            var cameraObject = new GameObject("Manga Camera", typeof(Camera));
            var camera = cameraObject.GetComponent<Camera>();
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
            camera.orthographic = true; camera.orthographicSize = 5f;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
            camera.nearClipPlane = .1f; camera.farClipPlane = 100f;
            camera.allowHDR = false;
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
            var canvasObject = new GameObject("MangaCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(PreserveAuthoredUI));
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera;
            canvas.planeDistance = 10f; canvas.sortingOrder = 5000;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = .5f;
            var matte = Rect("Matte", canvas.transform); Stretch(matte);
            matte.gameObject.AddComponent<Image>().color = Color.black;
            var master = Timeline("MangaIntro_Master");
            masterDirector.playableAsset = master;
            var controls = master.CreateTrack<ControlTrack>(null, "Chapters — ONE active at a time");
            var chapterRoots = new GameObject[data.Length];
            for (int index = 0; index < data.Length; index++)
            {
                var chapter = data[index];
                var root = BuildChapter(chapter, canvas.transform, font);
                chapterRoots[index] = root;
                var clip = controls.CreateClip<ControlPlayableAsset>();
                clip.displayName = chapter.id + " — " + ChapterTitle(index);
                clip.start = index * (ChapterLength + .2);
                clip.duration = ChapterLength; clip.clipIn = 0; clip.timeScale = 1;
                var control = (ControlPlayableAsset)clip.asset;
                control.sourceGameObject.exposedName = new PropertyName("MangaIntro_" + chapter.id);
                masterDirector.SetReferenceValue(control.sourceGameObject.exposedName, root);
                control.active = true; control.updateDirector = true;
                control.updateParticle = false; control.updateITimeControl = false; control.searchHierarchy = false;
                control.postPlayback = ActivationControlPlayable.PostPlaybackState.Inactive;
                control.directorOnClipEnd = DirectorControlPlayable.PauseAction.PauseDirector;
                root.SetActive(false);
            }
            var overlay = Rect("FadeOverlay", canvas.transform); Stretch(overlay);
            overlay.gameObject.AddComponent<Image>().color = Color.black;
            overlay.gameObject.AddComponent<CanvasGroup>().alpha = 1f;
            var overlayAnimator = overlay.gameObject.AddComponent<Animator>();
            var fadeTrack = master.CreateTrack<AnimationTrack>(null, "Shared FadeOverlay — move with chapter controls");
            masterDirector.SetGenericBinding(fadeTrack, overlayAnimator);
            for (int index = 0; index < data.Length; index++)
            {
                var animation = Clip("Master_Fade_" + data[index].id);
                Curve(animation, "", typeof(CanvasGroup), "m_Alpha", new[] { 0f, .6f, (float)ChapterLength - .8f, (float)ChapterLength }, new[] { 1f, 0f, 0f, 1f });
                AddAnimation(fadeTrack, animation, index * (ChapterLength + .2), ChapterLength, data[index].id + " — shared black fade");
            }
            DisableRaycasts(canvasObject);
            foreach (var root in chapterRoots) root.SetActive(false);
            EditorUtility.SetDirty(masterDirector);
            AssetDatabase.SaveAssets();
            if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new IOException("Could not save " + ScenePath);
            File.WriteAllText("Library/MangaIntroBuild.result.txt", "PASS: standalone scene, 1 native master + 3 child timelines, 15 cumulative polygon-masked panels, 21 editable TMP captions, native Control/Activation/Animation bindings and shared fades authored. Existing open scenes preserved.");
            Debug.Log("Manga Intro created: " + ScenePath + ". Open this scene and press Play, or inspect MangaIntro_Master in Timeline.");
        }
        catch (Exception exception)
        {
            File.WriteAllText("Library/MangaIntroBuild.result.txt", "FAIL: " + exception);
            Debug.LogException(exception);
        }
        finally
        {
            if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            if (scene.IsValid()) EditorSceneManager.CloseScene(scene, true);
        }
    }

    static GameObject BuildChapter(MangaIntroLayout.Chapter chapter, Transform parent, TMP_FontAsset font)
    {
        var root = Rect(chapter.id, parent); Stretch(root);
        var group = root.gameObject.AddComponent<CanvasGroup>(); group.alpha = 1f;
        var rootAnimator = root.gameObject.AddComponent<Animator>();
        var director = root.gameObject.AddComponent<PlayableDirector>();
        director.playOnAwake = false; director.extrapolationMode = DirectorWrapMode.Hold;
        director.timeUpdateMode = DirectorUpdateMode.Manual;
        var timeline = Timeline(chapter.id); director.playableAsset = timeline;
        var safeArea = Rect("PageSafeArea", root); Stretch(safeArea);
        safeArea.anchorMin = new Vector2(.05f, .035f); safeArea.anchorMax = new Vector2(.95f, .965f);
        var page = Rect("Page", safeArea); Stretch(page);
        var fit = page.gameObject.AddComponent<AspectRatioFitter>();
        fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        fit.aspectRatio = chapter.width / chapter.height;
        page.gameObject.AddComponent<Image>().color = Color.white;
        var pageTrack = timeline.CreateTrack<AnimationTrack>(null, "Page — fade in / hold / fade out");
        director.SetGenericBinding(pageTrack, rootAnimator);
        var pageIn = Clip(chapter.id + "_PageIn"); Alpha(pageIn, .45f, 0f, 1f);
        AddAnimation(pageTrack, pageIn, 0, .45, "PageIn");
        var hold = Clip(chapter.id + "_HoldFullPage"); Alpha(hold, 7.55f, 1f, 1f);
        AddAnimation(pageTrack, hold, 25.15, 7.55, "HoldFullPage — resize here");
        var pageOut = Clip(chapter.id + "_PageFadeOut"); Alpha(pageOut, .8f, 1f, 0f);
        AddAnimation(pageTrack, pageOut, 32.7, .8, "PageFadeOut");
        for (int index = 0; index < chapter.panels.Length; index++)
        {
            var spec = chapter.panels[index];
            string panelName = "Panel_" + (index + 1).ToString("00");
            var panel = Rect(panelName, page); Stretch(panel);
            panel.gameObject.AddComponent<CanvasGroup>().alpha = 0f;
            var animator = panel.gameObject.AddComponent<Animator>();
            var maskGraphic = panel.gameObject.AddComponent<MangaPolygonGraphic>();
            maskGraphic.SetPolygon(spec.polygon.Select(point => new Vector2(point.x / chapter.width, 1f - point.y / chapter.height)).ToArray());
            var mask = panel.gameObject.AddComponent<Mask>(); mask.showMaskGraphic = false;
            var background = Rect("Background", panel); Stretch(background);
            background.gameObject.AddComponent<RawImage>().texture = RequireTexture(chapter.background);
            var character = Rect("Character", panel); Stretch(character);
            var relic = Rect("Relic", panel); Stretch(relic);
            var effects = Rect("Effects", panel); Stretch(effects);
            effects.gameObject.AddComponent<Image>().color = Color.white;
            effects.gameObject.AddComponent<CanvasGroup>().alpha = 0f;
            var effectsAnimator = effects.gameObject.AddComponent<Animator>();
            foreach (var layer in spec.layers)
            {
                Transform layerParent = layer.role == "Relic" ? relic : layer.role == "Effects" ? effects : character;
                var layerRect = Rect(layer.name, layerParent);
                PageRect(layerRect, layer.rect, chapter);
                layerRect.gameObject.AddComponent<RawImage>().texture = RequireTexture(layer.path);
            }
            var dialogue = Rect("Dialogue", panel); Stretch(dialogue);
            dialogue.gameObject.AddComponent<CanvasGroup>().alpha = 0f;
            var textAnimator = dialogue.gameObject.AddComponent<Animator>();
            for (int line = 0; line < spec.dialogue.Length; line++)
            {
                var text = spec.dialogue[line];
                var bubble = Rect("Speech_" + (line + 1).ToString("00"), dialogue); PageRect(bubble, text.rect, chapter);
                var plate = bubble.gameObject.AddComponent<MangaSpeechBubbleGraphic>();
                plate.ellipse = text.ellipse; plate.borderWidth = 1.5f; plate.color = Color.white;
                var textRect = Rect("Text", bubble); Stretch(textRect);
                textRect.anchorMin = text.ellipse ? new Vector2(.14f, .10f) : new Vector2(.06f, .06f);
                textRect.anchorMax = text.ellipse ? new Vector2(.86f, .90f) : new Vector2(.94f, .94f);
                var label = textRect.gameObject.AddComponent<TextMeshProUGUI>();
                label.font = font; label.text = text.text; label.color = Color.black;
                label.fontStyle = FontStyles.Bold | FontStyles.Italic;
                label.alignment = TextAlignmentOptions.Center;
                label.enableAutoSizing = true; label.fontSizeMin = 7; label.fontSizeMax = 28;
                label.textWrappingMode = TextWrappingModes.Normal;
                label.overflowMode = TextOverflowModes.Overflow;
                label.lineSpacing = 0f; label.paragraphSpacing = 0f;
                label.margin = Vector4.zero; label.raycastTarget = false;
            }
            var activation = timeline.CreateTrack<ActivationTrack>(null, panelName + " — activation (keep to end)");
            activation.postPlaybackState = ActivationTrack.PostPlaybackState.Inactive;
            director.SetGenericBinding(activation, panel.gameObject);
            var activeClip = activation.CreateDefaultClip();
            activeClip.displayName = panelName + " stays on page";
            activeClip.start = PanelStarts[index]; activeClip.duration = ChapterLength - PanelStarts[index];
            var revealTrack = timeline.CreateTrack<AnimationTrack>(null, panelName + " — reveal");
            director.SetGenericBinding(revealTrack, animator);
            var reveal = Clip(chapter.id + "_" + panelName + "_Reveal"); Alpha(reveal, .65f, 0f, 1f);
            AddAnimation(revealTrack, reveal, PanelStarts[index], .65, panelName + " reveal");
            var motionTrack = timeline.CreateTrack<AnimationTrack>(null, panelName + " — character drift");
            director.SetGenericBinding(motionTrack, character.gameObject.AddComponent<Animator>());
            var motion = Clip(chapter.id + "_" + panelName + "_CharacterDrift");
            Curve(motion, "", typeof(RectTransform), "m_AnchoredPosition.x", new[] { 0f, .9f }, new[] { -12f, 0f });
            AddAnimation(motionTrack, motion, PanelStarts[index], .9, "Character settles into page");
            var effectsTrack = timeline.CreateTrack<AnimationTrack>(null, panelName + " — reveal light");
            director.SetGenericBinding(effectsTrack, effectsAnimator);
            var flash = Clip(chapter.id + "_" + panelName + "_Light");
            Curve(flash, "", typeof(CanvasGroup), "m_Alpha", new[] { 0f, .08f, .35f }, new[] { 0f, .10f, 0f });
            AddAnimation(effectsTrack, flash, PanelStarts[index] + .1, .35, "Subtle white reveal flash");
            var dialogueTrack = timeline.CreateTrack<AnimationTrack>(null, panelName + " — TMP dialogue");
            director.SetGenericBinding(dialogueTrack, textAnimator);
            var caption = Clip(chapter.id + "_" + panelName + "_Dialogue"); Alpha(caption, .4f, 0f, 1f);
            AddAnimation(dialogueTrack, caption, PanelStarts[index] + .45, .4, "Dialogue in — then hold");
            panel.gameObject.SetActive(false);
        }
        EditorUtility.SetDirty(director);
        return root.gameObject;
    }

    static TimelineAsset Timeline(string name)
    {
        var timeline = ScriptableObject.CreateInstance<TimelineAsset>();
        timeline.name = name; timeline.editorSettings.frameRate = 60;
        timeline.durationMode = TimelineAsset.DurationMode.BasedOnClips;
        AssetDatabase.CreateAsset(timeline, Output + "/Timelines/" + name + ".playable");
        return timeline;
    }

    static AnimationClip Clip(string name)
    {
        var clip = new AnimationClip { name = name, frameRate = 60f };
        AssetDatabase.CreateAsset(clip, Output + "/Animations/" + name + ".anim");
        var settings = AnimationUtility.GetAnimationClipSettings(clip); settings.loopTime = false;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        return clip;
    }

    static void Alpha(AnimationClip clip, float duration, float from, float to) =>
        Curve(clip, "", typeof(CanvasGroup), "m_Alpha", new[] { 0f, duration }, new[] { from, to });

    static void Curve(AnimationClip clip, string path, Type type, string property, float[] time, float[] value)
    {
        var curve = new AnimationCurve(time.Select((key, i) => new Keyframe(key, value[i])).ToArray());
        for (int i = 0; i < curve.length; i++)
        {
            AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto);
            AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto);
        }
        AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, type, property), curve);
    }

    static TimelineClip AddAnimation(AnimationTrack track, AnimationClip animation, double start, double duration, string name)
    {
        var clip = track.CreateClip<AnimationPlayableAsset>();
        ((AnimationPlayableAsset)clip.asset).clip = animation;
        clip.start = start; clip.duration = duration; clip.displayName = name;
        // Native Hold extrapolation keeps completed reveals visible and covers gaps between page clips.
        var serialized = new SerializedObject(track);
        var clips = serialized.FindProperty("m_Clips");
        int index = track.GetClips().ToList().IndexOf(clip);
        if (clips != null && index >= 0 && index < clips.arraySize)
        {
            clips.GetArrayElementAtIndex(index).FindPropertyRelative("m_PreExtrapolationMode").intValue = (int)TimelineClip.ClipExtrapolation.Hold;
            clips.GetArrayElementAtIndex(index).FindPropertyRelative("m_PostExtrapolationMode").intValue = (int)TimelineClip.ClipExtrapolation.Hold;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
        return clip;
    }

    static TMP_FontAsset PrepareFont(MangaIntroLayout.Chapter[] chapters)
    {
        string path = Output + "/Fonts/MangaIntro SDF.asset";
        if (!AssetDatabase.CopyAsset("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF - Fallback.asset", path))
            throw new IOException("Could not create the dedicated intro font.");
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
        font.name = "MangaIntro SDF";
        font.atlasPopulationMode = AtlasPopulationMode.Dynamic; font.isMultiAtlasTexturesEnabled = true;
        string text = string.Join("", chapters.SelectMany(chapter => chapter.panels).SelectMany(panel => panel.dialogue).Select(line => line.text));
        font.TryAddCharacters(text, out string missing);
        if (!string.IsNullOrEmpty(missing)) throw new InvalidOperationException("Intro font missing glyphs: " + missing);
        EditorUtility.SetDirty(font);
        return font;
    }

    static Texture2D RequireTexture(string path)
    {
        if (AssetDatabase.LoadAssetAtPath<Texture2D>(path) == null) AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) throw new InvalidOperationException("Missing intro texture: " + path);
        if (importer.mipmapEnabled || importer.textureCompression != TextureImporterCompression.Uncompressed || importer.maxTextureSize < 2048)
        {
            importer.mipmapEnabled = false; importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 2048; importer.wrapMode = TextureWrapMode.Clamp; importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    static RectTransform Rect(string name, Transform parent)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false); return rect;
    }
    static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero; rect.localScale = Vector3.one;
    }
    static void PageRect(RectTransform rect, Rect box, MangaIntroLayout.Chapter chapter)
    {
        rect.anchorMin = new Vector2(box.xMin / chapter.width, 1f - box.yMax / chapter.height);
        rect.anchorMax = new Vector2(box.xMax / chapter.width, 1f - box.yMin / chapter.height);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }
    static void DisableRaycasts(GameObject root)
    {
        foreach (var graphic in root.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;
    }
    static string ChapterTitle(int index) => new[] { "Tàn tích thức tỉnh", "Điện thờ và thánh vật", "Chiếc nhẫn và cổng thế giới khác" }[index];
    static void Folder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = path.Substring(0, path.LastIndexOf('/')); Folder(parent);
        AssetDatabase.CreateFolder(parent, path.Substring(path.LastIndexOf('/') + 1));
    }

    [MenuItem("Tools/Manga Intro/Sync Shared Fades To Chapter Clips")]
    public static void SyncMasterFades()
    {
        var master = AssetDatabase.LoadAssetAtPath<TimelineAsset>(Output + "/Timelines/MangaIntro_Master.playable");
        if (master == null) throw new InvalidOperationException("Build Manga Intro first.");
        var controls = master.GetOutputTracks().OfType<ControlTrack>().SelectMany(track => track.GetClips()).OrderBy(clip => clip.start).ToArray();
        for (int i = 1; i < controls.Length; i++)
            if (controls[i].start < controls[i - 1].end) throw new InvalidOperationException("Chapter Control clips overlap. Move them apart before playback.");
        var fadeTrack = master.GetOutputTracks().OfType<AnimationTrack>().Single();
        Undo.RecordObject(fadeTrack, "Sync Manga chapter fades");
        foreach (var control in controls)
        {
            var chapterId = control.displayName.Substring(0, 10);
            var fade = fadeTrack.GetClips().Single(clip => clip.displayName.StartsWith(chapterId, StringComparison.Ordinal));
            fade.start = control.start; fade.duration = control.duration; fade.clipIn = 0;
            fade.timeScale = ((AnimationPlayableAsset)fade.asset).clip.length / control.duration;
        }
        EditorUtility.SetDirty(fadeTrack); EditorUtility.SetDirty(master);
        AssetDatabase.SaveAssets();
        Debug.Log("Shared FadeOverlay clips now match chapter control intervals. Keep Control clips nonoverlapping.");
    }
}

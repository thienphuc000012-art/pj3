using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.Timeline;

/// <summary>Opt-in authoring of the supplied intro narration, using native Timeline audio.</summary>
[InitializeOnLoad]
public static class MangaIntroVoiceSetup
{
    const string ScenePath = "Assets/Scenes/MangaIntro.unity";
    const string RequestPath = "Library/MangaIntroVoice.request";
    const string Output = "Tools/MangaIntroVoice";
    static MangaIntroVoiceSetup() { EditorApplication.update += Poll; }

    static void Poll()
    {
        if (!File.Exists(RequestPath) || EditorApplication.isCompiling || EditorApplication.isUpdating ||
            EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (File.GetLastWriteTimeUtc("Library/ScriptAssemblies/Assembly-CSharp-Editor.dll") <
            File.GetLastWriteTimeUtc("Assets/Editor/MangaIntroVoiceSetup.cs")) return;
        string command;
        try { command = File.ReadAllText(RequestPath).Trim(); File.Delete(RequestPath); }
        catch (IOException) { return; }
        try
        {
            if (command == "inspect") Inspect();
            else if (command == "setup") Setup();
            else if (command == "validate") Validate();
            else if (command == "bind-stop") BindStopHandler();
            else if (command == "fix-scheduling") FixAudioScheduling();
            else if (command == "retime-ch1-panel2") RetimeChapterOnePanelTwo();
            else throw new InvalidOperationException("Unknown voice command: " + command);
        }
        catch (Exception e)
        {
            Directory.CreateDirectory(Output);
            File.WriteAllText(Output + "/Error-" + DateTime.Now.ToString("yyyyMMdd-HHmmssfff") + ".txt", e.ToString());
            Debug.LogException(e);
        }
    }

    static Scene LoadedIntro() => Enumerable.Range(0, SceneManager.sceneCount).Select(SceneManager.GetSceneAt)
        .Single(s => s.path == ScenePath && s.isLoaded);
    static PlayableDirector Master(Scene scene) => scene.GetRootGameObjects()
        .Single(r => r.name == "MangaIntro_Master").GetComponent<PlayableDirector>();
    static System.Collections.Generic.IEnumerable<TrackAsset> Tracks(TimelineAsset timeline) =>
        timeline.GetRootTracks().SelectMany(Flatten);
    static System.Collections.Generic.IEnumerable<TrackAsset> Flatten(TrackAsset track)
    {
        yield return track;
        foreach (var child in track.GetChildTracks()) foreach (var nested in Flatten(child)) yield return nested;
    }
    static string Number(double number) => number.ToString("0.000", CultureInfo.InvariantCulture);
    static void Require(bool valid, string message) { if (!valid) throw new InvalidOperationException(message); }
    static GameObject Bound(PlayableDirector director, TrackAsset track)
    {
        var value = director.GetGenericBinding(track);
        return value is Component component ? component.gameObject : value as GameObject;
    }
    static AudioClip[] Voices(int chapter, int panel)
    {
        string folder = "Assets/asset/intro/Chapter_" + chapter.ToString("00") + "/chap" + chapter + "dialog/Panel_" + panel.ToString("00");
        return !Directory.Exists(folder) ? Array.Empty<AudioClip>() : Directory.GetFiles(folder, "Speech_*.mp3")
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase).Select(p => AssetDatabase.LoadAssetAtPath<AudioClip>(p.Replace('\\', '/'))).ToArray();
    }
    static double Length(AudioClip clip) => (double)clip.samples / clip.frequency;

    [MenuItem("Tools/Manga Intro/Reflow After Chapter 1 Panel 2 Voice")]
    public static void RetimeChapterOnePanelTwo()
    {
        Require(!EditorApplication.isPlayingOrWillChangePlaymode && !AnimationMode.InAnimationMode(), "Stop Play Mode and Timeline Preview before retiming.");
        var scene = LoadedIntro(); var master = Master(scene); var timeline = (TimelineAsset)master.playableAsset;
        var controls = Tracks(timeline).OfType<ControlTrack>().SelectMany(t => t.GetClips()).OrderBy(c => c.start).ToArray();
        Require(controls.Length == 3 && controls.All(c => c.clipIn == 0 && c.timeScale == 1), "Preserve the three natural-speed chapters.");
        var root = ((ControlPlayableAsset)controls[0].asset).sourceGameObject.Resolve(master);
        Require(root.name == "Chapter_01", "Chapter 1 must be first.");
        var director = root.GetComponent<PlayableDirector>(); var chapter = (TimelineAsset)director.playableAsset;
        var voice = Tracks(timeline).OfType<AudioTrack>().Single(t => t.name.StartsWith("Chapter_01 —", StringComparison.Ordinal));
        var speech = voice.GetClips().Single(c => c.displayName == "Panel_02 / Speech_01");
        var audio = ((AudioPlayableAsset)speech.asset).clip;
        Require(audio != null && AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(audio)) == "8773e6da4ecfbc847a87b289e80b4ea9", "Keep the new recording selected by the user, including its current renamed path.");
        var panels = Enumerable.Range(1, 5).Select(i => root.transform.Find("PageSafeArea/Page/Panel_" + i.ToString("00")).gameObject).ToArray();
        var activations = panels.Select(p => Tracks(chapter).OfType<ActivationTrack>().Single(t => Bound(director, t) == p)).ToArray();
        double oldBoundary = activations[2].GetClips().Single().start;
        double newSpeechEnd = speech.start + Length(audio);
        double delta = newSpeechEnd - controls[0].start + .35 - oldBoundary;
        Require(Math.Abs(delta) < .0001 || delta < 0, "This operation only closes the gap after the shorter recording.");
        if (Math.Abs(delta) < .0001)
        {
            string alignmentBackup = Output + "/Before-VoiceAlignment-" + DateTime.Now.ToString("yyyyMMdd-HHmmssfff"); Directory.CreateDirectory(alignmentBackup);
            File.Copy(AssetDatabase.GetAssetPath(timeline), alignmentBackup + "/MangaIntro_Master.playable");
            File.WriteAllText(alignmentBackup + "/Master.current.json", string.Join("\n\n", AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(timeline))
                .Select(o => o.GetType().Name + " " + o.name + "\n" + EditorJsonUtility.ToJson(o, true))));
            AlignLaterChapterOneVoices(master, director, chapter, voice, controls[0].start);
            EditorUtility.SetDirty(timeline); AssetDatabase.SaveAssetIfDirty(timeline); Validate(); return;
        }
        Require(newSpeechEnd - controls[0].start > activations[1].GetClips().Single().start, "The new voice still belongs inside Panel 2.");
        var fades = Tracks(timeline).OfType<AnimationTrack>().SelectMany(t => t.GetClips()).OrderBy(c => c.start).ToArray();
        Require(fades.Length == 3 && fades.All(c => c.timeScale == 1 && c.clipIn == 0), "Preserve the authored master fades.");
        var fadeAnimation = ((AnimationPlayableAsset)fades[0].asset).clip;
        var pageTrack = Tracks(chapter).OfType<AnimationTrack>().Single(t => Bound(director, t) == root);
        var pageClips = pageTrack.GetClips().ToArray();
        double oldFadeStart = pageClips.Single(c => c.displayName == "PageFadeOut").start;
        double newLength = controls[0].duration + delta;
        string backup = Output + "/Before-ShorterVoice-" + DateTime.Now.ToString("yyyyMMdd-HHmmssfff"); Directory.CreateDirectory(backup);
        File.Copy(ScenePath, backup + "/MangaIntro.disk.unity");
        Require(EditorSceneManager.SaveScene(scene, backup + "/MangaIntro.current.unity", true), "Could not preserve the current scene edits.");
        foreach (var asset in new UnityEngine.Object[] { timeline, chapter, fadeAnimation })
        {
            string path = AssetDatabase.GetAssetPath(asset); File.Copy(path, backup + "/" + Path.GetFileName(path));
            File.WriteAllText(backup + "/" + Path.GetFileName(path) + ".current.json", string.Join("\n\n", AssetDatabase.LoadAllAssetsAtPath(path)
                .Select(o => o.GetType().Name + " " + o.name + "\n" + EditorJsonUtility.ToJson(o, true))));
        }
        speech.duration = Length(audio); EditorUtility.SetDirty(speech.asset);
        foreach (var track in Tracks(chapter).OfType<AnimationTrack>())
        {
            var target = Bound(director, track);
            if (panels.Skip(2).Any(p => target != null && target.transform.IsChildOf(p.transform)))
                foreach (var clip in track.GetClips()) clip.start += delta;
            EditorUtility.SetDirty(track);
        }
        foreach (var clip in pageClips.Where(c => c.displayName == "PageFadeOut" || c.displayName.StartsWith("HoldFullPage", StringComparison.Ordinal))) clip.start += delta;
        for (int p = 0; p < activations.Length; p++)
        {
            var clip = activations[p].GetClips().Single();
            if (p >= 2) clip.start += delta;
            clip.duration = newLength - clip.start; EditorUtility.SetDirty(activations[p]);
        }
        foreach (var track in Tracks(timeline).OfType<AudioTrack>())
        {
            foreach (var clip in track.GetClips())
                if (track != voice || clip.start - controls[0].start >= oldBoundary - .00001) clip.start += delta;
            EditorUtility.SetDirty(track);
        }
        foreach (var binding in AnimationUtility.GetCurveBindings(fadeAnimation))
        {
            var curve = AnimationUtility.GetEditorCurve(fadeAnimation, binding); var keys = curve.keys;
            for (int k = 0; k < keys.Length; k++) if (keys[k].time >= oldFadeStart - .0001) keys[k].time += (float)delta;
            curve.keys = keys; AnimationUtility.SetEditorCurve(fadeAnimation, binding, curve);
        }
        var fadeSettings = AnimationUtility.GetAnimationClipSettings(fadeAnimation);
        fadeSettings.stopTime = (float)newLength; AnimationUtility.SetAnimationClipSettings(fadeAnimation, fadeSettings);
        controls[0].duration = newLength; fades[0].duration = newLength;
        for (int i = 1; i < controls.Length; i++) { controls[i].start += delta; fades[i].start += delta; }
        AlignLaterChapterOneVoices(master, director, chapter, voice, controls[0].start);
        foreach (var track in Tracks(timeline)) EditorUtility.SetDirty(track);
        EditorUtility.SetDirty(timeline); EditorUtility.SetDirty(chapter); EditorUtility.SetDirty(fadeAnimation);
        AssetDatabase.SaveAssetIfDirty(chapter); AssetDatabase.SaveAssetIfDirty(fadeAnimation); AssetDatabase.SaveAssetIfDirty(timeline);
        File.WriteAllText(Output + "/ShorterVoice.txt", "Backup: " + backup + "\nRecording: " + AssetDatabase.GetAssetPath(audio) +
            "\nVoice: " + Number(speech.start) + ".." + Number(speech.end) + "s\nShift after Panel 2: " + Number(delta) +
            "s\nChapter 1 end: " + Number(controls[0].end) + "s\nIntro end: " + Number(controls[2].end) + "s\nUser scene edits preserved; original scene was not saved.\n");
        UnityEditor.Timeline.TimelineEditor.Refresh(UnityEditor.Timeline.RefreshReason.ContentsModified);
        Validate();
    }

    static void AlignLaterChapterOneVoices(PlayableDirector master, PlayableDirector director, TimelineAsset chapter, AudioTrack voice, double chapterStart)
    {
        var report = new StringBuilder();
        for (int p = 3; p <= 5; p++)
        {
            string name = "Panel_" + p.ToString("00"); var dialogue = director.transform.Find("PageSafeArea/Page/" + name + "/Dialogue");
            var text = Tracks(chapter).OfType<AnimationTrack>().Single(t => Bound(director, t) == dialogue.gameObject);
            double cursor = chapterStart + text.GetClips().Max(c => c.end) + .05;
            foreach (var clip in voice.GetClips().Where(c => c.displayName.StartsWith(name + " / ", StringComparison.Ordinal)).OrderBy(c => c.start).ToArray())
            {
                report.AppendLine(clip.displayName + " " + Number(clip.start) + " → " + Number(cursor) + "s");
                clip.start = cursor; cursor = clip.end + .18;
            }
        }
        EditorUtility.SetDirty(voice); File.WriteAllText(Output + "/VoiceAlignment.txt", report.ToString());
        UnityEditor.Timeline.TimelineEditor.Refresh(UnityEditor.Timeline.RefreshReason.ContentsModified);
    }

    [MenuItem("Tools/Manga Intro/Add Supplied Dialogue Voices")]
    public static void Setup()
    {
        Require(!EditorApplication.isPlayingOrWillChangePlaymode && !AnimationMode.InAnimationMode(), "Stop Play Mode and Timeline Preview before editing the intro.");
        var scene = LoadedIntro(); var master = Master(scene); var timeline = (TimelineAsset)master.playableAsset;
        var controls = Tracks(timeline).OfType<ControlTrack>().SelectMany(t => t.GetClips()).OrderBy(c => c.start).ToArray();
        Require(controls.Length == 3, "Expected three chapter controls.");
        var roots = controls.Select(c => ((ControlPlayableAsset)c.asset).sourceGameObject.Resolve(master)).ToArray();
        var directors = roots.Select(r => r.GetComponent<PlayableDirector>()).ToArray();
        var chapters = directors.Select(d => (TimelineAsset)d.playableAsset).ToArray();
        Require(!Tracks(timeline).OfType<AudioTrack>().Any() && chapters.All(t => !Tracks(t).OfType<AudioTrack>().Any()), "Intro already has audio tracks. Edit the authored Timeline directly; this setup never overwrites voice edits.");
        Require(controls.All(c => Math.Abs(c.clipIn) < .00001 && c.timeScale > 0), "Chapter controls need Clip In 0 and positive speed.");
        Require(Enumerable.Range(1, 3).All(c => Enumerable.Range(1, 5).SelectMany(p => Voices(c, p)).Any()), "Each chapter needs supplied voice clips.");

        Directory.CreateDirectory(Output);
        string backup = Output + "/Before-" + DateTime.Now.ToString("yyyyMMdd-HHmmssfff"); Directory.CreateDirectory(backup);
        File.Copy(ScenePath, backup + "/MangaIntro.disk.unity");
        Require(EditorSceneManager.SaveScene(scene, backup + "/MangaIntro.current.unity", true), "Could not preserve current scene edits.");
        var assets = chapters.Concat(new[] { timeline }).Cast<UnityEngine.Object>().Concat(Tracks(timeline)
            .SelectMany(t => t.GetClips()).Select(c => (c.asset as AnimationPlayableAsset)?.clip).Where(c => c != null)).Distinct().ToArray();
        foreach (var asset in assets)
        {
            string path = AssetDatabase.GetAssetPath(asset);
            File.Copy(path, backup + "/" + Path.GetFileName(path));
            File.WriteAllText(backup + "/" + Path.GetFileName(path) + ".current.json", string.Join("\n\n", AssetDatabase.LoadAllAssetsAtPath(path)
                .Select(o => o.GetType().Name + " " + o.name + "\n" + EditorJsonUtility.ToJson(o, true))));
        }
        var report = new StringBuilder("Backup: " + backup + "\n");
        var gaps = controls.Skip(1).Select((c, i) => Math.Max(0, c.start - controls[i].end)).ToArray();
        double masterStart = controls[0].start;
        for (int chapter = 0; chapter < 3; chapter++)
        {
            var director = directors[chapter]; var child = chapters[chapter]; var control = controls[chapter];
            double speed = control.timeScale;
            var page = roots[chapter].transform.Find("PageSafeArea/Page");
            var panels = Enumerable.Range(1, 5).Select(i => page.Find("Panel_" + i.ToString("00")).gameObject).ToArray();
            var activations = panels.Select(p => Tracks(child).OfType<ActivationTrack>().Single(t => Bound(director, t) == p)).ToArray();
            var oldStarts = activations.Select(t => t.GetClips().Single().start / speed).ToArray();
            var pageTrack = Tracks(child).OfType<AnimationTrack>().Single(t => Bound(director, t) == roots[chapter]);
            var pageClips = pageTrack.GetClips().ToArray();
            var hold = pageClips.Single(c => c.displayName.StartsWith("HoldFullPage", StringComparison.Ordinal));
            var fadeOut = pageClips.Single(c => c.displayName == "PageFadeOut");
            double oldDuration = control.duration, oldHold = hold.start / speed, fadeDuration = fadeOut.duration / speed;
            var starts = new double[5]; var speechStarts = new double[5]; var speechEnds = new double[5];
            var voices = Enumerable.Range(1, 5).Select(p => Voices(chapter + 1, p)).ToArray();
            var track = child.CreateTrack<AudioTrack>(null, "Dialogue Voice — Panel / Speech");
            var sourceObject = new GameObject("Dialogue Voice"); Undo.RegisterCreatedObjectUndo(sourceObject, "Add manga dialogue voice");
            sourceObject.transform.SetParent(roots[chapter].transform, false);
            var source = sourceObject.AddComponent<AudioSource>(); source.playOnAwake = false; source.loop = false;
            source.spatialBlend = 0; source.volume = .9f; source.pitch = 1; source.priority = 64;
            sourceObject.AddComponent<GameAudioSourceBus>().bus = GameAudioBus.Voice;
            director.SetGenericBinding(track, source);
            starts[0] = oldStarts[0];
            for (int p = 0; p < 5; p++)
            {
                var dialogue = panels[p].transform.Find("Dialogue").gameObject;
                var textTrack = Tracks(child).OfType<AnimationTrack>().Single(t => Bound(director, t) == dialogue);
                double lead = Math.Max(.25, textTrack.GetClips().Max(c => c.end) / speed - oldStarts[p] + .05);
                double cursor = starts[p] + lead; speechStarts[p] = cursor;
                foreach (var audio in voices[p])
                {
                    Require(audio != null && audio.samples > 0 && audio.frequency > 0, "Voice clip could not be imported.");
                    var clip = track.CreateClip(audio); ((AudioPlayableAsset)clip.asset).loop = false;
                    clip.displayName = "Panel_" + (p + 1).ToString("00") + " / " + audio.name;
                    clip.start = cursor; clip.duration = Length(audio); clip.timeScale = 1; clip.clipIn = 0;
                    clip.easeInDuration = .01; clip.easeOutDuration = .025;
                    report.AppendLine(roots[chapter].name + " " + clip.displayName + " " + Number(cursor) + ".." + Number(clip.end) + "s");
                    cursor = clip.end + .18;
                }
                speechEnds[p] = voices[p].Length == 0 ? starts[p] : cursor - .18;
                if (p < 4) starts[p + 1] = Math.Max(starts[p] + oldStarts[p + 1] - oldStarts[p], speechEnds[p] + .35);
            }
            double lastShift = starts[4] - oldStarts[4];
            double length = Math.Max(oldDuration + lastShift, speechEnds[4] + .35 + fadeDuration);
            double holdStart = oldHold + lastShift;
            foreach (var animation in Tracks(child).OfType<AnimationTrack>())
            {
                var target = Bound(director, animation);
                int panel = Array.FindIndex(panels, p => target != null && target.transform.IsChildOf(p.transform));
                foreach (var clip in animation.GetClips())
                {
                    clip.start = clip.start / speed + (panel < 0 ? 0 : starts[panel] - oldStarts[panel]);
                    clip.duration /= speed; clip.timeScale *= speed;
                    clip.easeInDuration /= speed; clip.easeOutDuration /= speed;
                }
                EditorUtility.SetDirty(animation);
            }
            double holdSourceDuration = hold.duration * hold.timeScale;
            hold.start = holdStart; hold.duration = length - fadeDuration - holdStart; hold.timeScale = holdSourceDuration / hold.duration;
            fadeOut.start = length - fadeDuration;
            for (int p = 0; p < 5; p++)
            {
                var clip = activations[p].GetClips().Single(); clip.start = starts[p]; clip.duration = length - starts[p];
                EditorUtility.SetDirty(activations[p]);
            }
            child.durationMode = TimelineAsset.DurationMode.BasedOnClips;
            var fade = Tracks(timeline).OfType<AnimationTrack>().SelectMany(t => t.GetClips()).Single(c =>
                c.asset is AnimationPlayableAsset a && a.clip != null && a.clip.name == "Master_Fade_" + roots[chapter].name);
            var fadeAsset = ((AnimationPlayableAsset)fade.asset).clip;
            foreach (var binding in AnimationUtility.GetCurveBindings(fadeAsset))
            {
                var curve = AnimationUtility.GetEditorCurve(fadeAsset, binding); double oldLength = fade.duration;
                double rate = fade.timeScale, oldTail = oldLength - fadeDuration;
                var keys = curve.keys;
                for (int k = 0; k < keys.Length; k++)
                {
                    double time = keys[k].time / rate;
                    keys[k].time = (float)(time >= oldTail - .0001 ? time + length - oldLength : time);
                    keys[k].inTangent *= (float)rate; keys[k].outTangent *= (float)rate;
                }
                curve.keys = keys; AnimationUtility.SetEditorCurve(fadeAsset, binding, curve);
            }
            fade.start = masterStart; fade.duration = length; fade.clipIn = 0; fade.timeScale = 1;
            control.start = masterStart; control.duration = length; control.timeScale = 1;
            masterStart = control.end + (chapter < 2 ? gaps[chapter] : 0);
            EditorUtility.SetDirty(track); EditorUtility.SetDirty(child); EditorUtility.SetDirty(director); EditorUtility.SetDirty(fadeAsset);
            AssetDatabase.SaveAssetIfDirty(child); AssetDatabase.SaveAssetIfDirty(fadeAsset);
            report.AppendLine(roots[chapter].name + " duration=" + Number(length) + "s; panel starts=" + string.Join(", ", starts.Select(Number)));
        }
        timeline.durationMode = TimelineAsset.DurationMode.BasedOnClips;
        foreach (var track in Tracks(timeline)) EditorUtility.SetDirty(track);
        EditorUtility.SetDirty(timeline); EditorUtility.SetDirty(master); AssetDatabase.SaveAssetIfDirty(timeline);
        BindChapters(master, directors);
        EditorSceneManager.MarkSceneDirty(scene); Require(EditorSceneManager.SaveScene(scene), "Could not save the updated intro.");
        File.WriteAllText(Output + "/Setup.txt", report.ToString());
        FixAudioScheduling();
    }

    [MenuItem("Tools/Manga Intro/Repair Dialogue Audio Scheduling")]
    public static void FixAudioScheduling()
    {
        Require(!EditorApplication.isPlayingOrWillChangePlaymode && !AnimationMode.InAnimationMode(), "Stop Play Mode and Timeline Preview before changing audio scheduling.");
        var scene = LoadedIntro(); var master = Master(scene); var timeline = (TimelineAsset)master.playableAsset;
        var controls = Tracks(timeline).OfType<ControlTrack>().SelectMany(t => t.GetClips()).OrderBy(c => c.start).ToArray();
        Require(controls.Length == 3 && !Tracks(timeline).OfType<AudioTrack>().Any(), "Repair expects the original three nested voice tracks and no existing master audio tracks.");
        var directors = controls.Select(c => ((ControlPlayableAsset)c.asset).sourceGameObject.Resolve(master).GetComponent<PlayableDirector>()).ToArray();
        var chapters = directors.Select(d => (TimelineAsset)d.playableAsset).ToArray();
        Require(controls.All(c => c.timeScale == 1 && c.clipIn == 0), "Keep the current natural-speed narration and chapter timings.");
        var voices = chapters.Select(t => Tracks(t).OfType<AudioTrack>().Single()).ToArray();
        Require(voices.All(t => !t.hasCurves && t.GetClips().All(c => c.curves == null)), "Animated audio gain curves require a separate migration; preserve them before proceeding.");
        string backup = Output + "/Before-DspScheduling-" + DateTime.Now.ToString("yyyyMMdd-HHmmssfff"); Directory.CreateDirectory(backup);
        File.Copy(ScenePath, backup + "/MangaIntro.disk.unity");
        Require(EditorSceneManager.SaveScene(scene, backup + "/MangaIntro.current.unity", true), "Could not preserve current intro scene edits.");
        foreach (var asset in chapters.Concat(new[] { timeline }))
        {
            string path = AssetDatabase.GetAssetPath(asset); File.Copy(path, backup + "/" + Path.GetFileName(path));
            File.WriteAllText(backup + "/" + Path.GetFileName(path) + ".current.json", string.Join("\n\n", AssetDatabase.LoadAllAssetsAtPath(path)
                .Select(o => o.GetType().Name + " " + o.name + "\n" + EditorJsonUtility.ToJson(o, true))));
        }
        var report = new StringBuilder("Backup: " + backup + "\n");
        report.AppendLine("Root cause: child directors use Manual clock. DirectorControlPlayable.ProcessFrame evaluates them on time resync; ScheduleRuntimeClip treats Evaluate as forceSeek and reschedules audio. Move narration to the autonomous master DSP graph.");
        var group = timeline.CreateTrack<GroupTrack>(null, "Dialogue Voices — DSP");
        for (int chapter = 0; chapter < chapters.Length; chapter++)
        {
            var oldTrack = voices[chapter]; var source = directors[chapter].GetGenericBinding(oldTrack) as AudioSource;
            Require(source != null, "Preserve the existing Voice source binding.");
            var track = timeline.CreateTrack<AudioTrack>(group, directors[chapter].name + " — Dialogue Voice (Panel / Speech)");
            var original = new SerializedObject(oldTrack); var copied = new SerializedObject(track);
            foreach (string field in new[] { "volume", "stereoPan", "spatialBlend" })
                copied.FindProperty("m_TrackProperties." + field).floatValue = original.FindProperty("m_TrackProperties." + field).floatValue;
            copied.ApplyModifiedPropertiesWithoutUndo(); track.muted = oldTrack.muted;
            foreach (var previous in oldTrack.GetClips().OrderBy(c => c.start))
            {
                var asset = (AudioPlayableAsset)previous.asset; var clip = track.CreateClip(asset.clip);
                EditorUtility.CopySerialized(asset, clip.asset);
                clip.displayName = previous.displayName; clip.start = controls[chapter].start + previous.start;
                clip.duration = previous.duration; clip.clipIn = previous.clipIn; clip.timeScale = previous.timeScale;
                clip.easeInDuration = previous.easeInDuration; clip.easeOutDuration = previous.easeOutDuration;
                report.AppendLine(directors[chapter].name + " " + clip.displayName + " master " + Number(clip.start) + ".." + Number(clip.end) + "s");
            }
            master.SetGenericBinding(track, source);
            directors[chapter].ClearGenericBinding(oldTrack); chapters[chapter].DeleteTrack(oldTrack);
            EditorUtility.SetDirty(directors[chapter]); EditorUtility.SetDirty(chapters[chapter]); EditorUtility.SetDirty(track);
            AssetDatabase.SaveAssetIfDirty(chapters[chapter]);
        }
        master.timeUpdateMode = DirectorUpdateMode.DSPClock;
        EditorUtility.SetDirty(group); EditorUtility.SetDirty(timeline); EditorUtility.SetDirty(master);
        AssetDatabase.SaveAssetIfDirty(timeline); EditorSceneManager.MarkSceneDirty(scene);
        Require(EditorSceneManager.SaveScene(scene), "Could not save DSP voice scheduling.");
        File.WriteAllText(Output + "/DspScheduling.txt", report.ToString());
        InspectAudioData(); Validate();
    }

    static void InspectAudioData()
    {
        var report = new StringBuilder();
        foreach (var path in Directory.GetFiles("Assets/asset/intro", "Speech_*.mp3", SearchOption.AllDirectories).OrderBy(p => p))
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path.Replace('\\', '/'));
            Require(clip.LoadAudioData(), "Audio data must load for inspection: " + path);
            var samples = new float[clip.samples * clip.channels]; Require(clip.GetData(samples, 0), "Audio must decode successfully: " + path);
            double peak = 0, sum = 0; int clipped = 0, invalid = 0;
            foreach (float value in samples)
            {
                if (!float.IsFinite(value)) { invalid++; continue; }
                peak = Math.Max(peak, Math.Abs(value)); sum += value * value;
                if (Math.Abs(value) >= .9999f) clipped++;
            }
            Require(invalid == 0, "Recording contains invalid samples: " + path);
            report.AppendLine(path + ": " + clip.frequency + "Hz, channels=" + clip.channels + ", peak=" + Number(peak) +
                ", RMS=" + Number(Math.Sqrt(sum / samples.Length)) + ", full-scale samples=" + clipped + "/" + samples.Length);
        }
        File.WriteAllText(Output + "/AudioData.txt", report.ToString());
    }

    static void BindChapters(PlayableDirector master, PlayableDirector[] chapters)
    {
        var skip = master.GetComponent<MangaIntroSkip>(); Require(skip != null, "Master needs its existing skip component.");
        var serialized = new SerializedObject(skip); var field = serialized.FindProperty("chapterDirectors");
        field.arraySize = chapters.Length;
        for (int i = 0; i < chapters.Length; i++) field.GetArrayElementAtIndex(i).objectReferenceValue = chapters[i];
        serialized.ApplyModifiedProperties();
    }
    static void BindStopHandler()
    {
        var scene = LoadedIntro(); var master = Master(scene);
        string backup = Output + "/Before-StopBinding-" + DateTime.Now.ToString("yyyyMMdd-HHmmssfff"); Directory.CreateDirectory(backup);
        File.Copy(ScenePath, backup + "/MangaIntro.disk.unity");
        Require(EditorSceneManager.SaveScene(scene, backup + "/MangaIntro.current.unity", true), "Could not preserve the intro before binding skip cleanup.");
        var chapters = Tracks((TimelineAsset)master.playableAsset).OfType<ControlTrack>().SelectMany(t => t.GetClips())
            .Select(c => ((ControlPlayableAsset)c.asset).sourceGameObject.Resolve(master).GetComponent<PlayableDirector>()).ToArray();
        BindChapters(master, chapters); EditorSceneManager.MarkSceneDirty(scene); Require(EditorSceneManager.SaveScene(scene), "Could not save chapter stop bindings.");
        Validate();
    }

    [MenuItem("Tools/Manga Intro/Validate Dialogue Voices")]
    public static void Validate()
    {
        Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Validate in Edit Mode.");
        var active = SceneManager.GetActiveScene(); var report = new StringBuilder();
        var scene = EditorSceneManager.OpenPreviewScene(ScenePath); PlayableDirector master = null; MangaIntroSkip skip = null;
        try
        {
            master = Master(scene); var timeline = (TimelineAsset)master.playableAsset;
            Require(master.timeUpdateMode == DirectorUpdateMode.DSPClock, "Narration must use the master DSP clock, not a manually evaluated child graph.");
            skip = master.GetComponent<MangaIntroSkip>();
            Require(skip != null && new SerializedObject(skip).FindProperty("chapterDirectors").arraySize == 3, "Skip cleanup is bound to all three chapters.");
            // Normal MonoBehaviour lifecycle does not run in an Edit Mode preview.
            // Subscribe the real skip handler explicitly to exercise the actual stopped event.
            PreviewSkipLifecycle(skip, true);
            var controls = Tracks(timeline).OfType<ControlTrack>().SelectMany(t => t.GetClips()).OrderBy(c => c.start).ToArray();
            Require(controls.Length == 3, "Three chapter controls remain bound.");
            var used = new System.Collections.Generic.HashSet<string>();
            var slots = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var control in controls)
            {
                var root = ((ControlPlayableAsset)control.asset).sourceGameObject.Resolve(master);
                var director = root.GetComponent<PlayableDirector>(); var chapter = (TimelineAsset)director.playableAsset;
                Require(control.timeScale == 1 && control.clipIn == 0 && Math.Abs(control.duration - chapter.duration) < .001, "Child narration runs at natural speed and is not cut off.");
                Require(!Tracks(chapter).OfType<AudioTrack>().Any(), "Manual child directors contain only visuals, so resync cannot restart voice playback.");
                var voice = Tracks(timeline).OfType<AudioTrack>().Single(t => t.name.StartsWith(root.name + " —", StringComparison.Ordinal));
                var source = master.GetGenericBinding(voice) as AudioSource;
                Require(source != null && !source.playOnAwake && !source.loop && source.spatialBlend == 0 && source.GetComponent<GameAudioSourceBus>().bus == GameAudioBus.Voice, "Voice uses a bound 2D source and the Voice volume category.");
                var clips = voice.GetClips().OrderBy(c => c.start).ToArray();
                var fade = Tracks(chapter).OfType<AnimationTrack>().Where(t => Bound(director, t) == root).SelectMany(t => t.GetClips()).Single(c => c.displayName == "PageFadeOut");
                for (int i = 0; i < clips.Length; i++)
                {
                    var clip = clips[i]; var asset = (AudioPlayableAsset)clip.asset; string path = AssetDatabase.GetAssetPath(asset.clip);
                    Require(slots.Add(root.name + "/" + clip.displayName.Replace(" / ", "/")), "Each dialogue slot is assigned once, even when its recording is replaced or renamed.");
                    Require(used.Add(path) && !asset.loop && clip.clipIn == 0 && clip.timeScale == 1 && Math.Abs(clip.duration - Length(asset.clip)) < .0001, "Every supplied recording plays once, in full, with natural pitch.");
                    Require(i == 0 || clips[i - 1].end <= clip.start, "Speech clips never overlap.");
                    Require(clip.end - control.start < fade.start, "The entire voice finishes before fading out.");
                    var panel = root.transform.Find("PageSafeArea/Page/" + clip.displayName.Split(' ')[0]);
                    var activation = Tracks(chapter).OfType<ActivationTrack>().Single(t => Bound(director, t) == panel.gameObject).GetClips().Single();
                    var next = Tracks(chapter).OfType<ActivationTrack>().SelectMany(t => t.GetClips()).Where(c => c.start > activation.start).OrderBy(c => c.start).FirstOrDefault();
                    Require(clip.start - control.start > activation.start && (next == null || clip.end - control.start < next.start), "The correct panel remains the newest panel for the full speech.");
                    master.time = clip.start + clip.duration * .5; master.Evaluate();
                    Require(root.activeSelf && panel.gameObject.activeInHierarchy && Math.Abs(director.time - (master.time - control.start)) < .04, "Native nested directors synchronize voice and active panel.");
                    var graph = master.playableGraph;
                    Require(Enumerable.Range(0, graph.GetOutputCount()).Select(graph.GetOutput).Any(o => o.IsPlayableOutputOfType<AudioPlayableOutput>() && ((AudioPlayableOutput)o).GetTarget() == source), "Native audio graph outputs to the saved Voice AudioSource.");
                    var audioPlayable = GraphPlayables(graph).Where(p => p.GetPlayableType() == typeof(AudioClipPlayable)).Select(p => (AudioClipPlayable)p).Single(p => p.GetClip() == asset.clip);
                    Require(Math.Abs(audioPlayable.GetTime() - clip.duration * .5) < .04, "Audio playhead matches the spoken line when seeking.");
                }
                report.AppendLine("PASS " + root.name + ": " + clips.Length + " voices, " + Number(control.start) + ".." + Number(control.end) + "s; full recordings, correct panel, native master DSP audio output and seek timing, Voice bus. No audio in the manual child graph.");
            }
            var suppliedSlots = Directory.GetFiles("Assets/asset/intro", "Speech_*.mp3", SearchOption.AllDirectories).Select(p =>
            {
                var parts = p.Replace('\\', '/').Split('/');
                return parts[3] + "/" + parts[5] + "/" + Path.GetFileNameWithoutExtension(parts[6]);
            }).ToArray();
            Require(slots.SetEquals(suppliedSlots) && Tracks(timeline).OfType<AudioTrack>().Count() == 3, "All dialogue slots are assigned exactly once across the three master chapter voice tracks; replacement recordings are allowed.");
            for (int i = 1; i < controls.Length; i++) Require(controls[i - 1].end <= controls[i].start, "Chapters do not overlap.");
            // Evaluate-only scrubbing never invokes OnGraphStart. Play the isolated graph
            // before Stop so native activation post-playback and audio cleanup are exercised.
            master.Play(); master.Evaluate(); master.Stop();
            var roots = controls.Select(c => ((ControlPlayableAsset)c.asset).sourceGameObject.Resolve(master)).ToArray();
            foreach (var root in roots)
                report.AppendLine("STOP " + root.name + " active=" + root.activeSelf + " director=" + root.GetComponent<PlayableDirector>().state +
                    " graphValid=" + root.GetComponent<PlayableDirector>().playableGraph.IsValid() + " sourcePlaying=" + root.GetComponentsInChildren<AudioSource>(true).Any(s => s.isPlaying));
            File.WriteAllText(Output + "/PlaybackState.txt", report.ToString());
            Require(roots.All(r => !r.activeSelf && r.GetComponent<PlayableDirector>().state != PlayState.Playing && r.GetComponentsInChildren<AudioSource>(true).All(s => !s.isPlaying)), "Stopping/skipping the master hides every chapter and leaves no playing narration.");
            report.AppendLine("PASS all " + used.Count + " recordings are assigned once. Master Stop hides all chapters and leaves no playing child director or narration source. Edit Mode preview only; no campaign save writes.");
        }
        finally
        {
            if (master != null) master.Stop();
            if (skip != null) PreviewSkipLifecycle(skip, false);
            EditorSceneManager.ClosePreviewScene(scene);
            Require(SceneManager.GetActiveScene() == active, "User active scene remains unchanged.");
        }
        Directory.CreateDirectory(Output); File.WriteAllText(Output + "/Validation.txt", report.ToString());
        Debug.Log("Manga dialogue voices PASS: " + Output + "/Validation.txt");
    }
    static System.Collections.Generic.IEnumerable<Playable> GraphPlayables(PlayableGraph graph)
    {
        var seen = new System.Collections.Generic.HashSet<Playable>();
        var pending = new System.Collections.Generic.Stack<Playable>();
        for (int i = 0; i < graph.GetRootPlayableCount(); i++) pending.Push(graph.GetRootPlayable(i));
        while (pending.Count > 0)
        {
            var playable = pending.Pop(); if (!playable.IsValid() || !seen.Add(playable)) continue;
            yield return playable;
            for (int i = 0; i < playable.GetInputCount(); i++) pending.Push(playable.GetInput(i));
        }
    }
    internal static void PreviewSkipLifecycle(MangaIntroSkip skip, bool enable) => typeof(MangaIntroSkip)
        .GetMethod(enable ? "OnEnable" : "OnDisable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(skip, null);

    [MenuItem("Tools/Manga Intro/Inspect Dialogue Voices")]
    public static void Inspect()
    {
        var scene = LoadedIntro(); var master = Master(scene); var report = new StringBuilder();
        report.AppendLine("Current intro scene, including unsaved edits. Dirty=" + scene.isDirty);
        var controls = Tracks((TimelineAsset)master.playableAsset).OfType<ControlTrack>()
            .SelectMany(t => t.GetClips()).OrderBy(c => c.start).ToArray();
        foreach (var control in controls)
        {
            var root = ((ControlPlayableAsset)control.asset).sourceGameObject.Resolve(master);
            var director = root.GetComponent<PlayableDirector>(); var timeline = (TimelineAsset)director.playableAsset;
            report.AppendLine(root.name + " master=" + Number(control.start) + ".." + Number(control.end) +
                " speed=" + control.timeScale + " childDuration=" + Number(timeline.duration) + " dirtyTimeline=" + EditorUtility.IsDirty(timeline));
            foreach (var track in Tracks(timeline))
            {
                var binding = director.GetGenericBinding(track);
                report.AppendLine("  " + track.GetType().Name + " " + track.name + " bind=" + binding?.name +
                    " | " + string.Join("; ", track.GetClips().Select(c => c.displayName + " " + Number(c.start) + ".." + Number(c.end) + " speed=" + c.timeScale)));
            }
        }
        foreach (var path in Directory.GetFiles("Assets/asset/intro", "*.mp3", SearchOption.AllDirectories).OrderBy(p => p))
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path.Replace('\\', '/'));
            report.AppendLine("AUDIO " + path + " = " + (clip == null ? "MISSING" : Number((double)clip.samples / clip.frequency)));
        }
        Directory.CreateDirectory(Output); File.WriteAllText(Output + "/Inspection.txt", report.ToString());
        Debug.Log("Manga dialogue voice inspection: " + Output + "/Inspection.txt");
    }
}

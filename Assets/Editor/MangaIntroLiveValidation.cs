#if UNITY_EDITOR
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;

/// <summary>Temporary, explicitly requested real Play Mode smoke test. Never saves scenes or changes gameplay data.</summary>
[InitializeOnLoad]
public static class MangaIntroLiveValidation
{
    const string ScenePath = "Assets/Scenes/MangaIntro.unity";
    const string RequestPath = "Library/MangaIntroLiveValidation.request";
    const string ResultPath = "Library/MangaIntroLiveValidation.result.txt";
    const string Key = "MangaIntroLiveValidation.";
    const string Entering = "Entering";
    const string Waiting = "Waiting";
    const string WaitingForStop = "WaitingForStop";
    const string Exiting = "Exiting";

    static MangaIntroLiveValidation()
    {
        EditorApplication.update += Update;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    static string Phase { get { return SessionState.GetString(Key + "Phase", ""); } set { SessionState.SetString(Key + "Phase", value); } }

    static void Update()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        string phase = Phase;
        try
        {
            if (phase.Length == 0)
            {
                if (File.Exists(RequestPath)) BeginRequested();
                return;
            }
            if (phase == Entering)
            {
                if (EditorApplication.isPlaying) StartWaiting();
                else if (Elapsed("BeginUtc") > 45) FailAndExit("Timed out while entering Play Mode.");
                return;
            }
            if (phase == Waiting)
            {
                if (!EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    FailAndExit("Play Mode ended before the real-time autoplay checks completed.");
                    return;
                }
                if (EditorApplication.isPlaying && Elapsed("PlayUtc") >= 2) CheckPlaybackAndExit();
                return;
            }
            if (phase == WaitingForStop)
            {
                Require(EditorApplication.isPlaying, "Play Mode ended before deferred Master.Stop cleanup was checked.");
                CheckDeferredStop();
                return;
            }
            if (phase == Exiting && !EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isPlaying)
                FinishInEditMode();
        }
        catch (Exception exception) { FailAndExit(exception.ToString()); }
    }

    static void BeginRequested()
    {
        File.Delete(RequestPath);
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isPlaying)
        {
            WriteResult("BLOCKED: Start this opt-in check in Edit Mode. The existing Play Mode session was left unchanged.");
            return;
        }
        var dirty = Enumerable.Range(0, SceneManager.sceneCount).Select(SceneManager.GetSceneAt)
            .Where(scene => scene.isLoaded && scene.isDirty).Select(scene => string.IsNullOrEmpty(scene.path) ? scene.name : scene.path).ToArray();
        if (dirty.Length > 0)
        {
            WriteResult("BLOCKED: Unsaved changes in loaded scenes: " + string.Join(", ", dirty) +
                ". No scene was opened, closed, saved or cleared. Save or discard those changes before requesting the test again.");
            return;
        }
        if (!File.Exists(ScenePath))
        {
            WriteResult("FAIL: Saved intro scene is missing: " + ScenePath);
            return;
        }
        SessionState.SetString(Key + "Report", "Actual Play Mode probe of " + ScenePath + "\nUTC: " + DateTime.UtcNow.ToString("O") + "\n");
        SessionState.SetBool(Key + "Passed", false);
        SessionState.SetString(Key + "BeginUtc", DateTime.UtcNow.ToString("O"));
        Phase = Entering;
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        EditorApplication.EnterPlaymode();
    }

    static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (Phase == Entering && state == PlayModeStateChange.EnteredPlayMode) StartWaiting();
        // Finishing runs from update after the editor has restored the saved scene following the domain reload.
    }

    static void StartWaiting()
    {
        SessionState.SetString(Key + "PlayUtc", DateTime.UtcNow.ToString("O"));
        Phase = Waiting;
    }

    static void CheckPlaybackAndExit()
    {
        PlayableDirector master = FindMaster();
        Require(master.playOnAwake, "Master Play On Awake is disabled.");
        Require(master.state == PlayState.Playing && master.time > .5,
            "Master did not advance through its native autoplay after two real-time seconds (state=" + master.state + ", time=" + Number(master.time) + ").");
        RequireOnlyChapter(1);
        Append("PASS: Native autoplay after " + Number(Elapsed("PlayUtc")) + " real-time seconds; master t=" + Number(master.time) + "s; only Chapter_01 active.\n");
        master.Pause();
        master.time = 36;
        master.Evaluate();
        RequireOnlyChapter(2);
        Append("PASS: Paused master seek to 36s activates only Chapter_02.\n");
        master.time = 70;
        master.Evaluate();
        RequireOnlyChapter(3);
        Append("PASS: Paused master seek to 70s activates only Chapter_03.\n");
        master.Stop();
        // PlayableGraph destruction and its post-playback activation cleanup can finish at the end of an engine frame.
        SessionState.SetString(Key + "StopUtc", DateTime.UtcNow.ToString("O"));
        SessionState.SetInt(Key + "StopUpdates", 0);
        Phase = WaitingForStop;
    }

    static void CheckDeferredStop()
    {
        int updates = SessionState.GetInt(Key + "StopUpdates", 0) + 1;
        SessionState.SetInt(Key + "StopUpdates", updates);
        double elapsed = Elapsed("StopUtc");
        if (updates < 2 || elapsed < .15) return;
        try { RequireOnlyChapter(0); }
        catch (InvalidOperationException)
        {
            if (elapsed < 2) return;
            throw;
        }
        Append("PASS: Native Master.Stop cleanup leaves all three chapters inactive after " + Number(elapsed) +
            " real-time seconds and " + updates + " editor updates; no validator deactivation was applied.\n");
        SessionState.SetBool(Key + "Passed", true);
        Phase = Exiting;
        EditorApplication.ExitPlaymode();
    }

    static void RequireOnlyChapter(int expected)
    {
        Scene scene = SceneManager.GetActiveScene();
        Require(scene.path == ScenePath, "The active scene changed during validation.");
        var canvas = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Canvas>(true))
            .Single(c => c.name == "MangaCanvas");
        for (int i = 1; i <= 3; i++)
        {
            var chapter = canvas.transform.Find("Chapter_" + i.ToString("00"));
            Require(chapter != null && chapter.gameObject.activeInHierarchy == (i == expected),
                "Expected only chapter " + expected + " active; Chapter_" + i.ToString("00") + " has an incorrect active state.");
        }
    }

    static PlayableDirector FindMaster()
    {
        Scene scene = SceneManager.GetActiveScene();
        Require(scene.path == ScenePath, "MangaIntro must be the active scene.");
        var master = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<PlayableDirector>(true))
            .SingleOrDefault(director => director.name == "MangaIntro_Master");
        Require(master != null, "MangaIntro_Master director is missing.");
        return master;
    }

    static void FailAndExit(string message)
    {
        Append("FAIL: " + message + "\n");
        SessionState.SetBool(Key + "Passed", false);
        Phase = Exiting;
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isPlaying) EditorApplication.ExitPlaymode();
        else FinishInEditMode();
    }

    static void FinishInEditMode()
    {
        bool passed = SessionState.GetBool(Key + "Passed", false);
        // Do not reopen or save anything here: exiting Play Mode restores the saved scene automatically.
        try
        {
            PlayableDirector master = FindMaster();
            Selection.activeGameObject = master.gameObject;
            EditorApplication.ExecuteMenuItem("Window/Sequencing/Timeline");
            Require(!SceneManager.GetActiveScene().isDirty, "Intro became dirty after exiting Play Mode.");
            Append("Returned to Edit Mode with MangaIntro open, its master selected and the native Timeline window open. No scene or asset was saved.\n");
        }
        catch (Exception exception) { passed = false; Append("FAIL: Edit Mode return: " + exception.Message + "\n"); }
        string report = (passed ? "PASS" : "FAIL") + ": Manga intro actual Play Mode smoke test.\n" + SessionState.GetString(Key + "Report", "") +
            "Elapsed: " + Number(Elapsed("BeginUtc")) + " real-time seconds.\n";
        Phase = "";
        WriteResult(report);
    }

    static double Elapsed(string name)
    {
        string value = SessionState.GetString(Key + name, "");
        return DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime start)
            ? (DateTime.UtcNow - start.ToUniversalTime()).TotalSeconds : 0;
    }
    static void Append(string text) { SessionState.SetString(Key + "Report", SessionState.GetString(Key + "Report", "") + text); }
    static string Number(double value) { return value.ToString("0.000", CultureInfo.InvariantCulture); }
    static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    static void WriteResult(string text)
    {
        Directory.CreateDirectory("Library");
        File.WriteAllText(ResultPath, text);
        if (text.StartsWith("PASS", StringComparison.Ordinal)) Debug.Log("Manga intro live validation PASS. " + ResultPath);
        else Debug.LogWarning("Manga intro live validation: " + ResultPath + "\n" + text);
    }
}
#endif

using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Explicit one-shot smoke test; never runs automatically in ordinary project use.
[InitializeOnLoad]
public static class MapStreamingSmokeTest
{
    const string Key = "Adventure.StreamingSmoke";
    const string Result = "Library/CombatValidation/MapStreamingSmoke.result.txt";
    static MapStreamingSmokeTest() { EditorApplication.update += Tick; }
    static void Tick()
    {
        const string request = "Library/CombatValidation/MapStreamingSmoke.request";
        if (File.Exists(request) && !EditorApplication.isCompiling && !EditorApplication.isUpdating)
        {
            File.Delete(request);
            if (EditorApplication.isPlayingOrWillChangePlaymode || Enumerable.Range(0,SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty))
            { File.WriteAllText(Result, "SKIPPED: editor already playing or has unsaved scenes."); return; }
            if (SceneManager.GetActiveScene().path != "Assets/Scenes/mapgame.unity" || SceneManager.sceneCount != 1)
            { File.WriteAllText(Result, "SKIPPED: open only mapgame before testing."); return; }
            SessionState.SetBool(Key, true);
            SessionState.SetString(Key + "Time", DateTime.UtcNow.ToString("O"));
            EditorApplication.isPlaying = true;
        }
        if (!SessionState.GetBool(Key, false)) return;
        if ((DateTime.UtcNow - DateTime.Parse(SessionState.GetString(Key + "Time", DateTime.UtcNow.ToString("O")))).TotalSeconds > 120)
        { Finish("FAIL: timed out waiting for streaming."); return; }
        if (!EditorApplication.isPlaying) return;
        var session = CampaignSession.Instance;
        if (session != null) { session.enabled = false; if (session.MapPlayer != null) session.MapPlayer.enabled = false; }
        var streamer = MapChunkStreamer.Instance;
        if (streamer == null) return;
        if (streamer.Error != null) { Finish("FAIL: " + streamer.Error); return; }
        if (!streamer.Ready || streamer.Blocked || session == null || session.MapPlayer == null) return;
        var player = session.MapPlayer;
        int expected = streamer.chunks.Count(x => MapChunkStreamer.Distance(x, player.transform.position) <= streamer.loadDistance);
        int loaded = streamer.chunks.Count(x => SceneManager.GetSceneByPath(x.scenePath).isLoaded);
        int mask = player.surfaceLayer.value != 0 ? player.surfaceLayer.value : Physics.DefaultRaycastLayers;
        bool ground = Physics.Raycast(player.transform.position + Vector3.up * 2, Vector3.down, out var hit, 15, mask, QueryTriggerInteraction.Ignore);
        Finish((loaded >= expected && ground ? "PASS" : "FAIL") + ": async startup completed; loaded " + loaded + "/" + streamer.chunks.Count +
            "; expected " + expected + "; movement gate released; ground collider " + (ground ? hit.collider.name : "NOT FOUND") + "; player " + player.transform.position);
    }
    static void Finish(string message)
    {
        File.WriteAllText(Result, message);
        SessionState.SetBool(Key, false);
        if (EditorApplication.isPlayingOrWillChangePlaymode) EditorApplication.isPlaying = false;
        Debug.Log(message);
    }
}

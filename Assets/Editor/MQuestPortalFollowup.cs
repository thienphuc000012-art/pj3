using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Small scene adjustment plus isolated first-visit audio checks. No Play Mode or player save writes.</summary>
public static class MQuestPortalFollowup
{
    const string Output = "Tools/MQuestPortal";
    const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
    const BindingFlags Static = BindingFlags.NonPublic | BindingFlags.Static;
    static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    static void Set(object owner, string name, object value) => owner.GetType().GetField(name, Private).SetValue(owner, value);
    [MenuItem("Tools/Adventure/Main Quest/Enlarge Ground Lightning")]
    public static void Setup()
    {
        Check(!EditorApplication.isPlayingOrWillChangePlaymode, "Use Edit Mode.");
        var scene = SceneManager.GetSceneByPath("Assets/Scenes/m.unity");
        if (!scene.IsValid() || !scene.isLoaded) scene = EditorSceneManager.OpenScene("Assets/Scenes/m.unity", OpenSceneMode.Additive);
        var controller = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MQuestPortalSequence>(true)).Single();
        Check(controller.transition != null && controller.transition.name == "Discovery Area" && controller.lightningAnchor != null, "Existing Discovery Area and lightning anchor required.");
        string backup = Output + "/Before-Voice-GroundLightning-" + DateTime.Now.ToString("yyyyMMdd-HHmmssfff"); Directory.CreateDirectory(backup);
        File.Copy(scene.path, backup + "/m.disk.unity"); Check(EditorSceneManager.SaveScene(scene, backup + "/m.current.unity", true), "Back up unsaved scene first.");
        controller.lightningAnchor.SetParent(controller.transition.transform, false);
        controller.lightningAnchor.localPosition = Vector3.zero; controller.lightningAnchor.localRotation = Quaternion.identity;
        controller.zapScale = Vector3.one * 3f;
        EditorUtility.SetDirty(controller); EditorUtility.SetDirty(controller.lightningAnchor); EditorSceneManager.MarkSceneDirty(scene);
        Check(EditorSceneManager.SaveScene(scene), "Save adjusted lightning.");
        File.WriteAllText(Output + "/Followup-Setup.txt", "Backup: " + backup + "\nLightning parent: " + controller.transition.name + "\nImpact center: " + controller.lightningAnchor.position.ToString("F3") + "\nZap scale: " + controller.zapScale + "\nRest voice slots are available on every RestPoint WorldInteraction; audio intentionally unassigned.\n");
        Validate();
    }
    [MenuItem("Tools/Adventure/Main Quest/Validate Ground Lightning and Rest Voices")]
    public static void Validate()
    {
        Check(!EditorApplication.isPlayingOrWillChangePlaymode, "Use Edit Mode."); Directory.CreateDirectory(Output);
        var active = SceneManager.GetActiveScene(); bool wasDirty = active.isDirty;
        var preview = EditorSceneManager.OpenPreviewScene("Assets/Scenes/m.unity"); var report = new StringBuilder();
        GameObject lightning = null;
        try
        {
            var controller = preview.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MQuestPortalSequence>(true)).Single();
            Check(controller.lightningAnchor.parent == controller.transition.transform && controller.lightningAnchor.localPosition == Vector3.zero, "Impact must follow the Discovery Area center below the portal.");
            Check(controller.zapScale == Vector3.one * 3, "Lightning must be enlarged to 3x.");
            lightning = (GameObject)typeof(MQuestPortalSequence).GetMethod("SpawnLightning", Private).Invoke(controller, null);
            SceneManager.MoveGameObjectToScene(lightning, preview);
            Check(Vector3.Distance(lightning.transform.position, controller.transition.transform.position) < .001f && lightning.transform.localScale == Vector3.one * 3, "Real lightning spawn must use the ground center and configured size.");
            var particles = lightning.GetComponentsInChildren<ParticleSystem>(true);
            Check(particles.Length > 0 && particles.All(p => p.main.scalingMode == ParticleSystemScalingMode.Hierarchy && !p.main.loop), "Every lightning system must inherit size and remain one-shot.");
            foreach (var root in particles.Where(p => p.transform.parent == null || p.transform.parent.GetComponentInParent<ParticleSystem>() == null)) root.Simulate(.055f, true, true, false);
            MQuestPortalValidation.Capture(preview, controller.portalCameraPose, "Portal-strike-large-ground", report);
            report.AppendLine("PASS: real SpawnLightning at Discovery Area center " + lightning.transform.position.ToString("F3") + ", 3x hierarchy scale, non-looping systems and no light shadows.");
            var restPoints = preview.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<WorldInteraction>(true)).Where(p => p.kind == WorldInteractionKind.RestPoint).ToArray();
            Check(restPoints.Length > 0 && restPoints.All(p => p.restFirstVisitVoice != null), "All m save points must expose voice slots.");
            report.AppendLine("PASS: " + restPoints.Length + " scene m save points expose First Visit Voice settings.");
        }
        finally { if (lightning != null) UnityEngine.Object.DestroyImmediate(lightning); EditorSceneManager.ClosePreviewScene(preview); }
        ValidateVoices(report);
        Check(SceneManager.GetActiveScene() == active && active.isDirty == wasDirty, "Validation must preserve live scene state.");
        report.AppendLine("PASS: Edit Mode only; no Play Mode, real campaign save, preferences or source prefab writes.");
        File.WriteAllText(Output + "/Followup-Validation.txt", report.ToString()); Debug.Log("[Ground lightning / Rest voices] PASS: " + Output + "/Followup-Validation.txt");
    }
    static void ValidateVoices(StringBuilder report)
    {
        var scene = EditorSceneManager.NewPreviewScene(); var previousSession = CampaignSession.Instance;
        var sessionInstance = typeof(CampaignSession).GetField("<Instance>k__BackingField", Static);
        var audioInstance = typeof(GameAudio).GetField("instance", Static); var previousAudio = audioInstance.GetValue(null);
        CampaignConfig config = null; GameAudioSettings settings = null; AudioClip clip = null;
        try
        {
            var stage = new GameObject("Rest voice validation"); SceneManager.MoveGameObjectToScene(stage, scene);
            var inactive = new GameObject("Inactive session"); inactive.SetActive(false); inactive.transform.SetParent(stage.transform);
            var session = inactive.AddComponent<CampaignSession>(); config = ScriptableObject.CreateInstance<CampaignConfig>(); config.mapScene = SceneManager.GetActiveScene().name;
            var data = new CampaignSave(); Set(session, "<Config>k__BackingField", config); Set(session, "<Data>k__BackingField", data); sessionInstance.SetValue(null, session);
            var playerHost = new GameObject("Fixture player"); playerHost.transform.SetParent(stage.transform); var player = playerHost.AddComponent<PlayerScript>(); player.enabled = false; Set(session, "<MapPlayer>k__BackingField", player);
            var audioHost = new GameObject("Inactive audio"); audioHost.SetActive(false); audioHost.transform.SetParent(stage.transform); var audio = audioHost.AddComponent<GameAudio>();
            settings = ScriptableObject.CreateInstance<GameAudioSettings>(); Set(audio, "settings", settings); Set(audio, "persistPreferences", false); audioInstance.SetValue(null, audio);
            var volumes = (float[])typeof(GameAudio).GetField("volumes", Private).GetValue(audio); for (int i = 0; i < volumes.Length; i++) volumes[i] = 1;
            volumes[(int)GameAudioBus.Master] = .5f; volumes[(int)GameAudioBus.Voice] = .4f;
            clip = AudioClip.Create("Silent validation clip", 4410, 1, 44100, false);
            WorldInteraction Point(string id)
            {
                var host = new GameObject(id); host.transform.SetParent(stage.transform); host.transform.position = Vector3.forward;
                var point = host.AddComponent<WorldInteraction>(); point.savedWorldId = id; point.kind = WorldInteractionKind.RestPoint; point.range = 2.5f; return point;
            }
            var first = Point("rest-voice-a"); var second = Point("rest-voice-b");
            var play = typeof(CampaignSession).GetMethod("TryPlayRestFirstVisitVoice", Private);
            bool Visit(WorldInteraction point) => (bool)play.Invoke(session, new object[] { point });
            Check(!Visit(first) && data.heardRestPointVoices.Count == 0, "Empty clip must not consume first visit.");
            first.restFirstVisitVoice.clips = new[] { clip };
            first.transform.position = Vector3.forward * 20; Check(!Visit(first), "Far away must not trigger."); first.transform.position = Vector3.forward;
            Set(session, "<Busy>k__BackingField", true); Check(!Visit(first), "Busy/cutscene must not trigger."); Set(session, "<Busy>k__BackingField", false);
            Check(session.TryBeginPresentation(first), "Fixture presentation lock should be available."); Check(!Visit(first), "Presentation must defer voice."); session.EndPresentation(first);
            Check(Visit(first) && !Visit(first) && data.heardRestPointVoices.Count == 1, "First point must play exactly once, even while standing/re-entering.");
            var voices = (IList)typeof(GameAudio).GetField("voices", Private).GetValue(audio); Check(voices.Count == 1, "One voice should be routed.");
            var voice = voices[0]; var source = (AudioSource)voice.GetType().GetField("source").GetValue(voice);
            Check(source.clip == clip && source.spatialBlend == 0 && source.pitch == 1 && Mathf.Abs(source.volume - .2f) < .001f && (GameAudioBus)voice.GetType().GetField("bus").GetValue(voice) == GameAudioBus.Voice, "Dialogue must use assigned clip, stable pitch, 2D voice and Master x Voice gain.");
            Check(!Visit(second), "Another unassigned save point must stay unheard."); second.restFirstVisitVoice.clips = new[] { clip };
            Check(Visit(second) && data.heardRestPointVoices.Count == 2, "Each save point must have independent first visit state.");
            var reloaded = JsonUtility.FromJson<CampaignSave>(JsonUtility.ToJson(data)); Set(session, "<Data>k__BackingField", reloaded);
            Check(!Visit(first) && !Visit(second) && reloaded.heardRestPointVoices.Count == 2, "Save/load must preserve both heard flags.");
            var legacy = new CampaignSave { heardRestPointVoices = null }; legacy.MigrateSkillUnlocks(); Set(session, "<Data>k__BackingField", legacy);
            Check(Visit(first), "Older saves must accept new optional voice state without breaking.");
            report.AppendLine("PASS: proximity only, empty-clip deferral, busy/presentation locks, one play per save point, independent points, 2D Voice bus/pitch/volume, JSON persistence and old-save migration.");
        }
        finally
        {
            sessionInstance.SetValue(null, previousSession); audioInstance.SetValue(null, previousAudio);
            EditorSceneManager.ClosePreviewScene(scene);
            foreach (var value in new UnityEngine.Object[] { config, settings, clip }) if (value != null) UnityEngine.Object.DestroyImmediate(value);
        }
    }
}

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Isolated Edit Mode checks. Never starts Play Mode or writes a campaign save.</summary>
public static class MQuestPortalValidation
{
    const string Output = "Tools/MQuestPortal";
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    static void Set(object target, string name, object value) => target.GetType().GetField(name, Private).SetValue(target, value);
    public static void Run()
    {
        Check(!EditorApplication.isPlayingOrWillChangePlaymode, "Use Edit Mode."); Directory.CreateDirectory(Output);
        var active = SceneManager.GetActiveScene(); bool dirty = active.isDirty;
        var previous = CampaignSession.Instance;
        var instance = typeof(CampaignSession).GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);
        var report = new StringBuilder(); var fixture = EditorSceneManager.NewPreviewScene();
        QuestPortalDefinition definition = null; AdventureQuest quest = null; CampaignConfig config = null;
        Scene preview = default;
        try
        {
            quest = ScriptableObject.CreateInstance<AdventureQuest>(); quest.questId = "fixture-quest"; quest.requiredCount = 2;
            definition = ScriptableObject.CreateInstance<QuestPortalDefinition>(); definition.quest = quest;
            definition.enemyWorldIds.AddRange(new[] { "a", "b" });
            var data = new CampaignSave();
            data.quests.Add(new AdventureQuestProgress { questId = quest.Id });
            Check(!definition.RecordVictory(data, "mapgame", "12", "a") && !definition.RecordVictory(data, "m", "wrong", "a") && !definition.RecordVictory(data, "m", "12", "other"), "Unrelated maps, marker groups and enemies must not advance the portal.");
            Check(definition.RecordVictory(data, "m", "12", "a") && definition.Pending(data, "a"), "First victory must queue its own presentation.");
            Check(!definition.RecordVictory(data, "m", "12", "a"), "Repeated victory must not queue twice.");
            var state = definition.Progress(data, false); state.presented.Add("a");
            Check(!definition.Pending(data, "a") && !definition.IsUnlocked(data), "One completed scene must leave the portal locked.");
            Check(definition.RecordVictory(data, "m", "12", "b") && definition.Pending(data, "b") && !definition.IsUnlocked(data), "Second victory must remain locked until its scene finishes.");
            var pendingSave = JsonUtility.FromJson<CampaignSave>(JsonUtility.ToJson(data));
            Check(definition.Pending(pendingSave, "b"), "Saving between combat and the cutscene must preserve the pending scene.");
            state.presented.Add("b");
            var roundTrip = JsonUtility.FromJson<CampaignSave>(JsonUtility.ToJson(data));
            Check(definition.IsUnlocked(roundTrip) && !definition.Pending(roundTrip, "a") && !definition.Pending(roundTrip, "b"), "Completed presentation flags must survive save/load without replay.");
            var legacy = new CampaignSave(); legacy.quests.Add(new AdventureQuestProgress { questId = quest.Id, objectiveCount = 1 }); legacy.collected.Add("a");
            Check(definition.RecordVictory(legacy, "m", "12", "b") && !definition.Pending(legacy, "a") && definition.Pending(legacy, "b"), "Older earned kills must remain earned, only the new victory gets a scene.");

            var early = new CampaignSave();
            Check(definition.RecordVictory(early, "m", "12", "a") && !definition.Pending(early, "a"), "A pre-quest victory should be saved without triggering a cutscene.");
            early = JsonUtility.FromJson<CampaignSave>(JsonUtility.ToJson(early));
            var earlyQuest = new AdventureQuestProgress { questId = quest.Id };
            early.quests.Add(earlyQuest);
            Check(definition.ReconcileAcceptedQuest(early) && earlyQuest.objectiveCount == 1 && !earlyQuest.completed && definition.Pending(early, "a"),
                "Accepting after a pre-quest victory should credit it and queue that cutscene.");

            var stuck = new CampaignSave();
            var stuckQuest = new AdventureQuestProgress { questId = quest.Id, objectiveCount = 1 };
            stuck.quests.Add(stuckQuest);
            stuck.collected.AddRange(new[] { "a", "b", "unrelated" });
            var stuckPortal = definition.Progress(stuck, true);
            Check(definition.ReconcileAcceptedQuest(stuck) && stuckQuest.objectiveCount == 2 && stuckQuest.completed &&
                !definition.Pending(stuck, "a") && definition.Pending(stuck, "b") && !definition.IsUnlocked(stuck),
                "An existing save should recover the missed boss without replaying the already credited one.");
            Check(!definition.ReconcileAcceptedQuest(stuck), "Recovering the same save again must not grant extra credit.");
            stuckPortal.presented.Add("b");
            Check(definition.IsUnlocked(stuck), "The recovered save should unlock the portal after the missing cutscene.");
            report.AppendLine("PASS: pre-quest victories, old save recovery, map/group/target filtering, distinct victories, pending-save recovery and two-scene unlock.");

            var host = new GameObject("Isolated portal fixture"); SceneManager.MoveGameObjectToScene(host, fixture); host.SetActive(false);
            var session = host.AddComponent<CampaignSession>(); config = ScriptableObject.CreateInstance<CampaignConfig>(); config.mapScene = "isolated-preview-only";
            Set(session, "<Config>k__BackingField", config); Set(session, "<Data>k__BackingField", data); instance.SetValue(null, session);
            var portalObject = new GameObject("Isolated map transition"); SceneManager.MoveGameObjectToScene(portalObject, fixture);
            var portal = portalObject.AddComponent<WorldInteraction>(); portal.kind = WorldInteractionKind.MapTransition; portal.useSavedMapPosition = true;
            portal.targetMapPosition = Vector3.one * 900;
            data.RememberMapPose("mapgame", new Vector3(10, 20, 30), Quaternion.Euler(0, 123, 0));
            data.RememberMapPose("m", new Vector3(-1, -2, -3), Quaternion.identity);
            data.RememberMapPose("mapgame", new Vector3(15, 25, 35), Quaternion.Euler(0, 124, 0));
            var savedPose = session.ResolveMapTransitionPose(portal, "mapgame");
            Check(savedPose.position == new Vector3(15, 25, 35) && Quaternion.Angle(savedPose.rotation, Quaternion.Euler(0, 124, 0)) < .01f && data.mapPositions.Count == 2, "Return must use the latest pose of mapgame, keeping m separate.");
            Check(JsonUtility.FromJson<CampaignSave>(JsonUtility.ToJson(data)).TryGetMapPose("mapgame", out var serialized) && serialized.position == savedPose.position, "Map history must survive JSON.");
            var gate = portalObject.AddComponent<QuestPortalTravelGate>(); gate.definition = definition;
            Check(gate.IsOpen, "Completed portal gate should be open."); state.presented.Remove("b"); Check(!gate.IsOpen, "Pending portal gate must be closed."); state.presented.Add("b");
            report.AppendLine("PASS: per-map position/rotation history, replacement rather than duplicate entries, JSON persistence and travel gate.");

            preview = EditorSceneManager.OpenPreviewScene("Assets/Scenes/m.unity");
            var controller = preview.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MQuestPortalSequence>(true)).Single();
            var authored = controller.definition;
            Check(authored.quest.displayName == "Main Quest" && authored.quest.objectiveWorldPointId == "12" && authored.quest.objectiveMapScene == "m" && !authored.quest.useFixedObjectivePosition, "Main Quest must follow the two dynamic m targets.");
            Check(controller.enemies.Length == 2 && controller.enemies.All(s => s.enemy != null && s.deathClip != null && s.dissolveMaterial != null && s.cameraPose != null), "Two complete enemy shots required.");
            Check(authored.enemyWorldIds.SequenceEqual(controller.enemies.Select(s => s.enemy.Id)), "Cutscene references must match the persistent target IDs.");
            Check(controller.enemies.All(s => s.enemy.enemyPrefabs.Any(unit => unit != null && unit.isBoss)), "Legacy victory recovery depends on both encounters preventing escape.");
            Check(controller.transition.targetMapScene == "mapgame" && controller.transition.useSavedMapPosition && !controller.transition.enabled && controller.transition.GetComponent<QuestPortalTravelGate>().definition == authored, "Authored transition must be locked and configured to return to mapgame.");
            Check(controller.portalCameraPose != null && controller.lightningAnchor != null && controller.darkAnchor != null && controller.npcDestination != null && controller.npc != null && controller.cinematicCamera != null && controller.cinematicBars != null && !controller.transitionFade.raycastTarget, "Camera, fade, NPC and VFX anchors required.");
            Check(Mathf.Abs(Vector3.Dot(controller.npcDestination.up, Vector3.up) - 1) < .001f, "NPC arrival must stay upright.");
            foreach (var shot in controller.enemies)
            {
                var animator = controller.EnsureVisual(shot); Check(animator != null, "Map enemy visual must be created.");
                Check(shot.enemy.GetComponentsInChildren<Animator>(true).Length == 1, "Cutscene must reuse the authored Enemy Visuals without creating a standing duplicate.");
                var renderers = animator.GetComponentsInChildren<Renderer>(true).Where(r => r is MeshRenderer || r is SkinnedMeshRenderer).ToArray();
                Check(renderers.Length > 0, "Enemy must have dissolvable renderers.");
                var materials = renderers.Select(r => r.sharedMaterials).ToArray();
                controller.BeginDeath(shot);
                var bones = animator.GetComponentsInChildren<Transform>(true);
                var poses = bones.Select(t => t.localRotation).ToArray();
                controller.SampleDeath(shot, .7f);
                int movedBones = bones.Where((t, i) => Quaternion.Angle(poses[i], t.localRotation) > .1f).Count();
                Check(movedBones > 0, "Death playable must actually animate the enemy rig.");
                report.AppendLine("Animation inspection: " + shot.enemy.displayName + " | active=" + animator.isActiveAndEnabled + " | avatar=" + animator.avatar + " | clip=" + AssetDatabase.GetAssetPath(shot.deathClip) + " | bindings=" + AnimationUtility.GetCurveBindings(shot.deathClip).Length + " | moved bones=" + movedBones);
                var block = new MaterialPropertyBlock(); renderers[0].GetPropertyBlock(block, 0);
                Check(block.GetFloat("_DissolveAmount") > .1f && block.GetFloat("_DissolveAmount") < 1, "Dissolve must advance during the death animation.");
                Capture(preview, shot.cameraPose, "Enemy-" + shot.enemy.displayName, report);
                controller.SampleDeath(shot, 1); renderers[0].GetPropertyBlock(block, 0); Check(block.GetFloat("_DissolveAmount") >= .999f, "Death must finish fully dissolved.");
                controller.EndDeath();
                for (int i = 0; i < renderers.Length; i++) Check(materials[i].SequenceEqual(renderers[i].sharedMaterials), "Dissolve must restore original shared materials.");
                shot.enemy.gameObject.SetActive(false);
                report.AppendLine("PASS: " + shot.enemy.displayName + " | Die=" + shot.deathClip.name + " | duration=" + shot.deathClip.length + " | renderers=" + renderers.Length + " | camera=" + shot.cameraPose.position);
            }
            var zap = UnityEngine.Object.Instantiate(controller.zapPrefab, controller.lightningAnchor.position, Quaternion.Euler(controller.zapRotationEuler)); SceneManager.MoveGameObjectToScene(zap, preview); zap.transform.localScale = controller.zapScale;
            foreach (var particles in zap.GetComponentsInChildren<ParticleSystem>(true)) { var main = particles.main; main.loop = false; main.stopAction = ParticleSystemStopAction.None; }
            foreach (var light in zap.GetComponentsInChildren<Light>(true)) light.shadows = LightShadows.None;
            foreach (var root in zap.GetComponentsInChildren<ParticleSystem>(true).Where(p => p.transform.parent == null || p.transform.parent.GetComponentInParent<ParticleSystem>() == null)) root.Simulate(.12f, true, true, false);
            foreach (var particles in zap.GetComponentsInChildren<ParticleSystem>(true)) report.AppendLine("Zap particle " + particles.name + " | count=" + particles.particleCount + " | bounds=" + particles.GetComponent<ParticleSystemRenderer>().bounds);
            Capture(preview, controller.portalCameraPose, "Portal-strike", report); UnityEngine.Object.DestroyImmediate(zap);
            // Apply actual scene unlock using isolated campaign data, never the user's live campaign.
            var actualData = new CampaignSave(); actualData.quests.Add(new AdventureQuestProgress { questId = authored.quest.Id });
            Set(session, "<Data>k__BackingField", actualData); authored.Progress(actualData, true);
            controller.ApplyUnlockedState(); Check(!controller.transition.enabled, "Portal must remain disabled before both victories.");
            var actualProgress = authored.Progress(actualData, false); actualProgress.defeated.AddRange(authored.enemyWorldIds); actualProgress.presented.AddRange(authored.enemyWorldIds);
            controller.ApplyUnlockedState();
            Check(controller.transition.enabled && Vector3.Distance(controller.npc.transform.position, controller.npcDestination.position) < .01f, "Both victories must enable transition and teleport quest NPC.");
            var dark = (GameObject)typeof(MQuestPortalSequence).GetField("dark", Private).GetValue(controller);
            Check(dark != null && Vector3.Distance(dark.transform.position, controller.darkAnchor.position) < .01f && dark.GetComponentsInChildren<ParticleSystem>(true).All(p => p.main.loop), "Dark VFX must loop at the transition, ignoring prefab authored world offset.");
            controller.ApplyUnlockedState(); Check(ReferenceEquals(dark, typeof(MQuestPortalSequence).GetField("dark", Private).GetValue(controller)), "Applying persisted unlock must not duplicate VFX.");
            foreach (var particles in dark.GetComponentsInChildren<ParticleSystem>(true)) particles.Simulate(.6f, false, true, false);
            Capture(preview, controller.portalCameraPose, "Portal-unlocked", report);
            report.AppendLine("PASS: actual scene bindings, two native death playables/dissolve cleanup, portal strike, locked/unlocked transition, one Dark instance and persistent NPC destination.");
        }
        finally
        {
            instance.SetValue(null, previous);
            if (preview.IsValid()) EditorSceneManager.ClosePreviewScene(preview);
            EditorSceneManager.ClosePreviewScene(fixture);
            foreach (var value in new UnityEngine.Object[] { config, definition, quest }) if (value != null) UnityEngine.Object.DestroyImmediate(value);
        }
        Check(SceneManager.GetActiveScene() == active && active.isDirty == dirty, "Validation must preserve the live scene and its dirty state.");
        report.AppendLine("PASS: no Play Mode, campaign save write, live scene mutation or active-scene switch.");
        File.WriteAllText(Output + "/Validation.txt", report.ToString()); Debug.Log("[m Quest Portal] PASS: " + Output + "/Validation.txt");
    }
    internal static void Capture(Scene scene, Transform pose, string name, StringBuilder report)
    {
        var host = new GameObject("Quest sequence preview", typeof(Camera)); SceneManager.MoveGameObjectToScene(host, scene);
        var camera = host.GetComponent<Camera>(); camera.enabled = false; camera.scene = scene; camera.overrideSceneCullingMask = EditorSceneManager.GetSceneCullingMask(scene);
        camera.fieldOfView = 48; camera.farClipPlane = 600; host.transform.SetPositionAndRotation(pose.position, pose.rotation);
        var texture = new RenderTexture(1280, 720, 24); var pixels = new Texture2D(1280, 720, TextureFormat.RGB24, false); var previous = RenderTexture.active;
        try
        {
            camera.targetTexture = texture; camera.Render(); RenderTexture.active = texture;
            pixels.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); pixels.Apply();
            string path = Output + "/" + name + ".png"; File.WriteAllBytes(path, pixels.EncodeToPNG()); report.AppendLine("Preview: " + path);
        }
        finally { camera.targetTexture = null; RenderTexture.active = previous; texture.Release(); UnityEngine.Object.DestroyImmediate(texture); UnityEngine.Object.DestroyImmediate(pixels); UnityEngine.Object.DestroyImmediate(host); }
    }
}

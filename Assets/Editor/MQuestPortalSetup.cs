using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[InitializeOnLoad]
public static class MQuestPortalSetup
{
    const string Request = "Library/CombatValidation/MQuestPortal.request";
    const string Output = "Tools/MQuestPortal";
    static MQuestPortalSetup() { EditorApplication.update += Poll; }
    static void Poll()
    {
        if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (File.GetLastWriteTimeUtc("Library/ScriptAssemblies/Assembly-CSharp-Editor.dll") < File.GetLastWriteTimeUtc("Assets/Editor/MQuestPortalSetup.cs")) return;
        if (File.GetLastWriteTimeUtc("Library/ScriptAssemblies/Assembly-CSharp-Editor.dll") < File.GetLastWriteTimeUtc("Assets/Editor/MQuestPortalValidation.cs")) return;
        if (File.GetLastWriteTimeUtc("Library/ScriptAssemblies/Assembly-CSharp-Editor.dll") < File.GetLastWriteTimeUtc("Assets/Editor/MQuestPortalFollowup.cs")) return;
        if (new[] { "MQuestPortalSequence", "QuestPortalDefinition", "MapDeathDissolve", "CampaignSession", "CampaignConfig", "WorldInteraction" }.Any(name =>
            File.GetLastWriteTimeUtc("Library/ScriptAssemblies/Assembly-CSharp.dll") < File.GetLastWriteTimeUtc("Assets/Scripts/Adventure/" + name + ".cs"))) return;
        string command;
        try { command = File.ReadAllText(Request).Trim(); File.Delete(Request); } catch (IOException) { return; }
        try { if (command == "setup") Setup(); else if (command == "polish") MQuestPortalFollowup.Setup(); else if (command == "validate-followup") MQuestPortalFollowup.Validate(); else if (command == "validate") MQuestPortalValidation.Run(); else Inspect(); } catch (Exception error) { Directory.CreateDirectory(Output); File.WriteAllText(Output + "/Error.txt", error.ToString()); Debug.LogException(error); }
    }
    [MenuItem("Tools/Adventure/Main Quest/Inspect Portal Sequence")]
    public static void Inspect()
    {
        Directory.CreateDirectory(Output); var scene = SceneManager.GetSceneByPath("Assets/Scenes/m.unity"); bool preview = !scene.IsValid() || !scene.isLoaded;
        if (preview) scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/m.unity");
        var report = new StringBuilder();
        try
        {
            var all = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
            foreach (var point in all.Select(t => t.GetComponent<WorldInteraction>()).Where(p => p != null && (p.kind == WorldInteractionKind.Encounter || p.kind == WorldInteractionKind.DialogueNPC || p.kind == WorldInteractionKind.MapTransition)))
            {
                report.AppendLine(point.name + " | " + point.displayName + " | " + point.kind + " | marker=" + point.questMarkerId + " | id=" + point.Id + " | position=" + point.transform.position.ToString("F3"));
                report.AppendLine("Parent: " + AnimationUtility.CalculateTransformPath(point.transform, null));
                report.AppendLine("Components: " + string.Join(", ", point.GetComponents<Component>().Where(c => c != null).Select(c => c.GetType().Name)));
                if (point.kind == WorldInteractionKind.Encounter)
                {
                    var animator = point.GetComponent<EnemyEncounterBehaviour>()?.mapAnimator ?? point.GetComponentInChildren<Animator>(true);
                    report.AppendLine("Animator: " + (animator != null ? AnimationUtility.CalculateTransformPath(animator.transform, point.transform) : "NONE"));
                    if (animator != null && animator.runtimeAnimatorController != null)
                        report.AppendLine("Death clips: " + string.Join(", ", animator.runtimeAnimatorController.animationClips.Where(c => c.name.IndexOf("die", StringComparison.OrdinalIgnoreCase) >= 0 || c.name.IndexOf("death", StringComparison.OrdinalIgnoreCase) >= 0).Select(c => c.name + " | " + AssetDatabase.GetAssetPath(c) + " | " + c.length)));
                    foreach (var unit in point.enemyPrefabs.Where(u => u != null)) report.AppendLine("Enemy prefab: " + AssetDatabase.GetAssetPath(unit) + " | dissolve=" + AssetDatabase.GetAssetPath(unit.enemyDeathDissolveMaterial));
                }
                if (point.kind == WorldInteractionKind.DialogueNPC)
                    foreach (var dialogue in point.AllDialogueAssets().Where(d => d != null && d.nodes != null))
                        foreach (var quest in dialogue.nodes.Where(n => n != null && n.choices != null).SelectMany(n => n.choices).Where(c => c != null && c.questToOffer != null).Select(c => c.questToOffer).Distinct())
                            report.AppendLine("Offers: " + AssetDatabase.GetAssetPath(quest) + " | " + quest.Id + " | " + quest.displayName);
                if (point.kind == WorldInteractionKind.MapTransition) report.AppendLine("Destination: " + point.targetMapScene + " | active=" + point.gameObject.activeSelf + " | enabled=" + point.enabled);
            }
            foreach (var portal in all.Where(t => t.name.IndexOf("portal_textured", StringComparison.OrdinalIgnoreCase) >= 0))
            {
                var renderers = portal.GetComponentsInChildren<Renderer>(true); var bounds = new Bounds(portal.position, Vector3.zero);
                foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
                report.AppendLine("PORTAL: " + AnimationUtility.CalculateTransformPath(portal, null) + " | position=" + portal.position.ToString("F3") + " | bounds=" + bounds + " | colliders=" + portal.GetComponentsInChildren<Collider>(true).Length);
            }
            foreach (var guid in AssetDatabase.FindAssets("t:AdventureQuest"))
            { var path = AssetDatabase.GUIDToAssetPath(guid); var quest = AssetDatabase.LoadAssetAtPath<AdventureQuest>(path); report.AppendLine("QUEST: " + path + " | name=" + quest.displayName + " | id=" + quest.Id + " | marker=" + quest.objectiveWorldPointId + " | map=" + quest.objectiveMapScene); }
            File.WriteAllText(Output + "/Inspection.txt", report.ToString()); Debug.Log("[m Quest Portal] Inspection: " + Output + "/Inspection.txt");
        }
        finally { if (preview) EditorSceneManager.ClosePreviewScene(scene); }
    }

    static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    [MenuItem("Tools/Adventure/Main Quest/Setup m Portal Sequence")]
    public static void Setup()
    {
        Check(!EditorApplication.isPlayingOrWillChangePlaymode, "Setup in Edit Mode.");
        var scene = SceneManager.GetSceneByPath("Assets/Scenes/m.unity");
        if (!scene.IsValid() || !scene.isLoaded) scene = EditorSceneManager.OpenScene("Assets/Scenes/m.unity", OpenSceneMode.Additive);
        string backup = Output + "/Before-" + DateTime.Now.ToString("yyyyMMdd-HHmmssfff"); Directory.CreateDirectory(backup);
        File.Copy(scene.path, backup + "/m.disk.unity"); Check(EditorSceneManager.SaveScene(scene, backup + "/m.current.unity", true), "Back up current scene first.");
        const string questPath = "Assets/Dialogue/Quest.asset"; File.Copy(questPath, backup + "/Quest.disk.asset");
        var quest = AssetDatabase.LoadAssetAtPath<AdventureQuest>(questPath); File.WriteAllText(backup + "/Quest.current.json", EditorJsonUtility.ToJson(quest, true));
        var points = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<WorldInteraction>(true)).ToArray();
        Vector3[] positions = { new Vector3(-76.8799973f, -47.347641f, -192.110001f), new Vector3(81.7900009f, -94.5581665f, -1.16999996f) };
        var enemies = positions.Select(position => points.Where(p => p.kind == WorldInteractionKind.Encounter && p.questMarkerId == "12").OrderBy(p => Vector3.Distance(p.transform.position, position)).First()).ToArray();
        for (int i = 0; i < enemies.Length; i++) Check(Vector3.Distance(enemies[i].transform.position, positions[i]) < .5f, "Missing enemy at supplied position " + positions[i]);
        Check(enemies.Distinct().Count() == 2, "Two distinct enemies are required.");
        var portal = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).Single(t => t.name == "portal_textured");
        var transition = points.Where(p => p.kind == WorldInteractionKind.MapTransition).OrderBy(p => Vector3.Distance(p.transform.position, portal.position)).First();
        var npc = points.Single(p => p.kind == WorldInteractionKind.DialogueNPC && p.AllDialogueAssets().Where(d => d != null && d.nodes != null).SelectMany(d => d.nodes).Any(n => n?.choices != null && n.choices.Any(c => c?.questToOffer == quest)));
        quest.displayName = "Main Quest"; quest.objectiveType = AdventureQuestObjectiveType.DefeatEnemy; quest.requiredCount = 2;
        quest.objectiveWorldPointId = "12"; quest.objectiveMapScene = "m"; quest.useFixedObjectivePosition = false;
        EditorUtility.SetDirty(quest); AssetDatabase.SaveAssetIfDirty(quest);
        const string definitionPath = "Assets/Resources/Adventure/MainQuestPortal.asset";
        var definition = AssetDatabase.LoadAssetAtPath<QuestPortalDefinition>(definitionPath);
        if (definition == null) { definition = ScriptableObject.CreateInstance<QuestPortalDefinition>(); AssetDatabase.CreateAsset(definition, definitionPath); }
        else File.Copy(definitionPath, backup + "/MainQuestPortal.asset");
        definition.quest = quest; definition.mapScene = "m"; definition.questMarkerId = "12"; definition.enemyWorldIds = enemies.Select(e => e.Id).ToList();
        EditorUtility.SetDirty(definition); AssetDatabase.SaveAssetIfDirty(definition);
        var controller = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MQuestPortalSequence>(true)).FirstOrDefault();
        if (controller == null) { var go = new GameObject("Main Quest Portal Sequence"); SceneManager.MoveGameObjectToScene(go, scene); controller = go.AddComponent<MQuestPortalSequence>(); }
        controller.definition = definition; controller.mapSetup = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MapGameSetup>(true)).Single();
        controller.npc = npc; controller.transition = transition;
        controller.zapPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Vefects/Zap VFX URP/VFX/Zap/Particles/VFX_Zap_01_Red.prefab");
        controller.darkPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/prefab/New Folder/Dark.prefab");
        Check(controller.zapPrefab != null && controller.darkPrefab != null, "Supplied VFX prefabs must exist.");
        var cameraTransform = Pose("Sequence Camera", controller.transform, portal.position + Vector3.forward * 20, portal.position);
        controller.cinematicCamera = cameraTransform.GetComponent<CinemachineCamera>() ?? cameraTransform.gameObject.AddComponent<CinemachineCamera>();
        controller.cinematicCamera.Priority = -100; controller.cinematicCamera.enabled = false; var lens = controller.cinematicCamera.Lens; lens.FieldOfView = 48; controller.cinematicCamera.Lens = lens;
        controller.enemies = enemies.Select((enemy, i) =>
        {
            var unit = enemy.enemyPrefabs.FirstOrDefault(u => u != null); Check(unit != null, "Enemy prefab missing.");
            var animator = unit.animator != null ? unit.animator : unit.GetComponentInChildren<Animator>(true);
            var clip = animator?.runtimeAnimatorController?.animationClips.Where(c => c.name.IndexOf("death", StringComparison.OrdinalIgnoreCase) >= 0 || c.name.IndexOf("die", StringComparison.OrdinalIgnoreCase) >= 0).Distinct().OrderBy(c => c.name.IndexOf(unit.deathStateName, StringComparison.OrdinalIgnoreCase) >= 0 ? 0 : 1).FirstOrDefault();
            Check(clip != null && unit.enemyDeathDissolveMaterial != null, "Die clip/dissolve material missing on " + unit.name);
            float height = i == 0 ? 6f : 7.5f;
            Vector3 focus = enemy.transform.position + Vector3.up * height * .4f;
            var pose = CameraPose("Enemy " + (i + 1) + " Death Camera", controller.transform, scene, focus, height * 1.6f, enemy.transform.forward);
            return new MQuestPortalSequence.EnemyShot { enemy = enemy, deathClip = clip, dissolveMaterial = unit.enemyDeathDissolveMaterial, cameraPose = pose };
        }).ToArray();
        Vector3 portalFocus = portal.position;
        controller.portalCameraPose = CameraPose("Portal Camera", controller.transform, scene, portalFocus, 28f, Vector3.left);
        if (controller.lightningAnchor == null) controller.lightningAnchor = Pose("Lightning Impact", transition.transform, transition.transform.position, transition.transform.position + Vector3.forward);
        controller.lightningAnchor.SetParent(transition.transform, false); controller.lightningAnchor.localPosition = Vector3.zero;
        controller.zapScale = Vector3.one * 3f;
        controller.darkAnchor = Pose("Dark Area", controller.transform, transition.transform.position + Vector3.up * .1f, transition.transform.position + Vector3.forward);
        Vector3 npcPosition = Ground(scene, transition.transform.position + Vector3.left * 4f);
        controller.npcDestination = Pose("NPC Arrival", controller.transform, npcPosition, transition.transform.position);
        Vector3 npcForward = Vector3.ProjectOnPlane(transition.transform.position - npcPosition, Vector3.up);
        if (npcForward.sqrMagnitude > .001f) controller.npcDestination.rotation = Quaternion.LookRotation(npcForward);
        var intro = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MExplorationTutorial>(true)).FirstOrDefault();
        controller.cinematicBars = intro != null ? intro.cinematicBars : null;
        var canvas = intro != null ? intro.tutorialCanvas : null;
        Check(canvas != null && controller.cinematicBars != null, "Existing cinematic canvas/bars required.");
        var fade = canvas.transform.Find("Main Quest Camera Fade");
        if (fade == null) { var go = new GameObject("Main Quest Camera Fade", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)); go.transform.SetParent(canvas.transform, false); fade = go.transform; }
        var rect = fade.GetComponent<RectTransform>(); rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; fade.SetAsLastSibling();
        controller.transitionFade = fade.GetComponent<Image>(); controller.transitionFade.color = Color.clear; controller.transitionFade.raycastTarget = false;
        var gate = transition.GetComponent<QuestPortalTravelGate>() ?? transition.gameObject.AddComponent<QuestPortalTravelGate>(); gate.definition = definition;
        transition.enabled = false; transition.targetMapScene = "mapgame"; transition.useSavedMapPosition = true;
        var mapgame = EditorSceneManager.OpenPreviewScene("Assets/Scenes/mapgame.unity");
        try { var player = mapgame.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MapGameSetup>(true)).Single().player; transition.targetMapPosition = player.transform.position; transition.targetMapEuler = player.transform.eulerAngles; }
        finally { EditorSceneManager.ClosePreviewScene(mapgame); }
        var config = controller.mapSetup.config != null ? controller.mapSetup.config : Resources.Load<CampaignConfig>("Adventure/CampaignConfig");
        if (!config.quests.Contains(quest))
        {
            File.Copy(AssetDatabase.GetAssetPath(config), backup + "/CampaignConfig.disk.asset");
            File.WriteAllText(backup + "/CampaignConfig.current.json", EditorJsonUtility.ToJson(config, true));
            config.quests.Add(quest); EditorUtility.SetDirty(config); AssetDatabase.SaveAssetIfDirty(config);
        }
        EditorUtility.SetDirty(controller); EditorUtility.SetDirty(transition); EditorUtility.SetDirty(gate); EditorSceneManager.MarkSceneDirty(scene);
        Check(EditorSceneManager.SaveScene(scene), "Save Main Quest sequence to m.");
        File.WriteAllText(Output + "/Setup.txt", "Backup: " + backup + "\nQuest 12 / marker group 12 / map m. One death/dissolve and portal strike per winning encounter. Transition gated until both presentations complete. Returns to saved mapgame pose, with authored spawn as fallback.\n");
        MQuestPortalValidation.Run();
    }
    static Transform Pose(string name, Transform parent, Vector3 position, Vector3 focus)
    {
        var result = parent.Find(name); if (result == null) { result = new GameObject(name).transform; result.SetParent(parent, false); }
        result.position = position; Vector3 forward = focus - position;
        if (forward.sqrMagnitude > .001f) result.rotation = Quaternion.LookRotation(forward.normalized, Vector3.up);
        return result;
    }
    static Transform CameraPose(string name, Transform parent, Scene scene, Vector3 focus, float distance, Vector3 preferred)
    {
        preferred = Vector3.ProjectOnPlane(preferred, Vector3.up).normalized; if (preferred.sqrMagnitude < .1f) preferred = Vector3.back;
        Vector3 best = focus + preferred * distance + Vector3.up * 2;
        var physics = scene.GetPhysicsScene();
        foreach (float height in new[] { 1.2f, 0f, 3f, -1f })
            foreach (float angle in new[] { 0f, 45f, -45f, 90f, -90f, 135f, -135f, 180f })
            {
                var candidate = focus + Quaternion.AngleAxis(angle, Vector3.up) * preferred * distance + Vector3.up * height;
                Vector3 ray = focus - candidate;
                if (!physics.Raycast(candidate, ray.normalized, out _, Mathf.Max(.1f, ray.magnitude - 2f), Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) return Pose(name, parent, candidate, focus);
            }
        return Pose(name, parent, best, focus);
    }
    static Vector3 Ground(Scene scene, Vector3 point)
    {
        if (scene.GetPhysicsScene().Raycast(point + Vector3.up * 10, Vector3.down, out var hit, 30, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) return hit.point;
        return point;
    }
}

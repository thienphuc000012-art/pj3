using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Encounter camera selection and actual dynamic framing, without Play Mode or save writes.</summary>
public static class EncounterCameraSettingsValidation
{
    const string Request = "Library/CombatValidation/EncounterCameraSettings.request";
    const string Result = "Tools/EncounterCamera/Validation.txt";
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static void Set(object target, string field, object value) => target.GetType().GetField(field, Private).SetValue(target, value);
    static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }

    [InitializeOnLoadMethod] static void Init() { EditorApplication.update -= Poll; EditorApplication.update += Poll; }
    static void Poll()
    {
        if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (File.GetLastWriteTimeUtc("Library/ScriptAssemblies/Assembly-CSharp-Editor.dll") < File.GetLastWriteTimeUtc("Assets/Editor/EncounterCameraSettingsValidation.cs")) return;
        File.Delete(Request); Run();
    }

    [MenuItem("Tools/Combat/Validate Encounter Camera Settings")]
    public static void Run()
    {
        Check(!EditorApplication.isPlayingOrWillChangePlaymode, "Run in Edit Mode.");
        Directory.CreateDirectory("Tools/EncounterCamera"); var report = new StringBuilder();
        Scene scene = default;
        CombatCameraDirector director = null;
        CombatCameraSettings common = null, first = null, second = null;
        var previousCombat = CombatManager.Instance; var previousCamera = CameraManager.Instance;
        try
        {
            var source = AssetDatabase.LoadAssetAtPath<CombatCameraSettings>("Assets/Adventure/CombatCameraSettings.asset");
            Check(source != null, "Missing existing combat camera settings asset.");
            string sourceBefore = EditorJsonUtility.ToJson(source);
            common = BattleEncounterSetupEditor.CreateProfileCopy(source);
            first = BattleEncounterSetupEditor.CreateProfileCopy(source);
            second = BattleEncounterSetupEditor.CreateProfileCopy(source);
            first.name = "First encounter camera"; second.name = "Second encounter camera";
            first.targetSelection.fieldOfView = 29; first.targetSelection.cameraDistance = 2.5f;
            first.targetSelection.maximumDistance = 0; first.battleIdle.blendDuration = .11f;
            second.targetSelection.fieldOfView = 58; second.targetSelection.cameraDistance = 8;
            second.targetSelection.maximumDistance = 0; second.battleIdle.blendDuration = .77f;
            Check(common.targetSelection.fieldOfView == source.targetSelection.fieldOfView, "Creating a private profile modified common shot data.");
            Check(!ReferenceEquals(first.targetSelection, second.targetSelection), "Encounter shot data is shared by cloned profiles.");
            report.AppendLine("PASS: private camera profiles clone existing authored settings and keep independent shot data.");

            scene = EditorSceneManager.NewPreviewScene();
            var root = new GameObject("Isolated encounter camera validation"); SceneManager.MoveGameObjectToScene(root, scene);
            var inactive = new GameObject("Inactive gameplay fixture"); inactive.SetActive(false); inactive.transform.SetParent(root.transform);
            var combat = inactive.AddComponent<CombatManager>();
            var firstSetup = inactive.AddComponent<BattleEncounterSetup>(); firstSetup.cameraSettings = first;
            var secondHost = new GameObject("Second setup"); secondHost.SetActive(false); secondHost.transform.SetParent(root.transform);
            var secondSetup = secondHost.AddComponent<BattleEncounterSetup>(); secondSetup.cameraSettings = second;
            var inheritedHost = new GameObject("Inherited setup"); inheritedHost.SetActive(false); inheritedHost.transform.SetParent(root.transform);
            var inherited = inheritedHost.AddComponent<BattleEncounterSetup>();

            var brainHost = new GameObject("Inactive output camera", typeof(Camera)); brainHost.SetActive(false); brainHost.transform.SetParent(root.transform);
            var brain = brainHost.AddComponent<CinemachineBrain>(); var output = brainHost.GetComponent<Camera>(); output.aspect = 16f / 9f;
            var cameraManager = root.AddComponent<CameraManager>(); director = root.AddComponent<CombatCameraDirector>();
            Set(cameraManager, "mainBrain", brain); Set(cameraManager, "director", director); Set(director, "settings", common);
            var enemies = new List<BattleUnit>();
            Check(director.ActiveSettings == common, "Unset encounter should use existing common settings.");
            cameraManager.ApplyEncounterSetup(firstSetup, enemies);
            Check(director.ActiveSettings == first && Mathf.Approximately(director.IdleBlendDuration, .11f), "First encounter override is ignored.");
            cameraManager.ApplyEncounterSetup(secondSetup, enemies);
            Check(director.ActiveSettings == second && Mathf.Approximately(director.IdleBlendDuration, .77f), "Second encounter inherits the first profile.");
            cameraManager.ApplyEncounterSetup(inherited, enemies); Check(director.ActiveSettings == common, "Null camera settings did not restore common profile.");
            cameraManager.ApplyEncounterSetup(firstSetup, enemies); cameraManager.ApplyEncounterSetup(null, null);
            Check(director.ActiveSettings == common, "Missing encounter setup leaks the previous camera override.");
            report.AppendLine("PASS: CameraManager applies A/B before initialization; unset profile and missing setup both restore common settings and blend duration.");

            var playerHost = new GameObject("Player fixture"); playerHost.SetActive(false); playerHost.transform.SetParent(root.transform); playerHost.transform.position = new Vector3(0, 0, -4);
            var player = playerHost.AddComponent<BattleUnit>(); player.isPlayer = true; Set(player, "<currentHP>k__BackingField", 100);
            var enemyHost = new GameObject("Enemy fixture"); enemyHost.SetActive(false); enemyHost.transform.SetParent(root.transform); enemyHost.transform.position = new Vector3(0, 0, 4);
            var enemy = enemyHost.AddComponent<BattleUnit>(); enemy.isPlayer = false; Set(enemy, "<currentHP>k__BackingField", 100);
            combat.playerParty = new List<BattleUnit> { player }; combat.enemyParty = new List<BattleUnit> { enemy };
            combat.playerSlots = new[] { player.transform }; combat.enemySlots = new[] { enemy.transform };
            var resolverType = typeof(CombatCameraDirector).Assembly.GetType("CombatCameraTargetResolver");
            var resolver = Activator.CreateInstance(resolverType, new object[] { combat });
            resolverType.GetMethod("RegisterParty").Invoke(resolver, null);
            var rigHost = new GameObject("Inactive dynamic rig"); rigHost.SetActive(false); rigHost.transform.SetParent(root.transform);
            var rig = rigHost.AddComponent<CinemachineCamera>();
            Set(director, "cameraRig", rig); Set(director, "combat", combat); Set(director, "output", output); Set(director, "targets", resolver);
            cameraManager.ApplyEncounterSetup(firstSetup, enemies); director.Select(player, enemy);
            var update = typeof(CombatCameraDirector).GetMethod("UpdateShot", Private);
            update.Invoke(director, new object[] { 0f, true });
            Check(Mathf.Approximately(rig.Lens.FieldOfView, 29f), "Actual target shot did not use encounter FOV.");
            Vector3 firstPosition = rig.transform.position;
            cameraManager.ApplyEncounterSetup(secondSetup, enemies); update.Invoke(director, new object[] { 0f, true });
            Check(Mathf.Approximately(rig.Lens.FieldOfView, 58f) && Vector3.Distance(firstPosition, rig.transform.position) > 1f, "Active camera did not update FOV/distance when changing encounters.");
            second.targetSelection.fieldOfView = 47; update.Invoke(director, new object[] { 0f, true });
            Check(Mathf.Approximately(rig.Lens.FieldOfView, 47f), "Live Inspector edits are not reflected by the dynamic rig.");
            cameraManager.ApplyEncounterSetup(null, enemies); update.Invoke(director, new object[] { 0f, true });
            Check(Mathf.Approximately(rig.Lens.FieldOfView, common.targetSelection.fieldOfView), "Dynamic shot retained previous encounter framing after reset.");
            report.AppendLine("PASS: actual dynamic Target Selection rig uses encounter FOV and distance, accepts live profile edits, then restores the common shot.");

            Set(cameraManager, "useDynamicCombatCamera", false); firstSetup.battleStartCamera = rig;
            cameraManager.ApplyEncounterSetup(firstSetup, enemies);
            Check(cameraManager.battleStartCam == rig && director.ActiveSettings == common, "Legacy cameras were changed by dynamic profile overrides.");
            report.AppendLine("PASS: legacy battle-start camera assignment still works; dynamic override does not affect legacy mode.");
            var editor = Editor.CreateEditor(firstSetup);
            try { Check(editor is BattleEncounterSetupEditor, "BattleEncounterSetup does not use the new inline camera Inspector."); }
            finally { UnityEngine.Object.DestroyImmediate(editor); }
            Check(sourceBefore == EditorJsonUtility.ToJson(source), "Validation changed the existing camera asset.");
            report.AppendLine("PASS: custom encounter Inspector is registered; existing camera asset unchanged. Isolated Edit Mode only, no scene/save writes.");
            File.WriteAllText(Result, report.ToString()); Debug.Log("[Encounter Camera] PASS: " + Result);
        }
        catch (Exception error) { File.WriteAllText(Result, report + "\nFAIL: " + error); Debug.LogException(error); }
        finally
        {
            if (director != null) Set(director, "cameraRig", null); // Fixture owns its rig, no deferred Destroy in Edit Mode.
            if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene);
            if (first != null) UnityEngine.Object.DestroyImmediate(first); if (second != null) UnityEngine.Object.DestroyImmediate(second); if (common != null) UnityEngine.Object.DestroyImmediate(common);
            CombatManager.Instance = previousCombat; CameraManager.Instance = previousCamera;
            ActiveEditorTracker.sharedTracker.ForceRebuild();
        }
    }
}

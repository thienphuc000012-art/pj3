using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class FantasyWolfHealSetup
{
    const string Folder = "Assets/Adventure/Prefabs/Enemy/FantasyWolf/";
    const string Request = "Library/CombatValidation/FantasyWolfHeal.request";
    const string Output = "Tools/FantasyWolfHeal";
    [InitializeOnLoadMethod] static void Init() { EditorApplication.update -= Poll; EditorApplication.update += Poll; }
    static void Poll()
    {
        if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (File.GetLastWriteTimeUtc("Library/ScriptAssemblies/Assembly-CSharp-Editor.dll") < File.GetLastWriteTimeUtc("Assets/Editor/FantasyWolfHealSetup.cs")) return;
        string command;
        try { command = File.ReadAllText(Request).Trim(); File.Delete(Request); } catch (IOException) { return; }
        try { if (command == "inspect") Inspect(); else if (command == "validate") Validate(); else Setup(); }
        catch (Exception error) { Directory.CreateDirectory(Output); File.WriteAllText(Output + "/Error-" + DateTime.Now.ToString("yyyyMMdd-HHmmssfff") + ".txt", error.ToString()); Debug.LogException(error); }
    }
    [MenuItem("Tools/Combat/Repair FantasyWolf Heal")]
    public static void Setup()
    {
        Check(!EditorApplication.isPlayingOrWillChangePlaymode, "Exit Play Mode before editing the heal animation.");
        Directory.CreateDirectory(Output); var backup = Output + "/Before-" + DateTime.Now.ToString("yyyyMMdd-HHmmssfff"); Directory.CreateDirectory(backup);
        var action = AssetDatabase.LoadAssetAtPath<ActionData>(Folder + "Action Data/Heal.asset");
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(Folder + "FantasyWolf.controller");
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder + "Anim/Sleeping.anim");
        Check(action != null && controller != null && clip != null, "Heal, controller and Sleeping clip resolve.");
        // Preserve the user's unsaved Inspector setup before saving only these three assets.
        foreach (var asset in new UnityEngine.Object[] { action, controller, clip }) File.Copy(AssetDatabase.GetAssetPath(asset), backup + "/" + Path.GetFileName(AssetDatabase.GetAssetPath(asset)));
        File.WriteAllText(backup + "/Heal-Current.json", EditorJsonUtility.ToJson(action, true));
        var objects = AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(controller));
        File.WriteAllText(backup + "/Controller-Current.txt", string.Join("\n\n", objects.Select(o => o.GetType().Name + " " + o.name + "\n" + EditorJsonUtility.ToJson(o, true))));
        foreach (var scene in Enumerable.Range(0, SceneManager.sceneCount).Select(SceneManager.GetSceneAt))
            if (scene.isLoaded && scene.isDirty && !string.IsNullOrEmpty(scene.path))
            {
                File.Copy(scene.path, backup + "/" + scene.name + "-Disk.unity", true);
                EditorSceneManager.SaveScene(scene, backup + "/" + scene.name + "-Current.unity", true);
            }
        Check(action.isHeal && action.isSelfOnly && !action.isMelee && action.animationTriggerName == "heal", "Preserve the configured self-heal action; unexpected Inspector changes require reinspection.");
        var states = States(controller.layers[0].stateMachine).ToArray();
        var enter = states.Single(s => s.name == "GoToRest"); var sleeping = states.Single(s => s.name == "Sleeping"); var rise = states.Single(s => s.name == "GoBackUp");
        foreach (var transition in new[] { enter.transitions.Single(t => t.destinationState == sleeping), sleeping.transitions.Single(t => t.destinationState == rise) })
        {
            Check(transition.conditions.Length == 0, "Preserve conditional transitions; this repair targets the two automatic transitions only.");
            transition.hasExitTime = true; EditorUtility.SetDirty(transition);
        }
        var existing = AnimationUtility.GetAnimationEvents(clip);
        if (!existing.Any(e => e.functionName == "AnimEvent_DealDamage"))
        {
            float time = existing.FirstOrDefault(e => e.functionName == "AnimEvent_PlayVFX")?.time ?? 1f / clip.frameRate;
            AnimationUtility.SetAnimationEvents(clip, existing.Concat(new[] { new AnimationEvent { time = time, functionName = "AnimEvent_DealDamage" } }).OrderBy(e => e.time).ToArray());
        }
        EditorUtility.SetDirty(action); EditorUtility.SetDirty(controller); EditorUtility.SetDirty(clip);
        AssetDatabase.SaveAssetIfDirty(action); AssetDatabase.SaveAssetIfDirty(controller); AssetDatabase.SaveAssetIfDirty(clip);
        File.WriteAllText(Output + "/Setup.txt", "Backup: " + backup + "\nPreserved the current Inspector Heal setup (power/multiplier/VFX/targeting). Added the heal resolution event at Sleeping VFX timing and enabled the two automatic Exit Time transitions. No original scene save or vendor asset edits.\n");
        Validate();
    }
    [MenuItem("Tools/Combat/Validate FantasyWolf Heal")]
    public static void Validate()
    {
        var report = new StringBuilder();
        var action = AssetDatabase.LoadAssetAtPath<ActionData>(Folder + "Action Data/Heal.asset");
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(Folder + "FantasyWolf.controller");
        var states = States(controller.layers[0].stateMachine).ToArray();
        var enter = states.Single(s => s.name == "GoToRest"); var sleeping = states.Single(s => s.name == "Sleeping"); var rise = states.Single(s => s.name == "GoBackUp");
        Check(action.isHeal && action.isSelfOnly && !action.isMelee, "Heal targets the caster.");
        Check(states.Single(s => s.name == "Idle").transitions.Any(t => t.destinationState == enter && t.conditions.Any(c => c.parameter == action.animationTriggerName)), "The heal trigger enters GoToRest from Idle.");
        var entering = enter.transitions.Single(t => t.destinationState == sleeping); var leaving = sleeping.transitions.Single(t => t.destinationState == rise);
        Check(entering.hasExitTime && leaving.hasExitTime && entering.conditions.Length == 0 && leaving.conditions.Length == 0 && leaving.exitTime < 1f, "The animation chain advances and leaves Sleeping before its second loop.");
        var clip = sleeping.motion as AnimationClip; var events = AnimationUtility.GetAnimationEvents(clip);
        var hit = events.Single(e => e.functionName == "AnimEvent_DealDamage"); var vfx = events.Single(e => e.functionName == "AnimEvent_PlayVFX");
        Check(Mathf.Abs(hit.time - vfx.time) < .0001f && hit.time < clip.length * leaving.exitTime, "Resolve heal once at the VFX timing, before leaving Sleeping.");
        var end = AnimationUtility.GetAnimationEvents((AnimationClip)rise.motion).Single(e => e.functionName == "AnimEvent_EndAttack");
        Check(rise.transitions.Any(t => t.hasExitTime && t.exitTime * ((AnimationClip)rise.motion).length + t.duration > end.time), "GoBackUp can deliver EndAttack before its blend finishes.");
        report.AppendLine("PASS: heal trigger → GoToRest → Sleeping → GoBackUp; both automatic transitions have Exit Time; exactly one heal event at " + hit.time.ToString("0.000") + "s matches the existing VFX; EndAttack is reachable.");

        var preview = EditorSceneManager.NewPreviewScene();
        var oldCombat = CombatManager.Instance; var oldUI = AdvancedUIManager.Instance; var oldParry = ParrySystem.Instance; var oldCamera = CameraManager.Instance;
        var oldRandom = UnityEngine.Random.state;
        try
        {
            var root = new GameObject("Isolated FantasyWolf Heal fixture"); root.SetActive(false); SceneManager.MoveGameObjectToScene(root, preview);
            GameObject Child(string name) { var go = new GameObject(name); go.transform.SetParent(root.transform); return go; }
            var manager = Child("Combat").AddComponent<CombatManager>(); var ui = Child("UI").AddComponent<AdvancedUIManager>();
            var wolf = Child("FantasyWolf").AddComponent<BattleUnit>(); wolf.isPlayer = false; wolf.unitName = "FantasyWolf"; wolf.maxHP = 100; wolf.baseAtk = 15; wolf.SetPersistentHP(20);
            var ally = Child("Other enemy").AddComponent<BattleUnit>(); ally.maxHP = 100; ally.SetPersistentHP(30);
            var player = Child("Player").AddComponent<BattleUnit>(); player.isPlayer = true; player.maxHP = 100; player.SetPersistentHP(40);
            var receiver = wolf.gameObject.AddComponent<BattleUnit_AnimationEvents>(); receiver.ownerUnit = wolf;
            root.SetActive(true); CombatManager.Instance = manager; AdvancedUIManager.Instance = ui; ParrySystem.Instance = null; CameraManager.Instance = null;
            manager.state = CombatState.Executing; manager.currentActiveUnit = wolf;
            // Deliberately leave the stale target on a player to verify self-only healing.
            manager.currentTarget = player; manager.enemyParty.Add(ally); manager.enemyParty.Add(wolf); manager.playerParty.Add(player);
            Set(manager, "selectedAction", action); Set(manager, "animationActionOwner", wolf); Set(manager, "animationActionData", action); Set(manager, "acceptingActionAnimationEvents", true);
            int amount = Mathf.Max(0, Mathf.RoundToInt(wolf.GetTotalAttack() * action.damageMultiplier + action.power)); Check(amount > 0, "Configured heal amount is positive.");
            receiver.AnimEvent_DealDamage();
            Check(wolf.currentHP == Mathf.Min(100, 20 + amount) && ally.currentHP == 30 && player.currentHP == 40, "Heal increases only FantasyWolf HP, not the player or another enemy.");
            wolf.SetPersistentHP(95); receiver.AnimEvent_DealDamage(); Check(wolf.currentHP == 100, "Healing clamps to max HP.");
            receiver.AnimEvent_DealDamage(); Check(wolf.currentHP == 100, "Full HP remains full.");
            receiver.AnimEvent_EndAttack(); Check(manager.isAttackAnimationFinished, "Healing animation releases the action on EndAttack.");
            report.AppendLine("PASS: real AnimationEvents/CombatManager path heals only FantasyWolf, preserves other enemy/player HP, clamps to max HP and ends the action. Fixture amount=" + amount + " (ATK15 × " + action.damageMultiplier + " + Power" + action.power + ").");
            foreach (var unit in UnityEngine.Object.FindObjectsByType<BattleUnit>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(u => u.gameObject.scene != preview && (u.name + u.unitName).IndexOf("wolf", StringComparison.OrdinalIgnoreCase) >= 0))
                Check(unit.animator != null && unit.animator.GetComponent<BattleUnit_AnimationEvents>()?.ownerUnit == unit && unit.animator.fireEvents, "Loaded FantasyWolf receives animation events.");
            report.AppendLine("Edit Mode only; no campaign save writes or live combat. User scene remains unsaved.");
        }
        finally
        {
            CombatManager.Instance = oldCombat; AdvancedUIManager.Instance = oldUI; ParrySystem.Instance = oldParry; CameraManager.Instance = oldCamera;
            UnityEngine.Random.state = oldRandom; EditorSceneManager.ClosePreviewScene(preview);
        }
        Directory.CreateDirectory(Output); File.WriteAllText(Output + "/Validation.txt", report.ToString()); Debug.Log("[FantasyWolf Heal] Passed: " + Output + "/Validation.txt");
    }
    static System.Collections.Generic.IEnumerable<AnimatorState> States(AnimatorStateMachine machine)
    {
        foreach (var state in machine.states) yield return state.state;
        foreach (var child in machine.stateMachines) foreach (var state in States(child.stateMachine)) yield return state;
    }
    static void Set(object owner, string name, object value) => owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(owner, value);
    static void Check(bool valid, string message) { if (!valid) throw new InvalidOperationException(message); }
    static void Inspect()
    {
        Directory.CreateDirectory(Output); var report = new StringBuilder();
        var action = AssetDatabase.LoadAssetAtPath<ActionData>(Folder + "Action Data/Heal.asset");
        report.AppendLine("Heal current in-memory settings (including unsaved Inspector edits):");
        report.AppendLine(EditorJsonUtility.ToJson(action, true));
        foreach (var unit in UnityEngine.Object.FindObjectsByType<BattleUnit>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Where(u => (u.name + " " + u.unitName).IndexOf("wolf", StringComparison.OrdinalIgnoreCase) >= 0))
        {
            report.AppendLine("Unit " + unit.name + " scene=" + unit.gameObject.scene.path + " dirty=" + unit.gameObject.scene.isDirty + " player=" + unit.isPlayer + " animator=" + unit.animator?.name);
            if (unit.animator == null) continue;
            report.AppendLine("Controller=" + AssetDatabase.GetAssetPath(unit.animator.runtimeAnimatorController) + "; receiver=" + unit.animator.GetComponent<BattleUnit_AnimationEvents>()?.ownerUnit?.name + "; fireEvents=" + unit.animator.fireEvents);
            foreach (var clip in unit.animator.runtimeAnimatorController.animationClips.Distinct())
            {
                if (!new[] { "Sleeping", "GoToRest", "GoBackUp", "Howl" }.Any(n => clip.name.IndexOf(n, StringComparison.OrdinalIgnoreCase) >= 0)) continue;
                report.AppendLine("CLIP " + AssetDatabase.GetAssetPath(clip) + " length=" + clip.length + " loop=" + clip.isLooping);
                foreach (var e in AnimationUtility.GetAnimationEvents(clip)) report.AppendLine("  " + e.time + " " + e.functionName);
            }
        }
        File.WriteAllText(Output + "/Inspection.txt", report.ToString()); Debug.Log("[FantasyWolf Heal] Inspection ready: " + Output + "/Inspection.txt");
    }
}

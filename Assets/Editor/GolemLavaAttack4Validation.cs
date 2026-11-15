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

/// <summary>Validates the authored SpawnAtTarget attack without starting Play Mode or writing campaign saves.</summary>
public static class GolemLavaAttack4Validation
{
    const string Root = "Assets/Adventure/Prefabs/Enemy/GolemLava/";
    const string Request = "Library/CombatValidation/GolemLavaAttack4.request";
    const string Output = "Tools/GolemLavaParry";
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    [InitializeOnLoadMethod] static void Init() { EditorApplication.update -= Poll; EditorApplication.update += Poll; }
    static void Poll()
    {
        if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (File.GetLastWriteTimeUtc("Library/ScriptAssemblies/Assembly-CSharp-Editor.dll") < File.GetLastWriteTimeUtc("Assets/Editor/GolemLavaAttack4Validation.cs")) return;
        File.Delete(Request);
        try { Validate(); }
        catch (Exception ex) { Directory.CreateDirectory(Output); File.WriteAllText(Output + "/Error-" + DateTime.Now.ToString("yyyyMMdd-HHmmssfff") + ".txt", ex.ToString()); Debug.LogException(ex); }
    }
    [MenuItem("Tools/Adventure/Combat/Validate GolemLava Attack4 Parry")]
    public static void Validate()
    {
        Check(!EditorApplication.isPlayingOrWillChangePlaymode, "Validate in Edit Mode.");
        var report = new StringBuilder();
        var action = AssetDatabase.LoadAssetAtPath<ActionData>(Root + "ActionData/attack4.asset");
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(Root + "Anim/attack4.anim");
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(Root + "GolemLava.controller");
        Check(action != null && clip != null && controller != null, "GolemLava attack assets are missing.");
        Check(action.vfxType == ActionData.VfxType.SpawnAtTarget && !action.isMelee, "Attack4 must keep its authored ranged SpawnAtTarget settings.");
        var state = States(controller.layers[0].stateMachine).FirstOrDefault(s => s.name == action.animationTriggerName);
        Check(state != null && state.motion == clip, "Attack4 controller state must use the corrected clip.");
        var events = AnimationUtility.GetAnimationEvents(clip);
        var open = events.Single(e => e.functionName == "AnimEvent_OpenParry");
        var hit = events.Single(e => e.functionName == "AnimEvent_DealDamage");
        Check(Mathf.Abs(open.time - .7f) < .0001f && Mathf.Abs(hit.time - 1f) < .0001f,
            "Attack4 needs a .70-1.00 second parry window.");
        Check(events.Single(e => e.functionName == "AnimEvent_PlayVFX").time == hit.time &&
            events.Single(e => e.functionName == "AnimEvent_EndAttack").time > hit.time, "Preserve VFX/damage/end timing.");
        report.AppendLine("PASS: GolemLava controller attack4 -> correct clip; OpenParry 0.70s, PlayVFX and DealDamage 1.00s, EndAttack unchanged.");

        var scene = EditorSceneManager.NewPreviewScene();
        var oldCombat = CombatManager.Instance; var oldParry = ParrySystem.Instance;
        var oldUI = AdvancedUIManager.Instance; var oldCamera = CameraManager.Instance;
        var oldRandom = UnityEngine.Random.state; float oldScale = Time.timeScale;
        ParrySystem parry = null;
        try
        {
            var root = new GameObject("Isolated GolemLava parry fixture"); root.SetActive(false); SceneManager.MoveGameObjectToScene(root, scene);
            var manager = Child(root, "Combat").AddComponent<CombatManager>();
            parry = Child(root, "Parry").AddComponent<ParrySystem>();
            var ui = Child(root, "UI").AddComponent<AdvancedUIManager>();
            var enemy = Child(root, "GolemLava").AddComponent<BattleUnit>();
            enemy.unitName = "GolemLava"; enemy.isPlayer = false; enemy.maxHP = 1000; enemy.SetPersistentHP(1000); enemy.defaultAttack = action;
            var player = Child(root, "Player").AddComponent<BattleUnit>();
            player.unitName = "Parry validation player"; player.isPlayer = true; player.maxHP = 1000; player.SetPersistentHP(1000);
            var receiver = enemy.gameObject.AddComponent<BattleUnit_AnimationEvents>(); receiver.ownerUnit = enemy;
            root.SetActive(true);
            CombatManager.Instance = manager; ParrySystem.Instance = parry; AdvancedUIManager.Instance = ui; CameraManager.Instance = null;
            manager.state = CombatState.Executing; manager.currentActiveUnit = enemy; manager.currentTarget = player; Set(manager, "selectedAction", action);
            manager.playerParty.Add(player); manager.enemyParty.Add(enemy);
            Set(manager, "animationActionOwner", enemy); Set(manager, "animationActionData", action); Set(manager, "acceptingActionAnimationEvents", true);
            parry.parrySuccessVfxPrefab = null; parry.parryImpactAudioClip = null;
            Check(!manager.ShouldUseMeleeHitboxParry(enemy) && !manager.ShouldUseVfxParryTiming(enemy), "SpawnAtTarget must accept animation-timed parry events.");
            var press = typeof(ParrySystem).GetMethod("TriggerParryAction", Private);

            parry.ResetParryState(); player.SetPersistentHP(1000);
            press.Invoke(parry, null); Check(!parry.parrySuccessful, "Input before OpenParry must fail.");
            receiver.AnimEvent_OpenParry(); Check(parry.isParryWindowOpen, "The corrected event must open the real parry system.");
            receiver.AnimEvent_DealDamage(); Check(player.currentHP < 1000, "Early input without a second press must still take damage.");
            report.AppendLine("PASS: early input does not block damage; OpenParry event reaches the actual ParrySystem.");

            parry.ResetParryState(); player.SetPersistentHP(1000);
            receiver.AnimEvent_OpenParry(); press.Invoke(parry, null);
            Check(parry.parrySuccessful, "Input inside the parry window must succeed.");
            receiver.AnimEvent_DealDamage();
            Check(player.currentHP == 1000 && !parry.isParryWindowOpen && !parry.parrySuccessful,
                "A correct parry must prevent Attack4 damage and reset the window after the hit.");
            parry.StopAllCoroutines(); Set(parry, "parryFeedbackRoutine", null); Time.timeScale = oldScale;
            report.AppendLine("PASS: in-window parry prevents actual CombatManager Attack4 HP damage; window/result reset after DealDamage.");

            parry.ResetParryState(); player.SetPersistentHP(1000);
            receiver.AnimEvent_OpenParry(); receiver.AnimEvent_DealDamage(); int damagedHP = player.currentHP;
            press.Invoke(parry, null);
            Check(damagedHP < 1000 && player.currentHP == damagedHP && !parry.parrySuccessful, "Late input must not undo damage or succeed after impact.");
            report.AppendLine("PASS: missed/late parry still takes damage and cannot undo the resolved hit.");
            InspectLoadedScene(report, action, clip);
            report.AppendLine("Edit Mode only. No live battle, map scene or campaign save writes; runtime combat scripts unchanged.");
            Directory.CreateDirectory(Output); File.WriteAllText(Output + "/Validation.txt", report.ToString()); Debug.Log("[GolemLava Attack4] " + report);
        }
        finally
        {
            if (parry != null) { parry.StopAllCoroutines(); Set(parry, "parryFeedbackRoutine", null); }
            CombatManager.Instance = oldCombat; ParrySystem.Instance = oldParry; AdvancedUIManager.Instance = oldUI; CameraManager.Instance = oldCamera;
            Time.timeScale = oldScale; UnityEngine.Random.state = oldRandom;
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }
    static void InspectLoadedScene(StringBuilder report, ActionData action, AnimationClip clip)
    {
        var scene = SceneManager.GetSceneByName("combattest");
        if (!scene.IsValid() || !scene.isLoaded) return;
        foreach (var unit in scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<BattleUnit>(true))
            .Where(unit => (unit.unitName + unit.name).IndexOf("golem", StringComparison.OrdinalIgnoreCase) >= 0))
        {
            report.AppendLine("Scene unit: " + unit.name + ", unitName=" + unit.unitName + ", active=" + unit.gameObject.activeInHierarchy);
            foreach (var animator in unit.GetComponentsInChildren<Animator>(true))
            {
                var receiver = animator.GetComponent<BattleUnit_AnimationEvents>();
                report.AppendLine("  Animator=" + animator.name + ", controller=" + AssetDatabase.GetAssetPath(animator.runtimeAnimatorController) +
                    ", receiver=" + (receiver != null) + ", owner=" + (receiver != null && receiver.ownerUnit != null ? receiver.ownerUnit.name : "auto-resolve on Awake"));
                if (animator.runtimeAnimatorController != null && animator.runtimeAnimatorController.animationClips.Contains(clip))
                    Check(receiver != null && (receiver.ownerUnit == null || receiver.ownerUnit == unit), "GolemLava Animator needs a correctly owned animation-event receiver.");
            }
        }
    }
    static System.Collections.Generic.IEnumerable<AnimatorState> States(AnimatorStateMachine machine)
    {
        foreach (var state in machine.states) yield return state.state;
        foreach (var child in machine.stateMachines) foreach (var state in States(child.stateMachine)) yield return state;
    }
    static GameObject Child(GameObject root, string name) { var go = new GameObject(name); go.transform.SetParent(root.transform, false); return go; }
    static void Set(object target, string field, object value) => target.GetType().GetField(field, Private).SetValue(target, value);
    static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}

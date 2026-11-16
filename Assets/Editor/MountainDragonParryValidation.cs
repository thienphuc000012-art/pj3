using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Exercises the three authored claw actions through real parry/damage events in an isolated Edit Mode scene.</summary>
public static class MountainDragonParryValidation
{
    const string Folder = "Assets/Adventure/Prefabs/Enemy/MountainDragon/";
    const string Output = "Tools/MountainDragonParry";
    const string Request = "Library/CombatValidation/MountainDragonParry.request";
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    [InitializeOnLoadMethod] static void Init() { EditorApplication.update -= Poll; EditorApplication.update += Poll; }
    static void Poll()
    {
        if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (File.GetLastWriteTimeUtc("Library/ScriptAssemblies/Assembly-CSharp-Editor.dll") < File.GetLastWriteTimeUtc("Assets/Editor/MountainDragonParryValidation.cs")) return;
        try { File.ReadAllText(Request); File.Delete(Request); } catch (IOException) { return; }
        try { Run(); }
        catch (Exception error) { Directory.CreateDirectory(Output); File.WriteAllText(Output + "/Error-" + DateTime.Now.ToString("yyyyMMdd-HHmmssfff") + ".txt", error.ToString()); Debug.LogException(error); }
    }
    [MenuItem("Tools/Combat/Validate MountainDragon Claw Parry")]
    public static void Run()
    {
        Check(!EditorApplication.isPlayingOrWillChangePlaymode, "Exit Play Mode before isolated validation.");
        var report = new StringBuilder();
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "MountainDragon_PBR 1.prefab");
        Check(prefab != null, "Current MountainDragon prefab resolves.");
        var unit = prefab.GetComponent<BattleUnit>();
        Check(unit != null && unit.animator != null && !unit.isPlayer && unit.animator.fireEvents, "Enemy Animator must dispatch gameplay events.");
        var receiver = unit.animator.GetComponent<BattleUnit_AnimationEvents>();
        Check(receiver != null && receiver.ownerUnit == unit, "Animator event receiver points to this MountainDragon unit.");
        var controller = unit.animator.runtimeAnimatorController as AnimatorController;
        Check(controller != null, "Current dragon uses its authored AnimatorController.");
        report.AppendLine("Prefab: " + AssetDatabase.GetAssetPath(prefab) + "; controller: " + AssetDatabase.GetAssetPath(controller));
        report.AppendLine("Enemy melee hitboxes: " + prefab.GetComponentsInChildren<EnemyMeleeParryHitbox>(true).Length + ". Claw actions use existing animation windows instead of requiring absent weapon hitboxes.");

        var scene = EditorSceneManager.NewPreviewScene();
        var oldCombat = CombatManager.Instance; var oldParry = ParrySystem.Instance; var oldUI = AdvancedUIManager.Instance; var oldCamera = CameraManager.Instance;
        var oldRandom = UnityEngine.Random.state; float oldScale = Time.timeScale;
        ParrySystem parry = null;
        try
        {
            var root = new GameObject("Isolated MountainDragon parry fixture"); root.SetActive(false); SceneManager.MoveGameObjectToScene(root, scene);
            var manager = Child(root, "Combat").AddComponent<CombatManager>();
            parry = Child(root, "Parry").AddComponent<ParrySystem>();
            var ui = Child(root, "UI").AddComponent<AdvancedUIManager>();
            var enemy = Child(root, "MountainDragon").AddComponent<BattleUnit>();
            enemy.unitName = "MountainDragon"; enemy.isPlayer = false; enemy.maxHP = 1000; enemy.SetPersistentHP(1000);
            var player = Child(root, "Player").AddComponent<BattleUnit>();
            player.unitName = "Parry validation player"; player.isPlayer = true; player.maxHP = 1000; player.baseDef = 0; player.SetPersistentHP(1000);
            var events = enemy.gameObject.AddComponent<BattleUnit_AnimationEvents>(); events.ownerUnit = enemy;
            root.SetActive(true);
            CombatManager.Instance = manager; ParrySystem.Instance = parry; AdvancedUIManager.Instance = ui; CameraManager.Instance = null;
            manager.state = CombatState.Executing; manager.currentActiveUnit = enemy; manager.currentTarget = player;
            manager.playerParty.Add(player); manager.enemyParty.Add(enemy);
            Set(manager, "animationActionOwner", enemy); Set(manager, "acceptingActionAnimationEvents", true);
            parry.parrySuccessVfxPrefab = null; parry.parryImpactAudioClip = null;
            var press = typeof(ParrySystem).GetMethod("TriggerParryAction", Private);
            void Reset()
            {
                parry.StopAllCoroutines(); Set(parry, "parryFeedbackRoutine", null); Time.timeScale = oldScale;
                parry.ResetParryState(); player.SetPersistentHP(1000); manager.isAttackAnimationFinished = false;
            }
            foreach (string name in new[] { "clawsAttack2HitCombo", "ClawsAttackLeft", "ClawsAttackRight" })
            {
                var action = AssetDatabase.LoadAssetAtPath<ActionData>(Folder + "ActionData/" + name + ".asset");
                Check(action != null && action.isMelee && !action.useMeleeHitboxParry, "Claw action retains melee movement but accepts animation-timed parry: " + name);
                var states = controller.layers.SelectMany(l => States(l.stateMachine)).Where(s => string.Equals(s.name, action.animationTriggerName, StringComparison.OrdinalIgnoreCase)).ToArray();
                Check(states.Length > 0, "Animator state for " + action.animationTriggerName);
                var clips = states.Select(s => s.motion as AnimationClip).Where(c => c != null).Distinct().ToArray();
                Check(clips.Length == 1, "Attack states resolve to one authored clip: " + name);
                var timeline = AnimationUtility.GetAnimationEvents(clips[0]).OrderBy(e => e.time).ToArray();
                var opens = timeline.Where(e => e.functionName == "AnimEvent_OpenParry").ToArray();
                var hits = timeline.Where(e => e.functionName == "AnimEvent_DealDamage").ToArray();
                int expectedHits = name == "clawsAttack2HitCombo" ? 2 : 1;
                Check(opens.Length == expectedHits && hits.Length == expectedHits, "Each hit needs its own open/damage event pair: " + name);
                for (int i = 0; i < hits.Length; i++)
                {
                    Check(opens[i].time < hits[i].time && (i == 0 || opens[i].time > hits[i - 1].time), "Independent, ordered parry windows: " + name);
                    if (i > 0) Check(opens[i].time - opens[i - 1].time >= parry.parryCooldown, "Combo timing permits pressing Space again after cooldown.");
                }
                Check(timeline.Any(e => e.functionName == "AnimEvent_EndAttack" && e.time > hits.Last().time), "Attack ends after all damage events: " + name);
                enemy.defaultAttack = action; Set(manager, "selectedAction", action); Set(manager, "animationActionData", action);
                Check(!manager.ShouldUseMeleeHitboxParry(enemy) && !manager.ShouldUseVfxParryTiming(enemy), "Animation windows are not bypassed: " + name);

                Reset(); press.Invoke(parry, null); Check(!parry.parrySuccessful, "Pressing before the window fails: " + name);
                events.AnimEvent_OpenParry(); Check(parry.isParryWindowOpen, "Open event reaches ParrySystem: " + name);
                events.AnimEvent_DealDamage(); Check(player.currentHP < 1000, "An early press does not pre-block a future hit: " + name);

                Reset(); events.AnimEvent_OpenParry(); events.AnimEvent_DealDamage(); int damaged = player.currentHP;
                press.Invoke(parry, null); Check(damaged < 1000 && player.currentHP == damaged && !parry.parrySuccessful, "A late press cannot undo damage: " + name);

                Reset();
                foreach (var hit in hits)
                {
                    events.AnimEvent_OpenParry(); press.Invoke(parry, null); Check(parry.parrySuccessful, "Pressing in the window succeeds: " + name);
                    events.AnimEvent_DealDamage();
                    Check(player.currentHP == 1000 && !parry.isParryWindowOpen && !parry.parrySuccessful, "Successful parry prevents real HP damage and resets per hit: " + name);
                    parry.StopAllCoroutines(); Set(parry, "parryFeedbackRoutine", null); Time.timeScale = oldScale;
                }
                if (expectedHits == 2)
                {
                    Reset(); events.AnimEvent_OpenParry(); press.Invoke(parry, null); events.AnimEvent_DealDamage();
                    events.AnimEvent_OpenParry(); events.AnimEvent_DealDamage(); Check(player.currentHP < 1000, "Parrying hit one must not automatically block hit two.");
                    Reset(); events.AnimEvent_OpenParry(); events.AnimEvent_DealDamage(); damaged = player.currentHP;
                    events.AnimEvent_OpenParry(); press.Invoke(parry, null); events.AnimEvent_DealDamage(); Check(player.currentHP == damaged, "Hit two remains parryable after missing hit one.");
                }
                events.AnimEvent_EndAttack(); Check(manager.isAttackAnimationFinished, "End event completes the action: " + name);
                report.AppendLine("PASS " + name + ": " + string.Join(", ", opens.Select((o, i) => o.time.ToString("0.000") + "–" + hits[i].time.ToString("0.000") + "s")) + "; early/late inputs take damage; on-time inputs block; per-hit reset and EndAttack pass.");
            }
            report.AppendLine("PASS combo isolation: block first/miss second and miss first/block second. Existing clip events, action damage/VFX, prefab assignments, open scenes and campaign saves unchanged.");
            report.AppendLine("Validation uses the real CombatManager damage path in Edit Mode; no live battle or save writes.");
            Directory.CreateDirectory(Output); File.WriteAllText(Output + "/Validation.txt", report.ToString()); Debug.Log("[MountainDragon Parry] Passed: " + Output + "/Validation.txt");
        }
        finally
        {
            if (parry != null) { parry.StopAllCoroutines(); Set(parry, "parryFeedbackRoutine", null); }
            CombatManager.Instance = oldCombat; ParrySystem.Instance = oldParry; AdvancedUIManager.Instance = oldUI; CameraManager.Instance = oldCamera;
            Time.timeScale = oldScale; UnityEngine.Random.state = oldRandom;
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }
    static IEnumerable<AnimatorState> States(AnimatorStateMachine machine)
    {
        foreach (var state in machine.states) yield return state.state;
        foreach (var child in machine.stateMachines) foreach (var state in States(child.stateMachine)) yield return state;
    }
    static GameObject Child(GameObject root, string name) { var child = new GameObject(name); child.transform.SetParent(root.transform); return child; }
    static void Set(object owner, string field, object value) => owner.GetType().GetField(field, Private).SetValue(owner, value);
    static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
}

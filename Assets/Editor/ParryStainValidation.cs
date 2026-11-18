using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Exercise actual timing/contact parry entry points with depleted shared Stain. No Play Mode or campaign saves.</summary>
[InitializeOnLoad]
public static class ParryStainValidation
{
    const string Request = "Library/CombatValidation/ParryStain.request", Output = "Tools/ParryStain";
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static ParryStainValidation() { EditorApplication.update += Poll; }
    static void Poll()
    {
        if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (File.GetLastWriteTimeUtc("Library/ScriptAssemblies/Assembly-CSharp-Editor.dll") < File.GetLastWriteTimeUtc("Assets/Editor/ParryStainValidation.cs") ||
            new[] { "ParrySystem", "CombatManager" }.Any(name => File.GetLastWriteTimeUtc("Library/ScriptAssemblies/Assembly-CSharp.dll") < File.GetLastWriteTimeUtc("Assets/Scripts/combat/" + name + ".cs"))) return;
        try { File.ReadAllText(Request); File.Delete(Request); } catch (IOException) { return; }
        try { Run(); } catch (Exception error) { Directory.CreateDirectory(Output); File.WriteAllText(Output + "/Error-" + DateTime.Now.ToString("yyyyMMdd-HHmmssfff") + ".txt", error.ToString()); Debug.LogException(error); }
    }
    static void Check(bool valid, string message) { if (!valid) throw new InvalidOperationException(message); }
    static void Set(object target, string name, object value) => target.GetType().GetField(name, Private).SetValue(target, value);
    static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, Private).Invoke(target, args);
    [MenuItem("Tools/Combat/Validate Parry Stain Rewards")]
    public static void Run()
    {
        Check(!EditorApplication.isPlayingOrWillChangePlaymode, "Use Edit Mode."); Directory.CreateDirectory(Output);
        var report = new StringBuilder(); var active = SceneManager.GetActiveScene(); bool dirty = active.isDirty;
        var scene = EditorSceneManager.NewPreviewScene();
        var oldCombat = CombatManager.Instance; var oldParry = ParrySystem.Instance; var oldUI = AdvancedUIManager.Instance; var oldCamera = CameraManager.Instance;
        float oldScale = Time.timeScale; var oldRandom = UnityEngine.Random.state;
        ParrySystem parry = null; ActionData action = null;
        try
        {
            var root = new GameObject("Isolated Stain rewards"); root.SetActive(false); SceneManager.MoveGameObjectToScene(root, scene);
            GameObject Child(string name) { var child = new GameObject(name); child.transform.SetParent(root.transform); return child; }
            var combat = Child("Combat").AddComponent<CombatManager>(); parry = Child("Parry").AddComponent<ParrySystem>();
            var ui = Child("UI").AddComponent<AdvancedUIManager>();
            CombatManager.Instance = combat; ParrySystem.Instance = parry; AdvancedUIManager.Instance = ui; CameraManager.Instance = null;
            BattleUnit Unit(string name, bool isPlayer)
            { var unit = Child(name).AddComponent<BattleUnit>(); unit.unitName = name; unit.isPlayer = isPlayer; unit.maxHP = 1000; unit.SetPersistentHP(1000); return unit; }
            var enemy = Unit("Enemy", false); var player = Unit("Player", true); var ally = Unit("Ally", true); var dead = Unit("Dead ally", true); dead.SetPersistentHP(0);
            combat.enemyParty.Add(enemy); combat.playerParty.AddRange(new[] { player, ally, dead }); combat.currentActiveUnit = enemy; combat.currentTarget = player; combat.state = CombatState.Executing;
            action = ScriptableObject.CreateInstance<ActionData>(); enemy.defaultAttack = action;
            Set(combat, "selectedAction", action); Set(combat, "animationActionOwner", enemy); Set(combat, "animationActionData", action);
            parry.parrySuccessVfxPrefab = null; parry.parryImpactAudioClip = null;
            root.SetActive(true);
            void Reset(int stains = 0)
            {
                parry.StopAllCoroutines(); Set(parry, "parryFeedbackRoutine", null); Time.timeScale = oldScale; parry.ResetParryState();
                combat.currentStains = stains; combat.maxStains = 4; ui.UpdateStainsUI(stains, 0); AdvancedUIManager.Instance = ui;
                action.isAoE = false; action.isMelee = false; action.vfxType = ActionData.VfxType.None; action.isFriendlyAction = action.isHeal = action.isSummonSkill = false;
                parry.parryStainGain = 1; combat.currentTarget = player;
                Set(player, "<UltimateEnergy>k__BackingField", 0f); Set(ally, "<UltimateEnergy>k__BackingField", 0f);
            }
            int UIStains() => (int)typeof(AdvancedUIManager).GetField("currentStainsForUI", Private).GetValue(ui);
            Reset(1); parry.OpenWindow(); Call(parry, "TriggerParryAction");
            Check(parry.parrySuccessful && combat.currentStains == 2 && UIStains() == 2 && player.UltimateEnergy == player.parryUltimateGain, "A successful timing parry must restore one Stain, update UI and retain Ultimate gain.");
            Call(parry, "TriggerParryAction"); Check(combat.currentStains == 2, "The closed window must not award twice.");
            Reset(); Call(parry, "TriggerParryAction"); Check(!parry.parrySuccessful && combat.currentStains == 0, "Wrong timing must not generate Stain.");
            Reset(4); parry.OpenWindow(); Call(parry, "TriggerParryAction"); Check(combat.currentStains == 4 && UIStains() == 4, "Full Stain must remain capped.");
            Reset(3); parry.parryStainGain = 2; parry.OpenWindow(); Call(parry, "TriggerParryAction"); Check(combat.currentStains == 4, "Configured gain must clamp to max.");
            Reset(); parry.parryStainGain = 0; parry.OpenWindow(); Call(parry, "TriggerParryAction"); Check(parry.parrySuccessful && combat.currentStains == 0 && player.UltimateEnergy > 0, "Zero disables only Stain reward.");
            Reset(); AdvancedUIManager.Instance = null; parry.OpenWindow(); Call(parry, "TriggerParryAction"); Check(combat.currentStains == 1, "Resource gain must survive a temporarily missing UI."); AdvancedUIManager.Instance = ui;
            report.AppendLine("PASS: timing success +1 and immediate UI, wrong/duplicate presses +0, max cap, configurable gain/disable, missing-UI safety and existing Ultimate gain.");

            foreach (var type in new[] { ActionData.VfxType.SpawnAtTarget, ActionData.VfxType.Shoot, ActionData.VfxType.Beam })
            {
                Reset(); action.vfxType = type; action.useVfxParry = true; parry.OpenWindow(); Call(parry, "TriggerParryAction");
                Check(combat.currentStains == 1 && UIStains() == 1, "VFX timing parry needs its reward: " + type);
                if (type != ActionData.VfxType.SpawnAtTarget)
                {
                    var stateType = typeof(CombatManager).GetNestedType("VfxActionState", BindingFlags.NonPublic);
                    var state = Activator.CreateInstance(stateType); stateType.GetField("parried").SetValue(state, true);
                    int hp = player.currentHP; Call(combat, "ResolveVfxImpactDamage", enemy, player, action, state); Call(combat, "ResolveVfxImpactDamage", enemy, player, action, state);
                    Check(combat.currentStains == 1 && player.currentHP == hp, "Impact callbacks must not repeat the resource award or damage a parried target.");
                }
            }
            Reset(); action.isAoE = true; parry.OpenWindow(); Call(parry, "TriggerParryAction");
            Check(parry.parrySuccessful && combat.currentStains == 1 && player.UltimateEnergy > 0 && ally.UltimateEnergy > 0 && dead.UltimateEnergy == 0, "Party timing parry must award one shared Stain rather than one per character.");
            report.AppendLine("PASS: SpawnAtTarget/Shoot/Beam windows, repeated projectile/beam impacts, party AoE awards one shared Stain, living members retain Ultimate gain.");

            var weapon = new GameObject("Enemy hitbox"); weapon.transform.SetParent(enemy.transform); var hitbox = weapon.AddComponent<BoxCollider>(); hitbox.size = Vector3.one; hitbox.isTrigger = true;
            var marker = weapon.AddComponent<EnemyMeleeParryHitbox>(); marker.hitbox = hitbox;
            foreach (var member in new[] { player, ally })
            { var guard = new GameObject("Guard"); guard.transform.SetParent(member.transform); var collider = guard.AddComponent<BoxCollider>(); collider.size = Vector3.one; collider.isTrigger = true; guard.AddComponent<PlayerMeleeParryZone>().guardCollider = collider; }
            void Melee(bool aoe = false) { action.isMelee = action.useMeleeHitboxParry = true; action.isAoE = aoe; action.vfxType = ActionData.VfxType.None; Physics.SyncTransforms(); }
            Reset(); Melee(); Check(!parry.TryResolveMeleeHitboxParry(enemy, player, out _) && combat.currentStains == 0, "Contact without pressing guard cannot generate Stain.");
            Call(parry, "BeginMeleeGuard", combat);
            Check(parry.TryResolveMeleeHitboxParry(enemy, player, out _) && combat.currentStains == 1 && UIStains() == 1, "Real weapon/guard contact must award Stain.");
            Check(!parry.TryResolveMeleeHitboxParry(enemy, player, out _) && combat.currentStains == 1, "Repeated collider contact cannot award again.");
            Reset(); Melee(true); Call(parry, "BeginMeleeGuard", combat);
            Check(parry.TryResolveMeleeHitboxParry(enemy, player, out _) && parry.TryResolveMeleeHitboxParry(enemy, ally, out _) && combat.currentStains == 1, "Melee AoE contacts share one award for the same press.");
            Call(parry, "BeginMeleeGuard", combat); Check(parry.TryResolveMeleeHitboxParry(enemy, player, out _) && combat.currentStains == 2, "Next successful combo guard must award again.");
            Reset(); Melee(); weapon.transform.localPosition = Vector3.right * 10; Physics.SyncTransforms(); Call(parry, "BeginMeleeGuard", combat);
            Check(!parry.TryResolveMeleeHitboxParry(enemy, player, out _) && combat.currentStains == 0, "Guard with no contact must not generate Stain.");
            report.AppendLine("PASS: real melee overlap +1, absent guard/contact +0, duplicate overlap +0, multiple AoE contacts +1 total, next combo guard +1.");
        }
        finally
        {
            if (parry != null) parry.StopAllCoroutines(); Time.timeScale = oldScale; UnityEngine.Random.state = oldRandom;
            CombatManager.Instance = oldCombat; ParrySystem.Instance = oldParry; AdvancedUIManager.Instance = oldUI; CameraManager.Instance = oldCamera;
            EditorSceneManager.ClosePreviewScene(scene); if (action != null) UnityEngine.Object.DestroyImmediate(action);
        }
        var authoredScene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/combattest.unity");
        try
        {
            var authored = authoredScene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<ParrySystem>(true)).ToArray(); Check(authored.Length == 1 && authored[0].parryStainGain == 1, "Authored combat parry must pick up default +1.");
            report.AppendLine("Scene binding: " + AnimationUtility.CalculateTransformPath(authored[0].transform, null) + " > Parry Stain Gain = " + authored[0].parryStainGain);
        }
        finally { EditorSceneManager.ClosePreviewScene(authoredScene); }
        Check(SceneManager.GetActiveScene() == active && active.isDirty == dirty && Time.timeScale == oldScale, "Live scene and time scale must be unchanged.");
        report.AppendLine("PASS: isolated Edit Mode checks; no Play Mode, campaign writes or source scene/prefab changes.");
        File.WriteAllText(Output + "/Validation.txt", report.ToString()); Debug.Log("[Parry Stain] PASS: " + Output + "/Validation.txt");
    }
}

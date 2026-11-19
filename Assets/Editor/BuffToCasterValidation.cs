using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.Build.Player;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class BuffToCasterValidation
{
    const string Request = "Library/CombatValidation/BuffToCaster.request", Output = "Tools/BuffToCaster/Validation.txt";
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static BuffToCasterValidation() { EditorApplication.update += Poll; }
    static void Poll()
    {
        if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || AnimationMode.InAnimationMode()) return;
        File.Delete(Request);
        try { Run(); } catch (Exception error) { Directory.CreateDirectory(Path.GetDirectoryName(Output)); File.WriteAllText(Output, "FAILED\n" + error); Debug.LogException(error); }
    }
    static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    static void Set(object host, string name, object value) => host.GetType().GetField(name, Private).SetValue(host, value);
    static object Call(object host, string name, params object[] values) => host.GetType().GetMethod(name, Private).Invoke(host, values);
    [MenuItem("Tools/Combat/Validate Buff To Caster")]
    public static void Run()
    {
        Check(!EditorApplication.isPlayingOrWillChangePlaymode, "Use Edit Mode.");
        Directory.CreateDirectory(Path.GetDirectoryName(Output)); var report = new StringBuilder();
        var active = SceneManager.GetActiveScene(); bool dirty = active.isDirty;
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive); SceneManager.SetActiveScene(scene);
        var oldCombat = CombatManager.Instance; var oldUI = AdvancedUIManager.Instance; var oldParry = ParrySystem.Instance; var oldCamera = CameraManager.Instance;
        var oldRandom = UnityEngine.Random.state; ActionData action = null; GameObject root = null;
        try
        {
            root = new GameObject("Buff To Caster Fixture");
            GameObject Child(string name) { var go = new GameObject(name); go.transform.SetParent(root.transform); return go; }
            var manager = Child("Combat").AddComponent<CombatManager>(); var ui = Child("UI").AddComponent<AdvancedUIManager>();
            CombatManager.Instance = manager; AdvancedUIManager.Instance = ui; ParrySystem.Instance = null; CameraManager.Instance = null;
            BattleUnit Unit(string name, bool player)
            {
                var unit = Child(name).AddComponent<BattleUnit>(); unit.isPlayer = player; unit.unitName = name;
                unit.maxHP = 1000; unit.baseAtk = 10; unit.baseDef = unit.baseCrit = 0; unit.element = ActionData.ElementType.None;
                unit.SetPersistentHP(1000); return unit;
            }
            var caster = Unit("Caster", true); var ally = Unit("Ally", true); var first = Unit("First enemy", false); var second = Unit("Second enemy", false);
            manager.playerParty.AddRange(new[] { caster, ally }); manager.enemyParty.AddRange(new[] { first, second });
            action = ScriptableObject.CreateInstance<ActionData>(); action.type = ActionData.ActionType.Skill;
            action.damageMultiplier = 1; action.power = 0; action.buffStat = ActionData.BuffStat.Atk; action.buffAmount = 20; action.buffDuration = 3; action.buffToCaster = true;
            var stateType = typeof(CombatManager).GetNestedType("VfxActionState", BindingFlags.NonPublic);
            object Cast(BattleUnit owner, BattleUnit target)
            {
                var state = Activator.CreateInstance(stateType); Set(manager, "vfxActionState", state);
                Set(manager, "animationActionOwner", owner); Set(manager, "animationActionData", action); Set(manager, "acceptingActionAnimationEvents", true);
                ((HashSet<BattleUnit>)typeof(CombatManager).GetField("statusTargetsThisAction", Private).GetValue(manager)).Clear();
                manager.currentActiveUnit = owner; manager.currentTarget = target; manager.state = CombatState.Executing; return state;
            }
            void Reset()
            {
                foreach (var unit in new[] { caster, ally, first, second }) unit.SetPersistentHP(1000);
                action.buffToCaster = true; action.isAoE = action.isHeal = action.isFriendlyAction = action.isSelfOnly = action.statusOnly = false;
                action.vfxType = ActionData.VfxType.None; action.vfxPrefab = null;
            }
            Cast(caster, first); manager.ApplyDamageFromAnimation(caster);
            Check(first.currentHP == 990 && first.activeBuffs.Count == 0 && caster.GetTotalAttack() == 30 && caster.activeBuffs.Count == 1, "First hit must damage the enemy before buffing the caster.");
            manager.ApplyDamageFromAnimation(caster); manager.ApplyDamageFromAnimation(caster);
            Check(first.currentHP == 930 && caster.activeBuffs.Count == 1, "Combo hits must not repeat the self buff.");
            Cast(caster, first); manager.ApplyDamageFromAnimation(caster); Check(caster.activeBuffs.Count == 2, "A new cast can award its own buff.");
            report.AppendLine("PASS: melee damage, buff goes to caster, first hit uses pre-buff ATK, later combo hits do not reapply, next cast gets a new buff.");
            Reset(); action.isAoE = true; Cast(caster, first); manager.ApplyDamageFromAnimation(caster);
            Check(first.currentHP == 990 && second.currentHP == 990 && caster.activeBuffs.Count == 1, "All targets in the first AoE hit must use pre-buff ATK, with one caster buff.");
            Reset(); action.buffToCaster = false; Cast(caster, first); manager.ApplyDamageFromAnimation(caster);
            Check(first.currentHP == 990 && caster.activeBuffs.Count == 0, "Disabled option must preserve existing offensive actions.");
            report.AppendLine("PASS: first AoE hit is consistent across targets; disabled option preserves existing assets.");

            Reset(); action.vfxType = ActionData.VfxType.Shoot; action.vfxPrefab = Child("Projectile prototype"); action.isAoE = true;
            var projectileState = Cast(caster, first);
            var impactA = (Action)Call(manager, "CreateImpactCallback", Child("Projectile A"), caster, first, action);
            var impactB = (Action)Call(manager, "CreateImpactCallback", Child("Projectile B"), caster, second, action);
            impactA(); impactA(); impactB();
            Check(first.currentHP == 990 && second.currentHP == 990 && caster.activeBuffs.Count == 1, "Projectile collisions/AoE must grant one buff and keep volley damage consistent.");
            var newProjectileState = Cast(caster, first);
            Call(manager, "ResolveVfxImpactDamage", caster, second, action, projectileState);
            Check(caster.activeBuffs.Count == 1, "Late impact of an old cast must not use the new cast's buff flag.");
            Call(manager, "ResolveVfxImpactDamage", caster, first, action, newProjectileState); Check(caster.activeBuffs.Count == 2, "New projectile cast gets one separate buff.");
            report.AppendLine("PASS: projectile impacts, duplicate collision callback, AoE volley and delayed callbacks retain per-cast ownership.");

            Reset(); action.vfxType = ActionData.VfxType.Beam; var enemyState = Cast(first, caster);
            stateType.GetField("parried").SetValue(enemyState, true); Call(manager, "ResolveVfxImpactDamage", first, caster, action, enemyState);
            Check(caster.currentHP == 1000 && first.activeBuffs.Count == 0, "A parried hit cannot trigger the enemy's self buff.");
            stateType.GetField("parried").SetValue(enemyState, false); Call(manager, "ResolveVfxImpactDamage", first, caster, action, enemyState);
            Check(caster.currentHP == 990 && first.activeBuffs.Count == 1, "Enemy beam must buff its own caster after an unblocked hit.");
            report.AppendLine("PASS: enemy caster support and parry gate.");

            Reset(); action.isFriendlyAction = action.isHeal = action.isAoE = true;
            caster.SetPersistentHP(900); ally.SetPersistentHP(900); Cast(caster, ally); manager.ApplyDamageFromAnimation(caster);
            Check(caster.currentHP == 910 && ally.currentHP == 910 && caster.activeBuffs.Count == 1 && ally.activeBuffs.Count == 0, "Friendly AoE healing must remain on allies, while the buff goes once to caster.");
            Reset(); action.isFriendlyAction = true; action.buffToCaster = false; Cast(caster, ally); manager.ApplyDamageFromAnimation(caster);
            Check(caster.activeBuffs.Count == 0 && ally.activeBuffs.Count == 1, "Legacy friendly buffs must stay on the selected ally.");
            report.AppendLine("PASS: heal + self buff, friendly AoE and legacy ally-buff behavior.");

            Reset(); action.buffStat = ActionData.BuffStat.Shield; Cast(caster, first); manager.ApplyDamageFromAnimation(caster);
            Check(caster.GetTotalShield() == 20 && first.GetTotalShield() == 0 && first.currentHP == 990, "Damage + self shield.");
            action.buffStat = ActionData.BuffStat.Atk;
            foreach (var language in new[] { GameLanguage.Vietnamese, GameLanguage.English })
            {
                string text = CombatActionDescription.Build(action, language);
                Check(text.Contains(language == GameLanguage.English ? "to the caster" : "cho bản thân") && text.Contains("20") && text.Contains("3"), "Description must show the self buff and its configured values.");
            }
            int before = CombatActionDescription.Revision(action); action.buffToCaster = false;
            Check(before != CombatActionDescription.Revision(action), "Changing the flag must invalidate cached descriptions.");
            var restored = ScriptableObject.CreateInstance<ActionData>(); JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(action), restored);
            Check(!restored.buffToCaster, "Disabled flag serializes without changing defaults."); UnityEngine.Object.DestroyImmediate(restored);
            restored = ScriptableObject.CreateInstance<ActionData>(); action.buffToCaster = true; JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(action), restored);
            Check(restored.buffToCaster, "Enabled flag must survive serialization."); UnityEngine.Object.DestroyImmediate(restored);
            report.AppendLine("PASS: self shield, Vietnamese/English generated descriptions, cache invalidation and serialization.");
        }
        finally
        {
            if (root != null) UnityEngine.Object.DestroyImmediate(root); if (action != null) UnityEngine.Object.DestroyImmediate(action);
            EditorSceneManager.CloseScene(scene, true); if (active.IsValid() && active.isLoaded) SceneManager.SetActiveScene(active);
            CombatManager.Instance = oldCombat; AdvancedUIManager.Instance = oldUI; ParrySystem.Instance = oldParry; CameraManager.Instance = oldCamera;
            UnityEngine.Random.state = oldRandom; Check(active.isDirty == dirty, "Keep the user's scene state.");
        }
        report.AppendLine("Compiling Windows64 Player scripts..."); File.WriteAllText(Output, report.ToString());
        string output = "Library/BuildValidation/BuffToCasterWindows64"; Directory.CreateDirectory(output);
        var result = PlayerBuildInterface.CompilePlayerScripts(new ScriptCompilationSettings { target = BuildTarget.StandaloneWindows64, group = BuildTargetGroup.Standalone, options = ScriptCompilationOptions.None }, output);
        Check(result.assemblies != null && result.assemblies.Count > 0 && File.Exists(output + "/Assembly-CSharp.dll"), "Player script compiler must pass.");
        report.AppendLine("PASS: Windows64 Player scripts compiled. No Play Mode, original ActionData assets, scene data or campaign saves changed.");
        File.WriteAllText(Output, report.ToString()); Debug.Log("Buff To Caster PASS: " + Output);
    }
}

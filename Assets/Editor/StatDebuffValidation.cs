using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.Build.Player;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[InitializeOnLoad]
public static class StatDebuffValidation
{
    const string Request = "Library/CombatValidation/StatDebuff.request";
    const string Output = "Tools/StatDebuff/Validation.txt";
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static StatDebuffValidation() { EditorApplication.update += Poll; }
    static void Poll()
    {
        if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating ||
            EditorApplication.isPlayingOrWillChangePlaymode || AnimationMode.InAnimationMode()) return;
        File.Delete(Request);
        try { Run(); }
        catch (Exception error) { Directory.CreateDirectory(Path.GetDirectoryName(Output)); File.WriteAllText(Output, "FAILED\n" + error); Debug.LogException(error); }
    }
    static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    static void Set(object host, string name, object value) => host.GetType().GetField(name, Private).SetValue(host, value);
    static object Get(object host, string name) => host.GetType().GetField(name, Private).GetValue(host);
    static object Call(object host, string name, params object[] values) => host.GetType().GetMethod(name, Private).Invoke(host, values);

    [MenuItem("Tools/Combat/Validate Negative Stat Debuffs")]
    public static void Run()
    {
        Check(!EditorApplication.isPlayingOrWillChangePlaymode, "Use Edit Mode.");
        Directory.CreateDirectory(Path.GetDirectoryName(Output)); var report = new StringBuilder();
        var original = SceneManager.GetActiveScene(); bool dirty = original.isDirty;
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        var oldCombat = CombatManager.Instance; var oldUI = AdvancedUIManager.Instance;
        var oldParry = ParrySystem.Instance; var oldCamera = CameraManager.Instance;
        var oldRandom = UnityEngine.Random.state;
        GameObject root = null; ActionData action = null, cleanse = null;
        try
        {
            root = new GameObject("Stat Debuff Fixture");
            GameObject Child(string name, bool rect = false)
            {
                var go = rect ? new GameObject(name, typeof(RectTransform)) : new GameObject(name);
                go.transform.SetParent(root.transform); return go;
            }
            var manager = Child("Combat").AddComponent<CombatManager>();
            var ui = Child("UI", true).AddComponent<AdvancedUIManager>();
            ui.buffSprites = new List<AdvancedUIManager.BuffSpriteMapping>();
            CombatManager.Instance = manager; AdvancedUIManager.Instance = ui; ParrySystem.Instance = null; CameraManager.Instance = null;
            BattleUnit Unit(string name, bool player)
            {
                var unit = Child(name).AddComponent<BattleUnit>(); unit.isPlayer = player; unit.unitName = name;
                unit.maxHP = 1000; unit.baseAtk = 20; unit.baseDef = unit.baseCrit = 0;
                unit.element = ActionData.ElementType.None; unit.SetPersistentHP(1000); return unit;
            }
            var caster = Unit("Caster", true); var ally = Unit("Ally", true);
            var first = Unit("Enemy A", false); var second = Unit("Enemy B", false);
            var units = new[] { caster, ally, first, second };
            manager.playerParty.AddRange(new[] { caster, ally }); manager.enemyParty.AddRange(new[] { first, second });
            action = ScriptableObject.CreateInstance<ActionData>(); action.type = ActionData.ActionType.Skill;
            cleanse = ScriptableObject.CreateInstance<ActionData>(); cleanse.isFriendlyAction = cleanse.statusOnly = true;
            var stateType = typeof(CombatManager).GetNestedType("VfxActionState", BindingFlags.NonPublic);
            object Cast(BattleUnit owner, BattleUnit target)
            {
                var state = Activator.CreateInstance(stateType); Set(manager, "vfxActionState", state);
                Set(manager, "animationActionOwner", owner); Set(manager, "animationActionData", action);
                Set(manager, "acceptingActionAnimationEvents", true);
                ((HashSet<BattleUnit>)Get(manager, "statusTargetsThisAction")).Clear();
                manager.currentActiveUnit = owner; manager.currentTarget = target; manager.state = CombatState.Executing;
                return state;
            }
            void Reset()
            {
                foreach (var unit in units) unit.SetPersistentHP(1000);
                action.damageMultiplier = 1; action.power = 0;
                action.buffStat = ActionData.BuffStat.Def; action.buffAmount = -10; action.buffDuration = 3;
                action.buffToCaster = action.isAoE = action.isFriendlyAction = action.isHeal = action.isSelfOnly = action.statusOnly = action.isSummonSkill = false;
                action.vfxType = ActionData.VfxType.None; action.vfxPrefab = null;
                action.cleanse = ActionData.CleanseType.None; action.debuffs = new ActionData.DebuffSettings[0];
            }
            Reset(); Cast(caster, first); manager.ApplyDamageFromAnimation(caster);
            Check(first.currentHP == 980 && first.GetBuffValue(ActionData.BuffStat.Def) == -10 && caster.activeBuffs.Count == 0,
                "First hit must use pre-debuff DEF and place the penalty on the enemy.");
            manager.ApplyDamageFromAnimation(caster);
            Check(first.currentHP == 950 && first.activeBuffs.Count == 1, "Combo applies the stat penalty once per target per cast.");
            Cast(caster, first); manager.ApplyDamageFromAnimation(caster);
            Check(first.activeBuffs.Count == 2 && first.GetBuffValue(ActionData.BuffStat.Def) == -20, "A new cast stacks independently.");
            Reset(); action.isAoE = true; Cast(caster, first); manager.ApplyDamageFromAnimation(caster); manager.ApplyDamageFromAnimation(caster);
            Check(first.currentHP == 950 && second.currentHP == 950 && first.activeBuffs.Count == 1 && second.activeBuffs.Count == 1,
                "AoE must debuff every hit enemy once, including repeated animation events.");
            report.AppendLine("PASS: melee damage ordering, combo once per target, new cast stacking and all AoE targets.");

            Reset(); action.vfxType = ActionData.VfxType.Shoot; action.vfxPrefab = Child("Projectile prototype"); action.isAoE = true;
            var oldState = Cast(caster, first);
            var hitA = (Action)Call(manager, "CreateImpactCallback", Child("Projectile A"), caster, first, action);
            var hitB = (Action)Call(manager, "CreateImpactCallback", Child("Projectile B"), caster, second, action);
            hitA(); hitA(); hitB();
            Check(first.currentHP == 980 && second.currentHP == 980 && first.activeBuffs.Count == 1 && second.activeBuffs.Count == 1,
                "Real projectile callbacks must damage and debuff once even on duplicate collision callbacks.");
            var nextState = Cast(caster, first);
            Call(manager, "ResolveVfxImpactDamage", caster, first, action, nextState);
            Call(manager, "ResolveVfxImpactDamage", caster, first, action, oldState);
            Check(first.activeBuffs.Count == 2, "Late old-cast impacts must not reapply modifiers after a new cast.");
            report.AppendLine("PASS: actual projectile callbacks, AoE, duplicate collisions and delayed old-cast status ownership.");

            Reset(); action.buffStat = ActionData.BuffStat.Atk; action.vfxType = ActionData.VfxType.Beam;
            var enemyState = Cast(first, caster); stateType.GetField("parried").SetValue(enemyState, true);
            Call(manager, "ResolveVfxImpactDamage", first, caster, action, enemyState);
            Check(caster.currentHP == 1000 && caster.activeBuffs.Count == 0, "Parry blocks damage and negative stat modifiers.");
            stateType.GetField("parried").SetValue(enemyState, false);
            Call(manager, "ResolveVfxImpactDamage", first, caster, action, enemyState);
            Check(caster.currentHP == 980 && caster.GetTotalAttack() == 10, "Enemy beam can apply an unblocked ATK penalty.");
            Reset(); action.statusOnly = true; Cast(caster, first); manager.ApplyDamageFromAnimation(caster);
            Check(first.currentHP == 1000 && first.GetBuffValue(ActionData.BuffStat.Def) == -10, "Status-only offensive action can reduce stats without damage.");
            report.AppendLine("PASS: enemy beam, parry gating and status-only target reduction.");

            foreach (var stat in new[] { ActionData.BuffStat.Atk, ActionData.BuffStat.Def, ActionData.BuffStat.Crit, ActionData.BuffStat.CritDamage })
            {
                Reset(); action.buffStat = stat; Cast(caster, first); manager.ApplyDamageFromAnimation(caster);
                Check(first.GetBuffValue(stat) == -10, "All supported stats accept negative amounts: " + stat);
                first.TickBuffs(); first.TickBuffs(); Check(first.GetBuffValue(stat) == -10, "Penalty lasts the configured turns.");
                first.TickBuffs(); Check(first.GetBuffValue(stat) == 0, "Penalty expires and restores the stat.");
            }
            Reset(); action.buffStat = ActionData.BuffStat.Shield; Cast(caster, first); manager.ApplyDamageFromAnimation(caster);
            Check(first.activeBuffs.Count == 0, "A negative target Shield must not create invalid absorption.");
            Reset(); action.buffAmount = 10; Cast(caster, first); manager.ApplyDamageFromAnimation(caster);
            Check(first.activeBuffs.Count == 0, "Positive offensive legacy amounts must not buff enemies.");
            Reset(); action.buffStat = ActionData.BuffStat.Atk; action.buffAmount = 10; action.buffToCaster = true;
            Cast(caster, first); manager.ApplyDamageFromAnimation(caster); manager.ApplyDamageFromAnimation(caster);
            Check(first.activeBuffs.Count == 0 && caster.activeBuffs.Count == 1 && caster.GetTotalAttack() == 30, "Existing self-buff option remains once per cast.");
            Reset(); action.buffAmount = 10; action.isFriendlyAction = true; Cast(caster, ally); manager.ApplyDamageFromAnimation(caster);
            Check(ally.GetBuffValue(ActionData.BuffStat.Def) == 10 && caster.activeBuffs.Count == 0, "Existing friendly buffs remain on the selected ally.");
            report.AppendLine("PASS: ATK/DEF/Crit/CritDamage, duration expiry, safe Shield exclusion and existing positive/self/friendly behavior.");

            Reset();
            void MixedEffects()
            {
                first.SetPersistentHP(1000);
                first.AddBuff(ActionData.BuffStat.Atk, 20, 3); first.AddBuff(ActionData.BuffStat.Shield, 50, 3);
                foreach (var stat in new[] { ActionData.BuffStat.Atk, ActionData.BuffStat.Def, ActionData.BuffStat.Crit, ActionData.BuffStat.CritDamage }) first.AddBuff(stat, -10, 2);
                first.activeDebuffs.Add(new BattleUnit.ActiveDebuff { settings = new ActionData.DebuffSettings { type = ActionData.DebuffType.Burn }, remainingTurns = 2 });
            }
            MixedEffects(); cleanse.cleanse = ActionData.CleanseType.RemoveDebuffs; first.ApplyStatusAction(cleanse);
            Check(first.activeDebuffs.Count == 0 && first.activeBuffs.Count == 2 && first.GetTotalAttack() == 40 && first.GetTotalShield() == 50,
                "RemoveDebuffs removes elemental and every negative stat modifier, preserving positive buffs and shield.");
            MixedEffects(); cleanse.cleanse = ActionData.CleanseType.RemoveBuffs; first.ApplyStatusAction(cleanse);
            Check(first.activeDebuffs.Count == 1 && first.activeBuffs.Count == 4 && first.GetTotalShield() == 0 && first.GetBuffValue(ActionData.BuffStat.Atk) == -10,
                "RemoveBuffs removes only positive buffs/shield and leaves negative stats and elemental debuffs.");
            MixedEffects(); action.cleanse = ActionData.CleanseType.RemoveDebuffs; first.ApplyStatusAction(action);
            Check(first.activeDebuffs.Count == 0 && first.activeBuffs.Count == 3 && first.GetBuffValue(ActionData.BuffStat.Def) == -10,
                "Cleansing happens before applying a new configured negative modifier.");
            report.AppendLine("PASS: cleanse both debuff kinds, preserve buffs/shields, dispel preserves penalties and cleanse-before-new-effect ordering.");

            Reset();
            foreach (var language in new[] { GameLanguage.Vietnamese, GameLanguage.English })
            {
                string text = CombatActionDescription.Build(action, language);
                Check(text.Contains("-10") && text.Contains(language == GameLanguage.English ? "to the target" : "cho mục tiêu"), "Descriptions identify negative value and recipient.");
                cleanse.cleanse = ActionData.CleanseType.RemoveDebuffs;
                Check(CombatActionDescription.Build(cleanse, language).Contains(language == GameLanguage.English ? "negative stat modifiers" : "giảm chỉ số"), "Cleanse description includes negative stats.");
            }
            int revision = CombatActionDescription.Revision(action); action.buffAmount = -15;
            Check(revision != CombatActionDescription.Revision(action), "Inspector changes invalidate description cache.");
            var table = Resources.Load<GameLocalizationTable>("Adventure/LocalizationTable");
            Check(table != null && table.Translate("Giảm phòng thủ", GameLanguage.English) == "Defense Down", "Negative tooltip names translate to English.");

            first.AddBuff(ActionData.BuffStat.Def, 10, 3); first.AddBuff(ActionData.BuffStat.Def, -5, 1); first.AddBuff(ActionData.BuffStat.Def, -5, 1);
            ui.buffIconPrefab = Child("Icon prototype", true); ui.buffIconPrefab.AddComponent<Image>();
            ui.enemyBuffContainer = Child("Enemy effects", true).transform; Set(ui, "boundEnemy", first);
            Call(ui, "RefreshEnemyBuffIcons");
            var enemyIcons = (List<GameObject>)Get(ui, "enemyBuffIcons");
            Check(enemyIcons.Count == 2, "Enemy HUD separates positive and negative modifiers of the same stat.");
            var negativeIcon = enemyIcons.Find(icon => icon.name.StartsWith("Enemy Stat Debuff"));
            Check(negativeIcon != null && negativeIcon.GetComponent<Image>().color == AdvancedUIManager.GetStatModifierColor(-10), "Negative icon is tinted red.");
            var hover = negativeIcon.GetComponent<StatusTooltipHover>();
            Check(((string)Get(hover, "detail")).Contains("-10") && !((string)Get(hover, "detail")).Contains("+-"), "Tooltip shows correct combined negative amount.");
            var hud = new PartyHUDUnit { gameObject = Child("Party card", true), buffContainer = Child("Party effects", true).transform };
            Set(hud, "boundUnit", first); Call(hud, "OnStatsChangedHandler", first.currentHP, first.maxHP, first.GetTotalShield());
            var icons = (List<Image>)Get(hud, "spawnedBuffIcons");
            Check(icons.Count == 2, "Party HUD also separates modifiers by sign.");
            hud.UpdateBuffBlinking(.35f);
            Check(icons[1].color.r == 1f && icons[1].color.g < .5f && Mathf.Approximately(icons[1].color.a, .35f), "Expiring negative effects keep their red tint while blinking.");
            report.AppendLine("PASS: Vietnamese/English descriptions and tooltip names; enemy/party HUD sign grouping, signed tooltip and red expiry blink.");
        }
        finally
        {
            if (root != null) UnityEngine.Object.DestroyImmediate(root);
            if (action != null) UnityEngine.Object.DestroyImmediate(action); if (cleanse != null) UnityEngine.Object.DestroyImmediate(cleanse);
            EditorSceneManager.CloseScene(scene, true);
            if (original.IsValid() && original.isLoaded) SceneManager.SetActiveScene(original);
            CombatManager.Instance = oldCombat; AdvancedUIManager.Instance = oldUI; ParrySystem.Instance = oldParry; CameraManager.Instance = oldCamera;
            UnityEngine.Random.state = oldRandom; Check(original.isDirty == dirty, "Keep the user's scene state.");
        }
        report.AppendLine("Compiling Windows64 Player scripts..."); File.WriteAllText(Output, report.ToString());
        string output = "Library/BuildValidation/StatDebuffWindows64"; Directory.CreateDirectory(output);
        var result = PlayerBuildInterface.CompilePlayerScripts(new ScriptCompilationSettings { target = BuildTarget.StandaloneWindows64, group = BuildTargetGroup.Standalone, options = ScriptCompilationOptions.None }, output);
        Check(result.assemblies != null && result.assemblies.Count > 0 && File.Exists(output + "/Assembly-CSharp.dll"), "Windows64 Player scripts must compile.");
        report.AppendLine("PASS: Windows64 Player script compilation. No Play Mode, source ActionData, user scene or campaign save changes.");
        File.WriteAllText(Output, report.ToString()); Debug.Log("Negative Stat Debuffs PASS: " + Output);
    }
}

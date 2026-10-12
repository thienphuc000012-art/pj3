using System;
using UnityEditor;
using UnityEngine;

public static class DebuffValidation
{
    [MenuItem("Tools/Combat/Validate Debuffs")]
    public static void Run()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Run this check in Edit Mode.");
        var randomState = UnityEngine.Random.state;
        var root = new GameObject("Debuff test (temporary)") { hideFlags = HideFlags.HideAndDontSave };
        var action = ScriptableObject.CreateInstance<ActionData>();
        void Check(bool value, string message) { if (!value) throw new Exception(message); }
        try
        {
            var unit = root.AddComponent<BattleUnit>();
            unit.maxHP = 90; unit.baseAtk = 15;
            unit.SetPersistentHP(90);
            var reportedBurn = new ActionData.DebuffSettings { type = ActionData.DebuffType.Burn, applyChance = 100, turns = 99, burnDamage = 10, burnAttackReductionPercent = 5 };
            action.debuffs = new[] { reportedBurn };
            unit.ApplyStatusAction(action);
            Check(unit.GetTotalAttack() == 14 && Mathf.RoundToInt(unit.GetTotalAttack() + 20) == 34, "Reported 5% burn must not amplify a 15 ATK / 20 power hit.");
            unit.maxHP = 200; unit.baseAtk = 100; unit.baseDef = 999;
            unit.SetPersistentHP(200);
            unit.AddBuff(ActionData.BuffStat.Atk, 50, 10);
            unit.AddBuff(ActionData.BuffStat.Shield, 100, 10);
            var burn = new ActionData.DebuffSettings { type = ActionData.DebuffType.Burn, applyChance = 100, turns = 2, burnDamage = 10, burnAttackReductionPercent = 20 };
            var poison = new ActionData.DebuffSettings { type = ActionData.DebuffType.Poison, applyChance = 100, turns = 2, poisonMaxHpPercent = 5 };
            action.debuffs = new[] { burn, poison };
            unit.ApplyStatusAction(action);
            Check(unit.GetTotalAttack() == 120, "Burn must reduce total ATK including buffs.");
            unit.ApplyStatusAction(action);
            Check(unit.activeDebuffs.Count == 1 && unit.activeDebuffs[0].settings.type == ActionData.DebuffType.Burn, "First successful debuff must win without stacking.");
            unit.TickDebuffsAtTurnEnd();
            Check(unit.currentHP == 190 && unit.GetTotalShield() == 100, "Burn must bypass DEF/shield.");
            unit.ApplyStatusAction(action);
            Check(unit.activeDebuffs[0].remainingTurns == 1, "Reapplication must not refresh duration.");
            unit.TickDebuffsAtTurnEnd();
            Check(unit.currentHP == 180 && unit.activeDebuffs.Count == 0 && unit.GetTotalAttack() == 150, "DOT duration/ATK restoration.");
            action.debuffs = new[] { burn };
            unit.ApplyStatusAction(action);
            poison.applyChance = 0;
            action.debuffs = new[] { poison };
            unit.ApplyStatusAction(action);
            Check(unit.activeDebuffs.Count == 1 && unit.GetTotalAttack() == 120, "Failed poison must preserve burn.");
            poison.applyChance = 100;
            unit.ApplyStatusAction(action);
            Check(unit.activeDebuffs.Count == 1 && unit.activeDebuffs[0].settings.type == ActionData.DebuffType.Burn && unit.GetTotalAttack() == 120, "Even 100% poison must not overwrite active burn.");
            unit.TickDebuffsAtTurnEnd();
            unit.TickDebuffsAtTurnEnd();
            unit.ApplyStatusAction(action);
            Check(unit.activeDebuffs.Count == 1 && unit.activeDebuffs[0].settings.type == ActionData.DebuffType.Poison && unit.GetTotalAttack() == 150, "Poison must apply after burn expires.");
            unit.TickDebuffsAtTurnEnd();
            Check(unit.currentHP == 150, "Poison must deal 5% of max HP, without residual burn damage.");
            unit.TickDebuffsAtTurnEnd();
            burn.applyChance = 0;
            action.debuffs = new[] { burn };
            unit.ApplyStatusAction(action);
            Check(unit.activeDebuffs.Count == 0, "Zero chance applied a debuff.");
            burn.applyChance = 100;
            unit.ApplyStatusAction(action);
            action.debuffs = new ActionData.DebuffSettings[0];
            action.cleanse = ActionData.CleanseType.RemoveDebuffs;
            unit.ApplyStatusAction(action);
            Check(unit.activeDebuffs.Count == 0 && unit.activeBuffs.Count == 2, "Cleanse removed buffs.");
            action.cleanse = ActionData.CleanseType.None; action.debuffs = new[] { burn };
            unit.ApplyStatusAction(action);
            action.debuffs = new ActionData.DebuffSettings[0]; action.cleanse = ActionData.CleanseType.RemoveBuffs;
            unit.ApplyStatusAction(action);
            Check(unit.activeDebuffs.Count == 1 && unit.activeBuffs.Count == 0, "Dispel removed debuffs.");
            unit.SetPersistentHP(200);
            var paralysis = new ActionData.DebuffSettings { type = ActionData.DebuffType.Paralysis, applyChance = 100, turns = 1, paralysisSkipChance = 100 };
            action.cleanse = ActionData.CleanseType.None; action.debuffs = new[] { paralysis };
            unit.ApplyStatusAction(action);
            Check(unit.RollParalysisAtTurnStart(), "100% paralysis did not skip.");
            unit.TickDebuffsAtTurnEnd();
            Check(!unit.RollParalysisAtTurnStart(), "Expired paralysis still skips.");
            unit.SetPersistentHP(5); action.debuffs = new[] { burn };
            unit.ApplyStatusAction(action); unit.TickDebuffsAtTurnEnd();
            Check(unit.IsDead && unit.currentHP == 0, "DOT death must clamp HP to zero.");
            Debug.Log("PASS Debuffs: burn, poison, chance limits, refresh, duration, cleanse/dispel, paralysis and lethal DOT. VFX/turn integration still needs Play Mode.");
        }
        finally
        {
            UnityEngine.Random.state = randomState;
            UnityEngine.Object.DestroyImmediate(root);
            UnityEngine.Object.DestroyImmediate(action);
        }
    }
}

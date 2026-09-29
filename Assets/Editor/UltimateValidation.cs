using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

[InitializeOnLoad]
public static class UltimateValidation
{
    const string Folder = "Library/CombatValidation/";
    static UltimateValidation() { EditorApplication.update += Poll; }
    static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    static void Poll()
    {
        if (!File.Exists(Folder + "Ultimate.request") || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Delete(Folder + "Ultimate.request");
        GameObject obj = null, prefab = null;
        ActionData action = null;
        var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
        try
        {
            obj = new GameObject("Ultimate test");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(obj, scene);
            var unit = obj.AddComponent<BattleUnit>(); unit.isPlayer = true; unit.maxHP = 100; unit.SetPersistentHP(100); unit.baseDef = 0;
            action = ScriptableObject.CreateInstance<ActionData>(); action.type = ActionData.ActionType.Skill; action.ultimateEnergyGain = 25;
            Check(unit.UltimateEnergy == 0, "Gauge must start empty");
            action.isUltimate = true;
            Check(!unit.CommitActionEnergy(action) && unit.UltimateEnergy == 0, "Empty ultimate accepted");
            action.isUltimate = false;
            Check(unit.CommitActionEnergy(action) && unit.UltimateEnergy == 0, "Energy gained before hit");
            unit.GainUltimateFromHit(action); unit.GainUltimateFromHit(action);
            Check(unit.UltimateEnergy == 50, "Combo hits did not charge separately");
            unit.AddUltimateEnergy(50); action.isUltimate = true; unit.CommitActionEnergy(action);
            unit.GainUltimateFromHit(action); Check(unit.UltimateEnergy == 0, "Ultimate recharged itself");
            action.isUltimate = false; unit.GainUltimateFromHit(action);
            action.type = ActionData.ActionType.Item; unit.CommitActionEnergy(action); unit.GainUltimateFromHit(action);
            Check(unit.UltimateEnergy == 25, "Item charged energy");
            unit.TakeDamage(20, false);
            Check(unit.UltimateEnergy == 35, "HP damage charge failed");
            unit.TakeDamage(20, true);
            Check(unit.UltimateEnergy == 35, "Blocked damage charged energy");
            unit.activeBuffs.Add(new ActiveBuff { stat = ActionData.BuffStat.Shield, amount = 30, duration = 1 });
            unit.TakeDamage(20, false);
            Check(unit.UltimateEnergy == 35, "Shield damage charged energy");
            unit.AddUltimateEnergy(unit.parryUltimateGain);
            Check(unit.UltimateEnergy == 50, "Parry amount failed");
            unit.AddUltimateEnergy(200);
            Check(unit.UltimateReady && unit.UltimateEnergy == 100, "Gauge overflow");
            action.type = ActionData.ActionType.Skill; action.isUltimate = true;
            Check(unit.CommitActionEnergy(action) && unit.UltimateEnergy == 0 && !unit.CommitActionEnergy(action), "Ultimate consumption failed");
            unit.SetPersistentHP(0); unit.AddUltimateEnergy(100);
            Check(unit.UltimateEnergy == 0 && !unit.CanUseAction(action), "Dead unit gained/used energy");

            const string path = "Assets/prefab/button/p1.prefab";
            prefab = PrefabUtility.LoadPrefabContents(path);
            var hud = new PartyHUDUnit { gameObject = prefab, hpFill = prefab.transform.Find("Image").GetComponent<Image>(), nameText = prefab.transform.Find("pName").GetComponent<TextMeshProUGUI>() };
            string originalName = hud.nameText.text;
            hud.EnsureUltimateBar(); hud.EnsureUltimateBar();
            Check(hud.ultimateFill != null && prefab.transform.Find("Ultimate Energy") != null, "Missing gauge");
            unit.SetPersistentHP(100); hud.BindUnit(unit); unit.AddUltimateEnergy(50);
            Check(hud.ultimateFill.fillAmount == 0, "Gauge jumped immediately");
            hud.AnimateUltimate(hud.ultimateFillDuration * .5f);
            Check(hud.ultimateFill.fillAmount > 0 && hud.ultimateFill.fillAmount < .5f, "Gauge did not interpolate");
            hud.AnimateUltimate(hud.ultimateFillDuration);
            Check(Mathf.Approximately(hud.ultimateFill.fillAmount, .5f), "HUD binding failed");
            unit.CommitActionEnergy(action); // Still not full: must preserve value.
            Check(Mathf.Approximately(hud.ultimateFill.fillAmount, .5f), "Rejected ultimate changed gauge");
            hud.ultimateFill.fillAmount = 0; hud.nameText.text = originalName;
            if (!File.Exists(Folder + "p1-before-ultimate.prefab")) File.Copy(path, Folder + "p1-before-ultimate.prefab");
            PrefabUtility.SaveAsPrefabAsset(prefab, path);
            File.WriteAllText(Folder + "Ultimate.result.txt", "PASS: no pre-hit energy, multi-hit gain, smooth HUD interpolation, empty/full gating, skill gain, item exclusion, HP/shield/parried damage, parry gain, clamp, ultimate consumption/no self-charge, dead-unit guard, HUD binding; p1 prefab updated.");
        }
        catch (Exception e) { File.WriteAllText(Folder + "Ultimate.result.txt", e.ToString()); }
        finally
        {
            if (prefab != null) PrefabUtility.UnloadPrefabContents(prefab);
            if (action != null) UnityEngine.Object.DestroyImmediate(action);
            UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);
        }
    }
}


using System;
using System.IO;
using System.Reflection;
using System.Text;
using LineworkLite.FreeOutline;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class CombatClickTargetValidation
{
    const string Request = "Library/CombatValidation/ClickTarget.request", Output = "Tools/K9Equipment/ClickTargetValidation.txt";
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static CombatClickTargetValidation() { EditorApplication.update += Poll; }
    static void Poll()
    {
        if (!File.Exists(Request) || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating || AnimationMode.InAnimationMode()) return;
        File.Delete(Request); try { Run(); } catch (Exception error) { Directory.CreateDirectory("Tools/K9Equipment"); File.WriteAllText(Output, "FAILED\n" + error); Debug.LogException(error); }
    }
    static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    static void Set(object host, string name, object value) => host.GetType().GetField(name, Private).SetValue(host, value);
    [MenuItem("Tools/Combat/Validate Clicked Target")]
    public static void Run()
    {
        Check(!EditorApplication.isPlayingOrWillChangePlaymode, "Use Edit Mode.");
        var original = SceneManager.GetActiveScene(); bool dirty = original.isDirty;
        var fixture = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive); SceneManager.SetActiveScene(fixture);
        var oldCombat = CombatManager.Instance; var oldUI = AdvancedUIManager.Instance; var oldCamera = CameraManager.Instance;
        GameObject root = null; ActionData action = null; FreeOutline feature = null; FreeOutlineSettings settings = null;
        try
        {
            root = new GameObject("Click target fixture");
            GameObject Child(string name) { var go = new GameObject(name); go.transform.SetParent(root.transform); return go; }
            var manager = root.AddComponent<CombatManager>(); CombatManager.Instance = manager; AdvancedUIManager.Instance = null; CameraManager.Instance = null;
            BattleUnit Unit(string name, bool player)
            {
                var unit = Child(name).AddComponent<BattleUnit>(); unit.isPlayer = player; unit.unitName = name; unit.maxHP = 100; unit.SetPersistentHP(100); return unit;
            }
            var caster = Unit("Caster", true); var ally = Unit("Ally", true); var enemyA = Unit("Enemy A", false); var enemyB = Unit("Enemy B", false);
            manager.playerParty.AddRange(new[] { caster, ally }); manager.enemyParty.AddRange(new[] { enemyA, enemyB });
            manager.currentActiveUnit = caster; manager.state = CombatState.PlayerTurn; manager.currentStains = 0;
            action = ScriptableObject.CreateInstance<ActionData>(); action.stainChange = -1;
            // Insufficient Stain blocks execution so these checks never start gameplay/coroutines.
            Set(manager, "selectedAction", action); Set(manager, "targetSelectionConfirmed", false);
            settings = ScriptableObject.CreateInstance<FreeOutlineSettings>(); feature = ScriptableObject.CreateInstance<FreeOutline>(); feature.SetSettings(settings);
            var view = root.AddComponent<CombatTargetSelection>(); view.combat = manager; view.outlineFeature = feature;
            manager.currentTarget = enemyA; manager.EnterTargetSelection();
            Check(view.CurrentTarget == enemyA, "Outline must synchronize immediately on target selection.");
            manager.SetTargetUnit(enemyB);
            Check(manager.currentTarget == enemyA && view.CurrentTarget == enemyA && manager.IsChoosingTarget, "Clicking B while A is outlined must not change or confirm the attack target.");
            manager.SelectNextTarget(); Check(manager.currentTarget == enemyB && view.CurrentTarget == enemyB, "A/D cycling changes model and outline together.");
            manager.SetTargetUnit(enemyA); Check(manager.currentTarget == enemyB && view.CurrentTarget == enemyB, "Clicking an outgoing/fading outline cannot switch recipients.");
            manager.SetTargetUnit(enemyB); Check(manager.currentTarget == enemyB, "Matching clicks retain the correct recipient, subject to normal action eligibility.");
            manager.SetTargetUnit(ally); Check(manager.currentTarget == enemyB, "Enemy-target action rejects a player click.");

            action.isFriendlyAction = true; manager.isSelectingBuffTarget = true; manager.currentTarget = caster; manager.EnterTargetSelection();
            manager.SetTargetUnit(ally); Check(manager.currentTarget == caster && view.CurrentTarget == caster, "Buff on highlighted caster must not jump to a clicked ally.");
            manager.SelectNextTarget(); Check(manager.currentTarget == ally && view.CurrentTarget == ally, "Ally targeting cycles with the blue outline.");
            manager.SetTargetUnit(caster); Check(manager.currentTarget == ally && view.CurrentTarget == ally, "Wrong ally click cannot confirm or change buff recipient.");
            manager.SetTargetUnit(enemyA); Check(manager.currentTarget == ally, "Buff rejects enemy clicks.");
            manager.isSelectingSelfOnly = true; manager.currentTarget = caster; manager.EnterTargetSelection();
            manager.SetTargetUnit(ally); manager.SelectNextTarget(); Check(manager.currentTarget == caster && view.CurrentTarget == caster, "Self-only skill cannot change target.");

            manager.isSelectingSelfOnly = manager.isSelectingBuffTarget = false; action.isFriendlyAction = false;
            manager.currentTarget = enemyA; manager.EnterTargetSelection(); enemyB.gameObject.SetActive(false);
            manager.SetTargetUnit(enemyB); Check(manager.currentTarget == enemyA, "Inactive/dead/invalid units cannot be clicked.");
            Set(manager, "selectedAction", null); manager.SetTargetUnit(enemyA); Check(manager.currentTarget == enemyA && !manager.IsChoosingTarget, "Pre-action preferred enemy selection still works.");
            Directory.CreateDirectory("Tools/K9Equipment");
            File.WriteAllText(Output, "PASS: attack and buff reject clicks on a different unit; A/D synchronizes outline immediately; matching clicks keep recipient; self-only/inactive/wrong-team restrictions and pre-action selection preserved. No Play Mode, action execution or save writes.\n");
            Debug.Log("Clicked target PASS: " + Output);
        }
        finally
        {
            if (root != null) UnityEngine.Object.DestroyImmediate(root);
            if (action != null) UnityEngine.Object.DestroyImmediate(action); if (feature != null) UnityEngine.Object.DestroyImmediate(feature); if (settings != null) UnityEngine.Object.DestroyImmediate(settings);
            EditorSceneManager.CloseScene(fixture, true); if (original.IsValid() && original.isLoaded) SceneManager.SetActiveScene(original);
            CombatManager.Instance = oldCombat; AdvancedUIManager.Instance = oldUI; CameraManager.Instance = oldCamera;
            Check(original.isDirty == dirty, "Preserve the user's unsaved scene state.");
        }
    }
}

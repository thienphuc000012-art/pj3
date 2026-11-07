using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Adds references only. Existing Canvas geometry, sprites and typography stay authored.</summary>
public static class SkillInkTransitionSetup
{
    const string ScenePath = "Assets/Scenes/combattest.unity";

    [MenuItem("Tools/Adventure/UI/Setup Skill Ink Transition")]
    public static void Setup()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("Exit Play Mode before setting up the skill ink transition.");
            return;
        }
        var managers = UnityEngine.Object.FindObjectsByType<AdvancedUIManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var manager in managers) Configure(manager);
        if (managers.Length == 0) Debug.LogWarning("Open a combat scene containing AdvancedUIManager first.");
    }

    static void Configure(AdvancedUIManager manager)
    {
        if (manager.subMenuPanel == null) throw new InvalidOperationException("Missing existing SubMenuPanel.");
        var list = manager.skillListUI != null ? manager.skillListUI : manager.subMenuPanel.GetComponent<CombatSkillListUI>();
        if (list == null || list.presentationRoot == null || list.rowsRoot == null)
            throw new InvalidOperationException("Set up the existing Combat Skill List first.");
        Undo.RecordObject(manager, "Connect skill ink transition");
        var controller = manager.GetComponent<SkillPanelTransitionController>();
        if (controller == null) controller = Undo.AddComponent<SkillPanelTransitionController>(manager.gameObject);
        var brush = manager.GetComponent<BrushRevealController>();
        if (brush == null) brush = Undo.AddComponent<BrushRevealController>(manager.gameObject);
        Undo.RecordObject(controller, "Bind skill ink references");
        manager.skillListUI = list;
        manager.skillPanelTransition = controller;
        controller.brushReveal = brush;
        manager.EnsureSkillTransition();
        EditorUtility.SetDirty(controller);
        EditorUtility.SetDirty(manager);
        EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
        Debug.Log("Skill ink transition connected on Canvas. Save the scene. Existing Skills button keeps CombatManager.OnSkillsClicked.", manager);
    }

    // Explicit batch entry only, never an automatic scene migration on import.
    public static void SetupBatch()
    {
        var scene = SceneManager.GetSceneByPath(ScenePath);
        if (scene.isLoaded && scene.isDirty) throw new InvalidOperationException("Save the open combat scene before setup.");
        string backup = "Tools/CanvasUI/BeforeSkillInk-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
        Directory.CreateDirectory(backup);
        File.Copy(ScenePath, backup + "/combattest.unity", false);
        if (!scene.isLoaded) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var manager = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<AdvancedUIManager>(true)).Single();
        Configure(manager);
        if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save skill ink references.");
        Directory.CreateDirectory("Library/CombatValidation");
        File.WriteAllText("Library/CombatValidation/SkillInkSetup.result.txt", "PASS: references saved. Existing UI layout preserved. Backup: " + backup);
    }
}

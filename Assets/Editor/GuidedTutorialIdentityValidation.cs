using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Build.Player;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class GuidedTutorialIdentityValidation
{
    const string Request = "Library/CombatValidation/GuidedTutorialIdentity.request", Output = "Tools/GuidedTutorialIdentity/Validation.txt";
    static bool busy;
    static GuidedTutorialIdentityValidation() { EditorApplication.update += Poll; }
    static void Poll()
    {
        if (busy || !File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || AnimationMode.InAnimationMode()) return;
        File.Delete(Request); busy = true;
        try { Run(); } catch (Exception error) { Directory.CreateDirectory(Path.GetDirectoryName(Output)); File.WriteAllText(Output, "FAILED\n" + error); Debug.LogException(error); }
        finally { busy = false; }
    }
    static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    [MenuItem("Tools/Adventure/Combat/Repair and Validate Guided Tutorial Identity")]
    public static void Run()
    {
        Check(!EditorApplication.isPlayingOrWillChangePlaymode && !AnimationMode.InAnimationMode(), "Use Edit Mode outside animation preview.");
        Directory.CreateDirectory(Path.GetDirectoryName(Output)); var report = new StringBuilder();
        var realConfig = Resources.Load<CampaignConfig>("Adventure/CampaignConfig");
        string previous = realConfig.GuidedTutorialSelectionKey;
        var selected = GuidedTutorialEncounterAuthoring.PrepareConfiguredSelection(realConfig);
        Check(realConfig.GuidedTutorialSelectionKey.StartsWith("guided-tutorial:v2:", StringComparison.Ordinal), "Selection must migrate to a serialized stable ID.");
        report.AppendLine("Previous selection: " + previous); report.AppendLine("Saved selection: " + realConfig.GuidedTutorialSelectionKey);
        if (selected != null) report.AppendLine("Selected enemy: " + selected.gameObject.scene.path + " | " + selected.name + " | " + selected.Id);
        report.AppendLine("PASS: existing selection recovered and saved; original scene/config backed up.");

        var active = SceneManager.GetActiveScene(); bool dirty = active.isDirty;
        Scene fixture = default, built = default;
        string temporary = "Assets/Editor/GuidedTutorialIdentityFixture-" + Guid.NewGuid().ToString("N") + ".unity";
        var config = ScriptableObject.CreateInstance<CampaignConfig>(); CampaignConfig runtimeConfig = null;
        try
        {
            fixture = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(fixture);
            var root = new GameObject("Guided Tutorial Fixture Group");
            var target = new GameObject("Enemy Tutorial").AddComponent<WorldInteraction>(); target.transform.SetParent(root.transform);
            target.kind = WorldInteractionKind.Encounter; target.persistentId = "fixture-shared"; target.savedWorldId = "world:fixture-shared";
            Check(EditorSceneManager.SaveScene(fixture, temporary), "Save isolated fixture.");
            Check(GuidedTutorialEncounterAuthoring.NativeIdentity(target) != null, "Saved scene component must have a persistent object identity.");
            GuidedTutorialEncounterAuthoring.Synchronize(config, fixture, target);
            string id = target.GuidedTutorialEncounterId, key = config.GuidedTutorialSelectionKey;
            Check(config.IsGuidedTutorialEncounter(target), "Selected encounter must match.");
            var editorOnly = new GameObject("Editor-only sibling"); editorOnly.tag = "EditorOnly"; editorOnly.transform.SetSiblingIndex(0);
            var newParent = new GameObject("Guided Tutorial Fixture Parent");
            root.name = "Guided Tutorial Fixture Renamed Group"; target.name = "Renamed tutorial"; target.transform.SetParent(newParent.transform); newParent.transform.SetSiblingIndex(0);
            Check(config.IsGuidedTutorialEncounter(target) && target.GuidedTutorialEncounterId == id, "Rename/reparent/reorder must retain selection.");
            UnityEngine.Object.DestroyImmediate(editorOnly);
            Check(config.IsGuidedTutorialEncounter(target), "Stripping EditorOnly siblings must retain selection.");
            Check(!GuidedTutorialEncounterAuthoring.Synchronize(config, fixture), "Hierarchy edits must not rewrite a stable selection.");
            report.AppendLine("PASS: object/parent renames, reparenting, root reorder and EditorOnly stripping retain the same selected enemy.");

            var copy = UnityEngine.Object.Instantiate(target.gameObject, newParent.transform).GetComponent<WorldInteraction>(); copy.name = "Duplicate encounter";
            GuidedTutorialEncounterAuthoring.Synchronize(config, fixture);
            Check(config.IsGuidedTutorialEncounter(target) && !config.IsGuidedTutorialEncounter(copy) && copy.GuidedTutorialEncounterId != id, "A duplicate must not also become the tutorial encounter.");
            Check(EditorSceneManager.SaveScene(fixture), "Save duplicate identity.");
            GuidedTutorialEncounterAuthoring.Synchronize(config, fixture, copy);
            Check(config.IsGuidedTutorialEncounter(copy) && !config.IsGuidedTutorialEncounter(target), "Selecting another encounter replaces the original.");
            config.SetGuidedTutorialEncounter(null); Check(!config.HasGuidedTutorialEncounter && !config.IsGuidedTutorialEncounter(copy), "Explicit uncheck still works.");
            GuidedTutorialEncounterAuthoring.Synchronize(config, fixture, target);
            Check(EditorSceneManager.SaveScene(fixture), "Save final fixture identity.");
            report.AppendLine("PASS: duplicate enemies with the same world/persistent ID remain distinct; explicit select/reselect/uncheck works.");

            string targetJson = JsonUtility.ToJson(target), configJson = JsonUtility.ToJson(config);
            string finalId = target.GuidedTutorialEncounterId;
            EditorSceneManager.CloseScene(fixture, true); fixture = EditorSceneManager.OpenScene(temporary, OpenSceneMode.Additive);
            var restored = fixture.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<WorldInteraction>(true)).Single(point => point.GuidedTutorialEncounterId == finalId);
            Check(config.IsGuidedTutorialEncounter(restored), "Scene save/reopen must retain selection.");
            built = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(built);
            var builtPoint = new GameObject("Different built hierarchy").AddComponent<WorldInteraction>(); JsonUtility.FromJsonOverwrite(targetJson, builtPoint);
            runtimeConfig = ScriptableObject.CreateInstance<CampaignConfig>(); JsonUtility.FromJsonOverwrite(configJson, runtimeConfig);
            Check(built.path != fixture.path && runtimeConfig.IsGuidedTutorialEncounter(builtPoint), "Serialized Player data must match without the editor scene path or hierarchy.");
            report.AppendLine("PASS: scene reopen and serialized Player-like reconstruction with a different scene path/hierarchy retain the tutorial flag.");

            var legacy = new SerializedObject(config); legacy.FindProperty("guidedTutorialEncounterKey").stringValue = restored.LegacyGuidedTutorialEncounterKey;
            legacy.ApplyModifiedPropertiesWithoutUndo(); restored.transform.parent.SetSiblingIndex(restored.transform.parent.GetSiblingIndex() + 1);
            // Unique world IDs recover old selections after hierarchy changes.
            copy = fixture.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<WorldInteraction>(true)).First(point => point != restored);
            copy.savedWorldId = "world:other"; copy.persistentId = "other";
            Check(GuidedTutorialEncounterAuthoring.Resolve(config, fixture) == restored, "Legacy selections must recover after a root reorder.");
            report.AppendLine("PASS: migration from the old hierarchy-dependent key remains compatible.");
        }
        finally
        {
            if (built.IsValid() && built.isLoaded) EditorSceneManager.CloseScene(built, true);
            if (fixture.IsValid() && fixture.isLoaded) EditorSceneManager.CloseScene(fixture, true);
            UnityEngine.Object.DestroyImmediate(config); if (runtimeConfig != null) UnityEngine.Object.DestroyImmediate(runtimeConfig);
            if (File.Exists(temporary)) AssetDatabase.DeleteAsset(temporary);
            if (active.IsValid() && active.isLoaded) SceneManager.SetActiveScene(active);
            Check(active.isDirty == dirty, "Tests must preserve the user's scene state.");
        }
        report.AppendLine("Compiling Windows64 Player scripts..."); File.WriteAllText(Output, report.ToString());
        string output = "Library/BuildValidation/GuidedTutorialWindows64"; Directory.CreateDirectory(output);
        var result = PlayerBuildInterface.CompilePlayerScripts(new ScriptCompilationSettings { target = BuildTarget.StandaloneWindows64, group = BuildTargetGroup.Standalone, options = ScriptCompilationOptions.None }, output);
        Check(result.assemblies != null && result.assemblies.Count > 0 && File.Exists(output + "/Assembly-CSharp.dll"), "Player script compilation must succeed.");
        report.AppendLine("PASS: Windows64 Player scripts compiled; identity matching has no UnityEditor dependency. No Play Mode or real campaign save writes.");
        File.WriteAllText(Output, report.ToString()); Debug.Log("Guided tutorial identity PASS: " + Output);
    }
}
